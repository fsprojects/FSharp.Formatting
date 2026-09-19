#!/usr/bin/env -S dotnet fsi --
#r "nuget: Fun.Build, 1.2.0"
#r "nuget: Fake.IO.FileSystem, 6.1.4"
#r "nuget: Ionide.KeepAChangelog, 0.2.0"

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
        | [] -> failwith "RELEASE_NOTES.md has no versioned releases."
        | h :: _ -> h

let solutionFile = "FSharp.Formatting.sln"

let lintStage =
    stage "Lint" {
        run "dotnet tool restore"
        run $"dotnet fantomas check %s{__SOURCE_FILE__} src tests docs"
    }

let testStage =
    stage "Tests" {
        run
            $"dotnet test %s{solutionFile} --configuration %s{configuration} --no-build --blame --logger trx --results-directory TestResults -tl"
    }

// Standalone doc-script type-check using the locally built fsdocs tool.
// Assumes the solution has been built (e.g. after running the CI pipeline or
// `dotnet build`). Catches type errors in .fsx documentation sources early,
// matching the `--strict` check in the full GenerateDocs stage.
let fsdocsLocalBin =
    let ext = if System.OperatingSystem.IsWindows() then ".exe" else ""
    $"src/fsdocs-tool/bin/Release/net10.0/fsdocs%s{ext}"

let checkDocScriptsStage =
    stage "CheckDocScripts" { run $"\"%s{fsdocsLocalBin}\" build --strict --clean --properties Configuration=Release" }

let buildStage =
    stage "Build" {
        run $"dotnet restore %s{solutionFile} -tl"
        run $"dotnet build %s{solutionFile} --configuration %s{configuration} -tl"
    }

// --------------------------------------------------------------------------------------
// Analyzers
//
// `dotnet msbuild /t:AnalyzeSolution` is the full run, the one CI does, and it takes minutes.
//
// The `AnalyzeChanged` pipeline below runs the same analyzers over the files the working tree
// touched, which takes seconds. It cannot see a finding your change causes in a file you did not
// edit, so the full run is still the one to do before opening a pull request.
// --------------------------------------------------------------------------------------

/// Where the analyzer packages were restored to. `dotnet restore` writes a `Pkg*` property per
/// package that asks for one, so no version is repeated here.
let private analyzerPaths (ctx: Internal.StageContext) =
    async {
        let properties = [ "PkgG-Research_FSharp_Analyzers"; "PkgIonide_Analyzers" ]

        let flags =
            properties
            |> List.map (fun property -> $"--getProperty:%s{property}")
            |> String.concat " "

        let! result =
            ctx.RunCommandCaptureAll(
                $"dotnet msbuild src/FSharp.Formatting/FSharp.Formatting.fsproj %s{flags}",
                workingDir = root,
                disablePrintCommand = true,
                disablePrintOutput = true
            )

        if result.ExitCode <> 0 then
            failwith $"Could not read the analyzer package paths.\n%s{result.StandardError}"

        use json = Text.Json.JsonDocument.Parse result.StandardOutput
        let values = json.RootElement.GetProperty "Properties"

        return
            properties
            |> List.map (fun property ->
                let path = values.GetProperty(property).GetString()

                if String.IsNullOrEmpty path then
                    failwith $"%s{property} is empty, restore the solution first."

                path @@ "analyzers/dotnet/fs")
    }

/// The same projects Directory.Solution.targets analyzes.
let private analyzerProjects =
    !!(root @@ "src/**/*.fsproj")
    |> Seq.map (fun project -> project.Replace('\\', '/'))
    |> Seq.sort
    |> List.ofSeq

type private AnalysisTarget =
    /// An empty file list asks for every file of the project.
    | Project of project: string * files: string list
    | Script of file: string

/// Every project that owns a changed file, scoped to the changed files of its own, plus the build
/// script when that changed. A changed project file asks for the whole project.
let private targetsFor (files: string list) =
    let endsWith extensions (path: string) =
        extensions
        |> List.exists (fun (extension: string) -> path.EndsWith(extension, StringComparison.Ordinal))

    // `--include-files` only matches an absolute path, and silently reports a clean run otherwise.
    let absolute (file: string) = (root @@ file).Replace('\\', '/')

    let keep extensions =
        List.choose
            (fun file ->
                if endsWith extensions file then
                    Some(absolute file)
                else
                    None)
            files

    let sources = keep [ ".fs"; ".fsi" ]
    let projectFiles = keep [ ".fsproj" ]

    [
        for project in analyzerProjects do
            let folder = project.Substring(0, project.LastIndexOf '/' + 1)

            let owns (file: string) =
                file.StartsWith(folder, StringComparison.Ordinal)

            if List.exists owns projectFiles then
                yield Project(project, [])
            else
                match List.filter owns sources with
                | [] -> ()
                | owned -> yield Project(project, owned)

        if List.contains "build.fsx" files then
            yield Script(absolute "build.fsx")
    ]

/// The changed files git reports, relative to the repository root, without the deleted ones.
let private changedFiles (ctx: Internal.StageContext) =
    async {
        let! result =
            ctx.RunCommandCaptureAll(
                "git status --porcelain --untracked-files=all",
                workingDir = root,
                disablePrintCommand = true,
                disablePrintOutput = true
            )

        if result.ExitCode <> 0 then
            failwith $"Could not read the git status.\n%s{result.StandardError}"

        return
            result.StandardOutput.Split('\n')
            |> Array.choose (fun (line: string) ->
                let line = line.TrimEnd('\r')

                if line.Length < 4 || line[0] = 'D' || line[1] = 'D' then
                    None
                else
                    let path = line.Substring 3

                    let path =
                        match path.IndexOf(" -> ", StringComparison.Ordinal) with
                        | -1 -> path
                        | arrow -> path.Substring(arrow + 4)

                    Some(path.Trim('"').Replace('\\', '/')))
            |> List.ofArray
    }

/// One process per target, several at a time, each printing its output in one piece as it
/// finishes. No SARIF is written: a report of the files you touched is not one of the solution.
let private runAnalyzers (ctx: Internal.StageContext) (targets: AnalysisTarget list) =
    async {
        let! analyzers = analyzerPaths ctx

        let nameOf target =
            match target with
            | Script file -> Path.GetFileName file
            | Project(project, _) -> Path.GetFileNameWithoutExtension project

        printfn $"""Analyzing: %s{targets |> List.map nameOf |> String.concat ", "}"""

        let analyze target =
            async {
                let started = DateTime.UtcNow

                let arguments =
                    [
                        for analyzer in analyzers do
                            "--analyzers-path"
                            analyzer

                        "--exclude-analyzers"
                        "PartialAppAnalyzer"
                        "ReturnStructPartialActivePatternAnalyzer"

                        // These report at Info by default, which never fails a run.
                        "--treat-as-error"
                        "IONIDE-002"
                        "IONIDE-010"
                        "IONIDE-012"

                        "-c"
                        configuration
                        "--code-root"
                        root

                        match target with
                        | Script file ->
                            // The scripts NuGet generates for a `#r "nuget: ..."` are not ours.
                            "--exclude-files"
                            "**/.packagemanagement/**"
                            "--script"
                            file
                        | Project(project, files) ->
                            match files with
                            | [] -> ()
                            | files ->
                                "--include-files"
                                yield! files

                            "--project"
                            project
                    ]

                let command =
                    arguments |> List.map (fun argument -> $"\"%s{argument}\"") |> String.concat " "

                let! result =
                    ctx.RunCommandCaptureAll(
                        $"dotnet fsharp-analyzers %s{command}",
                        workingDir = root,
                        disablePrintCommand = true,
                        disablePrintOutput = true
                    )

                let elapsed = DateTime.UtcNow - started

                let scope =
                    match target with
                    | Script _
                    | Project(_, []) -> ""
                    | Project(_, files) -> $" (%i{files.Length} files)"

                let outcome =
                    if result.ExitCode = 0 then
                        "clean"
                    else
                        $"exit code %i{result.ExitCode}"

                printfn $"\n=== %s{nameOf target}%s{scope}: %s{outcome} in %.1f{elapsed.TotalSeconds}s"
                printf "%s" result.StandardOutput
                eprintf "%s" result.StandardError

                return result.ExitCode
            }

        // Every process type checks a whole project, so a handful at a time is what keeps the
        // machine busy without the runs starving each other of memory.
        let! exitCodes =
            Async.Parallel(List.map analyze targets, max 2 (Environment.ProcessorCount / 2))

        return Array.fold max 0 exitCodes
    }

let packStage =
    stage "NuGet" {
        run $"dotnet pack %s{solutionFile} --output \"%s{artifactsDir}\" --configuration %s{configuration} -tl"
    }


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

        run $"\"%s{fsdocTool}\" build --strict --clean --properties Configuration=Release"
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

// The analyzers, over the files the working tree touched, which is seconds rather than minutes.
// `./build.fsx -p AnalyzeChanged`.
pipeline "AnalyzeChanged" {
    workingDir root
    stage "RestoreTools" { run "dotnet tool restore" }
    stage "Restore" { run $"dotnet restore %s{solutionFile} -tl" }

    stage "Analyze" {
        run (fun ctx ->
            async {
                let! files = changedFiles ctx

                match targetsFor files with
                | [] ->
                    Console.WriteLine "No changed file is analyzed."
                    return 0
                | targets -> return! runAnalyzers ctx targets
            })
    }

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

            $"dotnet watch --project src/fsdocs-tool -- watch %s{extraArgs}")
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
                let tag = $"v%O{releaseNugetVersion}"

                let nugetKey = Environment.GetEnvironmentVariable "NUGET_KEY"

                let publish (command: FormattableString) =
                    if isDryRun then
                        let masked =
                            command.GetArguments()
                            |> Array.map (fun a -> if a = box nugetKey then box "***" else a)

                        printfn $"[dry-run] %s{String.Format(command.Format, masked)}"
                        async { return Ok() }
                    else
                        ctx.RunSensitiveCommand command

                // The GitHub release goes with the NuGet push, so a version that is already on
                // NuGet gets neither.
                let! nugetVersions =
                    (new Net.Http.HttpClient())
                        .GetStringAsync("https://api.nuget.org/v3-flatcontainer/fsdocs-tool/index.json")
                    |> Async.AwaitTask

                if nugetVersions.Contains $"\"%O{releaseNugetVersion}\"" then
                    printfn $"fsdocs-tool %O{releaseNugetVersion} is already on NuGet, nothing to do."
                    return 0
                else

                    let packages = Directory.GetFiles(artifactsDir, $"*.%O{releaseNugetVersion}.nupkg")

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
                            |> List.choose (fun (header, text: string) ->
                                if String.IsNullOrWhiteSpace text then
                                    None
                                else
                                    Some(sprintf "### %s\n%s" header (text.Trim())))
                            |> String.concat "\n\n"

                    printfn $"Release notes for %s{tag}:\n---\n%s{notes}\n---"

                    for package in packages do
                        let! result =
                            publish
                                $"dotnet nuget push \"{package}\" --api-key {nugetKey} --source https://api.nuget.org/v3/index.json --skip-duplicate"

                        if Result.isError result then
                            failwith $"Pushing %s{Path.GetFileName package} failed."

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
