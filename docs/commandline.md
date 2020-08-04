# Command line tool

To use F# Formatting tools via the command line, you can use the `fsdocs` dotnet tool.

    dotnet tool install FSharp.Formatting.CommandTool
    dotnet fsdocs [command] [options]

## The build command

This command processes a `docs` directory and generates API docs for projects in the solution according to the
rules of [API doc generation](apidocs.html)

    [lang=text]
    fsdocs build

The input accepted is described in [content](content.html).

The command line options accepted are:

| Option                 |  Description    |
|:-----------------------|:-----------------------------------------:|
| --input     |   Input directory of documentation content (default: `docs`) |
| --projects     |   Project files to build API docs for outputs, defaults to all packable projects. |
| --output         |           Output Directory (default 'output' for 'build' and 'tmp/watch' for 'watch'. |
| --noapidocs       |          (Default: false) Disable generation of API docs. |
| --eval             |         (Default: false) Evaluate F# fragments in scripts. |
| --saveimages        |        (Default: none) Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs. |
| --nolinenumbers       |      Don't add line numbers, default is to add line numbers. |
| --parameters            |    Additional substitution parameters for templates. |
| --nonpublic           |      (Default: false) The tool will also generate documentation for non-public members |
| --nodefaultcontent      |    Do not copy default content styles, javascript or use default templates. |
| --clean                 |    (Default: false) Clean the output directory. |
| --help                  |    Display this help screen. |
| --version               |    Display version information. |

The following command line options are also accepted but it is instead recommended you use
settings in your .fsproj project files:

| Option                 |  Description    |
|:-----------------------|:-----------------------------------------:|
| --sourcefolder       |       Source folder at time of component build (defaults to value of `<FsDocsSourceFolder>` from project file, else current directory) |
| --sourcerepo         |       Source repository for github links (defaults to value of `<FsDocsSourceRepository>` from project file, else `<RepositoryUrl>/tree/<RepositoryBranch>` for Git repositories) |
| --mdcomments           |     Assume /// comments in F# code are markdown style (defaults to value of `<UsesMarkdownComments>` from project file) |

## The watch command

This command does the same as `fsdocs build` but in "watch" mode, waiting for changes. Only the files in the input
directory (e.g. `docs`) are watched.

    [lang=text]
    fsdocs watch

 Restarting may be necesssary on changes to project files. The same parameters are accepted, plus these:

| Option                 |  Description    |
|:-----------------------|:-----------------------------------------:|
| `--noserver`     |   (Default: false) Do not serve content when watching. |
| `--nolaunch`     |   (Default: false) Do not launch a browser window. |
| `--open`     |   (Default: ) URL extension to launch http://localhost:<port>/%s. |
| `--port`     |   (Default: 8901) Port to serve content for http://localhost serving. |



## Searchable docs

When using the command-line tool a Lunr search index is automatically generated in `index.json`.

A search box is included in the default template.  To add a search box
to your own `_template.html`, include `fsdocs-search.js`, which is added to the `content`
by default.

    ...
    <div id="header">
      <div class="searchbox">
        <label for="search-by">
          <i class="fas fa-search"></i>
        </label>
        <input data-search-input="" id="search-by" type="search" placeholder="Search..." />
        <span data-search-clear="">
          <i class="fas fa-times"></i>
        </span>
      </div>
    </div>
    ...

