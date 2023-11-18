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

    /// <summary>
    /// a set of default locations in this repo.
    /// these files are to be used in 2 scenarios assuming we are executing directly from
    ///
    ///   src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe
    ///
    ///   src\fsdocs-tool\bin\Release\net6.0\fsdocs.exe:
    ///
    /// - as default styles when running watch or build when there are no user equivalents present and `nodefaultcontent` is not set to true
    ///
    /// - as content of the output of the `init` command to initialize a default docs folder structure.
    ///
    /// Note that the path of these files will always be combined with the given `assemblyPath` because the cli tool will query it's own path on runtime via reflection.
    /// </summary>
    type InRepoLocations(relAssemblyPath) =

        // relAssemblyPath : relative path from assemly to repo root path
        member _.RelAssemblyPath = relAssemblyPath

        // default folder locations relative to the assembly path
        member this.docs = Path.Combine(this.RelAssemblyPath, "docs") |> Path.GetFullPath
        member this.docs_content = Path.Combine(this.docs, "content") |> Path.GetFullPath
        member this.docs_content_image = Path.Combine(this.docs_content, "img") |> Path.GetFullPath

        // specific files in this folder structure that might need special treatment instead of just copy pasting
        member this.template_html = Path.Combine(this.docs, "_template.html") |> Path.GetFullPath
        member this.template_ipynb = Path.Combine(this.docs, "_template.ipynb") |> Path.GetFullPath
        member this.template_tex = Path.Combine(this.docs, "_template.tex") |> Path.GetFullPath

        /// <summary>
        /// returns true if all special files and folders of this location exist.
        /// </summary>
        member this.Exist() =
            try
                Directory.Exists(this.docs)
                && Directory.Exists(this.docs_content)
                && Directory.Exists(this.docs_content_image)
                && File.Exists(this.template_html)
                && File.Exists(this.template_ipynb)
                && File.Exists(this.template_tex)
            with _ ->
                false

    /// <summary>
    /// a set of default locations in the nuget package created for fsdocs-tool.
    /// these files are to be used in 2 scenarios assuming the tool is invoked via cli:
    ///
    /// - as default styles when running watch or build when there are no user equivalents present and `nodefaultcontent` is not set to true
    ///
    /// - as content of the output of the `init` command to initialize a default docs folder structure.
    ///
    /// Note that the path of these files will always be combined with the given `assemblyPath` because the cli tool will query it's own path on runtime via reflection.
    /// </summary>
    type InPackageLocations(relAssemblyPath) =

        // relAssemblyPath : relative path from assemly to package root path
        member _.RelAssemblyPath = relAssemblyPath

        //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net6.0\any
        //   to .nuget\packages\fsdocs-tool\7.1.7\*

        // default folder locations relative to the assembly path
        member this.templates = Path.Combine(this.RelAssemblyPath, "templates") |> Path.GetFullPath
        member this.extras = Path.Combine(this.RelAssemblyPath, "extras") |> Path.GetFullPath
        member this.extras_content = Path.Combine(this.extras, "content") |> Path.GetFullPath
        member this.extras_content_img = Path.Combine(this.extras_content, "img") |> Path.GetFullPath

        // specific files in this folder structure that might need special treatment instead of just copy pasting
        member this.template_html = Path.Combine(this.templates, "_template.html") |> Path.GetFullPath
        member this.template_ipynb = Path.Combine(this.templates, "_template.ipynb") |> Path.GetFullPath
        member this.template_tex = Path.Combine(this.templates, "_template.tex") |> Path.GetFullPath

        /// <summary>
        /// returns true if all special files and folders of this location exist.
        /// </summary>
        member this.Exist() =
            try
                Directory.Exists(this.templates)
                && Directory.Exists(this.extras)
                && Directory.Exists(this.extras_content)
                && Directory.Exists(this.extras_content_img)
                && File.Exists(this.template_html)
                && File.Exists(this.template_ipynb)
                && File.Exists(this.template_tex)
            with _ ->
                false
