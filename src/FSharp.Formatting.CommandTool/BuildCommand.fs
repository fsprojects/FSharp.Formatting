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
open Dotnet.ProjInfo.Workspace

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
                   //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
                   //Description = props |> Map.tryFind "Description" 
                   PackageLicenseExpression = props |> Map.tryFind "PackageLicenseExpression" 
                   PackageTags = props |> Map.tryFind "PackageTags" 
                   Copyright = props |> Map.tryFind "Copyright"
                   PackageVersion = props |> Map.tryFind "PackageVersion"
                   PackageIconUrl = props |> Map.tryFind "PackageIconUrl"
                   //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
                   //PackageReleaseNotes = props |> Map.tryFind "PackageReleaseNotes"
                   RepositoryCommit = props |> Map.tryFind "RepositoryCommit" |}
                
            | Ok ok -> failwithf "huh? ok = %A" ok
            | Error err -> failwithf "error - %s\nlog - %s" (err.ToString()) (String.concat "\n" msgs)
        | Ok ok -> failwithf "huh? ok = %A" ok
        | Error err -> failwithf "error - %s\nlog - %s" (err.ToString()) (String.concat "\n" msgs)
                
    let getProjectsFromSlnFile (slnPath : string) =
        match InspectSln.tryParseSln slnPath with
        | Ok (_, slnData) ->
            InspectSln.loadingBuildOrder slnData

            //this.LoadProjects(projs, crosstargetingStrategy, useBinaryLogger, numberOfThreads)
        | Error d ->
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

    [<Option("clean", Required = false, Default=false, HelpText = "Clean the output directory.")>]
    member val clean = false with get, set

    member x.Execute() =
        let mutable res = 0

        let projectName, projectOutputs, paths, parameters, packageProjectUrl, repoUrlOption =
          let projects = Seq.toList x.projects
          let cacheFile = ".fsdocs/cache"
          Utils.cacheBinary cacheFile projects.IsEmpty (fun () ->
            if x.noApiDocs then
                "", [], [], [], "", None
            else
              let slnDir = Path.GetFullPath "."
                
              //printfn "x.projects = %A" x.projects
              let projectName, projectFiles =
                match projects with
                | [] ->
                    match Directory.GetFiles(slnDir, "*.sln") with
                    | [| sln |] ->
                        printfn "getting projects from solution file %s" sln
                        let projectName = Path.GetFileNameWithoutExtension(sln)
                        projectName, Crack.getProjectsFromSlnFile sln
                    | _ -> 
                        let projectFiles =
                            [ yield! Directory.EnumerateFiles(slnDir, "*.fsproj")
                              for d in Directory.EnumerateDirectories(slnDir) do
                                 yield! Directory.EnumerateFiles(d, "*.fsproj")
                                 for d2 in Directory.EnumerateDirectories(d) do
                                    yield! Directory.EnumerateFiles(d2, "*.fsproj") ]
                        let projectName = Path.GetFileName(slnDir)
                        projectName, projectFiles
                            
                | projectFiles -> 
                    let projectName = Path.GetFileName(slnDir)
                    projectName, projectFiles
            
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
              let packageProjectUrl = tryFindValue (fun info -> info.PackageProjectUrl) "PackageProjectUrl" "root" 
              let authors = tryFindValue (fun info -> info.Authors) "Authors" "authors"
              //let description = tryFindValue (fun info -> info.Description) "Description" "description"
              let repoUrlOption = projectInfos |> List.tryPick  (fun info -> info.RepositoryUrl) 
              let repoUrl = tryFindValue (fun info -> info.RepositoryUrl) "RepositoryUrl" "repository-url"
              let packageLicenseExpression = tryFindValue (fun info -> info.PackageLicenseExpression) "PackageLicenseExpression" "package-license"
              let packageTags = tryFindValue (fun info -> info.PackageTags) "PackageTags" "package-tags"
              let packageVersion = tryFindValue (fun info -> info.PackageVersion) "PackageVersion" "package-version"
              let packageIconUrl = tryFindValue (fun info -> info.PackageIconUrl) "PackageIconUrl" "package-icon-url"
              //let packageReleaseNotes = tryFindValue (fun info -> info.PackageReleaseNotes) "PackageReleaseNotes" "package-release-notes"
              let repositoryCommit = tryFindValue (fun info -> info.RepositoryCommit) "RepositoryCommit" "repository-commit"
              let copyright = tryFindValue (fun info -> info.Copyright) "Copyright" "copyright"
              let parameters = 
                [ "project-name", projectName
                  "root", packageProjectUrl
                  "authors", authors
                  //"description", description
                  "repository-url", repoUrl
                  "package-license", packageLicenseExpression
                  //"package-release-notes", packageReleaseNotes
                  "package-icon-url", packageIconUrl
                  "package-tags", packageTags
                  "package-version", packageVersion
                  "repository-commit", repositoryCommit
                  "copyright", copyright]
              let paths = [ for tp in projectOutputs -> Path.GetDirectoryName tp ]
              let parameters = evalPairwiseStringsNoOption x.parameters @ parameters

              for pn, p in parameters do
                  printfn "parameter %s = %s" pn p

              projectName, projectOutputs, paths, parameters, packageProjectUrl, repoUrlOption)

        let runConvert () =
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

            with
                | :?AggregateException as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    res <- -1
                | _ as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    res <- -1

        let runGenerate () =
            try
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
                        let outdir = Path.Combine(x.output, "reference")
                        let index =
                          ApiDocs.GenerateHtml (
                            dllFiles = projectOutputs,
                            outDir = outdir,
                            parameters = parameters,
                            ?template = initialTemplate2,
                            ?sourceRepo = repoUrlOption,
                            //?sigWidth = sigWidth,
                            rootUrl = packageProjectUrl,
                            //?sourceFolder = (evalString x.sourceFolder),
                            libDirs = paths,
                            ?publicOnly = Some (not x.nonPublic),
                            ?markDownComments = Some (not x.xmlComments)
                            )
                        let indxTxt = index |> Newtonsoft.Json.JsonConvert.SerializeObject

                        File.WriteAllText(Path.Combine(x.output, "index.json"), indxTxt)

            with
                | :?AggregateException as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    res <- -1
                | _ as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    res <- -1

        use docsWatcher = (if watch then new FileSystemWatcher(x.input) else null )
        let projectOutputWatchers = (if watch then [ for projectOuput in projectOutputs -> (new FileSystemWatcher(x.input), projectOuput) ] else [] )
        use _holder = { new IDisposable with member __.Dispose() = for (p,_) in projectOutputWatchers do p.Dispose() }

        // Only one update at a time
        let monitor = obj()
        // One of each kind of request at a time
        let mutable docsQueued = true
        let mutable generateQueued = true

        if watch then
            docsWatcher.IncludeSubdirectories <- true
            docsWatcher.NotifyFilter <- NotifyFilters.LastWrite
            useWaitForKey <- true
            docsWatcher.Changed.Add (fun _ ->
                if not docsQueued then
                    docsQueued <- true
                    printfn "Detected change in '%s', scheduling rebuild of docs..."  x.input
                    lock monitor (fun () ->
                        docsQueued <- false
                        runConvert()) ) 

            for (projectOutputWatcher, projectOutput) in projectOutputWatchers do
                
               projectOutputWatcher.Filter <- Path.GetFileName(projectOutput)
               projectOutputWatcher.Path <- Path.GetDirectoryName(projectOutput)
               projectOutputWatcher.NotifyFilter <- NotifyFilters.LastWrite
               projectOutputWatcher.Changed.Add (fun _ ->
                if not generateQueued then
                    generateQueued <- true
                    printfn "Detected change in '%s', scheduling rebuild of API docs..." projectOutput
                    lock monitor (fun () ->
                        generateQueued <- false
                        runGenerate()) ) 

            docsWatcher.EnableRaisingEvents <- true
            for (pathWatcher, _path) in projectOutputWatchers do
                pathWatcher.EnableRaisingEvents <- true
            printfn "Building docs first time..." 

        if x.clean then
            let rec clean dir =
                for file in Directory.EnumerateFiles(dir) do
                    File.Delete file |> ignore
                for subdir in Directory.EnumerateDirectories dir do
                   if not (Path.GetFileName(subdir).StartsWith ".") then
                       clean subdir
            if x.output <> "/" && x.output <> "." && Path.GetFullPath x.output <> Path.GetFullPath x.input && not (String.IsNullOrEmpty x.output) then
                try clean (Path.GetFullPath x.output)
                with e -> printfn "warning: error during cleaning, continuing: %s" e.Message
            else
                printfn "warning: skipping cleaning due to strange output path: \"%s\"" x.output

        if watch then
            printfn "Building docs first time..." 

        lock monitor (fun () -> runConvert(); runGenerate())
        generateQueued <- false
        docsQueued <- false

        waitForKey useWaitForKey
        res

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand() =
    inherit CoreBuildOptions(false)

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults and watch")>]
type WatchCommand() =
    inherit CoreBuildOptions(true)

