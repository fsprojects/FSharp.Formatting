namespace FSharp.Formatting.Literate

open FSharp.Formatting.Markdown
open FSharp.Formatting.CodeFormat
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Special paragraphs used in literate documents
// --------------------------------------------------------------------------------------

/// Specifies visibility of a code snippet.
[<RequireQualifiedAccess>]
type LiterateCodeVisibility =
    /// Ordinary visible code
    | VisibleCode
    /// Hidden snippet
    | HiddenCode
    /// Named snippet with captured output
    | NamedCode of string

/// Specifies the options for a literate paragraph
type LiterateParagraphOptions =
    {
        /// Specifies a conditional for inclusion of the snippet paragraph
        Condition: string option
    }

/// <summary>
/// Additional properties of a literate code snippet, embedded in a
/// <c>LiterateParagraph.LiterateCode</c>. The properties specify how should
/// a snippet be evaluated and formatted.
/// </summary>
type LiterateCodeOptions =
    {
        /// <summary>
        /// Specifies whether the snippet is evalauted while processing
        /// Use (*** do-not-eval ***) command to set this to <c>false</c>
        /// </summary>
        Evaluate: bool

        /// Specifies the name of the output produced by this snippet
        /// Use the (*** define-output:foo ***) command to set this value
        /// Other outputs are named cell1, cell2 etc.
        OutputName: string

        /// Indiciates the execution sequence number of the cell if it has been evaluated
        ExecutionCount: int option

        /// Specifies the visibility of the snippet in the generated HTML
        Visibility: LiterateCodeVisibility
    }

/// <summary>
/// Extends <c>MarkdownParagrap</c> using the <c>MarkdownEmbedParagraphs</c> case with
/// additional kinds of paragraphs that can appear in literate F# scripts
/// (such as various special commands to embed output of a snippet etc.)
/// </summary>
type LiterateParagraph =
    /// (*** include:foo ***) - Include formatted snippet from other part of the document here
    | CodeReference of string * LiterateParagraphOptions

    /// (*** include-fsi-output ***) - Include output from previous snippet
    /// (*** include-fsi-output:foo ***) - Include output from a named snippet
    | FsiOutputReference of string * LiterateParagraphOptions

    /// (*** include-fsi-merged-output ***) - Include output from previous snippet
    /// (*** include-fsi-merged-output:foo ***) - Include output from a named snippet
    | FsiMergedOutputReference of string * LiterateParagraphOptions

    /// (*** include-fsi-output ***) - Include F# Interactive output from previous snippet
    /// (*** include-fsi-output:foo ***) - Include F# Interactive from a named snippet
    | OutputReference of string * LiterateParagraphOptions

    /// (*** include-it ***) - Include "it" value from the subsequent snippet here
    /// (*** include-it:foo ***) - Include "it" value from a named snippet
    | ItValueReference of string * LiterateParagraphOptions

    /// (*** include-it-raw ***) - Include "it" value from the subsequent snippet here as raw text (Not formatted as fsi)
    /// (*** include-it-raw:foo ***) - Include "it" value from a named snippet as raw text (Not formatted as fsi)
    | ItRawReference of string * LiterateParagraphOptions

    /// (*** include-value:foo ***) - Include the formatting of a specified value here
    | ValueReference of string * LiterateParagraphOptions

    /// Emebdded literate code snippet. Consists of source lines and options
    | LiterateCode of Line list * LiterateCodeOptions * LiterateParagraphOptions

    /// Ordinary formatted code snippet in non-F# language (tagged with language code)
    | LanguageTaggedCode of string * string * LiterateParagraphOptions

    /// Block simply emitted without any formatting equivalent to <pre> tag in html
    | RawBlock of Line list * LiterateParagraphOptions

    member x.ParagraphOptions =
        match x with
        | CodeReference(_, popts) -> popts
        | FsiMergedOutputReference(_, popts) -> popts
        | FsiOutputReference(_, popts) -> popts
        | OutputReference(_, popts) -> popts
        | ItValueReference(_, popts) -> popts
        | ItRawReference(_, popts) -> popts
        | ValueReference(_, popts) -> popts
        | LiterateCode(_, _, popts) -> popts
        | LanguageTaggedCode(_, _, popts) -> popts
        | RawBlock(_, popts) -> popts

    interface MarkdownEmbedParagraphs with
        member x.Render() =
            failwith "LiterateParagraph elements cannot be directly formatted"

/// Represents the source of a literate document.
[<RequireQualifiedAccess>]
type LiterateSource =
    /// A markdown source
    | Markdown of string

    /// A parsed F# script file consisting of snippets.
    | Script of Snippet[]

/// Representation of a literate document - the representation of Paragraphs
/// uses an F# discriminated union type and so is best used from F#.
type LiterateDocument(paragraphs, formattedTips, links, source, sourceFile, rootInputFolder, diagnostics) =
    // Get the content of the document as a structurally comparable tuple
    let asTuple (doc: LiterateDocument) =
        List.ofSeq doc.DefinedLinks.Keys, List.ofSeq doc.DefinedLinks.Values, List.ofSeq doc.Diagnostics, doc.Paragraphs

    /// Returns a list of paragraphs in the document
    member _.Paragraphs: MarkdownParagraphs = paragraphs

    /// Returns a dictionary containing explicitly defined links
    member _.DefinedLinks: IDictionary<string, string * option<string>> = links

    /// Errors
    member _.Diagnostics: SourceError[] = diagnostics

    /// Original document source code
    member _.Source: LiterateSource = source

    /// Location where the file was loaded from
    member _.SourceFile: string = sourceFile

    /// Root for computing relative paths
    member _.RootInputFolder: string option = rootInputFolder

    /// Formatted tool tips
    member _.FormattedTips: string = formattedTips

    /// Return as markdown document, throwing away additional stuff
    member _.MarkdownDocument = MarkdownDocument(paragraphs, links)

    // Implement equality & get hash code for testing purposes
    override x.Equals(other) =
        match other with
        | :? LiterateDocument as other -> (asTuple x).Equals(asTuple other)
        | _ -> false

    override x.GetHashCode() = (asTuple x).GetHashCode()

    /// Clone the document and change some of its properties
    member x.With(?paragraphs, ?formattedTips, ?definedLinks, ?source, ?sourceFile, ?rootInputFolder, ?diagnostics) =
        LiterateDocument(
            defaultArg paragraphs x.Paragraphs,
            defaultArg formattedTips x.FormattedTips,
            defaultArg definedLinks x.DefinedLinks,
            defaultArg source x.Source,
            defaultArg sourceFile x.SourceFile,
            defaultArg rootInputFolder x.RootInputFolder,
            defaultArg diagnostics x.Diagnostics
        )

// --------------------------------------------------------------------------------------
// Pattern matching helpers
// --------------------------------------------------------------------------------------

/// <summary>
/// Provides active patterns for extracting <c>LiterateParagraph</c> values from
/// Markdown documents.
/// </summary>
module MarkdownPatterns =
    let (|LiterateParagraph|_|) =
        function
        | EmbedParagraphs(:? LiterateParagraph as lp, _) -> Some lp
        | _ -> None
