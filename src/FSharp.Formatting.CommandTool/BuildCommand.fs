namespace FSharp.Formatting.CommandTool

open CommandLine

open System
open System.IO
open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.CommandTool.Common
open System.Runtime.Serialization
open System.Runtime.Serialization.Formatters.Binary

open Dotnet.ProjInfo

module Utils =
    let saveBinary (object:'T) (fileName:string) =
        try Directory.CreateDirectory (Path.GetDirectoryName(fileName)) |> ignore with _ -> ()
        let formatter = BinaryFormatter()
        let fs = new FileStream(fileName, FileMode.Create)
        formatter.Serialize(fs, object)
        fs.Flush()
        fs.Close()

    let loadBinary<'T> (fileName:string):'T option =
        let formatter = BinaryFormatter()
        let fs = new FileStream(fileName, FileMode.Open)
        try
            let object = formatter.Deserialize(fs) :?> 'T
            fs.Close()
            Some object
        with e -> None

    let cacheBinary cacheFile cacheValid (f: unit -> 'T)  : 'T =
        let attempt =
            if cacheValid && File.Exists(cacheFile) then loadBinary cacheFile
            else None
        match attempt with
        | Some r ->
            printfn "restored project state from '%s'" cacheFile
            r
        | None ->
            let res = f()
            saveBinary res cacheFile
            res

module Inspect =

    module MSBuild =
        type MSbuildCli =
             | Property of string * string
             | Target of string
             | Switch of string
             | Project of string

        let sprintfMsbuildArg a =
            let quote (s: string) =
                if s.Contains(" ")
                then sprintf "\"%s\"" s
                else s

            match a with
             | Property (k,"") -> sprintf "\"/p:%s=\"" k
             | Property (k,v) -> sprintf "/p:%s=%s" k v |> quote
             | Target t -> sprintf "/t:%s" t |> quote
             | Switch w -> sprintf "/%s" w
             | Project w -> w |> quote

        let (|ConditionEquals|_|) (str: string) (arg: string) = 
            if System.String.Compare(str, arg, System.StringComparison.OrdinalIgnoreCase) = 0
            then Some() else None

        let (|StringList|_|) (str: string)  = 
            str.Split([| ';' |], System.StringSplitOptions.RemoveEmptyEntries)
            |> List.ofArray
            |> Some

    open MSBuild

    type GetProjectInfoErrors<'T> =
        | UnexpectedMSBuildResult of string
        | MSBuildFailed of int * 'T
        | MSBuildSkippedTarget

    [<RequireQualifiedAccess>]
    type MSBuildExePath =
        | Path of string
        | DotnetMsbuild of dotnetExePath: string

    let disableEnvVar envVarName =
        let oldEnv =
            match Environment.GetEnvironmentVariable(envVarName) with
            | null -> None
            | s ->
                Environment.SetEnvironmentVariable(envVarName, null)
                Some s
        { new IDisposable with 
            member x.Dispose() =
                match oldEnv with
                | None -> ()
                | Some s -> Environment.SetEnvironmentVariable(envVarName, s) }

    let msbuild msbuildExe run project args =
        let exe, beforeArgs =
            match msbuildExe with
            | MSBuildExePath.Path path -> path, []
            | MSBuildExePath.DotnetMsbuild path -> path, ["msbuild"]
        let msbuildArgs =
            Project(project) :: args @ [ Switch "nologo"; Switch "verbosity:quiet"]
            |> List.map (MSBuild.sprintfMsbuildArg)
    
        //HACK disable FrameworkPathOverride on msbuild, to make installedNETFrameworks work.
        //     That env var is used only in .net sdk to workaround missing gac assemblies on unix
        use disableFrameworkOverrideOnMsbuild =
            match msbuildExe with
            | MSBuildExePath.Path _ -> disableEnvVar "FrameworkPathOverride"
            | MSBuildExePath.DotnetMsbuild _ -> { new IDisposable with member x.Dispose() = () }

        match run exe (beforeArgs @ msbuildArgs) with
        | 0, x -> Ok x
        | n, x -> Result.Error (MSBuildFailed (n,x))

    let writeTargetFile log templates targetFileDestPath =
        // https://github.com/dotnet/cli/issues/5650

        let targetFileTemplate = 
            """
    <?xml version="1.0" encoding="utf-8" standalone="no"?>
    <Project ToolsVersion="14.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <PropertyGroup>
        <MSBuildAllProjects>$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
      </PropertyGroup>
            """
            + (templates |> String.concat (System.Environment.NewLine))
            +
            """
    </Project>
            """

        let targetFileOnDisk =
            if File.Exists(targetFileDestPath) then
                try
                    Some (File.ReadAllText targetFileDestPath)
                with
                | _ -> None
            else
                None

        let newTargetFile = targetFileTemplate.Trim()

        if targetFileOnDisk <> Some newTargetFile then
            log (sprintf "writing helper target file in '%s'" targetFileDestPath)
            File.WriteAllText(targetFileDestPath, newTargetFile)

        Ok targetFileDestPath

    type GetResult =
         | Properties of (string * string) list

    let getNewTempFilePath suffix =
        let outFile = System.IO.Path.GetTempFileName()
        //if File.Exists outFile then File.Delete outFile
        sprintf "%s.%s" outFile suffix

    let bindSkipped f outFile =
        if not(File.Exists outFile) then
            Result.Error MSBuildSkippedTarget
        else
            f outFile

    let parsePropertiesOut outFile =
        let firstAndRest (delim: char) (s: string) =
            match s.IndexOf(delim) with
            | -1 -> None
            | index -> Some(s.Substring(0, index), s.Substring(index + 1))

        let lines =
            File.ReadAllLines(outFile)
            |> Array.filter (fun s -> s.Length > 0)
            |> Array.map (fun s -> match s |> firstAndRest '=' with Some x -> Ok x | None -> Result.Error s)
            |> List.ofArray

        match lines |> List.partition (function Ok _ -> true | Error _ -> false) with
        | l, [] ->
            l
            |> List.choose (function Ok x -> Some x | Error _ -> None)
            |> (fun x -> Ok x)
        | _, err ->
            err
            |> List.choose (function Ok _ -> None | Error x -> Some x)
            |> sprintf "invalid temp file content '%A'"
            |> (fun x -> Result.Error (UnexpectedMSBuildResult x))

    let getProperties props =
        let templateF isCrossgen =
            """
      <Target Name="_Inspect_GetProperties_""" + (if isCrossgen then "CrossGen" else "NotCrossGen") + """"
              Condition=" '$(IsCrossTargetingBuild)' """ + (if isCrossgen then "==" else "!=") + """ 'true' "
              """ + (if isCrossgen then "" else "DependsOnTargets=\"ResolveReferences\"" ) + """ >
        <ItemGroup>
            """
            + (
                props
                |> List.mapi (fun i p -> sprintf """
            <_Inspect_GetProperties_OutLines Include="P%i">
                <PropertyName>%s</PropertyName>
                <PropertyValue>$(%s)</PropertyValue>
            </_Inspect_GetProperties_OutLines>
                                                 """ i p p)
                |> List.map (fun s -> s.TrimEnd())
                |> String.concat (System.Environment.NewLine) )
            +
            """
        </ItemGroup>
        <Message Text="%(_Inspect_GetProperties_OutLines.PropertyName)=%(_Inspect_GetProperties_OutLines.PropertyValue)" Importance="High" />
        <WriteLinesToFile
                Condition=" '$(_Inspect_GetProperties_OutFile)' != '' "
                File="$(_Inspect_GetProperties_OutFile)"
                Lines="@(_Inspect_GetProperties_OutLines -> '%(PropertyName)=%(PropertyValue)')"
                Overwrite="true" 
                Encoding="UTF-8"/>
      </Target>
            """.Trim()

        //doing like that (crossgen/notcrossgen) because ResolveReferences doesnt exists
        //if is crossgen

        let templateAll =
            """
      <Target Name="_Inspect_GetProperties"
              DependsOnTargets="_Inspect_GetProperties_CrossGen;_Inspect_GetProperties_NotCrossGen" />
            """

        let template =
            [ templateF true
              templateF false
              templateAll ]
            |> String.concat (System.Environment.NewLine)
    
        let outFile = getNewTempFilePath "GetProperties.txt"
        let args =
            [ Target "_Inspect_GetProperties"
              Property ("_Inspect_GetProperties_OutFile", outFile) ]
        template, args, (fun () -> outFile
                                   |> bindSkipped parsePropertiesOut
                                   |> Result.map Properties)

    let uninstall_old_target_file log (projPath: string) =
        let projDir, projName = Path.GetDirectoryName(projPath), Path.GetFileName(projPath)
        let objDir = Path.Combine(projDir, "obj")
        let targetFileDestPath = Path.Combine(objDir, (sprintf "%s.proj-info.targets" projName))

        log (sprintf "searching deprecated target file in '%s'." targetFileDestPath)
        if File.Exists targetFileDestPath then
            log (sprintf "found deprecated target file in '%s', deleting." targetFileDestPath)
            File.Delete targetFileDestPath

    let getProjectInfos log msbuildExec getters additionalArgs projPath =

        let templates, argsList, parsers = 
            getters
            |> List.map (fun getArgs -> getArgs ())
            |> List.unzip3

        let args = argsList |> List.concat

        // remove deprecated target file, if exists
        projPath
        |> uninstall_old_target_file log

        getNewTempFilePath "proj-info.hook.targets"
        |> writeTargetFile log templates
        |> Result.bind (fun targetPath -> msbuildExec projPath (args @ additionalArgs @ [ Property("CustomAfterMicrosoftCommonTargets", targetPath); Property("CustomAfterMicrosoftCommonCrossTargetingTargets", targetPath) ]))
        |> Result.map (fun _ -> parsers |> List.map (fun parse -> parse ()))

module internal InspectSln =

      let private normalizeDirSeparators (path: string) =
          match System.IO.Path.DirectorySeparatorChar with
          | '\\' -> path.Replace('/', '\\')
          | '/' -> path.Replace('\\', '/')
          | _ -> path

      type SolutionData = {
            Items: SolutionItem list
            Configurations: SolutionConfiguration list
            }
      and SolutionConfiguration = {
            Id: string
            ConfigurationName: string
            PlatformName: string
            IncludeInBuild: bool
            }
      and SolutionItem = {
            Guid: Guid
            Name: string
            Kind: SolutionItemKind
            }
      and SolutionItemKind =
            | MsbuildFormat of SolutionItemMsbuildConfiguration list
            | Folder of (SolutionItem list) * (string list)
            | Unsupported
            | Unknown
      and SolutionItemMsbuildConfiguration = {
            Id: string
            ConfigurationName: string
            PlatformName: string
            }

      let tryParseSln (slnFilePath: string) = 
        let parseSln (sln: Microsoft.Build.Construction.SolutionFile) =
            let slnDir = Path.GetDirectoryName slnFilePath
            let makeAbsoluteFromSlnDir =
                let makeAbs (path: string) =
                    if Path.IsPathRooted path then
                        path
                    else
                        Path.Combine(slnDir, path)
                        |> Path.GetFullPath
                normalizeDirSeparators >> makeAbs
            let rec parseItem (item: Microsoft.Build.Construction.ProjectInSolution) =
                let parseKind (item: Microsoft.Build.Construction.ProjectInSolution) =
                    match item.ProjectType with
                    | Microsoft.Build.Construction.SolutionProjectType.KnownToBeMSBuildFormat ->
                        (item.RelativePath |> makeAbsoluteFromSlnDir), SolutionItemKind.MsbuildFormat []
                    | Microsoft.Build.Construction.SolutionProjectType.SolutionFolder ->
                        let children =
                            sln.ProjectsInOrder
                            |> Seq.filter (fun x -> x.ParentProjectGuid = item.ProjectGuid)
                            |> Seq.map parseItem
                            |> List.ofSeq
                        let files =
                            item.FolderFiles
                            |> Seq.map makeAbsoluteFromSlnDir
                            |> List.ofSeq
                        item.ProjectName, SolutionItemKind.Folder (children, files)
                    | Microsoft.Build.Construction.SolutionProjectType.EtpSubProject
                    | Microsoft.Build.Construction.SolutionProjectType.WebDeploymentProject
                    | Microsoft.Build.Construction.SolutionProjectType.WebProject ->
                        (item.ProjectName |> makeAbsoluteFromSlnDir), SolutionItemKind.Unsupported
                    | Microsoft.Build.Construction.SolutionProjectType.Unknown
                    | _ ->
                        (item.ProjectName |> makeAbsoluteFromSlnDir), SolutionItemKind.Unknown

                let name, itemKind = parseKind item 
                { Guid = item.ProjectGuid |> Guid.Parse
                  Name = name
                  Kind = itemKind }

            let items =
                sln.ProjectsInOrder
                |> Seq.filter (fun x -> isNull x.ParentProjectGuid)
                |> Seq.map parseItem
            let data = {
                Items = items |> List.ofSeq
                Configurations = []
            }
            (slnFilePath, data)

        try
            slnFilePath
            |> Microsoft.Build.Construction.SolutionFile.Parse
            |> parseSln
            |> Choice1Of2
        with ex ->
            Choice2Of2 ex

      let loadingBuildOrder (data: SolutionData) =

        let rec projs (item: SolutionItem) =
            match item.Kind with
            | MsbuildFormat items ->
                [ item.Name ]
            | Folder (items, _) ->
                items |> List.collect projs
            | Unsupported
            | Unknown ->
                []

        data.Items |> List.collect projs

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


    let getTargetFromProjectFile slnDir (file : string) =

        let projDir = Path.GetDirectoryName(file)
        let projectAssetsJsonPath = Path.Combine(projDir, "obj", "project.assets.json")
        if not(File.Exists(projectAssetsJsonPath)) then
            failwithf "project '%s' not restored" file

        let additionalInfo =
            [ "OutputType"
              "IsTestProject"
              "IsPackable"
              "RepositoryUrl"
              "PackageProjectUrl"
              "Authors"
              "GenerateDocumentationFile"
              //"Description"
              "PackageLicenseExpression"
              "PackageTags"
              "Copyright"
              "PackageVersion"
              "PackageIconUrl"
              "PackageReleaseNotes"
              "RepositoryCommit"
            ]
        let gp () = Inspect.getProperties (["TargetPath"] @ additionalInfo)

        let loggedMessages = System.Collections.Concurrent.ConcurrentQueue<string>()
        let runCmd exePath args =
           let args = List.append args [ "/p:DesignTimeBuild=true" ]
           //printfn "%s, args = %A" exePath args
           let res = runProcess loggedMessages.Enqueue slnDir exePath (args |> String.concat " ")
           //printfn "done..."
           res

        let msbuildPath = Inspect.MSBuildExePath.DotnetMsbuild "dotnet"
        let msbuildExec = Inspect.msbuild msbuildPath runCmd

        let result = file |> Inspect.getProjectInfos loggedMessages.Enqueue msbuildExec [gp] []

        let msgs = (loggedMessages.ToArray() |> Array.toList)
        match result with
        | Ok [gpResult] ->
            match gpResult with
            | Ok (Inspect.GetResult.Properties props) ->
                let props = props |> Map.ofList
                let msbuildPropBool prop = props |> Map.tryFind prop |> Option.bind msbuildPropBool

                {| ProjectFileName = file
                   TargetPath = props |> Map.tryFind "TargetPath"
                   IsTestProject = msbuildPropBool "IsTestProject" |> Option.defaultValue false
                   IsLibrary = props |> Map.tryFind "OutputType" |> Option.map (fun s -> s.ToLowerInvariant()) |> ((=) (Some "library"))
                   IsPackable = msbuildPropBool "IsPackable" |> Option.defaultValue false
                   RepositoryUrl = props |> Map.tryFind "RepositoryUrl" 
                   PackageProjectUrl = props |> Map.tryFind "PackageProjectUrl" 
                   Authors = props |> Map.tryFind "Authors" 
                   GenerateDocumentationFile = msbuildPropBool "GenerateDocumentationFile" |> Option.defaultValue false
                   //Description = props |> Map.tryFind "Description" 
                   PackageLicenseExpression = props |> Map.tryFind "PackageLicenseExpression" 
                   PackageTags = props |> Map.tryFind "PackageTags" 
                   Copyright = props |> Map.tryFind "Copyright"
                   PackageVersion = props |> Map.tryFind "PackageVersion"
                   PackageIconUrl = props |> Map.tryFind "PackageIconUrl"
                   PackageReleaseNotes = props |> Map.tryFind "PackageReleaseNotes"
                   RepositoryCommit = props |> Map.tryFind "RepositoryCommit" |}
                
            | Error err -> failwithf "error - %s\nlog - %s" (err.ToString()) (String.concat "\n" msgs)
        | Ok ok -> failwithf "huh? ok = %A" ok
        | Error err -> failwithf "error - %s\nlog - %s" (err.ToString()) (String.concat "\n" msgs)
                
    let getProjectsFromSlnFile (slnPath : string) =
        match InspectSln.tryParseSln slnPath with
        | Choice1Of2 (_, slnData) ->
            InspectSln.loadingBuildOrder slnData

            //this.LoadProjects(projs, crosstargetingStrategy, useBinaryLogger, numberOfThreads)
        | Choice2Of2 d ->
            failwithf "cannot load the sln: %A" d

type CoreBuildOptions(watch) =

    let mutable useWaitForKey = false 

    [<Option("input", Required=false, Default="docs", HelpText = "Input directory of documentation content.")>]
    member val input = "" with get, set

    [<Option("projects", Required=false, HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member val projects = Seq.empty<string> with get, set

    [<Option("output", Default= "output", Required = false, HelpText = "Ouput Directory.")>]
    member val output = "" with get, set

    [<Option("noApiDocs", Default= false, Required = false, HelpText = "Disable generation of API docs.")>]
    member val noApiDocs = false with get, set

    [<Option("eval", Default= true, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = true with get, set

    [<Option("noLineNumbers", Required = false, HelpText = "Don't add line numbers, default is to add line numbers.")>]
    member val noLineNumbers = false with get, set

    [<Option("nonPublic", Default=false, Required = false, HelpText = "The tool will also generate documentation for non-public members")>]
    member val nonPublic = false with get, set

    [<Option("xmlComments", Default=false, Required = false, HelpText = "Do not use the Markdown parser for in-code comments. Recommended for C# assemblies.")>]
    member val xmlComments = false with get, set

    [<Option("parameters", Required = false, HelpText = "Additional substitution parameters for templates.")>]
    member val parameters = Seq.empty<string> with get, set

    member x.Execute() =
        let mutable res = 0
        use watcher = (if watch then new FileSystemWatcher(x.input) else null )


        let projectOutputs, paths, parameters, repoUrlOption =
          let projects = Seq.toList x.projects
          let cacheFile = ".fsdocs/cache"
          Utils.cacheBinary cacheFile projects.IsEmpty (fun () ->
            if x.noApiDocs then
                [], [], [], None
            else
              let slnDir = Path.GetFullPath "."
                
              //printfn "x.projects = %A" x.projects
              let slnName, projectFiles =
                match projects with
                | [] ->
                    match Directory.GetFiles(slnDir, "*.sln") with
                    | [| sln |] ->
                        printfn "getting projects from solution file %s" sln
                        let slnName = Path.GetFileNameWithoutExtension(sln)
                        slnName, Crack.getProjectsFromSlnFile sln
                    | _ -> 
                        let projectFiles =
                            [ yield! Directory.EnumerateFiles(slnDir, "*.fsproj")
                              for d in Directory.EnumerateDirectories(slnDir) do
                                 yield! Directory.EnumerateFiles(d, "*.fsproj")
                                 for d2 in Directory.EnumerateDirectories(d) do
                                    yield! Directory.EnumerateFiles(d2, "*.fsproj") ]
                        let slnName = Path.GetFileName(slnDir)
                        slnName, projectFiles
                            
                | projectFiles -> 
                    let slnName = Path.GetFileName(slnDir)
                    slnName, projectFiles
            
              //printfn "projects = %A" projectFiles
              let projectFiles =
                projectFiles |> List.choose (fun s ->
                    if s.Contains(".Tests") || s.Contains("test") then
                        printfn "skipping project '%s' because it looks like a test project" (Path.GetFileName s) 
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
                       Some (Crack.getTargetFromProjectFile slnDir p)
                    with e -> 
                       printfn "skipping project '%s' because an error occurred while cracking it: %A" (Path.GetFileName p) e
                       None)
                |> Array.toList
              let projectInfos =
                projectInfos
                |> List.choose (fun info ->
                    let shortName = Path.GetFileName info.ProjectFileName
                    if info.TargetPath.IsNone then
                        printfn "skipping project '%s' because it doesn't have a target path" shortName
                        None
                    elif not info.IsLibrary then 
                        printfn "skipping project '%s' because it isn't a library" shortName
                        None
                    elif info.IsTestProject then 
                        printfn "skipping project '%s' because it has <IsTestProject> true" shortName
                        None
                    elif not info.GenerateDocumentationFile then 
                        printfn "skipping project '%s' because it doesn't have <GenerateDocumentationFile>" shortName
                        None
                    else
                        Some info)
              let projectOutputs =
                projectInfos |> List.map (fun info -> info.TargetPath.Value)

              let tryFindValue f nm tag  =
                projectInfos
                |> List.tryPick f
                |> function
                    | Some url -> url
                    | None ->
                        printfn "no project defined <%s>, the {{%s}} substitution will not be replaced in any HTML templates" nm tag ;
                        "{{" + tag + "}}"
              let root = tryFindValue (fun info -> info.PackageProjectUrl) "PackageProjectUrl" "root" 
              let authors = tryFindValue (fun info -> info.Authors) "Authors" "authors"
              //let description = tryFindValue (fun info -> info.Description) "Description" "description"
              let repoUrlOption = projectInfos |> List.tryPick  (fun info -> info.RepositoryUrl) 
              let repoUrl = tryFindValue (fun info -> info.RepositoryUrl) "RepositoryUrl" "repository-url"
              let packageLicenseExpression = tryFindValue (fun info -> info.PackageLicenseExpression) "PackageLicenseExpression" "package-license"
              let packageTags = tryFindValue (fun info -> info.PackageTags) "PackageTags" "package-tags"
              let packageVersion = tryFindValue (fun info -> info.PackageVersion) "PackageVersion" "package-version"
              let packageIconUrl = tryFindValue (fun info -> info.PackageIconUrl) "PackageIconUrl" "package-icon-url"
              let packageReleaseNotes = tryFindValue (fun info -> info.PackageReleaseNotes) "PackageReleaseNotes" "package-release-notes"
              let repositoryCommit = tryFindValue (fun info -> info.RepositoryCommit) "RepositoryCommit" "repository-commit"
              let copyright = tryFindValue (fun info -> info.Copyright) "Copyright" "copyright"
              let parameters = 
                [ "project-name", slnName
                  "root", root
                  "authors", authors
                  //"description", description
                  "repository-url", repoUrl
                  "package-license", packageLicenseExpression
                  "package-release-notes", packageReleaseNotes
                  "package-icon-url", packageIconUrl
                  "package-tags", packageTags
                  "package-version", packageVersion
                  "repository-commit", repositoryCommit
                  "copyright", copyright]
              let paths = [ for tp in projectOutputs -> Path.GetDirectoryName tp ]
              let parameters = evalPairwiseStringsNoOption x.parameters @ parameters

              projectOutputs, paths, parameters, repoUrlOption)

        let run () =
            try
                //printfn "projectInfos = %A" projectInfos

                Literate.ConvertDirectory(
                    x.input,
                    generateAnchors = true,
                    outputDirectory = x.output,
                    ?formatAgent = None,
                    ?lineNumbers = Some (not x.noLineNumbers),
                    recursive = true,
                    references = false,
                    ?fsiEvaluator = (if x.eval then Some ( FsiEvaluator() :> _) else None),
                    parameters = parameters,
                    includeSource = true
                )

                if projectOutputs.Length > 0 then
                    let initialTemplate2 =
                        let t1 = Path.Combine(x.input, "reference", "_template.html")
                        let t2 = Path.Combine(x.input, "_template.html")
                        if File.Exists(t1) then
                            Some t1
                        elif File.Exists(t2) then
                            Some t2
                        else
                            printfn "note, expected template file '%s' or '%s' to exist, proceeding without template" t1 t2
                            None

                    if not x.noApiDocs then
                        ApiDocs.GenerateHtml (
                            dllFiles = projectOutputs,
                            outDir = (if x.output = "" then "output/reference" else Path.Combine(x.output, "reference")),
                            parameters = parameters,
                            ?template = initialTemplate2,
                            ?sourceRepo = repoUrlOption,
                            //?sourceFolder = (evalString x.sourceFolder),
                            libDirs = paths,
                            ?publicOnly = Some (not x.nonPublic),
                            ?markDownComments = Some (not x.xmlComments)
                            )

            with
                | :?AggregateException as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    res <- -1
                | _ as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    res <- -1

        let monitor = obj()
        let mutable queued = true
        if watch then
            watcher.IncludeSubdirectories <- true
            watcher.NotifyFilter <- NotifyFilters.LastWrite
            useWaitForKey <- true
            watcher.Changed.Add (fun _ ->
                if not queued then
                    queued <- true
                    printfn "Detected change in docs, waiting to rebuild..." 
                    lock monitor (fun () ->
                        queued <- false; run()) ) 
            watcher.EnableRaisingEvents <- true
            printfn "Building docs first time..." 

        lock monitor run
        queued <- false

        waitForKey useWaitForKey
        res

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand() =
    inherit CoreBuildOptions(false)

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults and watch")>]
type WatchCommand() =
    inherit CoreBuildOptions(true)

