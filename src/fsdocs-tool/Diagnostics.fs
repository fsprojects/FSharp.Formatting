namespace fsdocs

open System
open System.IO
open System.Reflection
open FSharp.Formatting.Templating
open FSharp.Formatting.ApiDocs

/// A command line option without effect for the current command.
type IgnoredOption = { Option: string; Reason: string }

/// The result of cracking one project, as far as documentation generation is concerned.
type internal ProjectDiagnostics =
    {
        ProjectFile: string
        TargetPath: string
        TargetExists: bool
        /// Substitutions of this project that differ from the site-wide ones
        OverridingSubstitutions: (string * string) list
    }

/// Where a substitution value came from.
[<Struct>]
type internal SubstitutionSource =
    | Project
    | Parameters
    | WatchOverride

type internal SubstitutionDiagnostics =
    {
        Key: string
        Value: string
        Source: SubstitutionSource
    }

/// How a template or folder was resolved: the paths tried, in order, and the one chosen.
type ResolutionDiagnostics =
    {
        Tried: string list
        Chosen: string option
        Note: string option
    }

/// The compiler references of one project, from its design-time build.
type internal ProjectReferences =
    {
        ProjectFile: string
        /// References that exist on disk and are passed to the F# compiler
        References: string list
        /// References dropped because they were not on disk
        DroppedReferences: string list
    }

/// Everything derived from the project files: the site-wide substitutions, the inputs of the API
/// docs and the diagnostics about the projects. Recomputed by 'watch' when a project file changes.
[<ReferenceEquality>]
type internal CrackResult =
    {
        Substitutions: Substitutions
        SubstitutionDiagnostics: SubstitutionDiagnostics list
        ApiDocInputs: ApiDocInput list
        /// The compiler flags for the API docs: references and user options
        ApiDocOtherFlags: string list
        /// Folders where referenced DLLs are found
        LibDirs: string list
        Projects: ProjectDiagnostics list
        References: ProjectReferences list
    }

/// Everything known about a build or watch session before any page is generated.
/// The console output and the doctor endpoints are both rendered from this value.
type internal Diagnostics =
    {
        ToolVersion: string
        CommandLine: string
        Command: string
        Input: string
        /// The output folder, None for 'watch'
        Output: string option
        Root: string
        CollectionName: string
        GenerateLlmsTxt: bool
        IgnoredOptions: IgnoredOption list
        Projects: ProjectDiagnostics list
        Substitutions: SubstitutionDiagnostics list
        /// The default '_template.html' shipped with the tool
        DefaultTemplate: ResolutionDiagnostics
        /// The default '_template.md' shipped with the tool
        DefaultMarkdownTemplate: ResolutionDiagnostics
        /// The template used for API reference pages, with the output kind it implies
        ApiDocsTemplate: ResolutionDiagnostics
        ApiDocsOutputKind: string
        /// The 'extras' folder copied into the site root
        Extras: ResolutionDiagnostics
        HeadTemplate: string option
        BodyTemplate: string option
        MenuTemplatesFound: bool
    }

module internal Diagnostics =

    let toolVersion () =
        let assembly = typeof<Diagnostics>.Assembly

        match assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
        | null -> string<Version>(assembly.GetName().Version)
        | attr -> attr.InformationalVersion

    let commandLine () =
        Environment.GetCommandLineArgs() |> Array.skip 1 |> String.concat " "

    let private fileExists (path: string) =
        try
            File.Exists path
        with _ ->
            false

    /// Choose the first existing path, remembering all candidates.
    let resolveFile (candidates: string list) : ResolutionDiagnostics =
        {
            Tried = candidates
            Chosen = candidates |> List.tryFind fileExists
            Note = None
        }

    /// Choose the first existing directory, remembering all candidates.
    let resolveDirectory (candidates: string list) : ResolutionDiagnostics =
        {
            Tried = candidates
            Chosen =
                candidates
                |> List.tryFind (fun d ->
                    try
                        Directory.Exists d
                    with _ ->
                        false)
            Note = None
        }

    /// Classify the site-wide substitutions by origin.
    let substitutions
        (docsSubstitutions: Substitutions)
        (userParameters: Substitutions)
        (watchOverrides: ParamKey list)
        : SubstitutionDiagnostics list =
        let user = set userParameters

        [
            for (ParamKey key as pk, value) in docsSubstitutions ->
                let source =
                    if List.contains pk watchOverrides then WatchOverride
                    elif user.Contains(pk, value) then Parameters
                    else Project

                {
                    Key = key
                    Value = value
                    Source = source
                }
        ]

    /// Log the session: a one-line summary at information level, the details at debug level.
    let log (d: Diagnostics) =
        if not d.Projects.IsEmpty then
            logger.Infof
                "%d projects for the API docs, %d substitutions (use --verbosity detailed to list them)"
                d.Projects.Length
                d.Substitutions.Length

            logger.Debugf "Inputs for API Docs:"

            for p in d.Projects do
                logger.Debugf "    %s" p.TargetPath

            logger.Debugf "Substitutions/parameters:"

            for s in d.Substitutions do
                logger.Debugf "  %s --> %s" s.Key s.Value

            for p in d.Projects do
                for (key, value) in p.OverridingSubstitutions do
                    logger.Debugf "  (%s) %s --> %s" (Path.GetFileNameWithoutExtension(p.TargetPath)) key value

        match d.Extras.Chosen, d.Extras.Tried with
        | Some chosen, _ -> logger.Debugf "using extra content from %s" chosen
        | None, [ attempt1; attempt2 ] -> logger.Warnf "no extra content found at %s or %s" attempt1 attempt2
        | None, _ -> ()

    let logIgnoredOptions (d: Diagnostics) =
        for o in d.IgnoredOptions do
            logger.Warnf "ignoring --%s: %s" o.Option o.Reason
