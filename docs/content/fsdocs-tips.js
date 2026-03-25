let currentTip = null;
let currentTipElement = null;
let hideTimer = null;

function cancelHide() {
    if (hideTimer !== null) {
        clearTimeout(hideTimer);
        hideTimer = null;
    }
}

function hideTip(name) {
    cancelHide();
    const el = document.getElementById(name);
    if (el) {
        try { el.hidePopover(); } catch (_) { }
    }
    currentTip = null;
    currentTipElement = null;
}

// Schedule a hide after a short delay so the mouse can travel from the trigger
// to the tooltip (which may have a positional gap) without the tooltip disappearing.
function scheduleHide(name) {
    cancelHide();
    hideTimer = setTimeout(function () {
        hideTimer = null;
        hideTip(name);
    }, 300);
}

function showTip(evt, name, unique) {
    // Cancel any pending hide so hovering back over the trigger keeps the tooltip open.
    cancelHide();
    if (currentTip === unique) return;

    // Hide the previously shown tooltip before showing the new one
    if (currentTipElement !== null) {
        const prev = document.getElementById(currentTipElement);
        if (prev) {
            try { prev.hidePopover(); } catch (_) { }
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

    try { el.showPopover(); } catch (_) { }

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

// Event delegation: trigger tooltips from data-fsdocs-tip attributes
document.addEventListener('mouseover', function (evt) {
    // Cancel any pending hide when the mouse enters the tooltip itself.
    if (evt.target.closest('div.fsdocs-tip')) {
        cancelHide();
        return;
    }
    const target = evt.target.closest('[data-fsdocs-tip]');
    if (!target) return;
    const name = target.dataset.fsdocsTip;
    const unique = parseInt(target.dataset.fsdocsTipUnique, 10);
    showTip(evt, name, unique);
});

document.addEventListener('mouseout', function (evt) {
    const target = evt.target.closest('[data-fsdocs-tip]');
    if (!target) return;
    // Only hide when the mouse has left the trigger element entirely
    if (target.contains(evt.relatedTarget)) return;
    const name = target.dataset.fsdocsTip;
    // Use a short delay so the mouse can travel across the gap between the trigger
    // and the tooltip without the tooltip disappearing.
    scheduleHide(name);
});

// Hide the tooltip when the mouse leaves it, unless it returns to the trigger element.
document.addEventListener('mouseout', function (evt) {
    const tip = evt.target.closest('div.fsdocs-tip');
    if (!tip) return;
    // Still moving within the tooltip (between child elements) — keep it open.
    if (tip.contains(evt.relatedTarget)) return;
    scheduleHide(tip.id);
});

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

window.Clipboard_CopyTo = Clipboard_CopyTo;
