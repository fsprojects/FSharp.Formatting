---
category: Documentation
categoryindex: 1
index: 2
---
# Command line

To use F# Formatting tools via the command line, you can use the `fsdocs` dotnet tool.

    [lang=text]
    dotnet tool install fsdocs-tool
    dotnet fsdocs [command] [options]

## The build command

This command processes a `docs` directory and generates API docs for projects in the solution according to the
rules of [API doc generation](apidocs.html). The input accepted is described in [content](content.html).

    [lang=text]
    fsdocs build

The command line options accepted are:

| Command Line Option  | Description                                                                                                                                                                                                                                                                                                                                                                                                     |
|:---------------------|:----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `--input`            | Input directory of content (default: `docs`)                                                                                                                                                                                                                                                                                                                                                                    |
| `--projects`         | Project files to build API docs for outputs, defaults to all packable projects                                                                                                                                                                                                                                                                                                                                  |
| `--output`           | Output Directory (default 'output' for 'build' and 'tmp/watch' for 'watch')                                                                                                                                                                                                                                                                                                                                     |
| `--ignoreuncategorized` | Disable generation of the 'Other' category in the navigation bar for uncategorized docs |
| `--noapidocs`        | Disable generation of API docs                                                                                                                                                                                                                                                                                                                                                                                  |
| `--ignoreprojects`   | Disable project cracking                                                                                                                                                                                                                                                                                                                                                                                        |
| `--eval`             | Evaluate F# fragments in scripts                                                                                                                                                                                                                                                                                                                                                                                |
| `--saveimages`       | Save images referenced in docs                                                                                                                                                                                                                                                                                                                                                                                  |
| `--nolinenumbers`    | Don't add line numbers, the default is to add line numbers.                                                                                                                                                                                                                                                                                                                                                          |
| `--parameters`       | Additional substitution parameters for templates                                                                                                                                                                                                                                                                                                                                                                |
| `--nonpublic`        | The tool will also generate documentation for non-public members                                                                                                                                                                                                                                                                                                                                                |
| `--nodefaultcontent` | Do not copy default content styles, javascript or use default templates                                                                                                                                                                                                                                                                                                                                         |
| `--clean`            | Clean the output directory                                                                                                                                                                                                                                                                                                                                                                                      |
| `--help`             | Display this help screen                                                                                                                                                                                                                                                                                                                                                                                        |
| `--version`          | Display version information                                                                                                                                                                                                                                                                                                                                                                                     |
| `--properties`       | Provide properties to dotnet msbuild, e.g. --properties Configuration=Release Version=3.4                                                                                                                                                                                                                                                                                                                       |
| `--fscoptions`       | Additional arguments passed down as `otherflags` to the F# compiler when the API is being generated.<br/>Note that these arguments are trimmed, this is to overcome [a limitation in the command line argument processing](https://github.com/commandlineparser/commandline/issues/58).<br/>A typical use-case would be to pass an addition assembly reference.<br/>Example `--fscoptions " -r:MyAssembly.dll"` |
| `--strict`           | Fail if docs are missing or can't be generated                                                                                                                                                                                                                                                                                                                                                                  |

The following command line options are also accepted but it is instead recommended you use
settings in your .fsproj project files:

| Command Line Option                 |  Description    |
|:-----------------------|:-----------------------------------------|
| `--sourcefolder`       |       Source folder at time of component build (`<FsDocsSourceFolder>`) |
| `--sourcerepo`         |       Source repository for github links (`<FsDocsSourceRepository>`) |
| `--mdcomments`           |     Assume comments in F# code are markdown (`<UsesMarkdownComments>`) |

The command will report on any `.fsproj` files that it finds, telling you if it decides to skip a particular file and why.

For example, a project will be skipped if:

* The project name contains ".Tests" or "test" (because it looks like a test project)

* The project does not contain
```
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
```

* The project `OutputType` is not `Library`. To include an executable project, add this to the project file:
```
  <FsDocsAllowExecutableProject>true</FsDocsAllowExecutableProject>
```

## The watch command

This command does the same as `fsdocs build` but in "watch" mode, waiting for changes. Only the files in the input
directory (e.g. `docs`) are watched. A browser will be launched automatically (unless `--nolaunch` is specified).

You will need to ensure that the input directory exists, and contains at least `index.md`, otherwise the browser will
report an error (e.g. "Problem loading...", "Connection was reset").

    [lang=text]
    fsdocs watch

 Restarting may be necesssary on changes to project files. The same parameters are accepted, plus these:

| Command Line Option                 |  Description    |
|:-----------------------|:-----------------------------------------|
| `--noserver`     |   Do not serve content when watching.  |
| `--nolaunch`     |   Do not launch a browser window. |
| `--open`     |   URL extension to launch http://localhost:<port>/%s. |
| `--port`     |   Port to serve content for http://localhost serving. |



## Searchable docs

When using the command-line tool a [Fuse](https://www.fusejs.io/) search index is automatically generated in `index.json`. 
A search box is included in the default template via an [HTML Dialog element](https://developer.mozilla.org/docs/Web/HTML/Element/dialog).  
To add search to your own `_template.html`:

- include an HTML element with id `search-btn`
- include a `dialog` element
- include `fsdocs-search.js` script

```html
<button id="search-btn">Open search dialog</button>
<dialog>
    <input type="search" placeholder="Search docs" />
    <div class="results">
        <ul></ul>
        <p class="empty">Type something to start searching.</p>
    </div>
</dialog>
<script type="module" src="{`{root}}content/fsdocs-search.js"></script>
```
