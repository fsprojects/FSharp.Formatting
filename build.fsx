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
open Fake.Testing
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
  let info = 
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion
        Attribute.InformationalVersion release.NugetVersion
        Attribute.Copyright license ]
  CreateFSharpAssemblyInfo "src/Common/AssemblyInfo.fs" info
  CreateCSharpAssemblyInfo "src/Common/AssemblyInfo.cs" info
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
        |> NUnit3 (fun p ->
            { p with 
                TimeOut = TimeSpan.FromMinutes 20. 
            })
    taskName ==> "RunTests" |> ignore
    "BuildTests" ==> taskName |> ignore

// --------------------------------------------------------------------------------------
// Build a NuGet package

// TODO: Contribute this to FAKE
type BreakingPoint =
  | SemVer
  | Minor
  | Patch

// See https://docs.nuget.org/create/versioning
let RequireRange breakingPoint version =
  let v = SemVerHelper.parse version
  match breakingPoint with
  | SemVer ->
    sprintf "[%s,%d.0)" version (v.Major + 1)
  | Minor -> // Like Semver but we assume that the increase of a minor version is already breaking
    sprintf "[%s,%d.%d)" version v.Major (v.Minor + 1)
  | Patch -> // Every update breaks
    version |> RequireExactly

Target "CopyFSharpCore" (fun _ ->
    // We need to include optdata and sigdata as well, we copy everything to be consistent
    for file in System.IO.Directory.EnumerateFiles("packages" @@ "FSharp.Core" @@ "lib" @@ "net40") do
        let source, dest = file, Path.Combine("bin", Path.GetFileName(file))
        printfn "Copying %s to %s" source dest
        File.Copy(source, dest, true))

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
                [ // From experience they always break something at the moment :(
                  "FSharpVSPowerTools.Core", GetPackageVersion "packages" "FSharpVSPowerTools.Core" |> RequireRange BreakingPoint.Minor
                  "FSharp.Compiler.Service", "[2.0.0.6]" // GetPackageVersion "packages" "FSharp.Compiler.Service" |> RequireRange BreakingPoint.Minor
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
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        "nuget/FSharp.Formatting.CommandTool.nuspec"
)

// --------------------------------------------------------------------------------------
// Generate the documentation

let fakePath = "packages" @@ "FAKE" @@ "tools" @@ "FAKE.exe"
let fakeStartInfo script workingDirectory args fsiargs environmentVars =
    (fun (info: System.Diagnostics.ProcessStartInfo) ->
        info.FileName <- System.IO.Path.GetFullPath fakePath
        info.Arguments <- sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
        info.WorkingDirectory <- workingDirectory
        let setVar k v =
            info.EnvironmentVariables.[k] <- v
        for (k, v) in environmentVars do
            setVar k v
        setVar "MSBuild" msBuildExe
        setVar "GIT" Git.CommandHelper.gitPath
        setVar "FSI" fsiPath)

let commandToolPath = "bin" @@ "fsformatting.exe"
let commandToolStartInfo workingDirectory environmentVars args =
    (fun (info: System.Diagnostics.ProcessStartInfo) ->
        info.FileName <- System.IO.Path.GetFullPath commandToolPath
        info.Arguments <- args
        info.WorkingDirectory <- workingDirectory
        let setVar k v =
            info.EnvironmentVariables.[k] <- v
        for (k, v) in environmentVars do
            setVar k v
        setVar "MSBuild" msBuildExe
        setVar "GIT" Git.CommandHelper.gitPath
        setVar "FSI" fsiPath)

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
    (commandToolStartInfo "." [] args)

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
    ensureDirectory (__SOURCE_DIRECTORY__ @@ "packages/FSharp.Formatting/lib/net40")
    let buildFiles = [ "CSharpFormat.dll"; "FSharp.CodeFormat.dll"; "FSharp.Literate.dll"
                       "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll"; "RazorEngine.dll";
                       "System.Web.Razor.dll"; "FSharp.Formatting.Common.dll" ]
    let bundledFiles =
        buildFiles
        |> List.map (fun f -> 
            __SOURCE_DIRECTORY__ @@ sprintf "bin/%s" f, 
            __SOURCE_DIRECTORY__ @@ sprintf "packages/FSharp.Formatting/lib/net40/%s" f)
        |> List.map (fun (source, dest) -> Path.GetFullPath source, Path.GetFullPath dest)
    for source, dest in bundledFiles do
        try
            printfn "Copying %s to %s" source dest
            File.Copy(source, dest, true)
        with e -> printfn "Could not copy %s to %s, because %s" source dest e.Message

Target "DogFoodCommandTool" (fun _ ->
    // generate metadata reference
    let dllFiles =
      [ "FSharp.CodeFormat.dll"; "FSharp.Formatting.Common.dll"
        "FSharp.Literate.dll"; "FSharp.Markdown.dll"; "FSharp.MetadataFormat.dll" ]
        |> List.map (sprintf "bin/%s")
    let layoutRoots =
      [ "docs/tools"; "misc/templates"; "misc/templates/reference" ]
    let libDirs = [ "bin/" ]
    let parameters =
      [ "page-author", "Matthias Dittrich"
        "project-author", "Matthias Dittrich"
        "page-description", "desc"
        "github-link", "https://github.com/tpetricek/FSharp.Formatting"
        "project-name", "FSharp.Formatting"
        "root", "https://tpetricek.github.io/FSharp.Formatting"
        "project-nuget", "https://www.nuget.org/packages/FSharp.Formatting/"
        "project-github", "https://github.com/tpetricek/FSharp.Formatting" ]
    CleanDir "temp/api_docs"
    let metadataReferenceArgs =
        commandToolMetadataFormatArgument
            dllFiles "temp/api_docs" layoutRoots libDirs parameters None
    buildDocumentationCommandTool metadataReferenceArgs

    CleanDir "temp/literate_docs"
    let literateArgs =
        commandToolLiterateArgument
            "docs/content" "temp/literate_docs" layoutRoots parameters
    buildDocumentationCommandTool literateArgs)

Target "GenerateDocs" (fun _ ->
    bootStrapDocumentationFiles ()
    buildDocumentationTarget "--define:RELEASE --define:REFERENCE --define:HELP" "Default")
      
Target "WatchDocs" (fun _ ->
    bootStrapDocumentationFiles ()
    buildDocumentationTarget "--define:WATCH" "Default")

// --------------------------------------------------------------------------------------
// Release Scripts

let gitHome = "git@github.com:tpetricek"

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

Target "CreateTag" (fun _ ->
    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
)

Target "Release" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

#r "System.IO.Compression"
#r "System.IO.Compression.FileSystem"
Target "DownloadPython" (fun _ ->
  if not isUnix then
    let w = new System.Net.WebClient()
    let zipFile = "temp"@@"cpython.zip"
    if File.Exists zipFile then File.Delete zipFile
    w.DownloadFile("https://www.python.org/ftp/python/3.5.1/python-3.5.1-embed-amd64.zip", zipFile)
    let cpython = "temp"@@"CPython"
    CleanDir cpython
    System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, cpython)
    let cpythonStdLib = cpython@@"stdlib"
    CleanDir cpythonStdLib
    System.IO.Compression.ZipFile.ExtractToDirectory(cpython@@"python35.zip", cpythonStdLib)
)

Target "CreateTestJson" (fun _ ->
    let targetPath = "temp/CommonMark"
    CleanDir targetPath
    Git.Repository.clone targetPath "https://github.com/jgm/CommonMark.git" "."

    let pythonExe, stdLib =
      if not isUnix then
        System.IO.Path.GetFullPath ("temp"@@"CPython"@@"python.exe"),
        System.IO.Path.GetFullPath ("temp"@@"CPython"@@"stdlib")
      else "python", ""

    let resultFile = "temp"@@"commonmark-tests.json"
    if File.Exists resultFile then File.Delete resultFile
    ( use fileStream = new StreamWriter(File.Open(resultFile, System.IO.FileMode.Create))
      executeHelper
        (executeWithRedirect traceError fileStream.WriteLine)
        "Creating test json file, this could take some time, please wait..."
        "generating documentation failed"
        (fun info ->
            info.FileName <- pythonExe
            info.Arguments <- "test/spec_tests.py --dump-tests"
            info.WorkingDirectory <- targetPath
            let setVar k v =
                info.EnvironmentVariables.[k] <- v
            if not isUnix then
              setVar "PYTHONPATH" stdLib
            setVar "MSBuild" msBuildExe
            setVar "GIT" Git.CommandHelper.gitPath
            setVar "FSI" fsiPath))
    File.Copy(resultFile, "tests"@@"commonmark_spec.json")
)

"Clean" ==> "AssemblyInfo" ==> "Build" ==> "BuildTests"
"Build" ==> "MergeVSPowerTools" ==> "All"
"BuildTests" ==> "RunTests" ==> "All"
"GenerateDocs" ==> "All"
"Build" ==> "CopyFSharpCore" ==> "DogFoodCommandTool" ==> "All"
"UpdateFsxVersions" ==> "All"

"CopyFSharpCore" ==> "NuGet"
"All"
  ==> "NuGet"
  ==> "ReleaseDocs"
//  ==> "ReleaseBinaries"
  ==> "CreateTag"
  ==> "Release"

"DownloadPython" ==> "CreateTestJson"

RunTargetOrDefault "All"
