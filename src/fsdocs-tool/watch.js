var wsUri = "ws://" + window.location.host + "/websocket";
var scrollKey = "fsdocs-scroll:" + window.location.pathname;
// The default template scrolls '<main>', not the window ('html' has 'overflow-y: hidden'); fall
// back to the window's own scrolling element for templates that scroll the page itself.
function scrollContainer() {
    return document.querySelector("main") || document.scrollingElement || document.documentElement;
}
// 'main' itself has a fixed height; its content wrapper is what actually grows, e.g. when
// Mermaid renders diagrams after 'load'.
function scrollContent() {
    return document.querySelector("main #content") || scrollContainer();
}

// Restore the saved scroll position. Idempotent: the first call consumes and clears the
// sessionStorage entry, so a later call (from the 'load' fallback below) is a no-op.
function restoreScroll() {
    const savedScroll = sessionStorage.getItem(scrollKey);
    if (savedScroll === null) return;
    sessionStorage.removeItem(scrollKey);
    const target = parseInt(savedScroll, 10);
    const container = scrollContainer();
    container.scrollTop = target;

    // Content can still grow well after this point: async scripts (e.g. Mermaid, which explicitly
    // waits for 'load' before it even starts fetching) may still be rendering, with no generic
    // browser signal for "nothing more will change" to wait on. So instead of guessing how long
    // that takes, watch the layout itself: keep reapplying the target for as long as it keeps
    // actually resizing, and only stop once it's been quiet for a bit, or the user scrolls on
    // their own. A generous cap is a safety net, not the primary signal, for a page that never
    // truly settles.
    let sawRealResize = false
    let quietTimer = null
    const capTimer = setTimeout(stopWatching, 20000)

    function stopWatching() {
        clearTimeout(quietTimer)
        clearTimeout(capTimer)
        resizeObserver.disconnect()
        container.removeEventListener("wheel", stopWatching)
        container.removeEventListener("touchstart", stopWatching)
    }
    const resizeObserver = new ResizeObserver(() => {
        // ResizeObserver fires once immediately on observe(), before anything has actually
        // changed; that first call isn't a sign that content is still settling.
        if (!sawRealResize) {
            sawRealResize = true
            return
        }
        container.scrollTop = target
        clearTimeout(quietTimer)
        quietTimer = setTimeout(stopWatching, 3000)
    })
    resizeObserver.observe(scrollContent())
    container.addEventListener("wheel", stopWatching, { once: true, passive: true })
    container.addEventListener("touchstart", stopWatching, { once: true, passive: true })
}

// 'pagereveal' fires before the page's first paint, including a cross-document view transition's
// (see the '@view-transition' rule in fsdocs-default.css): restoring there means the transition
// animates in at the right scroll position instead of jumping to it after 'load'. Not all browsers
// support it, so 'load' (via init(), below) is still the fallback.
if ("onpagereveal" in window) {
    window.addEventListener("pagereveal", restoreScroll, { once: true });
}

// Navigation progress bar. The dev server renders a page on first request, which can take a while
// for a big script, and the browser keeps showing the old page until the response arrives with no
// sign that anything is happening. So, when the user follows a link on this site (or a live reload
// starts), show an indeterminate bar along the bottom of the window until the new page paints.
const progressBarId = "fsdocs-watch-progress";

function progressBar() {
    let bar = document.getElementById(progressBarId);
    if (bar) return bar;
    const style = document.createElement("style");
    style.textContent = `
        #${progressBarId} {
            position: fixed; left: 0; right: 0; bottom: 0; height: 3px; z-index: 10000;
            overflow: hidden; background: color-mix(in srgb, var(--primary, #1e8bc3) 25%, transparent);
            pointer-events: none;
        }
        #${progressBarId}::before {
            content: ""; position: absolute; top: 0; bottom: 0; left: 0; width: 30%;
            background: var(--primary, #1e8bc3);
            animation: fsdocs-watch-progress 1.2s ease-in-out infinite;
        }
        @keyframes fsdocs-watch-progress {
            from { transform: translateX(-100%); }
            to { transform: translateX(333%); }
        }
        @media (prefers-reduced-motion: reduce) {
            #${progressBarId}::before { animation-duration: 3s; }
        }`;
    bar = document.createElement("div");
    bar.id = progressBarId;
    bar.setAttribute("role", "progressbar");
    bar.setAttribute("aria-label", "Loading page");
    bar.hidden = true;
    document.head.append(style);
    document.body.append(bar);
    return bar;
}

function showProgress() {
    progressBar().hidden = false;
}

function hideProgress() {
    const bar = document.getElementById(progressBarId);
    if (bar) bar.hidden = true;
}

// A plain left click on a link to another page of this site; anything else is left to the browser.
function isPageNavigation(ev) {
    if (ev.defaultPrevented || ev.button !== 0 || ev.metaKey || ev.ctrlKey || ev.shiftKey || ev.altKey) return false;
    const link = ev.target.closest("a[href]");
    if (!link || link.target && link.target !== "_self" || link.hasAttribute("download")) return false;
    const url = new URL(link.href, location.href);
    if (url.origin !== location.origin) return false;
    // Same document, different fragment: no request is made.
    return url.pathname !== location.pathname || url.search !== location.search;
}

document.addEventListener("click", ev => {
    if (isPageNavigation(ev)) showProgress();
});
// The page stays around when the navigation is cancelled (Escape) or comes back from the
// back/forward cache; the bar must not stay with it.
window.addEventListener("keydown", ev => {
    if (ev.key === "Escape") hideProgress();
});
window.addEventListener("pageshow", hideProgress);

function init()
{
    restoreScroll();
    websocket = new WebSocket(wsUri);
    websocket.onmessage = function(evt) {
        const data = evt.data;
        if (data.endsWith(".css")) {
            console.log(`Trying to reload ${data}`);
            const link = document.querySelector(`link[href*='${data}']`);
            if (link) {
                const href = new URL(link.href);
                const ticks = new Date().getTime();
                href.searchParams.set("v", ticks);
                link.href = href.toString();
            }
        }
        else {
            console.log('closing');
            websocket.close();
            sessionStorage.setItem(scrollKey, scrollContainer().scrollTop);
            showProgress();
            document.location.reload();
        }
    }
}
window.addEventListener("load", init, false);
