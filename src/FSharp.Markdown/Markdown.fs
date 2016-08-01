// --------------------------------------------------------------------------------------
// F# Markdown (Markdown.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.Markdown

open System
open System.IO
open System.Collections.Generic
open FSharp.Formatting.Common

// --------------------------------------------------------------------------------------
// Definition of the Markdown format
// --------------------------------------------------------------------------------------

/// A list kind can be `Ordered` or `Unordered` corresponding to `<ol>` and `<ul>` elements
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
  | Literal of text:string * range:MarkdownRange option
  | InlineCode of code:string * range:MarkdownRange option
  | Strong of body:MarkdownSpans * range:MarkdownRange option
  | Emphasis of body:MarkdownSpans * range:MarkdownRange option
  | AnchorLink of link:string * range:MarkdownRange option
  | DirectLink of body:MarkdownSpans * link:string * title:option<string> * range:MarkdownRange option
  | IndirectLink of body:MarkdownSpans * original:string * key:string * range:MarkdownRange option
  | DirectImage of body:string * link:string * title:option<string> * range:MarkdownRange option
  | IndirectImage of body:string * link:string * key:string * range:MarkdownRange option
  | HardLineBreak of range:MarkdownRange option
  | LatexInlineMath of code:string * range:MarkdownRange option
  | LatexDisplayMath of code:string * range:MarkdownRange option
  | EmbedSpans of customSpans:MarkdownEmbedSpans * range:MarkdownRange option

/// A type alias for a list of `MarkdownSpan` values
and MarkdownSpans = list<MarkdownSpan>

/// Provides an extensibility point for adding custom kinds of spans into a document
/// (`MarkdownEmbedSpans` values can be embedded using `MarkdownSpan.EmbedSpans`)
and MarkdownEmbedSpans =
  abstract Render : unit -> MarkdownSpans

/// A paragraph represents a (possibly) multi-line element of a Markdown document.
/// Paragraphs are headings, inline paragraphs, code blocks, lists, quotations, tables and
/// also embedded LaTeX blocks.
type MarkdownParagraph = 
  | Heading of size:int * body:MarkdownSpans * range:MarkdownRange option
  | Paragraph of body:MarkdownSpans * range:MarkdownRange option
  | CodeBlock of code:string * language:string * ignoredLine:string * range:MarkdownRange option
  | InlineBlock of code:string * range:MarkdownRange option
  | ListBlock of kind:MarkdownListKind * items:list<MarkdownParagraphs> * range:MarkdownRange option
  | QuotedBlock of paragraphs:MarkdownParagraphs * range:MarkdownRange option
  | Span of body:MarkdownSpans * range:MarkdownRange option
  | LatexBlock of body:list<string> * range:MarkdownRange option
  | HorizontalRule of character:char * range:MarkdownRange option
  | TableBlock of headers:option<MarkdownTableRow> * alignments:list<MarkdownColumnAlignment> * rows:list<MarkdownTableRow> * range:MarkdownRange option
  | EmbedParagraphs of customParagraphs:MarkdownEmbedParagraphs * range:MarkdownRange option

/// A type alias for a list of paragraphs
and MarkdownParagraphs = list<MarkdownParagraph>

/// A type alias representing table row as a list of paragraphs
and MarkdownTableRow = list<MarkdownParagraphs>

/// Provides an extensibility point for adding custom kinds of paragraphs into a document
/// (`MarkdownEmbedParagraphs` values can be embedded using `MarkdownParagraph.EmbedParagraphs`)
and MarkdownEmbedParagraphs =
  abstract Render : unit -> MarkdownParagraphs

// --------------------------------------------------------------------------------------
// Patterns that make recursive Markdown processing easier
// --------------------------------------------------------------------------------------

/// This module provides an easy way of processing Markdown documents.
/// It lets you decompose documents into leafs and nodes with nested paragraphs.
module Matching =
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
    | HardLineBreak _ -> 
        SpanLeaf(SL span)
    | Strong(spans, _)
    | Emphasis(spans , _)
    | DirectLink(spans, _, _, _)
    | IndirectLink(spans, _, _, _) -> 
        SpanNode(SN span, spans)

  let SpanLeaf (SL(span)) = span
  let SpanNode (SN(span), spans) =
    match span with
    | Strong(_, r) -> Strong(spans , r)
    | Emphasis(_, r) -> Emphasis(spans, r)
    | DirectLink(_, l, t, r) -> DirectLink(spans, l, t, r)
    | IndirectLink(_, a, b, r) -> IndirectLink(spans, a, b, r) 
    | _ -> invalidArg "" "Incorrect SpanNodeInfo"

  type ParagraphSpansInfo = private PS of MarkdownParagraph
  type ParagraphLeafInfo = private PL of MarkdownParagraph
  type ParagraphNestedInfo = private PN of MarkdownParagraph

  let (|ParagraphLeaf|ParagraphNested|ParagraphSpans|) par =
    match par with  
    | Heading(_, spans, _)
    | Paragraph(spans, _)
    | Span(spans, _) ->
        ParagraphSpans(PS par, spans)
    | CodeBlock _
    | InlineBlock _ 
    | EmbedParagraphs _
    | LatexBlock _
    | HorizontalRule _ ->
        ParagraphLeaf(PL par)
    | ListBlock(_, pars, _) ->
        ParagraphNested(PN par, pars)
    | QuotedBlock(nested, _) ->
        ParagraphNested(PN par, [nested])
    | TableBlock(headers, alignments, rows, _) ->
      match headers with
      | None -> ParagraphNested(PN par, rows |> List.concat)
      | Some columns -> ParagraphNested(PN par, columns::rows |> List.concat)

  let ParagraphSpans (PS(par), spans) = 
    match par with 
    | Heading(a, _, r) -> Heading(a, spans, r)
    | Paragraph(_, r) -> Paragraph(spans, r)
    | Span(_, r) -> Span(spans, r)
    | _ -> invalidArg "" "Incorrect ParagraphSpansInfo."

  let ParagraphLeaf (PL(par)) = par

  let ParagraphNested (PN(par), pars) =
    let splitEach n list =
      let rec loop n left ansList curList items =
        if List.isEmpty items && List.isEmpty curList then List.rev ansList
        elif left = 0 || List.isEmpty items then loop n n ((List.rev curList) :: ansList) [] items
        else loop n (left - 1) ansList ((List.head items) :: curList) (List.tail items)
      loop n n [] [] list

    match par with 
    | ListBlock(a, _, r) -> ListBlock(a, pars, r)
    | QuotedBlock(_, r) -> QuotedBlock(List.concat pars, r)
    | TableBlock(headers, alignments, _, r) ->
        let rows = splitEach (alignments.Length) pars
        if List.isEmpty rows || headers.IsNone then TableBlock(None, alignments, rows, r)
        else TableBlock(Some(List.head rows), alignments, List.tail rows, r)
    | _ -> invalidArg "" "Incorrect ParagraphNestedInfo."
