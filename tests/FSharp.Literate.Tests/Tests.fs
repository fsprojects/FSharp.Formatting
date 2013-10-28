#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Markdown.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#load "../Common/MarkdownUnit.fs"
#else
module FSharp.Literate.Tests.Simple
#endif

open FsUnit
open System
open System.IO
open System.Reflection
open FSharp.Literate
open FSharp.Markdown
open FSharp.Markdown.Unit
open NUnit.Framework

// --------------------------------------------------------------------------------------
// Setup - find the compiler assembly etc.
// --------------------------------------------------------------------------------------

let (@@) a b = Path.Combine(a, b)

let compilerAsembly =
  let files = 
    [ @"%ProgramFiles%\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll"
      @"%ProgramFiles(x86)%\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll"
      @"%ProgramFiles(x86)%\Microsoft F#\v4.0\FSharp.Compiler.dll" ]
  files |> Seq.pick (fun file ->
    let file = Environment.ExpandEnvironmentVariables(file)
    if File.Exists(file) then Some(Assembly.LoadFile(file))
    else None)

type TempFile() =
  let file = Path.GetTempFileName()
  member x.File = file
  member x.Content = File.ReadAllText(file)
  interface IDisposable with
    member x.Dispose() = File.Delete(file)

// --------------------------------------------------------------------------------------
// Test standalone literate parsing
// --------------------------------------------------------------------------------------

let formatAgent = FSharp.CodeFormat.CodeFormat.CreateAgent(compilerAsembly)

[<Test>]
let ``Can parse and format literate F# script`` () =
  let content = """
(** **hello** *)
let test = 42"""
  let doc = Literate.ParseScriptString(content, "C:\\A.fsx", formatAgent)
  doc.Errors |> Seq.length |> shouldEqual 0
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph [Strong [Literal "hello"]] -> true | _ -> false)

[<Test>] 
let ``Can parse and format markdown with F# snippet`` () =
  let content = """
**hello**

    let test = 42"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = formatAgent)
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
  let doc = Literate.ParseScriptString(content, "C:\\A.fsx", formatAgent, references=true)
  doc.Paragraphs |> shouldMatchPar (function ListBlock(_, _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchSpan (function Literal("Article") -> true | _ -> false) 
  doc.Paragraphs |> shouldMatchSpan (function Literal(" - Author") -> true | _ -> false) 

[<Test>]
let ``Can report errors in F# code snippets (in F# script file)`` () =
  let content = """
**hello**

    let test = 4 + 1.0"""
  let doc = Literate.ParseMarkdownString(content, formatAgent = formatAgent)
  doc.Errors |> Seq.length |> should be (greaterThan 0)

[<Test>]
let ``Can report errors in F# code snippets (in Markdown document)`` () =
  let content = """
(** **hello** *)
let test = 4 + 1.0"""
  let doc = Literate.ParseScriptString(content, "C:\\A.fsx", formatAgent)
  doc.Errors |> Seq.length |> should be (greaterThan 0)

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
  let doc1 = Literate.ParseMarkdownString(simpleMd, formatAgent = formatAgent)
  let doc2 = Literate.ParseScriptString(simpleFsx, formatAgent = formatAgent)
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
  Literate.ProcessMarkdown(simpleMd, templateHtml, temp.File, fsharpCompiler=compilerAsembly)
  temp.Content |> should contain "</a>"
  temp.Content |> should contain "val hello : string"

[<Test>]
let ``Code and HTML is formatted with a tooltip in F# Script file using HTML template``() =
  let simpleFsx = __SOURCE_DIRECTORY__ @@ "files/simple.fsx"
  use temp = new TempFile()
  Literate.ProcessScriptFile(simpleFsx, templateHtml, temp.File, fsharpCompiler=compilerAsembly)
  temp.Content |> should contain "</a>"
  temp.Content |> should contain "val hello : string"

[<Test>]
let ``Code and HTML is formatted with a tooltip in F# Script file using Razor template``() =
  let simpleFsx = __SOURCE_DIRECTORY__ @@ "files/simple.fsx"
  use temp = new TempFile()
  Literate.ProcessScriptFile
    ( simpleFsx, templateCsHtml, temp.File, fsharpCompiler=compilerAsembly, 
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
    ( simpleFsx, docPageTemplate, temp.File, fsharpCompiler = compilerAsembly, 
      layoutRoots = [__SOURCE_DIRECTORY__ @@ "../../misc/templates"], replacements = info)
  temp.Content |> should contain "val hello : string"
  temp.Content |> should contain "<title>Heading"

[<Test>]
let ``Can process md file using the template included in NuGet package``() =
  let simpleMd = __SOURCE_DIRECTORY__ @@ "files/simple.md"
  use temp = new TempFile()
  Literate.ProcessMarkdown
    ( simpleMd, docPageTemplate, temp.File, fsharpCompiler = compilerAsembly, 
      layoutRoots = [__SOURCE_DIRECTORY__ @@ "../../misc/templates"], replacements = info)
  temp.Content |> should contain "val hello : string"
  temp.Content |> should contain "<title>Heading"
