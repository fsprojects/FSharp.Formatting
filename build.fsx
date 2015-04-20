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
        Attribute.FileVersion release.AssemblyVersion
        Attribute.Copyright license ]
)

// --------------------------------------------------------------------------------------
// Clean build results


Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp" ]
    CleanDirs ["docs/output"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/FsLib/bin"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/FsLib/obj"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/crefLib/bin"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/crefLib/obj"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/csharpSupport/bin"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/csharpSupport/obj"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/TestLib/bin"]
    CleanDirs ["tests/FSharp.MetadataFormat.Tests/files/TestLib/obj"]
)

// --------------------------------------------------------------------------------------
// Update the assembly version numbers in the script file.

open System.IO

Target "UpdateFsxVersions" (fun _ ->
    let packages = [ "FSharp.Compiler.Service"; "FSharpVSPowerTools.Core" ]
    let replacements = 
      packages |> Seq.map (fun packageName ->
        sprintf "/%s.(.*)/lib" packageName,
        sprintf "/%s.%s/lib" packageName (GetPackageVersion "packages" packageName))
    let path = "./packages/FSharp.Formatting/FSharp.Formatting.fsx"
    let text = File.ReadAllText(path)
    let text = (text, replacements) ||> Seq.fold (fun text (pattern, replacement) ->
        Text.RegularExpressions.Regex.Replace(text, pattern, replacement) )
    File.WriteAllText(path, text)
)

// --------------------------------------------------------------------------------------
// Build library 

Target "Build" (fun _ ->
    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["FSharp.Formatting.sln"]
      Excludes = [] } 
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

Target "MergeVSPowerTools" (fun _ ->
    () (*
    let binDir = __SOURCE_DIRECTORY__ @@ "bin"
    CreateDir (binDir @@ "merged")

    let toPack =
        (binDir @@ "FSharp.CodeFormat.dll") + " " +
        (binDir @@ "FSharpVSPowerTools.Core.dll")

    let result =
        ExecProcess (fun info ->
            info.FileName <- currentDirectory @@ "packages/ILRepack/tools/ILRepack.exe"
            info.Arguments <-
              sprintf
                "/internalize /verbose /lib:bin /ver:%s /out:%s %s"
                release.AssemblyVersion (binDir @@ "merged" @@ "FSharp.CodeFormat.dll") toPack
            ) (TimeSpan.FromMinutes 5.)

    if result <> 0 then failwithf "Error during ILRepack execution."

    !! (binDir @@ "merged" @@ "*.*")
    |> CopyFiles binDir
    DeleteDir (binDir @@ "merged")
    *)
)
// --------------------------------------------------------------------------------------
// Build tests and generate tasks to run the tests in sequence

Target "BuildTests" (fun _ ->
    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["FSharp.Formatting.sln"]
      Excludes = [] } 
    |> MSBuildRelease "" "Rebuild"
    |> ignore

    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["tests/*/files/FsLib/FsLib.sln"]
      Excludes = [] }
    |> MSBuildDebug "" "Rebuild"
    |> ignore

    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["tests/*/files/crefLib/crefLib.sln"]
      Excludes = [] }
    |> MSBuildDebug "" "Rebuild"
    |> ignore

    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["tests/*/files/csharpSupport/csharpSupport.sln"]
      Excludes = [] }
    |> MSBuildDebug "" "Rebuild"
    |> ignore

    { BaseDirectory = __SOURCE_DIRECTORY__
      Includes = ["tests/*/files/TestLib/TestLib.sln"]
      Excludes = [] }
    |> MSBuildDebug "" "Rebuild"
    |> ignore
)

let testProjects = 
  [ "FSharp.CodeFormat.Tests"; "FSharp.Literate.Tests"; 
    "FSharp.Markdown.Tests"; "FSharp.MetadataFormat.Tests" ]

Target "RunTests" <| ignore

// For each test project file, generate a new "RunTest_Xyz" which 
// runs the test (to process them sequentially which is needed in Travis)
for name in testProjects do
    let taskName = sprintf "RunTest_%s" name
    Target taskName <| fun () ->
        !! (sprintf "tests/*/bin/Release/%s.dll" name)
        |> NUnit (fun p ->
            { p with
                DisableShadowCopy = true
                TimeOut = TimeSpan.FromMinutes 20.
                Framework = "4.0"
                OutputFile = "TestResults.xml" })
    taskName ==> "RunTests" |> ignore
    "BuildTests" ==> taskName |> ignore

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
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
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies =
                [ "FSharpVSPowerTools.Core", GetPackageVersion "packages" "FSharpVSPowerTools.Core" |> RequireExactly
                  "FSharp.Compiler.Service", GetPackageVersion "packages" "FSharp.Compiler.Service" ] })
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
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Formatting.CommandTool.nuspec"
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"; "--define:HELP"] [] then
      failwith "generating reference documentation failed")

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

"Clean" ==> "AssemblyInfo" ==> "Build" ==> "BuildTests"
"Build" ==> "MergeVSPowerTools" ==> "All"
"RunTests" ==> "All"
"GenerateDocs" ==> "All"
"UpdateFsxVersions" ==> "All"

"All"
  ==> "NuGet" 
  ==> "ReleaseDocs"
  ==> "ReleaseBinaries"
  ==> "Release"

RunTargetOrDefault "All"
