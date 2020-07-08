namespace FSharp.Formatting.CommandTool

open CommandLine

open System.IO
open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.CommandTool.Common

open Dotnet.ProjInfo

module internal InspectSln =

  open System

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

    data.Items
    |> List.collect projs

module Crack =

    let msbuildPropBool (s: string) =
        match s.Trim() with
        | "" -> None
        | Dotnet.ProjInfo.Inspect.MSBuild.ConditionEquals "True" -> Some true
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
            ]
        let gp () = Dotnet.ProjInfo.Inspect.getProperties (["TargetPath"] @ additionalInfo)

        let loggedMessages = System.Collections.Concurrent.ConcurrentQueue<string>()
        let runCmd exePath args =
           printfn "%s, args = %A" exePath args
           let res = runProcess loggedMessages.Enqueue slnDir exePath (args |> String.concat " ")
           printfn "done..."
           res

        let msbuildPath = Dotnet.ProjInfo.Inspect.MSBuildExePath.DotnetMsbuild "dotnet"
        let msbuildExec = Dotnet.ProjInfo.Inspect.msbuild msbuildPath runCmd

        let result = file |> Dotnet.ProjInfo.Inspect.getProjectInfos loggedMessages.Enqueue msbuildExec [gp] []

        let msgs = (loggedMessages.ToArray() |> Array.toList)
        match result with
        | Ok [gpResult] ->
            match gpResult with
            | Ok (Inspect.GetResult.Properties props) ->
                let props = props |> Map.ofList
                let targetPath =
                    match props |> Map.tryFind "TargetPath" with
                    | Some t -> t
                    | None -> failwith "error, 'TargetPath' property not found"
                let msbuildPropBool prop =
                    props |> Map.tryFind prop |> Option.bind msbuildPropBool
                let isTestProject = msbuildPropBool "IsTestProject" |> Option.defaultValue false
                let isLibrary = props |> Map.tryFind "OutputType" |> Option.map (fun s -> s.ToLowerInvariant()) |> ((=) (Some "library"))
                let isPackable = msbuildPropBool "IsPackable" |> Option.defaultValue false
                (targetPath, isTestProject, isPackable, isLibrary)
            | _ -> failwithf "error - %s" (String.concat "\n" msgs)
        | _ -> failwithf "error - %s" (String.concat "\n" msgs)
                
    let getProjectsFromSlnFile (slnPath : string) =
        match InspectSln.tryParseSln slnPath with
        | Choice1Of2 (_, slnData) ->
            InspectSln.loadingBuildOrder slnData

            //this.LoadProjects(projs, crosstargetingStrategy, useBinaryLogger, numberOfThreads)
        | Choice2Of2 d ->
            failwithf "cannot load the sln: %A" d

type CoreBuildOptions(watch) =

    let mutable useWaitForKey = false 

    [<Option("input", Required=false, Default="docs", HelpText = "Input directory of documentation content, defaults to 'docs'.")>]
    member val input = "" with get, set

    [<Option("projects", Required=false, HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member val projects = Seq.empty<string> with get, set

    [<Option("output", Default= "output", Required = false, HelpText = "Ouput Directory, defaults to 'output' (optional).")>]
    member val output = "" with get, set

    [<Option("generateNotebooks", Default= false, Required = false, HelpText = "Include 'ipynb' notebooks in outputs.")>]
    member val notebooks = false with get, set

    [<Option("eval", Default= true, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = true with get, set

    [<Option("noLineNumbers", Required = false, HelpText = "Don't add line numbers, default is to add line numbers (optional).")>]
    member val noLineNumbers = false with get, set

    [<Option("parameters", Required = false, HelpText = "Substitution parameters for templates.")>]
    member val parameters = Seq.empty<string> with get, set

    member x.Execute() =
        let mutable res = 0
        use watcher = (if watch then new FileSystemWatcher(x.input) else null )
        let run () =
            try
                let slnDir = Path.GetFullPath "."

                printfn "x.projects = %A" x.projects
                let slnName, projects =
                    match Seq.toList x.projects with
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
                    
                printfn "projects = %A" projects
                let projects = projects |> List.filter (fun s -> not (s.Contains(".Tests")) && not (s.Contains("test")))
                printfn "filtered projects = %A" projects
                let projectOutputs =
                    [ for p in projects do
                        printfn "cracking project %s" p
                        let tp, isTestProject, isPackable, isLibrary = Crack.getTargetFromProjectFile slnDir p
                        if (* isPackable &&*)  isLibrary && not isTestProject then
                           yield tp ]
                printfn "projectOutputs = %A" projectOutputs

                let paths = [ for tp in projectOutputs -> Path.GetDirectoryName tp ]
                let parameters = evalPairwiseStringsNoOption x.parameters @ [ ("project-name", slnName) ]
                let templateFile =
                    let t = Path.Combine(x.input, "_template.html")
                    if File.Exists(t) then
                        Some t
                    else
                        printfn "note, expected template file '%s' to exist, proceeding without template" t
                        None

                Literate.ConvertDirectory(
                    x.input,
                    generateAnchors = true,
                    ?template = templateFile,
                    outputDirectory = x.output,
                    format=OutputKind.Html,
                    ?formatAgent = None,
                    ?lineNumbers = Some (not x.noLineNumbers),
                    processRecursive = true,
                    references = true,
                    ?fsiEvaluator = (if x.eval then Some ( FsiEvaluator() :> _) else None),
                    parameters = parameters,
                    includeSource = true
                )
                if x.notebooks then
                    Literate.ConvertDirectory(
                        x.input,
                        generateAnchors = true,
                        template = Path.Combine(x.input, "no-template-for-notebooks.html"),
                        outputDirectory = x.output,
                        format=OutputKind.Pynb,
                        ?formatAgent = None,
                        lineNumbers = true,
                        processRecursive = true,
                        references = true,
                        ?fsiEvaluator = (if x.eval then Some ( FsiEvaluator() :> _) else None),
                        parameters = parameters,
                        includeSource = true
                    )

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

                ApiDocs.GenerateHtml (
                    dllFiles = projectOutputs,
                    outDir = (if x.output = "" then "output/reference" else Path.Combine(x.output, "reference")),
                    parameters = parameters,
                    ?template = initialTemplate2,
                    // TODO: grab source repository metadata from project file
                    //?sourceRepo = (evalString x.sourceRepo),
                    //?sourceFolder = (evalString x.sourceFolder),
                    libDirs = paths
                    )

            with
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

