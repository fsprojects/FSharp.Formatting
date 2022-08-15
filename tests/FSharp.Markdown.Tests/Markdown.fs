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
    text
        .Replace("\r\n", "\n")
        .Replace("\n", System.Environment.NewLine)

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
                    Some(
                        { StartLine = 1
                          StartColumn = 0
                          EndLine = 1
                          EndColumn = 29 }
                    )
                ) ],
              Some(
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 29 }
              )
          ) ]

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
                    Some(
                        { StartLine = 2
                          StartColumn = 3
                          EndLine = 2
                          EndColumn = 11 }
                    )
                ) ],
              Some(
                  { StartLine = 2
                    StartColumn = 0
                    EndLine = 2
                    EndColumn = 11 }
              )
          )
          Paragraph(
              [ Literal(
                    "Some more",
                    Some(
                        { StartLine = 3
                          StartColumn = 0
                          EndLine = 3
                          EndColumn = 9 }
                    )
                ) ],
              Some(
                  { StartLine = 3
                    StartColumn = 0
                    EndLine = 3
                    EndColumn = 9 }
              )
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
                    Some(
                        { StartLine = 2
                          StartColumn = 3
                          EndLine = 2
                          EndColumn = 8 }
                    )
                ) ],
              Some(
                  { StartLine = 2
                    StartColumn = 0
                    EndLine = 2
                    EndColumn = 13 }
              )
          )
          Paragraph(
              [ Literal(
                    "Some more",
                    Some(
                        { StartLine = 3
                          StartColumn = 0
                          EndLine = 3
                          EndColumn = 9 }
                    )
                ) ],
              Some(
                  { StartLine = 3
                    StartColumn = 0
                    EndLine = 3
                    EndColumn = 9 }
              )
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
                    Some(
                        { StartLine = 3
                          StartColumn = 4
                          EndLine = 3
                          EndColumn = 5 }
                    )
                ) ],
              Some(
                  { StartLine = 3
                    StartColumn = 4
                    EndLine = 3
                    EndColumn = 5 }
              )
          )
          Paragraph(
              [ Literal(
                    "c",
                    Some(
                        { StartLine = 5
                          StartColumn = 4
                          EndLine = 5
                          EndColumn = 5 }
                    )
                ) ],
              Some(
                  { StartLine = 5
                    StartColumn = 4
                    EndLine = 5
                    EndColumn = 5 }
              )
          ) ]

    match doc.Paragraphs.Head with
    | ListBlock (Unordered,
                 [ [ Span ([ Literal ("a",
                                      Some ({ StartLine = 2
                                              StartColumn = 2
                                              EndLine = 2
                                              EndColumn = 3 })) ],
                           _)
                     ListBlock (Unordered, [ body ], _) ] ],
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
                        Some(
                            { StartLine = 1
                              StartColumn = 0
                              EndLine = 1
                              EndColumn = 8 }
                        )
                    ) ],
                  Some(
                      { StartLine = 1
                        StartColumn = 0
                        EndLine = 1
                        EndColumn = 12 }
                  )
              ) ],
            Some(
                { StartLine = 1
                  StartColumn = 0
                  EndLine = 1
                  EndColumn = 12 }
            )
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Can escape special characters in LaTex inline math`` () =
    let doc = """test \$ is: $foo\$\$bar<>\$\&\%\$\#\_\{\}$""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ Literal(
                  "test $ is: ",
                  Some(
                      { StartLine = 1
                        StartColumn = 0
                        EndLine = 1
                        EndColumn = 11 }
                  )
              )
              LatexInlineMath(
                  "foo\$\$bar<>\$\&\%\$\#\_\{\}",
                  Some(
                      { StartLine = 1
                        StartColumn = 12
                        EndLine = 1
                        EndColumn = 40 }
                  )
              ) ],
            Some(
                { StartLine = 1
                  StartColumn = 0
                  EndLine = 1
                  EndColumn = 42 }
            )
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Test special character _ in LaTex inline math`` () =
    let doc = """$\bigcap_{x \in A} p_{x}A$""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ LatexInlineMath(
                  "\\bigcap_{x \\in A} p_{x}A",
                  Some(
                      { StartLine = 1
                        StartColumn = 1
                        EndLine = 1
                        EndColumn = 25 }
                  )
              ) ],
            Some(
                { StartLine = 1
                  StartColumn = 0
                  EndLine = 1
                  EndColumn = 26 }
            )
        )

    doc.Paragraphs.Head |> shouldEqual expected

[<Test>]
let ``Inline code can contain backticks when wrapped with spaces`` () =
    let doc = """` ``h`` `""" |> Markdown.Parse

    let expected =
        Paragraph(
            [ InlineCode(
                  "``h``",
                  Some(
                      { StartLine = 1
                        StartColumn = 2
                        EndLine = 1
                        EndColumn = 7 }
                  )
              ) ],
            Some(
                { StartLine = 1
                  StartColumn = 0
                  EndLine = 1
                  EndColumn = 9 }
            )
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
let ``Transform header 1 correctly`` () =
    let doc = "#Header 1\nHeader 1\r\n========"

    let expected = "<h1>Header 1</h1>\r\n<h1>Header 1</h1>\r\n" |> properNewLines

    Markdown.ToHtml doc |> shouldEqual expected

[<Test>]
let ``Transform header 2 correctly`` () =
    let doc = "##Header 2\nHeader 2\r\n--------"

    let expected = "<h2>Header 2</h2>\r\n<h2>Header 2</h2>\r\n" |> properNewLines

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
              Some(
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 5 }
              )
          )
          HorizontalRule(
              '*',
              Some(
                  { StartLine = 3
                    StartColumn = 0
                    EndLine = 3
                    EndColumn = 3 }
              )
          )
          HorizontalRule(
              '*',
              Some(
                  { StartLine = 5
                    StartColumn = 0
                    EndLine = 5
                    EndColumn = 5 }
              )
          )
          HorizontalRule(
              '-',
              Some(
                  { StartLine = 7
                    StartColumn = 0
                    EndLine = 7
                    EndColumn = 5 }
              )
          )
          HorizontalRule(
              '-',
              Some(
                  { StartLine = 9
                    StartColumn = 0
                    EndLine = 9
                    EndColumn = 39 }
              )
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
              Some(
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 1 }
              )
          )
          Paragraph(
              [ Literal(
                    "a",
                    Some(
                        { StartLine = 2
                          StartColumn = 0
                          EndLine = 2
                          EndColumn = 1 }
                    )
                ) ],
              Some(
                  { StartLine = 2
                    StartColumn = 0
                    EndLine = 2
                    EndColumn = 1 }
              )
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
                          Some(
                              { StartLine = 1
                                StartColumn = 1
                                EndLine = 1
                                EndColumn = 2 }
                          )
                      ) ],
                    Some(
                        { StartLine = 1
                          StartColumn = 1
                          EndLine = 1
                          EndColumn = 2 }
                    )
                ) ],
              Some(
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 2 }
              )
          )
          Paragraph(
              [ Literal(
                    "a",
                    Some(
                        { StartLine = 3
                          StartColumn = 0
                          EndLine = 3
                          EndColumn = 1 }
                    )
                ) ],
              Some(
                  { StartLine = 3
                    StartColumn = 0
                    EndLine = 3
                    EndColumn = 1 }
              )
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
                          Some(
                              { StartLine = 1
                                StartColumn = 4
                                EndLine = 1
                                EndColumn = 5 }
                          )
                      ) ],
                    Some(
                        { StartLine = 1
                          StartColumn = 4
                          EndLine = 1
                          EndColumn = 5 }
                    )
                ) ],
              Some(
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 5 }
              )
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
                          Some(
                              { StartLine = 1
                                StartColumn = 0
                                EndLine = 1
                                EndColumn = 24 }
                          )
                      ) ],
                    Some(
                        { StartLine = 1
                          StartColumn = 0
                          EndLine = 1
                          EndColumn = 26 }
                    )
                ) ],
              Some(
                  { StartLine = 1
                    StartColumn = 0
                    EndLine = 1
                    EndColumn = 26 }
              )
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
                    Some
                        { StartLine = 2
                          StartColumn = 4
                          EndLine = 2
                          EndColumn = 53 }
                ) ],
              Some
                  { StartLine = 2
                    StartColumn = 0
                    EndLine = 2
                    EndColumn = 53 }
          )
          Paragraph(
              [ Literal(
                    "Always use a bar before every case in the declaration of a discriminated union.",
                    Some
                        { StartLine = 4
                          StartColumn = 0
                          EndLine = 4
                          EndColumn = 79 }
                ) ],
              Some
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
