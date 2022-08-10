module FSharp.Formatting.Common.Menu

open System.IO
open FSharp.Formatting.Templating

type MenuItem = { Link: string; Content: string }

let createMenu (input: string) (header: string) (items: MenuItem list) : string =
    let pwd = Directory.GetCurrentDirectory()
    let menuTemplate = File.ReadAllText(Path.Combine(pwd, input, "_menu_template.html"))
    let menuItemTemplate = File.ReadAllText(Path.Combine(pwd, input, "_menu-item_template.html"))

    let menuItems =
        items
        |> List.map (fun (model: MenuItem) ->
            let link = model.Link
            let title = System.Web.HttpUtility.HtmlEncode model.Content

            SimpleTemplating.ApplySubstitutionsInText
                [| ParamKeys.``fsdocs-menu-item-link-key``, link; ParamKeys.``fsdocs-menu-item-content-key``, title |]
                menuItemTemplate)
        |> String.concat "\n"

    SimpleTemplating.ApplySubstitutionsInText
        [| ParamKeys.``fsdocs-menu-header-content-key``, header; ParamKeys.``fsdocs-menu-items-key``, menuItems |]
        menuTemplate

let isTemplatingAvailable (input: string) : bool =
    let pwd = Directory.GetCurrentDirectory()
    let menuTemplate = Path.Combine(pwd, input, "_menu_template.html")
    let menuItemTemplate = Path.Combine(pwd, input, "_menu-item_template.html")
    File.Exists(menuTemplate) && File.Exists(menuItemTemplate)
