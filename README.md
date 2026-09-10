fsdocs and FSharp.Formatting ![Build and Test](https://github.com/fsprojects/FSharp.Formatting/actions/workflows/push-main.yml/badge.svg) [![NuGet](https://img.shields.io/nuget/v/fsdocs-tool.svg)](https://www.nuget.org/packages/fsdocs-tool)
=================================

FSharp.Formatting is a set of libraries and tools for processing F# script files and markdown, and for
generating API documentation from XML doc comments. The primary tool is `fsdocs`.

Full documentation: https://fsprojects.github.io/FSharp.Formatting/

## The fsdocs tool

`fsdocs` builds a static documentation site from the markdown files and F# scripts in your `docs`
folder, plus API reference pages for the projects in your solution.

    dotnet tool install fsdocs-tool
    dotnet fsdocs build
    dotnet fsdocs watch

- `init` creates a `docs` folder with a default `index.md`.
- `build` processes the `docs` folder and writes the site to `output`.
- `watch` builds the site, serves it locally and rebuilds on every change.
- `convert` converts individual markdown and script files.

Run `dotnet fsdocs <command> --help` to see the options of a command, or read the
[command line guide](https://fsprojects.github.io/FSharp.Formatting/commandline.html).

## The libraries

The same functionality is available as NuGet packages for use from your own code:

- [FSharp.Formatting](https://www.nuget.org/packages/FSharp.Formatting) bundles the markdown parser,
  F# code formatter, literate programming support and API doc generator.
- [FSharp.Formatting.Literate](https://www.nuget.org/packages/FSharp.Formatting.Literate) only contains
  the literate programming support and its dependencies.

See [Markdown parser](https://fsprojects.github.io/FSharp.Formatting/markdown.html),
[F# code formatting](https://fsprojects.github.io/FSharp.Formatting/codeformat.html) and
[literate programming](https://fsprojects.github.io/FSharp.Formatting/literate.html) for examples.

## Contributing

See [CONTRIBUTING.md](https://github.com/fsprojects/FSharp.Formatting/blob/main/CONTRIBUTING.md) for how to build the repository and run the tool locally.

## Maintainer(s)

- [@dsyme](https://github.com/dsyme)
- [@nojaf](https://github.com/nojaf)
- [@nhirschey](https://github.com/nhirschey)
