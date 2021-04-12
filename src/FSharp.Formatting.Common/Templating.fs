namespace FSharp.Formatting.Templating

open System
open System.Collections.Generic
open System.IO
open System.Text

/// <summary>
/// A parameter key
/// </summary>
///
/// <namespacedoc>
///   <summary>Functionality relating to templating (mostly internal)</summary>
/// </namespacedoc>
[<Struct>]
type ParamKey = ParamKey of string
with
    override x.ToString() =
        match x with ParamKey x -> x

/// A list of parameters for substituting in templates, indexed by parameter keys
type Substitutions = (ParamKey * string) list

/// <summary>
///  Defines the parameter keys known to FSharp.Formatting processing code
/// </summary>
[<RequireQualifiedAccess>]
module ParamKeys =

    /// A parameter key known to FSharp.Formatting
    let ``root`` = ParamKey "root"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-authors`` = ParamKey "fsdocs-authors"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-collection-name`` = ParamKey "fsdocs-collection-name"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-content`` = ParamKey "fsdocs-content"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-collection-name-link`` = ParamKey "fsdocs-collection-name-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-copyright`` = ParamKey "fsdocs-copyright"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-license-link`` = ParamKey "fsdocs-license-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-list-of-namespaces`` = ParamKey "fsdocs-list-of-namespaces"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-list-of-documents`` = ParamKey "fsdocs-list-of-documents"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-logo-link`` = ParamKey "fsdocs-logo-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-logo-src`` = ParamKey "fsdocs-logo-src"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-navbar-position`` = ParamKey "fsdocs-navbar-position"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-license-expression`` = ParamKey "fsdocs-package-license-expression"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-project-url`` = ParamKey "fsdocs-package-project-url"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-tags`` = ParamKey "fsdocs-package-tags"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-version`` = ParamKey "fsdocs-package-version"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-icon-url`` = ParamKey "fsdocs-package-icon-url"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-page-title`` = ParamKey "fsdocs-page-title"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-page-source`` = ParamKey "fsdocs-page-source"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-release-notes-link`` = ParamKey "fsdocs-release-notes-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-branch`` = ParamKey "fsdocs-repository-branch"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-commit`` = ParamKey "fsdocs-repository-commit"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-link`` = ParamKey "fsdocs-repository-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-source`` = ParamKey "fsdocs-source"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-source-filename`` = ParamKey "fsdocs-source-filename"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-source-basename`` = ParamKey "fsdocs-source-basename"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-theme`` = ParamKey "fsdocs-theme"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-tooltips`` = ParamKey "fsdocs-tooltips"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-watch-script`` = ParamKey "fsdocs-watch-script"

module internal SimpleTemplating =

#if NETSTANDARD2_0
    type StringBuilder with
        member this.Append(span: ReadOnlySpan<char>) =
            this.Append(span.ToString())
#endif

    // Replace '{{xyz}}' in template text
    let ApplySubstitutionsInText (substitutions: seq<ParamKey * string>) (text: string) =
      if not (text.Contains "{{") then text
      else
        let substitutions = readOnlyDict substitutions
        let sb = StringBuilder(text.Length)
        let mutable span = text.AsSpan()
        while not span.IsEmpty do
            // We try to find the first double curly bracket.
            match span.IndexOf("{{".AsSpan(), StringComparison.Ordinal) with
            | -1 ->
                // If it's not found, there are no more tags in the template.
                // We simply append all the remaining text.
                sb.Append(span) |> ignore
                span <- ReadOnlySpan.Empty
            | curlyBraceBegin ->
                // If we found two beginning curly brackets,
                // we first append all the text before and
                // then advance our span until just after them.
                sb.Append(span.Slice(0, curlyBraceBegin)) |> ignore
                span <- span.Slice(curlyBraceBegin + "{{".Length)
                // Now we try to find the first double ending curly
                // bracket after the beginning ones we previously found.
                match span.IndexOf("}}".AsSpan(), StringComparison.Ordinal) with
                | -1 ->
                    // If the whole tag had not been closed, we add the beginning
                    // double curly brackets we had previously discarded and then
                    // add the rest of the text.
                    sb.Append("{{").Append(span) |> ignore
                    span <- ReadOnlySpan.Empty
                | curlyBraceEnd ->
                    // Otherwise we extract the tag's
                    // content, i.e. the parameter key.
                    let key = span.Slice(0, curlyBraceEnd).ToString()
                    match substitutions.TryGetValue(ParamKey key) with
                    | true, value ->
                        sb.Append(value) |> ignore
                    | false, _ ->
                        sb.Append("{{").Append(key).Append("}}") |> ignore
                    span <- span.Slice(curlyBraceEnd + "}}".Length)
        sb.ToString()

    // Replace '{{xyz}}' in text
    let ApplySubstitutions (substitutions: seq<ParamKey * string>) templateTextOpt =
        match templateTextOpt with
        | None | Some "" ->
            // If there is no template or the template is an empty file, return just document + tooltips (tooltips empty if not HTML)
            let lookup = readOnlyDict substitutions
            (if lookup.ContainsKey ParamKeys.``fsdocs-content`` then lookup.[ParamKeys.``fsdocs-content``] else "") +
            (if lookup.ContainsKey ParamKeys.``fsdocs-tooltips`` then "\n\n" + lookup.[ParamKeys.``fsdocs-tooltips``] else "")
        | Some templateText -> ApplySubstitutionsInText substitutions templateText

    let UseFileAsSimpleTemplate (substitutions, templateOpt, outputFile) =
        let templateTextOpt = templateOpt |> Option.map System.IO.File.ReadAllText
        let outputText = ApplySubstitutions substitutions templateTextOpt
        try
            let path = Path.GetFullPath(outputFile) |> Path.GetDirectoryName
            Directory.CreateDirectory(path) |> ignore
        with _ -> ()
        File.WriteAllText(outputFile, outputText)
