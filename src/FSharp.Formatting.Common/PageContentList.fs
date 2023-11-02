module FSharp.Formatting.Common.PageContentList

open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

type PageHeaderInfo =
    { Level: int
      Href: string
      Text: string }

[<Literal>]
let EmptyContent = "<div class=\"empty\"></div>"

let mkPageContentMenu (items: PageHeaderInfo list) =
    if List.isEmpty items then
        EmptyContent
    else

        let listItems =
            items
            |> List.map (fun item -> li [ Class $"level-%i{item.Level}" ] [ a [ Href item.Href ] [ !!item.Text ] ])

        let html = ul [] listItems

        html.ToString()
