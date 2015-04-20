#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Markdown.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#load "../Common/MarkdownUnit.fs"
#load "Setup.fs"
#else
module FSharp.Literate.Tests.Simple
#endif

open FsUnit
open FSharp.Literate
open FSharp.Markdown
open FSharp.Markdown.Unit
open NUnit.Framework
open FSharp.Literate.Tests.Setup

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

b""", __SOURCE_DIRECTORY__ @@ "Test.fsx")
    //[/test]
  doc.Paragraphs |> shouldMatchPar (function Paragraph [Literal "a"] -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function Paragraph [Literal "b"] -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function 
    | EmbedParagraphs(:? LiterateParagraph as cd) ->
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
  let doc = Literate.ParseScriptString(content, "C" @@ "A.fsx", getFormatAgent())
  doc.Errors |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph [Strong [Literal "hello"]] -> true | _ -> false) 

[<Test>]
let ``Can parse heading on the same line as opnening comment (#147)`` () =
  let content = """
(** ## Heading
content *)
let test = 42"""
  let doc = Literate.ParseScriptString(content, "C" @@ "A.fsx", getFormatAgent())
  doc.Paragraphs |> shouldMatchPar (function
    | Heading(2, [Literal "Heading"]) -> true | _ -> false)

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
    | Paragraph [Strong [Literal "hello"]] -> true | _ -> false)

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
    | Paragraph [Strong [Literal "hello"]] -> true | _ -> false)

[<Test>]
let ``Can generate references from indirect links`` () =
  let content = """
(** 
some [link][ref] to

  [ref]: http://there "Author: Article"
*)"""
  let doc = Literate.ParseScriptString(content, "C" @@ "A.fsx", getFormatAgent(), references=true)
  doc.Paragraphs |> shouldMatchPar (function ListBlock(_, _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchSpan (function Literal("Article") -> true | _ -> false) 
  doc.Paragraphs |> shouldMatchSpan (function Literal(" - Author") -> true | _ -> false) 

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
  let doc = Literate.ParseScriptString(content, "C" @@ "A.fsx", getFormatAgent())
  doc.Errors |> Seq.length |> should be (greaterThan 0)

// --------------------------------------------------------------------------------------
// Formatting C# code snippets
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can format the var keyword in C# code snippet`` () =
  let content = """
hello

    [lang=csharp]
    var a = 10 < 10;"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain "<span class=\"k\">var</span>"

[<Test>]
let ``Can format the var keyword in C# code snippet using Github-flavoured`` () =
  let content = """
hello

```csharp
var a = 10 < 10;
```"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain "<span class=\"k\">var</span>"

[<Test>]
let ``Codeblock whitespace is preserved`` () =
  let doc = "```markup\r\n    test\r\n    blub\r\n```\r\n";
  let expected = "<pre lang=\"markup\">    test\r\n    blub\r\n</pre>" |> properNewLines;
  let doc = Literate.ParseMarkdownString(doc, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain expected

[<Test>]
let ``Correctly handles Norwegian letters in SQL code block (#249)`` () =
  let content = """
    [lang=sql]
    Æøå"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain ">Æøå<"

[<Test>]
let ``Correctly handles apostrophes in JS code block (#213)`` () =
  let content = """
    [lang=js]
    var but = 'I\'m not so good...';"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain @"'I\'m not so good...'"

[<Test>]
let ``Correctly encodes special HTML characters (<, >, &) in code`` () =
  let content = """
    [lang=js]
    var pre = "<a> & <b>";"""
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain "&lt;a&gt; &amp; &lt;b&gt;"

[<Test>]
let ``Correctly encodes already encoded HTML entities and tags`` () =
  let content = """
    [lang=js]
    "&amp;" + "<em>" + "&quot;"; """
  let doc = Literate.ParseMarkdownString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc)
  html |> should contain "&amp;amp;"
  html |> should contain "&amp;quot;"
  html |> should contain "&lt;em&gt;"

[<Test>]
let ``Generates line numbers for F# code snippets`` () =
  let content = """
(** Hello *)
let a1 = 1
let a2 = 2"""
  let doc = Literate.ParseScriptString(content, formatAgent=getFormatAgent())
  let html = Literate.WriteHtml(doc, lineNumbers=true)
  html |> should contain "<p>Hello</p>"
  html |> should contain "1:"
  html |> should contain "2:"
  html |> should notContain "3:"

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
  html |> should contain "<p>Hello</p>"
  html |> should contain "1:"
  html |> should contain "2:"
  html |> should notContain "3:"

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

let templateHtml = __SOURCE_DIRECTORY__ @@ "files/template.html"
let templateCsHtml = __SOURCE_DIRECTORY__ @@ "files/template.cshtml"

[<Test>]
let ``Code and HTML is formatted with a tooltip in Markdown file using HTML template``() = 
  let simpleMd = __SOURCE_DIRECTORY__ @@ "files/simple.md"
  use temp = new TempFile()
  Literate.ProcessMarkdown(simpleMd, templateHtml, temp.File)
  temp.Content |> should contain "</a>"
  temp.Content |> should contain "val hello : string"

[<Test>]
let ``Code and HTML is formatted with a tooltip in F# Script file using HTML template``() =
  let simpleFsx = __SOURCE_DIRECTORY__ @@ "files/simple.fsx"
  use temp = new TempFile()
  Literate.ProcessScriptFile(simpleFsx, templateHtml, temp.File)
  temp.Content |> should contain "</a>"
  temp.Content |> should contain "val hello : string"

[<Test>]
let ``Code and HTML is formatted with a tooltip in F# Script file using Razor template``() =
  let simpleFsx = __SOURCE_DIRECTORY__ @@ "files/simple.fsx"
  use temp = new TempFile()
  Literate.ProcessScriptFile
    ( simpleFsx, templateCsHtml, temp.File, 
      layoutRoots = [__SOURCE_DIRECTORY__ @@ "files"] )
  temp.Content |> should contain "</a>"
  temp.Content |> should contain "val hello : string"
  temp.Content |> should contain "<title>Heading"

// --------------------------------------------------------------------------------------
// Test processing simple files using the NuGet included templates
// --------------------------------------------------------------------------------------

let info =
  [ "project-name", "FSharp.ProjectScaffold"
    "project-author", "Your Name"
    "project-summary", "A short summary of your project"
    "project-github", "http://github.com/pblasucci/fsharp-project-scaffold"
    "project-nuget", "http://nuget.com/packages/FSharp.ProjectScaffold"
    "root", "http://tpetricek.github.io/FSharp.FSharp.ProjectScaffold" ]

let docPageTemplate = __SOURCE_DIRECTORY__ @@ "../../misc/templates/docpage.cshtml"

[<Test>]
let ``Can process fsx file using the template included in NuGet package``() =
  let simpleFsx = __SOURCE_DIRECTORY__ @@ "files/simple.fsx"
  use temp = new TempFile()
  Literate.ProcessScriptFile
    ( simpleFsx, docPageTemplate, temp.File, 
      layoutRoots = [__SOURCE_DIRECTORY__ @@ "../../misc/templates"], replacements = info)
  temp.Content |> should contain "val hello : string"
  temp.Content |> should contain "<title>Heading"

[<Test>]
let ``Can process md file using the template included in NuGet package``() =
  let simpleMd = __SOURCE_DIRECTORY__ @@ "files/simple.md"
  use temp = new TempFile()
  Literate.ProcessMarkdown
    ( simpleMd, docPageTemplate, temp.File, 
      layoutRoots = [__SOURCE_DIRECTORY__ @@ "../../misc/templates"], replacements = info)
  temp.Content |> should contain "val hello : string"
  temp.Content |> should contain "<title>Heading"


[<Test>]
let ``Gives nice error when parsing unclosed comment`` () =
  let content = """
(** **hello** 
let test = 42"""
  try
    Literate.ParseScriptString(content, "C" @@ "A.fsx", getFormatAgent()) |> ignore
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
  html |> should contain "Some"


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
  let doc = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent())
  let doc2 = Literate.FormatLiterateNodes(doc,format=OutputKind.Html)
  let html = Literate.WriteHtml(doc2.With(formattedTips=""))
  let tips = doc2.FormattedTips
  tips |> should contain "test : int"
  html |> should notContain "test : int"
  html |> should contain "hello"