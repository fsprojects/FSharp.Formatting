module FSharp.Formatting.Common.PageContentList

open System.Text.RegularExpressions
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

[<Literal>]
let EmptyContent = "<div class=\"empty\"></div>"

/// We process the html to collect the table of content.
/// We can't use the doc.MarkdownDocument because we cannot easily get the generated id values.
/// It is safer to parse the html.
let mkPageContentMenu (html: string) =
    let headingLinkPattern = "<h(\\d)><a [^>]*href=\"([^\"]+)\">([^<]+)</a></h\\d>"

    let regex = Regex(headingLinkPattern)

    let extractHeadingLinks (matchItem: Match) =
        let level = int matchItem.Groups.[1].Value
        let href = matchItem.Groups.[2].Value
        let linkText = matchItem.Groups.[3].Value

        linkText, li [ Class $"level-%i{level}" ] [ a [ Href href ] [ !!linkText ] ]

    let headingTexts, listItems =
        regex.Matches(html)
        |> Seq.cast<Match>
        |> Seq.map extractHeadingLinks
        |> Seq.toList
        |> List.unzip

    match listItems with
    | [] -> List.empty, EmptyContent
    | items -> headingTexts, string (ul [] items)
