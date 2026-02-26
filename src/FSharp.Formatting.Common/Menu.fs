module FSharp.Formatting.Common.Menu

open System
open System.IO
open FSharp.Formatting.Templating

/// Represents a single item in a navigation menu.
type MenuItem =
    {
        /// The URL this menu item links to.
        Link: string
        /// The display text for this menu item.
        Content: string
        /// Whether this menu item represents the currently active page.
        IsActive: bool
    }

/// Converts a display string to a snake_case identifier suitable for use as an HTML element id.
let private snakeCase (v: string) =
    System.Text.RegularExpressions.Regex.Replace(v, "[A-Z]", "$0").Replace(" ", "_").ToLower()

/// Renders a navigation menu by applying the project's HTML menu templates.
/// The templates are loaded from <paramref name="input"/>/_menu_template.html and
/// <paramref name="input"/>/_menu-item_template.html.
let createMenu (input: string) (isCategoryActive: bool) (header: string) (items: MenuItem list) : string =
    let pwd = Directory.GetCurrentDirectory()
    let menuTemplate = File.ReadAllText(Path.Combine(pwd, input, "_menu_template.html"))
    let menuItemTemplate = File.ReadAllText(Path.Combine(pwd, input, "_menu-item_template.html"))

    let menuItems =
        items
        |> List.map (fun (model: MenuItem) ->
            let link = model.Link
            let title = System.Web.HttpUtility.HtmlEncode model.Content
            let id = snakeCase title

            SimpleTemplating.ApplySubstitutionsInText
                [| ParamKeys.``fsdocs-menu-item-link``, link
                   ParamKeys.``fsdocs-menu-item-content``, title
                   ParamKeys.``fsdocs-menu-item-id``, id
                   ParamKeys.``fsdocs-menu-item-active-class``, (if model.IsActive then "active" else "") |]
                menuItemTemplate)
        |> String.concat "\n"

    SimpleTemplating.ApplySubstitutionsInText
        [| ParamKeys.``fsdocs-menu-header-content``, header
           ParamKeys.``fsdocs-menu-header-id``, snakeCase header
           ParamKeys.``fsdocs-menu-header-active-class``, (if isCategoryActive then "active" else "")
           ParamKeys.``fsdocs-menu-items``, menuItems |]
        menuTemplate

/// Returns true when both menu HTML templates exist in the given input directory,
/// meaning menu rendering is available.
let isTemplatingAvailable (input: string) : bool =
    let pwd = Directory.GetCurrentDirectory()
    let menuTemplate = Path.Combine(pwd, input, "_menu_template.html")
    let menuItemTemplate = Path.Combine(pwd, input, "_menu-item_template.html")
    File.Exists(menuTemplate) && File.Exists(menuItemTemplate)

/// Returns the last-write times of the two menu template files so callers can detect
/// when the templates have changed and invalidate cached menus.
let getLastWriteTimes (input: string) : DateTime list =
    let pwd = Directory.GetCurrentDirectory()

    let getLastWriteTime f =
        Path.Combine(pwd, input, f) |> File.GetLastWriteTime

    [ getLastWriteTime "_menu_template.html"; getLastWriteTime "_menu-item_template.html" ]
