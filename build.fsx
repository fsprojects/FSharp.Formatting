#r "nuget: Fun.Build, 1.0.4"
#r "nuget: Fake.IO.FileSystem, 6.0.0"
#r "nuget: Ionide.KeepAChangelog, 0.1.8"

open System.IO
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.IO
open Ionide.KeepAChangelog
open Ionide.KeepAChangelog.Domain
open Fun.Build

let root = __SOURCE_DIRECTORY__

let configuration = "Release"

// Folder to deposit deploy artifacts
let artifactsDir = root @@ "artifacts"

// Local fsdocs-tool
let fsdocTool = artifactsDir @@ "fsdocs"

// Read release notes document
let releaseNugetVersion, _, _ =
    let changeLog = FileInfo(__SOURCE_DIRECTORY__ </> "RELEASE_NOTES.md")

    match Parser.parseChangeLog changeLog with
    | Error(msg, error) -> failwithf "%s msg\n%A" msg error
    | Ok result -> result.Releases |> List.head

let solutionFile = "FSharp.Formatting.sln"

let lintStage =
    stage "Lint" {
        run "dotnet tool restore"
        run $"dotnet fantomas {__SOURCE_FILE__} src tests docs --check"
    }

let testStage =
    stage "Tests" {
        run
            $"dotnet test {solutionFile} --configuration {configuration} --no-build --blame --logger trx --results-directory TestResults -tl"
    }

pipeline "CI" {
    lintStage

    stage "Clean" {
        run (fun _ ->
            !!artifactsDir ++ "temp" |> Shell.cleanDirs
            // in case the above pattern is empty as it only matches existing stuff
            [ "bin"; "temp"; "tests/bin" ] |> Seq.iter Directory.ensure)
    }

    stage "Build" {
        run $"dotnet restore {solutionFile} -tl"
        run $"dotnet build {solutionFile} --configuration {configuration} -tl"
    }

    stage "NuGet" { run $"dotnet pack {solutionFile} --output \"{artifactsDir}\" --configuration {configuration} -tl" }

    testStage

    stage "GenerateDocs" {
        run (fun _ ->
            Shell.cleanDir ".fsdocs"
            Shell.cleanDir ".packages")
        // Τhe tool has been uninstalled when the
        // artifacts folder was removed in the Clean stage.
        run
            $"dotnet tool install --no-cache --version %A{releaseNugetVersion} --add-source \"%s{artifactsDir}\" --tool-path \"%s{artifactsDir}\" fsdocs-tool"

        run $"\"{fsdocTool}\" build --strict --clean --properties Configuration=Release"
        run $"dotnet tool uninstall fsdocs-tool --tool-path \"%s{artifactsDir}\""
        run (fun _ -> Shell.cleanDir ".packages")
    }

    runIfOnlySpecified false
}

pipeline "Verify" {
    lintStage
    testStage
    stage "Analyzers" { run "dotnet msbuild /t:AnalyzeSolution" }
    runIfOnlySpecified true
}

let referenceProjectDir = "./tests/FSharp.ApiDocs.Tests/files/ReferenceProject"

// TODO: Revisit to see how we can use `dotnet watch` to run a local version of the tool
// against ReferenceProject
// dotnet watch change the CWD and fsdocs-tool does not seems to allow changing the CWD
pipeline "APIDocsReference" {

    stage "Clean up" {
        // Remove temporary files
        run (fun _ ->
            Shell.cleanDirs [
                "./tests/FSharp.ApiDocs.Tests/files/ReferenceProject/.fsdocs"
                "./tests/FSharp.ApiDocs.Tests/files/ReferenceProject/.tmp"
                "./tests/FSharp.ApiDocs.Tests/files/ReferenceProject/.bin"
                "./tests/FSharp.ApiDocs.Tests/files/ReferenceProject/.obj"
            ]
        )
    }

    stage "Build project"  {
        // Make sure we have the required information for generating the API docs
        workingDir referenceProjectDir
        run "dotnet build"
    }

    stage "Generate API Docs" {
        // Run a local version of the tool against the ReferenceProject
        workingDir referenceProjectDir
        run "dotnet run --project ../../../../src/fsdocs-tool -- watch --nolaunch --projects ReferenceProject.fsproj --sourcefolder ../../../../src/fsdocs-tool/../../tests/FSharp.ApiDocs.Tests/files/ReferenceProject"
    }

    runIfOnlySpecified
}

tryPrintPipelineCommandHelp ()
