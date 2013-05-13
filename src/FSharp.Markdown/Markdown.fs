// --------------------------------------------------------------------------------------
// F# Markdown (Markdown.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.Markdown

open System
open System.IO
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Definition of the Markdown format
// --------------------------------------------------------------------------------------

type MarkdownListKind = 
  | Ordered 
  | Unordered

type MarkdownColumnAlignment =
  | AlignLeft
  | AlignRight
  | AlignCenter
  | AlignDefault

type MarkdownSpan =
  | Literal of string
  | InlineCode of string
  | Strong of MarkdownSpans
  | Emphasis of MarkdownSpans
  | DirectLink of MarkdownSpans * (string * option<string>)
  | IndirectLink of MarkdownSpans * string * string
  | DirectImage of string * (string * option<string>)
  | IndirectImage of string * string * string
  | HardLineBreak

and MarkdownSpans = list<MarkdownSpan>

type MarkdownParagraph = 
  | Heading of int * MarkdownSpans
  | Paragraph of MarkdownSpans
  | CodeBlock of string
  | HtmlBlock of string
  | ListBlock of MarkdownListKind * list<MarkdownParagraphs>
  | QuotedBlock of MarkdownParagraphs
  | Span of MarkdownSpans
  | HorizontalRule 
  | TableBlock of option<MarkdownTableRow> * list<MarkdownColumnAlignment> * list<MarkdownTableRow>

and MarkdownParagraphs = list<MarkdownParagraph>

and MarkdownTableRow = list<MarkdownParagraphs>

// --------------------------------------------------------------------------------------
// Patterns that make recursive Markdown processing easier
// --------------------------------------------------------------------------------------

module Matching =
  type SpanLeafInfo = private SL of MarkdownSpan
  type SpanNodeInfo = private SN of MarkdownSpan
   
  let (|SpanLeaf|SpanNode|) span = 
    match span with
    | Literal _ 
    | InlineCode _
    | DirectImage _ 
    | IndirectImage _
    | HardLineBreak -> 
        SpanLeaf(SL span)
    | Strong spans 
    | Emphasis spans 
    | DirectLink(spans, _)
    | IndirectLink(spans, _, _) -> 
        SpanNode(SN span, spans)

  let SpanLeaf (SL(span)) = span
  let SpanNode (SN(span), spans) =
    match span with
    | Strong _ -> Strong spans 
    | Emphasis _ -> Emphasis spans
    | DirectLink(_, a) -> DirectLink(spans, a)
    | IndirectLink(_, a, b) -> IndirectLink(spans, a, b) 
    | _ -> invalidArg "" "Incorrect SpanNodeInfo"

  type ParagraphSpansInfo = private PS of MarkdownParagraph
  type ParagraphLeafInfo = private PL of MarkdownParagraph
  type ParagraphNestedInfo = private PN of MarkdownParagraph

  let (|ParagraphLeaf|ParagraphNested|ParagraphSpans|) par =
    match par with  
    | Heading(_, spans)
    | Paragraph(spans)
    | Span(spans) ->
        ParagraphSpans(PS par, spans)
    | CodeBlock _
    | HtmlBlock _ 
    | HorizontalRule ->
        ParagraphLeaf(PL par)
    | ListBlock(_, pars) ->
        ParagraphNested(PN par, pars)
    | QuotedBlock(nested) ->
        ParagraphNested(PN par, [nested])
    | TableBlock(headers, alignments, rows) ->
      match headers with
      | None -> ParagraphNested(PN par, rows |> List.concat)
      | Some columns -> ParagraphNested(PN par, columns::rows |> List.concat)

  let ParagraphSpans (PS(par), spans) = 
    match par with 
    | Heading(a, _) -> Heading(a, spans)
    | Paragraph(_) -> Paragraph(spans)
    | Span(_) -> Span(spans)
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
    | ListBlock(a, _) -> ListBlock(a, pars)
    | QuotedBlock(_) -> QuotedBlock(List.concat pars)
    | TableBlock(headers, alignments, _) ->
      let rows = splitEach (alignments.Length) pars
      if List.isEmpty rows || headers.IsNone then TableBlock(None, alignments, rows)
      else TableBlock(Some(List.head rows), alignments, List.tail rows)
    | _ -> invalidArg "" "Incorrect ParagraphNestedInfo."
