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
    | Ok result ->
        match result.Releases with
        | [] ->
            failwith
                "RELEASE_NOTES.md has no versioned releases. \
                 Note: blank lines between items inside a section (e.g. '### Added') \
                 cause Ionide.KeepAChangelog 0.1.8 to stop parsing — remove them."
        | h :: _ -> h

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

// Standalone doc-script type-check using the locally built fsdocs tool.
// Assumes the solution has been built (e.g. after running the CI pipeline or
// `dotnet build`). Catches type errors in .fsx documentation sources early,
// matching the `--strict` check in the full GenerateDocs stage.
let fsdocsLocalBin =
    let ext = if System.OperatingSystem.IsWindows() then ".exe" else ""
    $"src/fsdocs-tool/bin/Release/net10.0/fsdocs{ext}"

let checkDocScriptsStage =
    stage "CheckDocScripts" { run $"\"{fsdocsLocalBin}\" build --strict --clean --properties Configuration=Release" }

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

    checkDocScriptsStage

    stage "GenerateDocs" {
        // Skip on Windows CI runners: docs are only deployed from Linux
        whenNot { envVar "RUNNER_OS" "Windows" }

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
    checkDocScriptsStage
    runIfOnlySpecified true
}

tryPrintPipelineCommandHelp ()
