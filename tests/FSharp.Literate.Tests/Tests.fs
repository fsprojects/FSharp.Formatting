#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Markdown.dll"
#r "CSharpFormat.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net45/FsUnit.NUnit.dll"
#load "../Common/MarkdownUnit.fs"
#load "Setup.fs"
#else
[<NUnit.Framework.TestFixture>]
module FSharp.Literate.Tests.Simple
#endif

open FsUnit
open FSharp.Literate
open FSharp.Markdown
open FSharp.Markdown.Unit
open NUnit.Framework
open FSharp.Literate.Tests.Setup
#if !NETSTANDARD2_0
open FSharp.Formatting.Razor
#endif
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

    [lang=csharp,file=Tests.fs,key=test]

b""", __SOURCE_DIRECTORY__ </> "Test.fsx")
    //[/test]
  doc.Paragraphs |> shouldMatchPar (function Paragraph([Literal("a", Some({ StartLine = 2 }))], Some({ StartLine = 2 })) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function Paragraph([Literal("b", Some({ StartLine = 6 }))], Some({ StartLine = 6 })) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | EmbedParagraphs(:? LiterateParagraph as cd, Some({ StartLine = 4 })) ->
        match cd with LanguageTaggedCode("csharp", text) -> text.Contains "magic" | _ -> false
    | _ -> false)

// --------------------------------------------------------------------------------------
// Test standalone literate parsing
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can parse and format literate F# script`` () =
  let content = """
(** **hello** *)
let test = 42"""
  let doc = Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent())
  doc.Errors |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)
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
let ``Can parse and format markdown with F# snippet`` () =
  let content = """
**hello**

    let test = 42"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Errors |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph([Strong([Literal("hello", Some({ StartLine = 2 }))], Some({ StartLine = 2 }))], Some({ StartLine = 2 })) -> true | _ -> false)

[<Test>]
let ``Can parse and format markdown with Github-flavoured F# snippet`` () =
  let content = """
**hello**

```fsharp followed by some random text
let test = 42
```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Errors |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph([Strong([Literal("hello", Some({ StartLine = 2 }))], Some({ StartLine = 2 }))], Some({ StartLine = 2 })) -> true | _ -> false)

[<Test>]
let ``Can parse and format markdown with Github-flavoured F# snippet starting and ending with empty lines`` () =
  let content = """
```fsharp

let test = 42

```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = getFormatAgent())
  doc.Errors |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)

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
  doc.Errors |> Seq.length |> should be (greaterThan 0)

[<Test>]
let ``Can report errors in F# code snippets (in Markdown document)`` () =
  let content = """
(** **hello** *)
let test = 4 + 1.0"""
  let doc = Literate.ParseScriptString(content, "C" </> "A.fsx", getFormatAgent())
  doc.Errors |> Seq.length |> should be (greaterThan 0)

// --------------------------------------------------------------------------------------
// Formatting code snippets
// --------------------------------------------------------------------------------------

[<Test>]
let ``C# syntax highlighter can process html`` () =
  let html = """
<pre lang="csharp">
var
</pre>"""
  let formatted = CSharpFormat.SyntaxHighlighter.FormatHtml(html)
  let expected = html.Replace(" lang=\"csharp\"", "").Replace("var", "<span class=\"k\">var</span>")
  formatted |> shouldEqual expected

[<Test>]
let ``Can format the var keyword in C# code snippet`` () =
  let content = """
hello

    [lang=csharp]
    var a = 10 < 10;"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText "<span class=\"k\">var</span>"

[<Test>]
let ``Can format the var keyword in C# code snippet using Github-flavoured`` () =
  let content = """
hello

```csharp
var a = 10 < 10;
```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText "<span class=\"k\">var</span>"

[<Test>]
let ``Codeblock whitespace is preserved`` () =
  let doc = "```markup\r\n    test\r\n    blub\r\n```\r\n";
  let expected = "lang=\"markup\">    test\r\n    blub\r\n</" |> properNewLines;
  let doc = Literate.ParseMarkdownString(doc, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText expected

[<Test>]
let ``Correctly handles Norwegian letters in SQL code block (#249)`` () =
  let content = """
    [lang=sql]
    Æøå"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText (sprintf ">Æøå%s<" System.Environment.NewLine)

[<Test>]
let ``Correctly handles code starting with whitespace`` () =
  let content = """
    [lang=unknown]
      inner"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText ">  inner"

[<Test>]
let ``Correctly handles code which garbage after commands`` () =
  // This will trigger a warning!
  let content = """
    [lang=unknown] some ignored garbage
      inner"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText ">  inner"

[<Test>]
let ``Correctly handles apostrophes in JS code block (#213)`` () =
  let content = """
    [lang=js]
    var but = 'I\'m not so good...';"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText @"'I\'m not so good...'"

[<Test>]
let ``Correctly encodes special HTML characters (<, >, &) in code`` () =
  let forLang = sprintf """
    [lang=%s]
    var pre = "<a> & <b>";"""
  ["js"; "unknown-language"] |> Seq.iter (fun lang ->
    let content = forLang lang
    let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
    let html = Literate.WriteHtml(doc)
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
    let html = Literate.WriteHtml(doc)
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
  let html = Literate.WriteHtml(doc)
  html |> shouldContainText @"https://nuget.org/api/v2"

[<Test>]
let ``Path to network share should not be recognized as comments in Paket code blocks`` () =
  let content = """
    [lang=packet]
    cache //hive/dependencies"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
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
  let html = Literate.WriteHtml(doc)

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
  let html = Literate.WriteHtml(doc, lineNumbers=true)
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
  let html = Literate.WriteHtml(doc, lineNumbers=true)
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
  let html1 = Literate.WriteHtml(doc1, lineNumbers=true)
  let html2 = Literate.WriteHtml(doc2, lineNumbers=true)

  html1.Substring(0, html1.IndexOf("1:"))
  |> shouldEqual <| html2.Substring(0, html2.IndexOf("1:"))

[<Test>]
let ``HTML for snippets generated for F# and non-F# has 'fssnip' class``() =
  let content1 = "    [lang=js]\n    var"
  let content2 = "    let"
  let doc1 = Literate.ParseMarkdownString(content1, formatAgent=getFormatAgent())
  let doc2 = Literate.ParseMarkdownString(content2, formatAgent=getFormatAgent())
  let html1 = Literate.WriteHtml(doc1, lineNumbers=true)
  let html2 = Literate.WriteHtml(doc2, lineNumbers=true)

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
  let doc1 = Literate.ParseMarkdownString(simpleMd, path="/usr/File.fsx", formatAgent = getFormatAgent()) |> Literate.WriteHtml
  let doc2 = Literate.ParseScriptString(simpleFsx, path="/usr/File.fsx", formatAgent = getFormatAgent()) |> Literate.WriteHtml
  doc1 |> shouldEqual doc2

// --------------------------------------------------------------------------------------
// Test processing simple files using simple templates
// --------------------------------------------------------------------------------------
#if !NETSTANDARD2_0
let templateHtml = __SOURCE_DIRECTORY__ </> "files/template.html"
let templateCsHtml = __SOURCE_DIRECTORY__ </> "files/template.cshtml"

[<Test>]
let ``Code and HTML is formatted with a tooltip in Markdown file using HTML template``() =
  let simpleMd = __SOURCE_DIRECTORY__ </> "files/simple.md"
  use temp = new TempFile()
  RazorLiterate.ProcessMarkdown(simpleMd, templateHtml, temp.File)
  temp.Content |> shouldContainText "</a>"
  temp.Content |> shouldContainText "val hello : string"

[<Test>]
let ``Code and HTML is formatted with a tooltip in F# Script file using HTML template``() =
  let simpleFsx = __SOURCE_DIRECTORY__ </> "files/simple.fsx"
  use temp = new TempFile()
  RazorLiterate.ProcessScriptFile(simpleFsx, templateHtml, temp.File)
  temp.Content |> shouldContainText "</a>"
  temp.Content |> shouldContainText "val hello : string"

[<Test>][<Ignore "RazorEngine keeps breaking on template compilation in addition to underlying Issues with FSI configuration need to be addressed">]
let ``Code and HTML is formatted with a tooltip in F# Script file using Razor template``() =
  let simpleFsx = __SOURCE_DIRECTORY__ </> "files/simple.fsx"
  use temp = new TempFile()
  RazorLiterate.ProcessScriptFile
    ( simpleFsx, templateCsHtml, temp.File,
      layoutRoots = [__SOURCE_DIRECTORY__ </> "files"] )
  temp.Content |> shouldContainText "</a>"
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"
// --------------------------------------------------------------------------------------
// Test processing simple files using the NuGet included templates
// --------------------------------------------------------------------------------------

let info =
  [ "project-name", "FSharp.ProjectScaffold"
    "project-author", "Your Name"
    "project-summary", "A short summary of your project"
    "project-github", "http://github.com/pblasucci/fsharp-project-scaffold"
    "project-nuget", "http://nuget.com/packages/FSharp.ProjectScaffold"
    "root", "http://fsprojects.github.io/FSharp.FSharp.ProjectScaffold" ]

let docPageTemplate = __SOURCE_DIRECTORY__ </> "../../misc/templates/docpage.cshtml"

[<Test>][<Ignore "RazorEngine keeps breaking on template compilation in addition to underlying Issues with FSI configuration need to be addressed">]
let ``Can process fsx file using the template included in NuGet package``() =
  let simpleFsx = __SOURCE_DIRECTORY__ </> "files/simple.fsx"
  use temp = new TempFile()
  RazorLiterate.ProcessScriptFile
    ( simpleFsx, docPageTemplate, temp.File,
      layoutRoots = [__SOURCE_DIRECTORY__ </> "../../misc/templates"], replacements = info)
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"

[<Test>][<Ignore "RazorEngine keeps breaking on template compilation in addition to underlying Issues with FSI configuration need to be addressed">]
let ``Can process md file using the template included in NuGet package``() =
  let simpleMd = __SOURCE_DIRECTORY__ </> "files/simple.md"
  use temp = new TempFile()
  RazorLiterate.ProcessMarkdown
    ( simpleMd, docPageTemplate, temp.File,
      layoutRoots = [__SOURCE_DIRECTORY__ </> "../../misc/templates"], replacements = info)
  temp.Content |> shouldContainText "val hello : string"
  temp.Content |> shouldContainText "<title>Heading"

#endif

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
  let html = Literate.WriteHtml(doc)
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
  let html = Literate.WriteHtml(doc)
  html.IndexOf("First") < html.IndexOf("Second") |> shouldEqual true


[<Test>]
let ``Can split formatted document and formatted tool tips`` () =
  let content = """(**
hello
*)
let test = 42
"""
  let doc = Literate.ParseScriptString(content, "." </> "A.fsx", getFormatAgent())
  let doc2 = Literate.FormatLiterateNodes(doc,format=OutputKind.Html)
  let html = Literate.WriteHtml(doc2.With(formattedTips=""))
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
  Literate.WriteHtml(doc).Length |> should (be greaterThan) 0
