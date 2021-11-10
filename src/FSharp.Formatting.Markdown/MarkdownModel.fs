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
type MarkdownListKind =
    | Ordered
    | Unordered

/// Column in a table can be aligned to left, right, center or using the default alignment
type MarkdownColumnAlignment =
    | AlignLeft
    | AlignRight
    | AlignCenter
    | AlignDefault

/// Represents inline formatting inside a paragraph. This can be literal (with text), various
/// formattings (string, emphasis, etc.), hyperlinks, images, inline maths etc.
type MarkdownSpan =
    | Literal of text: string * range: MarkdownRange option
    | InlineCode of code: string * range: MarkdownRange option
    | Strong of body: MarkdownSpans * range: MarkdownRange option
    | Emphasis of body: MarkdownSpans * range: MarkdownRange option
    | AnchorLink of link: string * range: MarkdownRange option
    | DirectLink of body: MarkdownSpans * link: string * title: string option * range: MarkdownRange option
    | IndirectLink of body: MarkdownSpans * original: string * key: string * range: MarkdownRange option
    | DirectImage of body: string * link: string * title: string option * range: MarkdownRange option
    | IndirectImage of body: string * link: string * key: string * range: MarkdownRange option
    | HardLineBreak of range: MarkdownRange option
    | LatexInlineMath of code: string * range: MarkdownRange option
    | LatexDisplayMath of code: string * range: MarkdownRange option
    | EmbedSpans of customSpans: MarkdownEmbedSpans * range: MarkdownRange option

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
    | Heading of size: int * body: MarkdownSpans * range: MarkdownRange option
    | Paragraph of body: MarkdownSpans * range: MarkdownRange option

    /// A code block, whether fenced or via indentation
    | CodeBlock of
        code: string *
        executionCount: int option *
        fence: string option *
        language: string *
        ignoredLine: string *
        range: MarkdownRange option

    /// A HTML block
    | InlineHtmlBlock of code: string * executionCount: int option * range: MarkdownRange option

    /// A Markdown List block
    | ListBlock of kind: MarkdownListKind * items: list<MarkdownParagraphs> * range: MarkdownRange option

    /// A Markdown Quote block
    | QuotedBlock of paragraphs: MarkdownParagraphs * range: MarkdownRange option

    /// A Markdown Span block
    | Span of body: MarkdownSpans * range: MarkdownRange option

    /// A Markdown Latex block
    | LatexBlock of env: string * body: list<string> * range: MarkdownRange option

    /// A Markdown Horizontal rule
    | HorizontalRule of character: char * range: MarkdownRange option

    /// A Markdown Table
    | TableBlock of
        headers: option<MarkdownTableRow> *
        alignments: list<MarkdownColumnAlignment> *
        rows: list<MarkdownTableRow> *
        range: MarkdownRange option

    /// Represents a block of markdown produced when parsing of code or tables or quoted blocks is suppressed
    | OtherBlock of lines: (string * MarkdownRange) list * range: MarkdownRange option

    /// A special addition for computing paragraphs
    | EmbedParagraphs of customParagraphs: MarkdownEmbedParagraphs * range: MarkdownRange option

    /// A special addition for YAML-style frontmatter
    | YamlFrontmatter of yaml: string list * range: MarkdownRange option

    /// A special addition for inserted outputs
    | OutputBlock of output: string * kind: string * executionCount: int option

/// A type alias for a list of paragraphs
type MarkdownParagraphs = list<MarkdownParagraph>

/// A type alias representing table row as a list of paragraphs
type MarkdownTableRow = list<MarkdownParagraphs>

/// Provides an extensibility point for adding custom kinds of paragraphs into a document
/// (MarkdownEmbedParagraphs values can be embedded using MarkdownParagraph.EmbedParagraphs)
type MarkdownEmbedParagraphs =
    abstract Render: unit -> MarkdownParagraphs

module Dsl =
    let ``#`` value = Heading(1, value, None)
    let ``##`` value = Heading(2, value, None)
    let ``###`` value = Heading(3, value, None)
    let ``####`` value = Heading(4, value, None)
    let ``#####`` value = Heading(5, value, None)
    let strong value = Strong(value, None)
    let p value = Paragraph(value, None)
    let span value = Span(value, None)
    let (!!) value = Literal(value, None)
    let link content url = DirectLink(content, url, None, None)
    let ul value = ListBlock(Unordered, value, None)
    let ol value = ListBlock(Ordered, value, None)

    let table headers alignments rows =
        let hs =
            match headers with
            | [] -> None
            | hs -> Some hs

        TableBlock(hs, alignments, rows, None)

    let img body link = DirectImage(body, link, None, None)
// --------------------------------------------------------------------------------------
// Patterns that make recursive Markdown processing easier
// --------------------------------------------------------------------------------------

/// This module provides an easy way of processing Markdown documents.
/// It lets you decompose documents into leafs and nodes with nested paragraphs.
module MarkdownPatterns =
    type SpanLeafInfo = private SL of MarkdownSpan
    type SpanNodeInfo = private SN of MarkdownSpan

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
        | Strong (spans, _)
        | Emphasis (spans, _)
        | DirectLink (spans, _, _, _)
        | IndirectLink (spans, _, _, _) -> SpanNode(SN span, spans)

    let SpanLeaf (SL (span)) = span

    let SpanNode (SN (span), spans) =
        match span with
        | Strong (_, r) -> Strong(spans, r)
        | Emphasis (_, r) -> Emphasis(spans, r)
        | DirectLink (_, l, t, r) -> DirectLink(spans, l, t, r)
        | IndirectLink (_, a, b, r) -> IndirectLink(spans, a, b, r)
        | _ -> invalidArg "" "Incorrect SpanNodeInfo"

    type ParagraphSpansInfo = private PS of MarkdownParagraph
    type ParagraphLeafInfo = private PL of MarkdownParagraph
    type ParagraphNestedInfo = private PN of MarkdownParagraph

    let (|ParagraphLeaf|ParagraphNested|ParagraphSpans|) par =
        match par with
        | Heading (_, spans, _)
        | Paragraph (spans, _)
        | Span (spans, _) -> ParagraphSpans(PS par, spans)
        | OtherBlock _
        | OutputBlock _
        | CodeBlock _
        | InlineHtmlBlock _
        | EmbedParagraphs _
        | LatexBlock _
        | YamlFrontmatter _
        | HorizontalRule _ -> ParagraphLeaf(PL par)
        | ListBlock (_, pars, _) -> ParagraphNested(PN par, pars)
        | QuotedBlock (nested, _) -> ParagraphNested(PN par, [ nested ])
        | TableBlock (headers, _alignments, rows, _) ->
            match headers with
            | None -> ParagraphNested(PN par, rows |> List.concat)
            | Some columns -> ParagraphNested(PN par, columns :: rows |> List.concat)

    let ParagraphSpans (PS (par), spans) =
        match par with
        | Heading (a, _, r) -> Heading(a, spans, r)
        | Paragraph (_, r) -> Paragraph(spans, r)
        | Span (_, r) -> Span(spans, r)
        | _ -> invalidArg "" "Incorrect ParagraphSpansInfo."

    let ParagraphLeaf (PL (par)) = par

    let ParagraphNested (PN (par), pars) =
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
        | ListBlock (a, _, r) -> ListBlock(a, pars, r)
        | QuotedBlock (_, r) -> QuotedBlock(List.concat pars, r)
        | TableBlock (headers, alignments, _, r) ->
            let rows = splitEach (alignments.Length) pars

            if List.isEmpty rows || headers.IsNone then
                TableBlock(None, alignments, rows, r)
            else
                TableBlock(Some(List.head rows), alignments, List.tail rows, r)
        | _ -> invalidArg "" "Incorrect ParagraphNestedInfo."

/// Controls the parsing of markdown
type MarkdownParseOptions =
    | None = 0
    | ParseCodeAsOther = 1
    | ParseNonCodeAsOther = 2
    | AllowYamlFrontMatter = 4
