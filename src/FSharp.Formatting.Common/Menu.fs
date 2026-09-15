/// Internal helpers for building navigation menu HTML from template files
module FSharp.Formatting.Common.Menu

open System
open System.IO
open FSharp.Formatting.Templating

/// A single navigation menu item with its link, display text, hover text and active state
type MenuItem =
    {
        Link: string
        Content: string
        /// Shown on hover, where the display text alone does not say which page the item leads to.
        Title: string option
        IsActive: bool
    }

/// Converts a display string to a snake_case HTML id attribute value
let private snakeCaseRegex =
    System.Text.RegularExpressions.Regex("[A-Z]", System.Text.RegularExpressions.RegexOptions.Compiled)

let private snakeCase (v: string) =
    snakeCaseRegex.Replace(v, "$0").Replace(" ", "_").ToLower()

/// Renders an HTML navigation menu for the given header and items using template files in `input`.
///
/// `substitutions` are the ones the page itself is rendered with, `{{root}}` among them, so a menu
/// template can reach the rest of the site and not only the links it is handed. They go in first, so
/// a menu template cannot lose its own keys to a project that happens to define one of them.
let createMenu
    (input: string)
    (substitutions: Substitutions)
    (isCategoryActive: bool)
    (header: string)
    (items: MenuItem list)
    : string =
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
                [|
                    yield! substitutions
                    ParamKeys.``fsdocs-menu-item-link``, link
                    ParamKeys.``fsdocs-menu-item-content``, title
                    ParamKeys.``fsdocs-menu-item-id``, id
                    ParamKeys.``fsdocs-menu-item-title``,
                    (match model.Title with
                     | Some title -> System.Web.HttpUtility.HtmlEncode title
                     | None -> "")
                    ParamKeys.``fsdocs-menu-item-active-class``, (if model.IsActive then "active" else "")
                |]
                menuItemTemplate)
        |> String.concat "\n"

    SimpleTemplating.ApplySubstitutionsInText
        [|
            yield! substitutions
            ParamKeys.``fsdocs-menu-header-content``, header
            ParamKeys.``fsdocs-menu-header-id``, snakeCase header
            ParamKeys.``fsdocs-menu-header-active-class``, (if isCategoryActive then "active" else "")
            ParamKeys.``fsdocs-menu-items``, menuItems
        |]
        menuTemplate

/// Returns true when both required menu template files exist in `input`
let isTemplatingAvailable (input: string) : bool =
    let pwd = Directory.GetCurrentDirectory()
    let menuTemplate = Path.Combine(pwd, input, "_menu_template.html")
    let menuItemTemplate = Path.Combine(pwd, input, "_menu-item_template.html")
    File.Exists(menuTemplate) && File.Exists(menuItemTemplate)

/// Returns the last-write timestamps of the two menu template files
let getLastWriteTimes (input: string) : DateTime list =
    let pwd = Directory.GetCurrentDirectory()

    let getLastWriteTime f =
        Path.Combine(pwd, input, f) |> File.GetLastWriteTime

    [ getLastWriteTime "_menu_template.html"; getLastWriteTime "_menu-item_template.html" ]
