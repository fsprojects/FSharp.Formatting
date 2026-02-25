namespace fsdocs

open System.Collections.Concurrent
open CommandLine

open System
open System.Diagnostics
open System.IO
open System.Globalization
open System.Net
open System.Reflection
open System.Text

open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open fsdocs.Common
open FSharp.Formatting.Templating

open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Operators
open Suave.Filters
open Suave.Logging
open FSharp.Formatting.Markdown

#nowarn "44" // Obsolete WebClient

type CoreBuildOptions(watch) =

    [<Option("input", Required = false, Default = "docs", HelpText = "Input directory of documentation content.")>]
    member val input = "" with get, set

    [<Option("projects",
             Required = false,
             HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member val projects = Seq.empty<string> with get, set

    [<Option("output",
             Required = false,
             HelpText = "Output Folder (default 'output' for 'build' and 'tmp/watch' for 'watch'.")>]
    member val output = "" with get, set

    [<Option("noapidocs", Default = false, Required = false, HelpText = "Disable generation of API docs.")>]
    member val noapidocs = false with get, set

    [<Option("ignoreuncategorized",
             Default = false,
             Required = false,
             HelpText = "Disable generation of 'Other' category for uncategorized docs.")>]
    member val ignoreuncategorized = false with get, set

    [<Option("ignoreprojects", Default = false, Required = false, HelpText = "Disable project cracking.")>]
    member val ignoreprojects = false with get, set

    [<Option("strict", Default = false, Required = false, HelpText = "Fail if there is a problem generating docs.")>]
    member val strict = false with get, set

    [<Option("eval", Default = false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = false with get, set

    [<Option("qualify",
             Default = false,
             Required = false,
             HelpText =
                 "In API doc generation qualify the output by the collection name, e.g. 'reference/FSharp.Core/...' instead of 'reference/...' .")>]
    member val qualify = false with get, set

    [<Option("saveimages",
             Default = "none",
             Required = false,
             HelpText =
                 "Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs.")>]
    member val saveImages = "none" with get, set

    [<Option("sourcefolder",
             Required = false,
             HelpText =
                 "Source folder at time of component build (defaults to value of `<FsDocsSourceFolder>` from project file, else current directory)")>]
    member val sourceFolder = "" with get, set

    [<Option("sourcerepo",
             Required = false,
             HelpText =
                 "Source repository for github links (defaults to value of `<FsDocsSourceRepository>` from project file, else `<RepositoryUrl>/tree/<RepositoryBranch>` for Git repositories)")>]
    member val sourceRepo = "" with get, set

    [<Option("linenumbers", Default = false, Required = false, HelpText = "Add line numbers.")>]
    member val linenumbers = false with get, set

    [<Option("nonpublic",
             Default = false,
             Required = false,
             HelpText = "The tool will also generate documentation for non-public members")>]
    member val nonpublic = false with get, set

    [<Option("mdcomments",
             Default = false,
             Required = false,
             HelpText =
                 "Assume /// comments in F# code are markdown style (defaults to value of `<UsesMarkdownComments>` from project file)")>]
    member val mdcomments = false with get, set

    [<Option("parameters",
             Required = false,
             HelpText = "Additional substitution substitutions for templates, e.g. --parameters key1 value1 key2 value2")>]
    member val parameters = Seq.empty<string> with get, set

    [<Option("nodefaultcontent",
             Required = false,
             HelpText = "Do not copy default content styles, javascript or use default templates.")>]
    member val nodefaultcontent = false with get, set

    [<Option("properties",
             Required = false,
             HelpText = "Provide properties to dotnet msbuild, e.g. --properties Configuration=Release Version=3.4")>]
    member val extraMsbuildProperties = Seq.empty<string> with get, set

    [<Option("fscoptions",
             Required = false,
             HelpText = "Extra flags for F# compiler analysis, e.g. dependency resolution.")>]
    member val fscoptions = Seq.empty<string> with get, set

    [<Option("clean", Required = false, Default = false, HelpText = "Clean the output directory.")>]
    member val clean = false with get, set

    member this.Execute() =

        let onError msg =
            if this.strict then
                printfn "%s" msg
                exit 1

        let protect phase f =
            try
                f ()
                true
            with ex ->
                printfn "Error : \n%O" ex

                onError (sprintf "%s failed, and --strict is on : \n%O" phase ex)
                false

        /// The substitutions as given by the user
        let userParameters =
            let parameters = Array.ofSeq this.parameters

            if parameters.Length % 2 = 1 then
                printfn "The --parameters option's arguments' count has to be an even number"
                exit 1

            evalPairwiseStringsNoOption parameters
            |> List.map (fun (a, b) -> (ParamKey a, b))

        let userParametersDict = readOnlyDict userParameters

        // Adjust the user substitutions for 'watch' mode root
        let userRoot, userParameters =
            if watch then
                let userRoot = sprintf "http://localhost:%d/" this.port_option

                if userParametersDict.ContainsKey(ParamKeys.root) then
                    printfn "ignoring user-specified root since in watch mode, root = %s" userRoot

                let userParameters =
                    [ ParamKeys.root, userRoot ]
                    @ (userParameters |> List.filter (fun (a, _) -> a <> ParamKeys.root))

                Some userRoot, userParameters
            else
                let r =
                    match userParametersDict.TryGetValue(ParamKeys.root) with
                    | true, v -> Some v
                    | _ -> None

                r, userParameters

        let userCollectionName =
            match (dict userParameters).TryGetValue(ParamKeys.``fsdocs-collection-name``) with
            | true, v -> Some v
            | _ -> None

        // See https://github.com/ionide/proj-info/issues/123
        let prevDotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")

        let (root, collectionName, crackedProjects, paths, docsSubstitutions), _key =
            let projects = Seq.toList this.projects
            let cacheFile = ".fsdocs/cache"

            let getTime (p: string) =
                try
                    File.GetLastWriteTimeUtc(p)
                with _ ->
                    DateTime.Now

            let key1 =
                (userRoot,
                 this.parameters,
                 projects,
                 getTime (typeof<CoreBuildOptions>.Assembly.Location),
                 (projects |> List.map getTime |> List.toArray))

            Utils.cacheBinary cacheFile (fun (_, key2) -> key1 = key2) (fun () ->
                let props =
                    this.extraMsbuildProperties
                    |> Seq.toList
                    |> List.map (fun s ->
                        let arr = s.Split("=")

                        if arr.Length > 1 then
                            arr.[0], String.concat "=" arr.[1..]
                        else
                            failwith "properties must be of the form 'PropName=PropValue'")

                Crack.crackProjects (
                    onError,
                    props,
                    userRoot,
                    userCollectionName,
                    userParameters,
                    projects,
                    this.ignoreprojects
                ),
                key1)

        // See https://github.com/ionide/proj-info/issues/123
        System.Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", prevDotnetHostPath)

        if crackedProjects.Length > 0 then
            printfn ""
            printfn "Inputs for API Docs:"

            for (dllFile, _, _, _, _, _, _, _, _, _) in crackedProjects do
                printfn "    %s" dllFile

        //printfn "Comand lines for API Docs:"
        //for (_, runArguments, _, _, _, _, _, _, _, _) in crackedProjects do
        //    printfn "    %O" runArguments

        for (dllFile, _, _, _, _, _, _, _, _, _) in crackedProjects do
            if not (File.Exists dllFile) then
                let msg =
                    sprintf
                        "*** %s does not exist, has it been built? You may need to provide --properties Configuration=Release."
                        dllFile

                if this.strict then failwith msg else printfn "%s" msg

        if crackedProjects.Length > 0 then
            printfn ""
            printfn "Substitutions/parameters:"
            // Print the substitutions
            for (ParamKey pk, p) in docsSubstitutions do
                printfn "  %s --> %s" pk p

            // The substitutions may differ for some projects due to different settings in the project files, if so show that
            let pd = dict docsSubstitutions

            for (dllFile, _, _, _, _, _, _, _, _, projectParameters) in crackedProjects do
                for (((ParamKey pkv2) as pk2), p2) in projectParameters do
                    if pd.ContainsKey pk2 && pd.[pk2] <> p2 then
                        printfn "  (%s) %s --> %s" (Path.GetFileNameWithoutExtension(dllFile)) pkv2 p2

        let apiDocInputs =
            [ for (dllFile,
                   _,
                   repoUrlOption,
                   repoBranchOption,
                   repoTypeOption,
                   projectMarkdownComments,
                   projectWarn,
                   projectSourceFolder,
                   projectSourceRepo,
                   projectParameters) in crackedProjects ->
                  let sourceRepo =
                      match projectSourceRepo with
                      | Some s -> Some s
                      | None ->
                          match evalString this.sourceRepo with
                          | Some v -> Some v
                          | None ->
                              //printfn "repoBranchOption = %A" repoBranchOption
                              match repoUrlOption, repoBranchOption, repoTypeOption with
                              | Some url, Some branch, Some "git" when not (String.IsNullOrWhiteSpace branch) ->
                                  url + "/" + "tree/" + branch |> Some
                              | Some url, _, Some "git" -> url + "/" + "tree/" + "master" |> Some
                              | Some url, _, None -> Some url
                              | _ -> None

                  let sourceFolder =
                      match projectSourceFolder with
                      | Some s -> s
                      | None ->
                          match evalString this.sourceFolder with
                          | None -> Environment.CurrentDirectory
                          | Some v -> v

                  //printfn "sourceFolder = '%s'" sourceFolder
                  //printfn "sourceRepo = '%A'" sourceRepo
                  { Path = dllFile
                    XmlFile = None
                    SourceRepo = sourceRepo
                    SourceFolder = Some sourceFolder
                    Substitutions = Some projectParameters
                    MarkdownComments = this.mdcomments || projectMarkdownComments
                    Warn = projectWarn
                    PublicOnly = not this.nonpublic } ]

        // Compute the merge of all referenced DLLs across all projects
        // so they can be resolved during API doc generation.
        //
        // TODO: This is inaccurate: the different projects might not be referencing the same DLLs.
        // We should do doc generation for each output of each proejct separately
        let apiDocOtherFlags =
            [ for (_dllFile, otherFlags, _, _, _, _, _, _, _, _) in crackedProjects do
                  for otherFlag in otherFlags do
                      if otherFlag.StartsWith("-r:", StringComparison.Ordinal) then
                          if File.Exists(otherFlag.[3..]) then
                              yield otherFlag
                          else
                              printfn "NOTE: the reference '%s' was not seen on disk, ignoring" otherFlag ]
            // TODO: This 'distinctBy' is merging references that may be inconsistent across the project set
            |> List.distinctBy (fun ref -> Path.GetFileName(ref.[3..]))

        let rootOutputFolderAsGiven =
            if String.IsNullOrWhiteSpace this.output then
                if watch then "tmp/watch" else "output"
            else
                this.output

        // This is in-package
        //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net6.0\any
        //   to .nuget\packages\fsdocs-tool\7.1.7\templates
        let dir = Path.GetDirectoryName(typeof<CoreBuildOptions>.Assembly.Location)

        let defaultTemplateAttempt1 =
            Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "templates", "_template.html"))
        // This is in-repo only
        let defaultTemplateAttempt2 =
            Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "_template.html"))

        let defaultTemplate =
            if this.nodefaultcontent then
                None
            else if
                (try
                    File.Exists(defaultTemplateAttempt1)
                 with _ ->
                     false)
            then
                Some defaultTemplateAttempt1
            elif
                (try
                    File.Exists(defaultTemplateAttempt2)
                 with _ ->
                     false)
            then
                Some defaultTemplateAttempt2
            else
                None

        let extraInputs =
            [ if not this.nodefaultcontent then
                  // The "extras" content goes in "."
                  //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net6.0\any
                  //   to .nuget\packages\fsdocs-tool\7.1.7\extras
                  let attempt1 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "extras"))

                  if
                      (try
                          Directory.Exists(attempt1)
                       with _ ->
                           false)
                  then
                      printfn "using extra content from %s" attempt1
                      (attempt1, ".")
                  else
                      // This is for in-repo use only, assuming we are executing directly from
                      //   src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe
                      //   src\fsdocs-tool\bin\Release\net6.0\fsdocs.exe
                      let attempt2 =
                          Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "content"))

                      if
                          (try
                              Directory.Exists(attempt2)
                           with _ ->
                               false)
                      then
                          printfn "using extra content from %s" attempt2
                          (attempt2, "content")
                      else
                          printfn "no extra content found at %s or %s" attempt1 attempt2 ]

        // The incremental state (as well as the files written to disk)
        let mutable latestApiDocModel = None
        let mutable latestApiDocGlobalParameters = []
        let mutable latestApiDocCodeReferenceResolver = (fun _ -> None)
        let mutable latestApiDocPhase2 = (fun _ -> ())
        let mutable latestApiDocSearchIndexEntries = [||]
        let mutable latestDocContentPhase2 = (fun _ -> ())
        let mutable latestDocContentResults = Map.empty
        let mutable latestDocContentSearchIndexEntries = [||]
        let mutable latestDocContentGlobalParameters = []

        // Actions to read out the incremental state
        let getLatestGlobalParameters () =
            latestApiDocGlobalParameters @ latestDocContentGlobalParameters

        let regenerateSearchIndex () =
            let index = Array.append latestApiDocSearchIndexEntries latestDocContentSearchIndexEntries

            let indxTxt = System.Text.Json.JsonSerializer.Serialize index

            File.WriteAllText(Path.Combine(rootOutputFolderAsGiven, "index.json"), indxTxt)

        /// get the hot reload script if running in watch mode
        let getLatestWatchScript () =
            if watch then
                // if running in watch mode, inject hot reload script
                [ ParamKeys.``fsdocs-watch-script``, Serve.generateWatchScript this.port_option ]
            else
                // otherwise, inject empty replacement string
                [ ParamKeys.``fsdocs-watch-script``, "" ]

        // Incrementally generate API docs (regenerates all api docs, in two phases)
        let runGeneratePhase1 () =
            protect "API doc generation (phase 1)" (fun () ->
                if crackedProjects.Length = 0 || this.noapidocs then
                    latestApiDocGlobalParameters <- [ ParamKeys.``fsdocs-list-of-namespaces``, "" ]
                elif crackedProjects.Length > 0 then
                    let (outputKind, initialTemplate2) =
                        let templates =
                            [ OutputKind.Html, Path.Combine(this.input, "reference", "_template.html")
                              OutputKind.Html, Path.Combine(this.input, "_template.html")
                              OutputKind.Markdown, Path.Combine(this.input, "reference", "_template.md")
                              OutputKind.Markdown, Path.Combine(this.input, "_template.md") ]

                        match templates |> List.tryFind (fun (_, path) -> path |> File.Exists) with
                        | Some(kind, path) -> kind, Some path
                        | None ->
                            let templateFiles = templates |> Seq.map snd |> String.concat "', '"

                            match defaultTemplate with
                            | Some d ->
                                printfn
                                    "note, no template files: '%s' found, using default template %s"
                                    templateFiles
                                    d

                                OutputKind.Html, Some d
                            | None ->
                                printfn
                                    "note, no template file '%s' found, and no default template at '%s'"
                                    templateFiles
                                    defaultTemplateAttempt1

                                OutputKind.Html, None

                    printfn ""
                    printfn "API docs:"
                    printfn "  generating model for %d assemblies in API docs..." apiDocInputs.Length

                    let model, globals, index, phase2 =
                        match outputKind with
                        | OutputKind.Html ->
                            ApiDocs.GenerateHtmlPhased(
                                inputs = apiDocInputs,
                                output = rootOutputFolderAsGiven,
                                collectionName = collectionName,
                                substitutions = docsSubstitutions,
                                qualify = this.qualify,
                                ?template = initialTemplate2,
                                otherFlags = apiDocOtherFlags @ Seq.toList this.fscoptions,
                                root = root,
                                libDirs = paths,
                                onError = onError,
                                menuTemplateFolder = this.input
                            )
                        | OutputKind.Markdown ->
                            ApiDocs.GenerateMarkdownPhased(
                                inputs = apiDocInputs,
                                output = rootOutputFolderAsGiven,
                                collectionName = collectionName,
                                substitutions = docsSubstitutions,
                                qualify = this.qualify,
                                ?template = initialTemplate2,
                                otherFlags = apiDocOtherFlags @ Seq.toList this.fscoptions,
                                root = root,
                                libDirs = paths,
                                onError = onError
                            )
                        | _ -> failwithf "API Docs format '%A' is not supported" outputKind

                    // Used to resolve code references in content with respect to the API Docs model
                    let resolveInlineCodeReference (s: string) =
                        if s.StartsWith("cref:", StringComparison.Ordinal) then
                            let s = s.[5..]

                            match model.Resolver.ResolveCref s with
                            | None -> None
                            | Some cref -> Some(cref.NiceName, cref.ReferenceLink)
                        else
                            None

                    latestApiDocModel <- Some model
                    latestApiDocCodeReferenceResolver <- resolveInlineCodeReference
                    latestApiDocSearchIndexEntries <- index
                    latestApiDocGlobalParameters <- globals
                    latestApiDocPhase2 <- phase2)

        let runGeneratePhase2 () =
            protect "API doc generation (phase 2)" (fun () ->
                printfn ""
                printfn "Write API Docs:"

                let globals = getLatestWatchScript () @ getLatestGlobalParameters ()

                latestApiDocPhase2 globals
                regenerateSearchIndex ())

        // Incrementally convert content
        let runDocContentPhase1 () =
            protect "Content generation (phase 1)" (fun () ->
                //printfn "projectInfos = %A" projectInfos

                printfn ""
                printfn "Content:"

                let saveImages =
                    (match this.saveImages with
                     | "some" -> None
                     | "none" -> Some false
                     | "all" -> Some true
                     | _ -> None)

                let docContent =
                    DocContent(
                        rootOutputFolderAsGiven,
                        latestDocContentResults,
                        Some this.linenumbers,
                        this.eval,
                        docsSubstitutions,
                        saveImages,
                        watch,
                        root,
                        latestApiDocCodeReferenceResolver,
                        onError
                    )

                let docModels = docContent.Convert(this.input, defaultTemplate, extraInputs)
                let actualDocModels = docModels |> List.map fst |> List.choose id
                let extrasForSearchIndex = docContent.GetSearchIndexEntries(actualDocModels)

                let navEntriesWithoutActivePage =
                    docContent.GetNavigationEntries(
                        this.input,
                        actualDocModels,
                        None,
                        ignoreUncategorized = this.ignoreuncategorized
                    )

                let headTemplateContent =
                    let headTemplatePath = Path.Combine(this.input, "_head.html")

                    if not (File.Exists headTemplatePath) then
                        ""
                    else
                        File.ReadAllText headTemplatePath
                        |> SimpleTemplating.ApplySubstitutionsInText [ ParamKeys.root, root ]

                let bodyTemplateContent =
                    let bodyTemplatePath = Path.Combine(this.input, "_body.html")

                    if not (File.Exists bodyTemplatePath) then
                        ""
                    else
                        File.ReadAllText bodyTemplatePath
                        |> SimpleTemplating.ApplySubstitutionsInText [ ParamKeys.root, root ]

                let results =
                    Map.ofList
                        [ for (thing, _action) in docModels do
                              match thing with
                              | Some(file, _isOtherLang, model) -> (file, model)
                              | None -> () ]

                latestDocContentResults <- results
                latestDocContentSearchIndexEntries <- extrasForSearchIndex

                latestDocContentGlobalParameters <-
                    [ ParamKeys.``fsdocs-list-of-documents``, navEntriesWithoutActivePage
                      ParamKeys.``fsdocs-head-extra``, headTemplateContent
                      ParamKeys.``fsdocs-body-extra``, bodyTemplateContent ]

                latestDocContentPhase2 <-
                    (fun globals ->
                        printfn ""
                        printfn "Write Content:"

                        for (optDocModel, action) in docModels do
                            let globals =
                                match optDocModel with
                                | None -> globals
                                | Some(currentPagePath, _, _) ->
                                    // Update the nav entries with the current page doc model
                                    let navEntries =
                                        docContent.GetNavigationEntries(
                                            this.input,
                                            actualDocModels,
                                            Some currentPagePath,
                                            ignoreUncategorized = this.ignoreuncategorized
                                        )

                                    globals
                                    |> List.map (fun (pk, v) ->
                                        if pk <> ParamKeys.``fsdocs-list-of-documents`` then
                                            pk, v
                                        else
                                            ParamKeys.``fsdocs-list-of-documents``, navEntries)

                            action globals))

        let runDocContentPhase2 () =
            protect "Content generation (phase 2)" (fun () ->
                let globals = getLatestWatchScript () @ getLatestGlobalParameters ()

                latestDocContentPhase2 globals)

        //-----------------------------------------
        // Clean

        let rootInputFolderAsGiven = this.input
        let rootInputFolderFullPath = Path.GetFullPath rootInputFolderAsGiven
        let rootOutputFolderFullPath = Path.GetFullPath rootOutputFolderAsGiven

        if this.clean then
            let rec clean dir =
                for file in Directory.EnumerateFiles(dir) do
                    File.Delete file |> ignore

                for subdir in Directory.EnumerateDirectories dir do
                    if not (Path.GetFileName(subdir).StartsWith '.') then
                        clean subdir

            let isOutputPathOK =
                rootOutputFolderAsGiven <> "/"
                && rootOutputFolderAsGiven <> "."
                && rootOutputFolderFullPath <> rootInputFolderFullPath
                && not (String.IsNullOrEmpty rootOutputFolderAsGiven)

            if isOutputPathOK then
                try
                    clean rootOutputFolderFullPath
                with e ->
                    printfn "warning: error during cleaning, continuing: %s" e.Message
            else
                printfn "warning: skipping cleaning due to strange output path: \"%s\"" rootOutputFolderAsGiven

        if watch then
            printfn "Building docs first time..."

        //-----------------------------------------
        // Build

        let ok =
            let ok1 = runGeneratePhase1 ()
            // Note, the above generates these outputs:
            //   latestApiDocModel
            //   latestApiDocGlobalParameters
            //   latestApiDocCodeReferenceResolver
            //   latestApiDocPhase2
            //   latestApiDocSearchIndexEntries

            let ok2 = runDocContentPhase1 ()
            // Note, the above references these inputs:
            //   latestApiDocCodeReferenceResolver
            //
            // Note, the above generates these outputs:
            //   latestDocContentResults
            //   latestDocContentSearchIndexEntries
            //   latestDocContentGlobalParameters
            //   latestDocContentPhase2

            let ok2 = ok2 && runGeneratePhase2 ()

            // Run this second to override anything produced by API generate, e.g.
            // bespoke file for namespaces etc.
            let ok1 = ok1 && runDocContentPhase2 ()
            regenerateSearchIndex ()
            ok1 && ok2

        //-----------------------------------------
        // Watch

        if watch then

            let docsWatchers =
                [ if Directory.Exists(this.input) then
                      yield new FileSystemWatcher(this.input)
                  match defaultTemplate with
                  | Some defaultTemplate ->
                      yield new FileSystemWatcher(Path.GetDirectoryName(defaultTemplate), IncludeSubdirectories = true)
                  | None -> () ]

            let templateWatchers =
                if Directory.Exists(this.input) then
                    [ new FileSystemWatcher(this.input) ]
                else
                    []

            let projectOutputWatchers =
                [ for input in apiDocInputs do
                      let dir = Path.GetDirectoryName(input.Path)

                      if Directory.Exists(dir) then
                          new FileSystemWatcher(dir), input.Path ]

            use _holder =
                { new IDisposable with
                    member _.Dispose() =
                        for p in docsWatchers do
                            p.Dispose()

                        for p in templateWatchers do
                            p.Dispose()

                        for (p, _) in projectOutputWatchers do
                            p.Dispose() }

            // Only one update at a time
            let monitor = obj ()
            // One of each kind of request at a time
            let mutable docsQueued = true
            let mutable generateQueued = true

            let docsDependenciesChanged = FSharp.Control.Event<string>()

            docsDependenciesChanged.Publish.Add(fun fileName ->
                if not docsQueued then
                    docsQueued <- true
                    printfn "Detected change in '%s', scheduling rebuild of docs..." this.input

                    async {
                        do! Async.Sleep(300)

                        lock monitor (fun () ->
                            docsQueued <- false

                            if runDocContentPhase1 () then
                                if runDocContentPhase2 () then
                                    regenerateSearchIndex ())

                        Serve.refreshEvent.Trigger fileName
                    }
                    |> Async.Start)

            let apiDocsDependenciesChanged = FSharp.Control.Event<_>()

            apiDocsDependenciesChanged.Publish.Add(fun () ->
                if not generateQueued then
                    generateQueued <- true
                    printfn "Detected change in built outputs, scheduling rebuild of API docs..."

                    async {
                        do! Async.Sleep(300)

                        lock monitor (fun () ->
                            generateQueued <- false

                            if runGeneratePhase1 () then
                                if runGeneratePhase2 () then
                                    regenerateSearchIndex ())

                        Serve.refreshEvent.Trigger "full"
                    }
                    |> Async.Start)

            // Listen to changes in any input under docs
            for docsWatcher in docsWatchers do
                docsWatcher.IncludeSubdirectories <- true
                docsWatcher.NotifyFilter <- NotifyFilters.LastWrite
                docsWatcher.Changed.Add(fun fileEvent -> docsDependenciesChanged.Trigger fileEvent.Name)

            // When _template.* change rebuild everything
            for templateWatcher in templateWatchers do
                templateWatcher.IncludeSubdirectories <- true
                // _menu_template.html or _menu-item_template.html could be changed as well.
                templateWatcher.Filter <- "*template.html"
                templateWatcher.NotifyFilter <- NotifyFilters.LastWrite

                templateWatcher.Changed.Add(fun fileEvent ->
                    docsDependenciesChanged.Trigger fileEvent.Name
                    apiDocsDependenciesChanged.Trigger())

            // Listen to changes in output DLLs
            for (projectOutputWatcher, projectOutput) in projectOutputWatchers do
                projectOutputWatcher.Filter <- Path.GetFileName(projectOutput)
                projectOutputWatcher.Path <- Path.GetDirectoryName(projectOutput)
                projectOutputWatcher.NotifyFilter <- NotifyFilters.LastWrite
                projectOutputWatcher.Changed.Add(fun _ -> apiDocsDependenciesChanged.Trigger())

            // Start raising events
            for docsWatcher in docsWatchers do
                docsWatcher.EnableRaisingEvents <- true

            for templateWatcher in templateWatchers do
                templateWatcher.EnableRaisingEvents <- true

            for (pathWatcher, _path) in projectOutputWatchers do
                pathWatcher.EnableRaisingEvents <- true

            generateQueued <- false
            docsQueued <- false

            if not this.noserver_option then
                printfn
                    "starting server on http://localhost:%d for content in %s"
                    this.port_option
                    rootOutputFolderFullPath

                Serve.startWebServer rootOutputFolderFullPath this.port_option

            if not this.nolaunch_option then
                let url = sprintf "http://localhost:%d/%s" this.port_option this.open_option

                printfn "launching browser window to open %s" url

                try
                    Process.Start(new ProcessStartInfo(url, UseShellExecute = true)) |> ignore
                with ex ->
                    printfn "Warning, unable to launch browser(%s), try manually browsing to %s" ex.Message url

            waitForKey watch

        if ok then 0 else 1

    abstract noserver_option: bool
    default x.noserver_option = false

    abstract nolaunch_option: bool
    default x.nolaunch_option = false

    abstract open_option: string
    default x.open_option = ""

    abstract port_option: int
    default x.port_option = 0

[<Verb("convert",
       HelpText =
           "convert a single document (.md, .fsx, .ipynb) to HTML or another output format without building a full documentation site")>]
type ConvertCommand() =

    [<Value(0, MetaName = "input", Required = true, HelpText = "Input file to convert (.md, .fsx or .ipynb).")>]
    member val input = "" with get, set

    [<Option('o',
             "output",
             Required = false,
             HelpText =
                 "Output file path. Defaults to the input filename with the output format extension in the current directory.")>]
    member val output = "" with get, set

    [<Option("template",
             Required = false,
             HelpText = "Path to an HTML (or other format) template file. When omitted, raw content is written.")>]
    member val template = "" with get, set

    [<Option("outputformat",
             Required = false,
             Default = "",
             HelpText =
                 "Output format: html (default), ipynb, latex, fsx, markdown. When not specified, inferred from the output file extension.")>]
    member val outputFormat = "" with get, set

    [<Option("eval", Default = false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = false with get, set

    [<Option("linenumbers", Default = false, Required = false, HelpText = "Add line numbers.")>]
    member val linenumbers = false with get, set

    [<Option("parameters",
             Required = false,
             HelpText = "Additional substitution parameters, e.g. --parameters key1 value1 key2 value2")>]
    member val parameters = Seq.empty<string> with get, set

    member this.Execute() =
        let inputFile = Path.GetFullPath(this.input)

        if not (File.Exists inputFile) then
            printfn "error: input file '%s' does not exist" inputFile
            1
        else

            // Infer output format: explicit flag > extension of -o > default html
            let resolvedFormat =
                if not (String.IsNullOrWhiteSpace this.outputFormat) then
                    this.outputFormat.ToLowerInvariant()
                elif not (String.IsNullOrWhiteSpace this.output) then
                    let ext = Path.GetExtension(this.output).TrimStart('.').ToLowerInvariant()

                    match ext with
                    | "md" -> "markdown"
                    | "ipynb" -> "ipynb"
                    | "tex" -> "latex"
                    | "fsx" -> "fsx"
                    | _ -> "html"
                else
                    "html"

            let outputKind =
                match resolvedFormat with
                | "ipynb" -> OutputKind.Pynb
                | "latex" -> OutputKind.Latex
                | "fsx" -> OutputKind.Fsx
                | "markdown" -> OutputKind.Markdown
                | _ -> OutputKind.Html

            let outputFile =
                if String.IsNullOrWhiteSpace this.output then
                    let basename = Path.GetFileNameWithoutExtension(inputFile)
                    sprintf "%s.%s" basename outputKind.Extension
                else
                    this.output

            let templateOpt =
                if String.IsNullOrWhiteSpace this.template then
                    None
                else
                    Some this.template

            let userSubstitutions =
                let parameters = Array.ofSeq this.parameters

                if parameters.Length % 2 = 1 then
                    printfn "The --parameters option's argument count must be even"
                    exit 1

                evalPairwiseStringsNoOption parameters
                |> List.map (fun (a, b) -> (ParamKey a, b))

            let isFsx = inputFile.EndsWith(".fsx", StringComparison.OrdinalIgnoreCase)
            let isMd = inputFile.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            let isPynb = inputFile.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase)

            try
                if isMd then
                    printfn "converting %s --> %s" inputFile outputFile

                    Literate.ConvertMarkdownFile(
                        inputFile,
                        ?template = templateOpt,
                        output = outputFile,
                        outputKind = outputKind,
                        lineNumbers = this.linenumbers,
                        substitutions = userSubstitutions
                    )

                    0
                elif isFsx then
                    printfn "converting %s --> %s" inputFile outputFile

                    let fsiEvaluator =
                        if this.eval then
                            Some(FsiEvaluator(options = [| "--multiemit-" |]) :> IFsiEvaluator)
                        else
                            None

                    Literate.ConvertScriptFile(
                        inputFile,
                        ?template = templateOpt,
                        output = outputFile,
                        outputKind = outputKind,
                        lineNumbers = this.linenumbers,
                        ?fsiEvaluator = fsiEvaluator,
                        substitutions = userSubstitutions
                    )

                    0
                elif isPynb then
                    printfn "converting %s --> %s" inputFile outputFile

                    Literate.ConvertPynbFile(
                        inputFile,
                        ?template = templateOpt,
                        output = outputFile,
                        outputKind = outputKind,
                        lineNumbers = this.linenumbers,
                        substitutions = userSubstitutions
                    )

                    0
                else
                    printfn "error: unsupported input file type '%s'" (Path.GetExtension inputFile)
                    printfn "supported types: .md, .fsx, .ipynb"
                    1
            with ex ->
                printfn "Error during conversion: %O" ex
                1

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand() =
    inherit CoreBuildOptions(false)

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults, watch it and serve it")>]
type WatchCommand() =
    inherit CoreBuildOptions(true)

    override x.noserver_option = x.noserver

    [<Option("noserver", Required = false, Default = false, HelpText = "Do not serve content when watching.")>]
    member val noserver = false with get, set

    override x.nolaunch_option = x.nolaunch

    [<Option("nolaunch", Required = false, Default = false, HelpText = "Do not launch a browser window.")>]
    member val nolaunch = false with get, set

    override x.open_option = x.openv

    [<Option("open", Required = false, Default = "", HelpText = "URL extension to launch http://localhost:<port>/%s.")>]
    member val openv = "" with get, set

    override x.port_option = x.port

    [<Option("port", Required = false, Default = 8901, HelpText = "Port to serve content for http://localhost serving.")>]
    member val port = 8901 with get, set
