namespace fsdocs

open CommandLine

open System
open System.Diagnostics
open System.IO
open System.Reflection

open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open fsdocs.Common
open FSharp.Formatting.Templating
open Microsoft.Extensions.Logging

type CoreBuildOptions(watch) =

    [<Option("input", Required = false, Default = "docs", HelpText = "Input directory of documentation content.")>]
    member val input = "" with get, set

    [<Option("projects",
             Required = false,
             HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member val projects = Seq.empty<string> with get, set

    [<Option("output",
             Required = false,
             HelpText = "Output Folder (default 'output'). Ignored by 'watch', which keeps no output folder.")>]
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

    [<Option('v',
             "verbosity",
             Required = false,
             Default = "normal",
             HelpText = "How much to log: quiet, minimal, normal, detailed or diagnostic.")>]
    member val verbosity = "normal" with get, set

    member this.Execute() =

        if not (Verbosity.configure this.verbosity) then
            exit 1

        let onError msg =
            if this.strict then
                logger.Errorf "%s" msg
                exit 1

        let protect phase f =
            try
                f ()
                true
            with ex ->
                logger.Errorf "%s failed:\n%O" phase ex

                onError (sprintf "%s failed, and --strict is on : \n%O" phase ex)
                false

        /// The substitutions as given by the user
        let userParameters =
            let parameters = Array.ofSeq this.parameters

            if parameters.Length % 2 = 1 then
                logger.Errorf "The --parameters option's arguments' count has to be an even number"
                exit 1

            evalPairwiseStringsNoOption parameters
            |> List.map (fun (a, b) -> (ParamKey a, b))

        let userParametersDict = readOnlyDict userParameters

        // Adjust the user substitutions for 'watch' mode root
        let userRoot, userParameters =
            if watch then
                let userRoot =
                    match this.root_override_option with
                    | Some r -> r
                    | None -> sprintf "http://localhost:%d/" this.port_option

                if
                    userParametersDict.ContainsKey(ParamKeys.root)
                    && this.root_override_option.IsNone
                then
                    logger.Warnf "ignoring user-specified root since in watch mode, root = %s" userRoot

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

        let extraMsbuildProperties =
            this.extraMsbuildProperties
            |> Seq.toList
            |> List.map (fun s ->
                let arr = s.Split("=")

                if arr.Length > 1 then
                    arr.[0], String.concat "=" arr.[1..]
                else
                    failwith "properties must be of the form 'PropName=PropValue'")

        let getTime (p: string) =
            try
                File.GetLastWriteTimeUtc(p)
            with _ ->
                DateTime.Now

        let discoveredCollectionName, projectFiles =
            Crack.discoverProjects userCollectionName (Seq.toList this.projects) this.ignoreprojects

        let msbuildFiles =
            [ "Directory.Build.props"; "Directory.Build.targets"; "Directory.Packages.props"; "global.json" ]
            |> List.choose (fun file ->
                if File.Exists file then
                    Some(Path.GetFullPath file)
                else
                    None)

        // The cracked projects are cached in '.fsdocs/cache' until a project file, one of the
        // solution-wide MSBuild files, the options or the tool change. The key is computed on
        // every call so that a re-crack in watch mode sees the current file times.
        let crackKey () =
            (userRoot,
             Seq.toList this.parameters,
             extraMsbuildProperties,
             projectFiles,
             getTime (typeof<CoreBuildOptions>.Assembly.Location),
             (projectFiles @ msbuildFiles |> List.map getTime |> List.toArray))

        let evaluateProjectsCached () : Crack.CrackedProjectInfo list =
            let key = crackKey ()

            Utils.cacheBinary ".fsdocs/cache" (fun (_, key2) -> key = key2) (fun () ->
                Crack.evaluateProjects (onError, extraMsbuildProperties, projectFiles, this.ignoreprojects), key)
            |> fst

        let evaluatedInfos = evaluateProjectsCached ()
        let collectionName = discoveredCollectionName

        let (root, crackedProjects, _, docsSubstitutions, generateLlmsTxt) =
            Crack.siteOf (userRoot, userParameters, collectionName, evaluatedInfos, true)

        // In watch mode the logo must link to the locally hosted site, even when
        // <FsDocsLogoLink> is set to a production URL for the published site.
        let overrideLogoLinkForWatch substitutions =
            if watch then
                substitutions
                |> List.map (fun (pk, v) ->
                    if pk = ParamKeys.``fsdocs-logo-link`` then
                        (pk, root)
                    else
                        (pk, v))
            else
                substitutions

        let docsSubstitutions = overrideLogoLinkForWatch docsSubstitutions

        let watchOverrides =
            if watch then
                [ ParamKeys.root; ParamKeys.``fsdocs-logo-link`` ]
            else
                []

        let apiDocInputsOf (crackedProjects: Crack.CrackedProject list) =
            [
                for project in crackedProjects ->
                    let sourceRepo =
                        match project.SourceRepository with
                        | Some s -> Some s
                        | None ->
                            match evalString this.sourceRepo with
                            | Some v -> Some v
                            | None ->
                                //printfn "repoBranchOption = %A" repoBranchOption
                                match project.RepositoryUrl, project.RepositoryBranch, project.RepositoryType with
                                | Some url, Some branch, Some "git" when not (String.IsNullOrWhiteSpace branch) ->
                                    url + "/" + "tree/" + branch |> Some
                                | Some url, _, Some "git" -> url + "/" + "tree/" + "master" |> Some
                                | Some url, _, None -> Some url
                                | _ -> None

                    let sourceFolder =
                        match project.SourceFolder with
                        | Some s -> s
                        | None ->
                            match evalString this.sourceFolder with
                            | None -> Environment.CurrentDirectory
                            | Some v -> v

                    //printfn "sourceFolder = '%s'" sourceFolder
                    //printfn "sourceRepo = '%A'" sourceRepo
                    {
                        Path = project.TargetPath
                        XmlFile = None
                        SourceRepo = sourceRepo
                        SourceFolder = Some sourceFolder
                        Substitutions = Some(overrideLogoLinkForWatch project.Substitutions)
                        MarkdownComments = this.mdcomments || project.UsesMarkdownComments
                        Warn = project.WarnOnMissingDocs
                        PublicOnly = not this.nonpublic
                        ShowInheritedMembers = not project.NoInheritedMembers
                        TypeConstraintDisplayMode = project.TypeConstraints
                    }
            ]

        let apiDocInputs = apiDocInputsOf crackedProjects

        // The design-time build of the projects: the '-r:' references and the properties as set by
        // the targets. It is the expensive part of project cracking, so it runs only when the API
        // docs are generated and is cached in '.fsdocs/references' with the same key as the projects.
        let designTimeBuild (force: bool) (infos: Crack.CrackedProjectInfo list) : Map<string, Crack.DesignTimeBuild> =
            let key = crackKey ()

            let resolved, _ =
                Utils.cacheBinary ".fsdocs/references" (fun (_, key2) -> not force && key = key2) (fun () ->
                    logger.Infof "design-time build of %d projects..." infos.Length

                    let builds =
                        Crack.resolveCompilerOptions
                            extraMsbuildProperties
                            [ for info in infos -> info.ProjectFileName, info.TargetFrameworks ]

                    [
                        for info in infos do
                            match builds.TryFind info.ProjectFileName with
                            | Some build -> info.ProjectFileName, build
                            | None -> ()
                    ],
                    key)

            Map.ofList resolved

        /// The references of a project, split by whether they exist on disk.
        let referencesOf (build: Crack.DesignTimeBuild) =
            let refs =
                [
                    for otherFlag in build.OtherOptions do
                        if otherFlag.StartsWith("-r:", StringComparison.Ordinal) then
                            otherFlag.[3..]
                ]

            let kept, dropped = refs |> List.partition File.Exists

            for r in dropped do
                logger.Warnf "the reference '-r:%s' was not seen on disk, ignoring" r

            kept, dropped

        /// The substitutions whose value differs between two substitution lists: key, before, after.
        let changedSubstitutions (before: Substitutions) (after: Substitutions) =
            let old = dict before

            [
                for (ParamKey key as pk, value) in after do
                    match old.TryGetValue pk with
                    | true, v when v = value -> ()
                    | true, v -> key, v, value
                    | _ -> key, "", value
            ]

        // Compute the merge of all referenced DLLs across all projects
        // so they can be resolved during API doc generation.
        //
        // TODO: This is inaccurate: the different projects might not be referencing the same DLLs.
        // We should do doc generation for each output of each proejct separately
        let apiDocOtherFlagsOf (references: ProjectReferences list) =
            [
                for r in references do
                    for reference in r.References do
                        yield "-r:" + reference
            ]
            // TODO: This 'distinctBy' is merging references that may be inconsistent across the project set
            |> List.distinctBy (fun ref -> Path.GetFileName(ref.[3..]))

        let projectDiagnosticsOf (crackedProjects: Crack.CrackedProject list) (docsSubstitutions: Substitutions) =
            let siteSubstitutions = dict docsSubstitutions

            [
                for project in crackedProjects ->
                    {
                        ProjectFile = project.ProjectFileName
                        TargetPath = project.TargetPath
                        TargetExists = File.Exists project.TargetPath
                        OverridingSubstitutions =
                            [
                                for (ParamKey key as pk, value) in project.Substitutions do
                                    if siteSubstitutions.ContainsKey pk && siteSubstitutions.[pk] <> value then
                                        key, value
                            ]
                    }
            ]

        /// Everything derived from the evaluated projects. With a design-time build, the substitutions
        /// are recomputed from the properties as the targets set them and the references are known.
        /// The root URL stays the one chosen at startup.
        let crackResultOf
            (infos: Crack.CrackedProjectInfo list)
            (designTime: Map<string, Crack.DesignTimeBuild> option)
            : CrackResult =
            let siteOf infos =
                let (_, crackedProjects, paths, docsSubstitutions, _) =
                    Crack.siteOf (Some root, userParameters, collectionName, infos, false)

                crackedProjects, paths, overrideLogoLinkForWatch docsSubstitutions

            let crackedProjects, paths, docsSubstitutions = siteOf infos

            match designTime with
            | None ->
                {
                    Substitutions = docsSubstitutions
                    SubstitutionDiagnostics =
                        Diagnostics.substitutions docsSubstitutions userParameters watchOverrides []
                    ApiDocInputs = apiDocInputsOf crackedProjects
                    ApiDocOtherFlags = Seq.toList this.fscoptions
                    LibDirs = paths
                    Projects = projectDiagnosticsOf crackedProjects docsSubstitutions
                    References = []
                    DesignTimeBuilt = false
                }
            | Some builds ->
                let refinedInfos =
                    [
                        for info in infos ->
                            match builds.TryFind info.ProjectFileName with
                            | Some build -> Crack.refineProjectInfo info build
                            | None -> info
                    ]

                let refinedProjects, refinedPaths, refinedSubstitutions = siteOf refinedInfos
                let evaluatedByProject = crackedProjects |> List.map (fun p -> p.ProjectFileName, p) |> Map.ofList

                let references =
                    [
                        for project in refinedProjects do
                            match builds.TryFind project.ProjectFileName with
                            | Some build ->
                                let kept, dropped = referencesOf build

                                let before =
                                    evaluatedByProject.TryFind project.ProjectFileName
                                    |> Option.map (fun p -> p.Substitutions)
                                    |> Option.defaultValue []

                                {
                                    ProjectFile = project.ProjectFileName
                                    References = kept
                                    DroppedReferences = dropped
                                    ChangedSubstitutions = changedSubstitutions before project.Substitutions
                                }
                            | None -> ()
                    ]

                let changed = changedSubstitutions docsSubstitutions refinedSubstitutions

                let projectChanges =
                    [
                        for r in references do
                            for (key, before, after) in r.ChangedSubstitutions ->
                                Path.GetFileNameWithoutExtension r.ProjectFile, key, before, after
                    ]

                if not changed.IsEmpty || not projectChanges.IsEmpty then
                    logger.Infof
                        "design-time build: %d site-wide and %d project substitutions changed (use --verbosity detailed to list them)"
                        changed.Length
                        projectChanges.Length

                    for (key, before, after) in changed do
                        logger.Debugf "  %s: '%s' --> '%s'" key before after

                    for (project, key, before, after) in projectChanges do
                        logger.Debugf "  (%s) %s: '%s' --> '%s'" project key before after

                {
                    Substitutions = refinedSubstitutions
                    SubstitutionDiagnostics =
                        Diagnostics.substitutions
                            refinedSubstitutions
                            userParameters
                            watchOverrides
                            [ for (key, _, _) in changed -> ParamKey key ]
                    ApiDocInputs = apiDocInputsOf refinedProjects
                    ApiDocOtherFlags = apiDocOtherFlagsOf references @ Seq.toList this.fscoptions
                    LibDirs = refinedPaths
                    Projects = projectDiagnosticsOf refinedProjects refinedSubstitutions
                    References = references
                    DesignTimeBuilt = true
                }

        /// The crack result of the projects on disk (cached), before any design-time build
        let evaluatedCrack () =
            crackResultOf (evaluateProjectsCached ()) None

        /// The crack result after the design-time build, which runs now unless cached ('force' skips the cache)
        let resolvedCrack (force: bool) =
            let infos = evaluateProjectsCached ()
            crackResultOf infos (Some(designTimeBuild force infos))

        /// The crack result for 'build': the design-time build runs once, when the API docs need it
        let startupCrack =
            lazy
                (if this.noapidocs || crackedProjects.IsEmpty then
                     evaluatedCrack ()
                 else
                     resolvedCrack false)

        // Only used by 'build'; 'watch' keeps no output folder
        let rootOutputFolderAsGiven =
            if String.IsNullOrWhiteSpace this.output then
                "output"
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

        let defaultTemplateResolution =
            if this.nodefaultcontent then
                {
                    Tried = []
                    Chosen = None
                    Note = Some "--nodefaultcontent is on"
                }
            else
                Diagnostics.resolveFile [ defaultTemplateAttempt1; defaultTemplateAttempt2 ]

        let defaultTemplate = defaultTemplateResolution.Chosen

        // Default markdown template – used when generateLlmsTxt is enabled and no user _template.md exists.
        // An empty (or minimal) _template.md causes the processor to emit just the document content, which
        // is ideal for LLM consumption.
        let defaultMdTemplateAttempt1 =
            Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "templates", "_template.md"))

        let defaultMdTemplateAttempt2 =
            Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "_template.md"))

        let defaultMdTemplateResolution =
            if this.nodefaultcontent then
                {
                    Tried = []
                    Chosen = None
                    Note = Some "--nodefaultcontent is on"
                }
            else
                Diagnostics.resolveFile [ defaultMdTemplateAttempt1; defaultMdTemplateAttempt2 ]

        let defaultMdTemplate = defaultMdTemplateResolution.Chosen

        // The "extras" content goes in "."
        //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net6.0\any
        //   to .nuget\packages\fsdocs-tool\7.1.7\extras
        // This is for in-repo use only, assuming we are executing directly from
        //   src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe
        //   src\fsdocs-tool\bin\Release\net6.0\fsdocs.exe
        let extrasResolution =
            if this.nodefaultcontent then
                {
                    Tried = []
                    Chosen = None
                    Note = Some "--nodefaultcontent is on"
                }
            else
                Diagnostics.resolveDirectory
                    [
                        Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "extras"))
                        Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "content"))
                    ]

        let extraInputs =
            match extrasResolution.Chosen, extrasResolution.Tried with
            | Some chosen, attempt1 :: _ when chosen = attempt1 -> [ (chosen, ".") ]
            | Some chosen, _ -> [ (chosen, "content") ]
            | None, _ -> []

        // The template for API reference pages: a 'reference/_template.*' or '_template.*' in the input
        // folder, else the default template.
        let apiDocsTemplateResolution, apiDocsOutputKind, apiDocsTemplate =
            let templates =
                [
                    OutputKind.Html, Path.Combine(this.input, "reference", "_template.html")
                    OutputKind.Html, Path.Combine(this.input, "_template.html")
                    OutputKind.Markdown, Path.Combine(this.input, "reference", "_template.md")
                    OutputKind.Markdown, Path.Combine(this.input, "_template.md")
                ]

            let tried = templates |> List.map snd

            match templates |> List.tryFind (fun (_, path) -> path |> File.Exists) with
            | Some(kind, path) ->
                {
                    Tried = tried
                    Chosen = Some path
                    Note = None
                },
                kind,
                Some path
            | None ->
                let templateFiles = tried |> String.concat "', '"

                match defaultTemplate with
                | Some d ->
                    {
                        Tried = tried
                        Chosen = Some d
                        Note =
                            Some(
                                sprintf "note, no template files: '%s' found, using default template %s" templateFiles d
                            )
                    },
                    OutputKind.Html,
                    Some d
                | None ->
                    {
                        Tried = tried
                        Chosen = None
                        Note =
                            Some(
                                sprintf
                                    "note, no template file '%s' found, and no default template at '%s'"
                                    templateFiles
                                    defaultTemplateAttempt1
                            )
                    },
                    OutputKind.Html,
                    None

        let diagnostics: Diagnostics =
            let headTemplatePath = Path.Combine(this.input, "_head.html")
            let bodyTemplatePath = Path.Combine(this.input, "_body.html")

            {
                ToolVersion = Diagnostics.toolVersion ()
                CommandLine = Diagnostics.commandLine ()
                Command = (if watch then "watch" else "build")
                Input = this.input
                Output = (if watch then None else Some rootOutputFolderAsGiven)
                Root = root
                CollectionName = collectionName
                GenerateLlmsTxt = generateLlmsTxt
                IgnoredOptions = this.ignoredOptions
                Projects = projectDiagnosticsOf crackedProjects docsSubstitutions
                Substitutions = Diagnostics.substitutions docsSubstitutions userParameters watchOverrides []
                DefaultTemplate = defaultTemplateResolution
                DefaultMarkdownTemplate = defaultMdTemplateResolution
                ApiDocsTemplate = apiDocsTemplateResolution
                ApiDocsOutputKind = string<OutputKind> apiDocsOutputKind
                Extras = extrasResolution
                HeadTemplate =
                    (if File.Exists headTemplatePath then
                         Some headTemplatePath
                     else
                         None)
                BodyTemplate =
                    (if File.Exists bodyTemplatePath then
                         Some bodyTemplatePath
                     else
                         None)
                MenuTemplatesFound = Menu.isTemplatingAvailable this.input
            }

        Diagnostics.log diagnostics

        for project in diagnostics.Projects do
            if not project.TargetExists then
                let msg =
                    sprintf
                        "*** %s does not exist, has it been built? You may need to provide --properties Configuration=Release."
                        project.TargetPath

                if this.strict then failwith msg else logger.Warnf "%s" msg


        // The incremental state (as well as the files written to disk)
        let mutable latestApiDocModel = None
        let mutable latestApiDocGlobalParameters = []
        let mutable latestApiDocCodeReferenceResolver = (fun _ -> None)
        let mutable latestApiDocPhase2 = (fun _ -> ())
        let mutable latestApiDocSearchIndexEntries = [||]
        let mutable latestApiDocOutputKind = OutputKind.Html
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

        // Capture the bool value before it is shadowed by the generateLlmsTxt function below.
        let generateLlmsTxtEnabled = generateLlmsTxt

        let generateLlmsTxt () =
            if generateLlmsTxtEnabled then
                let index = Array.append latestApiDocSearchIndexEntries latestDocContentSearchIndexEntries
                // When FsDocsGenerateLlmsTxt is enabled, markdown is always generated alongside HTML
                // (using the bundled default markdown template if the user hasn't provided a _template.md).
                let docContentUsesMarkdown = true

                let apiDocUsesMarkdown = latestApiDocOutputKind = OutputKind.Markdown

                let llmsTxt, llmsFullTxt =
                    LlmsTxt.buildContent collectionName index docContentUsesMarkdown apiDocUsesMarkdown

                File.WriteAllText(Path.Combine(rootOutputFolderAsGiven, "llms.txt"), llmsTxt)
                File.WriteAllText(Path.Combine(rootOutputFolderAsGiven, "llms-full.txt"), llmsFullTxt)

        /// get the hot reload script if running in watch mode
        let getLatestWatchScript () =
            if watch then
                // if running in watch mode, inject hot reload script
                [ ParamKeys.``fsdocs-watch-script``, Serve.generateWatchScript () ]
            else
                // otherwise, inject empty replacement string
                [ ParamKeys.``fsdocs-watch-script``, "" ]

        /// Generate the API docs model and page renderers, None when there is nothing to generate.
        let generateApiDocs
            (crack: CrackResult)
            (outputFolder: string)
            (onError: string -> unit)
            : ApiDocsPhased option =
            if crack.ApiDocInputs.IsEmpty || this.noapidocs then
                None
            else
                apiDocsTemplateResolution.Note
                |> Option.iter (fun note -> logger.Warnf "%s" note)

                logger.Infof "API docs: generating the model for %d assemblies..." crack.ApiDocInputs.Length
                let stopwatch = Stopwatch.StartNew()

                let phased =
                    match apiDocsOutputKind with
                    | OutputKind.Html ->
                        Some(
                            ApiDocs.GenerateHtmlPhased(
                                inputs = crack.ApiDocInputs,
                                output = outputFolder,
                                collectionName = collectionName,
                                substitutions = crack.Substitutions,
                                qualify = this.qualify,
                                ?template = apiDocsTemplate,
                                otherFlags = crack.ApiDocOtherFlags,
                                root = root,
                                libDirs = crack.LibDirs,
                                onError = onError,
                                menuTemplateFolder = this.input
                            )
                        )
                    | OutputKind.Markdown ->
                        Some(
                            ApiDocs.GenerateMarkdownPhased(
                                inputs = crack.ApiDocInputs,
                                output = outputFolder,
                                collectionName = collectionName,
                                substitutions = crack.Substitutions,
                                qualify = this.qualify,
                                ?template = apiDocsTemplate,
                                otherFlags = crack.ApiDocOtherFlags,
                                root = root,
                                libDirs = crack.LibDirs,
                                onError = onError
                            )
                        )
                    | outputKind -> failwithf "API Docs format '%A' is not supported" outputKind

                phased
                |> Option.iter (fun phased ->
                    let namespaces = phased.Model.Collection.Namespaces

                    logger.Infof
                        "API docs: %d namespaces, %d entities in %.1f s"
                        namespaces.Length
                        (namespaces |> List.sumBy (fun ns -> ns.Entities.Length))
                        stopwatch.Elapsed.TotalSeconds)

                phased

        /// Resolve 'cref:' code references in content with respect to the API Docs model
        let makeCodeReferenceResolver (model: ApiDocModel) (s: string) =
            if s.StartsWith("cref:", StringComparison.Ordinal) then
                let s = s.[5..]

                match model.Resolver.ResolveCref s with
                | None -> None
                | Some cref -> Some(cref.NiceName, cref.ReferenceLink)
            else
                None

        // Incrementally generate API docs (regenerates all api docs, in two phases)
        let runGeneratePhase1 () =
            protect "API doc generation (phase 1)" (fun () ->
                match generateApiDocs startupCrack.Value rootOutputFolderAsGiven onError with
                | None -> latestApiDocGlobalParameters <- [ ParamKeys.``fsdocs-list-of-namespaces``, "" ]
                | Some phased ->
                    latestApiDocOutputKind <- apiDocsOutputKind
                    latestApiDocModel <- Some phased.Model
                    latestApiDocCodeReferenceResolver <- makeCodeReferenceResolver phased.Model
                    latestApiDocSearchIndexEntries <- phased.SearchIndex
                    latestApiDocGlobalParameters <- phased.GlobalSubstitutions
                    latestApiDocPhase2 <- phased.Generate)

        let runGeneratePhase2 () =
            protect "API doc generation (phase 2)" (fun () ->
                logger.Debugf "writing the API docs"

                let globals = getLatestWatchScript () @ getLatestGlobalParameters ()

                latestApiDocPhase2 globals
                regenerateSearchIndex ())

        // Incrementally convert content
        let runDocContentPhase1 () =
            protect "Content generation (phase 1)" (fun () ->
                //printfn "projectInfos = %A" projectInfos

                let stopwatch = Stopwatch.StartNew()

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
                        startupCrack.Value.Substitutions,
                        saveImages,
                        watch,
                        root,
                        latestApiDocCodeReferenceResolver,
                        onError
                    )

                let docModels =
                    // When llms.txt generation is enabled, ensure a markdown template is available so that
                    // .md versions of content pages are written alongside .html files, enabling
                    // llms.txt to link to the more LLM-friendly markdown versions.
                    // An empty template causes the processor to emit just the document content,
                    // which is ideal for LLM consumption.
                    let mdTemplate =
                        if generateLlmsTxtEnabled then
                            match defaultMdTemplate with
                            | Some _ -> defaultMdTemplate
                            | None ->
                                let tempMdTemplate = Path.GetTempFileName()
                                File.WriteAllText(tempMdTemplate, "")
                                Some tempMdTemplate
                        else
                            None

                    docContent.Convert(this.input, defaultTemplate, extraInputs, ?defaultMdTemplate = mdTemplate)

                let actualDocModels = docModels |> List.map fst |> List.choose id

                logger.Infof "Content: %d pages in %.1f s" actualDocModels.Length stopwatch.Elapsed.TotalSeconds
                let extrasForSearchIndex = docContent.GetSearchIndexEntries(actualDocModels)

                // Pre-compute the navigation structure once; returned closure cheaply
                // generates per-page nav HTML by only re-applying active-page flags.
                let getNavEntries =
                    docContent.GetNavigationEntriesFactory(
                        this.input,
                        actualDocModels,
                        ignoreUncategorized = this.ignoreuncategorized
                    )

                let navEntriesWithoutActivePage = getNavEntries None

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
                        [
                            for (thing, _action) in docModels do
                                match thing with
                                | Some(file, _isOtherLang, model) -> (file, model)
                                | None -> ()
                        ]

                latestDocContentResults <- results
                latestDocContentSearchIndexEntries <- extrasForSearchIndex

                latestDocContentGlobalParameters <-
                    [
                        ParamKeys.``fsdocs-list-of-documents``, navEntriesWithoutActivePage
                        ParamKeys.``fsdocs-head-extra``, headTemplateContent
                        ParamKeys.``fsdocs-body-extra``, bodyTemplateContent
                    ]

                latestDocContentPhase2 <-
                    (fun globals ->
                        logger.Debugf "writing the content"

                        for (optDocModel, action) in docModels do
                            let globals =
                                match optDocModel with
                                | None -> globals
                                | Some(currentPagePath, _, _) ->
                                    // Use the pre-computed factory closure (only sets IsActive, no re-sorting)
                                    let navEntries = getNavEntries (Some currentPagePath)

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
        // Watch: a lazy site served from memory, no output folder

        if watch then
            Diagnostics.logIgnoredOptions diagnostics

            // Errors are printed, never fatal: the server stays up so the page can be fixed and reloaded
            let siteOnError msg = logger.Errorf "%s" msg

            let config: SiteConfig =
                {
                    Input = this.input
                    ExtraInputs = extraInputs
                    DefaultTemplateFolder = defaultTemplate |> Option.map Path.GetDirectoryName
                    Root = root
                    CollectionName = collectionName
                    DefaultTemplate = defaultTemplate
                    DefaultMdTemplate = defaultMdTemplate
                    GenerateLlmsTxt = generateLlmsTxtEnabled
                    IgnoreUncategorized = this.ignoreuncategorized
                    ContentOptions =
                        {
                            LineNumbers = Some this.linenumbers
                            Evaluate = this.eval
                            Substitutions = docsSubstitutions
                            OnError = siteOnError
                        }
                    ApiDllPaths = [ for input in apiDocInputs -> input.Path ]
                    ProjectFiles = projectFiles @ msbuildFiles
                    ApiDocsOutputKind = apiDocsOutputKind
                    ApiDocsTemplate = apiDocsTemplate
                    Crack = evaluatedCrack
                    Resolve = resolvedCrack
                    GenerateApi = (fun crack outputFolder -> generateApiDocs crack outputFolder siteOnError)
                    WatchScript = Serve.generateWatchScript ()
                    Diagnostics = diagnostics
                }

            use site = new Site(config)
            site.Start()

            logger.Infof "starting server on http://localhost:%d for content in %s" this.port_option this.input
            logger.Infof "pages are built when first requested; see http://localhost:%d/.fsdocs/doctor" this.port_option

            DevServer.startWebServer site this.port_option

            if not this.nolaunch_option then
                let url = sprintf "http://localhost:%d/%s" this.port_option this.open_option

                logger.Infof "launching browser window to open %s" url

                try
                    Process.Start(new ProcessStartInfo(url, UseShellExecute = true)) |> ignore
                with ex ->
                    logger.Warnf "unable to launch browser (%s), try manually browsing to %s" ex.Message url

            waitForKey watch
            0
        else
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
                        logger.Warnf "error during cleaning, continuing: %s" e.Message
                else
                    logger.Warnf "skipping cleaning due to strange output path: \"%s\"" rootOutputFolderAsGiven

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
                generateLlmsTxt ()
                ok1 && ok2

            if ok then 0 else 1

    /// Options given on the command line that have no effect for this command.
    abstract ignoredOptions: IgnoredOption list
    default x.ignoredOptions = []

    abstract nolaunch_option: bool
    default x.nolaunch_option = false

    abstract open_option: string
    default x.open_option = ""

    abstract port_option: int
    default x.port_option = 0

    abstract root_override_option: string option
    default x.root_override_option = None

/// Helpers for the <c>fsdocs convert</c> command.
module private ConvertHelpers =

    open System.Text.RegularExpressions

    // Compiled at module load; shared across all calls to embedResourcesInHtml.
    let private cssPattern =
        Regex(
            """<link\b(?=[^>]*\brel=["']stylesheet["'])[^>]*\bhref=["']([^"']+)["'][^>]*/?>""",
            RegexOptions.IgnoreCase ||| RegexOptions.Compiled
        )

    let private jsPattern =
        Regex(
            """<script\b[^>]*\bsrc=["']([^"']+)["'][^>]*>\s*</script>""",
            RegexOptions.IgnoreCase ||| RegexOptions.Compiled
        )

    let private imgPattern =
        Regex("""(<img\b[^>]*\bsrc=["'])([^"']+)(["'][^>]*>)""", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

    /// Return candidate directories in which to search for locally-referenced assets (CSS, JS, images).
    /// The search order is: output directory → template directory → default content directories.
    let findContentSearchDirs (outputFile: string) (templateFile: string option) =
        let dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)

        [
            yield Path.GetDirectoryName(Path.GetFullPath(outputFile))

            match templateFile with
            | Some t when not (String.IsNullOrWhiteSpace t) -> yield Path.GetDirectoryName(Path.GetFullPath(t))
            | _ -> ()

            // NuGet package layout: <package-root>/extras contains a "content" sub-directory.
            let nugetExtras = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "extras"))

            if
                (try
                    Directory.Exists(nugetExtras)
                 with _ ->
                     false)
            then
                yield nugetExtras

            // In-repo development layout: src/fsdocs-tool/bin/…/fsdocs.exe → docs/
            let repoDocs = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs"))

            if
                (try
                    Directory.Exists(repoDocs)
                 with _ ->
                     false)
            then
                yield repoDocs
        ]

    /// Inline local CSS, JS, and image resources that are referenced in the generated HTML file.
    /// Remote URLs (http/https) and data-URIs are left untouched.
    let embedResourcesInHtml (htmlPath: string) (searchDirs: string list) =
        let isRemote (href: string) =
            href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("//", StringComparison.OrdinalIgnoreCase)

        let tryFindFile (href: string) =
            if isRemote href then
                None
            else
                let normalized =
                    if href.StartsWith("./", StringComparison.Ordinal) then
                        href[2..]
                    else
                        href

                searchDirs
                |> List.tryPick (fun dir ->
                    let fullPath = Path.GetFullPath(Path.Combine(dir, normalized))
                    if File.Exists(fullPath) then Some fullPath else None)

        let html = File.ReadAllText(htmlPath)

        // Inline CSS: handles both <link rel="stylesheet" href="..."> and <link href="..." rel="stylesheet">
        let html =
            cssPattern.Replace(
                html,
                fun m ->
                    let href = m.Groups[1].Value

                    match tryFindFile href with
                    | Some fullPath -> sprintf "<style>%s</style>" (File.ReadAllText(fullPath))
                    | None -> m.Value
            )

        // Inline JS: <script src="..."></script>  (self-closing or with optional whitespace body)
        let html =
            jsPattern.Replace(
                html,
                fun m ->
                    let src = m.Groups[1].Value

                    match tryFindFile src with
                    | Some fullPath -> sprintf "<script>%s</script>" (File.ReadAllText(fullPath))
                    | None -> m.Value
            )

        // Inline local images as base64 data-URIs.
        // Capture groups: 1 = everything up to and including src=", 2 = path, 3 = " and rest of tag.
        let html =
            imgPattern.Replace(
                html,
                fun m ->
                    let src = m.Groups[2].Value

                    match tryFindFile src with
                    | Some fullPath ->
                        let bytes = File.ReadAllBytes(fullPath)
                        let ext = Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant()

                        let mimeType =
                            match ext with
                            | "png" -> "image/png"
                            | "jpg"
                            | "jpeg" -> "image/jpeg"
                            | "gif" -> "image/gif"
                            | "svg" -> "image/svg+xml"
                            | "ico" -> "image/x-icon"
                            | "webp" -> "image/webp"
                            | _ -> "image/png"

                        let b64 = Convert.ToBase64String(bytes)
                        sprintf "%sdata:%s;base64,%s%s" m.Groups[1].Value mimeType b64 m.Groups[3].Value
                    | None -> m.Value
            )

        File.WriteAllText(htmlPath, html)

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
             HelpText =
                 "Path to an HTML template file, or 'fsdocs' to use the built-in default template. When omitted, raw content is written.")>]
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

    [<Option("no-embed-resources",
             Default = false,
             Required = false,
             HelpText =
                 "Disable automatic inlining of local CSS, JS, and images into the output HTML. By default, when a template is used for HTML output, all locally-referenced assets are embedded so the output is a self-contained single file.")>]
    member val noEmbedResources = false with get, set

    [<Option('v',
             "verbosity",
             Required = false,
             Default = "normal",
             HelpText = "How much to log: quiet, minimal, normal, detailed or diagnostic.")>]
    member val verbosity = "normal" with get, set

    member this.Execute() =
        if not (Verbosity.configure this.verbosity) then
            exit 1

        let inputFile = Path.GetFullPath(this.input)

        if not (File.Exists inputFile) then
            logger.Errorf "input file '%s' does not exist" inputFile
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

            // Handle --template fsdocs: extract the embedded default template to a temp file.
            // Handle --template <path>: use as-is.
            // Handle no template: raw content only (no resource embedding needed).
            let templateOpt, tempFileToCleanUp =
                if String.IsNullOrWhiteSpace this.template then
                    None, None
                elif this.template.Equals("fsdocs", StringComparison.OrdinalIgnoreCase) then
                    let asm = Assembly.GetExecutingAssembly()
                    use stream = asm.GetManifestResourceStream("fsdocs._template.html")
                    use reader = new StreamReader(stream)
                    let content = reader.ReadToEnd()

                    let tmp =
                        Path.Combine(
                            Path.GetTempPath(),
                            sprintf "fsdocs-template-%s.html" (Guid.NewGuid().ToString("N"))
                        )

                    File.WriteAllText(tmp, content)
                    Some tmp, Some tmp
                else
                    Some this.template, None

            let userSubstitutions =
                let parameters = Array.ofSeq this.parameters

                if parameters.Length % 2 = 1 then
                    logger.Errorf "The --parameters option's argument count must be even"
                    exit 1

                evalPairwiseStringsNoOption parameters
                |> List.map (fun (a, b) -> (ParamKey a, b))

            // When embedding resources we need {{root}} to resolve to "" so that paths like
            // "{{root}}content/fsdocs-default.css" become "content/fsdocs-default.css".
            // Only add this default if the user has not already supplied a root substitution.
            let embedResources = not this.noEmbedResources && outputKind = OutputKind.Html && templateOpt.IsSome

            // When a template is used, supply sensible defaults for every standard fsdocs template
            // parameter so that {{fsdocs-*}} placeholders in the template are replaced with empty
            // strings (or a meaningful value) rather than being left as raw text in the output.
            // User-supplied --parameters values always take priority.
            let substitutions =
                match templateOpt with
                | None -> userSubstitutions
                | Some _ ->
                    let pageTitle = Path.GetFileNameWithoutExtension(inputFile)

                    let defaults =
                        [
                            ParamKeys.root, (if embedResources then "" else "")
                            ParamKeys.``fsdocs-page-title``, pageTitle
                            ParamKeys.``fsdocs-source-basename``, pageTitle
                            ParamKeys.``fsdocs-source-filename``, Path.GetFileName(inputFile)
                            ParamKeys.``fsdocs-collection-name``, pageTitle
                            ParamKeys.``fsdocs-authors``, ""
                            ParamKeys.``fsdocs-body-class``, "content"
                            ParamKeys.``fsdocs-body-extra``, ""
                            ParamKeys.``fsdocs-copyright``, ""
                            ParamKeys.``fsdocs-favicon-src``, ""
                            ParamKeys.``fsdocs-head-extra``, ""
                            ParamKeys.``fsdocs-license-link``, "#"
                            ParamKeys.``fsdocs-list-of-documents``, ""
                            ParamKeys.``fsdocs-list-of-namespaces``, ""
                            ParamKeys.``fsdocs-logo-alt``, pageTitle
                            ParamKeys.``fsdocs-logo-link``, "#"
                            ParamKeys.``fsdocs-logo-src``, ""
                            ParamKeys.``fsdocs-meta-tags``, ""
                            ParamKeys.``fsdocs-page-content-list``, ""
                            ParamKeys.``fsdocs-package-license-expression``, ""
                            ParamKeys.``fsdocs-package-project-url``, ""
                            ParamKeys.``fsdocs-package-tags``, ""
                            ParamKeys.``fsdocs-package-version``, ""
                            ParamKeys.``fsdocs-package-icon-url``, ""
                            ParamKeys.``fsdocs-release-notes-link``, "#"
                            ParamKeys.``fsdocs-repository-link``, "#"
                            ParamKeys.``fsdocs-repository-branch``, ""
                            ParamKeys.``fsdocs-repository-commit``, ""
                            ParamKeys.``fsdocs-source``, ""
                            ParamKeys.``fsdocs-theme``, ""
                            ParamKeys.``fsdocs-tooltips``, ""
                            ParamKeys.``fsdocs-watch-script``, ""
                            ParamKeys.``fsdocs-collection-name-link``, "#"
                            ParamKeys.``fsdocs-page-source``, ""
                        ]

                    // User-supplied values override defaults.
                    let userKeys = userSubstitutions |> List.map fst |> set

                    let filteredDefaults = defaults |> List.filter (fun (k, _) -> not (userKeys.Contains k))

                    userSubstitutions @ filteredDefaults

            let isFsx = inputFile.EndsWith(".fsx", StringComparison.OrdinalIgnoreCase)
            let isMd = inputFile.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            let isPynb = inputFile.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase)

            try
                if isMd then
                    logger.Infof "converting %s --> %s" inputFile outputFile

                    Literate.ConvertMarkdownFile(
                        inputFile,
                        ?template = templateOpt,
                        output = outputFile,
                        outputKind = outputKind,
                        lineNumbers = this.linenumbers,
                        substitutions = substitutions
                    )

                    0
                elif isFsx then
                    logger.Infof "converting %s --> %s" inputFile outputFile

                    let fsiEvaluator =
                        if this.eval then
                            Some(new FsiEvaluator(options = [| "--multiemit-" |]) :> IFsiEvaluator)
                        else
                            None

                    Literate.ConvertScriptFile(
                        inputFile,
                        ?template = templateOpt,
                        output = outputFile,
                        outputKind = outputKind,
                        lineNumbers = this.linenumbers,
                        ?fsiEvaluator = fsiEvaluator,
                        substitutions = substitutions
                    )

                    0
                elif isPynb then
                    logger.Infof "converting %s --> %s" inputFile outputFile

                    Literate.ConvertPynbFile(
                        inputFile,
                        ?template = templateOpt,
                        output = outputFile,
                        outputKind = outputKind,
                        lineNumbers = this.linenumbers,
                        substitutions = substitutions
                    )

                    0
                else
                    logger.Errorf
                        "unsupported input file type '%s', supported types: .md, .fsx, .ipynb"
                        (Path.GetExtension inputFile)

                    1
            with ex ->
                logger.Errorf "Error during conversion: %O" ex
                1
            |> fun exitCode ->
                // Clean up any temporary template file we created.
                match tempFileToCleanUp with
                | Some tmp ->
                    try
                        File.Delete(tmp)
                    with _ ->
                        ()
                | None -> ()

                // Post-process the HTML to inline all local asset references.
                if exitCode = 0 && embedResources then
                    let searchDirs =
                        ConvertHelpers.findContentSearchDirs outputFile (Option.map Path.GetFullPath templateOpt)

                    logger.Debugf
                        "embedding resources into %s (search dirs: %s)"
                        outputFile
                        (String.concat ", " searchDirs)

                    ConvertHelpers.embedResourcesInHtml outputFile searchDirs

                exitCode

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand() =
    inherit CoreBuildOptions(false)

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults, watch it and serve it")>]
type WatchCommand() =
    inherit CoreBuildOptions(true)

    override x.ignoredOptions =
        [
            if x.output <> "" then
                {
                    Option = "output"
                    Reason = "'fsdocs watch' keeps no output folder, pages are served from memory"
                }
            if x.clean then
                {
                    Option = "clean"
                    Reason = "'fsdocs watch' keeps no output folder, there is nothing to clean"
                }
            if x.saveImages <> "none" then
                {
                    Option = "saveimages"
                    Reason = "'fsdocs watch' serves images from their source and does not download them"
                }
        ]

    override x.nolaunch_option = x.nolaunch

    [<Option("nolaunch", Required = false, Default = false, HelpText = "Do not launch a browser window.")>]
    member val nolaunch = false with get, set

    override x.open_option = x.openv

    [<Option("open", Required = false, Default = "", HelpText = "URL extension to launch http://localhost:<port>/%s.")>]
    member val openv = "" with get, set

    override x.port_option = x.port

    [<Option("port", Required = false, Default = 8901, HelpText = "Port to serve content for http://localhost serving.")>]
    member val port = 8901 with get, set

    override x.root_override_option = if String.IsNullOrEmpty x.root then None else Some x.root

    [<Option("root",
             Required = false,
             Default = "",
             HelpText =
                 "Override the root URL for generated pages. Useful for reverse proxies or GitHub Codespaces. E.g. --root / or --root https://example.com/docs/. When not set, defaults to http://localhost:<port>/.")>]
    member val root = "" with get, set
