namespace fsdocs

open System
open System.Diagnostics
open System.IO
open System.Runtime.Serialization
open System.Xml

open System.Xml.Linq
open FSharp.Formatting.Templating
open FSharp.Formatting.Common

open Ionide.ProjInfo

/// General utility helpers shared across the fsdocs tool.
[<AutoOpen>]
module Utils =
    /// Creates the directory at <c>path</c> if it does not already exist.
    val ensureDirectory: path: string -> unit
    /// Attempts to restore a previously cached value from <c>cacheFile</c>. If the cache
    /// is absent or invalid (as judged by <c>cacheValid</c>), calls <c>f</c> to compute
    /// a fresh value and saves it to the cache.
    val cacheBinary: cacheFile: string -> cacheValid: ('T -> bool) -> f: (unit -> 'T) -> 'T

/// Project-cracking logic: evaluates the fsdocs-specific MSBuild properties of each project
/// with `dotnet msbuild --getProperty`, and resolves the compiler references with a design-time
/// build (`dotnet msbuild -t:...CoreCompile -getItem:FscCommandLineArgs`) only for the projects
/// that take part in the API docs, and only when they are needed.
module Crack =
    [<return: Struct>]
    val (|ConditionEquals|_|): str: string -> arg: string -> unit voption

    /// All fsdocs-relevant MSBuild properties of a single project, read by evaluating the project
    /// (no design-time build).
    type CrackedProjectInfo =
        {
            ProjectFileName: string
            TargetPath: string option
            /// The target frameworks of a multi-targeting project, empty otherwise
            TargetFrameworks: string list
            IsTestProject: bool
            IsLibrary: bool
            IsPackable: bool
            RepositoryUrl: string option
            RepositoryType: string option
            RepositoryBranch: string option
            UsesMarkdownComments: bool
            FsDocsLicenseLink: string option
            FsDocsLogoLink: string option
            FsDocsLogoSource: string option
            FsDocsLogoAlt: string option
            FsDocsReleaseNotesLink: string option
            FsDocsSourceFolder: string option
            FsDocsSourceRepository: string option
            FsDocsFaviconSource: string option
            FsDocsTheme: string option
            FsDocsWarnOnMissingDocs: bool
            FsDocsGenerateLlmsTxt: bool
            FsDocsAllowExecutableProject: bool
            FsDocsNoInheritedMembers: bool
            FsDocsTypeConstraints: FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode
            PackageProjectUrl: string option
            Authors: string option
            GenerateDocumentationFile: bool
            //Removed because this is typically a multi-line string and dotnet-proj-info can't handle this
            //Description : string option
            PackageLicenseExpression: string option
            PackageTags: string option
            Copyright: string option
            PackageVersion: string option
            PackageIconUrl: string option
            RepositoryCommit: string option
        }

    /// The result of the design-time build of one project.
    type DesignTimeBuild =
        {
            /// The compiler options, including the '-r:' references
            OtherOptions: string list
            /// The fsdocs properties as they are after the targets ran; properties set by targets
            /// (such as a version computed from a changelog) are only correct here
            Properties: (string * string) list
        }

    /// Run the design-time build of the given projects (in parallel) and return the result per
    /// project file; projects whose build fails are reported and left out.
    val resolveCompilerOptions:
        extraMsbuildProperties: (string * string) list ->
        projects: (string * string list) list ->
            Map<string, DesignTimeBuild>

    /// The project info as the design-time build saw it: the same properties, but with the values
    /// set by MSBuild targets. The evaluated value is kept where the build reported none.
    val refineProjectInfo: info: CrackedProjectInfo -> build: DesignTimeBuild -> CrackedProjectInfo

    /// Discovers project files (from solutions, directories, or explicit lists),
    /// cracks each one, and returns the collection name, collection URL, and per-project info.
    /// A project whose API documentation is generated, with the settings read from the project file.
    /// The compiler references are not part of it: see resolveCompilerOptions.
    type CrackedProject =
        {
            ProjectFileName: string
            TargetPath: string
            TargetFrameworks: string list
            RepositoryUrl: string option
            RepositoryBranch: string option
            RepositoryType: string option
            UsesMarkdownComments: bool
            WarnOnMissingDocs: bool
            SourceFolder: string option
            SourceRepository: string option
            NoInheritedMembers: bool
            TypeConstraints: FSharp.Formatting.ApiDocs.TypeConstraintDisplayMode
            Substitutions: (ParamKey * string) list
        }

    /// Find the project files to document: the projects given explicitly, else the solution in the
    /// current folder, else the project files up to two folders deep. Returns the collection name too.
    val discoverProjects:
        userCollectionName: string option -> projects: string list -> ignoreProjects: bool -> string * string list

    /// Evaluate the discovered projects and keep the documentable ones. No design-time build.
    val evaluateProjects:
        onError: (string -> unit) *
        extraMsbuildProperties: (string * string) list *
        projectFiles: string list *
        ignoreProjects: bool ->
            CrackedProjectInfo list

    /// The site-wide settings and the per-project substitutions of the given projects: the root URL,
    /// the documented projects, the folders holding their DLLs, the substitutions of the content
    /// pages and whether llms.txt is generated. Missing settings are only reported when 'warnMissing'
    /// is set, so a recomputation after a design-time build stays quiet.
    val siteOf:
        userRoot: string option *
        userParameters: (ParamKey * string) list *
        collectionName: string *
        projectInfos: CrackedProjectInfo list *
        warnMissing: bool ->
            string * CrackedProject list * string list * (ParamKey * string) list * bool
