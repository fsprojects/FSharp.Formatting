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
///
/// The menu marks the section the reader is in without JavaScript, through CSS scroll-driven
/// animations. Every heading and its menu item get a `data-fsdocs-heading` index, and a `<style>`
/// after the menu wires them up: each heading gets a named view timeline, each menu item animates on
/// the timelines of its own heading and the next one, `body` hoists the names into scope
/// (the menu is a sibling of `main`) and a focused heading (hotkeys `j` / `k`) marks its item.
/// The keyframes, the reading line and the colours live in fsdocs-default.css.

// Compiled once at module load; reused across all pages.
let private headingLinkRegex = Regex("<h(\\d)><a [^>]*href=\"([^\"]+)\">([^<]+)</a></h\\d>", RegexOptions.Compiled)

let private timelineName (index: int) = $"--fsdocs-heading-%i{index}"

let private headingSelector (index: int) = $"[data-fsdocs-heading=\"%i{index}\"]"

/// The scroll timeline of the content scroller, named in fsdocs-default.css.
let private mainTimeline = "--fsdocs-main"

let private scrollSpyStyle (headingCount: int) =
    let scope =
        [ yield! List.map timelineName [ 1..headingCount ]; yield mainTimeline ]
        |> String.concat ", "

    let rules =
        [ 1..headingCount ]
        |> List.map (fun index ->
            let selector = headingSelector index
            // The animations of an item run on the timeline of its own heading, of the next heading
            // and, for the last two items, of the content scroller (reaching the bottom of the page
            // counts as reaching the last heading). fsdocs-default.css assigns the animations.
            let timelines =
                [
                    yield timelineName index
                    if index < headingCount then
                        yield timelineName (index + 1)
                    if index >= headingCount - 1 then
                        yield mainTimeline
                ]
                |> String.concat ", "

            String.concat
                "\n"
                [
                    $"#content {selector} {{ view-timeline-name: {timelineName index}; }}"
                    $"#fsdocs-page-menu {selector} {{ animation-timeline: {timelines}; }}"
                    $"body:has(#content {selector}:is(:focus, .fsdocs-hotkey-focus)) #fsdocs-page-menu {selector} {{ --fsdocs-toc-focused: 1; }}"
                ])
        |> String.concat "\n"

    $"<style>\nbody {{ timeline-scope: {scope}; }}\n{rules}\n</style>"

/// Returns the heading texts (for the search index), the menu HTML and the content HTML with the
/// headings annotated with their index.
let mkPageContentMenu (html: string) =

    let headings = ResizeArray()

    let annotateHeading (matchItem: Match) =
        let level = int matchItem.Groups.[1].Value
        let href = matchItem.Groups.[2].Value
        let linkText = matchItem.Groups.[3].Value
        headings.Add((level, href, linkText))
        // "<hN>" is 4 characters, the rest of the match is kept as is.
        $"""<h%i{level} data-fsdocs-heading="%i{headings.Count}">{matchItem.Value.Substring(4)}"""

    let annotatedHtml = headingLinkRegex.Replace(html, annotateHeading)

    let listItems =
        headings
        |> Seq.mapi (fun i (level, href, linkText) ->
            li
                [ Class $"level-%i{level}"; Custom("data-fsdocs-heading", string<int>(i + 1)) ]
                [ a [ Href href ] [ !!linkText ] ])
        |> Seq.toList

    let headingTexts = headings |> Seq.map (fun (_, _, text) -> text) |> Seq.toList

    match listItems with
    | [] -> List.empty, EmptyContent, html
    | items ->
        let menu = string<HtmlElement>(ul [] items) + scrollSpyStyle headings.Count
        headingTexts, menu, annotatedHtml
