module FSharp.Formatting.Menu

open System.IO
open FSharp.Formatting.Templating

type MenuItem =
    {
        Link: string
        Content: string
    }
let fsdocsMenuHeaderContentKey = ParamKey "fsdocs-menu-header-content"
let fsdocsMenuItemsKey = ParamKey "fsdocs-menu-items"
let fsdocsMenuItemLinkKey = ParamKey "fsdocs-menu-item-link"
let fsdocsMenuItemContentKey = ParamKey "fsdocs-menu-item-content"
let pwd = Directory.GetCurrentDirectory()
let createMenu (input: string) (header : string) (items : MenuItem list) : string =
     let menuTemplate = Path.Combine(pwd, input, "_menu_template.html") //repeated lets, dont know yet how to avoid these :(
     let menuItemTemplate = Path.Combine(pwd, input, "_menu-item_template.html")
     let menuItems =
         items
         |> List.map (fun (model: MenuItem) ->
             let link = model.Link
             let title = System.Web.HttpUtility.HtmlEncode model.Content

             SimpleTemplating.ApplySubstitutionsInText
                 [| fsdocsMenuItemLinkKey, link; fsdocsMenuItemContentKey, title |]
                 menuItemTemplate)
         |> String.concat "\n"

     SimpleTemplating.ApplySubstitutionsInText
         [| fsdocsMenuHeaderContentKey, header; fsdocsMenuItemsKey, menuItems |]
         menuTemplate

let isTemplatingAvailable (input:string) : bool =
     let menuTemplate = Path.Combine(pwd, input, "_menu_template.html")
     let menuItemTemplate = Path.Combine(pwd, input, "_menu-item_template.html")
     File.Exists(menuTemplate) && File.Exists(menuItemTemplate)
