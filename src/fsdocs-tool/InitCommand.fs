namespace fsdocs

open System.IO
open CommandLine

open Spectre.Console

[<Verb("init", HelpText = "initialize the necessary folder structure and files for creating documentation with fsdocs.")>]
type InitCommand() =

    let dir = Path.GetDirectoryName(typeof<InitCommand>.Assembly.Location)

    // get template locations for in-package and in-repo files and decide which to use later
    let inNugetPackageLocations = Common.InNugetPackageLocations(Path.Combine(dir, "..", "..", ".."))
    let inThisRepoLocations = Common.InDocsFolderLocations(Path.Combine(dir, "..", "..", "..", "..", "..", "docs"))

    [<Option("output",
             Required = false,
             Default = "docs",
             HelpText = "The output path for the documentation folder structure")>]
    member val output: string = "docs" with get, set

    [<Option("force",
             Required = false,
             Default = false,
             HelpText = "Whether to force-overwrite existing files in the output folder.")>]
    member val force: bool = false with get, set

    [<Option("non-interactive",
             Required = false,
             Default = true,
             HelpText = "Run the tool in non-interactive mode, creating default output.")>]
    member val ``non-interactive``: bool = false with get, set

    member this.Execute() =

        let docsOutputPath = Path.GetFullPath(this.output)
        let initLocations = Common.InDocsFolderLocations(docsOutputPath)

        let ensureOutputDirs () =
            [ docsOutputPath; initLocations.DocsFolder.Path; initLocations.img.Path ]
            |> List.iter ensureDirectory

        if inNugetPackageLocations.AllLocationsExist() then
            // if the in-package locations exist, this means fsdocs is run from the nuget package.
            try
                ensureOutputDirs ()

                let fileMap =
                    [ inNugetPackageLocations.``templates/template.html``, initLocations.``template.html``.Path
                      inNugetPackageLocations.``templates/template.ipynb``, initLocations.``template.ipynb``.Path
                      inNugetPackageLocations.``templates/template.tex``, initLocations.``template.tex``.Path
                      inNugetPackageLocations.Dockerfile, initLocations.Dockerfile.Path
                      inNugetPackageLocations.``Nuget.config``, initLocations.``Nuget.config``.Path
                      inNugetPackageLocations.``extras/content/img/badge-binder.svg``,
                      initLocations.``img/badge-binder.svg``.Path
                      inNugetPackageLocations.``extras/content/img/badge-notebook.svg``,
                      initLocations.``img/badge-notebook.svg``.Path
                      inNugetPackageLocations.``extras/content/img/badge-script.svg``,
                      initLocations.``img/badge-script.svg``.Path
                      // these files must be renamed, because files prefixed with a dot are otherwise ignored by fsdocs. We want this in the source repo, but not in the output of this command.
                      inNugetPackageLocations.``templates/init/.logo.png``,
                      Path.GetFullPath(Path.Combine(initLocations.img.Path, "logo.png"))
                      inNugetPackageLocations.``templates/init/.index_md_template.md``,
                      Path.GetFullPath(Path.Combine(initLocations.DocsFolder.Path, "index.md"))
                      inNugetPackageLocations.``templates/init/.literate_sample_template.fsx``,
                      Path.GetFullPath(Path.Combine(initLocations.DocsFolder.Path, "literate_sample.fsx")) ]

                fileMap
                |> List.map (fun (src, dst) ->
                    src,
                    dst,
                    if this.``non-interactive`` then
                        true
                    else
                        Common.CLI.confirmFileCreation dst src.Description)
                |> List.iter (fun (src, dst, copy) ->
                    if copy then
                        File.Copy(src.Path, dst, this.force))

                printfn ""
                printfn "a basic fsdocs scaffold has been created in %s." this.output
                printfn ""
                printfn "check it out by running 'dotnet fsdocs watch' !"

                0
            with _ as exn ->
                printfn "Error: %s" exn.Message
                1

        elif inThisRepoLocations.AllLocationsExist() then
            // if the in-repo locations exist, this means fsdocs is run from inside the FSharp.Formatting repo itself.

            try
                ensureOutputDirs ()

                let fileMap =
                    [ (inThisRepoLocations.``template.html``, initLocations.``template.html``.Path)
                      (inThisRepoLocations.``template.ipynb``, initLocations.``template.ipynb``.Path)
                      (inThisRepoLocations.``template.tex``, initLocations.``template.tex``.Path)
                      (inThisRepoLocations.Dockerfile, initLocations.Dockerfile.Path)
                      (inThisRepoLocations.``Nuget.config``, initLocations.``Nuget.config``.Path)
                      (inThisRepoLocations.``img/badge-binder.svg``, initLocations.``img/badge-binder.svg``.Path)
                      (inThisRepoLocations.``img/badge-notebook.svg``, initLocations.``img/badge-notebook.svg``.Path)
                      (inThisRepoLocations.``img/badge-script.svg``, initLocations.``img/badge-script.svg``.Path)
                      // these files must be renamed, because files prefixed with a dot are otherwise ignored by fsdocs. We want this in the source repo, but not in the output of this command.
                      (inThisRepoLocations.``templates/init/.logo.png``,
                       Path.GetFullPath(Path.Combine(initLocations.img.Path, "logo.png")))
                      (inThisRepoLocations.``templates/init/.index_md_template.md``,
                       Path.GetFullPath(Path.Combine(initLocations.DocsFolder.Path, "index.md")))
                      (inThisRepoLocations.``templates/init/.literate_sample_template.fsx``,
                       Path.GetFullPath(Path.Combine(initLocations.DocsFolder.Path, "literate_sample.fsx"))) ]

                fileMap
                |> List.map (fun (src, dst) ->
                    src,
                    dst,
                    if this.``non-interactive`` then
                        true
                    else
                        Common.CLI.confirmFileCreation dst src.Description)
                |> List.iter (fun (src, dst, copy) ->
                    if copy then
                        File.Copy(src.Path, dst, this.force))

                printfn ""
                printfn "a basic fsdocs scaffold has been created in %s." this.output
                printfn ""
                printfn "check it out by running 'dotnet fsdocs watch' !"

                0
            with _ as exn ->
                printfn "Error: %s" exn.Message
                1
        else
            printfn
                "no sources for default files found from either %s or %s"
                inNugetPackageLocations.NugetPackageRootPath.Path
                inThisRepoLocations.DocsFolder.Path

            1
