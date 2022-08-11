module FSharp.Formatting.Common.Menu

open System
open System.IO
open FSharp.Formatting.Templating

type MenuItem = { Link: string; Content: string }

let private snakeCase (v: string) =
    System
        .Text
        .RegularExpressions
        .Regex
        .Replace(v, "[A-Z]", "_$0")
        .Replace(" ","")
        .ToLower()

let createMenu (input: string) (header: string) (items: MenuItem list) : string =
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
                   ParamKeys.``fsdocs-menu-item-id``, id |]
                menuItemTemplate)
        |> String.concat "\n"

    SimpleTemplating.ApplySubstitutionsInText
        [| ParamKeys.``fsdocs-menu-header-content``, header
           ParamKeys.``fsdocs-menu-header-id``, snakeCase header
           ParamKeys.``fsdocs-menu-items``, menuItems |]
        menuTemplate

let isTemplatingAvailable (input: string) : bool =
    let pwd = Directory.GetCurrentDirectory()
    let menuTemplate = Path.Combine(pwd, input, "_menu_template.html")
    let menuItemTemplate = Path.Combine(pwd, input, "_menu-item_template.html")
    File.Exists(menuTemplate) && File.Exists(menuItemTemplate)

let getLastWriteTimes (input: string) : DateTime list =
    let pwd = Directory.GetCurrentDirectory()

    let getLastWriteTime f =
        Path.Combine(pwd, input, f) |> File.GetLastWriteTime

    [ getLastWriteTime "_menu_template.html"; getLastWriteTime "_menu-item_template.html" ]
