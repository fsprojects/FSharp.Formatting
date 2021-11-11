// --------------------------------------------------------------------------------------
// F# CodeFormat (Main.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.Formatting.CodeFormat

open System
open System.Diagnostics

/// <summary>
///  Represents an individual formatted snippet with title as key
/// </summary>
///
/// <namespacedoc>
///   <summary>Functionality relating to formatting F# scripts and code snippets</summary>
/// </namespacedoc>
type FormattedSnippet(key: string, content: string) =

    /// Returns the title of the snippet
    member x.Title = key

    /// Returns the key of the snippet
    member x.Key = key

    /// Returns the formatted content code for the snipet
    member x.Content = content


/// Represents formatted snippets
type FormattedContent(snippets: FormattedSnippet [], tips: string) =
    /// Returns the processed snippets as an array
    member x.Snippets = snippets

    /// Returns string with ToolTip elements for all the snippets
    member x.ToolTip = tips

/// Exposes functionality of the F# code formatter with a nice interface
type CodeFormat =

    /// Formats the .fsx snippets as HTML. The parameters specify prefix for HTML tags, whether lines should
    /// be added to outputs and whether errors should be printed.
    static member FormatHtml
        (
            snippets,
            prefix,
            ?openTag,
            ?closeTag,
            ?lineNumbers,
            ?openLinesTag,
            ?closeLinesTag,
            ?addErrors,
            ?tokenKindToCss
        ) =
        let openTag = defaultArg openTag "<pre class=\"fssnip\">"

        let closeTag = defaultArg closeTag "</pre>"
        let openLinesTag = defaultArg openLinesTag openTag
        let closeLinesTag = defaultArg closeLinesTag closeTag
        let lineNumbers = defaultArg lineNumbers true
        let addErrors = defaultArg addErrors false
        let tokenKindToCss = defaultArg tokenKindToCss CodeFormatHelper.defaultTokenMap

        let snip, tip =
            Html.formatSnippetsAsHtml
                lineNumbers
                addErrors
                prefix
                openTag
                closeTag
                openLinesTag
                closeLinesTag
                tokenKindToCss
                snippets

        let snip = [| for key, h in snip -> FormattedSnippet(key, h) |]

        FormattedContent(snip, tip)

    /// Formats the .fsx snippets as LaTeX. The parameters specify prefix for LaTeX tags, whether lines should
    /// be added to outputs.
    static member FormatLatex(snippets, ?openTag, ?closeTag, ?lineNumbers) =
        let lineNumbers = defaultArg lineNumbers true
        let openTag = defaultArg openTag @"\begin{Verbatim}"
        let closeTag = defaultArg closeTag @"\end{Verbatim}"

        let snips = Latex.formatSnippetsAsLatex lineNumbers openTag closeTag snippets

        let snips = Array.map FormattedSnippet snips
        FormattedContent(snips, "")

    /// Formats the .fsx snippets as iPython notebook using the default settings.
    static member FormatFsx(snippets) =
        let snips =
            [| for (Snippet (key, lines)) in snippets do
                   let str =
                       [| for (Line (originalLine, _spans)) in lines -> originalLine |]
                       |> String.concat Environment.NewLine

                   yield key, str |]

        let snips = Array.map FormattedSnippet snips
        FormattedContent(snips, "")
