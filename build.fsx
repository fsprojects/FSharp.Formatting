// This is a FAKE 5.0 script, run using
//    dotnet fake build

#r "paket:
    storage: none
    source https://api.nuget.org/v3/index.json
    nuget Fake.Core.Target
    nuget Fake.Core.Vault
    nuget Fake.Core.ReleaseNotes
    nuget Fake.DotNet.AssemblyInfoFile
    nuget Fake.DotNet.Cli
    nuget Fake.DotNet.Testing.NUnit
    nuget Fake.DotNet.NuGet
    nuget Fake.DotNet.MsBuild
    nuget Fake.Tools.Git
	nuget Fake.DotNet.Paket //"

#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#r "netstandard"
#endif

open System
open System.IO
open Fake.Core
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.DotNet
open Fake.IO
open Fake.Tools
open Fake.DotNet.NuGet

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Formatting"
let projectTool = "FSharp.Formatting.CommandTool"

let authors = ["Tomas Petricek"; "Oleg Pestov"; "Anh-Dung Phan"; "Xiang Zhang"; "Matthias Dittrich"]
let authorsTool = ["Friedrich Boeckh"; "Tomas Petricek"]

let summary = "A package of libraries for building great F# documentation, samples and blogs"
let summaryTool = "A command line tool for building great F# documentation, samples and blogs"

let description = """
  The package is a collection of libraries that can be used for literate programming
  with F# (great for building documentation) and for generating library documentation
  from inline code comments. The key componments are Markdown parser, tools for formatting
  F# code snippets, including tool tip type information and a tool for generating
  documentation from library metadata."""
let descriptionTool = """
  The package contains a command line version of F# Formatting libraries, which
  can be used for literate programming with F# (great for building documentation)
  and for generating library documentation from inline code comments. The key componments
  are Markdown parser, tools for formatting F# code snippets, including tool tip
  type information and a tool for generating documentation from library metadata."""

let license = "Apache 2.0 License"
let tags = "F# fsharp formatting markdown code fssnip literate programming"

// Read release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"



// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target.create "AssemblyInfo" (fun _ ->
    let info = [
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion
        AssemblyInfo.InformationalVersion release.NugetVersion
        AssemblyInfo.Copyright license
    ]

    AssemblyInfoFile.createFSharp "src/Common/AssemblyInfo.fs" info
    AssemblyInfoFile.createCSharp "src/Common/AssemblyInfo.cs" info
)

// Clean build results
// --------------------------------------------------------------------------------------

Target.create "Clean" (fun _ ->
    !! "bin"
    ++ "temp"
    ++ "docs/output"
    ++ "tests/bin"
    ++ "src/**/obj"
    ++ "tests/FSharp.MetadataFormat.Tests/files/**/bin"
    ++ "tests/FSharp.MetadataFormat.Tests/files/**/obj"
    |> Shell.cleanDirs
    // in case the above pattern is empty as it only matches existing stuff
    ["bin"; "temp"; "docs/output"; "tests/bin"]
    |> Seq.iter Directory.ensure
)


// Update the assembly version numbers in the script file.
// --------------------------------------------------------------------------------------

/// Gets the version no. for a given package in the deployments folder
let getPackageVersion deploymentsDir package =
    try
        if Directory.Exists deploymentsDir |> not then
            failwithf "Package %s was not found, because the deployment directory %s doesn't exist." package deploymentsDir
        let version =
            let dirs = Directory.GetDirectories(deploymentsDir, sprintf "%s*" package)
            if Seq.isEmpty dirs then failwithf "Package %s was not found." package
            let folder = Seq.head dirs
            let index = folder.LastIndexOf package + package.Length + 1
            if index < folder.Length then
                folder.Substring index
            else
                let nuspec = Directory.GetFiles(folder, sprintf "%s.nuspec" package) |> Seq.head
                let doc = Xml.Linq.XDocument.Load(nuspec)
                let vers = doc.Descendants(Xml.Linq.XName.Get("version", doc.Root.Name.NamespaceName))
                (Seq.head vers).Value

        Trace.logfn "Version %s found for package %s" version package
        version
    with
    | exn -> new Exception("Could not detect package version for " + package, exn) |> raise


// Build library
// --------------------------------------------------------------------------------------

let solutionFile = "FSharp.Formatting.sln"


//Target.Create "InstallDotNetCore" (fun _ ->
//    try
//        (Fake.DotNet.Cli.DotnetInfo (fun _ -> Fake.DotNet.Cli.DotNetInfoOptions.Default)).RID
//        |> trace
//    with _ ->
//        Fake.DotNet.Cli.DotnetCliInstall (fun _ -> Fake.DotNet.Cli.DotNetCliInstallOptions.Default )
//        Environment.SetEnvironmentVariable("DOTNET_EXE_PATH", Fake.DotNet.Cli.DefaultDotnetCliDir)
//)

let assertExitCodeZero x =
    if x = 0 then () else
    failwithf "Command failed with exit code %i" x

let runCmdIn workDir exe =
    Printf.ksprintf (fun args ->
        let res =
            (Process.execWithResult (fun info ->
                { info with
                    FileName = exe
                    Arguments = args
                    WorkingDirectory = workDir
                }) TimeSpan.MaxValue)
        res.Messages |> Seq.iter Trace.trace
        res.ExitCode |> assertExitCodeZero)

/// Execute a dotnet cli command
let dotnet workDir = runCmdIn workDir "dotnet"


let restore proj =
    let opts (def:DotNet.Options) =
        { def with
            WorkingDirectory = __SOURCE_DIRECTORY__
        }
    (DotNet.exec opts "restore" (sprintf "%s" (Path.getFullName proj))).Messages |> Seq.iter Trace.trace

Target.create "Build" (fun _ ->
    Paket.restore id

    //restore solutionFile
    DotNet.restore id solutionFile

    solutionFile
    |> DotNet.build (fun opts ->
        { opts with
            Configuration = DotNet.BuildConfiguration.Release
        })
)


// Build tests and generate tasks to run the tests in sequence
// --------------------------------------------------------------------------------------
Target.create "BuildTests" (fun _ ->
    let debugBuild sln =
        //!! sln |> Seq.iter restore
        !! sln |> Seq.iter (fun s -> dotnet "" "restore %s" s)
        !! sln
        |> Seq.iter (fun proj ->
            proj
            |> DotNet.build (fun opts ->
                { opts with
                    Configuration = DotNet.BuildConfiguration.Release
                    OutputPath = Some "tests/bin"
                      }
            )
        )

    debugBuild "tests/*/files/FsLib/FsLib.sln"
    debugBuild "tests/*/files/crefLib/crefLib.sln"
    debugBuild "tests/*/files/csharpSupport/csharpSupport.sln"
    debugBuild "tests/*/files/TestLib/TestLib.sln"
)

open Fake.DotNet.Testing
open Microsoft.FSharp.Core


let testAssemblies =
    [   "FSharp.CodeFormat.Tests"; "FSharp.Literate.Tests";
        "FSharp.Markdown.Tests"; "FSharp.MetadataFormat.Tests" ]
    |> List.map (fun asm -> sprintf "tests/bin/net461/%s.dll" asm)

let testProjects =
    [   "FSharp.CodeFormat.Tests"; "FSharp.Literate.Tests";
        "FSharp.Markdown.Tests"; "FSharp.MetadataFormat.Tests" ]
    |> List.map (fun asm -> sprintf "tests/%s/%s.fsproj" asm asm)

Target.create "DotnetTests" (fun _ ->
    testProjects
    |> Seq.iter (fun proj -> DotNet.test (fun p ->
        { p with ResultsDirectory = Some __SOURCE_DIRECTORY__ }) proj)
)


// --------------------------------------------------------------------------------------
// Build a NuGet package

// TODO: Use FAKE Version
type BreakingPoint =
  | SemVer
  | Minor
  | Patch

// See https://docs.nuget.org/create/versioning
let RequireRange breakingPoint version =
  let v = SemVer.parse version
  match breakingPoint with
  | SemVer ->
    sprintf "[%s,%d.0)" version (int v.Major + 1)
  | Minor -> // Like Semver but we assume that the increase of a minor version is already breaking
    sprintf "[%s,%d.%d)" version v.Major (int v.Minor + 1)
  | Patch -> // Every update breaks
    version |> Fake.DotNet.NuGet.NuGet.RequireExactly

let RunNuget publish apikey =

    NuGet.NuGet (fun p ->
        { p with
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes
            Tags = tags
            OutputPath = "bin"
            AccessKey = apikey
            Publish = publish
            Dependencies =
                [ // We need Razor dependency in the package until we split out Razor into a separate package.
                  "Microsoft.AspNet.Razor", getPackageVersion "packages" "Microsoft.AspNet.Razor" |> RequireRange BreakingPoint.SemVer
                  "FSharp.Compiler.Service", getPackageVersion "packages" "FSharp.Compiler.Service" |> RequireRange BreakingPoint.SemVer
                  "System.ValueTuple", getPackageVersion "packages" "System.ValueTuple" |> RequireRange BreakingPoint.SemVer
                   ] })
        "NuGet/FSharp.Formatting.nuspec"

    NuGet.NuGet (fun p ->
        { p with
            Authors = authors
            Project = "FSharp.Literate"
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes
            Tags = tags
            OutputPath = "bin"
            AccessKey = apikey
            Publish = publish
            Dependencies =
                [ "FSharp.Compiler.Service", getPackageVersion "packages" "FSharp.Compiler.Service" |> RequireRange BreakingPoint.SemVer
                  "System.ValueTuple", getPackageVersion "packages" "System.ValueTuple" |> RequireRange BreakingPoint.SemVer
                   ] })
        "NuGet/FSharp.Literate.nuspec"

    NuGet.NuGet (fun p ->
        { p with
            Authors = authorsTool
            Project = projectTool
            Summary = summaryTool
            Description = descriptionTool
            Version = release.NugetVersion
            ReleaseNotes = String.toLines release.Notes
            Tags = tags
            OutputPath = "bin"
            AccessKey = apikey
            Publish = publish
            Dependencies = [] })
        "NuGet/FSharp.Formatting.CommandTool.nuspec"

Target.create "NuGet" (fun _ -> RunNuget false "")
        
        


// Generate the documentation
// --------------------------------------------------------------------------------------


let commandToolPath = "bin" </> "net461" </> "fsformatting.exe"
let commandToolStartInfo workingDirectory environmentVars args =
    (fun (info:ProcStartInfo) ->
        { info with
            FileName = Path.GetFullPath commandToolPath
            Arguments = args
            WorkingDirectory = workingDirectory
        }
        |> Process.withFramework
        // |> Process.setEnvironmentVariable "MSBuild" MSBuild.msBuildExe
        |> Process.setEnvironmentVariable "GIT" Git.CommandHelper.gitPath
    )


/// Run the given buildscript with FAKE.exe
let executeWithOutput configStartInfo =
    let exitCode =
        Process.execRaw
            configStartInfo
            TimeSpan.MaxValue false ignore ignore
    Threading.Thread.Sleep 1000
    exitCode

let executeWithRedirect errorF messageF configStartInfo =
    let exitCode =
        Process.execRaw
            configStartInfo
            TimeSpan.MaxValue true errorF messageF
    Threading.Thread.Sleep 1000
    exitCode

let executeHelper executer traceMsg failMessage configStartInfo =
    Trace.trace traceMsg
    let exit = executer configStartInfo
    if exit <> 0 then
        failwith failMessage
    ()

let execute = executeHelper executeWithOutput

// Documentation
let buildDocumentationCommandTool args =
  execute
    "Building documentation (CommandTool), this could take some time, please wait..."
    "generating documentation failed"
    (commandToolStartInfo __SOURCE_DIRECTORY__ [] args)


let createArg argName arguments =
    (arguments : string seq)
    |> String.concat "\" \""
    |> fun e -> if String.IsNullOrWhiteSpace e then ""
                else sprintf "--%s \"%s\"" argName e

let commandToolMetadataFormatArgument dllFiles outDir layoutRoots libDirs parameters sourceRepo =
    let dllFilesArg = createArg "dllfiles" dllFiles
    let layoutRootsArgs = createArg "layoutRoots" layoutRoots
    let libDirArgs = createArg "libDirs" libDirs

    let parametersArg =
        parameters
        |> Seq.collect (fun (key, value) -> [key; value])
        |> createArg "parameters"

    let reproAndFolderArg =
        match sourceRepo with
        | Some (repo, folder) -> sprintf "--sourceRepo \"%s\" --sourceFolder \"%s\"" repo folder
        | _ -> ""

    sprintf "metadataFormat --generate %s %s %s %s %s %s"
        dllFilesArg (createArg "outDir" [outDir]) layoutRootsArgs libDirArgs parametersArg
        reproAndFolderArg

let commandToolLiterateArgument inDir outDir layoutRoots parameters =
    let inDirArg = createArg "inputDirectory" [ inDir ]
    let outDirArg = createArg "outputDirectory" [ outDir ]

    let layoutRootsArgs = createArg "layoutRoots" layoutRoots

    let replacementsArgs =
        parameters
        |> Seq.collect (fun (key, value) -> [key; value])
        |> createArg "replacements"

    sprintf "literate --processDirectory %s %s %s %s" inDirArg outDirArg layoutRootsArgs replacementsArgs


Target.create "DogFoodCommandTool" (fun _ ->
    // generate metadata reference
    let dllFiles =
      [ "FSharp.CodeFormat.dll"; "FSharp.Formatting.Common.dll"
        "FSharp.Literate.dll"; "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll"; "FSharp.Formatting.Razor.dll" ]

    let layoutRoots =
      [ "docs/tools"; "misc/templates"; "misc/templates/reference" ]
    let libDirs = [ "bin/" ]
    let parameters =
      [ "page-author", "Matthias Dittrich"
        "project-author", "Matthias Dittrich"
        "page-description", "desc"
        "github-link", "https://github.com/fsprojects/FSharp.Formatting"
        "project-name", "FSharp.Formatting"
        "root", "https://fsprojects.github.io/FSharp.Formatting"
        "project-nuget", "https://www.nuget.org/packages/FSharp.Formatting/"
        "project-github", "https://github.com/fsprojects/FSharp.Formatting" ]
    Shell.cleanDir "temp/api_docs"
    let metadataReferenceArgs =
        commandToolMetadataFormatArgument
            dllFiles "temp/api_docs" layoutRoots libDirs parameters None
    buildDocumentationCommandTool metadataReferenceArgs

    Shell.cleanDir "temp/literate_docs"
    let literateArgs =
        commandToolLiterateArgument
            "docs/content" "temp/literate_docs" layoutRoots parameters
    buildDocumentationCommandTool literateArgs)

let fsiExe = (__SOURCE_DIRECTORY__ @@ "packages" @@ "build" @@ "FSharp.Compiler.Tools" @@ "tools" @@ "fsi.exe")

Target.create "GenerateDocs" (fun _ ->
    execute
        (sprintf "Building documentation, this could take some time, please wait...")
        "generating reference documentation failed"
        (fun p -> { p with 
                       FileName = if Environment.isWindows then fsiExe else "mono"
                       Arguments = (if Environment.isWindows then "" else fsiExe + " ") + "--define:RELEASE --define:REFERENCE --define:HELP --exec generate.fsx"
                       WorkingDirectory = __SOURCE_DIRECTORY__ @@ "docs" @@ "tools" } ))

// --------------------------------------------------------------------------------------
// Release Scripts

let gitHome = "git@github.com:fsprojects"

Target.create "ReleaseDocs" (fun _ ->
    Git.Repository.clone "" (gitHome + "/FSharp.Formatting.git") "temp/gh-pages"
    Git.Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    Shell.copyRecursive "docs/output" "temp/gh-pages" true |> printfn "%A"
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Git.Branches.push "temp/gh-pages"
)

let apikey =  Environment.environVarOrDefault "nugetkey" ""

Target.create "PushPackagesToNugetOrg" (fun _ ->
    if System.String.IsNullOrEmpty apikey then
        failwith "could not push any nuget packages, because environment variable NUGETKEY was not set"
    RunNuget true apikey
)

Target.create "PushReleaseToGithub" (fun _ ->
    Git.Repository.clone "" (gitHome + "/FSharp.Formatting.git") "temp/release"
    Git.Branches.checkoutBranch "temp/release" "release"
    Shell.copyRecursive "bin" "temp/release" true |> printfn "%A"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/release" cmd |> printfn "%s"
    Git.Branches.push "temp/release"
)

Target.create "CreateTag" (fun _ ->
    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion
)

Target.create "Release" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

#r "System.IO.Compression.FileSystem"
Target.create "DownloadPython" (fun _ ->
  if not Environment.isUnix then
    let w = new Net.WebClient()
    let zipFile = "temp"</>"cpython.zip"
    if File.Exists zipFile then File.Delete zipFile
    w.DownloadFile("https://www.python.org/ftp/python/3.5.1/python-3.5.1-embed-amd64.zip", zipFile)
    let cpython = "temp"</>"CPython"
    Shell.cleanDir cpython
    Compression.ZipFile.ExtractToDirectory(zipFile, cpython)
    let cpythonStdLib = cpython</>"stdlib"
    Shell.cleanDir cpythonStdLib
    Compression.ZipFile.ExtractToDirectory(cpython</>"python35.zip", cpythonStdLib)
)

Target.create "CreateTestJson" (fun _ ->
    let targetPath = "temp/CommonMark"
    Shell.cleanDir targetPath
    Git.Repository.clone targetPath "https://github.com/jgm/CommonMark.git" "."

    let pythonExe, stdLib =
      if not Environment.isUnix then
        Path.GetFullPath ("temp"</>"CPython"</>"python.exe"),
        Path.GetFullPath ("temp"</>"CPython"</>"stdlib")
      else "python", ""

    let resultFile = "temp"</>"commonmark-tests.json"
    if File.Exists resultFile then File.Delete resultFile
    ( use fileStream = new StreamWriter(File.Open(resultFile, FileMode.Create))
      executeHelper
        (executeWithRedirect Trace.traceError fileStream.WriteLine)
        "Creating test json file, this could take some time, please wait..."
        "generating documentation failed"
        (fun info ->
            { info with
                FileName = pythonExe
                Arguments = "test/spec_tests.py --dump-tests"
                WorkingDirectory = targetPath
            }.WithEnvironmentVariables [
               "GIT", Git.CommandHelper.gitPath
            ] |> fun info ->
                if not Environment.isUnix then
                    info.WithEnvironmentVariable ("PYTHONPATH", stdLib)
                else info
        )
    )
    File.Copy(resultFile, "tests"</>"commonmark_spec.json")
)

open Fake.Core.TargetOperators


"Clean"
  //==> "InstallDotnetcore"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "BuildTests"

"Build"
  ==> "All"

"BuildTests"
  ==> "DotnetTests"
  ==> "All"

"Build"
    ==> "NuGet"
    ==> "All"

"BuildTests"
    ==> "GenerateDocs"
    ==> "All"

"Build"
  ==> "DogFoodCommandTool"
  ==> "All"

"All"
  ==> "CreateTag"
  ==> "ReleaseDocs"
  ==> "PushPackagesToNugetOrg"
  ==> "PushReleaseToGithub"
  ==> "Release"

"DownloadPython" ==> "CreateTestJson"

Target.runOrDefault "All"
//Target.runOrDefault "Build"
