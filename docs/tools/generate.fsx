// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

#I __SOURCE_DIRECTORY__
#I "../../src/FSharp.Formatting/bin/Release/netstandard2.0"
#r "FSharp.Formatting.CodeFormat.dll"
#r "FSharp.Formatting.Literate.dll"
#r "FSharp.Formatting.Markdown.dll"
#r "FSharp.Formatting.ApiDocs.dll"
#r "FSharp.Formatting.Common.dll"

open System
open System.IO
open FSharp.Formatting
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------
let subDirectories (dir: string) = Directory.EnumerateDirectories dir 

let rec copyRecursive dir1 dir2 =
  Directory.CreateDirectory dir2 |> ignore
  for subdir1 in Directory.EnumerateDirectories dir1 do
       let subdir2 = Path.Combine(dir2, Path.GetFileName subdir1)
       copyRecursive subdir1 subdir2
  for file in Directory.EnumerateFiles dir1 do
       File.Copy(file, file.Replace(dir1, dir2), true)

// --------------------------------------------------------------------------------------
// Settings
// --------------------------------------------------------------------------------------

// Web site location for the generated documentation
#if TESTING
let website = __SOURCE_DIRECTORY__ + "/../output" |> Path.GetFullPath |> Uri |> string
#else
let website = "/FSharp.Formatting"
#endif

let githubLink = "http://github.com/fsprojects/FSharp.Formatting"

// Specify more information about your project
let info =
  [ "project-name", "FSharp.Formatting"
    "project-author", "Tomas Petricek"
    "project-summary", "A package for building great F# documentation, samples and blogs"
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/FSharp.Formatting" ]

let referenceBinaries =
  [ "FSharp.Formatting.CodeFormat.dll"; "FSharp.Formatting.Literate.dll"; "FSharp.Formatting.Markdown.dll"; "FSharp.Formatting.ApiDocs.dll"; "FSharp.Formatting.Common.dll" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
let root = website

Directory.SetCurrentDirectory (__SOURCE_DIRECTORY__)

// Paths with template/source/output locations
let bin        = "../../src/FSharp.Formatting/bin/Release/netstandard2.0"
let content    = "../content"
let output     = "../output"
let files      = "../files"
let templates  = "."
let docTemplate = templates + "/template.html"
let refTemplate = templates + "/reference/template.html"
let referenceOut = (output + "/" + "reference")

// Copy static files and CSS + JS from F# Formatting
let copyFiles () = copyRecursive files output

let binaries =
    referenceBinaries
    |> List.ofSeq
    |> List.map (fun b -> bin + "/" + b)

let libDirs = [bin]

// Build API reference from XML comments
let buildReference () =
  printfn "building reference docs..."
  if Directory.Exists referenceOut then Directory.Delete(referenceOut, true)
  Directory.CreateDirectory referenceOut |> ignore
  ApiDocs.GenerateHtml
    (binaries, output + "/" + "reference",
     template = refTemplate,
     parameters = ("root", root)::info,
     sourceRepo = githubLink + "/" + "tree/master",
     sourceFolder = __SOURCE_DIRECTORY__ + "/" + ".." + "/" + "..",
     publicOnly = true, libDirs = libDirs)

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  printfn "building docs..."
  Literate.ConvertDirectory
      (content, template=docTemplate, outputDirectory=output,
       replacements = ("root", root)::info,
       generateAnchors = true,
       processRecursive = false,
       includeSource = true)

// Generate
copyFiles()
buildDocumentation()
buildReference()
