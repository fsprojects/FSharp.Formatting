#if INTERACTIVE
#r "../../bin/FSharp.Formatting.Markdown.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
[<NUnit.Framework.TestFixture>]
module FSharp.Markdown.Tests.Parsing
#endif

open FsUnit
open NUnit.Framework
open FSharp.Formatting.Markdown
open FSharp.Formatting.Common
open FsUnitTyped

let properNewLines (text: string) =
    text.Replace("\r\n", "\n").Replace("\n", System.Environment.NewLine)

let shouldEqualNoWhiteSpace (x: string) (y: string) = shouldEqual (x.Split()) (y.Split())

[<Test>]
let ``Don't double encode HTML entities outside of code`` () =
    "a &gt; & &copy; b"
    |> Markdown.ToHtml
    |> should contain "<p>a &gt; &amp; &copy; b</p>"

[<Test>]
let ``Escape HTML entities inside of code`` () =
    "`a &gt; & b`"
    |> Markdown.ToHtml
    |> should contain "<p><code>a &amp;gt; &amp; b</code></p>"

[<Test>]
let ``Inline HTML tag containing 'at' is not turned into hyperlink`` () =
    let doc = """<a href="mailto:a@b.c">hi</a>""" |> Markdown.Parse

    doc.Paragraphs
    |> shouldEqual
        [ Paragraph(
              [ Literal(
                    """<a href="mailto:a@b.c">hi</a>""",
                    { StartLine = 1
                      StartColumn = 0
                      EndLine = 1
                      EndColumn = 29 }
                ) ],
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 29 }
          ) ]

// --------------------------------------------------------------------------------------
// Emoji in Markdown → HTML (Issue #964)
// These tests verify the full FSX → HTML path for emoji characters.
// Emoji should be preserved as-is in HTML output (raw UTF-8).
// --------------------------------------------------------------------------------------

// Supplementary plane emoji (U+1F389, stored as surrogate pair in UTF-16)
let emojiParty = "\U0001F389" // 🎉 PARTY POPPER
let emojiRocket = "\U0001F680" // 🚀 ROCKET
let emojiConstruction = "\U0001F6A7" // 🚧 CONSTRUCTION SIGN
// Basic multilingual plane emoji (single UTF-16 code unit)
let emojiStar = "\u2B50" // ⭐ WHITE MEDIUM STAR
let emojiCheck = "\u2705" // ✅ WHITE HEAVY CHECK MARK
// Emoji with variation selector (two code points)
let emojiWarning = "\u26A0\uFE0F" // ⚠️ WARNING SIGN + VS-16
// ZWJ sequence (multiple code points joined)
let emojiFamily = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466" // 👨‍👩‍👧‍👦

[<Test>]
let ``Supplementary plane emoji (surrogate pair) are preserved in paragraph`` () =
    let html = sprintf "Like this %s and %s" emojiParty emojiRocket |> Markdown.ToHtml
    html |> should contain emojiParty
    html |> should contain emojiRocket

[<Test>]
let ``BMP emoji (single code unit) are preserved in paragraph`` () =
    let html = sprintf "Stars %s and checks %s" emojiStar emojiCheck |> Markdown.ToHtml
    html |> should contain emojiStar
    html |> should contain emojiCheck

[<Test>]
let ``Emoji with variation selector are preserved`` () =
    let html = sprintf "Warning %s sign" emojiWarning |> Markdown.ToHtml
    html |> should contain emojiWarning

[<Test>]
let ``ZWJ emoji sequences are preserved`` () =
    let html = sprintf "Family %s emoji" emojiFamily |> Markdown.ToHtml
    html |> should contain emojiFamily

[<Test>]
let ``Emoji are preserved in headings`` () =
    let html =
        sprintf "# Heading %s\n\n## Subheading %s" emojiParty emojiRocket
        |> Markdown.ToHtml

    html |> should contain emojiParty
    html |> should contain emojiRocket

[<Test>]
let ``Emoji are preserved in bold and italic spans`` () =
    let html = sprintf "**Bold %s** and _italic %s_" emojiParty emojiStar |> Markdown.ToHtml
    html |> should contain emojiParty
    html |> should contain emojiStar

[<Test>]
let ``Emoji are preserved in list items`` () =
    let html =
        sprintf "- Item %s\n- Item %s\n- Item %s" emojiParty emojiStar emojiCheck
        |> Markdown.ToHtml

    html |> should contain emojiParty
    html |> should contain emojiStar
    html |> should contain emojiCheck

[<Test>]
let ``Emoji are preserved in link text`` () =
    let html = sprintf "[Link %s](http://example.com)" emojiParty |> Markdown.ToHtml
    html |> should contain emojiParty

[<Test>]
let ``Emoji are preserved in inline code`` () =
    let html = sprintf "Code `%s emoji`" emojiParty |> Markdown.ToHtml
    html |> should contain emojiParty

[<Test>]
let ``Emoji do not break HTML escaping of & < > characters`` () =
    let html = sprintf "A &amp; %s and &lt;tag&gt;" emojiParty |> Markdown.ToHtml
    html |> should contain "&amp;"
    html |> should contain "&lt;"
    html |> should contain "&gt;"
    html |> should contain emojiParty

[<Test>]
let ``Multiple emoji types together are all preserved`` () =
    let text = sprintf "%s%s%s%s%s" emojiParty emojiConstruction emojiStar emojiWarning emojiCheck
    let html = text |> Markdown.ToHtml
    html |> should contain emojiParty
    html |> should contain emojiConstruction
    html |> should contain emojiStar
    html |> should contain emojiWarning
    html |> should contain emojiCheck

[<Test>]
let ``Emoji at start and end of paragraph are preserved`` () =
    let html = sprintf "%s Start and End %s" emojiParty emojiRocket |> Markdown.ToHtml
    html |> should contain emojiParty
    html |> should contain emojiRocket

[<Test>]
let ``Emoji are preserved in fenced code block`` () =
    let md = sprintf "```\nlet emoji = \"%s\"\n```" emojiParty
    let html = md |> Markdown.ToHtml
    html |> should contain emojiParty

// End emoji tests

[<Test>]
let ``Encode '<' and '>' characters as HTML entities`` () =
    let doc = "foo\n\n - a --> b" |> Markdown.ToHtml
    doc |> should contain "&gt;"
    let doc = "foo\n\n - a <-- b" |> Markdown.ToHtml
    doc |> should contain "&lt;"

[<Test>]
let ``Headings ending with F# are parsed correctly`` () =
    let doc =
        """
## Hello F#
Some more"""
        |> Markdown.Parse

    doc.Paragraphs
    |> shouldEqual
        [ Heading(
              2,
              [ Literal(
                    "Hello F#",
                    { StartLine = 2
                      StartColumn = 3
                      EndLine = 2
                      EndColumn = 11 }
                ) ],
              { StartLine = 2
                StartColumn = 0
                EndLine = 2
                EndColumn = 11 }
          )
          Paragraph(
              [ Literal(
                    "Some more",
                    { StartLine = 3
                      StartColumn = 0
                      EndLine = 3
                      EndColumn = 9 }
                ) ],
              { StartLine = 3
                StartColumn = 0
                EndLine = 3
                EndColumn = 9 }
          ) ]

[<Test>]
let ``Headings ending with spaces followed by # are parsed correctly`` () =
    let doc =
        """
## Hello ####
Some more"""
        |> Markdown.Parse

    doc.Paragraphs
    |> shouldEqual
        [ Heading(
              2,
              [ Literal(
                    "Hello",
                    { StartLine = 2
                      StartColumn = 3
                      EndLine = 2
                      EndColumn = 8 }
                ) ],
              { StartLine = 2
                StartColumn = 0
                EndLine = 2
                EndColumn = 13 }
          )
          Paragraph(
              [ Literal(
                    "Some more",
                    { StartLine = 3
                      StartColumn = 0
                      EndLine = 3
                      EndColumn = 9 }
                ) ],
              { StartLine = 3
                StartColumn = 0
                EndLine = 3
                EndColumn = 9 }
          ) ]

[<Test>]
let ``Should be able to create nested list item with two paragraphs`` () =
    let doc =
        """
- a
  - b

    c"""
        |> Markdown.Parse

    let expectedBody =
        [ Paragraph(
              [ Literal(
                    "b",
                    { StartLine = 3
                      StartColumn = 4
                      EndLine = 3
                      EndColumn = 5 }
                ) ],
              { StartLine = 3
                StartColumn = 4
                EndLine = 3
                EndColumn = 5 }
          )
          Paragraph(
              [ Literal(
                    "c",
                    { StartLine = 5
                      StartColumn = 4
                      EndLine = 5
                      EndColumn = 5 }
                ) ],
              { StartLine = 5
                StartColumn = 4
                EndLine = 5
                EndColumn = 5 }
          ) ]

    match doc.Paragraphs.Head with
    | ListBlock(Unordered,
                [ [ Span([ Literal("a",
                                   { StartLine = 2
                                     StartColumn = 2
                                     EndLine = 2
                                     EndColumn = 3 }) ],
                         _)
                    ListBlock(Unordered, [ body ], _) ] ],
                _) -> body |> shouldEqual expectedBody
    | _ -> Assert.Fail "Expected list block with a nested list block"

[<Test>]
let ``Can escape special characters such as "*" in emphasis`` () =
    let doc = """*foo\*\*bar*""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ Emphasis(
                  [ Literal(
                        "foo**bar",
                        { StartLine = 1
                          StartColumn = 0
                          EndLine = 1
                          EndColumn = 8 }
                    ) ],
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 12 }
              ) ],
            { StartLine = 1
              StartColumn = 0
              EndLine = 1
              EndColumn = 12 }
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Can escape special characters in LaTex inline math`` () =
    let doc = """test \$ is: $foo\$\$bar<>\$\&\%\$\#\_\{\}$""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ Literal(
                  "test $ is: ",
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 11 }
              )
              LatexInlineMath(
                  "foo\$\$bar<>\$\&\%\$\#\_\{\}",
                  { StartLine = 1
                    StartColumn = 12
                    EndLine = 1
                    EndColumn = 40 }
              ) ],
            { StartLine = 1
              StartColumn = 0
              EndLine = 1
              EndColumn = 42 }
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Test special character _ in LaTex inline math`` () =
    let doc = """$\bigcap_{x \in A} p_{x}A$""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ LatexInlineMath(
                  "\\bigcap_{x \\in A} p_{x}A",
                  { StartLine = 1
                    StartColumn = 1
                    EndLine = 1
                    EndColumn = 25 }
              ) ],
            { StartLine = 1
              StartColumn = 0
              EndLine = 1
              EndColumn = 26 }
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Inline code can contain backticks when wrapped with spaces`` () =
    let doc = """` ``h`` `""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ InlineCode(
                  "``h``",
                  { StartLine = 1
                    StartColumn = 2
                    EndLine = 1
                    EndColumn = 7 }
              ) ],
            { StartLine = 1
              StartColumn = 0
              EndLine = 1
              EndColumn = 9 }
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Transform bold text correctly`` () =
    let doc = "This is **bold**. This is also __bold__."

    let expected =
        "<p>This is <strong>bold</strong>. This is also <strong>bold</strong>.</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform LaTex inline math correctly`` () =
    let doc = """test \$ is: $foo\$\$bar<>\$\&\%\$\#\_\{\}$."""

    let expected = """<p>test $ is: <span class="math">\(foo\$\$bar&lt;&gt;\$\&amp;\%\$\#\_\{\}\)</span>.</p>"""

    (Markdown.ToHtml doc).Trim() |> shouldEqual expected

[<Test>]
let ``Transform LaTex block correctly`` () =
    let doc =
        """$$$
foo\$\$bar<>\$\&\%\$\#\_\{\}
foo\$\$bar<>\$\&\%\$\#\_\{\}"""

    let expected =
        properNewLines
            """<p><span class="math">\[foo\$\$bar&lt;&gt;\$\&amp;\%\$\#\_\{\}
foo\$\$bar&lt;&gt;\$\&amp;\%\$\#\_\{\}\]</span></p>"""

    (Markdown.ToHtml doc).Trim() |> shouldEqual expected

[<Test>]
let ``Transform italic text correctly`` () =
    let doc = "This is *italic*. This is also _italic_."

    let expected =
        "<p>This is <em>italic</em>. This is also <em>italic</em>.</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected


[<Test>]
let ``Transform hyperlinks correctly`` () =
    let doc = "This is [a link][1].\r\n\r\n  [1]: http://www.example.com"

    let expected =
        "<p>This is <a href=\"http://www.example.com\">a link</a>.</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform bracketed hyperlinks correctly`` () =
    let doc = "Have you visited <http://www.example.com> before?"

    let expected =
        "<p>Have you visited <a href=\"http://www.example.com\">http://www.example.com</a> before?</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform auto hyperlinks correctly`` () =
    let doc = "Have you visited http://www.example.com or https://www.example.com before?"

    let expected =
        "<p>Have you visited <a href=\"http://www.example.com\">http://www.example.com</a> or <a href=\"https://www.example.com\">https://www.example.com</a> before?</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform auto hyperlinks at end of line correctly`` () =
    let doc = "Improved CLI documentation - https://github.com/fsharp/FAKE/pull/472"

    let expected =
        "<p>Improved CLI documentation - <a href=\"https://github.com/fsharp/FAKE/pull/472\">https://github.com/fsharp/FAKE/pull/472</a></p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform auto hyperlinks at start of line correctly`` () =
    let doc = "https://github.com/fsharp/FAKE/pull/472 shows we improved CLI documentation"

    let expected =
        "<p><a href=\"https://github.com/fsharp/FAKE/pull/472\">https://github.com/fsharp/FAKE/pull/472</a> shows we improved CLI documentation</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform alternative links correctly`` () =
    let doc = "Have you visited [example](http://www.example.com) before?"

    let expected =
        "<p>Have you visited <a href=\"http://www.example.com\">example</a> before?</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform images correctly`` () =
    let doc = "An image goes here: ![alt text][1]\r\n\r\n  [1]: http://www.google.com/intl/en_ALL/images/logo.gif"

    let expected =
        "<p>An image goes here: <img src=\"http://www.google.com/intl/en_ALL/images/logo.gif\" alt=\"alt text\" /></p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform blockquotes correctly`` () =
    let doc = "Here is a quote\r\n\r\n> Sample blockquote\r\n"

    let expected =
        "<p>Here is a quote</p>\r\n<blockquote>\r\n<p>Sample blockquote</p>\r\n</blockquote>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform numbered lists correctly`` () =
    let doc = "A numbered list:\r\n\n1. a\n2. b\n3. c\r\n"

    let expected =
        "<p>A numbered list:</p>\r\n<ol>\r\n<li>a</li>\r\n<li>b</li>\r\n<li>c</li>\r\n</ol>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform bulleted lists correctly`` () =
    let doc = "A bulleted list:\r\n\r\n- a\r\n- b\r\n- c\r\n"

    let expected =
        "<p>A bulleted list:</p>\r\n<ul>\r\n<li>a</li>\r\n<li>b</li>\r\n<li>c</li>\r\n</ul>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``ATX heading without space after hash is treated as paragraph`` () =
    let doc = "#Header 1\nHeader 1\r\n========"

    let expected = "<p>#Header 1</p>\r\n<h1>Header 1</h1>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform header 2 correctly`` () =
    let doc = "##Header 2\nHeader 2\r\n--------"

    let expected = "<p>##Header 2</p>\r\n<h2>Header 2</h2>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``ATX heading with space after hash is transformed to heading element`` () =
    let doc = "# Header 1\n## Header 2"

    let expected = "<h1>Header 1</h1>\r\n<h2>Header 2</h2>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform code blocks in list correctly`` () =
    let doc = "- code sample:\r\n\r\n\r\n    let x = 1\r\n"

    let expected =
        "<ul>\r\n<li>code sample:</li>\r\n</ul>\r\n<pre><code>let x = 1\r\n</code></pre>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform code blocks correctly`` () =
    let doc = "code sample:\r\n\r\n    <head>\r\n    <title>page title</title>\r\n    </head>\r\n"

    let expected =
        "<p>code sample:</p>\r\n<pre><code>&lt;head&gt;\r\n&lt;title&gt;page title&lt;/title&gt;\r\n&lt;/head&gt;\r\n</code></pre>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform github flavored code blocks correctly`` () =
    let doc = "code sample:\r\n\r\n```\r\n<head>\r\n<title>page title</title>\r\n</head>\r\n```\r\n"

    let expected =
        "<p>code sample:</p>\r\n<pre><code>&lt;head&gt;\r\n&lt;title&gt;page title&lt;/title&gt;\r\n&lt;/head&gt;\r\n</code></pre>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform github flavored language code blocks correctly`` () =
    let doc = "code sample:\r\n\r\n```markup\r\n<head>\r\n<title>page title</title>\r\n</head>\r\n```\r\n"

    let expected =
        "<p>code sample:</p>\r\n<pre><code class=\"language-markup\">&lt;head&gt;\r\n&lt;title&gt;page title&lt;/title&gt;\r\n&lt;/head&gt;\r\n</code></pre>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform github flavored code blocks with whitespace correctly`` () =
    let doc = "```\r\n    test\r\n    blub\r\n```\r\n"

    let expected = "<pre><code>    test\r\n    blub\r\n</code></pre>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Fenced code blocks do not require newline`` () =
    let doc = "> This is an annnotation\r\n> ```vb\r\n> Module\r\n> ```"

    let actual = Markdown.ToHtml(doc)
    actual |> should contain "<pre"

[<Test>]
let ``Transform code spans correctly`` () =
    let doc = "HTML contains the `<blink>` tag"

    let expected = "<p>HTML contains the <code>&lt;blink&gt;</code> tag</p>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform display math correctly`` () =
    let doc = """$$\bigcap_{x \in A} p_{x}A$$"""
    let res = Markdown.ToHtml doc
    let res = res.TrimEnd([| '\r'; '\n' |])

    res
    |> shouldEqual "<p><span class=\"math\">\\[\\bigcap_{x \\in A} p_{x}A\\]</span></p>"

[<Test>]
let ``Transform HTML passthrough correctly`` () =
    let doc = "<div>\r\nHello World!\r\n</div>\r\n"

    let expected = "<div>\r\nHello World!\r\n</div>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform escaped characters correctly`` () =
    let doc = @"\`foo\`"
    let expected = "<p>`foo`</p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform horizontal rules correctly`` () =
    let doc = "* * *\r\n\r\n***\r\n\r\n*****\r\n\r\n- - -\r\n\r\n---------------------------------------\r\n\r\n"

    let expected = "<hr />\r\n<hr />\r\n<hr />\r\n<hr />\r\n<hr />\r\n" |> properNewLines

    Markdown.Parse(doc).Paragraphs
    |> shouldEqual
        [ HorizontalRule(
              '*',
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 5 }
          )
          HorizontalRule(
              '*',
              { StartLine = 3
                StartColumn = 0
                EndLine = 3
                EndColumn = 3 }
          )
          HorizontalRule(
              '*',
              { StartLine = 5
                StartColumn = 0
                EndLine = 5
                EndColumn = 5 }
          )
          HorizontalRule(
              '-',
              { StartLine = 7
                StartColumn = 0
                EndLine = 7
                EndColumn = 5 }
          )
          HorizontalRule(
              '-',
              { StartLine = 9
                StartColumn = 0
                EndLine = 9
                EndColumn = 39 }
          ) ]

    Markdown.ToHtml doc |> shouldEqual expected

let ``Transform tables with delimiters in code or math correctly`` () =
    let doc =
        """| a | b |
|---|---|
| 1 |$|$|
|`|`|123|
| 3 | 4
---------
"""

    let expected =
        """
          <table>
<thead>
<tr class="header">
<th><p>a</p></th>
<th><p>b</p></th>
</tr>
</thead>
<tbody>
<tr class="odd">
<td><p>1</p></td>
<td><p><span class="math">\(|\)</span></p></td>
</tr>
<tr class="even">
<td><p><code>|</code></p></td>
<td><p>123</p></td>
</tr>
<tr class="odd">
<td><p>3</p></td>
<td><p>4</p></td>
</tr>
</tbody>
</table>     """
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqualNoWhiteSpace expected

[<Test>]
let ``Parse empty blockquote followed by content`` () =
    let doc =
        ">
a"

    let expected =
        [ QuotedBlock(
              [],
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 1 }
          )
          Paragraph(
              [ Literal(
                    "a",
                    { StartLine = 2
                      StartColumn = 0
                      EndLine = 2
                      EndColumn = 1 }
                ) ],
              { StartLine = 2
                StartColumn = 0
                EndLine = 2
                EndColumn = 1 }
          ) ]

    (Markdown.Parse doc).Paragraphs |> shouldEqual expected

[<Test>]
let ``Parse blockquote teriminated by empty blockquote line and followed by content`` () =
    let doc =
        ">a
>
a"

    let expected =
        [ QuotedBlock(
              [ Paragraph(
                    [ Literal(
                          "a",
                          { StartLine = 1
                            StartColumn = 1
                            EndLine = 1
                            EndColumn = 2 }
                      ) ],
                    { StartLine = 1
                      StartColumn = 1
                      EndLine = 1
                      EndColumn = 2 }
                ) ],
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 2 }
          )
          Paragraph(
              [ Literal(
                    "a",
                    { StartLine = 3
                      StartColumn = 0
                      EndLine = 3
                      EndColumn = 1 }
                ) ],
              { StartLine = 3
                StartColumn = 0
                EndLine = 3
                EndColumn = 1 }
          ) ]

    (Markdown.Parse doc).Paragraphs |> shouldEqual expected

[<Test>]
let ``Parse blockquote with three leading spaces`` () =
    let doc = "   >a"

    let expected =
        [ QuotedBlock(
              [ Paragraph(
                    [ Literal(
                          "a",
                          { StartLine = 1
                            StartColumn = 4
                            EndLine = 1
                            EndColumn = 5 }
                      ) ],
                    { StartLine = 1
                      StartColumn = 4
                      EndLine = 1
                      EndColumn = 5 }
                ) ],
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 5 }
          ) ]

    (Markdown.Parse doc).Paragraphs |> shouldEqual expected

[<Test>]
let ``Underscore inside italic is preserved`` () =
    let doc = "_fsharp_space_after_comma_"

    let expected =
        [ Paragraph(
              [ Emphasis(
                    [ Literal(
                          "fsharp_space_after_comma",
                          { StartLine = 1
                            StartColumn = 0
                            EndLine = 1
                            EndColumn = 24 }
                      ) ],
                    { StartLine = 1
                      StartColumn = 0
                      EndLine = 1
                      EndColumn = 26 }
                ) ],
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 26 }
          ) ]

    (Markdown.Parse doc).Paragraphs |> shouldEqual expected

[<Test>]
let ``Underscores inside word in heading`` () =
    let doc =
        """
### fsharp_bar_before_discriminated_union_declaration

Always use a bar before every case in the declaration of a discriminated union.
"""

    let expected =
        [ Heading(
              3,
              [ Literal(
                    "fsharp_bar_before_discriminated_union_declaration",
                    { StartLine = 2
                      StartColumn = 4
                      EndLine = 2
                      EndColumn = 53 }
                ) ],
              { StartLine = 2
                StartColumn = 0
                EndLine = 2
                EndColumn = 53 }
          )
          Paragraph(
              [ Literal(
                    "Always use a bar before every case in the declaration of a discriminated union.",
                    { StartLine = 4
                      StartColumn = 0
                      EndLine = 4
                      EndColumn = 79 }
                ) ],
              { StartLine = 4
                StartColumn = 0
                EndLine = 4
                EndColumn = 79 }
          ) ]

    (Markdown.Parse doc).Paragraphs |> shouldEqual expected

[<Test>]
let ``Underscore inside italic and bold near punctuation is preserved`` () =
    let doc = "This is **bold_bold**, and this _italic_; and _this_too_: again."

    let expected =
        "<p>This is <strong>bold_bold</strong>, and this <em>italic</em>; and <em>this_too</em>: again.</p>\r\n"
        |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``emphasis with space`` () =
    let doc = "*foo bar*"
    let actual = "<p><em>foo bar</em></p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``No emphasis if opening * is followed by whitespace`` () =
    let doc = "a * foo bar*"
    let actual = "<p>a * foo bar*</p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``No emphasis if opening * is preceded by alphanumeric and followed by punctuation`` () =
    let doc = """a*"foo"*"""
    let actual = """<p>a*"foo"*</p>""" + "\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``Intraword emphasis with * is permitted`` () =
    let doc = "foo*bar*"
    let actual = "<p>foo<em>bar</em></p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

    let doc2 = "5*6*78"
    let actual2 = "<p>5<em>6</em>78</p>\r\n" |> properNewLines
    Markdown.ToHtml doc2 |> shouldEqual actual2

[<Test>]
let ``emphasis using _ with space`` () =
    let doc = "_foo bar_"
    let actual = "<p><em>foo bar</em></p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``No emphasis if opening _ is followed by whitespace`` () =
    let doc = "_ foo bar_"
    let actual = "<p>_ foo bar_</p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``No emphasis if opening _ is preceded by alphanumeric and followed by punctuation`` () =
    let doc = """a_"foo"_"""
    let actual = """<p>a_"foo"_</p>""" + "\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``Intraword emphasis with _ is not permitted`` () =
    let doc = "foo_bar_"
    let actual = "<p>foo_bar_</p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

    let doc2 = "5_6_78"
    let actual2 = "<p>5_6_78</p>\r\n" |> properNewLines
    Markdown.ToHtml doc2 |> shouldEqual actual2

    let doc3 = "пристаням_стремятся_"
    let actual3 = "<p>пристаням_стремятся_</p>\r\n" |> properNewLines
    Markdown.ToHtml doc3 |> shouldEqual actual3

[<Test>]
let ``No emphasis if first _ is right flanking and second is left flanking`` () =
    let doc = """aa_"bb"_cc"""
    let actual = """<p>aa_"bb"_cc</p>""" + "\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``Emphasis if first _ is left and right flanking and preceded by punctuation`` () =
    let doc = "foo-_(bar)_"
    let actual = "<p>foo-<em>(bar)</em></p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``No emphasis if open and close delim do not match`` () =
    let doc = "_foo*"
    let actual = "<p>_foo*</p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

[<Test>]
let ``No emphasis if closing * is preceded by whitespace`` () =
    let doc1 = "*foo bar *"
    let actual1 = "<p>*foo bar *</p>\r\n" |> properNewLines
    Markdown.ToHtml doc1 |> shouldEqual actual1

    let doc2 =
        "*foo bar
*"

    let actual2 =
        "<p>*foo bar
*</p>"
        + "\r\n"
        |> properNewLines

    Markdown.ToHtml doc2 |> shouldEqual actual2

[<Test>]
let ``Do not close emphasis if second * is preceded by punctuation and followed by alphanumeric`` () =
    let doc = "*(*foo)"
    let actual = "<p>*(*foo)</p>\r\n" |> properNewLines
    Markdown.ToHtml doc |> shouldEqual actual

    let doc2 = "*(*foo*)*"
    let actual2 = "<p><em>(<em>foo</em>)</em></p>\r\n" |> properNewLines
    Markdown.ToHtml doc2 |> shouldEqual actual2

[<Test>]
let ``Replace relative markdown file in anchor href attribute`` () =
    let doc = "<a href=\"./other-file.md\" target=\"_blank\">my link</a>"
    let mdlinkResolver _ = Some "./other-file.html"

    let actual =
        "<a href=\"./other-file.html\" target=\"_blank\">my link</a>\r\n"
        |> properNewLines

    Markdown.ToHtml(doc, mdlinkResolver = mdlinkResolver) |> shouldEqual actual

[<Test>]
let ``Replace relative markdown file in multiple anchors`` () =
    let doc = "<a href=\"./other-file.md\">link one</a><a href=\"./other-file.md\">link two</a>"
    let mdlinkResolver _ = Some "./other-file.html"

    let actual =
        "<a href=\"./other-file.html\">link one</a><a href=\"./other-file.html\">link two</a>\r\n"
        |> properNewLines

    Markdown.ToHtml(doc, mdlinkResolver = mdlinkResolver) |> shouldEqual actual

[<Test>]
let ``Replace relative markdown file in multiple attributes`` () =
    let doc = "<a b=\"./other-file.md\" c=\"./other-file.md\">d</a>"
    let mdlinkResolver _ = Some "./other-file.html"
    let actual = "<a b=\"./other-file.html\" c=\"./other-file.html\">d</a>\r\n" |> properNewLines
    Markdown.ToHtml(doc, mdlinkResolver = mdlinkResolver) |> shouldEqual actual

[<Test>]
let ``Replace relative markdown file in custom attribute`` () =
    let doc = "<x-web-component data-attribute=\"./other-file.md\"></x-web-component>"
    let mdlinkResolver _ = Some "./other-file.html"

    let actual =
        "<x-web-component data-attribute=\"./other-file.html\"></x-web-component>\r\n"
        |> properNewLines

    Markdown.ToHtml(doc, mdlinkResolver = mdlinkResolver) |> shouldEqual actual

// --------------------------------------------------------------------------------------
// Span range correctness tests (Issue #744)
// These tests verify that column positions are tracked correctly across span types.
// --------------------------------------------------------------------------------------

[<Test>]
let ``Indirect link and subsequent literal have correct column ranges`` () =
    //                   1         2
    //         0123456789012345678901234567
    let doc = "Before [indirectLink] After" |> Markdown.Parse

    doc.Paragraphs
    |> shouldEqual
        [ Paragraph(
              [ Literal(
                    "Before ",
                    { StartLine = 1
                      StartColumn = 0
                      EndLine = 1
                      EndColumn = 7 }
                )
                IndirectLink(
                    [ Literal(
                          "indirectLink",
                          { StartLine = 1
                            StartColumn = 8
                            EndLine = 1
                            EndColumn = 20 }
                      ) ],
                    "",
                    "indirectLink",
                    { StartLine = 1
                      StartColumn = 7
                      EndLine = 1
                      EndColumn = 21 }
                )
                Literal(
                    " After",
                    { StartLine = 1
                      StartColumn = 21
                      EndLine = 1
                      EndColumn = 27 }
                ) ],
              { StartLine = 1
                StartColumn = 0
                EndLine = 1
                EndColumn = 27 }
          ) ]

[<Test>]
let ``Direct link and subsequent literal have correct column ranges`` () =
    //                   1         2         3
    //         0123456789012345678901234567890123
    let doc = "Before [link](http://x.com) After" |> Markdown.Parse

    match doc.Paragraphs with
    | [ Paragraph([ Literal("Before ", litRange1)
                    DirectLink(_, "http://x.com", _, linkRange)
                    Literal(" After", litRange2) ],
                  _) ] ->
        litRange1
        |> shouldEqual (
            { StartLine = 1
              StartColumn = 0
              EndLine = 1
              EndColumn = 7 }
        )

        linkRange
        |> shouldEqual (
            { StartLine = 1
              StartColumn = 7
              EndLine = 1
              EndColumn = 27 }
        )

        litRange2
        |> shouldEqual (
            { StartLine = 1
              StartColumn = 27
              EndLine = 1
              EndColumn = 33 }
        )
    | _ -> Assert.Fail "Expected paragraph with literal + direct link + literal"

[<Test>]
let ``Inline code and subsequent literal have correct column ranges`` () =
    //         0123456789012
    let doc = "foo `bar` baz" |> Markdown.Parse

    match doc.Paragraphs with
    | [ Paragraph([ Literal("foo ", litRange1); InlineCode("bar", codeRange); Literal(" baz", litRange2) ], _) ] ->
        litRange1
        |> shouldEqual (
            { StartLine = 1
              StartColumn = 0
              EndLine = 1
              EndColumn = 4 }
        )

        codeRange
        |> shouldEqual (
            { StartLine = 1
              StartColumn = 5
              EndLine = 1
              EndColumn = 8 }
        )

        litRange2
        |> shouldEqual (
            { StartLine = 1
              StartColumn = 9
              EndLine = 1
              EndColumn = 13 }
        )
    | _ -> Assert.Fail "Expected paragraph with literal + inline code + literal"

    let doc = "<pre link=\"valid link though.md\">content</pre>"
    let mdlinkResolver _ = failwith "should not be reached!"
    let actual = "<pre link=\"valid link though.md\">content</pre>\r\n" |> properNewLines
    Markdown.ToHtml(doc, mdlinkResolver = mdlinkResolver) |> shouldEqual actual

[<Test>]
let ``Don't replace links in generated code block in table`` () =
    let doc = "<table class=\"pre\" link=\"valid link though.md\">content</table>"
    let mdlinkResolver _ = failwith "should not be reached!"

    let actual =
        "<table class=\"pre\" link=\"valid link though.md\">content</table>\r\n"
        |> properNewLines

    Markdown.ToHtml(doc, mdlinkResolver = mdlinkResolver) |> shouldEqual actual

[<Test>]
let ``Paragraph between sublists should not be absorbed into first sublist item (issue 347)`` () =
    // Per CommonMark, a paragraph indented at the outer list item's continuation level
    // should remain a sibling of the surrounding sublists, not be absorbed into the
    // first sublist item's body.
    let html =
        "1.  List item\n\n    1. Subone\n\n    Paragraph\n\n    7. SubRestart\n\n5.  Another list item\n"
        |> Markdown.ToHtml

    // The paragraph must appear between the two sublists, not inside the first.
    html |> should contain "<p>Paragraph</p>"

    // There must be two separate ordered sub-lists.
    let firstSublistEnd = html.IndexOf("</ol>")
    let paragraphPos = html.IndexOf("<p>Paragraph</p>")
    let secondSublistStart = html.LastIndexOf("<ol>")

    // Paragraph comes after first sublist ends.
    paragraphPos |> should be (greaterThan firstSublistEnd)
    // Second sublist starts after paragraph.
    secondSublistStart |> should be (greaterThan paragraphPos)

// -----------------------------------------------------------------------
// ToMd serialisation tests
// These verify the round-trip Markdown-to-Markdown serialiser.  Prior to
// this test file there was zero coverage of Markdown.ToMd.
// -----------------------------------------------------------------------

/// Round-trip helper: parse markdown, serialise back with ToMd (Unix newlines
/// so comparisons are platform-independent), then strip trailing whitespace.
let toMd (input: string) =
    Markdown.Parse(input)
    |> (fun doc -> Markdown.ToMd(doc, newline = "\n"))
    |> (fun s -> s.TrimEnd())

[<Test>]
let ``ToMd preserves a plain paragraph`` () =
    "Hello, world." |> toMd |> should contain "Hello, world."

[<Test>]
let ``ToMd preserves a level-1 heading`` () =
    "# Heading One" |> toMd |> shouldEqual "# Heading One"

[<Test>]
let ``ToMd preserves a level-2 heading`` () =
    "## Heading Two" |> toMd |> shouldEqual "## Heading Two"

[<Test>]
let ``ToMd preserves a level-3 heading`` () =
    "### Heading Three" |> toMd |> shouldEqual "### Heading Three"

[<Test>]
let ``ToMd preserves strong (bold) text`` () =
    "**bold**" |> toMd |> should contain "**bold**"

[<Test>]
let ``ToMd preserves inline code`` () =
    "Use `printf` here." |> toMd |> should contain "`printf`"

[<Test>]
let ``ToMd round-trips inline code containing a single backtick`` () =
    // "a`b" must be serialised with a double-backtick fence so it re-parses correctly.
    let original = "`` a`b ``"
    let md = Markdown.Parse original
    let result = Markdown.ToMd md
    // The serialised form must round-trip: re-parsing must yield the same InlineCode body.
    let reparsed = Markdown.Parse result

    match reparsed.Paragraphs with
    | [ Paragraph([ InlineCode("a`b", _) ], _) ] -> ()
    | _ -> Assert.Fail(sprintf "Expected InlineCode(\"a`b\") after round-trip, got: %A" reparsed.Paragraphs)

[<Test>]
let ``ToMd round-trips inline code containing multiple backticks`` () =
    // Body "``h``" contains double backticks — needs a triple-backtick fence.
    let original = "` ``h`` `"
    let md = Markdown.Parse original
    let result = Markdown.ToMd md

    match (Markdown.Parse result).Paragraphs with
    | [ Paragraph([ InlineCode("``h``", _) ], _) ] -> ()
    | _ -> Assert.Fail(sprintf "Expected InlineCode(\"``h``\") after round-trip, got: %A" result)

[<Test>]
let ``ToMd preserves a direct link`` () =
    "[FSharp](https://fsharp.org)"
    |> toMd
    |> should contain "[FSharp](https://fsharp.org)"

[<Test>]
let ``ToMd preserves a direct link with title`` () =
    let md = "[FSharp](https://fsharp.org \"F# language\")"
    let result = toMd md
    result |> should contain "[FSharp]("
    result |> should contain "https://fsharp.org"
    result |> should contain "\"F# language\""

[<Test>]
let ``ToMd preserves a direct link without title unchanged`` () =
    let result = "[link](http://example.com)" |> toMd
    result |> should contain "[link](http://example.com)"
    result |> should not' (contain "\"")

[<Test>]
let ``ToMd preserves a direct image`` () =
    "![alt text](image.png)" |> toMd |> should contain "![alt text](image.png)"

[<Test>]
let ``ToMd preserves a direct image with title`` () =
    let md = "![photo](image.png \"My Photo\")"
    let result = toMd md
    result |> should contain "![photo]("
    result |> should contain "image.png"
    result |> should contain "\"My Photo\""

[<Test>]
let ``ToMd preserves an unordered list`` () =
    let md = "* apple\n* banana\n* cherry"
    let result = toMd md
    result |> should contain "apple"
    result |> should contain "banana"
    result |> should contain "cherry"

[<Test>]
let ``ToMd preserves emphasis (italic) text`` () =
    // Emphasis must serialise as *...* not **...** (bold)
    "*italic*" |> toMd |> should contain "*italic*"

[<Test>]
let ``ToMd preserves emphasis distinct from strong`` () =
    let result = "**bold** and *italic*" |> toMd
    result |> should contain "**bold**"
    // Emphasis must not be rendered with double asterisks
    result |> should not' (contain "**italic**")
    result |> should contain "*italic*"

[<Test>]
let ``ToMd preserves an ordered list with correct numbering`` () =
    let result = "1. first\n2. second\n3. third" |> toMd
    result |> should contain "1. first"
    result |> should contain "2. second"
    result |> should contain "3. third"

[<Test>]
let ``ToMd ordered list does not use zero-based numbering`` () =
    // Before fix: ordered list items were prefixed "0 ", "1 ", "2 " (0-indexed, no period)
    let result = "1. only item" |> toMd
    result |> should not' (contain "0 only item")
    result |> should contain "1. only item"

[<Test>]
let ``ToMd preserves a fenced code block`` () =
    let md = "```fsharp\nlet x = 1\n```"
    let result = toMd md
    result |> should contain "let x = 1"
    result |> should contain "```"

[<Test>]
let ``ToMd preserves a blockquote`` () =
    let md = "> This is a quote."
    let result = toMd md
    result |> should contain "> "
    result |> should contain "This is a quote."

[<Test>]
let ``ToMd preserves a horizontal rule`` () =
    let md = "Before\n\n---\n\nAfter"
    let result = toMd md
    result |> should contain "Before"
    result |> should contain "---"
    result |> should contain "After"

[<Test>]
let ``ToMd preserves LaTeX inline math`` () =
    let md = "Einstein's $E = mc^2$ equation."
    let result = toMd md
    result |> should contain "$E = mc^2$"

[<Test>]
let ``ToMd preserves inline HTML block`` () =
    let md = "<div class=\"note\">Note content</div>"
    let result = toMd md
    result |> should contain "<div"
    result |> should contain "Note content"

[<Test>]
let ``ToMd handles a document with heading and paragraph`` () =
    let md = "# Title\n\nBody text here."
    let result = toMd md
    result |> should contain "# Title"
    result |> should contain "Body text here."

[<Test>]
let ``ToMd handles a table`` () =
    let md = "Col1 | Col2\n:--- | :---\nA | B"
    let result = toMd md
    result |> should contain "Col1"
    result |> should contain "Col2"
    result |> should contain "A"
    result |> should contain "B"

[<Test>]
let ``ToMd handles empty document`` () = "" |> toMd |> shouldEqual ""

[<Test>]
let ``ToMd preserves an indirect link when key is not resolved`` () =
    // Indirect link whose reference definition is present — should round-trip
    let md = "[link text][ref]\n\n[ref]: https://example.com"
    let result = toMd md
    // ToMd resolves the indirect link to a direct link form
    result |> should contain "[link text]"
    result |> should contain "https://example.com"

// --------------------------------------------------------------------------------------
// ToMd round-trip: indirect images (issue - failwith "tbd - IndirectImage")
// --------------------------------------------------------------------------------------

[<Test>]
let ``ToMd round-trip: indirect image with resolved reference`` () =
    // Markdown with a reference-style image and a reference definition
    let input = "![alt text][img-id]\n\n[img-id]: http://example.com/image.png \"Title\""
    let doc = Markdown.Parse(input)
    // Should NOT throw (was failing with failwith "tbd - IndirectImage")
    let result = Markdown.ToMd(doc)
    // When key is resolved, should render as a direct image
    result |> should contain "![alt text]("
    result |> should contain "http://example.com/image.png"

[<Test>]
let ``ToMd round-trip: indirect image with unresolved reference`` () =
    // Parse just the image token without a reference definition
    let input = "![alt text][unknown-ref]"
    let doc = Markdown.Parse(input)
    // Should NOT throw (was failing with failwith "tbd - IndirectImage")
    let result = Markdown.ToMd(doc)
    // When key is not resolved, should preserve the indirect form
    result |> should contain "![alt text][unknown-ref]"

// --------------------------------------------------------------------------------------
// ToMd additional coverage: headings, nested structures, LaTeX display math, inline code
// --------------------------------------------------------------------------------------

[<Test>]
let ``ToMd preserves heading level 4`` () =
    "#### Heading Four" |> toMd |> shouldEqual "#### Heading Four"

[<Test>]
let ``ToMd preserves heading level 5`` () =
    "##### Heading Five" |> toMd |> shouldEqual "##### Heading Five"

[<Test>]
let ``ToMd preserves heading level 6`` () =
    "###### Heading Six" |> toMd |> shouldEqual "###### Heading Six"

[<Test>]
let ``ToMd preserves emphasis inside a heading`` () =
    let result = "## Hello *world*" |> toMd
    result |> should contain "## Hello *world*"

[<Test>]
let ``ToMd preserves strong text inside a heading`` () =
    let result = "## Hello **world**" |> toMd
    result |> should contain "## Hello **world**"

[<Test>]
let ``ToMd preserves LaTeX display math`` () =
    let md = "$$E = mc^2$$"
    let result = toMd md
    result |> should contain "$$E = mc^2$$"

[<Test>]
let ``ToMd preserves inline code containing special chars`` () =
    let md = "Use `a + b = c` inline."
    let result = toMd md
    result |> should contain "`a + b = c`"

[<Test>]
let ``ToMd preserves nested unordered list`` () =
    // Outer list item containing an inner list
    let md = "* outer\n\n  * inner"
    let result = toMd md
    result |> should contain "outer"
    result |> should contain "inner"

[<Test>]
let ``ToMd preserves a nested blockquote`` () =
    // A blockquote that itself contains a blockquote
    let md = "> > inner quote"
    let result = toMd md
    result |> should contain "> "
    result |> should contain "inner quote"
    // The inner quote marker should appear in the output (two levels of '>')
    result |> should contain "> >"

[<Test>]
let ``ToMd preserves emphasis inside a blockquote`` () =
    let md = "> *italic text*"
    let result = toMd md
    result |> should contain "> "
    result |> should contain "*italic text*"

[<Test>]
let ``ToMd preserves inline code inside a blockquote`` () =
    let md = "> use `printf` here"
    let result = toMd md
    result |> should contain "> "
    result |> should contain "`printf`"

[<Test>]
let ``ToMd preserves a code block without language`` () =
    let md = "```\nsome code\n```"
    let result = toMd md
    result |> should contain "some code"
    result |> should contain "```"

[<Test>]
let ``ToMd preserves horizontal rule (dash variant)`` () =
    let md = "---"
    let result = toMd md
    result |> should contain "---"

[<Test>]
let ``ToMd preserves a link with a title`` () =
    // Title attribute is allowed in Markdown links
    let md = "[FSharp](https://fsharp.org \"F# home\")"
    let result = toMd md
    result |> should contain "[FSharp]("
    result |> should contain "https://fsharp.org"

[<Test>]
let ``ToMd serialises EmbedParagraphs by delegating to Render()`` () =
    // EmbedParagraphs was previously falling through to the catch-all '| _' branch,
    // emitting a debug printfn and yielding an empty string.  It should instead
    // delegate to the Render() method and format the resulting paragraphs.
    let inner =
        { new MarkdownEmbedParagraphs with
            member _.Render() =
                [ Paragraph([ Literal("embedded text", MarkdownRange.zero) ], MarkdownRange.zero) ] }

    let doc = MarkdownDocument([ EmbedParagraphs(inner, MarkdownRange.zero) ], dict [])
    let result = Markdown.ToMd(doc)
    result |> should contain "embedded text"
