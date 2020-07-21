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

  * `--input` - (Default: docs) Input directory of documentation content.
  * --projects - Project files to build API docs for outputs, defaults to all packable projects.
  * --output     -         Output Directory (default 'output' for 'build' and 'tmp/watch' for 'watch'.
  * --noapidocs   -        (Default: false) Disable generation of API docs.
  * --eval         -       (Default: false) Evaluate F# fragments in scripts.
  * --saveimages    -      (Default: none) Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs.
  * --sourcefolder   -     Source folder within the source repo (which is detected from project properties).
  * --sourcerepo     -     Source repository for github links.
  * --nolinenumbers   -    Don't add line numbers, default is to add line numbers.
  * --nonpublic       -    (Default: false) The tool will also generate documentation for non-public members
  * --mdcomments       -   (Default: false) Assume /// comments in F# code are markdown style.
  * --parameters        -  Additional substitution parameters for templates.
  * --nodefaultcontent  -  Do not copy default content styles, javascript or use default templates.
  * --clean             -  (Default: false) Clean the output directory.
  * --help              -  Display this help screen.
  * --version           -  Display version information.
  

The watch command
----------------------------

This command does the same as `fsdocs build` but in "watch" mode, waiting for changes. Only the files in the input
directory (e.g. `docs`) are watched.

    [lang=text]
    fsdocs watch

 Restarting may be necesssary on changes to project files. The same parameters are accepted, plus these:

  * `--noserver` - (Default: false) Do not serve content when watching.
  * `--nolaunch` - (Default: false) Do not launch a browser window.
  * `--open` - (Default: ) URL extension to launch http://localhost:<port>/%s.
  * `--port` - (Default: 8901) Port to serve content for http://localhost serving.



