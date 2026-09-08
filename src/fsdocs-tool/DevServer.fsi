namespace fsdocs

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open System.Threading
open System.Diagnostics

open FSharp.Data.Adaptive
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.Templating
open Microsoft.Extensions.Logging

open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Operators
open Suave.Filters

/// Processes and runs Suave server to host them on localhost
module Serve =
    /// generate the script to inject into html to enable hot reload during development
    val generateWatchScript: unit -> string
    /// The port to actually serve on: the given port if free, else port + 1; raises with a clear
    /// message when neither is free.
    val resolvePort: host: string -> port: int -> int

/// The websocket clients of the browser live reload and the broadcasts to them.
type internal LiveReload =
    internal new: unit -> LiveReload
    member internal SocketHandler: webSocket: WebSocket -> _context: HttpContext -> SocketOp<unit>
    /// Send a message to every connected browser: a css file name is hot swapped, anything else reloads the page.
    member internal Broadcast: msg: string -> unit
    member internal ClientCount: int

/// Identity of a watched file as seen by the dependency graph.
[<Struct>]
type internal FileStamp =
    {
        Length: int64
        LastWriteUtc: DateTime
        /// SHA-256 of the content for files whose content feeds the graph, otherwise empty
        Hash: string
    }

/// A content page served by the site: the input file and how it is rendered for one output kind.
type internal ContentRoute =
    {
        InputFile: string
        OutputKind: OutputKind
        Template: string option
        OutputFileRelativeToRoot: string
        OutputFolderRelativeToRoot: string
        /// The folder containing the input file
        InputFolder: string
        /// The input root (as given on the command line) this file belongs to
        RootInputFolder: string
        IsOtherLang: bool
    }

/// What a URL of the site resolves to.
type internal Route =
    | ContentPage of ContentRoute
    | StaticFile of sourceFullPath: string
    | ApiPage of relativeFile: string
    | SearchIndex
    | LlmsTxt
    | LlmsFullTxt

/// Cheap facts about a content file, recomputed when the file changes.
type internal ContentMeta =
    {
        FrontMatter: ScannedFrontMatter
        /// The front matter used for next/previous links, present only when complete
        FrontMatterFile: FrontMatterFile option
        /// Full paths of the files loaded with '#load'
        Loads: string list
        /// Whether the file mentions 'cref:' and therefore needs the API model
        UsesCref: bool
        Error: string option
    }

/// The result of scanning the input trees.
type internal ScanResult =
    {
        Routes: Map<string, Route>
        FullPathFileMap: Map<(string * OutputKind), string>
        FilesWithFrontMatter: FrontMatterFile array
        NavPages: NavPage list
        TitleSources: Map<string, TitleSource>
        Skipped: (string * string) list
    }

/// The API documentation model with its page renderers, rebuilt when a project DLL changes.
[<ReferenceEquality>]
type internal ApiState =
    {
        Phased: ApiDocsPhased option
        /// The global substitutions for a page with the given relative root
        GlobalsFor: string -> Substitutions
        /// The 'cref:' resolver for a page with the given relative root
        CrefResolver: string -> string -> (string * string) option
        Pages: Map<string, string option -> Substitutions -> string>
        SearchIndex: ApiDocsSearchIndexEntry array
        Error: string option
        BuiltAt: DateTime option
    }

type internal Response =
    {
        ContentType: string
        Body: byte array
    }

type internal RenderResult =
    | Rendered of Response
    | NotFound
    | Failed of exn

type internal WatchEvent =
    {
        Time: DateTime
        Path: string
        Change: string
        Invalidated: bool
    }

type internal UrlState =
    {
        Url: string
        Valid: bool
        LastBuilt: DateTime option
        LastError: string option
    }

type internal SiteConfig =
    {
        Input: string
        /// Extra input folders (as given) and the output folder they map to
        ExtraInputs: (string * string) list
        /// The folder holding the default template, watched for changes
        DefaultTemplateFolder: string option
        /// The absolute URL of the site; the pages use relative roots
        Root: string
        CollectionName: string
        DefaultTemplate: string option
        DefaultMdTemplate: string option
        GenerateLlmsTxt: bool
        IgnoreUncategorized: bool
        ContentOptions: ContentOptions
        /// The project DLLs feeding the API docs
        ApiDllPaths: string list
        ApiDocsOutputKind: OutputKind
        ApiDocsTemplate: string option
        /// Generate the API docs for a (virtual) output folder, None when there are none
        GenerateApi: CrackResult -> string -> ApiDocsPhased option
        /// Re-crack the projects from disk (an evaluation, no design-time build); called when a
        /// project file changes
        Crack: unit -> CrackResult
        /// Run the design-time build of the projects on disk and return the crack result with the
        /// references and the properties set by targets; 'true' skips the on-disk cache
        Resolve: bool -> CrackResult
        /// The project files and solution-wide MSBuild files that feed the crack
        ProjectFiles: string list
        WatchScript: string
        Diagnostics: Diagnostics
    }

/// The documentation site as an adaptive dependency graph: every URL is computed on first
/// request and cached until a watched file that influences it changes. No output folder is written.
type internal Site =
    internal new: config: SiteConfig -> Site
    /// The mime type for a static file.
    member internal MimeOf: path: string -> string
    /// Resolve a URL path (e.g. '/index.html') to what it is served from.
    member internal Resolve: url: string -> Route option
    /// The URL to redirect a folder URL without its trailing slash to (e.g. '/docs' to '/docs/'), as
    /// static web servers do. The relative links of the index page only work with the slash.
    member internal RedirectTo: url: string -> string option
    /// Compute (or reuse) the response for a URL path. Static files are read from their source.
    member internal Render: url: string -> RenderResult
    /// Bring the graph up to date with a file that may have changed.
    member internal Refresh: path: string -> bool
    /// Walk the watched roots and refresh every difference with the last snapshot.
    member internal Reconcile: unit -> unit
    /// Raised (with the full path) whenever a change invalidated something in the graph.
    member internal Changed: IEvent<string>
    /// The number of content page models computed so far.
    member internal ModelComputations: int
    /// The content page models computed so far, oldest first (at most the last 500).
    member internal ComputedModels: (string * OutputKind * DateTime) list
    /// The current scan of the input trees.
    member internal Scan: ScanResult
    /// The current crack result (re-cracks when a project file changed), with the design-time
    /// build when it ran.
    member internal CrackResult: CrackResult
    /// When the design-time build of the current projects ran in this session, if it did.
    member internal DesignTimeBuiltAt: DateTime option
    /// Run the design-time build of the projects now, skipping the cache.
    member internal RunDesignTimeBuild: unit -> unit
    /// The API docs state when it has been built, None when it is not built or being built.
    member internal ApiState: ApiState option
    /// Whether the API docs are being generated right now.
    member internal ApiBuilding: bool
    member internal UrlStates: UrlState list
    /// The cache state of every URL that has a node, whether requested since its last invalidation or not.
    member internal NodeStates: (string * bool) list
    member internal Events: WatchEvent list
    /// The error messages reported while computing pages and API docs, oldest first.
    member internal Errors: (DateTime * string) list
    member internal Config: SiteConfig
    /// Warn when the logo or the favicon of the site is not served: the value comes from the cracked
    /// projects (or the default), which need not match the input folder when the tool runs elsewhere.
    member internal CheckAssets: unit -> unit
    /// Start the file watchers, the reconciler and the background API docs build.
    member internal Start: unit -> unit
    interface IDisposable

/// The state of the watch session, as shown by /.fsdocs/doctor and /.fsdocs/doctor.json.
/// Plain records and strings only, so System.Text.Json can serialize it.
type DoctorSubstitution =
    {
        Key: string
        Value: string
        Source: string
    }

type DoctorSubstitutionChange =
    {
        Key: string
        Before: string
        After: string
    }

type DoctorProject =
    {
        ProjectFile: string
        TargetPath: string
        TargetExists: bool
        /// 'done' once the design-time build ran, else 'not run yet'
        DesignTimeBuild: string
        References: string list
        DroppedReferences: string list
        /// The substitutions whose value the design-time build changed (properties set by targets)
        ChangedByDesignTimeBuild: DoctorSubstitutionChange list
        OverridingSubstitutions: DoctorSubstitution list
    }

type DoctorRoute =
    {
        Url: string
        Kind: string
        Source: string
        Template: string option
    }

type DoctorNavPage =
    {
        Title: string
        TitleSource: string
        Category: string option
        CategoryIndex: int option
        Index: int option
        Source: string
        Url: string
    }

type DoctorSkipped = { Path: string; Reason: string }

type DoctorApi =
    {
        Dlls: string list
        Template: string option
        OutputKind: string
        Status: string
        BuiltAt: DateTime option
        Error: string option
        Namespaces: int
        Entities: int
        Pages: int
    }

type DoctorUrl =
    {
        Url: string
        State: string
        LastBuilt: DateTime option
        LastError: string option
    }

type DoctorEvent =
    {
        Time: DateTime
        Path: string
        Change: string
        Invalidated: bool
    }

type DoctorComputed =
    {
        Time: DateTime
        Source: string
        OutputKind: string
    }

type DoctorError = { Time: DateTime; Message: string }

type Doctor =
    {
        ToolVersion: string
        CommandLine: string
        Command: string
        Input: string
        Root: string
        CollectionName: string
        GenerateLlmsTxt: bool
        IgnoredOptions: IgnoredOption list
        Projects: DoctorProject list
        /// When the design-time build of the projects ran in this session, None when it did not
        DesignTimeBuiltAt: DateTime option
        Substitutions: DoctorSubstitution list
        DefaultTemplate: ResolutionDiagnostics
        DefaultMarkdownTemplate: ResolutionDiagnostics
        ApiDocsTemplate: ResolutionDiagnostics
        Extras: ResolutionDiagnostics
        HeadTemplate: string option
        BodyTemplate: string option
        MenuTemplatesFound: bool
        Routes: DoctorRoute list
        NavPages: DoctorNavPage list
        Skipped: DoctorSkipped list
        Api: DoctorApi
        Urls: DoctorUrl list
        Events: DoctorEvent list
        ComputedModels: DoctorComputed list
        Errors: DoctorError list
        /// The last log lines of the process, oldest first
        Log: LogLine list
    }

/// The Suave application serving a Site.
module internal DevServer =
    val startWebServer: site: Site -> host: string -> port: int -> unit
