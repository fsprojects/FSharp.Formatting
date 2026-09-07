namespace fsdocs

open System
open System.Diagnostics
open System.IO
open System.Runtime.Serialization
open System.Xml

open System.Xml.Linq
open FSharp.Formatting.Templating
open FSharp.Formatting.Common

open Ionide.ProjInfo

[<AutoOpen>]
/// General utility helpers shared across the fsdocs tool.
module Utils =

    /// Creates the directory at <c>path</c> if it does not already exist.
    let ensureDirectory path =
        let dir = DirectoryInfo(path)

        if not dir.Exists then
            dir.Create()

    /// Serialises <c>object</c> to <c>fileName</c> using <see cref="DataContractSerializer"/> binary XML format.
    let saveBinary (object: 'T) (fileName: string) =
        try
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)) |> ignore
        with _ ->
            ()

        let formatter = DataContractSerializer(typeof<'T>)
        use fs = File.Create(fileName)

        use xw = XmlDictionaryWriter.CreateBinaryWriter(fs)

        formatter.WriteObject(xw, object)
        fs.Flush()

    /// Deserialises a value from <c>fileName</c> using <see cref="DataContractSerializer"/> binary XML format,
    /// returning <c>None</c> on any error.
    let loadBinary<'T> (fileName: string) : 'T option =
        let formatter = DataContractSerializer(typeof<'T>)
        use fs = File.OpenRead(fileName)

        use xw = XmlDictionaryReader.CreateBinaryReader(fs, XmlDictionaryReaderQuotas.Max)

        try
            let object = formatter.ReadObject(xw) :?> 'T
            Some object
        with _ ->
            None

    /// Attempts to restore a previously cached value from <c>cacheFile</c>. If the cache
    /// is absent or invalid (as judged by <c>cacheValid</c>), calls <c>f</c> to compute
    /// a fresh value and saves it to the cache.
    let cacheBinary cacheFile cacheValid (f: unit -> 'T) : 'T =
        let attempt =
            if File.Exists(cacheFile) then
                let v = loadBinary cacheFile

                match v with
                | Some v ->
                    if cacheValid v then
                        logger.Debugf "restored project state from '%s'" cacheFile
                        Some v
                    else
                        logger.Debugf "discarding project state in '%s' as now invalid" cacheFile
                        None
                | None -> None
            else
                None

        match attempt with
        | Some r -> r
        | None ->
            let res = f ()
            saveBinary res cacheFile
            res

    /// Appends a trailing <c>/</c> to a URL or path string if it does not already end with
    /// <c>/</c> or <c>.html</c>.
    let ensureTrailingSlash (s: string) =
        if s.EndsWith '/' || s.EndsWith(".html", StringComparison.Ordinal) then
            s
        else
            s + "/"

/// Thin wrappers around dotnet CLI commands used during project cracking.
module DotNetCli =

    /// Run `dotnet msbuild <args>` and receive the trimmed standard output.
    let msbuild (pwd: string) (args: string) : string =
        let psi = ProcessStartInfo "dotnet"
        psi.WorkingDirectory <- pwd
        psi.Arguments <- $"msbuild %s{args}"
        psi.RedirectStandardOutput <- true
        psi.UseShellExecute <- false
        use ps = new Process()
        ps.StartInfo <- psi
        ps.Start() |> ignore
        let output = ps.StandardOutput.ReadToEnd()
        ps.WaitForExit()
        output.Trim()

    /// Run `dotnet msbuild <args>` and receive the exit code, standard output and standard error.
    let msbuildResult (pwd: string) (args: string) : int * string * string =
        let psi = ProcessStartInfo "dotnet"
        psi.WorkingDirectory <- pwd
        psi.Arguments <- $"msbuild %s{args}"
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        use ps = new Process()
        ps.StartInfo <- psi
        ps.Start() |> ignore
        let output = ps.StandardOutput.ReadToEndAsync()
        let error = ps.StandardError.ReadToEndAsync()
        ps.WaitForExit()
        ps.ExitCode, output.Result.Trim(), error.Result.Trim()

/// Project-cracking logic: evaluates the fsdocs-specific MSBuild properties of each project
/// with `dotnet msbuild --getProperty`, and resolves the compiler references with a design-time
/// build (`dotnet msbuild -t:...CoreCompile -getItem:FscCommandLineArgs`) only for the projects
/// that take part in the API docs, and only when they are needed.
module Crack =

    [<return: Struct>]
    let (|ConditionEquals|_|) (str: string) (arg: string) =
        if System.String.Compare(str, arg, System.StringComparison.OrdinalIgnoreCase) = 0 then
            ValueSome()
        else
            ValueNone

    /// Parses a trimmed MSBuild property string as an optional boolean
    /// (<c>None</c> for whitespace-only values, <c>Some true</c> for "True").
    let msbuildPropBool (s: string) =
        let trimmed = s.Trim()

        if String.IsNullOrWhiteSpace trimmed then
            None
        else
            match trimmed with
            | ConditionEquals "True" -> Some true
            | _ -> Some false

    /// Runs an external process, routing stdout and stderr lines to <c>log</c>,
    /// and returns the exit code together with the process arguments for diagnostics.
    let runProcess (log: string -> unit) (workingDir: string) (exePath: string) (args: string) =
        let psi = System.Diagnostics.ProcessStartInfo()
        psi.FileName <- exePath
        psi.WorkingDirectory <- workingDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.Arguments <- args
        psi.CreateNoWindow <- true
        psi.UseShellExecute <- false

        use p = new System.Diagnostics.Process()
        p.StartInfo <- psi

        p.OutputDataReceived.Add(fun ea -> log (ea.Data))

        p.ErrorDataReceived.Add(fun ea -> log (ea.Data))

        // printfn "running: %s %s" psi.FileName psi.Arguments

        p.Start() |> ignore
        p.BeginOutputReadLine()
        p.BeginErrorReadLine()
        p.WaitForExit()

        let exitCode = p.ExitCode

        exitCode, (workingDir, exePath, args)

    /// All fsdocs-relevant MSBuild properties of a single project, read by evaluating the project
    /// (no design-time build).
    type CrackedProjectInfo =
        {
            ProjectFileName: string
            TargetPath: string option
            /// The target frameworks of a multi-targeting project, empty otherwise
            TargetFrameworks: string list
            IsTestProject: bool
            IsLibrary: bool
            IsPackable: bool
            RepositoryUrl: string option
            RepositoryType: string option
            RepositoryBranch: string option
            UsesMarkdownComments: bool
            FsDocsLicenseLink: string option
            FsDocsLogoLink: string option
            FsDocsLogoSource: string option
            FsDocsLogoAlt: string option
            FsDocsReleaseNotesLink: string option
            FsDocsSourceFolder: string option
            FsDocsSourceRepository: string option
            FsDocsFaviconSource: string option
            FsDocsTheme: string option
            FsDocsWarnOnMissingDocs: bool
            FsDocsGenerateLlmsTxt: bool
            FsDocsAllowExecutableProject: bool
            FsDocsNoInheritedMembers: bool
            FsDocsTypeConstraints: FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode
            PackageProjectUrl: string option
            Authors: string option
            GenerateDocumentationFile: bool
            //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
            //Description : string option
            PackageLicenseExpression: string option
            PackageTags: string option
            Copyright: string option
            PackageVersion: string option
            PackageIconUrl: string option
            RepositoryCommit: string option
        }

    /// The MSBuild properties fsdocs reads from a project.
    let private fsdocsProperties =
        [
            "TargetPath"
            "TargetFrameworks"
            "OutputType"
            "IsTestProject"
            "IsPackable"
            "RepositoryUrl"
            "UsesMarkdownComments"
            "FsDocsCollectionNameLink"
            "FsDocsLogoSource"
            "FsDocsLogoAlt"
            "FsDocsFaviconSource"
            "FsDocsTheme"
            "FsDocsLogoLink"
            "FsDocsLicenseLink"
            "FsDocsReleaseNotesLink"
            "FsDocsSourceFolder"
            "FsDocsSourceRepository"
            "FsDocsWarnOnMissingDocs"
            "FsDocsGenerateLlmsTxt"
            "FsDocsAllowExecutableProject"
            "FsDocsNoInheritedMembers"
            "FsDocsTypeConstraints"
            "RepositoryType"
            "RepositoryBranch"
            "PackageProjectUrl"
            "Authors"
            "GenerateDocumentationFile"
            //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
            //"Description"
            "PackageLicenseExpression"
            "PackageTags"
            "Copyright"
            "PackageVersion"
            "PackageIconUrl"
            //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
            //"PackageReleaseNotes"
            "RepositoryCommit"
        ]

    /// Evaluate the fsdocs properties of a project with `dotnet msbuild --getProperty`.
    /// This is a plain MSBuild evaluation: no restore, no design-time build, a fraction of a second.
    let evaluateProperties
        (extraMsbuildProperties: (string * string) list)
        (projectFile: string)
        : Map<string, string> =
        let args =
            [
                yield sprintf "\"%s\"" projectFile
                yield "-nologo"
                for p in fsdocsProperties do
                    yield "--getProperty:" + p
                for (k, v) in extraMsbuildProperties do
                    yield sprintf "-p:%s=\"%s\"" k v
            ]
            |> String.concat " "

        let exitCode, output, error = DotNetCli.msbuildResult (Path.GetDirectoryName projectFile) args

        if exitCode <> 0 then
            failwithf "evaluating '%s' failed (exit code %d):\n%s\n%s" projectFile exitCode output error

        try
            use json = System.Text.Json.JsonDocument.Parse output

            json.RootElement.GetProperty("Properties").EnumerateObject()
            |> Seq.map (fun p -> p.Name, p.Value.GetString())
            |> Map.ofSeq
        with ex ->
            failwithf "could not read the properties of '%s' from:\n%s\n%s" projectFile output ex.Message

    let private infoOfProperties (projectFile: string) (props: Map<string, string>) : CrackedProjectInfo =
        let msbuildPropString prop =
            props
            |> Map.tryFind prop
            |> Option.bind (function
                | s when String.IsNullOrWhiteSpace(s) -> None
                | s -> Some s)

        let targetFrameworks =
            match msbuildPropString "TargetFrameworks" with
            | Some s ->
                s.Split(";", StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun s' -> s'.Trim())
                |> Array.toList
            | None -> []

        let msbuildPropBool prop =
            prop |> msbuildPropString |> Option.bind msbuildPropBool

        {
            ProjectFileName = projectFile
            TargetPath = msbuildPropString "TargetPath"
            TargetFrameworks = targetFrameworks
            IsTestProject = msbuildPropBool "IsTestProject" |> Option.defaultValue false
            IsLibrary =
                msbuildPropString "OutputType"
                |> Option.map (fun s -> s.ToLowerInvariant())
                |> ((=) (Some "library"))
            IsPackable = msbuildPropBool "IsPackable" |> Option.defaultValue false
            RepositoryUrl = msbuildPropString "RepositoryUrl"
            RepositoryType = msbuildPropString "RepositoryType"
            RepositoryBranch = msbuildPropString "RepositoryBranch"
            FsDocsSourceFolder = msbuildPropString "FsDocsSourceFolder"
            FsDocsSourceRepository = msbuildPropString "FsDocsSourceRepository"
            FsDocsLicenseLink = msbuildPropString "FsDocsLicenseLink"
            FsDocsReleaseNotesLink = msbuildPropString "FsDocsReleaseNotesLink"
            FsDocsLogoLink = msbuildPropString "FsDocsLogoLink"
            FsDocsLogoSource = msbuildPropString "FsDocsLogoSource"
            FsDocsLogoAlt = msbuildPropString "FsDocsLogoAlt"
            FsDocsFaviconSource = msbuildPropString "FsDocsFaviconSource"
            FsDocsTheme = msbuildPropString "FsDocsTheme"
            FsDocsWarnOnMissingDocs = msbuildPropBool "FsDocsWarnOnMissingDocs" |> Option.defaultValue false
            FsDocsGenerateLlmsTxt = msbuildPropBool "FsDocsGenerateLlmsTxt" |> Option.defaultValue true
            FsDocsAllowExecutableProject = msbuildPropBool "FsDocsAllowExecutableProject" |> Option.defaultValue false
            FsDocsNoInheritedMembers = msbuildPropBool "FsDocsNoInheritedMembers" |> Option.defaultValue false
            FsDocsTypeConstraints =
                msbuildPropString "FsDocsTypeConstraints"
                |> Option.bind (fun s ->
                    match s.Trim() with
                    | "None" -> Some FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode.None
                    | "Short" -> Some FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode.Short
                    | "Full" -> Some FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode.Full
                    | _ -> None)
                |> Option.defaultValue FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode.Short
            UsesMarkdownComments = msbuildPropBool "UsesMarkdownComments" |> Option.defaultValue false
            PackageProjectUrl = msbuildPropString "PackageProjectUrl"
            Authors = msbuildPropString "Authors"
            GenerateDocumentationFile = msbuildPropBool "GenerateDocumentationFile" |> Option.defaultValue false
            PackageLicenseExpression = msbuildPropString "PackageLicenseExpression"
            PackageTags = msbuildPropString "PackageTags"
            Copyright = msbuildPropString "Copyright"
            PackageVersion = msbuildPropString "PackageVersion"
            PackageIconUrl = msbuildPropString "PackageIconUrl"
            RepositoryCommit = msbuildPropString "RepositoryCommit"
        }

    /// Reads the fsdocs properties of a project. A multi-targeting project has no target path
    /// until a target framework is chosen; the first one is used.
    let crackProjectFile extraMsbuildProperties (file: string) : CrackedProjectInfo =
        let info = infoOfProperties file (evaluateProperties extraMsbuildProperties file)

        match info.TargetPath, info.TargetFrameworks with
        | None, tfm :: _ ->
            let props = evaluateProperties (extraMsbuildProperties @ [ "TargetFramework", tfm ]) file

            { infoOfProperties file props with
                TargetFrameworks = info.TargetFrameworks
            }
        | _ -> info

    /// Checks whether the project has been restored (i.e. the assets file exists) and
    /// fails if not.
    let private ensureProjectWasRestored (file: string) =
        let projDir = Path.GetDirectoryName(file)
        let projectAssetsJsonPath = Path.Combine(projDir, "obj", "project.assets.json")

        if File.Exists projectAssetsJsonPath then
            ()
        else
            // In dotnet 8 <UseArtifactsOutput> was introduced, see https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output
            // We will try and use CLI-based project evaluation to determine the location of project.assets.json file.
            // See https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8#cli-based-project-evaluation
            // Some projects (e.g. those with nonstandard artifact output locations) may not put project.assets.json
            // under <projDir>/obj/. If we can detect the actual path via MSBuild, we use that; otherwise we warn
            // and proceed so that the cracking step itself can produce the definitive error.
            let detectedPath =
                try
                    let path = DotNetCli.msbuild projDir "--getProperty:ProjectAssetsFile"
                    Some path
                with _ ->
                    None

            match detectedPath with
            | Some path when File.Exists path -> ()
            | Some _ ->
                // MSBuild reported a path but the file doesn't exist there — project is definitely not restored.
                failwithf $"project '%s{file}' not restored"
            | None ->
                // Could not determine the assets file location (e.g. old SDK without --getProperty support,
                // or nonstandard project layout). Warn and continue; if the project truly isn't restored the
                // subsequent cracking step will fail with a more specific error.
                logger.Warnf $"could not verify that project '%s{file}' was restored. Proceeding anyway."

    /// The result of the design-time build of one project.
    type DesignTimeBuild =
        {
            /// The compiler options, including the '-r:' references
            OtherOptions: string list
            /// The fsdocs properties as they are after the targets ran; properties set by targets
            /// (such as a version computed from a changelog) are only correct here
            Properties: (string * string) list
        }

    /// The targets of a design-time build: they resolve the references and make CoreCompile emit
    /// the compiler command line without running the compiler. The list is the one Ionide.ProjInfo
    /// uses, see https://github.com/dotnet/project-system/blob/main/docs/design-time-builds.md.
    let private designTimeTargets =
        [
            "ResolveAssemblyReferencesDesignTime"
            "ResolveProjectReferencesDesignTime"
            "ResolvePackageDependenciesDesignTime"
            "FindReferenceAssembliesForReferences"
            "_GenerateCompileDependencyCache"
            "_ComputeNonExistentFileProperty"
            "BeforeBuild"
            "BeforeCompile"
            "CoreCompile"
        ]

    /// The global properties of a design-time build: no compiler run, no build of the referenced
    /// projects, and a non-existent input so that CoreCompile is never skipped as up to date.
    let private designTimeProperties =
        [
            "ProvideCommandLineArgs", "true"
            "DesignTimeBuild", "true"
            "SkipCompilerExecution", "true"
            "GeneratePackageOnBuild", "false"
            "BuildProjectReferences", "false"
            "NonExistentFile", Path.Combine("__NonExistentSubDir__", "__NonExistentFile__")
        ]

    /// Run the design-time build of one project with `dotnet msbuild` and read the compiler command
    /// line and the fsdocs properties from its output. Unlike an evaluation, the properties reflect
    /// what the targets set, such as a version computed from a changelog.
    let private designTimeBuildOf
        (extraMsbuildProperties: (string * string) list)
        (projectFile: string)
        : DesignTimeBuild =
        let args =
            [
                yield sprintf "\"%s\"" projectFile
                yield "-nologo"
                yield "-t:" + String.concat ";" designTimeTargets
                yield "-getItem:FscCommandLineArgs"
                for p in fsdocsProperties do
                    yield "-getProperty:" + p
                for (k, v) in designTimeProperties @ extraMsbuildProperties do
                    yield sprintf "-p:%s=\"%s\"" k v
            ]
            |> String.concat " "

        let exitCode, output, error = DotNetCli.msbuildResult (Path.GetDirectoryName projectFile) args

        if exitCode <> 0 then
            failwithf "the design-time build of '%s' failed (exit code %d):\n%s\n%s" projectFile exitCode output error

        try
            use json = System.Text.Json.JsonDocument.Parse output

            let properties =
                json.RootElement.GetProperty("Properties").EnumerateObject()
                |> Seq.map (fun p -> p.Name, p.Value.GetString())
                |> List.ofSeq

            let options =
                [
                    for item in json.RootElement.GetProperty("Items").GetProperty("FscCommandLineArgs").EnumerateArray() ->
                        item.GetProperty("Identity").GetString()
                ]

            {
                OtherOptions = options
                Properties = properties
            }
        with ex ->
            failwithf "could not read the design-time build of '%s' from:\n%s\n%s" projectFile output ex.Message

    /// Run the design-time build of the given projects (in parallel) and return the result per
    /// project file; projects whose build fails are reported and left out.
    let resolveCompilerOptions
        (extraMsbuildProperties: (string * string) list)
        (projects: (string * string list) list)
        : Map<string, DesignTimeBuild> =
        for (projectFile, _) in projects do
            ensureProjectWasRestored projectFile

        projects
        |> List.map (fun (projectFile, targetFrameworks) ->
            async {
                // A multi-targeting project needs its target framework as a global property
                let properties =
                    match targetFrameworks with
                    | tfm :: _ -> extraMsbuildProperties @ [ "TargetFramework", tfm ]
                    | [] -> extraMsbuildProperties

                return
                    try
                        Some(projectFile, designTimeBuildOf properties projectFile)
                    with ex ->
                        logger.Warnf
                            "could not resolve the references of '%s': %s"
                            (Path.GetFileName projectFile)
                            ex.Message

                        None
            })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.choose id
        |> Map.ofArray

    /// The project info as the design-time build saw it: the same properties, but with the values
    /// set by MSBuild targets. The evaluated value is kept where the build reported none.
    let refineProjectInfo (info: CrackedProjectInfo) (build: DesignTimeBuild) : CrackedProjectInfo =
        let refined = infoOfProperties info.ProjectFileName (Map.ofList build.Properties)

        { refined with
            TargetPath = refined.TargetPath |> Option.orElse info.TargetPath
            TargetFrameworks = info.TargetFrameworks
        }

    /// Parses a Visual Studio solution file and returns the ordered list of project file paths.
    let getProjectsFromSlnFile (slnPath: string) =
        match InspectSln.tryParseSln slnPath with
        | Ok slnData -> InspectSln.loadingBuildOrder slnData

        //this.LoadProjects(projs, crosstargetingStrategy, useBinaryLogger, numberOfThreads)
        | Error e -> raise (exn ("cannot load the sln", e))

    /// Discovers project files (from solutions, directories, or explicit lists),
    /// cracks each one, and returns the collection name, collection URL, and per-project info.
    /// A project whose API documentation is generated, with the settings read from the project file.
    /// The compiler references are not part of it: see resolveCompilerOptions.
    type CrackedProject =
        {
            ProjectFileName: string
            TargetPath: string
            TargetFrameworks: string list
            RepositoryUrl: string option
            RepositoryBranch: string option
            RepositoryType: string option
            UsesMarkdownComments: bool
            WarnOnMissingDocs: bool
            SourceFolder: string option
            SourceRepository: string option
            NoInheritedMembers: bool
            TypeConstraints: FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode
            Substitutions: (ParamKey * string) list
        }

    /// Find the project files to document: the projects given explicitly, else the solution in the
    /// current folder, else the project files up to two folders deep. Returns the collection name too.
    let discoverProjects (userCollectionName: string option) (projects: string list) (ignoreProjects: bool) =
        let slnDir = Path.GetFullPath "."

        //printfn "x.projects = %A" x.projects
        let collectionName, projectFiles =
            match projects, ignoreProjects with
            | [], false ->
                let slnFiles =
                    Directory.GetFiles(slnDir, "*.sln*")
                    |> Array.filter (fun f ->
                        f.EndsWith(".sln", StringComparison.Ordinal)
                        || f.EndsWith(".slnf", StringComparison.Ordinal)
                        || f.EndsWith(".slnx", StringComparison.Ordinal))

                match slnFiles with
                | [| sln |] ->
                    logger.Debugf "getting projects from solution file %s" sln

                    let collectionName = defaultArg userCollectionName (Path.GetFileNameWithoutExtension(sln))

                    collectionName, getProjectsFromSlnFile sln
                | _ ->
                    let projectFiles =
                        [
                            yield! Directory.EnumerateFiles(slnDir, "*.fsproj")
                            for d in Directory.EnumerateDirectories(slnDir) do
                                yield! Directory.EnumerateFiles(d, "*.fsproj")

                                for d2 in Directory.EnumerateDirectories(d) do
                                    yield! Directory.EnumerateFiles(d2, "*.fsproj")
                        ]

                    let collectionName =
                        match projectFiles with
                        | [ file1 ] -> Path.GetFileNameWithoutExtension file1
                        | _ -> Path.GetFileName slnDir
                        |> defaultArg userCollectionName

                    collectionName, projectFiles

            | projectFiles, false ->
                let collectionName = Path.GetFileName(slnDir)
                collectionName, projectFiles |> List.map Path.GetFullPath
            | _, true ->
                let collectionName = defaultArg userCollectionName (Path.GetFileName slnDir)

                collectionName, []

        //printfn "projects = %A" projectFiles
        let projectFiles =
            projectFiles
            |> List.choose (fun projectFile ->
                let s = Path.GetFullPath projectFile

                let isFSharpFormattingTestProject =
                    s.Contains $"FSharp.ApiDocs.Tests%c{Path.DirectorySeparatorChar}files"
                    || s.EndsWith("FSharp.Formatting.TestHelpers.fsproj", StringComparison.Ordinal)

                if isFSharpFormattingTestProject then
                    logger.Debugf
                        $"skipping project '%s{Path.GetFileName s}' because the project is part of the FSharp.Formatting test suite."

                    None
                else
                    Some s)

        collectionName, projectFiles

    /// Evaluate the discovered projects and keep the documentable ones. No design-time build.
    let evaluateProjects
        (onError, extraMsbuildProperties, projectFiles: string list, ignoreProjects)
        : CrackedProjectInfo list =
        //printfn "filtered projects = %A" projectFiles
        if projectFiles.Length = 0 && (ignoreProjects |> not) then
            logger.Warnf "no project files found, no API docs will be generated"

        if ignoreProjects then
            logger.Infof "project files are ignored, no API docs will be generated"

        logger.Debugf "cracking projects..."

        let projectInfos =
            projectFiles
            |> List.map (fun p ->
                async {
                    return
                        try
                            Some(crackProjectFile extraMsbuildProperties p)
                        with e ->
                            logger.Warnf
                                "skipping project '%s' because an error occurred while cracking it: %O"
                                (Path.GetFileName p)
                                e

                            if not ignoreProjects then
                                onError "Project cracking failed and --strict is on, exiting"

                            None
                })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Array.toList
            |> List.choose id

        //printfn "projectInfos = %A" projectInfos
        let projectInfos =
            projectInfos
            |> List.choose (fun info ->
                let shortName = Path.GetFileName info.ProjectFileName

                if info.TargetPath.IsNone then
                    logger.Warnf "skipping project '%s' because it doesn't have a target path" shortName
                    None
                elif not info.IsLibrary && not info.FsDocsAllowExecutableProject then
                    logger.Debugf
                        "skipping project '%s' because it isn't a library (add <FsDocsAllowExecutableProject>true</FsDocsAllowExecutableProject> to include it)"
                        shortName

                    None
                elif info.IsTestProject then
                    logger.Debugf "skipping project '%s' because it has <IsTestProject> true" shortName
                    None
                elif not info.GenerateDocumentationFile then
                    logger.Warnf "skipping project '%s' because it doesn't have <GenerateDocumentationFile>" shortName
                    None
                else
                    Some info)

        //printfn "projectInfos = %A" projectInfos

        if projectInfos.Length = 0 && projectFiles.Length > 0 then
            logger.Warnf "While cracking project files, no project files succeeded."

        projectInfos

    /// The site-wide settings and the per-project substitutions of the given projects: the root URL,
    /// the documented projects, the folders holding their DLLs, the substitutions of the content
    /// pages and whether llms.txt is generated. Missing settings are only reported when 'warnMissing'
    /// is set, so a recomputation after a design-time build stays quiet.
    let siteOf
        (
            userRoot: string option,
            userParameters: (ParamKey * string) list,
            collectionName: string,
            projectInfos: CrackedProjectInfo list,
            warnMissing: bool
        ) : string * CrackedProject list * string list * (ParamKey * string) list * bool =
        let param setting key v =
            match v with
            | Some v -> Some(key, v)
            | None ->
                match setting, warnMissing with
                | Some setting, true -> logger.Warnf "please set '%s' in 'Directory.Build.props'" setting
                | _ -> ()

                None

        /// Try and xpath query a fallback value from the current Directory.Build.props file.
        /// This is useful to set some settings when there are no actual (c|f)sproj files.
        let fallbackFromDirectoryProps =
            if not (File.Exists "Directory.Build.props") then
                fun _ optProp -> optProp
            else
                let xDoc = XDocument.Load("Directory.Build.props")

                fun xpath optProp ->
                    optProp
                    |> Option.orElseWith (fun () ->
                        let xe = System.Xml.XPath.Extensions.XPathSelectElement(xDoc, xpath)
                        if isNull xe then None else Some xe.Value)

        // For the 'docs' directory we use the best info we can find from across all projects
        let projectInfoForDocs =
            {
                ProjectFileName = ""
                TargetPath = None
                TargetFrameworks = []
                IsTestProject = false
                IsLibrary = true
                IsPackable = true
                RepositoryUrl =
                    projectInfos
                    |> List.tryPick (fun info -> info.RepositoryUrl)
                    |> fallbackFromDirectoryProps "//RepositoryUrl"
                    |> Option.map ensureTrailingSlash
                RepositoryType = projectInfos |> List.tryPick (fun info -> info.RepositoryType)
                RepositoryBranch = projectInfos |> List.tryPick (fun info -> info.RepositoryBranch)
                FsDocsLicenseLink =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsLicenseLink)
                    |> fallbackFromDirectoryProps "//FsDocsLicenseLink"
                FsDocsReleaseNotesLink =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsReleaseNotesLink)
                    |> fallbackFromDirectoryProps "//FsDocsReleaseNotesLink"
                FsDocsLogoLink =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsLogoLink)
                    |> fallbackFromDirectoryProps "//FsDocsLogoLink"
                FsDocsLogoSource =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsLogoSource)
                    |> fallbackFromDirectoryProps "//FsDocsLogoSource"
                FsDocsLogoAlt =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsLogoAlt)
                    |> fallbackFromDirectoryProps "//FsDocsLogoAlt"
                FsDocsFaviconSource =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsFaviconSource)
                    |> fallbackFromDirectoryProps "//FsDocsFaviconSource"
                FsDocsSourceFolder = projectInfos |> List.tryPick (fun info -> info.FsDocsSourceFolder)
                FsDocsSourceRepository =
                    projectInfos
                    |> List.tryPick (fun info -> info.FsDocsSourceRepository)
                    |> fallbackFromDirectoryProps "//RepositoryUrl"
                FsDocsTheme = projectInfos |> List.tryPick (fun info -> info.FsDocsTheme)
                FsDocsWarnOnMissingDocs = false
                FsDocsGenerateLlmsTxt = projectInfos |> List.forall (fun i -> i.FsDocsGenerateLlmsTxt)
                FsDocsAllowExecutableProject = false
                FsDocsNoInheritedMembers = false
                FsDocsTypeConstraints = FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode.Short
                PackageProjectUrl =
                    projectInfos
                    |> List.tryPick (fun info -> info.PackageProjectUrl)
                    |> Option.map ensureTrailingSlash
                Authors =
                    projectInfos
                    |> List.tryPick (fun info -> info.Authors)
                    |> fallbackFromDirectoryProps "//Authors"
                GenerateDocumentationFile = true
                PackageLicenseExpression = projectInfos |> List.tryPick (fun info -> info.PackageLicenseExpression)
                PackageTags = projectInfos |> List.tryPick (fun info -> info.PackageTags)
                UsesMarkdownComments = false
                Copyright = projectInfos |> List.tryPick (fun info -> info.Copyright)
                PackageVersion =
                    projectInfos
                    |> List.tryPick (fun info -> info.PackageVersion)
                    |> fallbackFromDirectoryProps "//Version"
                PackageIconUrl = projectInfos |> List.tryPick (fun info -> info.PackageIconUrl)
                RepositoryCommit = projectInfos |> List.tryPick (fun info -> info.RepositoryCommit)
            }

        let root =
            let projectUrl = projectInfoForDocs.PackageProjectUrl |> Option.map ensureTrailingSlash

            defaultArg userRoot (defaultArg projectUrl ("/" + collectionName) |> ensureTrailingSlash)

        let parametersForProjectInfo (info: CrackedProjectInfo) =
            let projectUrl =
                info.PackageProjectUrl
                |> Option.map ensureTrailingSlash
                |> Option.defaultValue root

            let repoUrl = info.RepositoryUrl |> Option.map ensureTrailingSlash

            List.choose
                id
                [
                    param None ParamKeys.root (Some root)
                    param None ParamKeys.``fsdocs-authors`` (Some(info.Authors |> Option.defaultValue ""))
                    param None ParamKeys.``fsdocs-collection-name`` (Some collectionName)
                    param None ParamKeys.``fsdocs-copyright`` info.Copyright
                    param
                        (Some "<FsDocsLogoSource>")
                        ParamKeys.``fsdocs-logo-src``
                        (Some(defaultArg info.FsDocsLogoSource "img/logo.png"))
                    param
                        (Some "<FsDocsLogoAlt>")
                        ParamKeys.``fsdocs-logo-alt``
                        (Some(defaultArg info.FsDocsLogoAlt "Logo"))
                    param
                        (Some "<FsDocsFaviconSource>")
                        ParamKeys.``fsdocs-favicon-src``
                        (Some(defaultArg info.FsDocsFaviconSource "img/favicon.ico"))
                    param None ParamKeys.``fsdocs-theme`` (Some(defaultArg info.FsDocsTheme "default"))
                    param
                        (Some "<FsDocsLogoLink>")
                        ParamKeys.``fsdocs-logo-link``
                        (Some(info.FsDocsLogoLink |> Option.defaultValue projectUrl))
                    param
                        (Some "<FsDocsLicenseLink>")
                        ParamKeys.``fsdocs-license-link``
                        (info.FsDocsLicenseLink
                         |> Option.orElse (Option.map (sprintf "%sblob/master/LICENSE.md") repoUrl))
                    param
                        (Some "<FsDocsReleaseNotesLink>")
                        ParamKeys.``fsdocs-release-notes-link``
                        (info.FsDocsReleaseNotesLink
                         |> Option.orElse (Option.map (sprintf "%sblob/master/RELEASE_NOTES.md") repoUrl))
                    param None ParamKeys.``fsdocs-package-project-url`` (Some projectUrl)
                    param None ParamKeys.``fsdocs-package-license-expression`` info.PackageLicenseExpression
                    param None ParamKeys.``fsdocs-package-icon-url`` info.PackageIconUrl
                    param None ParamKeys.``fsdocs-package-tags`` (Some(info.PackageTags |> Option.defaultValue ""))
                    param (Some "<Version>") ParamKeys.``fsdocs-package-version`` info.PackageVersion
                    param (Some "<RepositoryUrl>") ParamKeys.``fsdocs-repository-link`` repoUrl
                    param None ParamKeys.``fsdocs-repository-branch`` info.RepositoryBranch
                    param None ParamKeys.``fsdocs-repository-commit`` info.RepositoryCommit
                ]
            @ userParameters

        let crackedProjects =
            projectInfos
            |> List.choose (fun info ->
                match info.TargetPath with
                | Some targetPath ->
                    let substitutions = parametersForProjectInfo info

                    Some
                        {
                            ProjectFileName = info.ProjectFileName
                            TargetPath = targetPath
                            TargetFrameworks = info.TargetFrameworks
                            RepositoryUrl = info.RepositoryUrl
                            RepositoryBranch = info.RepositoryBranch
                            RepositoryType = info.RepositoryType
                            UsesMarkdownComments = info.UsesMarkdownComments
                            WarnOnMissingDocs = info.FsDocsWarnOnMissingDocs
                            SourceFolder = info.FsDocsSourceFolder
                            SourceRepository = info.FsDocsSourceRepository
                            NoInheritedMembers = info.FsDocsNoInheritedMembers
                            TypeConstraints = info.FsDocsTypeConstraints
                            Substitutions = substitutions
                        }
                | _ -> None)

        let paths =
            projectInfos
            |> List.choose (fun projectInfo -> projectInfo.TargetPath |> Option.map Path.GetDirectoryName)

        let docsParameters = parametersForProjectInfo projectInfoForDocs
        root, crackedProjects, paths, docsParameters, projectInfoForDocs.FsDocsGenerateLlmsTxt
