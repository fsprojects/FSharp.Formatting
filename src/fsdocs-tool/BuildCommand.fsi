namespace fsdocs

open CommandLine

open System
open System.Diagnostics
open System.IO
open System.Reflection

open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open fsdocs.Common
open FSharp.Formatting.Templating
open Microsoft.Extensions.Logging

type CoreBuildOptions =
    new: watch: bool -> CoreBuildOptions

    [<Option("input", Required = false, Default = "docs", HelpText = "Input directory of documentation content.")>]
    member input: string with get, set

    [<Option("projects",
             Required = false,
             HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member projects: string seq with get, set

    [<Option("output",
             Required = false,
             HelpText = "Output Folder (default 'output'). Ignored by 'watch', which keeps no output folder.")>]
    member output: string with get, set

    [<Option("noapidocs", Default = false, Required = false, HelpText = "Disable generation of API docs.")>]
    member noapidocs: bool with get, set

    [<Option("ignoreuncategorized",
             Default = false,
             Required = false,
             HelpText = "Disable generation of 'Other' category for uncategorized docs.")>]
    member ignoreuncategorized: bool with get, set

    [<Option("ignoreprojects", Default = false, Required = false, HelpText = "Disable project cracking.")>]
    member ignoreprojects: bool with get, set

    [<Option("strict", Default = false, Required = false, HelpText = "Fail if there is a problem generating docs.")>]
    member strict: bool with get, set

    [<Option("eval", Default = false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member eval: bool with get, set

    [<Option("qualify",
             Default = false,
             Required = false,
             HelpText =
                 "In API doc generation qualify the output by the collection name, e.g. 'reference/FSharp.Core/...' instead of 'reference/...' .")>]
    member qualify: bool with get, set

    [<Option("saveimages",
             Default = "none",
             Required = false,
             HelpText =
                 "Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs.")>]
    member saveImages: string with get, set

    [<Option("sourcefolder",
             Required = false,
             HelpText =
                 "Source folder at time of component build (defaults to value of `<FsDocsSourceFolder>` from project file, else current directory)")>]
    member sourceFolder: string with get, set

    [<Option("sourcerepo",
             Required = false,
             HelpText =
                 "Source repository for github links (defaults to value of `<FsDocsSourceRepository>` from project file, else `<RepositoryUrl>/tree/<RepositoryBranch>` for Git repositories)")>]
    member sourceRepo: string with get, set

    [<Option("linenumbers", Default = false, Required = false, HelpText = "Add line numbers.")>]
    member linenumbers: bool with get, set

    [<Option("nonpublic",
             Default = false,
             Required = false,
             HelpText = "The tool will also generate documentation for non-public members")>]
    member nonpublic: bool with get, set

    [<Option("mdcomments",
             Default = false,
             Required = false,
             HelpText =
                 "Assume /// comments in F# code are markdown style (defaults to value of `<UsesMarkdownComments>` from project file)")>]
    member mdcomments: bool with get, set

    [<Option("parameters",
             Required = false,
             HelpText = "Additional substitution substitutions for templates, e.g. --parameters key1 value1 key2 value2")>]
    member parameters: string seq with get, set

    [<Option("nodefaultcontent",
             Required = false,
             HelpText = "Do not copy default content styles, javascript or use default templates.")>]
    member nodefaultcontent: bool with get, set

    [<Option("properties",
             Required = false,
             HelpText = "Provide properties to dotnet msbuild, e.g. --properties Configuration=Release Version=3.4")>]
    member extraMsbuildProperties: string seq with get, set

    [<Option("fscoptions",
             Required = false,
             HelpText = "Extra flags for F# compiler analysis, e.g. dependency resolution.")>]
    member fscoptions: string seq with get, set

    [<Option("clean", Required = false, Default = false, HelpText = "Clean the output directory.")>]
    member clean: bool with get, set

    [<Option('v',
             "verbosity",
             Required = false,
             Default = "normal",
             HelpText = "How much to log: quiet, minimal, normal, detailed or diagnostic.")>]
    member verbosity: string with get, set

    member Execute: unit -> int

    /// Options given on the command line that have no effect for this command.
    abstract ignoredOptions: IgnoredOption list
    default ignoredOptions: IgnoredOption list

    abstract nolaunch_option: bool
    default nolaunch_option: bool

    abstract open_option: string
    default open_option: string

    abstract port_option: int
    default port_option: int

    abstract host_option: string
    default host_option: string

    abstract site_root_option: string option
    default site_root_option: string option

[<Verb("convert",
       HelpText =
           "convert a single document (.md, .fsx, .ipynb) to HTML or another output format without building a full documentation site")>]
type ConvertCommand =
    new: unit -> ConvertCommand

    [<Value(0, MetaName = "input", Required = true, HelpText = "Input file to convert (.md, .fsx or .ipynb).")>]
    member input: string with get, set

    [<Option('o',
             "output",
             Required = false,
             HelpText =
                 "Output file path. Defaults to the input filename with the output format extension in the current directory.")>]
    member output: string with get, set

    [<Option("template",
             Required = false,
             HelpText =
                 "Path to an HTML template file, or 'fsdocs' to use the built-in default template. When omitted, raw content is written.")>]
    member template: string with get, set

    [<Option("outputformat",
             Required = false,
             Default = "",
             HelpText =
                 "Output format: html (default), ipynb, latex, fsx, markdown. When not specified, inferred from the output file extension.")>]
    member outputFormat: string with get, set

    [<Option("eval", Default = false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member eval: bool with get, set

    [<Option("linenumbers", Default = false, Required = false, HelpText = "Add line numbers.")>]
    member linenumbers: bool with get, set

    [<Option("parameters",
             Required = false,
             HelpText = "Additional substitution parameters, e.g. --parameters key1 value1 key2 value2")>]
    member parameters: string seq with get, set

    [<Option("no-embed-resources",
             Default = false,
             Required = false,
             HelpText =
                 "Disable automatic inlining of local CSS, JS, and images into the output HTML. By default, when a template is used for HTML output, all locally-referenced assets are embedded so the output is a self-contained single file.")>]
    member noEmbedResources: bool with get, set

    [<Option('v',
             "verbosity",
             Required = false,
             Default = "normal",
             HelpText = "How much to log: quiet, minimal, normal, detailed or diagnostic.")>]
    member verbosity: string with get, set

    member Execute: unit -> int

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand =
    new: unit -> BuildCommand
    inherit CoreBuildOptions

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults, watch it and serve it")>]
type WatchCommand =
    new: unit -> WatchCommand
    inherit CoreBuildOptions
    override ignoredOptions: IgnoredOption list
    override nolaunch_option: bool

    [<Option("nolaunch", Required = false, Default = false, HelpText = "Do not launch a browser window.")>]
    member nolaunch: bool with get, set

    override open_option: string

    [<Option("open", Required = false, Default = "", HelpText = "URL extension to launch http://localhost:<port>/%s.")>]
    member openv: string with get, set

    override port_option: int

    [<Option("port", Required = false, Default = 8901, HelpText = "Port to serve content for http://localhost serving.")>]
    member port: int with get, set

    override host_option: string

    [<Option("host",
             Required = false,
             Default = "localhost",
             HelpText =
                 "Address to bind the server to. Use 0.0.0.0 to serve on all interfaces, e.g. to browse from another machine; page links are relative so the site works from any address.")>]
    member host: string with get, set

    override site_root_option: string option

    [<Option("site-root",
             Required = false,
             Default = "",
             HelpText =
                 "The absolute URL of the site ({{fsdocs-site-root}}), only used by the links that must be absolute such as Open Graph metadata and llms.txt; page links are relative. When not set, defaults to http://<host>:<port>/.")>]
    member siteroot: string with get, set
