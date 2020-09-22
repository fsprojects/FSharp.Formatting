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

let projectRepo = "https://github.com/fsprojects/FSharp.Formatting"

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

// Generate the documentation by dogfooding the tools pacakge
// --------------------------------------------------------------------------------------

Target.create "GenerateDocs" (fun _ ->
    Shell.cleanDir ".fsdocs"
    Shell.cleanDir ".packages"
    DotNet.exec id "tool" "uninstall --local FSharp.Formatting.CommandTool" |> ignore
    // Use a local package store to avoid reuse of previous builds of the package with the same version
    try 
      Environment.setEnvironVar "NUGET_PACKAGES" (__SOURCE_DIRECTORY__ + "/.packages")
      DotNet.exec id "tool" ("install --local --no-cache --version " + release.NugetVersion + " --add-source " + artifactsDir + " FSharp.Formatting.CommandTool")  |> ignore
    finally
      Environment.setEnvironVar "NUGET_PACKAGES" ""
    DotNet.exec id "fsdocs" "build --strict --clean --property Configuration=Release" |> ignore
    DotNet.exec id "tool" "uninstall --local FSharp.Formatting.CommandTool" |> ignore
    Shell.cleanDir ".packages"
)

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    Shell.cleanDir "temp"
    Git.CommandHelper.runSimpleGitCommand "." (sprintf "clone %s temp/gh-pages --depth 1 -b gh-pages" projectRepo) |> ignore
    Shell.copyRecursive "output" "temp/gh-pages" true |> printfn "%A"
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Git.Branches.push "temp/gh-pages"
)


Target.create "PublishNuget" (fun _ ->
    let source = "https://api.nuget.org/v3/index.json"
    let apikey =  Environment.environVar "NUGET_KEY"
    for artifact in !! (artifactsDir + "/*nupkg") do
        let result = DotNet.exec id "nuget" (sprintf "push -s %s -k %s %s" source apikey artifact)
        if not result.OK then failwith "failed to push packages"  
)

Target.create "CreateTag" (fun _ ->
    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" projectRepo release.NugetVersion
)

Target.create "All" ignore
Target.create "Release" ignore

// clean and recreate assembly inform on release
"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "NuGet"
  ==> "Tests"
  ==> "All"
  ==> "CreateTag"
  ==> "PublishNuget"
  ==> "Release"

"NuGet"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"
  ==> "Release"

"GenerateDocs"
  ==> "All"

Target.runOrDefault "All"
