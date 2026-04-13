/// Builds a "page content" table-of-contents menu by scanning heading links in rendered HTML
module FSharp.Formatting.Common.PageContentList

open System.Text.RegularExpressions
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

/// Placeholder HTML emitted when a page has no headings to list
[<Literal>]
let EmptyContent = "<div class=\"empty\"></div>"

/// Parses the rendered HTML to extract heading links and builds a nested <ul> table-of-contents.
/// We process the html to collect the table of content.
/// We can't use the doc.MarkdownDocument because we cannot easily get the generated id values.
/// It is safer to parse the html.

// Compiled once at module load; reused across all pages.
let private headingLinkRegex = Regex("<h(\\d)><a [^>]*href=\"([^\"]+)\">([^<]+)</a></h\\d>", RegexOptions.Compiled)

let mkPageContentMenu (html: string) =

    let extractHeadingLinks (matchItem: Match) =
        let level = int matchItem.Groups.[1].Value
        let href = matchItem.Groups.[2].Value
        let linkText = matchItem.Groups.[3].Value

        linkText, li [ Class $"level-%i{level}" ] [ a [ Href href ] [ !!linkText ] ]

    let headingTexts, listItems =
        headingLinkRegex.Matches(html)
        |> Seq.cast<Match>
        |> Seq.map extractHeadingLinks
        |> Seq.toList
        |> List.unzip

    match listItems with
    | [] -> List.empty, EmptyContent
    | items -> headingTexts, string<HtmlElement> (ul [] items)
