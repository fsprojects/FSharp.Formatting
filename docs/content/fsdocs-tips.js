let currentTip = null;
let currentTipElement = null;

function hideTip(evt, name, unique) {
    const el = document.getElementById(name);
    if (el) {
        if (el.hidePopover) {
            try { el.hidePopover(); } catch (_) { }
        } else {
            el.style.display = "none";
        }
    }
    currentTip = null;
    currentTipElement = null;
}

function hideUsingEsc(e) {
    if (currentTipElement) {
        hideTip(e, currentTipElement, currentTip);
    }
}

function showTip(evt, name, unique, owner) {
    document.onkeydown = hideUsingEsc;
    if (currentTip === unique) return;

    // Hide the previously shown tooltip (for non-auto-popover fallback path)
    if (currentTipElement !== null) {
        const prev = document.getElementById(currentTipElement);
        if (prev && !prev.showPopover) {
            prev.style.display = "none";
        }
    }

    currentTip = unique;
    currentTipElement = name;

    const offset = 20;
    let x = evt.clientX;
    let y = evt.clientY + offset;

    const el = document.getElementById(name);
    const maxWidth = document.documentElement.clientWidth - x - 16;
    el.style.maxWidth = `${maxWidth}px`;
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;

    if (el.showPopover) {
        // Popover API path: element is placed in the top layer with fixed positioning
        el.style.position = "fixed";
        el.showPopover();
    } else {
        // Fallback for browsers without Popover API support
        el.style.position = "absolute";
        el.style.display = "block";
    }

    const rect = el.getBoundingClientRect();
    // Move tooltip if it would appear outside the viewport
    if (rect.bottom > window.innerHeight) {
        y = y - el.clientHeight - offset;
        el.style.top = `${y}px`;
    }
    if (rect.right > window.innerWidth) {
        x = x - el.clientWidth - offset;
        el.style.left = `${x}px`;
        el.style.maxWidth = `${document.documentElement.clientWidth - x - 16}px`;
    }
}

function Clipboard_CopyTo(value) {
    if (navigator.clipboard) {
        navigator.clipboard.writeText(value);
    } else {
        const tempInput = document.createElement("input");
        tempInput.value = value;
        document.body.appendChild(tempInput);
        tempInput.select();
        document.execCommand("copy");
        document.body.removeChild(tempInput);
    }
}

window.showTip = showTip;
window.hideTip = hideTip;
// Used by API documentation
window.Clipboard_CopyTo = Clipboard_CopyTo;
