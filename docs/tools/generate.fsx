System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
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


#I "../../packages/FAKE/tools/"
#I "../../packages/FSharp.Compiler.Service/lib/net45/"
#I "../../bin"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
#r "FSharp.Compiler.Service.dll"
#r "RazorEngine.dll"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"

// Ensure that FSharpVSPowerTools.Core.dll is loaded before trying to load FSharp.CodeFormat.dll
;;

#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "FSharp.Formatting.Razor.dll"



open Fake
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators

//#load "../../packages/FSharp.Formatting/FSharp.Formatting.fsx"

open FSharp.Literate
open FSharp.MetadataFormat
open FSharp.Formatting.Razor

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
DirectoryInfo.getSubDirectories (System.IO.DirectoryInfo templates)
|> Seq.iter (fun d ->
    let name = d.Name
    if name.Length = 2 || name.Length = 3 then
        layoutRootsAll.Add(
            name, [ templates @@ name
                    formatting @@ "templates"
                    formatting @@ "templates/reference" ]))

//let fsiEvaluator = lazy (Some (FsiEvaluator() :> IFsiEvaluator))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  Shell.copyRecursive files output true |> Fake.Core.Trace.logItems  "Copying file: "
  Directory.ensure (output @@ "content")
  //CopyRecursive (formatting @@ "styles") (output @@ "content") true
  //  |> Log "Copying styles and scripts: "

let binaries =
    referenceBinaries
    |> List.ofSeq
    |> List.map (fun b -> bin @@ b)

let libDirs = [bin]

// Build API reference from XML comments
let buildReference () =
  Shell.cleanDir (output @@ "reference")
  RazorMetadataFormat.Generate
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
    RazorLiterate.ProcessDirectory
      ( dir, template, output @@ sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots,
        generateAnchors = true,
        processRecursive = false,
        includeSource = true
         // Only needed for 'side-by-side' pages, but does not hurt others
      )
       //, ?fsiEvaluator = fsiEvaluator.Value ) // Currently we don't need it but it's a good stress test to have it here.

let watch () =
  printfn "Starting watching by initial building..."
  let rebuildDocs () =
    Shell.cleanDir output // Just in case the template changed (buildDocumentation is caching internally, maybe we should remove that)
    copyFiles()
    buildReference()
    buildDocumentation()
  rebuildDocs()
  printfn "Watching for changes..."

  let full s = Path.getFullName s
  let queue = new System.Collections.Concurrent.ConcurrentQueue<_>()
  let processTask () =
    async {
      let! tok = Async.CancellationToken
      while not tok.IsCancellationRequested do
        try
          if queue.IsEmpty then
            do! Async.Sleep 1000
          else
            let mutable data = []
            let mutable hasData = true
            while hasData do
              match queue.TryDequeue() with
              | true, d ->
                data <- d::data
              | _ ->
                hasData <- false

            printfn "Detected changes (%A). Invalidate cache and rebuild." data
            FSharp.Formatting.Razor.RazorEngineCache.InvalidateCache (data |> Seq.map (fun change -> change.FullPath))
            rebuildDocs()
            printfn "Documentation generation finished."
        with e ->
          printfn "Documentation generation failed: %O" e
    }

  use watcher =
    !! (full content + "/*.*")
    ++ (full templates + "/*.*")
    ++ (full files + "/*.*")
    ++ (full formatting + "templates/*.*")
    |> ChangeWatcher.run (fun changes ->
      changes |> Seq.iter queue.Enqueue)
  use source = new System.Threading.CancellationTokenSource()
  Async.Start(processTask (), source.Token)
  printfn "Press enter to exit watching..."
  System.Console.ReadLine() |> ignore
  watcher.Dispose()
  source.Cancel()

// Generate
#if HELP
copyFiles()
buildDocumentation()
#endif
#if REFERENCE
copyFiles()
buildReference()
#endif
#if WATCH
watch()
#endif