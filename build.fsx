#r "nuget: Fun.Build, 0.3.8"
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

// Information about the project to be used at NuGet and in AssemblyInfo files
let project = "FSharp.Formatting"

let summary =
    "A package of libraries for building great F# documentation, samples and blogs"

let license = "Apache 2.0 License"

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

pipeline "CI" {
    stage "Lint" {
        run "dotnet tool restore"
        run $"dotnet fantomas {__SOURCE_FILE__} src tests docs --check"
    }

    stage "Clean" {
        run (fun _ ->
            !!artifactsDir ++ "temp" |> Shell.cleanDirs
            // in case the above pattern is empty as it only matches existing stuff
            [ "bin"; "temp"; "tests/bin" ] |> Seq.iter Directory.ensure)
    }

    stage "Build" {
        run $"dotnet restore {solutionFile}"
        run $"dotnet build {solutionFile} --configuration {configuration}"
    }

    stage "NuGet" { run $"dotnet pack {solutionFile} --output \"{artifactsDir}\" --configuration {configuration}" }

    stage "Tests" {
        run
            $"dotnet test {solutionFile} --configuration {configuration} --no-build --blame --logger trx --framework net7.0 --results-directory TestResults"
    }

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

tryPrintPipelineCommandHelp ()
