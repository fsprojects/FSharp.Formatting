namespace FSharp.Formatting.CommandTool

open System
open System.IO
open System.Runtime.Serialization
open System.Xml

open FSharp.Formatting.Templating

open Dotnet.ProjInfo
open Dotnet.ProjInfo.Workspace

[<AutoOpen>]
module Utils =
    let ensureDirectory path =
        let dir = DirectoryInfo(path)
        if not dir.Exists then dir.Create()

    let saveBinary (object:'T) (fileName:string) =
        try Directory.CreateDirectory (Path.GetDirectoryName(fileName)) |> ignore with _ -> ()
        let formatter = DataContractSerializer(typeof<'T>)
        use fs = File.Create(fileName)
        use xw = XmlDictionaryWriter.CreateBinaryWriter(fs)
        formatter.WriteObject(xw, object)
        fs.Flush()

    let loadBinary<'T> (fileName:string):'T option =
        let formatter = DataContractSerializer(typeof<'T>)
        use fs = File.OpenRead(fileName)
        use xw = XmlDictionaryReader.CreateBinaryReader(fs, XmlDictionaryReaderQuotas.Max)
        try
            let object = formatter.ReadObject(xw) :?> 'T
            Some object
        with _ -> None

    let cacheBinary cacheFile cacheValid (f: unit -> 'T)  : 'T =
        let attempt =
            if File.Exists(cacheFile) then
                let v = loadBinary cacheFile
                match v with
                | Some v ->
                    if cacheValid v then
                        printfn "restored project state from '%s'" cacheFile
                        Some v
                    else
                        printfn "discarding project state in '%s' as now invalid" cacheFile
                        None
                | None -> None
            else None
        match attempt with
        | Some r -> r
        | None ->
            let res = f()
            saveBinary res cacheFile
            res
    let ensureTrailingSlash (s:string) =
        if s.EndsWith("/") || s.EndsWith(".html") then s else s + "/"

module Crack =

    let msbuildPropBool (s: string) =
        match s.Trim() with
        | "" -> None
        | Inspect.MSBuild.ConditionEquals "True" -> Some true
        | _ -> Some false

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

    type CrackedProjectInfo = {
        ProjectFileName : string
        TargetPath : string option
        IsTestProject : bool
        IsLibrary : bool
        IsPackable : bool
        RepositoryUrl : string option
        RepositoryType : string option
        RepositoryBranch : string option
        UsesMarkdownComments: bool
        FsDocsCollectionNameLink : string option
        FsDocsLicenseLink : string option
        FsDocsLogoLink : string option
        FsDocsLogoSource : string option
        FsDocsNavbarPosition: string option
        FsDocsReleaseNotesLink : string option
        FsDocsSourceFolder : string option
        FsDocsSourceRepository : string option
        FsDocsTheme: string option
        FsDocsWarnOnMissingDocs: bool
        PackageProjectUrl : string option
        Authors : string option
        GenerateDocumentationFile : bool
        //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
        //Description : string option
        PackageLicenseExpression : string option
        PackageTags : string option
        Copyright : string option
        PackageVersion : string option
        PackageIconUrl : string option
        //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
        //PackageReleaseNotes : string option
        RepositoryCommit : string option
    }

    let crackProjectFile slnDir extraMsbuildProperties (file : string) : CrackedProjectInfo =

        let projDir = Path.GetDirectoryName(file)
        let projectAssetsJsonPath = Path.Combine(projDir, "obj", "project.assets.json")
        if not(File.Exists(projectAssetsJsonPath)) then
            failwithf "project '%s' not restored" file

        let additionalInfo = [
            "OutputType"
            "IsTestProject"
            "IsPackable"
            "RepositoryUrl"
            "UsesMarkdownComments"
            "FsDocsCollectionNameLink"
            "FsDocsLogoSource"
            "FsDocsNavbarPosition"
            "FsDocsTheme"
            "FsDocsLogoLink"
            "FsDocsLicenseLink"
            "FsDocsReleaseNotesLink"
            "FsDocsSourceFolder"
            "FsDocsSourceRepository"
            "FsDocsWarnOnMissingDocs"
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
        let gp() = Inspect.getProperties ("TargetPath" :: additionalInfo)

        let loggedMessages = System.Collections.Concurrent.ConcurrentQueue<string>()
        let runCmd exePath args =
            let args = args @ [ yield "/p:DesignTimeBuild=true"; yield! Seq.map ((+) "/p:") extraMsbuildProperties]
            //printfn "%s, args = %A" exePath args
            let res = runProcess loggedMessages.Enqueue slnDir exePath (args |> String.concat " ")
            //printfn "done..."
            res

        let msbuildPath = Inspect.MSBuildExePath.DotnetMsbuild "dotnet"
        let msbuildExec = Inspect.msbuild msbuildPath runCmd

        let result = file |> Inspect.getProjectInfos loggedMessages.Enqueue msbuildExec [gp] []

        let msgs = (loggedMessages.ToArray() |> Array.toList)
        //printfn "msgs = %A" msgs
        match result with
        | Ok [Ok (Inspect.GetResult.Properties props)] ->
            let props = readOnlyDict props
            //printfn "props = %A" (Map.toList props)
            let msbuildPropString prop =
                match props.TryGetValue prop with
                | false, _ -> None
                | true, s when String.IsNullOrWhiteSpace s -> None
                | true, s -> Some s
            let msbuildPropBool prop = prop |> msbuildPropString |> Option.bind msbuildPropBool

            {
                ProjectFileName = file
                TargetPath = msbuildPropString "TargetPath"
                IsTestProject = msbuildPropBool "IsTestProject" |> Option.defaultValue false
                IsLibrary = msbuildPropString "OutputType" |> Option.map (fun s -> s.Equals("library", StringComparison.OrdinalIgnoreCase)) |> Option.defaultValue false
                IsPackable = msbuildPropBool "IsPackable" |> Option.defaultValue false
                RepositoryUrl = msbuildPropString "RepositoryUrl"
                RepositoryType = msbuildPropString "RepositoryType"
                RepositoryBranch = msbuildPropString "RepositoryBranch"
                FsDocsCollectionNameLink = msbuildPropString "FsDocsCollectionNameLink"
                FsDocsSourceFolder = msbuildPropString "FsDocsSourceFolder"
                FsDocsSourceRepository = msbuildPropString "FsDocsSourceRepository"
                FsDocsLicenseLink = msbuildPropString "FsDocsLicenseLink"
                FsDocsReleaseNotesLink = msbuildPropString "FsDocsReleaseNotesLink"
                FsDocsLogoLink = msbuildPropString "FsDocsLogoLink"
                FsDocsLogoSource = msbuildPropString "FsDocsLogoSource"
                FsDocsNavbarPosition = msbuildPropString "FsDocsNavbarPosition"
                FsDocsTheme = msbuildPropString "FsDocsTheme"
                FsDocsWarnOnMissingDocs = msbuildPropBool "FsDocsWarnOnMissingDocs" |> Option.defaultValue false
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
        | Ok ok -> failwithf "huh? ok = %A" ok
        | Error err -> failwithf "error - %O\nlog - %s" err (String.concat "\n" msgs)

    let getProjectsFromSlnFile (slnPath : string) =
        match InspectSln.tryParseSln slnPath with
        | Ok (_, slnData) ->
            InspectSln.loadingBuildOrder slnData

            //this.LoadProjects(projs, crosstargetingStrategy, useBinaryLogger, numberOfThreads)
        | Error e ->
            raise (exn("cannot load the sln", e))

    let crackProjects (strict, extraMsbuildProperties, userRoot, userCollectionName, userParameters, projects) =
        let slnDir = Path.GetFullPath "."

        //printfn "x.projects = %A" x.projects
        let collectionName, projectFiles =
            match projects with
            | [] ->
                match Directory.GetFiles(slnDir, "*.sln") with
                | [| sln |] ->
                    printfn "getting projects from solution file %s" sln
                    let collectionName = defaultArg userCollectionName (Path.GetFileNameWithoutExtension(sln))
                    collectionName, getProjectsFromSlnFile sln
                | _ ->
                    let projectFiles = [
                        yield! Directory.EnumerateFiles(slnDir, "*.fsproj")
                        for d in Directory.EnumerateDirectories(slnDir) do
                            yield! Directory.EnumerateFiles(d, "*.fsproj")
                            for d2 in Directory.EnumerateDirectories(d) do
                                yield! Directory.EnumerateFiles(d2, "*.fsproj")
                    ]

                    let collectionName =
                        match projectFiles with
                        | [file1] -> Path.GetFileNameWithoutExtension file1
                        | _ -> Path.GetFileName slnDir
                        |> defaultArg userCollectionName

                    collectionName, projectFiles

            | projectFiles ->
                let collectionName = Path.GetFileName(slnDir)
                collectionName, projectFiles

          //printfn "projects = %A" projectFiles
        let projectFiles =
            projectFiles |> List.choose (fun s ->
                if s.Contains(".Tests") || s.Contains("test") then
                    printfn "  skipping project '%s' because it looks like a test project" (Path.GetFileName s)
                    None
                else
                    Some s)

        //printfn "filtered projects = %A" projectFiles
        if projectFiles.Length = 0 then
            printfn "no project files found, no API docs will be generated"

        printfn "cracking projects..."
        let projectInfos =
            projectFiles
            |> Array.ofList
            |> Array.Parallel.choose (fun p ->
                try
                    Some (crackProjectFile slnDir extraMsbuildProperties p)
                with e ->
                    if strict then exit 1
                    printfn "  skipping project '%s' because an error occurred while cracking it: %A" (Path.GetFileName p) e
                    None)
            |> Array.toList

        //printfn "projectInfos = %A" projectInfos
        let projectInfos =
            projectInfos
            |> List.choose (fun info ->
                let shortName = Path.GetFileName info.ProjectFileName
                if info.TargetPath.IsNone then
                    printfn "  skipping project '%s' because it doesn't have a target path" shortName
                    None
                elif not info.IsLibrary then
                    printfn "  skipping project '%s' because it isn't a library" shortName
                    None
                elif info.IsTestProject then
                    printfn "  skipping project '%s' because it has <IsTestProject> true" shortName
                    None
                elif not info.GenerateDocumentationFile then
                    printfn "  skipping project '%s' because it doesn't have <GenerateDocumentationFile>" shortName
                    None
                else
                    Some info)

        //printfn "projectInfos = %A" projectInfos

        if projectInfos.Length = 0 && projectFiles.Length > 0 then
            printfn "Error while cracking project files, no project files succeeded, exiting."
            exit 1

        let param setting key v =
            match v with
            | Some v -> Some(key, v)
            | None ->
                match setting with
                | Some setting ->
                    printfn "please set '%s' in 'Directory.Build.props'" setting
                | None _ -> ()
                None

        // For the 'docs' directory we use the best info we can find from across all projects
        let projectInfoForDocs =
            {
                ProjectFileName = ""
                TargetPath = None
                IsTestProject = false
                IsLibrary = true
                IsPackable = true
                RepositoryUrl =  projectInfos |> List.tryPick  (fun info -> info.RepositoryUrl) |> Option.map ensureTrailingSlash
                RepositoryType =  projectInfos |> List.tryPick  (fun info -> info.RepositoryType)
                RepositoryBranch = projectInfos |> List.tryPick  (fun info -> info.RepositoryBranch)
                FsDocsCollectionNameLink = projectInfos |> List.tryPick  (fun info -> info.FsDocsCollectionNameLink)
                FsDocsLicenseLink = projectInfos |> List.tryPick  (fun info -> info.FsDocsLicenseLink)
                FsDocsReleaseNotesLink = projectInfos |> List.tryPick  (fun info -> info.FsDocsReleaseNotesLink)
                FsDocsLogoLink = projectInfos |> List.tryPick  (fun info -> info.FsDocsLogoLink)
                FsDocsLogoSource = projectInfos |> List.tryPick  (fun info -> info.FsDocsLogoSource)
                FsDocsSourceFolder = projectInfos |> List.tryPick  (fun info -> info.FsDocsSourceFolder)
                FsDocsSourceRepository = projectInfos |> List.tryPick  (fun info -> info.FsDocsSourceRepository)
                FsDocsNavbarPosition = projectInfos |> List.tryPick  (fun info -> info.FsDocsNavbarPosition)
                FsDocsTheme = projectInfos |> List.tryPick  (fun info -> info.FsDocsTheme)
                FsDocsWarnOnMissingDocs = false
                PackageProjectUrl = projectInfos |> List.tryPick  (fun info -> info.PackageProjectUrl) |> Option.map ensureTrailingSlash
                Authors = projectInfos |> List.tryPick  (fun info -> info.Authors)
                GenerateDocumentationFile = true
                PackageLicenseExpression = projectInfos |> List.tryPick  (fun info -> info.PackageLicenseExpression)
                PackageTags = projectInfos |> List.tryPick  (fun info -> info.PackageTags)
                UsesMarkdownComments = false
                Copyright = projectInfos |> List.tryPick  (fun info -> info.Copyright)
                PackageVersion = projectInfos |> List.tryPick  (fun info -> info.PackageVersion)
                PackageIconUrl = projectInfos |> List.tryPick  (fun info -> info.PackageIconUrl)
                RepositoryCommit = projectInfos |> List.tryPick  (fun info -> info.RepositoryCommit)
            }

        let root =
            let projectUrl = projectInfoForDocs.PackageProjectUrl |> Option.map ensureTrailingSlash
            defaultArg userRoot (defaultArg projectUrl ("/" + collectionName) |> ensureTrailingSlash)

        let parametersForProjectInfo (info: CrackedProjectInfo) =
            let projectUrl = info.PackageProjectUrl |> Option.map ensureTrailingSlash |> Option.defaultValue root
            let repoUrl = info.RepositoryUrl |> Option.map ensureTrailingSlash
            userParameters @ List.choose id [
                param None ParamKeys.``root`` (Some root)
                param None ParamKeys.``fsdocs-authors`` (Some (info.Authors |> Option.defaultValue ""))
                param None ParamKeys.``fsdocs-collection-name`` (Some collectionName)
                param None ParamKeys.``fsdocs-collection-name-link`` (Some (info.FsDocsCollectionNameLink |> Option.defaultValue projectUrl))
                param None ParamKeys.``fsdocs-copyright`` info.Copyright
                param None ParamKeys.``fsdocs-logo-src`` (Some (defaultArg info.FsDocsLogoSource (sprintf "%simg/logo.png"  root)))
                param None ParamKeys.``fsdocs-navbar-position`` (Some (defaultArg info.FsDocsNavbarPosition "fixed-right"))
                param None ParamKeys.``fsdocs-theme`` (Some (defaultArg info.FsDocsTheme "default"))
                param None ParamKeys.``fsdocs-logo-link`` (Some (info.FsDocsLogoLink |> Option.defaultValue projectUrl))
                param (Some "<FsDocsLicenseLink>") ParamKeys.``fsdocs-license-link`` (info.FsDocsLicenseLink |> Option.orElse (Option.map (sprintf "%sblob/master/LICENSE.md") repoUrl))
                param (Some "<FsDocsReleaseNotesLink>") ParamKeys.``fsdocs-release-notes-link`` (info.FsDocsReleaseNotesLink |> Option.orElse (Option.map (sprintf "%sblob/master/RELEASE_NOTES.md") repoUrl))
                param None ParamKeys.``fsdocs-package-project-url`` (Some projectUrl)
                param None ParamKeys.``fsdocs-package-license-expression`` info.PackageLicenseExpression
                param None ParamKeys.``fsdocs-package-icon-url`` info.PackageIconUrl
                param None ParamKeys.``fsdocs-package-tags`` (Some (info.PackageTags |> Option.defaultValue ""))
                param (Some "<Version>") ParamKeys.``fsdocs-package-version`` info.PackageVersion
                param (Some "<RepositoryUrl>") ParamKeys.``fsdocs-repository-link`` repoUrl
                param None ParamKeys.``fsdocs-repository-branch`` info.RepositoryBranch
                param None ParamKeys.``fsdocs-repository-commit`` info.RepositoryCommit
            ]

        let crackedProjects =
            projectInfos
            |> List.map (fun info ->
                let substitutions = parametersForProjectInfo info
                info.TargetPath.Value,
                info.RepositoryUrl,
                info.RepositoryBranch,
                info.RepositoryType,
                info.UsesMarkdownComments,
                info.FsDocsWarnOnMissingDocs,
                info.FsDocsSourceFolder,
                info.FsDocsSourceRepository,
                substitutions
            )

        let paths = [ for info in projectInfos -> Path.GetDirectoryName info.TargetPath.Value ]

        let docsParameters = parametersForProjectInfo projectInfoForDocs

        root, collectionName, crackedProjects, paths, docsParameters
