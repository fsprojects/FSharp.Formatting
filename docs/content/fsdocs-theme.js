// Automatically scroll to the active aside menu item.
const activeItem = document.querySelector("aside .nav-item.active");
if (activeItem) {
    activeItem.scrollIntoView();
}