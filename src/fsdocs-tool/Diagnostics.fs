namespace fsdocs

open System
open System.IO
open System.Reflection
open FSharp.Formatting.Templating

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
        /// References passed to the F# compiler when generating API docs
        References: string list
        /// References dropped because they were not on disk
        DroppedReferences: string list
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

    /// The 'Inputs for API Docs' block of the console output.
    let printInputs (d: Diagnostics) =
        if not d.Projects.IsEmpty then
            printfn ""
            printfn "Inputs for API Docs:"

            for p in d.Projects do
                printfn "    %s" p.TargetPath

    /// The 'Substitutions/parameters' block of the console output.
    let printSubstitutions (d: Diagnostics) =
        if not d.Projects.IsEmpty then
            printfn ""
            printfn "Substitutions/parameters:"

            for s in d.Substitutions do
                printfn "  %s --> %s" s.Key s.Value

            for p in d.Projects do
                for (key, value) in p.OverridingSubstitutions do
                    printfn "  (%s) %s --> %s" (Path.GetFileNameWithoutExtension(p.TargetPath)) key value

    let printDroppedReferences (d: Diagnostics) =
        for p in d.Projects do
            for r in p.DroppedReferences do
                printfn "NOTE: the reference '%s' was not seen on disk, ignoring" r

    let printExtras (d: Diagnostics) =
        match d.Extras.Chosen, d.Extras.Tried with
        | Some chosen, _ -> printfn "using extra content from %s" chosen
        | None, [ attempt1; attempt2 ] -> printfn "no extra content found at %s or %s" attempt1 attempt2
        | None, _ -> ()

    let printIgnoredOptions (d: Diagnostics) =
        for o in d.IgnoredOptions do
            printfn "note, ignoring --%s: %s" o.Option o.Reason
