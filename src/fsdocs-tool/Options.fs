namespace fsdocs

module Common =

    let evalString s =
        if System.String.IsNullOrEmpty s then None else Some s

    // https://stackoverflow.com/questions/4126351
    let private pairs (xs: _ seq) =
        seq {
            use enumerator = xs.GetEnumerator()

            while enumerator.MoveNext() do
                let first = enumerator.Current

                if enumerator.MoveNext() then
                    let second = enumerator.Current
                    yield first, second
        }

    let evalPairwiseStrings (a: string array) =
        match Array.tryExactlyOne a with
        | Some v when System.String.IsNullOrWhiteSpace v -> None
        | _ -> a |> pairs |> List.ofSeq |> Some

    let evalPairwiseStringsNoOption (a: string array) =
        evalPairwiseStrings a |> Option.defaultValue []

    let concat a =
        let s = String.concat " " a
        if s = " " then "" else s

    let waitForKey b =
        if b then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore

    open System.IO

    [<RequireQualifiedAccess>]
    module DefaultLocationDescriptions =

        //folders
        [<Literal>]
        let ``docs folder`` = "the path to the folder that contains the inputs (documentation) for fsdocs."

        let ``nuget package root path`` =
            "the root path of the nuget package, e.g. when the tool is installed via `dotnet tool install`."

        [<Literal>]
        let ``templates`` = "contains additional default files (e.g., default files for the `init` command)"

        [<Literal>]
        let ``extras`` = "contains additional default files (e.g., default files for the `init` command)"

        [<Literal>]
        let ``templates/init`` = "contains the default files for the init command."

        [<Literal>]
        let ``img`` = "base folder to contain all images for your documentation"

        [<Literal>]
        let ``content`` = "contains additional content (e.g., custom css themes)"

        // files in the docs folder
        [<Literal>]
        let ``_template.html`` =
            "The root html template used for creating web pages. Documentation pages will all be based on this file with substitutions. If you do not want to customize this file, it is recommended to NOT include it in your docs folder and therefore use the default file that comes with the tool."

        [<Literal>]
        let ``_template.ipynb`` =
            "The root ipynb template used for creating notebooks. Notebooks of your documentation pages will all be based on this file with substitutions. If you do not want to customize this file, it is recommended to NOT include it in your docs folder and therefore use the default file that comes with the tool."

        [<Literal>]
        let ``_template.tex`` =
            "The root tex template used for creating LaTeX. LaTeX versions of your documentation pages will all be based on this file with substitutions. If you do not want to customize this file, it is recommended to NOT include it in your docs folder and therefore use the default file that comes with the tool."

        [<Literal>]
        let ``Dockerfile`` =
            "Dockerfile used for setting up a binder instance that can host the generated notebooks. Include this file if you plan to provide binder links to generated notebooks."

        [<Literal>]
        let ``Nuget.config`` =
            "Additional nuget sources used for setting up a binder instance that can host the generated notebooks. Include this file if you plan to provide binder links to generated notebooks."

        [<Literal>]
        let ``img/badge-binder.svg`` = "A badge that can be used for adding pretty link buttons to binder."

        [<Literal>]
        let ``img/badge-notebook.svg`` =
            "A badge that can be used for adding pretty download link buttons for generated notebooks."

        [<Literal>]
        let ``img/badge-script.svg`` = "A badge that can be used for adding pretty download link buttons for scripts."

        [<Literal>]
        let ``img/logo.png`` = "The logo of the project."

        // specific files for the init command
        [<Literal>]
        let ``templates/init/.logo.png`` = "A logo placeholder for better preview of how pages will look with a logo."

        [<Literal>]
        let ``templates/init/.index_md_template.md`` =
            "A basic landing page markdown template that showcases how markdown files will be rendered."

        [<Literal>]
        let ``templates/init/.literate_sample_template.fsx`` =
            "A basic literate script that showcases how literate scripts will be rendered."

    type AnnotatedPath =
        { Path: string
          Description: string }

        static member Combine(ap: AnnotatedPath, path, ?description) =
            { Path = Path.Combine(ap.Path, path) |> Path.GetFullPath
              Description = defaultArg description "" }

    /// <summary>
    /// A set of default locations in a folder containing documentation inputs for fsdocs.
    ///
    /// When the fsdocs tool binary is called directly via
    ///
    /// `src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe` or `src\fsdocs-tool\bin\Release\net6.0\fsdocs.exe`,
    ///
    /// these locations can also be used
    ///
    /// - as default content for the `watch` and `build` commands when no user equivalents present and `nodefaultcontent` is not set to true. This can be achieved by using the relative assembly path (plus "/docs") of the command classes as `docsFolderPath`.
    ///
    /// - as output paths of the `init` command to initialize a default docs folder structure.
    ///
    /// because the paths will exist relative to the FSharp.Formatting repo root path.
    /// </summary>
    type InDocsFolderLocations(docsFolderPath) =

        // DocsFolderPath : the path to the docs folder which is used as the base path to construct the other paths.
        // note that this folder is not necessarily named "docs", it can be any location that is used as the base folder containing inputs for fsdocs.
        member _.DocsFolder =
            { Path = docsFolderPath
              Description = DefaultLocationDescriptions.``docs folder`` }

        // default folder locations based on the docs folder path
        member this.templates =
            AnnotatedPath.Combine(this.DocsFolder, "templates", DefaultLocationDescriptions.``templates``)

        member this.``templates/init`` =
            AnnotatedPath.Combine(this.templates, "init", DefaultLocationDescriptions.``templates/init``)

        member this.content = AnnotatedPath.Combine(this.DocsFolder, "content", DefaultLocationDescriptions.content)
        member this.img = AnnotatedPath.Combine(this.DocsFolder, "img", DefaultLocationDescriptions.``img``)

        // specific files in the docs folder.
        member this.``template.html`` =
            AnnotatedPath.Combine(this.DocsFolder, "_template.html", DefaultLocationDescriptions.``_template.html``)

        member this.``template.ipynb`` =
            AnnotatedPath.Combine(this.DocsFolder, "_template.ipynb", DefaultLocationDescriptions.``_template.ipynb``)

        member this.``template.tex`` =
            AnnotatedPath.Combine(this.DocsFolder, "_template.tex", DefaultLocationDescriptions.``_template.tex``)

        member this.Dockerfile =
            AnnotatedPath.Combine(this.DocsFolder, "Dockerfile", DefaultLocationDescriptions.Dockerfile)

        member this.``Nuget.config`` =
            AnnotatedPath.Combine(this.DocsFolder, "Nuget.config", DefaultLocationDescriptions.``Nuget.config``)

        member this.``img/badge-binder.svg`` =
            AnnotatedPath.Combine(this.img, "badge-binder.svg", DefaultLocationDescriptions.``img/badge-binder.svg``)

        member this.``img/badge-notebook.svg`` =
            AnnotatedPath.Combine(
                this.img,
                "badge-notebook.svg",
                DefaultLocationDescriptions.``img/badge-notebook.svg``
            )

        member this.``img/badge-script.svg`` =
            AnnotatedPath.Combine(this.img, "badge-script.svg", DefaultLocationDescriptions.``img/badge-script.svg``)

        // specific files for the init command. Note that these typically only exist in the FSharp.Formatting repo because they are to be copied and renamed on running `fsdocs init```
        member this.``templates/init/.logo.png`` =
            AnnotatedPath.Combine(
                this.``templates/init``,
                ".logo.png",
                DefaultLocationDescriptions.``templates/init/.logo.png``
            )

        member this.``templates/init/.index_md_template.md`` =
            AnnotatedPath.Combine(
                this.``templates/init``,
                ".index_md_template.md",
                DefaultLocationDescriptions.``templates/init/.index_md_template.md``
            )

        member this.``templates/init/.literate_sample_template.fsx`` =
            AnnotatedPath.Combine(
                this.``templates/init``,
                ".literate_sample_template.fsx",
                DefaultLocationDescriptions.``templates/init/.literate_sample_template.fsx``
            )

        /// <summary>
        /// returns true if all files and folders of this location exist.
        /// </summary>
        member this.AllLocationsExist() =
            try
                Directory.Exists(this.DocsFolder.Path)
                && Directory.Exists(this.templates.Path)
                && Directory.Exists(this.``templates/init``.Path)
                && Directory.Exists(this.content.Path)
                && Directory.Exists(this.img.Path)
                && File.Exists(this.``template.html``.Path)
                && File.Exists(this.``template.ipynb``.Path)
                && File.Exists(this.``template.tex``.Path)
                && File.Exists(this.Dockerfile.Path)
                && File.Exists(this.``img/badge-binder.svg``.Path)
                && File.Exists(this.``img/badge-notebook.svg``.Path)
                && File.Exists(this.``img/badge-script.svg``.Path)
                && File.Exists(this.``templates/init/.logo.png``.Path)
                && File.Exists(this.``templates/init/.index_md_template.md``.Path)
                && File.Exists(this.``templates/init/.literate_sample_template.fsx``.Path)
            with _ ->
                false

    /// <summary>
    /// a set of default locations in the nuget package created for fsdocs-tool.
    /// these files are to be used when fsdocs is run as dotnet tool installed via `dotnet tool install` in the following scenarios:
    ///
    /// - as default files when running watch or build when there are no user equivalents present and `nodefaultcontent` is not set to true
    ///
    /// - as content of the output of the `init` command to initialize a default docs folder structure.
    ///
    /// Note that the path of these files will always be combined with the given `assemblyPath` because the cli tool will query it's own path on runtime via reflection.
    /// </summary>
    type InNugetPackageLocations(nugetPackageRootPath) =

        // PackageRootPath : the root path of the nuget package, e.g. when the tool is installed via `dotnet tool install`.
        // for example, default on windows would be: ~\.nuget\packages\fsdocs-tool\20.0.0-alpha-010
        member _.NugetPackageRootPath =
            { Path = nugetPackageRootPath
              Description =
                "the root path of the nuget package, e.g. when the tool is installed via `dotnet tool install`." }

        // default folder locations relative to the package root path
        member this.templates =
            AnnotatedPath.Combine(this.NugetPackageRootPath, "templates", DefaultLocationDescriptions.templates)

        member this.``templates/init`` =
            AnnotatedPath.Combine(this.templates, "init", DefaultLocationDescriptions.templates)

        member this.extras = AnnotatedPath.Combine(this.NugetPackageRootPath, "extras")
        member this.``extras/content`` = AnnotatedPath.Combine(this.extras, "content")
        member this.``extras/content/img`` = AnnotatedPath.Combine(this.``extras/content``, "img")

        // specific files in this folder structure that might need special treatment instead of just copy pasting
        member this.``templates/template.html`` = AnnotatedPath.Combine(this.templates, "_template.html")
        member this.``templates/template.ipynb`` = AnnotatedPath.Combine(this.templates, "_template.ipynb")
        member this.``templates/template.tex`` = AnnotatedPath.Combine(this.templates, "_template.tex")
        member this.Dockerfile = AnnotatedPath.Combine(this.extras, "Dockerfile")
        member this.``Nuget.config`` = AnnotatedPath.Combine(this.extras, "Nuget.config")

        member this.``extras/content/img/badge-binder.svg`` =
            AnnotatedPath.Combine(
                this.``extras/content/img``,
                "badge-binder.svg",
                DefaultLocationDescriptions.``img/badge-binder.svg``
            )

        member this.``extras/content/img/badge-notebook.svg`` =
            AnnotatedPath.Combine(
                this.``extras/content/img``,
                "badge-notebook.svg",
                DefaultLocationDescriptions.``img/badge-notebook.svg``
            )

        member this.``extras/content/img/badge-script.svg`` =
            AnnotatedPath.Combine(
                this.``extras/content/img``,
                "badge-script.svg",
                DefaultLocationDescriptions.``img/badge-script.svg``
            )
        // specific files for the init command
        member this.``templates/init/.logo.png`` = AnnotatedPath.Combine(this.``templates/init``, ".logo.png")

        member this.``templates/init/.index_md_template.md`` =
            AnnotatedPath.Combine(this.``templates/init``, ".index_md_template.md")

        member this.``templates/init/.literate_sample_template.fsx`` =
            AnnotatedPath.Combine(this.``templates/init``, ".literate_sample_template.fsx")

        /// <summary>
        /// returns true if all special files and folders of this location exist.
        /// </summary>
        member this.AllLocationsExist() =
            try
                Directory.Exists(this.templates.Path)
                && Directory.Exists(this.extras.Path)
                && Directory.Exists(this.``extras/content``.Path)
                && Directory.Exists(this.``extras/content/img``.Path)
                && File.Exists(this.``templates/template.html``.Path)
                && File.Exists(this.``templates/template.ipynb``.Path)
                && File.Exists(this.``templates/template.tex``.Path)
                && File.Exists(this.Dockerfile.Path)
                && File.Exists(this.``extras/content/img/badge-binder.svg``.Path)
                && File.Exists(this.``extras/content/img/badge-notebook.svg``.Path)
                && File.Exists(this.``extras/content/img/badge-script.svg``.Path)
                && File.Exists(this.``templates/init/.logo.png``.Path)
                && File.Exists(this.``templates/init/.index_md_template.md``.Path)
                && File.Exists(this.``templates/init/.literate_sample_template.fsx``.Path)
            with _ ->
                false

    module CLI =
        open Spectre.Console

        let confirmFileCreation (path: string) (description: string) =
            let table =
                Table()
                    .AddColumn(new TableColumn("""[bold]Default file[/]"""))
                    .AddColumn(new TableColumn($"""[green]{Path.GetFileName(path)}[/]"""))
                    .AddEmptyRow()
                    .AddRow("""[bold]Will be created at[/]""", $"""[green]{path}[/]""")
                    .AddEmptyRow()
                    .AddRow("""[bold]Description[/]""", $"""{description}""")

            AnsiConsole.WriteLine()
            // AnsiConsole.MarkupLine $"""[bold]Default file:[/] [green]{Path.GetFileName(path)}[/]"""
            // AnsiConsole.MarkupLine $"""[bold]Will be created at[/] [green]{path}[/]"""
            // AnsiConsole.MarkupLine $"""[bold]Description:[/] {description}"""
            // AnsiConsole.WriteLine()
            AnsiConsole.Write(table)
            let conf = AnsiConsole.Confirm $"""[bold]Do you want to create this file?[/]"""
            AnsiConsole.WriteLine()
            conf
