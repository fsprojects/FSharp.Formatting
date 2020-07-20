// This is a FAKE 5.0 script, run using
//    dotnet fake build

#r "paket: groupref fake //"

#if !FAKE
#load ".fake/build.fsx/intellisense.fsx"
#r "netstandard"
#endif

open System
open System.IO
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.DotNet
open Fake.IO
open Fake.Tools

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Formatting"

let summary = "A package of libraries for building great F# documentation, samples and blogs"

let license = "Apache 2.0 License"

let configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault "configuration" DotNet.BuildConfiguration.Release

// Folder to deposit deploy artifacts
let artifactsDir = __SOURCE_DIRECTORY__ @@ "artifacts"

// Read release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

let gitHome = "https://github.com/fsprojects"
let projectRepo = gitHome + "/FSharp.Formatting"

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
    let releaseNotes = String.toLines release.Notes |> System.Net.WebUtility.HtmlEncode
    File.WriteAllText("version.props", sprintf """<Project>
      <PropertyGroup>
        <Version>%s</Version>
        <PackageReleaseNotes>
%s
        </PackageReleaseNotes>
      </PropertyGroup>
    </Project>""" release.NugetVersion releaseNotes)
)

// Clean build results
// --------------------------------------------------------------------------------------

Target.create "Clean" (fun _ ->
    !! artifactsDir
    ++ "temp"
    |> Shell.cleanDirs
    // in case the above pattern is empty as it only matches existing stuff
    ["bin"; "temp"; "tests/bin"]
    |> Seq.iter Directory.ensure
)

// Build library
// --------------------------------------------------------------------------------------

let solutionFile = "FSharp.Formatting.sln"

Target.create "Build" (fun _ ->
    solutionFile
    |> DotNet.build (fun opts -> { opts with Configuration = configuration } )
)

Target.create "Tests" (fun _ ->
    solutionFile
    |> DotNet.test (fun opts ->
        { opts with
            Blame = true
            NoBuild = true
            Framework = if Environment.isWindows then opts.Framework else Some "netcoreapp3.1"
            Configuration = configuration
            ResultsDirectory = Some "TestResults"
            Logger = Some "trx"
        })
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "NuGet" (fun _ ->
    DotNet.pack (fun pack ->
        { pack with
            OutputPath = Some artifactsDir 
            Configuration = configuration 
        }) solutionFile
)


// Generate the documentation
// --------------------------------------------------------------------------------------

let toolPath = "temp"

let docsToolPath = toolPath </> "fsdocs" + (if Environment.isWindows then ".exe" else "")
let docsToolStartInfo workingDirectory args =
    (fun (info:ProcStartInfo) ->
        { info with
            FileName = Path.GetFullPath docsToolPath
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

let execute traceMsg failMessage configStartInfo = executeHelper executeWithOutput traceMsg failMessage configStartInfo

// Documentation
let docTool args =
  execute
    "Building documentation (CommandTool), this could take some time, please wait..."
    "generating documentation failed"
    (docsToolStartInfo __SOURCE_DIRECTORY__ args)


let createArg argName arguments =
    (arguments : string seq)
    |> String.concat "\" \""
    |> fun e -> if String.IsNullOrWhiteSpace e then ""
                else sprintf "--%s \"%s\"" argName e

Target.create "GenerateDocs" (fun _ ->
    Shell.cleanDir "temp"
    Shell.cleanDir ".fsdocs"
    let result =
        DotNet.exec
            (fun p -> { p with WorkingDirectory = __SOURCE_DIRECTORY__ })
            "tool" ("install --add-source " + artifactsDir + " --tool-path " + toolPath + " --version " + release.NugetVersion + " FSharp.Formatting.CommandTool")

    if not result.OK then failwith "failed to install fsdocs as dotnet tool"

    docTool (sprintf "build docs --clean --mdcomments"))

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    Git.Repository.clone "" projectRepo "temp/gh-pages"
    Git.Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    Shell.copyRecursive "output" "temp/gh-pages" true |> printfn "%A"
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Git.Branches.push "temp/gh-pages"
)


Target.create "PushPackagesToNugetOrg" (fun _ ->
    let source = "https://api.nuget.org/v3/index.json"
    let apikey =  Environment.environVar "NUGET_KEY"
    for artifact in !! (artifactsDir + "/*nupkg") do
        let result = DotNet.exec id "nuget" (sprintf "push -s %s -k %s %s" source apikey artifact)
        if not result.OK then failwith "failed to push packages"  
)

Target.create "PushReleaseToGithub" (fun _ ->
    Git.Repository.clone "" projectRepo "temp/release"
    Git.Branches.checkoutBranch "temp/release" "release"
    Shell.copyRecursive "bin" "temp/release" true |> printfn "%A"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/release" cmd |> printfn "%s"
    Git.Branches.push "temp/release"
)

Target.create "CreateTag" (fun _ ->
    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" projectRepo release.NugetVersion
)

Target.create "Root" ignore
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

"Root"
  ==> "Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "Tests"
  ==> "All"

"Build"
  ==> "NuGet"
  ==> "GenerateDocs"
  ==> "All"

"GenerateDocs"
  ==> "ReleaseDocs"
  ==> "Release"

"All"
  ==> "CreateTag"
  ==> "PushPackagesToNugetOrg"
  ==> "PushReleaseToGithub"
  ==> "Release"

"DownloadPython" ==> "CreateTestJson"

Target.runOrDefault "All"
