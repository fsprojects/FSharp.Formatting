namespace fsdocs

open CommandLine
open System
open System.IO
open System.Reflection

/// The fsdocs init command: scaffold a minimal docs folder for a new project.
[<Verb("init", HelpText = "initialize a docs folder with a default index.md and optionally a _template.html")>]
type InitCommand() =

    [<Option("input", Required = false, Default = "docs", HelpText = "The directory to initialize (default: docs).")>]
    member val input = "docs" with get, set

    [<Option("force", Required = false, Default = false, HelpText = "Overwrite existing files (default: false).")>]
    member val force = false with get, set

    [<Option("with-template",
             Required = false,
             Default = false,
             HelpText = "Also scaffold a _template.html file in the docs directory.")>]
    member val withTemplate = false with get, set

    member this.Execute() =
        let docsDir =
            if Path.IsPathRooted(this.input) then
                this.input
            else
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, this.input))

        if not (Directory.Exists(docsDir)) then
            printfn "Creating directory: %s" docsDir
            Directory.CreateDirectory(docsDir) |> ignore

        let indexPath = Path.Combine(docsDir, "index.md")

        let writeIfNeeded path content =
            if File.Exists(path) && not this.force then
                printfn "Skipping %s (already exists; use --force to overwrite)" path
            else
                printfn "Writing %s" path
                File.WriteAllText(path, (content: string))

        let indexContent =
            """---
title: Documentation
category: Documentation
categoryindex: 1
index: 1
---

# Your Project Name

Welcome to the documentation for **Your Project Name**.

## Getting Started

Add your documentation here. You can use Markdown, F# scripts (`.fsx`) or Jupyter notebooks (`.ipynb`).

Run `dotnet fsdocs watch` to preview the site locally.
"""

        writeIfNeeded indexPath indexContent

        if this.withTemplate then
            let templatePath = Path.Combine(docsDir, "_template.html")

            let templateContent =
                let asm = Assembly.GetExecutingAssembly()

                use stream = asm.GetManifestResourceStream("fsdocs._template.html")
                use reader = new StreamReader(stream)
                reader.ReadToEnd()

            writeIfNeeded templatePath templateContent

        printfn ""
        printfn "Done! Run 'dotnet fsdocs watch --input %s' to preview your documentation." this.input
        0
