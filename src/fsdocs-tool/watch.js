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
            document.location.reload();
        }
    }
}
window.addEventListener("load", init, false);
