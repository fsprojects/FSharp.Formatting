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
let binDir = source ++ "../../bin"

// When running locally, you can use your path
let buildRoots = 
  [ "github", "http://tpetricek.github.io/FSharp.Formatting"
    "local", sprintf "file://%s" (Path.GetFullPath(output ++ "local")) ]

// Additional strings to be replaced in the HTML template
let projInfo =
  [ "page-author", "Tomas Petricek"
    "project-author", "Tomas Petricek"
    "project-nuget", "https://www.nuget.org/packages/FSharp.Formatting"
    "project-github", "http://github.com/tpetricek/FSharp.Formatting"
    "page-description", "A package for building great F# documentation, samples and blogs"
    "github-link", "http://github.com/tpetricek/FSharp.Formatting"
    "project-name", "F# Formatting" ]

// How RazorEngine searches templates
let layoutRoots = 
  [ source; source ++ "reference" ]
// Compiler options (reference the two dll files and System.Web.dll)
let options = 
  "--reference:\"" + source + "/../../bin/FSharp.CompilerBinding.dll\" " +
  "--reference:\"" + source + "/../../bin/FSharp.CodeFormat.dll\" " +
  "--reference:\"" + source + "/../../bin/FSharp.Markdown.dll\" " +
  "--reference:System.Web.dll"

let generateDocs (outDirName, root) =
  let projInfo = ("root", root) :: projInfo
  let output = output ++ outDirName
  // Copy all sample data files to the "data" directory
  let copy = [ sources ++ "../files/misc", output ++ "misc"
               sources ++ "../files/content", output ++ "content" ]
  let rec copyRecursive source dest =
    Directory.CreateDirectory dest |> ignore
    for f in Directory.EnumerateFiles source do
      File.Copy(f, dest ++ Path.GetFileName f)
    for d in Directory.EnumerateDirectories source do
      copyRecursive d (dest ++ Path.GetFileName d)

  for source, target in copy do
    if Directory.Exists target then Directory.Delete(target, true)
    copyRecursive source target

  // Now we can process the samples directory (with some additional references)
  // and then we clean up the files & directories we had to create earlier
  Literate.ProcessDirectory
    ( sources, template, output, OutputKind.Html, replacements = projInfo, generateAnchors = true )

  // Process the side-by-side scripts and markdown documents separately
  let processScript input output scriptInfo =
    Literate.ProcessScriptFile
      ( input, templateSideBySide, output, compilerOptions = options,
        replacements = scriptInfo, includeSource = true, generateAnchors = true )
  let processMarkdown input output scriptInfo =
    Literate.ProcessMarkdown
      ( input, templateSideBySide, output, compilerOptions = options,
        replacements = scriptInfo, includeSource = true, generateAnchors = true )
  let sideBySideScripts = 
    [ "script.fsx", "sidescript.html", "F# Script file: Side-by-side example", processScript
      "extensions.md", "sideextensions.html", "F# Markdown: Formatting extensions", processMarkdown 
      "markdown.md", "sidemarkdown.html", "F# Markdown: Side-by-side example", processMarkdown ]
          
  for file, outFile, title, proc in sideBySideScripts do
    let scriptInfo = projInfo @ [ "custom-title", title ]
    let changeTime = File.GetLastWriteTime(source ++ ("../content/sidebyside/" + file))
    let generateTime = File.GetLastWriteTime(output ++ outFile)
    if not (File.Exists(output ++ outFile)) || (changeTime > generateTime) then
      printfn "Generating '%s'" outFile
      proc (source ++ ("../content/sidebyside/" + file)) (output ++ outFile) scriptInfo

  // Create API reference
  let binaries = 
    [ "FSharp.CodeFormat.dll"; "FSharp.Literate.dll"; "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll" ]
    |> Seq.map (fun b -> binDir ++ b)
    |> Seq.toList
  let referenceDir = Path.GetFullPath (output ++ "references")
  if not <| Directory.Exists referenceDir then Directory.CreateDirectory referenceDir |> ignore
  MetadataFormat.Generate
    ( binaries, referenceDir, layoutRoots,
      parameters = projInfo,
      libDirs = [ binDir ],
      //otherFlags = [],
      sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/blob/master/",
      sourceFolder = "../../",
      publicOnly = true,
      markDownComments = false)
      
// Generate documentation
buildRoots |> Seq.iter generateDocs

