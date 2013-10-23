// --------------------------------------------------------------------------------------
// Builds the documentation from the files in the 'docs' directory
// --------------------------------------------------------------------------------------

#I "../../bin"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Literate.dll"
#r "FSharp.MetadataFormat.dll"
open System.IO
open FSharp.Literate
open FSharp.MetadataFormat

let (++) a b = Path.Combine(a, b)
let source = __SOURCE_DIRECTORY__
let template = source ++ "template.html"
let templateSideBySide = source ++ "template-sidebyside.html"
let sources = source ++ "../content"
let output = source ++ "../output"

// When running locally, you can use your path
//let root = @"file://C:\Tomas\FSharp.Formatting\generated"
let root = "http://tpetricek.github.io/FSharp.Formatting"

// Additional strings to be replaced in the HTML template
let projInfo =
  [ "page-author", "Tomas Petricek"
    "page-description", "A package for building great F# documentation, samples and blogs"
    "github-link", "http://github.com/tpetricek/FSharp.Formatting"
    "project-name", "F# Formatting"
    "root", root ]

// Compiler options (reference the two dll files and System.Web.dll)
let options = 
  "--reference:\"" + source + "/../../bin/FSharp.CompilerBinding.dll\" " +
  "--reference:\"" + source + "/../../bin/FSharp.CodeFormat.dll\" " +
  "--reference:\"" + source + "/../../bin/FSharp.Markdown.dll\" " +
  "--reference:System.Web.dll"

let generateDocs() =
  // Copy all sample data files to the "data" directory
  let copy = [ sources ++ "../files/misc", output ++ "misc"
               sources ++ "../files/content", output ++ "content" ]
  for source, target in copy do
    if Directory.Exists target then Directory.Delete(target, true)
    Directory.CreateDirectory target |> ignore
    for fileInfo in DirectoryInfo(source).EnumerateFiles() do
        fileInfo.CopyTo(target ++ fileInfo.Name) |> ignore

  // Now we can process the samples directory (with some additional references)
  // and then we clean up the files & directories we had to create earlier
  Literate.ProcessDirectory
    ( sources, template, output, OutputKind.Html, replacements = projInfo )

  // Process the sidebyside/script.fsx script separately
  let scriptInfo = projInfo @ [ "custom-title", "F# Script file: Side-by-side example" ]
  let changeTime = File.GetLastWriteTime(source ++ "../content/sidebyside/script.fsx")
  let generateTime = File.GetLastWriteTime(output ++ "sidescript.html")
  if changeTime > generateTime then
    printfn "Generating 'sidescript.html'"
    Literate.ProcessScriptFile
      ( Path.Combine(source, "../content/sidebyside/script.fsx"), templateSideBySide,
        Path.Combine(output, "sidescript.html"), 
        compilerOptions = options, replacements = scriptInfo, includeSource = true)

  // Process the sidebyside/markdown.md file separately
  let scriptInfo = projInfo @ [ "custom-title", "F# Markdown: Side-by-side example" ]
  let changeTime = File.GetLastWriteTime(source ++ "../content/sidebyside/markdown.md")
  let generateTime = File.GetLastWriteTime(output ++ "sidemarkdown.html")
  if changeTime > generateTime then
    printfn "Generating 'sidemarkdown.html'"
    Literate.ProcessMarkdown
      ( Path.Combine(source, "../content/sidebyside/markdown.md"), templateSideBySide,
        Path.Combine(output, "sidemarkdown.html"), 
        compilerOptions = options, replacements = scriptInfo, includeSource = true)


// Generate documentation
generateDocs()