// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#I @"packages/FAKE/tools"
#r @"packages/FAKE/tools/FakeLib.dll"

open System
open System.IO
open Fake 
open Fake.AssemblyInfoFile
open Fake.Git
open Fake.ReleaseNotesHelper

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Formatting"
let authors = ["Tomas Petricek"; "Oleg Pestov"; "Anh-Dung Phan"; "Xiang Zhang"]
let summary = "A package for building great F# documentation, samples and blogs"
let description = """
  The package is a collection of libraries that can be used for literate programming
  with F# (great for building documentation) and for generating library documentation 
  from inline code comments. The key componments (also available separately) are 
  Markdown parser, tools for formatting F# code snippets, including tool tip
  type information and a tool for generating documentation from library metadata."""

let tags = "F# fsharp formatting markdown code fssnip literate programming"

// Read release notes document
let release = ReleaseNotesHelper.parseReleaseNotes (File.ReadLines "RELEASE_NOTES.md")

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target "AssemblyInfo" (fun _ ->
  let fileName = "src/Common/AssemblyInfo.fs"
  CreateFSharpAssemblyInfo fileName
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ] 
)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target "RestorePackages" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" }))
)

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp" ]
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library 

Target "Build" (fun _ ->
    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["FSharp.Formatting.sln"]
      Excludes = [] } 
    |> MSBuildRelease "" "Rebuild"
    |> ignore

    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["FSharp.Formatting.Tests.sln"]
      Excludes = [] } 
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete

Target "RunTests" (fun _ ->
    let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
    let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion

    ActivateFinalTarget "CloseTestRunner"

    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["tests/*/bin/Release/FSharp.*Tests*.dll"]
      Excludes = [] } 
    |> Scan
    |> NUnit (fun p ->
        { p with
            ToolPath = nunitPath
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })
)

FinalTarget "CloseTestRunner" (fun _ ->  
    ProcessHelper.killProcess "nunit-agent.exe"
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description.Replace("\r", "").Replace("\n", "").Replace("  ", " ")
    let nugetPath = ".nuget/nuget.exe"
    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = release.NugetVersion
            ReleaseNotes = String.concat " " release.Notes
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Formatting.nuspec"
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateDocs" (fun _ ->
    executeFSI "docs/tools" "generate.fsx" [] |> ignore
)

// --------------------------------------------------------------------------------------
// Release Scripts

let gitHome = "https://github.com/tpetricek"

Target "ReleaseDocs" (fun _ ->
    Repository.clone "" (gitHome + "/FSharp.Formatting.git") "temp/gh-pages"
    Branches.checkoutBranch "temp/gh-pages" "gh-pages"
    CopyRecursive "docs/output" "temp/gh-pages" true |> printfn "%A"
    CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
    CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
    Branches.push "temp/gh-pages"
)

Target "ReleaseBinaries" (fun _ ->
    Repository.clone "" (gitHome + "/FSharp.Formatting.git") "temp/release"
    Branches.checkoutBranch "temp/release" "release"
    CopyRecursive "bin" "temp/release" true |> printfn "%A"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" release.NugetVersion
    CommandHelper.runSimpleGitCommand "temp/release" cmd |> printfn "%s"
    Branches.push "temp/release"
)

Target "Release" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunTests"
  ==> "GenerateDocs"
  ==> "All"

"All" 
  ==> "ReleaseDocs"
  ==> "ReleaseBinaries"
  ==> "NuGet"
  ==> "Release"

RunTargetOrDefault "All"
