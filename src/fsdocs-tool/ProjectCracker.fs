namespace fsdocs

open System
open System.Diagnostics
open System.IO
open System.Runtime.InteropServices
open System.Runtime.Serialization
open System.Xml

open System.Xml.Linq
open FSharp.Formatting.Templating

open Ionide.ProjInfo
open Ionide.ProjInfo.Types

[<AutoOpen>]
module Utils =

    let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

    let dotnet = if isWindows then "dotnet.exe" else "dotnet"

    let fileExists pathToFile =
        try
            File.Exists(pathToFile)
        with _ ->
            false

    // Look for global install of dotnet sdk
    let getDotnetGlobalHostPath () =
        let pf = Environment.GetEnvironmentVariable("ProgramW6432")

        let pf =
            if String.IsNullOrEmpty(pf) then
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            else
                pf

        let candidate = Path.Combine(pf, "dotnet", dotnet)

        if fileExists candidate then
            Some candidate
        else
            // Can't find it --- give up
            None

    // from dotnet/fsharp
    let getDotnetHostPath () =
        // How to find dotnet.exe --- woe is me; probing rules make me sad.
        // Algorithm:
        // 1. Look for DOTNET_HOST_PATH environment variable
        //    this is the main user programable override .. provided by user to find a specific dotnet.exe
        // 2. Probe for are we part of an .NetSDK install
        //    In an sdk install we are always installed in:   sdk\3.0.100-rc2-014234\FSharp
        //    dotnet or dotnet.exe will be found in the directory that contains the sdk directory
        // 3. We are loaded in-process to some other application ... Eg. try .net
        //    See if the host is dotnet.exe ... from net5.0 on this is fairly unlikely
        // 4. If it's none of the above we are going to have to rely on the path containing the way to find dotnet.exe
        // Use the path to search for dotnet.exe
        let probePathForDotnetHost () =
            let paths =
                let p = Environment.GetEnvironmentVariable("PATH")

                if not (isNull p) then p.Split(Path.PathSeparator) else [||]

            paths |> Array.tryFind (fun f -> fileExists (Path.Combine(f, dotnet)))

        match (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")) with
        // Value set externally
        | value when not (String.IsNullOrEmpty(value)) && fileExists value -> Some value
        | _ ->
            // Probe for netsdk install, dotnet. and dotnet.exe is a constant offset from the location of System.Int32
            let candidate =
                let assemblyLocation = Path.GetDirectoryName(typeof<Int32>.Assembly.Location)
                Path.GetFullPath(Path.Combine(assemblyLocation, "..", "..", "..", dotnet))

            if fileExists candidate then
                Some candidate
            else
                match probePathForDotnetHost () with
                | Some f -> Some(Path.Combine(f, dotnet))
                | None -> getDotnetGlobalHostPath ()

    let ensureDirectory path =
        let dir = DirectoryInfo(path)

        if not dir.Exists then
            dir.Create()

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

    let loadBinary<'T> (fileName: string) : 'T option =
        let formatter = DataContractSerializer(typeof<'T>)
        use fs = File.OpenRead(fileName)

        use xw = XmlDictionaryReader.CreateBinaryReader(fs, XmlDictionaryReaderQuotas.Max)

        try
            let object = formatter.ReadObject(xw) :?> 'T
            Some object
        with _ ->
            None

    let cacheBinary cacheFile cacheValid (f: unit -> 'T) : 'T =
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
            else
                None

        match attempt with
        | Some r -> r
        | None ->
            let res = f ()
            saveBinary res cacheFile
            res

    let ensureTrailingSlash (s: string) =
        if s.EndsWith '/' || s.EndsWith(".html", StringComparison.Ordinal) then
            s
        else
            s + "/"

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

module Crack =

    [<return: Struct>]
    let (|ConditionEquals|_|) (str: string) (arg: string) =
        if System.String.Compare(str, arg, System.StringComparison.OrdinalIgnoreCase) = 0 then
            ValueSome()
        else
            ValueNone

    let msbuildPropBool (s: string) =
        let trimmed = s.Trim()

        if String.IsNullOrWhiteSpace trimmed then
            None
        else
            match trimmed with
            | ConditionEquals "True" -> Some true
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

    type private CrackErrors = GetProjectOptionsErrors of error: string * messages: string list

    type CrackedProjectInfo =
        { ProjectFileName: string
          ProjectOptions: ProjectOptions option
          TargetPath: string option
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
          FsDocsAllowExecutableProject: bool
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
          //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
          //PackageReleaseNotes : string option
          RepositoryCommit: string option }

    let private crackProjectFileAndIncludeTargetFrameworks _slnDir extraMsbuildProperties (projectFile: string) =
        let additionalInfo =
            [ "OutputType"
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
              "FsDocsAllowExecutableProject"
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
              "TargetFrameworks"
              "RunArguments" ]

        let customProperties = ("TargetPath" :: additionalInfo)

        let loggedMessages = System.Collections.Concurrent.ConcurrentQueue<string>()


        let result =
            // Needs to be done before anything else
            let cwd = System.Environment.CurrentDirectory |> System.IO.DirectoryInfo
            let dotnetExe = getDotnetHostPath () |> Option.map System.IO.FileInfo
            let _toolsPath = Init.init cwd dotnetExe
            ProjectLoader.getProjectInfo projectFile extraMsbuildProperties BinaryLogGeneration.Off customProperties
        //file |> Inspect.getProjectInfos loggedMessages.Enqueue msbuildExec [gp] []

        let msgs = (loggedMessages.ToArray() |> Array.toList)

        match result with
        | Ok projOptions ->

            let props =
                projOptions.CustomProperties
                |> List.map (fun p -> p.Name, p.Value)
                |> Map.ofList
            //printfn "props = %A" (Map.toList props)
            let msbuildPropString prop =
                props
                |> Map.tryFind prop
                |> Option.bind (function
                    | s when String.IsNullOrWhiteSpace(s) -> None
                    | s -> Some s)

            let splitTargetFrameworks =
                function
                | Some(s: string) ->
                    s.Split(";", StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s' -> s'.Trim())
                    |> Some
                | _ -> None

            let targetFrameworks = msbuildPropString "TargetFrameworks" |> splitTargetFrameworks

            let msbuildPropBool prop =
                prop |> msbuildPropString |> Option.bind msbuildPropBool

            let projOptions2 =

                { ProjectFileName = projectFile
                  ProjectOptions = Some projOptions
                  TargetPath = msbuildPropString "TargetPath"
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
                  FsDocsAllowExecutableProject =
                    msbuildPropBool "FsDocsAllowExecutableProject" |> Option.defaultValue false
                  UsesMarkdownComments = msbuildPropBool "UsesMarkdownComments" |> Option.defaultValue false
                  PackageProjectUrl = msbuildPropString "PackageProjectUrl"
                  Authors = msbuildPropString "Authors"
                  GenerateDocumentationFile = msbuildPropBool "GenerateDocumentationFile" |> Option.defaultValue false
                  PackageLicenseExpression = msbuildPropString "PackageLicenseExpression"
                  PackageTags = msbuildPropString "PackageTags"
                  Copyright = msbuildPropString "Copyright"
                  PackageVersion = msbuildPropString "PackageVersion"
                  PackageIconUrl = msbuildPropString "PackageIconUrl"
                  RepositoryCommit = msbuildPropString "RepositoryCommit" }

            Ok(targetFrameworks, projOptions2)
        | Error err -> GetProjectOptionsErrors(err, msgs) |> Result.Error

    let private ensureProjectWasRestored (file: string) =
        let projDir = Path.GetDirectoryName(file)
        let projectAssetsJsonPath = Path.Combine(projDir, "obj", "project.assets.json")

        if File.Exists projectAssetsJsonPath then
            ()
        else
            // In dotnet 8 <UseArtifactsOutput> was introduced, see https://learn.microsoft.com/en-us/dotnet/core/sdk/artifacts-output
            // We will try and use CLI-based project evaluation to determine the location of project.assets.json file
            // See https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8#cli-based-project-evaluation
            try
                let path = DotNetCli.msbuild projDir "--getProperty:ProjectAssetsFile"

                if not (File.Exists path) then
                    failwithf $"project '%s{file}' not restored"
            with ex ->
                failwithf $"Failed to detect if the project '%s{file}' was restored"

    let crackProjectFile slnDir extraMsbuildProperties (file: string) : CrackedProjectInfo =
        ensureProjectWasRestored file

        let result = crackProjectFileAndIncludeTargetFrameworks slnDir extraMsbuildProperties file
        //printfn "msgs = %A" msgs
        match result with
        | Ok(Some targetFrameworks, crackedProjectInfo) when
            crackedProjectInfo.TargetPath.IsNone && targetFrameworks.Length > 1
            ->
            // no targetpath and there are multiple target frameworks
            // let us retry with first target framework specified:
            let extraMsbuildPropertiesAndFirstTargetFramework =
                List.append extraMsbuildProperties [ ("TargetFramework", targetFrameworks.[0]) ]

            let result2 =
                crackProjectFileAndIncludeTargetFrameworks slnDir extraMsbuildPropertiesAndFirstTargetFramework file

            match result2 with
            | Ok(_, crackedProjectInfo) -> crackedProjectInfo
            | Error(GetProjectOptionsErrors(err, msgs)) ->
                failwithf "error - %s\nlog - %s" (err.ToString()) (String.concat "\n" msgs)
        | Ok(_, crackedProjectInfo) -> crackedProjectInfo
        | Error(GetProjectOptionsErrors(err, msgs)) ->
            failwithf "error - %s\nlog - %s" (err.ToString()) (String.concat "\n" msgs)

    let getProjectsFromSlnFile (slnPath: string) =
        match InspectSln.tryParseSln slnPath with
        | Ok(_, slnData) -> InspectSln.loadingBuildOrder slnData

        //this.LoadProjects(projs, crosstargetingStrategy, useBinaryLogger, numberOfThreads)
        | Error e -> raise (exn ("cannot load the sln", e))

    let crackProjects
        (onError, extraMsbuildProperties, userRoot, userCollectionName, userParameters, projects, ignoreProjects)
        =
        let slnDir = Path.GetFullPath "."

        //printfn "x.projects = %A" x.projects
        let collectionName, projectFiles =
            match projects, ignoreProjects with
            | [], false ->
                match Directory.GetFiles(slnDir, "*.sln") with
                | [| sln |] ->
                    printfn "getting projects from solution file %s" sln

                    let collectionName = defaultArg userCollectionName (Path.GetFileNameWithoutExtension(sln))

                    collectionName, getProjectsFromSlnFile sln
                | _ ->
                    let projectFiles =
                        [ yield! Directory.EnumerateFiles(slnDir, "*.fsproj")
                          for d in Directory.EnumerateDirectories(slnDir) do
                              yield! Directory.EnumerateFiles(d, "*.fsproj")

                              for d2 in Directory.EnumerateDirectories(d) do
                                  yield! Directory.EnumerateFiles(d2, "*.fsproj") ]

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
            |> List.filter (fun s ->
                let isFSharpFormattingTestProject =
                    s.Contains $"FSharp.ApiDocs.Tests%c{Path.DirectorySeparatorChar}files"
                    || s.EndsWith("FSharp.Formatting.TestHelpers.fsproj", StringComparison.Ordinal)

                if isFSharpFormattingTestProject then
                    printfn
                        $"  skipping project '%s{Path.GetFileName s}' because the project is part of the FSharp.Formatting test suite."

                not isFSharpFormattingTestProject)

        //printfn "filtered projects = %A" projectFiles
        if projectFiles.Length = 0 && (ignoreProjects |> not) then
            printfn "no project files found, no API docs will be generated"

        if ignoreProjects then
            printfn "project files are ignored, no API docs will be generated"

        printfn "cracking projects..."

        let projectInfos =
            projectFiles
            |> Array.ofList
            |> Array.choose (fun p ->
                try
                    Some(crackProjectFile slnDir extraMsbuildProperties p)
                with e ->
                    printfn
                        "  skipping project '%s' because an error occurred while cracking it: %O"
                        (Path.GetFileName p)
                        e

                    if not ignoreProjects then
                        onError "Project cracking failed and --strict is on, exiting"

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
                elif not info.IsLibrary && not info.FsDocsAllowExecutableProject then
                    printfn
                        "  skipping project '%s' because it isn't a library (add <FsDocsAllowExecutableProject>true</FsDocsAllowExecutableProject> to include it)"
                        shortName

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
            printfn "Warning: While cracking project files, no project files succeeded."

        let param setting key v =
            match v with
            | Some v -> Some(key, v)
            | None ->
                match setting with
                | Some setting -> printfn "please set '%s' in 'Directory.Build.props'" setting
                | None -> ()

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
            { ProjectFileName = ""
              ProjectOptions = None
              TargetPath = None
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
              FsDocsAllowExecutableProject = false
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
              RepositoryCommit = projectInfos |> List.tryPick (fun info -> info.RepositoryCommit) }

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
                [ param None ParamKeys.root (Some root)
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
                  param None ParamKeys.``fsdocs-repository-commit`` info.RepositoryCommit ]
            @ userParameters

        let crackedProjects =
            projectInfos
            |> List.choose (fun info ->
                match info.TargetPath, info.ProjectOptions with
                | Some targetPath, Some projectOptions ->
                    let substitutions = parametersForProjectInfo info

                    Some(
                        targetPath,
                        projectOptions.OtherOptions,
                        info.RepositoryUrl,
                        info.RepositoryBranch,
                        info.RepositoryType,
                        info.UsesMarkdownComments,
                        info.FsDocsWarnOnMissingDocs,
                        info.FsDocsSourceFolder,
                        info.FsDocsSourceRepository,
                        substitutions
                    )
                | _ -> None)

        let paths =
            projectInfos
            |> List.choose (fun projectInfo -> projectInfo.TargetPath |> Option.map Path.GetDirectoryName)

        let docsParameters = parametersForProjectInfo projectInfoForDocs
        root, collectionName, crackedProjects, paths, docsParameters
