fsdocs and FSharp.Formatting ![Build and Test](https://github.com/fsprojects/FSharp.Formatting/actions/workflows/push-main.yml/badge.svg)
=================================

The FSharp.Formatting package includes libraries and tools for processing F# script files, markdown and components
for documentation generation. The primary tool is "fsdocs".

See https://fsprojects.github.io/FSharp.Formatting/


## Development

    dotnet fsi build.fsx


Once built, you can run the command-line tool to self-build the docs for this directory using 

    dotnet build
    src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe watch
    src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe build --clean

### Pipelines

Run
    dotnet fsi build.fsx -- --help

to see what other pipelines can be run from `build.fsx`.

    dotnet fsi build.fsx -- -p Verify

Will perform the linting, unit tests and analyzer check.
This is useful to run locally before submitting your PR.

## Maintainer(s)

- [@dsyme](https://github.com/dsyme)
- [@baronfel](https://github.com/baronfel)
- [@nhirschey](https://github.com/nhirschey)
- [@nojaf](https://github.com/nojaf)
