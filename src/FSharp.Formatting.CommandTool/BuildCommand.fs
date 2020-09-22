namespace FSharp.Formatting.CommandTool

open CommandLine

open System
open System.Diagnostics
open System.IO
open System.Globalization
open System.Reflection
open System.Runtime.InteropServices
open System.Runtime.Serialization.Formatters.Binary
open System.Text

open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.CommandTool.Common
open FSharp.Formatting.Templating

open Dotnet.ProjInfo
open Dotnet.ProjInfo.Workspace

open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Operators
open Suave.Filters

[<AutoOpen>]
module Utils =
    let ensureDirectory path =
        let dir = DirectoryInfo(path)
        if not dir.Exists then dir.Create()

    let saveBinary (object:'T) (fileName:string) =
        try Directory.CreateDirectory (Path.GetDirectoryName(fileName)) |> ignore with _ -> ()
        let formatter = BinaryFormatter()
        use fs = new FileStream(fileName, FileMode.Create)
        formatter.Serialize(fs, object)
        fs.Flush()

    let loadBinary<'T> (fileName:string):'T option =
        let formatter = BinaryFormatter()
        use fs = new FileStream(fileName, FileMode.Open)
        try
            let object = formatter.Deserialize(fs) :?> 'T
            Some object
        with e -> None

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

    type CrackedProjectInfo =
        { ProjectFileName : string
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
          RepositoryCommit : string option }

    let crackProjectFile slnDir extraMsbuildProperties (file : string) : CrackedProjectInfo =

        let projDir = Path.GetDirectoryName(file)
        let projectAssetsJsonPath = Path.Combine(projDir, "obj", "project.assets.json")
        if not(File.Exists(projectAssetsJsonPath)) then
            failwithf "project '%s' not restored" file

        let additionalInfo =
            [ "OutputType"
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
        let gp () = Inspect.getProperties (["TargetPath"] @ additionalInfo)

        let loggedMessages = System.Collections.Concurrent.ConcurrentQueue<string>()
        let runCmd exePath args =
           let args = List.append args [ yield "/p:DesignTimeBuild=true"; for p in extraMsbuildProperties do yield ("/p:" + p) ]
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
        | Ok [gpResult] ->
            match gpResult with
            | Ok (Inspect.GetResult.Properties props) ->
                let props = props |> Map.ofList
                //printfn "props = %A" (Map.toList props)
                let msbuildPropString prop = props |> Map.tryFind prop |> Option.bind (function s when String.IsNullOrWhiteSpace(s) -> None | s -> Some s)
                let msbuildPropBool prop = prop |> msbuildPropString |> Option.bind msbuildPropBool

                {  ProjectFileName = file
                   TargetPath = msbuildPropString "TargetPath"
                   IsTestProject = msbuildPropBool "IsTestProject" |> Option.defaultValue false
                   IsLibrary = msbuildPropString "OutputType" |> Option.map (fun s -> s.ToLowerInvariant()) |> ((=) (Some "library"))
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
                   RepositoryCommit = msbuildPropString "RepositoryCommit" }
                
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
                    let projectFiles =
                        [ yield! Directory.EnumerateFiles(slnDir, "*.fsproj")
                          for d in Directory.EnumerateDirectories(slnDir) do
                             yield! Directory.EnumerateFiles(d, "*.fsproj")
                             for d2 in Directory.EnumerateDirectories(d) do
                                yield! Directory.EnumerateFiles(d2, "*.fsproj") ]

                    let collectionName = 
                        defaultArg userCollectionName 
                           (match projectFiles with
                            | [ file1 ] -> Path.GetFileNameWithoutExtension(file1)
                            | _ -> Path.GetFileName(slnDir))

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

        let param setting (ParamKey tag as key) v =
            match v with
            | Some v -> (key, v)
            | None ->
                match setting with
                | Some setting -> printfn "please set '%s' in 'Directory.Build.props'" setting
                | None _ -> ()
                (key, "{{" + tag + "}}")

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
              userParameters @
              [ param None ParamKeys.``root`` (Some root)
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
            [ for info in projectInfos do
                 let substitutions = parametersForProjectInfo info
                 (info.TargetPath.Value,
                  info.RepositoryUrl,
                  info.RepositoryBranch,
                  info.RepositoryType,
                  info.UsesMarkdownComments,
                  info.FsDocsWarnOnMissingDocs,
                  info.FsDocsSourceFolder,
                  info.FsDocsSourceRepository,
                  substitutions) ]
              
        let paths = [ for info in projectInfos -> Path.GetDirectoryName info.TargetPath.Value ]

        let docsParameters = parametersForProjectInfo projectInfoForDocs

        root, collectionName, crackedProjects, paths, docsParameters

/// Convert markdown, script and other content into a static site
type internal DocContent(outputDirectory, previous: Map<_,_>, lineNumbers, fsiEvaluator, substitutions, saveImages, watch, root) =

  let createImageSaver (outputDirectory) =
        // Download images so that they can be embedded
        let wc = new System.Net.WebClient()
        let mutable counter = 0
        fun (url:string) ->
            if url.StartsWith("http") || url.StartsWith("https") then
                counter <- counter + 1
                let ext = Path.GetExtension(url)
                let url2 = sprintf "savedimages/saved%d%s" counter ext
                let fn = sprintf "%s/%s" outputDirectory url2

                ensureDirectory (sprintf "%s/savedimages" outputDirectory)
                printfn "downloading %s --> %s" url fn
                wc.DownloadFile(url, fn)
                url2
            else url

  let processFile (inputFile: string) outputKind template outputPrefix imageSaver =
        [
          let name = Path.GetFileName(inputFile)
          if name.StartsWith(".") then 
              printfn "skipping file %s" inputFile
          elif name.StartsWith "_template" then 
              ()
          else
              let isFsx = inputFile.EndsWith(".fsx", true, CultureInfo.InvariantCulture) 
              let isMd = inputFile.EndsWith(".md", true, CultureInfo.InvariantCulture)

              // A _template.tex or _template.pynb is needed to generate those files
              match outputKind, template with
              | OutputKind.Pynb, None -> ()
              | OutputKind.Latex, None -> ()
              | OutputKind.Fsx, None -> ()
              | _ ->

              let imageSaverOpt = 
                match outputKind with
                | OutputKind.Pynb when saveImages <> Some false -> Some imageSaver
                | OutputKind.Latex when saveImages <> Some false -> Some imageSaver
                | OutputKind.Fsx when saveImages = Some true -> Some imageSaver
                | OutputKind.Html when saveImages = Some true -> Some imageSaver
                | _ -> None

              let ext = outputKind.Extension
              let relativeOutputFile =
                  if isFsx || isMd then
                      let basename = Path.GetFileNameWithoutExtension(inputFile)
                      Path.Combine(outputPrefix, sprintf "%s.%s" basename ext)
                  else
                      Path.Combine(outputPrefix, name)

              // Update only when needed - template or file or tool has changed
              let outputFile = Path.GetFullPath(Path.Combine(outputDirectory, relativeOutputFile))
              let changed =
                  let fileChangeTime = try File.GetLastWriteTime(inputFile) with _ -> DateTime.MaxValue
                  let templateChangeTime =
                      match template with
                      | Some t when isFsx || isMd -> try File.GetLastWriteTime(t) with _ -> DateTime.MaxValue
                      | _ -> DateTime.MinValue
                  let toolChangeTime =
                      try File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location) with _ -> DateTime.MaxValue
                  let changeTime = fileChangeTime |> max templateChangeTime |> max toolChangeTime
                  let generateTime = try File.GetLastWriteTime(outputFile) with _ -> System.DateTime.MinValue
                  changeTime > generateTime

              // If it's changed or we don't know anything about it
              // we have to compute the model to get the global substitutions right
              let mainRun = (outputKind = OutputKind.Html)
              let haveModel = previous.TryFind inputFile
              if changed || (watch && mainRun && haveModel.IsNone) then
                  if isFsx then
                      printfn "  generating model for %s --> %s" inputFile relativeOutputFile
                      let model =
                        Literate.ParseAndTransformScriptFile
                          (inputFile, output = relativeOutputFile, outputKind = outputKind,
                            ?formatAgent = None, ?prefix = None, ?fscoptions = None,
                            ?lineNumbers = lineNumbers, references=false, ?fsiEvaluator = fsiEvaluator,
                            substitutions = substitutions,
                            generateAnchors = true,
                            //?customizeDocument = customizeDocument,
                            //?tokenKindToCss = tokenKindToCss,
                            ?imageSaver=imageSaverOpt)

                      yield ((if mainRun then Some (inputFile, model) else None),
                              (fun p ->
                                 printfn "  writing %s --> %s" inputFile relativeOutputFile
                                 ensureDirectory (Path.GetDirectoryName(outputFile))
                                 SimpleTemplating.UseFileAsSimpleTemplate( p@model.Substitutions, template, outputFile)))

                  elif isMd then
                      printfn "  preparing %s --> %s" inputFile relativeOutputFile
                      let model =
                        Literate.ParseAndTransformMarkdownFile
                          (inputFile, output = relativeOutputFile, outputKind = outputKind,
                            ?formatAgent = None, ?prefix = None, ?fscoptions = None,
                            ?lineNumbers = lineNumbers, references=false,
                            substitutions = substitutions,
                            generateAnchors = true,
                            //?customizeDocument=customizeDocument,
                            //?tokenKindToCss = tokenKindToCss,
                            ?imageSaver=imageSaverOpt)

                      yield ( (if mainRun then Some (inputFile, model) else None),
                              (fun p ->
                                  printfn "  writing %s --> %s" inputFile relativeOutputFile
                                  ensureDirectory (Path.GetDirectoryName(outputFile))
                                  SimpleTemplating.UseFileAsSimpleTemplate( p@model.Substitutions, template, outputFile)))

                  else 
                    if mainRun then
                      yield (None, 
                              (fun _p ->
                                  printfn "  copying %s --> %s" inputFile relativeOutputFile
                                  ensureDirectory (Path.GetDirectoryName(outputFile))
                                  // check the file still exists for the incremental case
                                  if (File.Exists inputFile) then
                                     // ignore errors in watch mode
                                     try
                                       File.Copy(inputFile, outputFile, true)
                                       File.SetLastWriteTime(outputFile,DateTime.Now)
                                     with _ when watch -> () ))
              else
                 if mainRun && watch then
                     //printfn "skipping unchanged file %s" inputFile
                     yield (Some (inputFile, haveModel.Value), (fun _ -> ()))
          ]
  let rec processDirectory (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate) indir outputPrefix =
       [
        // Look for the presence of the _template.* files to activate the
        // generation of the content.
        let possibleNewHtmlTemplate = Path.Combine(indir, "_template.html")
        let htmlTemplate = if (try File.Exists(possibleNewHtmlTemplate) with _ -> false) then Some possibleNewHtmlTemplate else htmlTemplate
        let possibleNewPynbTemplate = Path.Combine(indir, "_template.ipynb")
        let pynbTemplate = if (try File.Exists(possibleNewPynbTemplate) with _ -> false) then Some possibleNewPynbTemplate else pynbTemplate
        let possibleNewFsxTemplate = Path.Combine(indir, "_template.fsx")
        let fsxTemplate = if (try File.Exists(possibleNewFsxTemplate) with _ -> false) then Some possibleNewFsxTemplate else fsxTemplate
        let possibleNewLatexTemplate = Path.Combine(indir, "_template.tex")
        let texTemplate = if (try File.Exists(possibleNewLatexTemplate) with _ -> false) then Some possibleNewLatexTemplate else texTemplate

        ensureDirectory (Path.Combine(outputDirectory, outputPrefix))

        let inputs = Directory.GetFiles(indir, "*") 
        let imageSaver = createImageSaver (Path.Combine(outputDirectory, outputPrefix))

        // Look for the four different kinds of content
        for input in inputs do
            yield! processFile input OutputKind.Html htmlTemplate outputPrefix imageSaver
            yield! processFile input OutputKind.Latex texTemplate outputPrefix imageSaver
            yield! processFile input OutputKind.Pynb pynbTemplate outputPrefix imageSaver
            yield! processFile input OutputKind.Fsx fsxTemplate outputPrefix imageSaver

        for subdir in Directory.EnumerateDirectories(indir) do
            let name = Path.GetFileName(subdir)
            if name.StartsWith "." then
                printfn "  skipping directory %s" subdir
            else
                yield! processDirectory (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate) (Path.Combine(indir, name)) (Path.Combine(outputPrefix, name))
       ]

  member _.Convert(input, htmlTemplate, extraInputs) =

    let inputDirectories = extraInputs @ [(input, ".") ]
    [
      for (inputDirectory, outputPrefix) in inputDirectories do
        yield! processDirectory (htmlTemplate, None, None, None) inputDirectory outputPrefix
    ]

  member _.GetSearchIndexEntries(docModels: (string * LiterateDocModel) list) =
        [| for (_inputFile, model) in docModels do
                match model.IndexText with
                | Some text -> {title=model.Title; content = text; uri=model.Uri(root) }
                | _ -> () |]

  member _.GetNavigationEntries(docModels: (string * LiterateDocModel) list) =
    let modelsForList =
        [ for thing in docModels do
             match thing with
             | (inputFile, model)
                when model.OutputKind = OutputKind.Html &&
                        // Don't put the index in the list
                        not (Path.GetFileNameWithoutExtension(inputFile) = "index") -> model
             | _ -> () ]

    [ if modelsForList.Length > 0 then
            li [Class "nav-header"] [!! "Documentation"]
      for model in modelsForList do
            let link = model.Uri(root)
            li [Class "nav-item"] [ a [Class "nav-link"; (Href link)] [encode model.Title ] ]
    ]
    |> List.map (fun html -> html.ToString()) |> String.concat "             \n"

/// Processes and runs Suave server to host them on localhost
module Serve =

    let refreshEvent = new Event<_>()

    let socketHandler (webSocket : WebSocket) _ = socket {
      while true do
        do!
          refreshEvent.Publish
          |> Control.Async.AwaitEvent
          |> Suave.Sockets.SocketOp.ofAsync
        do! webSocket.send Text (ByteSegment (Encoding.UTF8.GetBytes "refreshed")) true }

    let startWebServer outputDirectory localPort =
        let defaultBinding = defaultConfig.bindings.[0]
        let withPort = { defaultBinding.socketBinding with port = uint16 localPort }
        let serverConfig =
            { defaultConfig with
                bindings = [ { defaultBinding with socketBinding = withPort } ]
                homeFolder = Some outputDirectory }
        let app =
            choose [
              path "/" >=> Redirection.redirect "/index.html"
              path "/websocket" >=> handShake socketHandler
              Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
              >=> Writers.setHeader "Pragma" "no-cache"
              >=> Writers.setHeader "Expires" "0"
              >=> Files.browseHome ]
        startWebServerAsync serverConfig app |> snd |> Async.Start

type CoreBuildOptions(watch) =

    [<Option("input", Required=false, Default="docs", HelpText = "Input directory of documentation content.")>]
    member val input = "" with get, set

    [<Option("projects", Required=false, HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member val projects = Seq.empty<string> with get, set

    [<Option("output", Required = false, HelpText = "Output Directory (default 'output' for 'build' and 'tmp/watch' for 'watch'.")>]
    member val output = "" with get, set

    [<Option("noapidocs", Default= false, Required = false, HelpText = "Disable generation of API docs.")>]
    member val noapidocs = false with get, set

    [<Option("strict", Default= false, Required = false, HelpText = "Fail if there is a problem generating docs.")>]
    member val strict = false with get, set

    [<Option("eval", Default=false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = false with get, set

    [<Option("qualify", Default= false, Required = false, HelpText = "In API doc generation qualify the output by the collection name, e.g. 'reference/FSharp.Core/...' instead of 'reference/...' .")>]
    member val qualify = false with get, set

    [<Option("saveimages", Default= "none", Required = false, HelpText = "Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs.")>]
    member val saveImages = "none" with get, set

    [<Option("sourcefolder", Required = false, HelpText = "Source folder at time of component build (defaults to value of `<FsDocsSourceFolder>` from project file, else current directory)")>]
    member val sourceFolder = "" with get, set

    [<Option("sourcerepo", Required = false, HelpText = "Source repository for github links (defaults to value of `<FsDocsSourceRepository>` from project file, else `<RepositoryUrl>/tree/<RepositoryBranch>` for Git repositories)")>]
    member val sourceRepo = "" with get, set

    [<Option("linenumbers", Default=false, Required = false, HelpText = "Add line numbers.")>]
    member val linenumbers = false with get, set

    [<Option("nonpublic", Default=false, Required = false, HelpText = "The tool will also generate documentation for non-public members")>]
    member val nonpublic = false with get, set

    [<Option("mdcomments", Default=false, Required = false, HelpText = "Assume /// comments in F# code are markdown style (defaults to value of `<UsesMarkdownComments>` from project file)")>]
    member val mdcomments = false with get, set

    [<Option("parameters", Required = false, HelpText = "Additional substitution substitutions for templates.")>]
    member val parameters = Seq.empty<string> with get, set

    [<Option("nodefaultcontent", Required = false, HelpText = "Do not copy default content styles, javascript or use default templates.")>]
    member val nodefaultcontent = false with get, set

    [<Option("property", Required = false, HelpText = "Provide a property to dotnet msbuild, e.g. --property Configuration=Release")>]
    member val extraMsbuildProperties = Seq.empty<string> with get, set

    [<Option("fscoptions", Required=false, HelpText = "Extra flags for F# compiler analysis, e.g. dependency resolution.")>]
    member val fscoptions = Seq.empty<string> with get, set

    [<Option("clean", Required = false, Default=false, HelpText = "Clean the output directory.")>]
    member val clean = false with get, set

    member this.Execute() =
        let protect f = 
            try
                f()
                true
            with
                | :?AggregateException as ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    if this.strict then exit 1
                    false
                | ex ->
                    Log.errorf "received exception :\n %A" ex
                    printfn "Error : \n%O" ex
                    if this.strict then exit 1
                    false

        /// The substitutions as given by the user
        let userParameters =
            (evalPairwiseStringsNoOption this.parameters
                |> List.map (fun (a,b) -> (ParamKey a, b)))

        // Adjust the user substitutions for 'watch' mode root
        let userRoot, userParameters =
            if watch then
                let userRoot = sprintf "http://localhost:%d/" this.port_option
                if (dict userParameters).ContainsKey(ParamKeys.root) then
                   printfn "ignoring user-specified root since in watch mode, root = %s" userRoot
                let userParameters =
                    [ ParamKeys.``root``,  userRoot] @
                    (userParameters |> List.filter (fun (a, _) -> a <> ParamKeys.``root``))
                Some userRoot, userParameters
            else
                let r =
                    match (dict userParameters).TryGetValue(ParamKeys.root) with
                    | true, v -> Some v
                    | _ -> None
                r, userParameters

        let userCollectionName =
            match (dict userParameters).TryGetValue(ParamKeys.``fsdocs-collection-name``) with
            | true, v -> Some v
            | _ -> None

        let (root, collectionName, crackedProjects, paths, docsParameters), _key =
          let projects = Seq.toList this.projects
          let cacheFile = ".fsdocs/cache"
          let getTime p = try File.GetLastWriteTimeUtc(p) with _ -> DateTime.Now
          let key1 =
             (userRoot, this.parameters, projects,
              getTime (typeof<CoreBuildOptions>.Assembly.Location),
              (projects |> List.map getTime |> List.toArray))
          Utils.cacheBinary cacheFile
           (fun (_, key2) -> key1 = key2)
           (fun () -> Crack.crackProjects (this.strict, this.extraMsbuildProperties, userRoot, userCollectionName, userParameters, projects), key1)

        if crackedProjects.Length > 0 then
            printfn "" 
            printfn "Inputs for API Docs:" 
            for (dllFile, _, _, _, _, _, _, _, _) in crackedProjects do
                printfn "    %s" dllFile

        for (dllFile, _, _, _, _, _, _, _, _) in crackedProjects do
            if not (File.Exists dllFile) then
                let msg = sprintf "*** %s does not exist, has it been built? You may need to provide --property Configuration=Release." dllFile
                if this.strict then
                    failwith msg
                else 
                    printfn "%s" msg

        if crackedProjects.Length > 0 then
            printfn "" 
            printfn "Substitutions/parameters:" 
            // Print the substitutions
            for (ParamKey pk, p) in docsParameters do  
                 printfn "  %s --> %s" pk p

            // The substitutions may differ for some projects due to different settings in the project files, if so show that
            let pd = dict docsParameters
            for (dllFile, _, _, _, _, _, _, _, projectParameters) in crackedProjects do
                for (((ParamKey pkv2) as pk2) , p2) in projectParameters do
                if pd.ContainsKey pk2 &&  pd.[pk2] <> p2 then
                    printfn "  (%s) %s --> %s" (Path.GetFileNameWithoutExtension(dllFile)) pkv2 p2

        let apiDocInputs =
            [ for (dllFile, repoUrlOption, repoBranchOption, repoTypeOption, projectMarkdownComments, projectWarn, projectSourceFolder, projectSourceRepo, projectParameters) in crackedProjects -> 
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
                            url + "/" + "tree/" +  branch |> Some
                        | Some url, _, Some "git" ->
                            url + "/" + "tree/" + "master" |> Some
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
                { Path = dllFile;
                  XmlFile = None;
                  SourceRepo = sourceRepo;
                  SourceFolder = Some sourceFolder;
                  Substitutions = Some projectParameters;
                  MarkdownComments = this.mdcomments || projectMarkdownComments;
                  Warn = projectWarn;
                  PublicOnly = not this.nonpublic } ]

        let output =
           if this.output = "" then
              if watch then "tmp/watch" else "output"
           else this.output

        // This is in-package
        //   From .nuget\packages\fsharp.formatting.commandtool\7.1.7\tools\netcoreapp3.1\any
        //   to .nuget\packages\fsharp.formatting.commandtool\7.1.7\templates
        let dir = Path.GetDirectoryName(typeof<CoreBuildOptions>.Assembly.Location)
        let defaultTemplateAttempt1 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "templates", "_template.html"))
        // This is in-repo only
        let defaultTemplateAttempt2 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "_template.html"))
        let defaultTemplate =
           if this.nodefaultcontent then
              None
           else
              if (try File.Exists(defaultTemplateAttempt1) with _ -> false) then
                  Some defaultTemplateAttempt1
              elif (try File.Exists(defaultTemplateAttempt2) with _ -> false) then
                  Some defaultTemplateAttempt2
              else None

        let extraInputs =
           [ if not this.nodefaultcontent then
              // The "extras" content goes in "."
              //   From .nuget\packages\fsharp.formatting.commandtool\7.1.7\tools\netcoreapp3.1\any
              //   to .nuget\packages\fsharp.formatting.commandtool\7.1.7\extras
              let attempt1 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "extras"))
              if (try Directory.Exists(attempt1) with _ -> false) then
                  printfn "using extra content from %s" attempt1
                  (attempt1, ".")
              else 
                  // This is for in-repo use only, assuming we are executing directly from
                  //   src\FSharp.Formatting.CommandTool\bin\Debug\netcoreapp3.1\fsdocs.exe 
                  //   src\FSharp.Formatting.CommandTool\bin\Release\netcoreapp3.1\fsdocs.exe 
                  let attempt2 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "content"))
                  if (try Directory.Exists(attempt2) with _ -> false) then
                      printfn "using extra content from %s" attempt2
                      (attempt2, "content")
                  else
                      printfn "no extra content found at %s or %s" attempt1 attempt2
            ]

        // The incremental state (as well as the files written to disk)
        let mutable latestApiDocGlobalParameters = [ ]
        let mutable latestApiDocPhase2 = (fun _ -> ())
        let mutable latestApiDocSearchIndexEntries = [| |]
        let mutable latestDocContentPhase2 = (fun _ -> ())
        let mutable latestDocContentResults = Map.empty
        let mutable latestDocContentSearchIndexEntries = [| |]
        let mutable latestDocContentGlobalParameters = []

        // Actions to read out the incremental state
        let getLatestGlobalParameters() =
            latestApiDocGlobalParameters @
            latestDocContentGlobalParameters

        let regenerateSearchIndex() =
            let index = Array.append latestApiDocSearchIndexEntries  latestDocContentSearchIndexEntries
            let indxTxt = index |> Newtonsoft.Json.JsonConvert.SerializeObject
            File.WriteAllText(Path.Combine(output, "index.json"), indxTxt)

        // Incrementally convert content
        let runDocContentPhase1 () =
            protect (fun () ->
                //printfn "projectInfos = %A" projectInfos

                printfn "" 
                printfn "Content:" 
                let saveImages = (match this.saveImages with "some" -> None | "none" -> Some false | "all" -> Some true | _ -> None)
                let fsiEvaluator = (if this.eval then Some ( FsiEvaluator(strict=this.strict) :> IFsiEvaluator) else None)
                let docContent =
                    DocContent(output, latestDocContentResults,
                        Some this.linenumbers, fsiEvaluator, docsParameters,
                        saveImages, watch, root)

                let docModels = docContent.Convert(this.input, defaultTemplate, extraInputs)

                let actualDocModels = docModels |> List.map fst |> List.choose id
                let extrasForSearchIndex = docContent.GetSearchIndexEntries(actualDocModels)
                let navEntries = docContent.GetNavigationEntries(actualDocModels)
                let results =
                    Map.ofList [
                       for (thing, _action) in docModels do
                          match thing with
                          | Some res -> res
                          | None -> () ]

                latestDocContentResults <- results
                latestDocContentSearchIndexEntries <- extrasForSearchIndex
                latestDocContentGlobalParameters <- [ ParamKeys.``fsdocs-list-of-documents`` , navEntries ]
                latestDocContentPhase2 <- (fun globals ->

                    printfn "" 
                    printfn "Write Content:" 
                    for (_thing, action) in docModels do
                        action globals

                )
            )

        let runDocContentPhase2 () =
            protect (fun () ->
                let globals = getLatestGlobalParameters()
                latestDocContentPhase2 globals
            )

        // Incrementally generate API docs (actually regenerates everything)
        let runGeneratePhase1 () =
            protect (fun () ->
                if crackedProjects.Length > 0 then

                    if not this.noapidocs then

                        let initialTemplate2 =
                            let t1 = Path.Combine(this.input, "reference", "_template.html")
                            let t2 = Path.Combine(this.input, "_template.html")
                            if File.Exists(t1) then
                                Some t1
                            elif File.Exists(t2) then
                                Some t2
                            else
                                match defaultTemplate with
                                | Some d ->
                                    printfn "note, no template file '%s' or '%s', using default template %s" t1 t2 d
                                    Some d
                                | None -> 
                                    printfn "note, no template file '%s' or '%s', and no default template at '%s'" t1 t2 defaultTemplateAttempt1
                                    None

                        printfn "" 
                        printfn "API docs:" 
                        printfn "  generating model for %d assemblies in API docs..." apiDocInputs.Length
                        let globals, index, phase2 =
                          ApiDocs.GenerateHtmlPhased (
                            inputs = apiDocInputs,
                            output = output,
                            collectionName = collectionName,
                            substitutions = docsParameters,
                            qualify = this.qualify,
                            ?template = initialTemplate2,
                            otherFlags = Seq.toList this.fscoptions,
                            root = root,
                            libDirs = paths,
                            strict = this.strict
                            )

                        latestApiDocSearchIndexEntries <- index
                        latestApiDocGlobalParameters <- globals
                        latestApiDocPhase2 <- phase2 
            )

        let runGeneratePhase2 () =
            protect (fun () ->
                printfn "" 
                printfn "Write API Docs:" 
                let globals = getLatestGlobalParameters()
                latestApiDocPhase2 globals 
                regenerateSearchIndex()
            )

        //-----------------------------------------
        // Clean

        let fullOut = Path.GetFullPath output
        let fullIn = Path.GetFullPath this.input

        if this.clean then
            let rec clean dir =
                for file in Directory.EnumerateFiles(dir) do
                    File.Delete file |> ignore
                for subdir in Directory.EnumerateDirectories dir do
                   if not (Path.GetFileName(subdir).StartsWith ".") then
                       clean subdir
            if output <> "/" && output <> "." && fullOut <> fullIn && not (String.IsNullOrEmpty output) then
                try clean fullOut
                with e -> printfn "warning: error during cleaning, continuing: %s" e.Message
            else
                printfn "warning: skipping cleaning due to strange output path: \"%s\"" output

        if watch then
            printfn "Building docs first time..." 

        //-----------------------------------------
        // Build

        let ok =
            let ok1 = runDocContentPhase1() 
            let ok2 = runGeneratePhase1() 
            let ok2 = ok2 && runGeneratePhase2()
            // Run this second to override anything produced by API generate, e.g.
            // bespoke file for namespaces etc.
            let ok1 = ok1 && runDocContentPhase2() 
            regenerateSearchIndex()
            ok1 && ok2

        //-----------------------------------------
        // Watch

        if watch then

            use docsWatcher = new FileSystemWatcher(this.input)
            use templateWatcher = new FileSystemWatcher(this.input)

            let projectOutputWatchers = [ for input in apiDocInputs -> (new FileSystemWatcher(this.input), input.Path) ]
            use _holder = { new IDisposable with member __.Dispose() = for (p,_) in projectOutputWatchers do p.Dispose() }

            // Only one update at a time
            let monitor = obj()
            // One of each kind of request at a time
            let mutable docsQueued = true
            let mutable generateQueued = true

            let docsDependenciesChanged = Event<_>()
            docsDependenciesChanged.Publish.Add(fun () -> 
                if not docsQueued then
                    docsQueued <- true
                    printfn "Detected change in '%s', scheduling rebuild of docs..."  this.input
                    Async.Start(async {
                        do! Async.Sleep(300)
                        lock monitor (fun () ->
                        docsQueued <- false
                        if runDocContentPhase1() then
                            if runDocContentPhase2() then
                                regenerateSearchIndex()
                        ) }) ) 

            let apiDocsDependenciesChanged = Event<_>()
            apiDocsDependenciesChanged.Publish.Add(fun () -> 
                if not generateQueued then
                    generateQueued <- true
                    printfn "Detected change in built outputs, scheduling rebuild of API docs..."  
                    Async.Start(async {
                        do! Async.Sleep(300)
                        lock monitor (fun () ->
                        generateQueued <- false
                        if runGeneratePhase1() then
                            if runGeneratePhase2() then
                                regenerateSearchIndex()) }))


            // Listen to changes in any input under docs
            docsWatcher.IncludeSubdirectories <- true
            docsWatcher.NotifyFilter <- NotifyFilters.LastWrite
            docsWatcher.Changed.Add (fun _ ->docsDependenciesChanged.Trigger())

            // When _template.* change rebuild everything
            templateWatcher.IncludeSubdirectories <- true
            templateWatcher.Filter <- "_template.html"
            templateWatcher.NotifyFilter <- NotifyFilters.LastWrite
            templateWatcher.Changed.Add (fun _ ->
                docsDependenciesChanged.Trigger()
                apiDocsDependenciesChanged.Trigger())

            // Listen to changes in output DLLs
            for (projectOutputWatcher, projectOutput) in projectOutputWatchers do
               projectOutputWatcher.Filter <- Path.GetFileName(projectOutput)
               projectOutputWatcher.Path <- Path.GetDirectoryName(projectOutput)
               projectOutputWatcher.NotifyFilter <- NotifyFilters.LastWrite
               projectOutputWatcher.Changed.Add (fun _ -> apiDocsDependenciesChanged.Trigger())

            // Start raising events
            docsWatcher.EnableRaisingEvents <- true
            templateWatcher.EnableRaisingEvents <- true
            for (pathWatcher, _path) in projectOutputWatchers do
                pathWatcher.EnableRaisingEvents <- true

            generateQueued <- false
            docsQueued <- false
            if not this.noserver_option then
                printfn "starting server on http://localhost:%d for content in %s" this.port_option fullOut
                Serve.startWebServer fullOut this.port_option
            if not this.nolaunch_option then
                let url = sprintf "http://localhost:%d/%s" this.port_option this.open_option
                printfn "launching browser window to open %s" url
                let OpenBrowser(url: string) =
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) then
                        Process.Start(new ProcessStartInfo(url, UseShellExecute = true)) |> ignore
                    elif (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) then
                        Process.Start("xdg-open", url)  |> ignore
                    elif (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) then
                        Process.Start("open", url) |> ignore
            
                OpenBrowser (url)
            waitForKey watch

        if ok then 0 else 1 

    abstract noserver_option : bool
    default x.noserver_option = false

    abstract nolaunch_option : bool
    default x.nolaunch_option = false

    abstract open_option : string
    default x.open_option = ""

    abstract port_option : int
    default x.port_option = 0

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand() =
    inherit CoreBuildOptions(false)

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults, watch it and serve it")>]
type WatchCommand() =
    inherit CoreBuildOptions(true)

    override x.noserver_option = x.noserver
    [<Option("noserver", Required = false, Default=false, HelpText = "Do not serve content when watching.")>]
    member val noserver = false with get, set

    override x.nolaunch_option = x.nolaunch
    [<Option("nolaunch", Required = false, Default=false, HelpText = "Do not launch a browser window.")>]
    member val nolaunch = false with get, set

    override x.open_option = x.openv
    [<Option("open", Required = false, Default="", HelpText = "URL extension to launch http://localhost:<port>/%s.")>]
    member val openv = "" with get, set

    override x.port_option = x.port
    [<Option("port", Required = false, Default=8901, HelpText = "Port to serve content for http://localhost serving.")>]
    member val port = 8901 with get, set



