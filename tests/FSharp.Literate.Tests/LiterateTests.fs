#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Formatting.Literate.dll"
#r "FSharp.Formatting.CodeFormat.dll"
#r "FSharp.Formatting.Markdown.dll"
#r "FSharp.Formatting.CSharpFormat.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net45/FsUnit.NUnit.dll"
#load "../Common/MarkdownUnit.fs"
#load "Setup.fs"
#else
[<NUnit.Framework.TestFixture>]
module FSharp.Literate.Tests.Simple
#endif

open FsUnit
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown
open FSharp.Formatting.Markdown.Unit
open NUnit.Framework
open FSharp.Literate.Tests.Setup
open FsUnitTyped
open FSharp.Formatting

do TestHelpers.enableLogging()

let properNewLines (text: string) = text.Replace("\r\n", System.Environment.NewLine)

// --------------------------------------------------------------------------------------
// Test embedding code from a file
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can embed content from an external file`` () =
  let doc =
    //[test]
    // magic
    Literate.ParseMarkdownString("""
a

    [lang=csharp,file=LiterateTests.fs,key=test]

b""", __SOURCE_DIRECTORY__ </> "Test.fsx")
    //[/test]
  doc.Paragraphs |> shouldMatchPar (function Paragraph([Literal("a", Some({ StartLine = 2 }))], Some({ StartLine = 2 })) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function Paragraph([Literal("b", Some({ StartLine = 6 }))], Some({ StartLine = 6 })) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | EmbedParagraphs(:? LiterateParagraph as cd, Some({ StartLine = 4 })) ->
        match cd with LanguageTaggedCode("csharp", text, _popts) -> text.Contains "magic" | _ -> false
    | _ -> false)

// --------------------------------------------------------------------------------------
// Test standalone literate parsing
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can parse literate F# script`` () =
  let content = """
(** **hello** *)
let test = 42"""
  let doc = Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent())
  doc.Diagnostics |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | MarkdownPatterns.LiterateParagraph(LiterateCode _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph([Strong([Literal("hello", Some({ StartLine = 1 }))], Some({ StartLine = 1 }))], Some({ StartLine = 1 })) -> true | _ -> false)

[<Test>]
let ``Can parse heading on the same line as opnening comment (#147)`` () =
  let content = """
(** ## Heading
content *)
let test = 42"""
  let doc = Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent())
  doc.Paragraphs |> shouldMatchPar (function
    | Heading(2, [Literal("Heading", Some({ StartLine = 1 }))], Some({ StartLine = 1 })) -> true | _ -> false)

[<Test>]
let ``Can parse markdown with F# snippet`` () =
  let content = """
**hello**

    let test = 42"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Diagnostics |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | MarkdownPatterns.LiterateParagraph(LiterateCode _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph([Strong([Literal("hello", Some({ StartLine = 2 }))], Some({ StartLine = 2 }))], Some({ StartLine = 2 })) -> true | _ -> false)

[<Test>]
let ``Can parse markdown with Github-flavoured F# snippet`` () =
  let content = """
**hello**

```fsharp followed by some random text
let test = 42
```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Diagnostics |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | MarkdownPatterns.LiterateParagraph(LiterateCode _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph([Strong([Literal("hello", Some({ StartLine = 2 }))], Some({ StartLine = 2 }))], Some({ StartLine = 2 })) -> true | _ -> false)

[<Test>]
let ``Can parse markdown with Github-flavoured F# snippet starting and ending with empty lines`` () =
  let content = """
```fsharp

let test = 42

```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Diagnostics |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | MarkdownPatterns.LiterateParagraph(LiterateCode _) -> true | _ -> false)

[<Test>]
let ``Can generate references from indirect links`` () =
  let content = """
(**
some [link][ref] to

  [ref]: http://there "Author: Article"
*)"""
  let doc = Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent(), references=true)
  doc.Paragraphs |> shouldMatchPar (function ListBlock(_, _, _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchSpan (function Literal("Article", None) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchSpan (function Literal(" - Author", None) -> true | _ -> false)

[<Test>]
let ``Can report errors in F# code snippets (in F# script file)`` () =
  let content = """
**hello**

    let test = 4 + 1.0"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Diagnostics |> Seq.length |> should be (greaterThan 0)

[<Test>]
let ``Can report errors in F# code snippets (in Markdown document)`` () =
  let content = """
(** **hello** *)
let test = 4 + 1.0"""
  let doc = Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent())
  doc.Diagnostics |> Seq.length |> should be (greaterThan 0)

// --------------------------------------------------------------------------------------
// Formatting code snippets
// --------------------------------------------------------------------------------------

[<Test>]
let ``C# syntax highlighter can process html`` () =
  let html = """
<pre lang="csharp">
var
</pre>"""
  let formatted = FSharp.Formatting.CSharpFormat.SyntaxHighlighter.FormatHtml(html)
  let expected = html.Replace(" lang=\"csharp\"", "").Replace("var", "<span class=\"k\">var</span>")
  formatted |> shouldEqual expected

[<Test>]
let ``Can format the var keyword in C# code snippet`` () =
  let content = """
hello

    [lang=csharp]
    var a = 10 < 10;"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText "<span class=\"k\">var</span>"

[<Test>]
let ``Can format the var keyword in C# code snippet using Github-flavoured`` () =
  let content = """
hello

```csharp
var a = 10 < 10;
```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText "<span class=\"k\">var</span>"

[<Test>]
let ``Codeblock whitespace is preserved`` () =
  let doc = "```markup\r\n    test\r\n    blub\r\n```\r\n";
  let expected = "lang=\"markup\">    test\r\n    blub\r\n</" |> properNewLines;
  let doc = Literate.ParseMarkdownString(doc, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText expected

[<Test>]
let ``Correctly handles Norwegian letters in SQL code block (#249)`` () =
  let content = """
    [lang=sql]
    Æøå"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText (sprintf ">Æøå%s<" System.Environment.NewLine)

[<Test>]
let ``Correctly handles code starting with whitespace`` () =
  let content = """
    [lang=unknown]
      inner"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText ">  inner"

[<Test>]
let ``Correctly handles code which garbage after commands`` () =
  // This will trigger a warning!
  let content = """
    [lang=unknown] some ignored garbage
      inner"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText ">  inner"

[<Test>]
let ``Correctly handles apostrophes in JS code block (#213)`` () =
  let content = """
    [lang=js]
    var but = 'I\'m not so good...';"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText @"'I\'m not so good...'"

[<Test>]
let ``Correctly encodes special HTML characters (<, >, &) in code`` () =
  let forLang = sprintf """
    [lang=%s]
    var pre = "<a> & <b>";"""
  ["js"; "unknown-language"] |> Seq.iter (fun lang ->
    let content = forLang lang
    let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
    let html = Literate.ToHtml(doc)
    html |> shouldContainText "&lt;a&gt; &amp; &lt;b&gt;"
  )

[<Test>]
let ``Correctly encodes already encoded HTML entities and tags`` () =
  let forLang = sprintf """
    [lang=%s]
    "&amp;" + "<em>" + "&quot;"; """
  ["js"; "unknown-language"] |> Seq.iter (fun lang ->
    let content = forLang lang
    content |> shouldContainText lang
    let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
    let html = Literate.ToHtml(doc)
    html |> shouldContainText "&amp;amp;"
    html |> shouldContainText "&amp;quot;"
    html |> shouldContainText "&lt;em&gt;"
  )

[<Test>]
let ``Urls should not be recognized as comments in Paket code blocks`` () =
  let content = """
    [lang=packet]
    source https://nuget.org/api/v2"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText @"https://nuget.org/api/v2"

[<Test>]
let ``Path to network share should not be recognized as comments in Paket code blocks`` () =
  let content = """
    [lang=packet]
    cache //hive/dependencies"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldNotContainText "<span class=\"c\">//hive/dependencies</span>"

[<Test>]
let ``Correctly handles Paket coloring`` () =
  let content = """
    [lang=paket]
    references: strict
    framework: net35, net40
    content: none
    import_targets: false
    copy_local: false
    copy_content_to_output_dir: always
    redirects: on
    strategy: min
    lowest_matching: true
    source https://nuget.org/api/v2 // nuget.org
    cache //hive/dependencies
    storage: none

    // NuGet packages
    nuget NUnit ~> 2.6.3
    nuget FAKE ~> 3.4
    nuget DotNetZip == 1.9
    nuget SourceLink.Fake
    nuget xunit.runners.visualstudio >= 2.0 version_in_path: true
    nuget Example @~> 1.2 // use "max" version resolution strategy
    nuget Example2 !~> 1.2 // use "min" version resolution strategy
    nuget Example-A @> 0

    // Files from GitHub repositories
    github forki/FsUnit FsUnit.fs
    git https://github.com/fsprojects/Paket.git master

    // Gist files
    gist Thorium/1972349 timestamp.fs

    // HTTP resources
    http http://www.fssnip.net/1n decrypt.fs

    // GIT tags
    git file:///C:\Users\Steffen\AskMe >= 1 alpha      // at least 1.0 including alpha versions
    """
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)

  html |> shouldContainText "<span class=\"k\">nuget</span>"
  html |> shouldContainText "<span class=\"k\">github</span>"
  html |> shouldContainText "<span class=\"k\">git</span>"
  html |> shouldContainText "<span class=\"k\">gist</span>"
  html |> shouldContainText "<span class=\"k\">http</span>"
  html |> shouldContainText "<span class=\"k\">references</span>"
  html |> shouldContainText "<span class=\"k\">framework</span>"
  html |> shouldContainText "<span class=\"k\">content</span>"
  html |> shouldContainText "<span class=\"k\">import_targets</span>"
  html |> shouldContainText "<span class=\"k\">copy_local</span>"
  html |> shouldContainText "<span class=\"k\">copy_content_to_output_dir</span>"
  html |> shouldContainText "<span class=\"k\">lowest_matching</span>"
  html |> shouldContainText "<span class=\"k\">redirects</span>"
  html |> shouldContainText "<span class=\"k\">strategy</span>"
  html |> shouldContainText "<span class=\"k\">version_in_path</span>"
  html |> shouldContainText "<span class=\"k\">storage</span>"

  html |> shouldNotContainText "<span class=\"k\">http</span>s"
  html |> shouldNotContainText ".<span class=\"k\">git</span>"

  html |> shouldContainText "<span class=\"o\">~&gt;</span>"
  html |> shouldContainText "<span class=\"o\">&gt;=</span>"
  html |> shouldContainText "<span class=\"o\">==</span>"
  html |> shouldContainText "<span class=\"o\">!</span><span class=\"o\">~&gt;</span>"
  html |> shouldContainText "<span class=\"o\">@</span><span class=\"o\">~&gt;</span>"
  html |> shouldContainText "<span class=\"o\">@</span><span class=\"o\">&gt;</span>"

  html |> shouldContainText "<span class=\"n\">3.4</span>"
  html |> shouldContainText "<span class=\"n\">2.6.3</span>"

  html |> shouldContainText "<span class=\"c\">// NuGet packages</span>"
  html |> shouldContainText "<span class=\"c\">// nuget.org</span>"
  html |> shouldNotContainText "<span class=\"c\">//hive/dependencies</span>"

  html |> shouldContainText @"https://nuget.org/api/v2"
  html |> shouldContainText @"http://www.fssnip.net/1n"

  html |> shouldContainText @"file:///C:\Users\Steffen\AskMe"
  html |> shouldContainText "<span class=\"c\">// at least 1.0 including alpha versions</span>"

[<Test>]
let ``Generates line numbers for F# code snippets`` () =
  let content = """
(** Hello *)
let a1 = 1
let a2 = 2"""
  let doc = Literate.ParseScriptString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc, lineNumbers=true)
  html |> shouldContainText "<p>Hello</p>"
  html |> shouldContainText "1:"
  html |> shouldContainText "2:"
  html |> shouldNotContainText "3:"

[<Test>]
let ``Generates line numbers for non-F# code snippets`` () =
  let content = """
(** Hello

```csharp
var a1 = 1;
var a2 = 2;
``` *)"""
  let doc = Literate.ParseScriptString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc, lineNumbers=true)
  html |> shouldContainText "<p>Hello</p>"
  html |> shouldContainText "1:"
  html |> shouldContainText "2:"
  html |> shouldNotContainText "3:"

[<Test>]
let ``HTML for line numbers generated for F# and non-F# is the same``() =
  let content1 = "    [lang=js]\n    var"
  let content2 = "    let"
  let doc1 = Literate.ParseMarkdownString(content1, formatAgent=getFormatAgent())
  let doc2 = Literate.ParseMarkdownString(content2, formatAgent=getFormatAgent())
  let html1 = Literate.ToHtml(doc1, lineNumbers=true)
  let html2 = Literate.ToHtml(doc2, lineNumbers=true)

  html1.Substring(0, html1.IndexOf("1:"))
  |> shouldEqual <| html2.Substring(0, html2.IndexOf("1:"))

[<Test>]
let ``HTML for snippets generated for F# and non-F# has 'fssnip' class``() =
  let content1 = "    [lang=js]\n    var"
  let content2 = "    let"
  let doc1 = Literate.ParseMarkdownString(content1, formatAgent=getFormatAgent())
  let doc2 = Literate.ParseMarkdownString(content2, formatAgent=getFormatAgent())
  let html1 = Literate.ToHtml(doc1, lineNumbers=true)
  let html2 = Literate.ToHtml(doc2, lineNumbers=true)

  // the 'fssnip' class appears for both <pre> with lines and <pre> with code
  html1.Split([| "fssnip" |], System.StringSplitOptions.None).Length |> shouldEqual 3
  html2.Split([| "fssnip" |], System.StringSplitOptions.None).Length |> shouldEqual 3

// --------------------------------------------------------------------------------------
// Test that parsed documents for Markdown and F# #scripts are the same
// --------------------------------------------------------------------------------------

let simpleFsx = """
(** **Hello** *)
let test = 42"""
let simpleMd = """
**Hello**

    let test = 42"""

[<Test>]
let ``Parsing simple script and markdown produces the same result`` () =
  // Use path "/usr/File.fsx" which makes them equal, including the tool tips on Mono
  let doc1 = Literate.ParseMarkdownString(simpleMd, path="/usr/File.fsx", formatAgent = getFormatAgent()) |> Literate.ToHtml
  let doc2 = Literate.ParseScriptString(simpleFsx, path="/usr/File.fsx", formatAgent = getFormatAgent()) |> Literate.ToHtml
  doc1 |> shouldEqual doc2

// --------------------------------------------------------------------------------------
// Test processing simple files using simple templates
// --------------------------------------------------------------------------------------

[<Test>]
let ``Code and HTML is formatted with a tooltip in Markdown file using substitution in HTML template``() =
  let templateHtml = __SOURCE_DIRECTORY__ </> "files/template.html"
  let simpleMd = __SOURCE_DIRECTORY__ </> "files/simple.md"
  use temp = new TempFile()
  Literate.ConvertMarkdown(simpleMd, templateHtml, temp.File)
  temp.Content |> shouldContainText "</a>"
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"

[<Test>]
let ``Code and HTML is formatted with a tooltip in F# Script file using substitution in HTML template``() =
  let templateHtml = __SOURCE_DIRECTORY__ </> "files/template.html"
  let simpleFsx = __SOURCE_DIRECTORY__ </> "files/simple.fsx"
  use temp = new TempFile()
  Literate.ConvertScriptFile(simpleFsx, templateHtml, temp.File)
  temp.Content |> shouldContainText "</a>"
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"

// --------------------------------------------------------------------------------------
// Test processing simple files using the NuGet included templates
// --------------------------------------------------------------------------------------

let info =
  [ "project-name", "FSharp.ProjectScaffold"
    "authors", "Your Name"
    "repository-url", "http://github.com/pblasucci/fsharp-project-scaffold"
    "root", "http://fsprojects.github.io/FSharp.FSharp.ProjectScaffold" ]


[<Test>]
let ``Can process fsx file using HTML template``() =
  let docPageTemplate = __SOURCE_DIRECTORY__ </> "../../misc/templates/_template.html"
  let simpleFsx = __SOURCE_DIRECTORY__ </> "files/simple.fsx"
  use temp = new TempFile()
  Literate.ConvertScriptFile(simpleFsx, docPageTemplate, temp.File, parameters=info)
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"

[<Test>]
let ``Can process md file using HTML template``() =
  let docPageTemplate = __SOURCE_DIRECTORY__ </> "../../misc/templates/_template.html"
  let simpleMd = __SOURCE_DIRECTORY__ </> "files/simple.md"
  use temp = new TempFile()
  Literate.ConvertMarkdown(simpleMd, docPageTemplate, temp.File, parameters=info)
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"

[<Test>]
let ``Gives nice error when parsing unclosed comment`` () =
  let content = """
(** **hello**
let test = 42"""
  try
    Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent()) |> ignore
    failwith ""
  with
  | e when e.Message.Contains("comment was not closed") -> ()
  | _ -> failwith "not correct error"

// --------------------------------------------------------------------------------------
// Formatting F# code snippets
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can format the seq as first line in F# code snippet`` () =
  let content = """
hello

    ["Some"]
    |> Seq.length"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html |> shouldContainText "Some"


[<Test>]
let ``Can move snippets around using include and define commands`` () =
  let content = """
(*** include:later-bit ***)
(**
Second
*)
(*** define:later-bit ***)
let First = 0
"""
  let doc = Literate.ParseScriptString(content, formatAgent=getFormatAgent())
  let html = Literate.ToHtml(doc)
  html.IndexOf("First") < html.IndexOf("Second") |> shouldEqual true


[<Test>]
let ``Can split formatted document and formatted tool tips`` () =
  let content = """(**
hello
*)
let test = 42
"""
  let doc = Literate.ParseScriptString(content, "." </> "A.fsx", getFormatAgent())
  let doc2 = Literate.FormatLiterateNodes(doc,outputKind=OutputKind.Html)
  let html = Literate.ToHtml(doc2.With(formattedTips=""))
  let tips = doc2.FormattedTips
  tips |> shouldContainText "test : int"
  html |> shouldNotContainText "test : int"
  html |> shouldContainText "hello"


[<Test>]
let ``Can format single snippet with label using literate parser`` () =
  let source = """
// [snippet:demo]
let add a b = a + b
// [/snippet]"""
  let doc = Literate.ParseScriptString(source, "/somewhere/test.fsx", getFormatAgent())
  doc.Paragraphs |> shouldMatchPar (function Heading(_, [Literal("demo", Some({ StartLine = 1 }))], Some({ StartLine = 1 })) -> true | _ -> false)


[<Test>]
let ``Can format multiple snippets with labels using literate parser`` () =
  let source = """
// [snippet:demo1]
let add a b = a + b
// [/snippet]
// [snippet:demo2]
let mul a b = a * b
// [/snippet]"""
  let doc = Literate.ParseScriptString(source, "/somewhere/test.fsx", getFormatAgent())
  doc.Paragraphs |> shouldMatchPar (function Heading(_, [Literal("demo1", Some({ StartLine = 1 }))], Some({ StartLine = 1 })) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function Heading(_, [Literal("demo2", Some({ StartLine = 1 }))], Some({ StartLine = 1 })) -> true | _ -> false)

[<Test>]
let ``Formatter does not crash on source that contains invalid string`` () =
  let source = "\"\r\"\n0"
  let doc = Literate.ParseScriptString(source, "/somewhere/test.fsx", getFormatAgent())
  Literate.ToHtml(doc).Length |> should (be greaterThan) 0

[<Test>]
let ``Markdown is formatted as Latex``() =
  let md = Literate.ParseMarkdownString("""Heading
=======
  
With some [hyperlink](http://tomasp.net)
  
    let hello = "Code sample"
  """)
  let latex = Literate.ToLatex(md)
  printfn "----" 
  printfn "%s" latex
  printfn "----" 
  latex |> shouldContainText @"\section*{Heading}"
  latex |> shouldContainText @"With some \href{http://tomasp.net}{hyperlink}"
  latex |> shouldContainText @"\begin{Verbatim}[commandchars=\\\{\}, numbers=left]"
  latex |> shouldContainText """\kwd{let} \id{hello} \ops{=} \str{"Code sample"}"""
  latex |> shouldContainText """\end{Verbatim}"""
  
  
[<Test>]
let ``Markdown with code is formatted as Pynb``() =

  // Note the parsing options used for markdown --> ipynb mean most the markdown (except code) is parsed throuhh
  // unchanged
  let md = Literate.ParseMarkdownString("""Heading
=======
  
With some [hyperlink](http://tomasp.net)
  
    let hello = "Code sample"
  """, parseOptions=MarkdownParseOptions.ParseNonCodeAsOther)
  let pynb = Literate.ToPynb(md)
  printfn "----" 
  printfn "%s" pynb
  printfn "----" 
  pynb |> shouldContainText """ "cells": ["""
  pynb |> shouldContainText """ "cell_type": "markdown","""
  pynb |> shouldContainText """ "source": ["Heading\n","""
  pynb |> shouldContainText """"=======\n","""
  pynb |> shouldContainText """With some [hyperlink](http://tomasp.net)"""
  pynb |> shouldContainText """ "cell_type": "code","""
  pynb |> shouldContainText """ "execution_count": null, "outputs": [], """
  pynb |> shouldContainText """ "source": ["let hello = \"Code sample"""
  pynb |> shouldContainText """ "kernelspec": {"display_name": ".NET (F#)", "language": "F#", "name": ".net-fsharp"},"""
  pynb |> shouldContainText """ "file_extension": ".fs","""
  pynb |> shouldContainText """ "mimetype": "text/x-fsharp","""
  pynb |> shouldContainText """ "pygments_lexer": "fsharp","""
  pynb |> shouldContainText """ "version": "4.5"""
  pynb |> shouldContainText """ "nbformat": 4,"""
  pynb |> shouldContainText """ "nbformat_minor": 1"""
  
  
[<Test>]
let ``Script with markdown is formatted as Pynb with all markdown passed through``() =
  let md = Literate.ParseScriptString("""
(**
Heading
=======

|  Col1 | Col2 |
|:----:|------|
|  Table with heading cell A1   | Table with heading cell B1    |
|  Table with heading cell A2   | Table with heading cell B2    |

|:----:|------|
|  Table without heading cell A1   | Table without heading cell B1    |
|  Table without heading cell A2   | Table without heading cell B2    |


```emptyblockcode
```

```singlelineblockcode
single line block code
```

```
two line block code line 1
two line block code line 2
```

> hello

> two line blockquote line 1
> two line blockquote line 2

two line paragraph line 1
two line paragraph line 2

- list block line 1
- list block line 2

Another paragraph

- list block with gap line 1

- list block with gap line 2

Another paragraph

1. ordered list block line 1
2. ordered list block line 2

Another paragraph

1. ordered list block with gap line 1

2. ordered list block with gap line 2

With some [hyperlink](http://tomasp.net)
*)
let hello = "Code sample"
(*** condition: ipynb ***)
#if IPYNB
let hello1 = 1 // Conditional code is still present in notebooks
#endif // IPYNB
(*** condition: fsx ***)
let hello2 = 2 // Conditional code is not present in notebooks
(*** condition: formatting ***)
let hello3 = 3 // Formatting code is not present in notebooks
(*** condition: html ***)
let hello4 = 4 // Conditional code is not present in notebooks
(*** condition: prepare ***)
let hello5 = 4 // Doc preparation code is not present in generated notebooks

""", parseOptions=(MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther))
  let pynb = Literate.ToPynb(md)
  printfn "----" 
  printfn "%s" pynb
  printfn "----" 
  pynb |> shouldContainText """ "cells": ["""
  pynb |> shouldContainText """ "cell_type": "markdown","""
  pynb |> shouldContainText """ "source": ["Heading"""
  pynb |> shouldContainText """====="""
  pynb |> shouldContainText """```emptyblockcode"""
  pynb |> shouldContainText """|  Table with heading cell A2"""
  pynb |> shouldContainText """|  Col1 | Col2 |"""
  pynb |> shouldContainText """|:----:|------|"""
  pynb |> shouldContainText """|  Table with heading cell A1   | Table with heading cell B1    |"""
  pynb |> shouldContainText """|  Table with heading cell A2   | Table with heading cell B2    |"""
  pynb |> shouldContainText """|:----:|------|"""
  pynb |> shouldContainText """|  Table without heading cell A1   | Table without heading cell B1    |"""
  pynb |> shouldContainText """|  Table without heading cell A2   | Table without heading cell B2    |"""
  pynb |> shouldContainText """```emptyblockcode"""
  pynb |> shouldContainText """```singlelineblockcode"""
  pynb |> shouldContainText """single line block code"""
  pynb |> shouldContainText """two line block code line 1"""
  pynb |> shouldContainText """two line block code line 2"""
  pynb |> shouldContainText """\u003e hello"""
  pynb |> shouldContainText """\u003e two line blockquote line 1"""
  pynb |> shouldContainText """\u003e two line blockquote line 2"""
  pynb |> shouldContainText """two line paragraph line 1"""
  pynb |> shouldContainText """two line paragraph line 2"""
  pynb |> shouldContainText """- list block line 1"""
  pynb |> shouldContainText """- list block line 2"""
  pynb |> shouldContainText """- list block with gap line 1"""
  pynb |> shouldContainText """- list block with gap line 2"""
  pynb |> shouldContainText """1. ordered list block line 1"""
  pynb |> shouldContainText """2. ordered list block line 2"""
  pynb |> shouldContainText """1. ordered list block with gap line 1"""
  pynb |> shouldContainText """2. ordered list block with gap line 2"""
  pynb |> shouldContainText """With some [hyperlink](http://tomasp.net)"""
  pynb |> shouldContainText """let hello1 = 1 // Conditional code is still present in notebooks"""
  pynb |> shouldNotContainText """#if IPYNB"""
  pynb |> shouldNotContainText """#endif"""
  pynb |> shouldNotContainText """Conditional code is not present in notebooks"""
  pynb |> shouldNotContainText """Formatting code is not present in notebooks"""
  pynb |> shouldNotContainText """Doc preparation code is not present in generated notebooks"""
  pynb |> shouldContainText """ "cell_type": "code","""
  pynb |> shouldContainText """ "execution_count": null, "outputs": [], """
  pynb |> shouldContainText """ "source": ["let hello = \"Code sample"""
  pynb |> shouldContainText """ "kernelspec": {"display_name": ".NET (F#)", "language": "F#", "name": ".net-fsharp"},"""
  pynb |> shouldContainText """ "file_extension": ".fs","""
  pynb |> shouldContainText """ "mimetype": "text/x-fsharp","""
  pynb |> shouldContainText """ "pygments_lexer": "fsharp","""
  pynb |> shouldContainText """ "version": "4.5"""
  pynb |> shouldContainText """ "nbformat": 4,"""
  pynb |> shouldContainText """ "nbformat_minor": 1"""
  
  
[<Test>]
let ``Notebook output is exactly right``() =
  let md = Literate.ParseScriptString("""
let hello = 1

let goodbye = 2
""", parseOptions=(MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther))
  let pynb = Literate.ToPynb(md)
  printfn "----" 
  printfn "%s" pynb
  printfn "----" 
  let pynb2 = pynb.Replace("\r\n", "\n").Replace("\n", "!")
  let expected = """
        {
            "cells": [
          {
           "cell_type": "code",
           "metadata": {},
            "execution_count": null, "outputs": [], 
           "source": ["let hello = 1\n",
"\n",
"let goodbye = 2\n"]
          }],
            "metadata": {
            "kernelspec": {"display_name": ".NET (F#)", "language": "F#", "name": ".net-fsharp"},
            "langauge_info": {
        "file_extension": ".fs",
        "mimetype": "text/x-fsharp",
        "name": "C#",
        "pygments_lexer": "fsharp",
        "version": "4.5"
        }
        },
            "nbformat": 4,
            "nbformat_minor": 1
        }
        """
  let expected2 = expected.Replace("\r\n", "\n").Replace("\n", "!")
  pynb2 |> shouldEqual expected2

  
[<Test>]
let ``Script output is exactly right``() =
  let md = Literate.ParseScriptString("""
let hello = 1

let goodbye = 2
""", parseOptions=(MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther))
  let fsx = Literate.ToFsx(md)
  printfn "----" 
  printfn "%s" fsx
  printfn "----" 
  let fsx2 = fsx.Replace("\r\n", "\n").Replace("\n", "!")
// NOTE: the script output is a bit trimmed, we should fix this
  let expected = """let hello = 1

let goodbye = 2"""
  let expected2 = expected.Replace("\r\n", "\n").Replace("\n", "!")
  fsx2 |> shouldEqual expected2

