// Automatically scroll to the active aside menu item.
const activeItem = document.querySelector("aside .nav-item.active");
const mainMenu = document.getElementById('fsdocs-main-menu');
if (activeItem && mainMenu) {
    const halfMainMenuHeight = mainMenu.offsetHeight / 2
    if(activeItem.offsetTop > halfMainMenuHeight){
        mainMenu.scrollTop = (activeItem.offsetTop - halfMainMenuHeight) - (activeItem.offsetHeight / 2);
    }
}

if(location.hash) {
    const header = document.querySelector(`a[href='${location.hash}']`);
    header.scrollIntoView({ behavior: 'instant'});
}

