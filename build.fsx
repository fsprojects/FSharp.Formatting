#!/usr/bin/env -S dotnet fsi --
#r "nuget: Fun.Build, 1.1.18"
#r "nuget: Fake.IO.FileSystem, 6.0.0"
#r "nuget: Ionide.KeepAChangelog, 0.1.8"

open System
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
let releaseNugetVersion, _, releaseNotesData =
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
        run $"dotnet fantomas check {__SOURCE_FILE__} src tests docs"
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

let buildStage =
    stage "Build" {
        run $"dotnet restore {solutionFile} -tl"
        run $"dotnet build {solutionFile} --configuration {configuration} -tl"
    }

let packStage =
    stage "NuGet" { run $"dotnet pack {solutionFile} --output \"{artifactsDir}\" --configuration {configuration} -tl" }


pipeline "CI" {
    lintStage

    stage "Clean" {
        run (fun _ ->
            !!artifactsDir ++ "temp" |> Shell.cleanDirs
            // in case the above pattern is empty as it only matches existing stuff
            [ "bin"; "temp"; "tests/bin" ] |> Seq.iter Directory.ensure)
    }

    buildStage

    packStage

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
    buildStage
    lintStage
    testStage
    stage "Analyzers" { run "dotnet msbuild /t:AnalyzeSolution" }
    checkDocScriptsStage
    runIfOnlySpecified true
}

// Start the documentation site in watch mode with the locally built fsdocs tool.
// Runs until interrupted (Ctrl+C); the site is served on http://localhost:8901.
// Every argument after the pipeline name is passed on to `fsdocs watch`, for example
// `./build.fsx -p Docs --nolaunch --port 8080`.
pipeline "Docs" {
    stage "WatchDocs" {
        run (fun _ ->
            let extraArgs =
                fsi.CommandLineArgs
                |> Array.skipWhile (fun arg -> arg <> "Docs")
                |> Array.skip 1
                |> String.concat " "

            $"dotnet run --project src/fsdocs-tool -- watch {extraArgs}")
    }

    runIfOnlySpecified true
}

// Build and pack the solution, publish the packages to NuGet and create the matching GitHub
// release. Runs from the push-to-main workflow after the CI pipeline. Every push to main runs
// it, so it does nothing when NuGet already has the version at the top of RELEASE_NOTES.md. Pass `--dry-run` to see the notes and the commands without publishing:
// `./build.fsx -p Release --dry-run`.
pipeline "Release" {
    buildStage
    packStage

    stage "Release" {
        run (fun ctx ->
            async {
                let isDryRun = fsi.CommandLineArgs |> Array.contains "--dry-run"
                let tag = $"v{releaseNugetVersion}"

                let nugetKey = Environment.GetEnvironmentVariable "NUGET_KEY"

                let publish (command: FormattableString) =
                    if isDryRun then
                        let masked =
                            command.GetArguments()
                            |> Array.map (fun a -> if a = box nugetKey then box "***" else a)

                        printfn $"[dry-run] {String.Format(command.Format, masked)}"
                        async { return Ok() }
                    else
                        ctx.RunSensitiveCommand command

                // The GitHub release goes with the NuGet push, so a version that is already on
                // NuGet gets neither.
                let! nugetVersions =
                    (new Net.Http.HttpClient())
                        .GetStringAsync("https://api.nuget.org/v3-flatcontainer/fsdocs-tool/index.json")
                    |> Async.AwaitTask

                if nugetVersions.Contains $"\"{releaseNugetVersion}\"" then
                    printfn $"fsdocs-tool {releaseNugetVersion} is already on NuGet, nothing to do."
                    return 0
                else

                    let packages = Directory.GetFiles(artifactsDir, $"*.{releaseNugetVersion}.nupkg")

                    let notes =
                        match releaseNotesData with
                        | None -> failwith "The release at the top of RELEASE_NOTES.md has no sections."
                        | Some data ->
                            [
                                "Added", data.Added
                                "Changed", data.Changed
                                "Fixed", data.Fixed
                                "Deprecated", data.Deprecated
                                "Removed", data.Removed
                                "Security", data.Security
                                yield! Map.toList data.Custom
                            ]
                            |> List.filter (fun (_, lines) -> not lines.IsEmpty)
                            |> List.map (fun (header, lines) ->
                                lines
                                |> List.map (fun line -> line.TrimStart())
                                |> String.concat "\n"
                                |> sprintf "### %s\n%s" header)
                            |> String.concat "\n\n"

                    printfn $"Release notes for {tag}:\n---\n{notes}\n---"

                    for package in packages do
                        let! result =
                            publish
                                $"dotnet nuget push \"{package}\" --api-key {nugetKey} --source https://api.nuget.org/v3/index.json --skip-duplicate"

                        if Result.isError result then
                            failwith $"Pushing {Path.GetFileName package} failed."

                    let notesFile = Path.GetTempFileName()
                    File.WriteAllText(notesFile, notes)
                    let files = packages |> Array.map (sprintf "\"%s\"") |> String.concat " "

                    let prerelease =
                        if String.IsNullOrEmpty releaseNugetVersion.Prerelease then
                            ""
                        else
                            "--prerelease"

                    let! result =
                        publish
                            $"gh release create {tag} {files} --title {releaseNugetVersion} --notes-file \"{notesFile}\" {prerelease}"

                    File.Delete notesFile

                    if Result.isError result then
                        return failwith "Creating the GitHub release failed."
                    else
                        return 0
            })
    }

    runIfOnlySpecified true
}

tryPrintPipelineCommandHelp ()
