// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

#I __SOURCE_DIRECTORY__
#I "../../bin/netstandard2.0"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Literate.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.MetadataFormat.dll"
#r "FSharp.Formatting.Common.dll"
#r "Microsoft.AspNetCore.Razor.dll"
#r "RazorEngine.NetCore.dll"
#r "FSharp.Formatting.Razor.dll"
//#load "../../packages/FSharp.Formatting/FSharp.Formatting.fsx"

open System.IO
open FSharp.Formatting.Razor

// --------------------------------------------------------------------------------------
// Helpers
// --------------------------------------------------------------------------------------
let subDirectories (dir: string) = Directory.EnumerateDirectories dir 

let rec copyRecursive dir1 dir2 = 
  Directory.CreateDirectory dir2 |> ignore
  for subdir1 in Directory.EnumerateDirectories dir1 do
       let subdir2 = Path.Combine(dir2, Path.GetDirectoryName subdir1)
       copyRecursive subdir1 subdir2
  for file in Directory.EnumerateFiles dir1 do
       File.Copy(file, file.Replace(dir1, dir2), true)

// --------------------------------------------------------------------------------------
// Settings
// --------------------------------------------------------------------------------------

// Web site location for the generated documentation
#if TESTING
let website = __SOURCE_DIRECTORY__ + "../output"
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
  [ "FSharp.CodeFormat.dll"; "FSharp.Literate.dll"; "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll"; "FSharp.Formatting.Common.dll" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ + "/" + "../output")
#endif

Directory.SetCurrentDirectory (__SOURCE_DIRECTORY__)

// Paths with template/source/output locations
let bin        = "../../bin"
let content    = "../content"
let output     = "../output"
let files      = "../files"
let templates  = "."
let formatting = "../../misc/"
let docTemplate = formatting + "/" + "templates/docpage.cshtml"
let docTemplateSbS = templates + "/" + "docpage-sidebyside.cshtml"
let referenceOut = (output + "/" + "reference")

// Where to look for *.csproj templates (in this order)
let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting + "/" + "templates"
                          formatting + "/" + "templates/reference" ])
subDirectories templates
|> Seq.iter (fun name ->
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates + "/" + name
                                   formatting + "/" + "templates"
                                   formatting + "/" + "templates/reference" ]))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  copyRecursive files output 
  Directory.CreateDirectory (output + "/" + "content") |> ignore
  //CopyRecursive (formatting + "/" + "styles") (output + "/" + "content") true
  //  |> Log "Copying styles and scripts: "

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
  RazorMetadataFormat.Generate
    ( binaries, output + "/" + "reference", layoutRootsAll.["en"],
      parameters = ("root", root)::info,
      sourceRepo = githubLink + "/" + "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ + "/" + ".." + "/" + "..",
      publicOnly = true, libDirs = libDirs)

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  printfn "building docs..."
  let subdirs =
    [ content + "/" + "sidebyside", docTemplateSbS
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
    RazorLiterate.ProcessDirectory
      ( dir, template, output + "/" + sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,
        generateAnchors = true,
        processRecursive = false,
        includeSource = true
      )

// Generate
copyFiles()
buildDocumentation()
buildReference()
