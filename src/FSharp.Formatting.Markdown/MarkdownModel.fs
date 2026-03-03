// --------------------------------------------------------------------------------------
// F# Markdown (Markdown.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace rec FSharp.Formatting.Markdown

open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Definition of the Markdown format
// --------------------------------------------------------------------------------------

/// <summary>
///   A list kind can be Ordered or Unordered corresponding to <c>&lt;ol&gt;</c> and <c>&lt;ul&gt;</c> elements
/// </summary>
[<Struct>]
type MarkdownListKind =
    | Ordered
    | Unordered

/// Column in a table can be aligned to left, right, center or using the default alignment
[<Struct>]
type MarkdownColumnAlignment =
    | AlignLeft
    | AlignRight
    | AlignCenter
    | AlignDefault

/// Represents inline formatting inside a paragraph. This can be literal (with text), various
/// formattings (string, emphasis, etc.), hyperlinks, images, inline maths etc.
type MarkdownSpan =
    | Literal of text: string * range: MarkdownRange
    | InlineCode of code: string * range: MarkdownRange
    | Strong of body: MarkdownSpans * range: MarkdownRange
    | Emphasis of body: MarkdownSpans * range: MarkdownRange
    | AnchorLink of link: string * range: MarkdownRange
    | DirectLink of body: MarkdownSpans * link: string * title: string option * range: MarkdownRange
    | IndirectLink of body: MarkdownSpans * original: string * key: string * range: MarkdownRange
    | DirectImage of body: string * link: string * title: string option * range: MarkdownRange
    | IndirectImage of body: string * link: string * key: string * range: MarkdownRange
    | HardLineBreak of range: MarkdownRange
    | LatexInlineMath of code: string * range: MarkdownRange
    | LatexDisplayMath of code: string * range: MarkdownRange
    | EmbedSpans of customSpans: MarkdownEmbedSpans * range: MarkdownRange

/// A type alias for a list of MarkdownSpan values
type MarkdownSpans = MarkdownSpan list

/// Provides an extensibility point for adding custom kinds of spans into a document
/// (MarkdownEmbedSpans values can be embedded using MarkdownSpan.EmbedSpans)
type MarkdownEmbedSpans =
    abstract Render: unit -> MarkdownSpans

/// A paragraph represents a (possibly) multi-line element of a Markdown document.
/// Paragraphs are headings, inline paragraphs, code blocks, lists, quotations, tables and
/// also embedded LaTeX blocks.
type MarkdownParagraph =
    | Heading of size: int * body: MarkdownSpans * range: MarkdownRange
    | Paragraph of body: MarkdownSpans * range: MarkdownRange

    /// A code block, whether fenced or via indentation
    | CodeBlock of
        code: string *
        executionCount: int option *
        fence: string option *
        language: string *
        ignoredLine: string *
        range: MarkdownRange

    /// A HTML block
    | InlineHtmlBlock of code: string * executionCount: int option * range: MarkdownRange

    /// A Markdown List block
    | ListBlock of kind: MarkdownListKind * items: MarkdownParagraphs list * range: MarkdownRange

    /// A Markdown Quote block
    | QuotedBlock of paragraphs: MarkdownParagraphs * range: MarkdownRange

    /// A Markdown Span block
    | Span of body: MarkdownSpans * range: MarkdownRange

    /// A Markdown Latex block
    | LatexBlock of env: string * body: string list * range: MarkdownRange

    /// A Markdown Horizontal rule
    | HorizontalRule of character: char * range: MarkdownRange

    /// A Markdown Table
    | TableBlock of
        headers: MarkdownTableRow option *
        alignments: MarkdownColumnAlignment list *
        rows: MarkdownTableRow list *
        range: MarkdownRange

    /// Represents a block of markdown produced when parsing of code or tables or quoted blocks is suppressed
    | OtherBlock of lines: (string * MarkdownRange) list * range: MarkdownRange

    /// A special addition for computing paragraphs
    | EmbedParagraphs of customParagraphs: MarkdownEmbedParagraphs * range: MarkdownRange

    /// A special addition for YAML-style frontmatter
    | YamlFrontmatter of yaml: string list * range: MarkdownRange

    /// A special addition for inserted outputs
    | OutputBlock of output: string * kind: string * executionCount: int option

/// A type alias for a list of paragraphs
type MarkdownParagraphs = MarkdownParagraph list

/// A type alias representing table row as a list of paragraphs
type MarkdownTableRow = MarkdownParagraphs list

/// Provides an extensibility point for adding custom kinds of paragraphs into a document
/// (MarkdownEmbedParagraphs values can be embedded using MarkdownParagraph.EmbedParagraphs)
type MarkdownEmbedParagraphs =
    abstract Render: unit -> MarkdownParagraphs

/// <summary>
/// Concise F# DSL operators for constructing <see cref="MarkdownParagraph"/> and <see cref="MarkdownSpan"/> values.
/// Useful for building Markdown ASTs programmatically without specifying range information.
/// </summary>
module Dsl =
    /// Creates an H1 heading paragraph
    let ``#`` value = Heading(1, value, MarkdownRange.zero)
    /// Creates an H2 heading paragraph
    let ``##`` value = Heading(2, value, MarkdownRange.zero)
    /// Creates an H3 heading paragraph
    let ``###`` value = Heading(3, value, MarkdownRange.zero)
    /// Creates an H4 heading paragraph
    let ``####`` value = Heading(4, value, MarkdownRange.zero)
    /// Creates an H5 heading paragraph
    let ``#####`` value = Heading(5, value, MarkdownRange.zero)
    /// Creates a strong (bold) inline span
    let strong value = Strong(value, MarkdownRange.zero)
    /// Creates a paragraph block
    let p value = Paragraph(value, MarkdownRange.zero)
    /// Creates a span block
    let span value = Span(value, MarkdownRange.zero)
    /// Creates a literal (plain text) inline span
    let (!!) value = Literal(value, MarkdownRange.zero)

    /// Creates a direct hyperlink span
    let link content url =
        DirectLink(content, url, None, MarkdownRange.zero)

    /// Creates an unordered list block
    let ul value =
        ListBlock(Unordered, value, MarkdownRange.zero)

    /// Creates an ordered list block
    let ol value =
        ListBlock(Ordered, value, MarkdownRange.zero)

    /// Creates a table block; an empty header list is treated as no header row
    let table headers alignments rows =
        let hs =
            match headers with
            | [] -> None
            | hs -> Some hs

        TableBlock(hs, alignments, rows, MarkdownRange.zero)

    /// Creates a direct image span
    let img body link =
        DirectImage(body, link, None, MarkdownRange.zero)
// --------------------------------------------------------------------------------------
// Patterns that make recursive Markdown processing easier
// --------------------------------------------------------------------------------------

/// This module provides an easy way of processing Markdown documents.
/// It lets you decompose documents into leafs and nodes with nested paragraphs.
module MarkdownPatterns =
    /// Carries the identity of a leaf span (one with no child spans)
    type SpanLeafInfo = private SL of MarkdownSpan
    /// Carries the identity of a node span (one with child spans)
    type SpanNodeInfo = private SN of MarkdownSpan

    /// Active pattern that classifies a <see cref="MarkdownSpan"/> as either a leaf (no children) or a node (with children)
    let (|SpanLeaf|SpanNode|) span =
        match span with
        | Literal _
        | AnchorLink _
        | InlineCode _
        | DirectImage _
        | IndirectImage _
        | LatexInlineMath _
        | LatexDisplayMath _
        | EmbedSpans _
        | HardLineBreak _ -> SpanLeaf(SL span)
        | Strong(spans, _)
        | Emphasis(spans, _)
        | DirectLink(spans, _, _, _)
        | IndirectLink(spans, _, _, _) -> SpanNode(SN span, spans)

    /// Reconstructs a leaf span from its <see cref="SpanLeafInfo"/> tag
    let SpanLeaf (SL(span)) = span

    /// Reconstructs a node span from its <see cref="SpanNodeInfo"/> tag and a (possibly updated) child span list
    let SpanNode (SN(span), spans) =
        match span with
        | Strong(_, r) -> Strong(spans, r)
        | Emphasis(_, r) -> Emphasis(spans, r)
        | DirectLink(_, l, t, r) -> DirectLink(spans, l, t, r)
        | IndirectLink(_, a, b, r) -> IndirectLink(spans, a, b, r)
        | _ -> invalidArg "" "Incorrect SpanNodeInfo"

    /// Carries the identity of a paragraph that itself contains inline spans
    type ParagraphSpansInfo = private PS of MarkdownParagraph
    /// Carries the identity of a leaf paragraph (no children)
    type ParagraphLeafInfo = private PL of MarkdownParagraph
    /// Carries the identity of a paragraph that contains nested paragraph lists
    type ParagraphNestedInfo = private PN of MarkdownParagraph

    /// Active pattern that classifies a <see cref="MarkdownParagraph"/> as spans-container, leaf, or nested-paragraphs container
    let (|ParagraphLeaf|ParagraphNested|ParagraphSpans|) par =
        match par with
        | Heading(_, spans, _)
        | Paragraph(spans, _)
        | Span(spans, _) -> ParagraphSpans(PS par, spans)
        | OtherBlock _
        | OutputBlock _
        | CodeBlock _
        | InlineHtmlBlock _
        | EmbedParagraphs _
        | LatexBlock _
        | YamlFrontmatter _
        | HorizontalRule _ -> ParagraphLeaf(PL par)
        | ListBlock(_, pars, _) -> ParagraphNested(PN par, pars)
        | QuotedBlock(nested, _) -> ParagraphNested(PN par, [ nested ])
        | TableBlock(headers, _alignments, rows, _) ->
            match headers with
            | None -> ParagraphNested(PN par, rows |> List.concat)
            | Some columns -> ParagraphNested(PN par, columns :: rows |> List.concat)

    /// Reconstructs a spans-container paragraph with an updated span list
    let ParagraphSpans (PS(par), spans) =
        match par with
        | Heading(a, _, r) -> Heading(a, spans, r)
        | Paragraph(_, r) -> Paragraph(spans, r)
        | Span(_, r) -> Span(spans, r)
        | _ -> invalidArg "" "Incorrect ParagraphSpansInfo."

    /// Reconstructs a leaf paragraph from its <see cref="ParagraphLeafInfo"/> tag
    let ParagraphLeaf (PL(par)) = par

    /// Reconstructs a nested-paragraph container with an updated flat list of child paragraphs
    let ParagraphNested (PN(par), pars) =
        let splitEach n list =
            let rec loop n left ansList curList items =
                if List.isEmpty items && List.isEmpty curList then
                    List.rev ansList
                elif left = 0 || List.isEmpty items then
                    loop n n ((List.rev curList) :: ansList) [] items
                else
                    loop n (left - 1) ansList ((List.head items) :: curList) (List.tail items)

            loop n n [] [] list

        match par with
        | ListBlock(a, _, r) -> ListBlock(a, pars, r)
        | QuotedBlock(_, r) -> QuotedBlock(List.concat pars, r)
        | TableBlock(headers, alignments, _, r) ->
            let rows = splitEach (alignments.Length) pars

            if List.isEmpty rows || headers.IsNone then
                TableBlock(None, alignments, rows, r)
            else
                TableBlock(Some(List.head rows), alignments, List.tail rows, r)
        | _ -> invalidArg "" "Incorrect ParagraphNestedInfo."

/// <summary>
/// Controls the parsing of markdown
/// </summary>
type MarkdownParseOptions =
    | None = 0
    /// Treat fenced/indented code blocks as <see cref="MarkdownParagraph.OtherBlock"/> instead of <see cref="MarkdownParagraph.CodeBlock"/>
    | ParseCodeAsOther = 1
    /// Treat non-code content as <see cref="MarkdownParagraph.OtherBlock"/>
    | ParseNonCodeAsOther = 2
    /// Allow and parse YAML front-matter at the top of the document
    | AllowYamlFrontMatter = 4
