#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Literate.Tests.Simple
#endif

open FsUnit
open System
open System.IO
open System.Reflection
open FSharp.Literate
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
  temp.Content |> should contain "<title>FSharp.ProjectScaffold"

[<Test>]
let ``Can process md file using the template included in NuGet package``() =
  let simpleMd = __SOURCE_DIRECTORY__ @@ "files/simple.md"
  use temp = new TempFile()
  Literate.ProcessMarkdown
    ( simpleMd, docPageTemplate, temp.File, fsharpCompiler = compilerAsembly, 
      layoutRoots = [__SOURCE_DIRECTORY__ @@ "../../misc/templates"], replacements = info)
  temp.Content |> should contain "val hello : string"
  temp.Content |> should contain "<title>FSharp.ProjectScaffold"
