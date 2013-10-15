// Given a typical setup (with 'FSharp.Formatting' referenced using NuGet),
// the following will include binaries and load the literate script
#I "../lib/net40/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll" // TODO: For generating docs from XML comments.. (work in progress)
open System.IO
open FSharp.Literate

// Create output directories & copy content files there
// (We have two sets of samples in "output" and "output-all" directories,
//  for simplicitly, this just creates them & copies content there)
if not (Directory.Exists(__SOURCE_DIRECTORY__ + "\\output")) then
  Directory.CreateDirectory(__SOURCE_DIRECTORY__ + "\\output") |> ignore
  Directory.CreateDirectory (__SOURCE_DIRECTORY__ + "\\output\\content") |> ignore
if not (Directory.Exists(__SOURCE_DIRECTORY__ + "\\output-all")) then
  Directory.CreateDirectory(__SOURCE_DIRECTORY__ + "\\output-all") |> ignore
  Directory.CreateDirectory (__SOURCE_DIRECTORY__ + "\\output-all\\content") |> ignore
for fileInfo in DirectoryInfo(__SOURCE_DIRECTORY__ + "\\content").EnumerateFiles() do
  fileInfo.CopyTo(Path.Combine(__SOURCE_DIRECTORY__ + "\\output\\content", fileInfo.Name)) |> ignore
  fileInfo.CopyTo(Path.Combine(__SOURCE_DIRECTORY__ + "\\output-all\\content", fileInfo.Name)) |> ignore

/// This functions processes a single F# Script file
let processScript templateFile outputKind =
  let file = __SOURCE_DIRECTORY__ + "\\demo.fsx"
  let output = __SOURCE_DIRECTORY__ + "\\output\\demo-script." + (outputKind.ToString())
  let template = __SOURCE_DIRECTORY__ + templateFile
  Literate.ProcessScriptFile(file, template, output, format = outputKind)

/// This functions processes a single Markdown document
let processDocument templateFile outputKind =
  let file = __SOURCE_DIRECTORY__ + "\\demo.md"
  let output = __SOURCE_DIRECTORY__ + "\\output\\demo-markdown." + (outputKind.ToString())
  let template = __SOURCE_DIRECTORY__ + templateFile
  Literate.ProcessMarkdown(file, template, output, format = outputKind)

/// This functions processes an entire directory containing
/// multiple script files (*.fsx) and Markdown documents (*.md)
/// and it specifies additional replacements for the template file
let processDirectory() =
  let dir = __SOURCE_DIRECTORY__
  let template = __SOURCE_DIRECTORY__ + "\\templates\\template-project.html"
  let projInfo =
    [ "page-description", "F# Literate Programming"
      "page-author", "Tomas Petricek"
      "github-link", "https://github.com/tpetricek/FSharp.Formatting"
      "project-name", "F# Formatting" ]

  Literate.ProcessDirectory
    ( dir, template, dir + "\\output-all", OutputKind.Html, 
      replacements = projInfo)

// Generate output for sample scripts & documents in both HTML & Latex
processScript "\\templates\\template-file.html" OutputKind.Html
processDocument "\\templates\\template-file.html" OutputKind.Html
processScript "\\templates\\template-color.tex" OutputKind.Latex
processDocument "\\templates\\template-color.tex" OutputKind.Latex

// Or, automatically process all files in the current directory (as HTML)
processDirectory()