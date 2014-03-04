#if INTERACTIVE
#r "../../bin/FSharp.Markdown.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Markdown.Tests.Parsing
#endif

open FsUnit
open NUnit.Framework
open FSharp.Markdown

let properNewLines (text: string) = text.Replace("\r\n", System.Environment.NewLine)

[<Test>]
let ``Inline HTML tag containing 'at' is not turned into hyperlink`` () =
  let doc = """<a href="mailto:a@b.c">hi</a>""" |> Markdown.Parse
  doc.Paragraphs
  |> shouldEqual [ Paragraph [Literal """<a href="mailto:a@b.c">hi</a>""" ]]

[<Test>]
let ``Headings ending with F# are parsed correctly`` () =
  let doc = """
## Hello F#
Some more""" |> Markdown.Parse

  doc.Paragraphs
  |> shouldEqual [
      Heading(2, [Literal "Hello F#"]); 
      Paragraph [Literal "Some more"]]

[<Test>]
let ``Headings ending with spaces followed by # are parsed correctly`` () =
  let doc = """
## Hello ####
Some more""" |> Markdown.Parse

  doc.Paragraphs
  |> shouldEqual [
      Heading(2, [Literal "Hello"]); 
      Paragraph [Literal "Some more"]]

[<Test>]
let ``Can escape special characters such as "*" in emphasis`` () =
  let doc = """*foo\*\*bar*""" |> Markdown.Parse
  let expected = Paragraph [Emphasis [Literal "foo**bar"]] 
  doc.Paragraphs.Head 
  |> shouldEqual expected

[<Test>]
let ``Can escape special characters in LaTex inline math`` () =
  let doc = """test \$ is: $foo\$\$bar<>\$\&\%\$\#\_\{\}$""" |> Markdown.Parse
  let expected = Paragraph [Literal "test $ is: "; LatexInlineMath "foo\$\$bar<>\$\&\%\$\#\_\{\}"]
  doc.Paragraphs.Head
  |> shouldEqual expected

[<Test>]
let ``Test special character _ in LaTex inline math`` () =
    let doc = """$\bigcap_{x \in A} p_{x}A$""" |> Markdown.Parse
    let expected = Paragraph [ LatexInlineMath "\\bigcap_{x \\in A} p_{x}A" ]
    doc.Paragraphs.Head
    |> shouldEqual expected
      
[<Test>]
let ``Inline code can contain backticks when wrapped with spaces`` () =
  let doc = """` ``h`` `""" |> Markdown.Parse
  let expected = Paragraph [InlineCode "``h``"]
  doc.Paragraphs.Head 
  |> shouldEqual expected

[<Test>]
let ``Transform bold text correctly``() =
    let doc = "This is **bold**. This is also __bold__.";
    let expected = "<p>This is <strong>bold</strong>. This is also <strong>bold</strong>.</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform LaTex inline math correctly``() =
  let doc = """test \$ is: $foo\$\$bar<>\$\&\%\$\#\_\{\}$."""
  let expected = """<p>test $ is: <span class="math">\(foo\$\$bar&lt;&gt;\$\&amp;\%\$\#\_\{\}\)</span>.</p>"""
  (Markdown.TransformHtml doc).Trim()
  |> shouldEqual expected

[<Test>]
let ``Transform LaTex block correctly``() =
  let doc = """$$$
foo\$\$bar<>\$\&\%\$\#\_\{\}
foo\$\$bar<>\$\&\%\$\#\_\{\}"""
  let expected = """<p><span class="math">\[foo\$\$bar&lt;&gt;\$\&amp;\%\$\#\_\{\}
foo\$\$bar&lt;&gt;\$\&amp;\%\$\#\_\{\}\]</span></p>"""
  (Markdown.TransformHtml doc).Trim()
  |> shouldEqual expected

[<Test>]
let ``Transform italic text correctly``() =    
    let doc = "This is *italic*. This is also _italic_.";
    let expected = "<p>This is <em>italic</em>. This is also <em>italic</em>.</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected


[<Test>]
let ``Transform hyperlinks correctly``() =
    let doc = "This is [a link][1].\r\n\r\n  [1]: http://www.example.com";
    let expected = "<p>This is <a href=\"http://www.example.com\">a link</a>.</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform bracketed hyperlinks correctly``() =
    let doc = "Have you visited <http://www.example.com> before?";
    let expected = "<p>Have you visited <a href=\"http://www.example.com\">http://www.example.com</a> before?</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform auto hyperlinks correctly``() =
    let doc = "Have you visited http://www.example.com or https://www.example.com before?";
    let expected = "<p>Have you visited <a href=\"http://www.example.com\">http://www.example.com</a> or <a href=\"https://www.example.com\">https://www.example.com</a> before?</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform alternative links correctly``() =
    let doc = "Have you visited [example](http://www.example.com) before?";
    let expected = "<p>Have you visited <a href=\"http://www.example.com\">example</a> before?</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform images correctly``() =
    let doc = "An image goes here: ![alt text][1]\r\n\r\n  [1]: http://www.google.com/intl/en_ALL/images/logo.gif";
    let expected = "<p>An image goes here: <img src=\"http://www.google.com/intl/en_ALL/images/logo.gif\" alt=\"alt text\" /></p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform blockquotes correctly``() =
    let doc = "Here is a quote\r\n\r\n> Sample blockquote\r\n";
    let expected = "<p>Here is a quote</p>\r\n\r\n<blockquote>\r\n  <p>Sample blockquote</p>\r\n</blockquote>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform numbered lists correctly``() =
    let doc = "A numbered list:\r\n\n1. a\n2. b\n3. c\r\n";
    let expected = "<p>A numbered list:</p>\r\n\r\n<ol>\r\n<li>a</li>\r\n<li>b</li>\r\n<li>c</li>\r\n</ol>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform bulleted lists correctly``() =
    let doc = "A bulleted list:\r\n\r\n- a\r\n- b\r\n- c\r\n";
    let expected = "<p>A bulleted list:</p>\r\n\r\n<ul>\r\n<li>a</li>\r\n<li>b</li>\r\n<li>c</li>\r\n</ul>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform header 1 correctly``() =
    let doc = "#Header 1\nHeader 1\r\n========";
    let expected = "<h1>Header 1</h1>\r\n\r\n<h1>Header 1</h1>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform header 2 correctly``() =
    let doc = "##Header 2\nHeader 2\r\n--------";
    let expected = "<h2>Header 2</h2>\r\n\r\n<h2>Header 2</h2>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform code blocks correctly``() =
    let doc = "code sample:\r\n\r\n    <head>\r\n    <title>page title</title>\r\n    </head>\r\n";
    let expected = "<p>code sample:</p>\r\n\r\n<pre><code>&lt;head&gt;\r\n&lt;title&gt;page title&lt;/title&gt;\r\n&lt;/head&gt;\r\n</code></pre>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform code spans correctly``() =
    let doc = "HTML contains the `<blink>` tag";
    let expected = "<p>HTML contains the <code>&lt;blink&gt;</code> tag</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected
    
[<Test>]
let ``Transform display math correctly``() =
     let doc = """$$\bigcap_{x \in A} p_{x}A$$"""
     let res = Markdown.TransformHtml doc
     let res = res.TrimEnd([|'\r';'\n'|])
     res |> shouldEqual "<p><span class=\"math\">\\[\\bigcap_{x \\in A} p_{x}A\\]</span></p>"

[<Test>]
let ``Transform HTML passthrough correctly``() =
    let doc = "<div>\r\nHello World!\r\n</div>\r\n";
    let expected = "<div>\r\nHello World!\r\n</div>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform escaped characters correctly``() =
    let doc = @"\`foo\`";
    let expected = "<p>`foo`</p>\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

[<Test>]
let ``Transform horizontal rules correctly``() =
    let doc = "* * *\r\n\r\n***\r\n\r\n*****\r\n\r\n- - -\r\n\r\n---------------------------------------\r\n\r\n";
    let expected = "<hr />\r\n\r\n<hr />\r\n\r\n<hr />\r\n\r\n<hr />\r\n\r\n<hr />\r\n" |> properNewLines;
    Markdown.TransformHtml doc
    |> shouldEqual expected

