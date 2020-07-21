F# Formatting: Command line tool
================================

To use F# Formatting tools via the command line, you can use the `fsdocs` dotnet tool.

    dotnet tool install FSharp.Formatting.CommandTool
    dotnet fsdocs [command] [options]

The build command
----------------------------

This command processes a `docs` directory and generates API docs for projects in the solution according to the
rules of [API doc generation](apidocs.html)

    [lang=text]
    fsdocs build

The input accepted is described in [content](content.html).

The command line options accepted are:

  * `--input` - Input directory containing `*.fsx` and `*.md` files and other content, defaults to `docs`.
  * `--projects` - The project files to process. Defaults to the packable projects in the solution in the current directory, else all packable projects.
  * `--output` -  Output directory, defaults to `output`
  * `--noapidocs` -  Do not generate API docs
  * `--eval` - Use the default FsiEvaluator to actually evaluate code in documentation, defaults to `false`.
  * `--nolinenumbers` -  Line number option, defaults to `true`.
  * `--nonpublic` -  Generate docs for non-public members
  * `--mdcomments` -  Assume `///` comments in F# code are markdown style
  * `--saveImages` -  Save images referenced in docs (some|none|all).
  * `--parameters` -  A whitespace separated list of string pairs as extra text replacement patterns for the format template file.
  * `--clean` -  Clean the output directory before building (except directories starting with ".")
  * `--help` -  Display the specific help message for `convert`.

The watch command
----------------------------

This command does the same as `fsdocs build` but in "watch" mode, waiting for changes. Only the files in the input
directory (e.g. `docs`) are watched.

    [lang=text]
    fsdocs watch

 Restarting may be necesssary on changes to project files. The same parameters are accepted, plus these:

  * `--noserver` -  Do not serve content on http://localhost:<port>.
  * `--port` -  Port to serve content for http://localhost serving.



