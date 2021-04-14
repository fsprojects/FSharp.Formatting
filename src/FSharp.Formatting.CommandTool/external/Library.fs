namespace Ionide2.ProjInfo

open System
open System.Collections.Generic
open Microsoft.Build.Evaluation
open Microsoft.Build.Framework
open System.Runtime.Loader
open System.IO
open Microsoft.Build.Execution
open Types
open Microsoft.Build.Graph

[<RequireQualifiedAccess>]
module Init =
    ///Initialize the MsBuild integration. Returns path to MsBuild tool that was detected by Locator. Needs to be called before doing anything else
    let init () =
        let instance = Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults()
        //Workaround from https://github.com/microsoft/MSBuildLocator/issues/86#issuecomment-640275377
        AssemblyLoadContext.Default.add_Resolving
            (fun assemblyLoadContext assemblyName ->
                let path = Path.Combine(instance.MSBuildPath, assemblyName.Name + ".dll")

                if File.Exists path then
                    assemblyLoadContext.LoadFromAssemblyPath path
                else
                    null)

        ToolsPath instance.MSBuildPath

///Low level APIs for single project loading. Doesn't provide caching, and doesn't follow p2p references.
/// In most cases you want to use `Ionide.ProjInf.WorkspaceLoader` type instead
module ProjectLoader =

    type LoadedProject = internal LoadedProject of ProjectInstance

    type ProjectLoadingStatus =
        private
        | Success of LoadedProject
        | Error of string

    let internal logger (writer: StringWriter) =
        { new ILogger with
            member this.Initialize(eventSource: IEventSource) : unit =
                // eventSource.ErrorRaised.Add(fun t -> writer.WriteLine t.Message) //Only log errors
                eventSource.AnyEventRaised.Add(fun t -> writer.WriteLine t.Message)

            member this.Parameters : string = ""

            member this.Parameters
                with set (v: string): unit = printfn "v"

            member this.Shutdown() : unit = ()
            member this.Verbosity : LoggerVerbosity = LoggerVerbosity.Detailed

            member this.Verbosity
                with set (v: LoggerVerbosity): unit = () }

    let getTfm (path: string) =
        let pi = ProjectInstance(path)
        let tfm = pi.GetPropertyValue "TargetFramework"

        if String.IsNullOrWhiteSpace tfm then
            let tfms = pi.GetPropertyValue "TargetFrameworks"
            let actualTFM = tfms.Split(';').[0]
            Some actualTFM
        else
            None

    let createLoggers (paths: string seq) (generateBinlog: bool) (sw: StringWriter) =
        let logger = logger (sw)

        if generateBinlog then
            let loggers =
                paths
                |> Seq.map (fun path -> Microsoft.Build.Logging.BinaryLogger(Parameters = Path.Combine(Path.GetDirectoryName(path), "msbuild.binlog")) :> ILogger)

            [ logger; yield! loggers ]
        else
            [ logger ]

    let getGlobalProps (path: string) (tfm: string option) (globalProperties: (string * string) list)=
        dict [ "ProvideCommandLineArgs", "true"
               "DesignTimeBuild", "true"
               "SkipCompilerExecution", "true"
               "GeneratePackageOnBuild", "false"
               "Configuration", "Debug"
               "DefineExplicitDefaults", "true"
               "BuildProjectReferences", "false"
               "UseCommonOutputDirectory", "false"
               if tfm.IsSome then
                   "TargetFramework", tfm.Value
               if path.EndsWith ".csproj" then
                   "NonExistentFile", Path.Combine("__NonExistentSubDir__", "__NonExistentFile__")
               "DotnetProjInfo", "true" 
               yield! globalProperties ]


    let buildArgs =
        [| "ResolvePackageDependenciesDesignTime"
           "_GenerateCompileDependencyCache"
           "CoreCompile" |]

    let loadProject (path: string) (generateBinlog: bool) (ToolsPath toolsPath) globalProperties =
        try
            let tfm = getTfm path

            let globalProperties = getGlobalProps path tfm globalProperties

            match System.Environment.GetEnvironmentVariable "DOTNET_HOST_PATH" with
            | null
            | "" -> System.Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", Ionide2.ProjInfo.Paths.dotnetRoot)
            | _alreadySet -> ()

            use pc = new ProjectCollection(globalProperties)

            let pi = pc.LoadProject(path, globalProperties, toolsVersion=null)

            use sw = new StringWriter()

            let loggers = createLoggers [ path ] generateBinlog sw

            let pi = pi.CreateProjectInstance()


            let build = pi.Build(buildArgs, loggers)

            let t = sw.ToString()

            if build then
                Success(LoadedProject pi)
            else
                Error(sw.ToString())
        with exc -> Error(exc.Message)

    let getFscArgs (LoadedProject project) =
        project.Items |> Seq.filter (fun p -> p.ItemType = "FscCommandLineArgs") |> Seq.map (fun p -> p.EvaluatedInclude)

    let getCscArgs (LoadedProject project) =
        project.Items |> Seq.filter (fun p -> p.ItemType = "CscCommandLineArgs") |> Seq.map (fun p -> p.EvaluatedInclude)

    let getP2Prefs (LoadedProject project) =
        project.Items
        |> Seq.filter (fun p -> p.ItemType = "_MSBuildProjectReferenceExistent")
        |> Seq.map
            (fun p ->
                let relativePath = p.EvaluatedInclude
                let path = p.GetMetadataValue "FullPath"

                let tfms =
                    if p.HasMetadata "TargetFramework" then
                        p.GetMetadataValue "TargetFramework"
                    else
                        p.GetMetadataValue "TargetFrameworks"

                { RelativePath = relativePath
                  ProjectFileName = path
                  TargetFramework = tfms })

    let getCompileItems (LoadedProject project) =
        project.Items
        |> Seq.filter (fun p -> p.ItemType = "Compile")
        |> Seq.map
            (fun p ->
                let name = p.EvaluatedInclude

                let link =
                    if p.HasMetadata "Link" then
                        Some(p.GetMetadataValue "Link")
                    else
                        None

                let fullPath = p.GetMetadataValue "FullPath"

                { Name = name
                  FullPath = fullPath
                  Link = link })

    let getNuGetReferences (LoadedProject project) =
        project.Items
        |> Seq.filter (fun p -> p.ItemType = "Reference" && p.GetMetadataValue "NuGetSourceType" = "Package")
        |> Seq.map
            (fun p ->
                let name = p.GetMetadataValue "NuGetPackageId"
                let version = p.GetMetadataValue "NuGetPackageVersion"
                let fullPath = p.GetMetadataValue "FullPath"

                { Name = name
                  Version = version
                  FullPath = fullPath })

    let getProperties (LoadedProject project) (properties: string list) =
        project.Properties
        |> Seq.filter (fun p -> List.contains p.Name properties)
        |> Seq.map
            (fun p ->
                { Name = p.Name
                  Value = p.EvaluatedValue })

    let getSdkInfo (props: Property seq) =
        let (|ConditionEquals|_|) (str: string) (arg: string) =
            if System.String.Compare(str, arg, System.StringComparison.OrdinalIgnoreCase) = 0 then
                Some()
            else
                None

        let (|StringList|_|) (str: string) =
            str.Split([| ';' |], System.StringSplitOptions.RemoveEmptyEntries) |> List.ofArray |> Some

        let msbuildPropBool (s: Property) =
            match s.Value.Trim() with
            | "" -> None
            | ConditionEquals "True" -> Some true
            | _ -> Some false

        let msbuildPropStringList (s: Property) =
            match s.Value.Trim() with
            | "" -> []
            | StringList list -> list
            | _ -> []

        let msbuildPropBool (prop) =
            props |> Seq.tryFind (fun n -> n.Name = prop) |> Option.bind msbuildPropBool

        let msbuildPropStringList prop =
            props |> Seq.tryFind (fun n -> n.Name = prop) |> Option.map msbuildPropStringList

        let msbuildPropString prop =
            props |> Seq.tryFind (fun n -> n.Name = prop) |> Option.map (fun n -> n.Value.Trim())

        { IsTestProject = msbuildPropBool "IsTestProject" |> Option.defaultValue false
          Configuration = msbuildPropString "Configuration" |> Option.defaultValue ""
          IsPackable = msbuildPropBool "IsPackable" |> Option.defaultValue false
          TargetFramework = msbuildPropString "TargetFramework" |> Option.defaultValue ""
          TargetFrameworkIdentifier = msbuildPropString "TargetFrameworkIdentifier" |> Option.defaultValue ""
          TargetFrameworkVersion = msbuildPropString "TargetFrameworkVersion" |> Option.defaultValue ""

          MSBuildAllProjects = msbuildPropStringList "MSBuildAllProjects" |> Option.defaultValue []
          MSBuildToolsVersion = msbuildPropString "MSBuildToolsVersion" |> Option.defaultValue ""

          ProjectAssetsFile = msbuildPropString "ProjectAssetsFile" |> Option.defaultValue ""
          RestoreSuccess = msbuildPropBool "RestoreSuccess" |> Option.defaultValue false

          Configurations = msbuildPropStringList "Configurations" |> Option.defaultValue []
          TargetFrameworks = msbuildPropStringList "TargetFrameworks" |> Option.defaultValue []

          RunArguments = msbuildPropString "RunArguments"
          RunCommand = msbuildPropString "RunCommand"

          IsPublishable = msbuildPropBool "IsPublishable" }

    let mapToProject (path: string) (compilerArgs: string seq) (p2p: ProjectReference seq) (compile: CompileItem seq) (nugetRefs: PackageReference seq) (sdkInfo: ProjectSdkInfo) (props: Property seq) (customProps: Property seq) =
        let projDir = Path.GetDirectoryName path

        let outputType, sourceFiles, otherOptions =
            if path.EndsWith ".fsproj" then
                let fscArgsNormalized =
                    //workaround, arguments in rsp can use relative paths
                    compilerArgs |> Seq.map (FscArguments.useFullPaths projDir) |> Seq.toList

                let sourceFiles, otherOptions = fscArgsNormalized |> List.partition (FscArguments.isSourceFile path)
                let outputType = FscArguments.outType fscArgsNormalized
                outputType, sourceFiles, otherOptions
            else
                let cscArgsNormalized =
                    //workaround, arguments in rsp can use relative paths
                    compilerArgs |> Seq.map (CscArguments.useFullPaths projDir) |> Seq.toList

                let sourceFiles, otherOptions = cscArgsNormalized |> List.partition (CscArguments.isSourceFile path)
                let outputType = CscArguments.outType cscArgsNormalized
                outputType, sourceFiles, otherOptions

        let compileItems = sourceFiles |> List.map (VisualTree.getCompileProjectItem (compile |> Seq.toList) path)

        let project =
            { ProjectId = Some path
              ProjectFileName = path
              TargetFramework = sdkInfo.TargetFramework
              SourceFiles = sourceFiles
              OtherOptions = otherOptions
              ReferencedProjects = List.ofSeq p2p
              PackageReferences = List.ofSeq nugetRefs
              LoadTime = DateTime.Now
              TargetPath = props |> Seq.tryFind (fun n -> n.Name = "TargetPath") |> Option.map (fun n -> n.Value) |> Option.defaultValue ""
              ProjectOutputType = outputType
              ProjectSdkInfo = sdkInfo
              Items = compileItems
              CustomProperties = List.ofSeq customProps }


        project


    let getLoadedProjectInfo (path: string) customProperties project =
        // let (LoadedProject p) = project
        // let path = p.FullPath

        let properties =
            [ "OutputType"
              "IsTestProject"
              "TargetPath"
              "Configuration"
              "IsPackable"
              "TargetFramework"
              "TargetFrameworkIdentifier"
              "TargetFrameworkVersion"
              "MSBuildAllProjects"
              "ProjectAssetsFile"
              "RestoreSuccess"
              "Configurations"
              "TargetFrameworks"
              "RunArguments"
              "RunCommand"
              "IsPublishable"
              "BaseIntermediateOutputPath"
              "TargetPath"
              "IsCrossTargetingBuild"
              "TargetFrameworks" ]

        let p2pRefs = getP2Prefs project

        let comandlineArgs =
            if path.EndsWith ".fsproj" then
                getFscArgs project
            else
                getCscArgs project

        let compileItems = getCompileItems project
        let nuGetRefs = getNuGetReferences project
        let props = getProperties project properties
        let sdkInfo = getSdkInfo props
        let customProps = getProperties project customProperties

        if not sdkInfo.RestoreSuccess then
            Result.Error "not restored"
        else

            let proj = mapToProject path comandlineArgs p2pRefs compileItems nuGetRefs sdkInfo props customProps

            Result.Ok proj

    /// <summary>
    /// Main entry point for project loading.
    /// </summary>
    /// <param name="path">Full path to the `.fsproj` file</param>
    /// <param name="toolsPath">Path to MsBuild obtained from `ProjectLoader.init ()`</param>
    /// <param name="generateBinlog">Enable Binary Log generation</param>
    /// <param name="globalProperties">The global properties to use (e.g. Configuration=Release). Some additional global properties are pre-set by the tool</param>
    /// <param name="customProperties">List of additional MsBuild properties that you want to obtain.</param>
    /// <returns>Returns the record instance representing the loaded project or string containing error message</returns>
    let getProjectInfo (path: string) (toolsPath: ToolsPath) (globalProperties: (string*string) list) (generateBinlog: bool) (customProperties: string list) : Result<Types.ProjectOptions, string> =
        let loadedProject = loadProject path generateBinlog toolsPath globalProperties 

        match loadedProject with
        | Success project -> getLoadedProjectInfo path customProperties project
        | Error e -> Result.Error e




open Ionide.ProjInfo.Logging

module WorkspaceLoaderViaProjectGraph =
    let locker = obj ()


type IWorkspaceLoader =
    abstract member LoadProjects : string list * list<string> * bool -> seq<ProjectOptions>
    abstract member LoadProjects : string list -> seq<ProjectOptions>
    abstract member LoadSln : string -> seq<ProjectOptions>

    [<CLIEvent>]
    abstract Notifications : IEvent<WorkspaceProjectState>

type WorkspaceLoaderViaProjectGraph private (toolsPath: ToolsPath, ?globalProperties: (string*string) list) =
    let globalProperties = defaultArg globalProperties []
    let logger = LogProvider.getLoggerFor<WorkspaceLoaderViaProjectGraph> ()
    let loadingNotification = new Event<Types.WorkspaceProjectState>()



    let handleProjectGraphFailures f =
        try
            f () |> Some
        with :? Microsoft.Build.Exceptions.InvalidProjectFileException as e ->
            let p = e.ProjectFile
            loadingNotification.Trigger(WorkspaceProjectState.Failed(p, ProjectNotFound(p)))
            None

    let projectInstanceFactory projectPath (_globalProperties: IDictionary<string,string>) (projectCollection: ProjectCollection) =
        let tfm = ProjectLoader.getTfm projectPath
        //let globalProperties = globalProperties |> Seq.toList |> List.map (fun (KeyValue(k,v)) -> (k,v))
        let globalProperties = ProjectLoader.getGlobalProps projectPath tfm globalProperties
        ProjectInstance(projectPath, globalProperties, toolsVersion=null, projectCollection=projectCollection)

    let projectGraphProjs (paths: string seq) =

        handleProjectGraphFailures
        <| fun () ->
            paths |> Seq.iter (fun p -> loadingNotification.Trigger(WorkspaceProjectState.Loading p))
            let entryPoints = paths |> Seq.map ProjectGraphEntryPoint
            ProjectGraph(entryPoints, projectCollection=ProjectCollection.GlobalProjectCollection, projectInstanceFactory=projectInstanceFactory)

    let projectGraphSln (path: string) =
        handleProjectGraphFailures
        <| fun () ->
            let pg = ProjectGraph(path, ProjectCollection.GlobalProjectCollection, projectInstanceFactory)

            pg.ProjectNodesTopologicallySorted
            |> Seq.distinctBy (fun p -> p.ProjectInstance.FullPath)
            |> Seq.map (fun p -> p.ProjectInstance.FullPath)
            |> Seq.iter (fun p -> loadingNotification.Trigger(WorkspaceProjectState.Loading p))

            pg






    let loadProjects (projects: ProjectGraph, customProperties: string list, generateBinlog: bool) =
        try
            lock WorkspaceLoaderViaProjectGraph.locker
            <| fun () ->
                let allKnown = projects.ProjectNodesTopologicallySorted |> Seq.distinctBy (fun p -> p.ProjectInstance.FullPath)

                let allKnownNames = allKnown |> Seq.map (fun p -> p.ProjectInstance.FullPath) |> Seq.toList

                logger.info (
                    Log.setMessage "Started loading projects {count} {projects}"
                    >> Log.addContextDestructured "count" (allKnownNames |> Seq.length)
                    >> Log.addContextDestructured "projects" (allKnownNames)
                )



                let gbr = GraphBuildRequestData(projects, ProjectLoader.buildArgs, null, BuildRequestDataFlags.ReplaceExistingProjectInstance)
                let bm = BuildManager.DefaultBuildManager
                use sw = new StringWriter()
                let loggers = ProjectLoader.createLoggers allKnownNames generateBinlog sw
                bm.BeginBuild(new BuildParameters(Loggers = loggers))
                let result = bm.BuildRequest gbr

                bm.EndBuild()

                let resultsByNode = result.ResultsByNode |> Seq.map (fun kvp -> kvp.Key) |> Seq.cache
                let buildProjs = resultsByNode |> Seq.map (fun p -> p.ProjectInstance.FullPath) |> Seq.toList

                logger.info (
                    Log.setMessage "{overallCode}, projects built {count} {projects} "
                    >> Log.addContextDestructured "count" (buildProjs |> Seq.length)
                    >> Log.addContextDestructured "projects" (buildProjs)
                    >> Log.addContextDestructured "overallCode" result.OverallResult
                    >> Log.addExn result.Exception
                )

                let projects =
                    resultsByNode
                    |> Seq.map
                        (fun p ->

                            p.ProjectInstance.FullPath, ProjectLoader.getLoadedProjectInfo p.ProjectInstance.FullPath customProperties (ProjectLoader.LoadedProject p.ProjectInstance))

                    |> Seq.choose
                        (fun (projectPath, projectOptionResult) ->
                            match projectOptionResult with
                            | Ok projectOptions ->

                                Some projectOptions
                            | Error e ->
                                logger.error (Log.setMessage "Failed loading projects {error}" >> Log.addContextDestructured "error" e)
                                loadingNotification.Trigger(WorkspaceProjectState.Failed(projectPath, GenericError(projectPath, e)))
                                None)

                let allProjectOptions = projects |> Seq.toList

                allProjectOptions
                |> Seq.iter
                    (fun po ->
                        logger.info (Log.setMessage "Project loaded {project}" >> Log.addContextDestructured "project" po.ProjectFileName)
                        loadingNotification.Trigger(WorkspaceProjectState.Loaded(po, allProjectOptions |> Seq.toList, false)))

                allProjectOptions :> seq<_>
        with e ->
            let msg = e.Message

            logger.error (Log.setMessage "Failed loading" >> Log.addExn e)

            projects.ProjectNodesTopologicallySorted
            |> Seq.distinctBy (fun p -> p.ProjectInstance.FullPath)
            |> Seq.iter
                (fun p ->

                    let p = p.ProjectInstance.FullPath

                    if msg.Contains "The project file could not be loaded." then
                        loadingNotification.Trigger(WorkspaceProjectState.Failed(p, ProjectNotFound(p)))
                    elif msg.Contains "not restored" then
                        loadingNotification.Trigger(WorkspaceProjectState.Failed(p, ProjectNotRestored(p)))
                    else
                        loadingNotification.Trigger(WorkspaceProjectState.Failed(p, GenericError(p, msg))))

            Seq.empty



    interface IWorkspaceLoader with
        override this.LoadProjects(projects: string list, customProperties, generateBinlog: bool) =
            projectGraphProjs projects
            |> Option.map (fun pg -> loadProjects (pg, customProperties, generateBinlog))
            |> Option.defaultValue Seq.empty

        override this.LoadProjects(projects: string list) = this.LoadProjects(projects, [], false)

        override this.LoadSln(sln) = this.LoadSln(sln, [], false)

        [<CLIEvent>]
        override this.Notifications = loadingNotification.Publish

    member this.LoadProjects(projects: string list, customProperties: string list, generateBinlog: bool) =
        (this :> IWorkspaceLoader)
            .LoadProjects(projects, customProperties, generateBinlog)

    member this.LoadProjects(projects: string list, customProperties) =
        this.LoadProjects(projects, customProperties, false)




    member this.LoadProject(project: string, customProperties: string list, generateBinlog: bool) =
        this.LoadProjects([ project ], customProperties, generateBinlog)

    member this.LoadProject(project: string, customProperties: string list) =
        this.LoadProjects([ project ], customProperties)

    member this.LoadProject(project: string) =
        (this :> IWorkspaceLoader)
            .LoadProjects([ project ])


    member this.LoadSln(sln: string, customProperties: string list, generateBinlog: bool) =
        projectGraphSln sln
        |> Option.map (fun pg -> loadProjects (pg, customProperties, generateBinlog))
        |> Option.defaultValue Seq.empty

    member this.LoadSln(sln, customProperties) =
        this.LoadSln(sln, customProperties, false)


    static member Create(toolsPath: ToolsPath, ?globalProperties) =
        WorkspaceLoaderViaProjectGraph(toolsPath, ?globalProperties=globalProperties) :> IWorkspaceLoader

type WorkspaceLoader private (toolsPath: ToolsPath, ?globalProperties: (string * string) list) =
    let globalProperties = defaultArg globalProperties []
    let loadingNotification = new Event<Types.WorkspaceProjectState>()



    interface IWorkspaceLoader with

        [<CLIEvent>]
        override __.Notifications = loadingNotification.Publish

        override __.LoadProjects(projects: string list, customProperties: string list, generateBinlog: bool) =
            let cache = Dictionary<string, ProjectOptions>()

            let getAllKnonw () =
                cache |> Seq.map (fun n -> n.Value) |> Seq.toList

            let rec loadProject p =
                let res = ProjectLoader.getProjectInfo p toolsPath globalProperties generateBinlog customProperties

                match res with
                | Ok project ->
                    try
                        cache.Add(p, project)
                        let lst = project.ReferencedProjects |> Seq.map (fun n -> n.ProjectFileName) |> Seq.toList
                        let info = Some project
                        lst, info
                    with exc ->
                        loadingNotification.Trigger(WorkspaceProjectState.Failed(p, GenericError(p, exc.Message)))
                        [], None
                | Error msg when msg.Contains "The project file could not be loaded." ->
                    loadingNotification.Trigger(WorkspaceProjectState.Failed(p, ProjectNotFound(p)))
                    [], None
                | Error msg when msg.Contains "not restored" ->
                    loadingNotification.Trigger(WorkspaceProjectState.Failed(p, ProjectNotRestored(p)))
                    [], None
                | Error msg when msg.Contains "The operation cannot be completed because a build is already in progress." ->
                    //Try to load project again
                    Threading.Thread.Sleep(50)
                    loadProject p
                | Error msg ->
                    loadingNotification.Trigger(WorkspaceProjectState.Failed(p, GenericError(p, msg)))
                    [], None

            let rec loadProjectList (projectList: string list) =
                for p in projectList do
                    let newList, toTrigger =
                        if cache.ContainsKey p then
                            let project = cache.[p]
                            loadingNotification.Trigger(WorkspaceProjectState.Loaded(project, getAllKnonw (), true)) //TODO: Should it even notify here?
                            let lst = project.ReferencedProjects |> Seq.map (fun n -> n.ProjectFileName) |> Seq.toList
                            lst, None
                        else
                            loadingNotification.Trigger(WorkspaceProjectState.Loading p)
                            loadProject p


                    loadProjectList newList

                    toTrigger
                    |> Option.iter (fun project -> loadingNotification.Trigger(WorkspaceProjectState.Loaded(project, getAllKnonw (), false)))

            loadProjectList projects
            cache |> Seq.map (fun n -> n.Value)

        override this.LoadProjects(projects) = this.LoadProjects(projects, [], false)

        override this.LoadSln(sln) = this.LoadSln(sln, [], false)

    member this.LoadProjects(projects: string list, customProperties: string list, generateBinlog: bool) =
        (this :> IWorkspaceLoader)
            .LoadProjects(projects, customProperties, generateBinlog)

    member this.LoadProjects(projects, customProperties) =
        this.LoadProjects(projects, customProperties, false)



    member this.LoadProject(project, customProperties: string list, generateBinlog: bool) =
        this.LoadProjects([ project ], customProperties, generateBinlog)

    member this.LoadProject(project, customProperties: string list) =
        this.LoadProjects([ project ], customProperties)

    member this.LoadProject(project) =
        (this :> IWorkspaceLoader)
            .LoadProjects([ project ])


    member this.LoadSln(sln, customProperties: string list, generateBinlog: bool) =
        match InspectSln.tryParseSln sln with
        | Ok (_, slnData) ->
            let projs = InspectSln.loadingBuildOrder slnData
            this.LoadProjects(projs, customProperties, generateBinlog)
        | Error d -> failwithf "Cannot load the sln: %A" d

    member this.LoadSln(sln, customProperties) =
        this.LoadSln(sln, customProperties, false)



    static member Create(toolsPath: ToolsPath, ?globalProperties) =
        WorkspaceLoader(toolsPath, ?globalProperties=globalProperties) :> IWorkspaceLoader

type ProjectViewerTree =
    { Name: string
      Items: ProjectViewerItem list }

and [<RequireQualifiedAccess>] ProjectViewerItem = Compile of string * ProjectViewerItemConfig

and ProjectViewerItemConfig = { Link: string }

module ProjectViewer =

    let render (proj: ProjectOptions) =

        let compileFiles =
            let sources = proj.Items

            //the generated assemblyinfo.fs are not shown as sources
            let isGeneratedAssemblyinfo (name: string) =
                let projName = proj.ProjectFileName |> Path.GetFileNameWithoutExtension
                //TODO check is in `obj` dir for the tfm
                //TODO better, get the name from fsproj
                //TODO cs too
                name.EndsWith(sprintf "%s.AssemblyInfo.fs" projName)

            sources
            |> List.choose
                (function
                | ProjectItem.Compile (name, fullpath) -> Some(name, fullpath))
            |> List.filter (fun (_, p) -> not (isGeneratedAssemblyinfo p))

        { ProjectViewerTree.Name = proj.ProjectFileName |> Path.GetFileNameWithoutExtension
          Items =
              compileFiles
              |> List.map (fun (name, fullpath) -> ProjectViewerItem.Compile(fullpath, { ProjectViewerItemConfig.Link = name })) }
