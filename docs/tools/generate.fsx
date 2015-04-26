﻿// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Web site location for the generated documentation
let website = "/FSharp.Formatting"

let githubLink = "http://github.com/tpetricek/FSharp.Formatting"

// Specify more information about your project
let info =
  [ "project-name", "FSharp.Formatting"
    "project-author", "Tomas Petricek"
    "project-summary", "A package for building great F# documentation, samples and blogs"
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/FSharp.Formatting" ]

let referenceBinaries = 
  [ "FSharp.CodeFormat.dll"; "FSharp.Literate.dll"; "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------


#I "../../packages/FAKE/tools/"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper

// This is needed to bootstrap ourself (make sure we have the same environment while building as our users) ...
// If you came here from the nuspec file add your file.
// If you add files here to make the CI happy add those files to the .nuspec file as well
// TODO: INSTEAD build the nuspec file before generating the documentation and extract it...
ensureDirectory (__SOURCE_DIRECTORY__ @@ "../../packages/FSharp.Formatting/lib/net40")
let buildFiles = [ "CSharpFormat.dll"; "FSharp.CodeFormat.dll"; "FSharp.Literate.dll"
                   "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll"; "RazorEngine.dll";
                   "System.Web.Razor.dll"; "FSharpVSPowerTools.Core.dll"; "FSharp.Compiler.Service.dll" ]
let bundledFiles =
  buildFiles
  |> List.map (fun f -> 
      __SOURCE_DIRECTORY__ @@ sprintf "../../bin/%s" f, 
      __SOURCE_DIRECTORY__ @@ sprintf "../../packages/FSharp.Formatting/lib/net40/%s" f)
for source, dest in bundledFiles do File.Copy(source, dest, true)
#load "../../packages/FSharp.Formatting/FSharp.Formatting.fsx"

open FSharp.Literate
open FSharp.MetadataFormat

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ @@ "../output")
#endif

System.IO.Directory.SetCurrentDirectory (__SOURCE_DIRECTORY__)

// Paths with template/source/output locations
let bin        = "../../bin"
let content    = "../content"
let output     = "../output"
let files      = "../files"
let templates  = "." 
let formatting = "../../misc/"
let docTemplate = formatting @@ "templates/docpage.cshtml"
let docTemplateSbS = templates @@ "docpage-sidebyside.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting @@ "templates"
                          formatting @@ "templates/reference" ])
subDirectories (directoryInfo templates)
|> Seq.iter (fun d ->
                let name = d.Name
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates @@ name
                                   formatting @@ "templates"
                                   formatting @@ "templates/reference" ]))

let fsiEvaluator = lazy (Some (FsiEvaluator() :> IFsiEvaluator))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  //CopyRecursive (formatting @@ "styles") (output @@ "content") true 
  //  |> Log "Copying styles and scripts: "

let binaries =
    referenceBinaries
    |> List.ofSeq
    |> List.map (fun b -> bin @@ b)

let libDirs = [bin]

// Build API reference from XML comments
let buildReference () =
  CleanDir (output @@ "reference")
  MetadataFormat.Generate
    ( binaries, output @@ "reference", layoutRootsAll.["en"],
      parameters = ("root", root)::info,
      sourceRepo = githubLink @@ "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
      publicOnly = true, libDirs = libDirs)

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs = 
    [ content @@ "sidebyside", docTemplateSbS
      content, docTemplate; ]
  for dir, template in subdirs do
    let sub = "." // Everything goes into the same output directory here
    let langSpecificPath(lang, path:string) =
        path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.exists(fun i -> i = lang)
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    Literate.ProcessDirectory
      ( dir, template, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,
        generateAnchors = true,
        processRecursive = false,
        includeSource = true, // Only needed for 'side-by-side' pages, but does not hurt others
        ?fsiEvaluator = fsiEvaluator.Value ) // Currently we don't need it but it's a good stress test to have it here.

// Generate
copyFiles()
#if HELP
buildDocumentation()
#endif
#if REFERENCE
buildReference()
#endif
