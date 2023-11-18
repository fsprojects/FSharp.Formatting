namespace fsdocs

open System.IO
open CommandLine

[<Verb("init", HelpText = "initialize the necessary folder structure and files for creating documentation with fsdocs.")>]
type InitCommand() =

    let dir = Path.GetDirectoryName(typeof<InitCommand>.Assembly.Location)

    // get template locations for in-package and in-repo and decide which to use later
    let inPackageLocations = Common.InPackageLocations(Path.Combine(dir, "..", "..", ".."))
    let inRepoLocations = Common.InRepoLocations(Path.Combine(dir, "..", "..", "..", "..", ".."))

    [<Option("output",
             Required = false,
             Default = "docs",
             HelpText = "The output path for the documentation folder structure")>]
    member val output: string = "docs" with get, set

    member this.Execute() =

        let outputPath = Path.GetFullPath(this.output)
        let repoRoot = Path.GetFullPath(Path.Combine(outputPath, ".."))
        let initLocations = Common.InRepoLocations(repoRoot)

        let ensureOutputDirs () =
            [ outputPath; initLocations.docs; initLocations.docs_img ]
            |> List.iter ensureDirectory

        if inPackageLocations.Exist() then
            ensureOutputDirs ()

            try
                [ (inPackageLocations.template_html, initLocations.template_html)
                  (inPackageLocations.template_ipynb, initLocations.template_ipynb)
                  (inPackageLocations.template_tex, initLocations.template_tex)
                  (inPackageLocations.dockerfile, initLocations.dockerfile)
                  (inPackageLocations.nuget_config, initLocations.nuget_config)
                  (inPackageLocations.logo_template, Path.GetFullPath(Path.Combine(initLocations.docs_img, "logo.png")))
                  (inPackageLocations.index_md_template, Path.GetFullPath(Path.Combine(initLocations.docs, "index.md")))
                  (inPackageLocations.literate_sample_template,
                   Path.GetFullPath(Path.Combine(initLocations.docs, "literate_sample.fsx"))) ]
                |> List.iter (fun (src, dst) -> File.Copy(src, dst, true))

                0
            with e as exn ->
                printfn "Error: %s" exn.Message
                1

        elif inRepoLocations.Exist() then
            ensureOutputDirs ()

            try
                [ (inRepoLocations.template_html, initLocations.template_html)
                  (inRepoLocations.template_ipynb, initLocations.template_ipynb)
                  (inRepoLocations.template_tex, initLocations.template_tex)
                  (inRepoLocations.dockerfile, initLocations.dockerfile)
                  (inRepoLocations.nuget_config, initLocations.nuget_config)
                  (inRepoLocations.logo_template, Path.GetFullPath(Path.Combine(initLocations.docs_img, "logo.png")))
                  (inRepoLocations.index_md_template, Path.GetFullPath(Path.Combine(initLocations.docs, "index.md")))
                  (inRepoLocations.literate_sample_template,
                   Path.GetFullPath(Path.Combine(initLocations.docs, "literate_sample.fsx"))) ]
                |> List.iter (fun (src, dst) -> File.Copy(src, dst, true))

                0
            with e as exn ->
                printfn "Error: %s" exn.Message
                1
        else
            printfn
                "no sources for default files found from either %s or %s"
                inPackageLocations.RelAssemblyPath
                inRepoLocations.RelAssemblyPath

            1
