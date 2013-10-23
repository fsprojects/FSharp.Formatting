#if INTERACTIVE
#I "../../bin"
#r "FSharp.MetadataFormat.dll"
#r "RazorEngine.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.MetadataFormat.Tests
#endif

open FsUnit
open System.IO
open NUnit.Framework
open FSharp.MetadataFormat

// --------------------------------------------------------------------------------------
// Run the metadata formatter on sample project 
// --------------------------------------------------------------------------------------

let (@@) a b = Path.Combine(a, b)

let library = 
  __SOURCE_DIRECTORY__ @@ "files" @@ "Deedle.dll"

let output  = 
  let tempFile = Path.GetTempFileName()
  File.Delete(tempFile)
  Directory.CreateDirectory(tempFile).FullName

let layoutRoots = 
  [ __SOURCE_DIRECTORY__ @@ "../../misc/templates"
    __SOURCE_DIRECTORY__ @@ "../../misc/templates/reference" ]

let info =
  [ "project-name", "FSharp.ProjectScaffold"
    "project-author", "Your Name"
    "project-summary", "A short summary of your project"
    "project-github", "http://github.com/pblasucci/fsharp-project-scaffold"
    "project-nuget", "http://nuget.com/packages/FSharp.ProjectScaffold"
    "project-website", "http://tpetricek.github.io/FSharp.FSharp.ProjectScaffold" ]

[<Test>]
let ``MetadataFormat works on sample Deedle assembly``() = 
  MetadataFormat.Generate(library, output, layoutRoots, info)
  let files = Directory.GetFiles(output)
  
  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith "index.html")
  optIndex.IsSome |> shouldEqual true
  
  let optSeriesMod = files |> Seq.tryFind (fun s -> s.Contains "seriesmodule")
  optSeriesMod.IsSome |> shouldEqual true

#if INTERACTIVE
System.Diagnostics.Process.Start(output)
#endif

