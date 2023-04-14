#r "nuget: Fun.Build, 0.3.8"
#r "nuget: Fake.Core.ReleaseNotes, 6.0.0"
#r "nuget: Fake.DotNet.AssemblyInfoFile, 6.0.0"

open System.Xml.Linq
open Fake.Core
open Fake.DotNet
open Fake.IO.Globbing.Operators
open Fake.IO.FileSystemOperators
open Fake.IO
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
let release = ReleaseNotes.load "RELEASE_NOTES.md"
let solutionFile = "FSharp.Formatting.sln"

pipeline "CI" {
    stage "Lint" {
        run "dotnet tool restore"
        run $"dotnet fantomas {__SOURCE_FILE__} src tests docs --check"
    }

    stage "AssemblyInfo" {
        // Generate assembly info files with the right version & up-to-date information
        run (fun _ ->
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

            versionProps.Save("version.props"))
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

    stage "NuGet" { run $"dotnet pack {solutionFile} --output {artifactsDir} --configuration {configuration}" }

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
            $"dotnet tool install --no-cache --version %s{release.NugetVersion} --add-source %s{artifactsDir} --tool-path %s{artifactsDir} fsdocs-tool"

        run $"{fsdocTool} build --strict --clean --properties Configuration=Release"
        run $"dotnet tool uninstall fsdocs-tool --tool-path %s{artifactsDir}"
        run (fun _ -> Shell.cleanDir ".packages")
    }

    runIfOnlySpecified false
}

tryPrintPipelineCommandHelp ()
