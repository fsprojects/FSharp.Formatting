#r "paket: groupref netcorebuild //"
#load ".fake/build.fsx/intellisense.fsx"

open System
open System.IO
open Fake.Core
open Fake.Core.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.DotNet
open Fake.DotNet.AssemblyInfo
open Fake.IO
open Fake.Tools

// Workaround https://github.com/fsharp/FAKE/issues/1776
System.Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", null)

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
let release = ReleaseNotes.LoadReleaseNotes "RELEASE_NOTES.md"



// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target.Create "AssemblyInfo" (fun _ ->
    let info = [
        AssemblyInfo.Product project
        AssemblyInfo.Description summary
        AssemblyInfo.Version release.AssemblyVersion
        AssemblyInfo.FileVersion release.AssemblyVersion
        AssemblyInfo.InformationalVersion release.NugetVersion
        AssemblyInfo.Copyright license
    ]

    AssemblyInfoFile.CreateFSharp "src/Common/AssemblyInfo.fs" info
    AssemblyInfoFile.CreateCSharp "src/Common/AssemblyInfo.cs" info
)

// Clean build results
// --------------------------------------------------------------------------------------

Target.Create "Clean" (fun _ ->
    !! "bin"
    ++ "temp"
    ++ "docs/output"
    ++ "tests/bin"
    ++ "tests/FSharp.MetadataFormat.Tests/files/**/bin"
    ++ "tests/FSharp.MetadataFormat.Tests/files/**/obj"
    |> Shell.CleanDirs
    // in case the above pattern is empty as it only matches existing stuff
    ["bin"; "temp"; "docs/output"; "tests/bin"]
    |> Seq.iter Directory.ensure
)


// Update the assembly version numbers in the script file.
// --------------------------------------------------------------------------------------

open System.IO
open Fake.DotNet.Cli
open Fake.Core.Trace
open Fake.Core.String
open Fake.Core.Environment
open Fake.DotNet.NuGet.NuGet
open Fake.DotNet.MsBuild
open Fake.Core.Process

Target.Create "UpdateFsxVersions" (fun _ ->
    let packages = [ "FSharp.Compiler.Service" ]
    let replacements =
        packages |> Seq.map (fun packageName ->
            sprintf "/%s.(.*)/lib" packageName,

            sprintf "/%s.%s/lib" packageName (GetPackageVersion "packages" packageName)
        )
    let path = "./packages/FSharp.Formatting/FSharp.Formatting.fsx"
    let text = File.ReadAllText(path)
    let text =
        (text, replacements)
        ||> Seq.fold (fun text (pattern, replacement) ->
            Text.RegularExpressions.Regex.Replace (text, pattern, replacement)
        )
    File.WriteAllText(path, text)
)


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
            (ExecProcessAndReturnMessages (fun info ->
                { info with
                    FileName = exe
                    Arguments = args
                    WorkingDirectory = workDir
                }) TimeSpan.MaxValue)
        res.Messages |> Seq.iter trace
        res.ExitCode |> assertExitCodeZero)

/// Execute a dotnet cli command
let dotnet workDir = runCmdIn workDir "dotnet"


let restore proj =
    let opts =
        { DotnetOptions.Default with
            WorkingDirectory = __SOURCE_DIRECTORY__
        }
    (Dotnet  opts (sprintf "restore %s" (Path.getFullName proj))).Messages |> Seq.iter trace

Target.Create "Build" (fun _ ->
    //restore solutionFile
    dotnet "" "restore %s" solutionFile
    solutionFile
    |> MsBuild.build (fun opts ->
        { opts with
            RestorePackagesFlag = true
            Targets = ["Rebuild"]
            Verbosity = Some MSBuildVerbosity.Minimal
            Properties =
              [ "VisualStudioVersion", "15.0"
                "Verbosity", "Minimal"
                //"OutputPath", ""
                "Configuration", "Release"
              ]
        })
    //)   MSBuild "" "Rebuild" 
    //|> MSBuildRelease "" "Rebuild"
    //|> ignore
)


// Build tests and generate tasks to run the tests in sequence
// --------------------------------------------------------------------------------------
Target.Create"BuildTests" (fun _ ->
    let debugBuild sln =            
        //!! sln |> Seq.iter restore 
        !! sln |> Seq.iter (fun s -> dotnet "" "restore %s" s)
        !! sln 
        |> Seq.iter (fun proj ->
            proj
            |> MsBuild.build (fun opts ->
                { opts with
                    RestorePackagesFlag = true
                    Targets = ["Build"]
                    Verbosity = Some MSBuildVerbosity.Minimal
                    Properties =
                      [ "VisualStudioVersion", "15.0"
                        "Verbosity", "Minimal"
                        "OutputPath", "tests/bin"
                        "Configuration", "Release" ]}
            )
        )

    debugBuild "tests/*/files/FsLib/FsLib.sln"
    debugBuild "tests/*/files/crefLib/crefLib.sln"
    debugBuild "tests/*/files/csharpSupport/csharpSupport.sln"
    debugBuild "tests/*/files/TestLib/TestLib.sln"
)

open Fake.DotNet.Testing.NUnit3
open Fake.Core.Process
open Microsoft.FSharp.Core


let testAssemblies =
    [   "FSharp.CodeFormat.Tests"; "FSharp.Literate.Tests";
        "FSharp.Markdown.Tests"; "FSharp.MetadataFormat.Tests" ]
    |> List.map (fun asm -> sprintf "tests/bin/%s.dll" asm)

let testProjects =
    [   "FSharp.CodeFormat.Tests"; "FSharp.Literate.Tests";
        "FSharp.Markdown.Tests"; "FSharp.MetadataFormat.Tests" ]
    |> List.map (fun asm -> sprintf "tests/%s/%s.fsproj" asm asm)

// DOTNET TEST
/// dotnet build command options
type DotNetTestOptions =
    {   
        /// Common tool options
        Common: DotnetOptions
        /// Settings to use when running tests (--settings)
        Settings: string option
        /// Lists discovered tests (--list-tests)
        ListTests: bool
        /// Run tests that match the given expression. (--filter)
        ///  Examples:
        ///   Run tests with priority set to 1: --filter "Priority = 1"
        ///   Run a test with the specified full name: --filter "FullyQualifiedName=Namespace.ClassName.MethodName"
        ///   Run tests that contain the specified name: --filter "FullyQualifiedName~Namespace.Class"
        ///   More info on filtering support: https://aka.ms/vstest-filtering
        Filter: string option
        /// Use custom adapters from the given path in the test run. (--test-adapter-path)
        TestAdapterPath: string option
        /// Specify a logger for test results. (--logger)
        Logger: string option
        ///Configuration to use for building the project.  Default for most projects is  "Debug". (--configuration)
        Configuration: BuildConfiguration
        /// Target framework to publish for. The target framework has to be specified in the project file. (--framework)
        Framework: string option
        ///  Directory in which to find the binaries to be run (--output)
        Output: string option
        /// Enable verbose logs for test platform. Logs are written to the provided file. (--diag)
        Diag: string option
        ///  Do not build project before testing. (--no-build)
        NoBuild: bool
        /// The directory where the test results are going to be placed. The specified directory will be created if it does not exist. (--results-directory)
        ResultsDirectory: string option
        /// Enables data collector for the test run. More info here : https://aka.ms/vstest-collect (--collect)
        Collect: string option
        ///  Does not do an implicit restore when executing the command. (--no-restore)
        NoRestore: bool
        /// Arguments to pass runsettings configurations through commandline. Arguments may be specified as name-value pair of the form [name]=[value] after "-- ". Note the space after --.
        RunSettingsArguments : string option
    }

    /// Parameter default values.
    static member Default = {
        Common = DotnetOptions.Default
        Settings = None
        ListTests = false
        Filter = None
        TestAdapterPath = None
        Logger = None
        Configuration = BuildConfiguration.Debug
        Framework = None
        Output = None
        Diag = None
        NoBuild = false
        ResultsDirectory = None
        Collect = None
        NoRestore = false
        RunSettingsArguments = None
    }

/// [omit]
let private argList2 name values =
    values
    |> Seq.collect (fun v -> ["--" + name; sprintf @"""%s""" v])
    |> String.concat " "

/// [omit]
let private argOption name value =
    match value with
        | true -> sprintf "--%s" name
        | false -> ""
/// [omit]
let private buildConfigurationArg (param: BuildConfiguration) =
    sprintf "--configuration %s" 
        (match param with
        | Debug -> "Debug"
        | Release -> "Release"
        | Custom config -> config)
/// [omit]
let private buildTestArgs (param: DotNetTestOptions) =
    [  
        param.Settings |> Option.toList |> argList2 "settings"
        param.ListTests |> argOption "list-tests"
        param.Filter |> Option.toList |> argList2 "filter"
        param.TestAdapterPath |> Option.toList |> argList2 "test-adapter-path"
        param.Logger |> Option.toList |> argList2 "logger"
        buildConfigurationArg param.Configuration
        param.Framework |> Option.toList |> argList2 "framework"
        param.Output |> Option.toList |> argList2 "output"
        param.Diag |> Option.toList |> argList2 "diag"
        param.NoBuild |> argOption "no-build"
        param.ResultsDirectory |> Option.toList |> argList2 "results-directory"
        param.Collect |> Option.toList |> argList2 "collect"
        param.NoRestore |> argOption "no-restore"
    ] |> Seq.filter (not << String.IsNullOrEmpty) |> String.concat " "


/// Execute dotnet build command
/// ## Parameters
///
/// - 'setParams' - set compile command parameters
/// - 'project' - project to compile
let DotnetTest setParams project =    
    use t = Trace.traceTask "Dotnet:test" project
    let param = DotNetTestOptions.Default |> setParams    
    let args = sprintf "test %s %s" project (buildTestArgs param)
    let result = Cli.Dotnet param.Common args
    if not result.OK then failwithf "dotnet test failed with code %i" result.ExitCode
//


Target.Create"DotnetTests" (fun _ ->
    testProjects

    |> Seq.iter (fun proj -> DotnetTest id proj)    
)


Target.Create"RunTests" (fun _ ->
    testAssemblies
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            ToolPath = "./packages/test/NUnit.ConsoleRunner/tools/nunit3-console.exe"
            OutputDir = "TestResults.xml" })
)


// --------------------------------------------------------------------------------------
// Build a NuGet package

// TODO: Contribute this to FAKE
type BreakingPoint =
  | SemVer
  | Minor
  | Patch

// See https://docs.nuget.org/create/versioning
let RequireRange breakingPoint version =
  let v = SemVer.parse version
  match breakingPoint with
  | SemVer ->
    sprintf "[%s,%d.0)" version (v.Major + 1)
  | Minor -> // Like Semver but we assume that the increase of a minor version is already breaking
    sprintf "[%s,%d.%d)" version v.Major (v.Minor + 1)
  | Patch -> // Every update breaks
    version |> Fake.DotNet.NuGet.NuGet.RequireExactly

Target.Create"CopyFSharpCore" (fun _ ->
    // We need to include optdata and sigdata as well, we copy everything to be consistent
    for file in System.IO.Directory.EnumerateFiles("packages" </> "FSharp.Core" </> "lib" </> "net45") do
        let source, binDest = file, "bin" </> Path.GetFileName file
        printfn "Copying %s to %s" source binDest
        File.Copy (source, binDest, true)
)


Target.Create"SetupLibForTests" (fun _ ->

    let copyPackageFiles dir =
        let dir = Path.GetFullPath dir
        for file in System.IO.Directory.EnumerateFiles dir do
            let fileName = Path.GetFileName file
            if not (fileName.StartsWith "FSharp.Compiler.Service.MSBuild.") then
                let source, libDest = file, "tests"</>"bin"</>fileName
                tracefn "Copying %s to %s" source libDest
                File.Copy (source, libDest, true)
    [   "packages" </> "FSharp.Core" </> "lib" </> "net45"
        "packages" </> "System.ValueTuple" </> "lib" </> "portable-net40+sl4+win8+wp8"
        "packages" </> "FSharp.Compiler.Service" </> "lib" </> "net45"
        "packages" </> "FSharp.Data" </> "lib" </> "portable-net45+netcore45"
    ] |> List.iter copyPackageFiles
)


Target.Create"NuGet" (fun _ ->
    NuGet (fun p ->
        { p with
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes
            Tags = tags
            OutputPath = "bin"
            AccessKey = environVarOrDefault "nugetkey" ""
            Publish = hasEnvironVar "nugetkey"
            Dependencies =
                [ // We need Razor dependency in the package until we split out Razor into a separate package.
                  "Microsoft.AspNet.Razor", GetPackageVersion "packages" "Microsoft.AspNet.Razor" |> RequireRange BreakingPoint.SemVer
                  "FSharp.Compiler.Service", GetPackageVersion "packages" "FSharp.Compiler.Service" |> RequireRange BreakingPoint.SemVer
                  "System.ValueTuple", GetPackageVersion "packages" "System.ValueTuple" |> RequireRange BreakingPoint.SemVer
                   ] })
        "nuget/FSharp.Formatting.nuspec"

    NuGet (fun p ->
        { p with
            Authors = authorsTool
            Project = projectTool
            Summary = summaryTool
            Description = descriptionTool
            Version = release.NugetVersion
            ReleaseNotes = toLines release.Notes
            Tags = tags
            OutputPath = "bin"
            AccessKey =  environVarOrDefault "nugetkey" ""
            Publish = hasEnvironVar "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Formatting.CommandTool.nuspec"
)


// Generate the documentation
// --------------------------------------------------------------------------------------


let fakePath = "packages" </> "FAKE" </> "tools" </> "FAKE.exe"
let fakeStartInfo script workingDirectory args fsiargs environmentVars =
    //(fun (info: System.Diagnostics.ProcessStartInfo) ->
    (fun (info: ProcStartInfo) ->
            { info with
                FileName = System.IO.Path.GetFullPath fakePath
                Arguments = sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
                WorkingDirectory = workingDirectory
                Environment =
                    [   "MSBuild", msBuildExe
                        "GIT", Git.CommandHelper.gitPath
                    //    "FSI", Fake.FSIHelper.fsiPath
                    ] |> Map.ofList |> Some
            }
    )

let commandToolPath = "bin" </> "fsformatting.exe"
let commandToolStartInfo workingDirectory environmentVars args =
    (fun (info:ProcStartInfo) ->
        { info with
            FileName = System.IO.Path.GetFullPath commandToolPath
            Arguments = args
            WorkingDirectory = workingDirectory
            Environment =
                [   "MSBuild", msBuildExe
                    "GIT", Git.CommandHelper.gitPath
                //    "FSI", Fake.FSIHelper.fsiPath
                ] |> Map.ofList |> Some
        }
    )


/// Run the given buildscript with FAKE.exe
let executeWithOutput configStartInfo =
    let exitCode =
        ExecProcessWithLambdas
            configStartInfo
            TimeSpan.MaxValue false ignore ignore
    System.Threading.Thread.Sleep 1000
    exitCode

let executeWithRedirect errorF messageF configStartInfo =
    let exitCode =
        ExecProcessWithLambdas
            configStartInfo
            TimeSpan.MaxValue true errorF messageF
    System.Threading.Thread.Sleep 1000
    exitCode

let executeHelper executer traceMsg failMessage configStartInfo =
    trace traceMsg
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
    |> fun files -> String.Join("\" \"", files)
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

// Documentation
let buildDocumentationTarget fsiargs target =
    execute
        (sprintf "Building documentation (%s), this could take some time, please wait..." target)
        "generating reference documentation failed"
        (fakeStartInfo "generate.fsx" "docs/tools" "" fsiargs ["target", target])

let bootStrapDocumentationFiles () =
    // This is needed to bootstrap ourself (make sure we have the same environment while building as our users) ...
    // If you came here from the nuspec file add your file.
    // If you add files here to make the CI happy add those files to the .nuspec file as well
    // TODO: INSTEAD build the nuspec file before generating the documentation and extract it...
    Directory.ensure (__SOURCE_DIRECTORY__ </> "packages/FSharp.Formatting/lib/net40")
    let buildFiles = [
        "CSharpFormat.dll";
        "FSharp.CodeFormat.dll"; "FSharp.CodeFormat.dll.config";
        "FSharp.Literate.dll"; "FSharp.Literate.dll.config";
                       "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll"; "RazorEngine.dll";
                       "System.Web.Razor.dll"; "FSharp.Formatting.Common.dll"; "FSharp.Formatting.Razor.dll"
    ]
                     //|> List.append (!! ( "bin/*.dll.config" )).Includes

    let bundledFiles =
        buildFiles
        |> List.map (fun f ->
            __SOURCE_DIRECTORY__ </> sprintf "bin/%s" f,
            __SOURCE_DIRECTORY__ </> sprintf "packages/FSharp.Formatting/lib/net40/%s" f)
        
        |> List.map (fun (source, dest) -> Path.GetFullPath source, Path.GetFullPath dest)
    for source, dest in bundledFiles do
        try
            printfn "Copying %s to %s" source dest
            File.Copy(source, dest, true)
        with e -> printfn "Could not copy %s to %s, because %s" source dest e.Message

Target.Create"DogFoodCommandTool" (fun _ ->
    // generate metadata reference
    let dllFiles =
      [ "FSharp.CodeFormat.dll"; "FSharp.Formatting.Common.dll"
        "FSharp.Literate.dll"; "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll"; "FSharp.Formatting.Razor.dll" ]
        //|> List.collect (fun s -> [sprintf "bin/%s" s;sprintf "bin/%s.config" s])

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
    Shell.CleanDir "temp/api_docs"
    let metadataReferenceArgs =
        commandToolMetadataFormatArgument
            dllFiles "temp/api_docs" layoutRoots libDirs parameters None
    buildDocumentationCommandTool metadataReferenceArgs

    Shell.CleanDir "temp/literate_docs"
    let literateArgs =
        commandToolLiterateArgument
            "docs/content" "temp/literate_docs" layoutRoots parameters
    buildDocumentationCommandTool literateArgs)

Target.Create"GenerateDocs" (fun _ ->
    bootStrapDocumentationFiles ()
    buildDocumentationTarget "--define:RELEASE --define:REFERENCE --define:HELP" "Default")

Target.Create"WatchDocs" (fun _ ->
    bootStrapDocumentationFiles ()
    buildDocumentationTarget "--define:WATCH" "Default")

// --------------------------------------------------------------------------------------
// Release Scripts

let gitHome = "git@github.com:fsprojects"

Target.Create"ReleaseDocs" (fun _ ->
    Git.Repository.clone "" (gitHome + "/FSharp.Formatting.git") "temp/gh-pages"
    Git.Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    Shell.CopyRecursive "docs/output" "temp/gh-pages" true |> printfn "%A"
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Git.Branches.push "temp/gh-pages"
)

Target.Create"ReleaseBinaries" (fun _ ->
    Git.Repository.clone "" (gitHome + "/FSharp.Formatting.git") "temp/release"
    Git.Branches.checkoutBranch "temp/release" "release"
    Shell.CopyRecursive "bin" "temp/release" true |> printfn "%A"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/release" cmd |> printfn "%s"
    Git.Branches.push "temp/release"
)

Target.Create"CreateTag" (fun _ ->
    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" "origin" release.NugetVersion
)

Target.Create"Release" Target.DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.Create"All" Target.DoNothing

#r "System.IO.Compression.FileSystem"
Target.Create"DownloadPython" (fun _ ->
  if not isUnix then
    let w = new System.Net.WebClient()
    let zipFile = "temp"</>"cpython.zip"
    if File.Exists zipFile then File.Delete zipFile
    w.DownloadFile("https://www.python.org/ftp/python/3.5.1/python-3.5.1-embed-amd64.zip", zipFile)
    let cpython = "temp"</>"CPython"
    Shell.CleanDir cpython
    System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, cpython)
    let cpythonStdLib = cpython</>"stdlib"
    Shell.CleanDir cpythonStdLib
    System.IO.Compression.ZipFile.ExtractToDirectory(cpython</>"python35.zip", cpythonStdLib)
)

Target.Create"CreateTestJson" (fun _ ->
    let targetPath = "temp/CommonMark"
    Shell.CleanDir targetPath
    Git.Repository.clone targetPath "https://github.com/jgm/CommonMark.git" "."

    let pythonExe, stdLib =
      if not isUnix then
        System.IO.Path.GetFullPath ("temp"</>"CPython"</>"python.exe"),
        System.IO.Path.GetFullPath ("temp"</>"CPython"</>"stdlib")
      else "python", ""

    let resultFile = "temp"</>"commonmark-tests.json"
    if File.Exists resultFile then File.Delete resultFile
    ( use fileStream = new StreamWriter(File.Open(resultFile, System.IO.FileMode.Create))
      executeHelper
        (executeWithRedirect traceError fileStream.WriteLine)
        "Creating test json file, this could take some time, please wait..."
        "generating documentation failed"
        (fun info ->
            { info with 
                FileName = pythonExe
                Arguments = "test/spec_tests.py --dump-tests"
                WorkingDirectory = targetPath
            }.WithEnvironmentVariables [
               "MSBuild", msBuildExe
               "GIT", Git.CommandHelper.gitPath
            //   "FSI", Fake.FSIHelper.fsiPath
            ] |> fun info -> 
                if not isUnix then
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
  ==> "CopyFSharpCore"
  ==> "SetupLibForTests"
  ==> "Build"
  ==> "BuildTests"

"Build"
  ==> "All"


"BuildTests"
  ==> "DotnetTests"

"BuildTests"
  ==> "RunTests"
  ==> "All"

"GenerateDocs" ==> "All"

"Build"
  ==> "DogFoodCommandTool"
  ==> "All"

"UpdateFsxVersions" ==> "All"

"CopyFSharpCore" ==> "NuGet"

"All"
  ==> "NuGet"
  ==> "ReleaseDocs"
//  ==> "ReleaseBinaries"
  ==> "CreateTag"
  ==> "Release"

"DownloadPython" ==> "CreateTestJson"

//Target.RunOrDefault "All"
Target.RunOrDefault "Build"
