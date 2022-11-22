open System
open System.Xml.Linq
open Fake.Core
open Fake.Core.TargetOperators
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.DotNet
open Fake.IO

let root = Path.combine __SOURCE_DIRECTORY__ ".."
Environment.CurrentDirectory <- root

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Formatting"

let summary = "A package of libraries for building great F# documentation, samples and blogs"

let license = "Apache 2.0 License"

let configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault "configuration" DotNet.BuildConfiguration.Release

// Folder to deposit deploy artifacts
let artifactsDir = root @@ "artifacts"

// Read release notes document
let release = ReleaseNotes.load "RELEASE_NOTES.md"

let projectRepo = "https://github.com/fsprojects/FSharp.Formatting"

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information
let assemblyInfo _ =
    let info =
        [ AssemblyInfo.Product project
          AssemblyInfo.Description summary
          AssemblyInfo.Version release.AssemblyVersion
          AssemblyInfo.FileVersion release.AssemblyVersion
          AssemblyInfo.InformationalVersion release.NugetVersion
          AssemblyInfo.Copyright license ]

    AssemblyInfoFile.createFSharp "src/Common/AssemblyInfo.fs" info
    AssemblyInfoFile.createCSharp "src/Common/AssemblyInfo.cs" info

    let versionProps =
        XElement(
            XName.Get "Project",
            XElement(
                XName.Get "PropertyGroup",
                XElement(XName.Get "Version", release.NugetVersion),
                XElement(XName.Get "PackageReleaseNotes", String.toLines release.Notes)
            )
        )

    versionProps.Save("version.props")

// Clean build results
// --------------------------------------------------------------------------------------
let clean _ =
    !!artifactsDir ++ "temp" |> Shell.cleanDirs
    // in case the above pattern is empty as it only matches existing stuff
    [ "bin"; "temp"; "tests/bin" ] |> Seq.iter Directory.ensure

// Build library
// --------------------------------------------------------------------------------------

let solutionFile = "FSharp.Formatting.sln"

let build _ =
    solutionFile
    |> DotNet.build (fun opts -> { opts with Configuration = configuration })

let tests _ =
    solutionFile
    |> DotNet.test (fun opts ->
        { opts with
            Blame = true
            NoBuild = true
            Framework = Some "net7.0"
            Configuration = configuration
            ResultsDirectory = Some "TestResults"
            Logger = Some "trx" })

// --------------------------------------------------------------------------------------
// Build a NuGet package
let nuget _ =
    DotNet.pack
        (fun pack ->
            { pack with
                OutputPath = Some artifactsDir
                Configuration = configuration })
        solutionFile

// Generate the documentation by dogfooding the tools pacakge
// --------------------------------------------------------------------------------------
let generateDocs _ =
    Shell.cleanDir ".fsdocs"
    Shell.cleanDir ".packages"
    // Τhe tool has been uninstalled when the
    // artifacts folder was removed in the Clean target.
    DotNet.exec
        id
        "tool"
        ("install --no-cache --version "
         + release.NugetVersion
         + " --add-source "
         + "\""
         + artifactsDir
         + "\""
         + " --tool-path "
         + "\""
         + artifactsDir
         + "\""
         + " fsdocs-tool")
    |> ignore

    CreateProcess.fromRawCommand
        (artifactsDir @@ "fsdocs")
        [ "build"; "--strict"; "--clean"; "--properties"; "Configuration=Release" ]
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore
    // DotNet.exec id "fsdocs" "build --strict --clean --properties Configuration=Release" |> ignore
    // DotNet.exec id "tool" "uninstall --local fsdocs-tool" |> ignore
    Shell.cleanDir ".packages"

let initTargets () =
    Target.create "AssemblyInfo" assemblyInfo
    Target.create "Clean" clean
    Target.create "Build" build
    Target.create "Tests" tests
    Target.create "NuGet" nuget
    Target.create "GenerateDocs" generateDocs
    Target.create "All" ignore

    // clean and recreate assembly inform on release
    "Clean"
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "NuGet"
    ==> "Tests"
    ==> "GenerateDocs"
    ==> "All"
    |> ignore

//-----------------------------------------------------------------------------
// Target Start
//-----------------------------------------------------------------------------
[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext

    initTargets ()

    Target.runOrDefaultWithArguments "All"

    0 // return an integer exit code
