import {LitElement, html, css} from 'https://cdn.jsdelivr.net/gh/lit/dist@2/core/lit-core.min.js';
import 'https://code.iconify.design/iconify-icon/1.0.7/iconify-icon.min.js';

export class DarkLightToggle extends LitElement {
    static properties = {
        _theme: 'light'
    }

    constructor() {
        super();
        this.theme = localStorage.getItem("theme");
        if (this.theme === "dark") {
            document.documentElement.setAttribute("data-bs-theme", "dark");
            this._theme = "dark";
        }
    }

    toggleTheme() {
        const current = document.documentElement.getAttribute("data-bs-theme");
        if (current === "dark") {
            document.documentElement.setAttribute("data-bs-theme", "light");
            localStorage.setItem("theme", "light");
            this._theme = "light";
        } else {
            document.documentElement.setAttribute("data-bs-theme", "dark");
            localStorage.setItem("theme", "dark");
            this._theme = "dark";
        }
    }

    static styles = css`
      div {
        margin: 1rem 0;
      }

      input[type=checkbox] {
        opacity: 0;
        position: absolute;
        margin: 0;
      }

      label {
        background-color: rgb(34, 34, 34);
        width: 45px;
        height: 20px;
        border-radius: 40px;
        position: relative;
        padding: 3px;
        cursor: pointer;
        display: flex;
        justify-content: space-between;
        align-items: center;
      }

      div label .ball {
        background-color: rgb(255, 255, 255);
        width: 18px;
        height: 18px;
        position: absolute;
        left: 5px;
        top: 4px;
        border-radius: 50%;
        transition: transform 0.2s linear;
      }

      div input[type=checkbox]:checked + label .ball {
        transform: translateX(25px);
      }
    `;

    render() {
        return html`
            <div @click="${this.toggleTheme}">
                <input type="checkbox" ?checked=${this._theme === 'dark'}>
                <label for="checkbox">
                    <iconify-icon icon="ph:sun-fill" style="color: #f39c12;"></iconify-icon>
                    <iconify-icon icon="ph:moon-fill" style="color: #f1c40f;"></iconify-icon>
                    <span class="ball"></span>
                </label>
            </div>`;
    }
}

customElements.define('fsdocs-dark-light-toggle', DarkLightToggle);
