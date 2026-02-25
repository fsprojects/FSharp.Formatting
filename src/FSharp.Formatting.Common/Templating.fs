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
type ParamKey =
    | ParamKey of string

    override x.ToString() =
        match x with
        | ParamKey x -> x

/// A list of parameters for substituting in templates, indexed by parameter keys
type Substitutions = (ParamKey * string) list

/// Meta data from files that contains front matter
/// Used to determine upfront which files have front matter so that previous and next substitutes can be discovered.
type FrontMatterFile =
    { FileName: string
      Category: string
      CategoryIndex: int
      Index: int }

    /// Parses the category, categoryindex and index from the frontmatter lines
    static member ParseFromLines (fileName: string) (lines: string seq) =
        let (|ValidIndex|_|) (value: string) =
            match Int32.TryParse value with
            | true, i -> Some i
            | false, _ -> None

        let keyValues =
            lines
            // Skip opening lines
            |> Seq.skipWhile (fun line ->
                let line = line.Trim()
                line = "(**" || line = "---")
            |> Seq.takeWhile (fun line ->
                // Allow empty lines in frontmatter
                let isBlankLine = String.IsNullOrWhiteSpace line
                isBlankLine || line.Contains(":"))
            |> Seq.choose (fun line ->
                if String.IsNullOrWhiteSpace line |> not then
                    let parts = line.Split(":") |> Array.toList

                    match parts with
                    | first :: second :: _ -> Some(first.ToLowerInvariant(), second)
                    | _ -> None
                else
                    None)
            |> Map.ofSeq

        match
            Map.tryFind "category" keyValues, Map.tryFind "categoryindex" keyValues, Map.tryFind "index" keyValues
        with
        | Some category, Some(ValidIndex categoryindex), Some(ValidIndex index) ->
            Some
                { FileName = fileName
                  Category = category.Trim()
                  CategoryIndex = categoryindex
                  Index = index }
        | _ -> None

/// <summary>
///  Defines the parameter keys known to FSharp.Formatting processing code
/// </summary>
[<RequireQualifiedAccess>]
module ParamKeys =

    /// A parameter key known to FSharp.Formatting
    let root = ParamKey "root"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-authors`` = ParamKey "fsdocs-authors"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-collection-name`` = ParamKey "fsdocs-collection-name"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-content`` = ParamKey "fsdocs-content"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-page-content-list`` = ParamKey "fsdocs-page-content-list"

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
    let ``fsdocs-logo-alt`` = ParamKey "fsdocs-logo-alt"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-favicon-src`` = ParamKey "fsdocs-favicon-src"

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

    /// A parameter key known to FSharp.Formatting, available in _menu_template.html
    let ``fsdocs-menu-header-content`` = ParamKey "fsdocs-menu-header-content"

    /// A parameter key known to FSharp.Formatting, available in _menu_template.html
    let ``fsdocs-menu-header-id`` = ParamKey "fsdocs-menu-header-id"

    /// A parameter key known to FSharp.Formatting, available in _menu_template.html
    /// This will be an empty string if the category is not active.
    let ``fsdocs-menu-header-active-class`` = ParamKey "fsdocs-menu-header-active-class"

    /// A parameter key known to FSharp.Formatting, available in _menu_template.html
    let ``fsdocs-menu-items`` = ParamKey "fsdocs-menu-items"

    /// A parameter key known to FSharp.Formatting, available in _menu-item_template.html
    let ``fsdocs-menu-item-link`` = ParamKey "fsdocs-menu-item-link"

    /// A parameter key known to FSharp.Formatting, available in _menu-item_template.html
    let ``fsdocs-menu-item-content`` = ParamKey "fsdocs-menu-item-content"

    /// A parameter key known to FSharp.Formatting, available in _menu-item_template.html
    let ``fsdocs-menu-item-id`` = ParamKey "fsdocs-menu-item-id"

    /// A parameter key known to FSharp.Formatting, available in _menu-item_template.html
    /// /// This will be an empty string if the item is not active.
    let ``fsdocs-menu-item-active-class`` = ParamKey "fsdocs-menu-item-active-class"

    /// A parameter key known to FSharp.Formatting, available when frontmatter is used correctly
    let ``fsdocs-previous-page-link`` = ParamKey "fsdocs-previous-page-link"

    /// A parameter key known to FSharp.Formatting, available when frontmatter is used correctly
    let ``fsdocs-next-page-link`` = ParamKey "fsdocs-next-page-link"

    /// A parameter key known to FSharp.Formatting, available when `_head.html` exists in the input folder.
    let ``fsdocs-head-extra`` = ParamKey "fsdocs-head-extra"

    /// A parameter key known to FSharp.Formatting, available when `_head.html` exists in the input folder.
    let ``fsdocs-body-extra`` = ParamKey "fsdocs-body-extra"

    /// A parameter key known to FSharp.Formatting, either 'content' or 'api-doc'
    /// Mean to be used on the `class` attribute in the `<body>` tag.
    /// This helps to differentiate styles between API docs and custom content.
    let ``fsdocs-body-class`` = ParamKey "fsdocs-body-class"

    /// A parameter key known to FSharp.Formatting, it is HTML composed from additional frontmatter information.
    /// Such as tags and description
    /// This can be empty when both properties are not provided for the current page.
    let ``fsdocs-meta-tags`` = ParamKey "fsdocs-meta-tags"

module internal SimpleTemplating =

#if NETSTANDARD2_0
    type StringBuilder with

        member this.Append(span: ReadOnlySpan<char>) = this.Append(span.ToString())
#endif

    // Replace '{{xyz}}' in template text
    let ApplySubstitutionsInText (substitutions: (ParamKey * string) seq) (text: string) =
        if not (text.Contains "{{") then
            text
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
                        | true, value -> sb.Append(value) |> ignore
                        | false, _ -> sb.Append("{{").Append(key).Append("}}") |> ignore

                        span <- span.Slice(curlyBraceEnd + "}}".Length)

            sb.ToString()

    /// Replace '{{xyz}}' in text
    let ApplySubstitutions (substitutions: (ParamKey * string) seq) (templateTextOpt: string option) =
        let opt =
            templateTextOpt
            |> Option.bind (fun s ->
                let trimmed = s.Trim()

                if String.IsNullOrWhiteSpace trimmed then
                    None
                else
                    Some trimmed)

        match opt with
        | None ->
            // If there is no template or the template is an empty file, return just document + tooltips (tooltips empty if not HTML)
            let lookup = readOnlyDict substitutions

            (match lookup.TryGetValue ParamKeys.``fsdocs-content`` with
             | true, lookupContent -> lookupContent
             | false, _ -> "")
            + (match lookup.TryGetValue ParamKeys.``fsdocs-tooltips`` with
               | true, lookupTips -> "\n\n" + lookupTips
               | false, _ -> "")
        | Some templateText -> ApplySubstitutionsInText substitutions templateText

    let UseFileAsSimpleTemplate (substitutions, templateOpt, outputFile) =
        let templateTextOpt = templateOpt |> Option.map System.IO.File.ReadAllText

        let outputText = ApplySubstitutions substitutions templateTextOpt

        try
            let path = Path.GetFullPath(outputFile) |> Path.GetDirectoryName

            Directory.CreateDirectory(path) |> ignore
        with _ ->
            ()

        File.WriteAllText(outputFile, outputText)
