module FSharp.Markdown.Tests.PageContentList

open FsUnit
open NUnit.Framework
open FSharp.Formatting.Common

let private html =
    """<h2><a name="First" class="anchor" href="#First">First</a></h2>
<p>text</p>
<h3><a name="Second" class="anchor" href="#Second">Second</a></h3>
<p>more text</p>"""

[<Test>]
let ``Headings get their index in document order`` () =
    let _, _, annotated = PageContentList.mkPageContentMenu html

    annotated
    |> should contain """<h2 data-fsdocs-heading="1"><a name="First" class="anchor" href="#First">First</a></h2>"""

    annotated
    |> should contain """<h3 data-fsdocs-heading="2"><a name="Second" class="anchor" href="#Second">Second</a></h3>"""

[<Test>]
let ``Menu items carry the index of their heading`` () =
    let _, menu, _ = PageContentList.mkPageContentMenu html

    menu |> should contain """<li class="level-2" data-fsdocs-heading="1">"""
    menu |> should contain """<li class="level-3" data-fsdocs-heading="2">"""

[<Test>]
let ``Menu style names a view timeline per heading and hoists them to body`` () =
    let _, menu, _ = PageContentList.mkPageContentMenu html

    menu
    |> should contain "body { timeline-scope: --fsdocs-heading-1, --fsdocs-heading-2, --fsdocs-main; }"

    menu
    |> should contain """#content [data-fsdocs-heading="1"] { view-timeline-name: --fsdocs-heading-1; }"""

    menu
    |> should contain """#content [data-fsdocs-heading="2"] { view-timeline-name: --fsdocs-heading-2; }"""

[<Test>]
let ``Menu items animate on their own heading, the next one and, at the end, the scroller`` () =
    let threeHeadings = html + """<h2><a name="Third" class="anchor" href="#Third">Third</a></h2>"""

    let _, menu, _ = PageContentList.mkPageContentMenu threeHeadings

    menu
    |> should
        contain
        """#fsdocs-page-menu [data-fsdocs-heading="1"] { animation-timeline: --fsdocs-heading-1, --fsdocs-heading-2; }"""

    // The last two items also watch the scroller: the bottom of the page counts as reaching the last heading.
    menu
    |> should
        contain
        """#fsdocs-page-menu [data-fsdocs-heading="2"] { animation-timeline: --fsdocs-heading-2, --fsdocs-heading-3, --fsdocs-main; }"""

    menu
    |> should
        contain
        """#fsdocs-page-menu [data-fsdocs-heading="3"] { animation-timeline: --fsdocs-heading-3, --fsdocs-main; }"""

[<Test>]
let ``A focused heading marks its menu item`` () =
    let _, menu, _ = PageContentList.mkPageContentMenu html

    menu
    |> should
        contain
        """body:has(#content [data-fsdocs-heading="1"]:is(:focus, .fsdocs-hotkey-focus)) #fsdocs-page-menu [data-fsdocs-heading="1"] { --fsdocs-toc-focused: 1; }"""

[<Test>]
let ``Menu still lists the heading texts and links`` () =
    let texts, menu, _ = PageContentList.mkPageContentMenu html

    texts |> should equal [ "First"; "Second" ]
    menu |> should contain """<a href="#First">"""
    menu |> should contain """<a href="#Second">"""

[<Test>]
let ``A page without headings is left untouched`` () =
    let texts, menu, annotated = PageContentList.mkPageContentMenu "<p>no headings</p>"

    texts |> should be Empty
    menu |> should equal PageContentList.EmptyContent
    annotated |> should equal "<p>no headings</p>"
