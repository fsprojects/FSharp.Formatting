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

type MarkdownParagrph = 
  | Heading of int * MarkdownSpans
  | Paragraph of MarkdownSpans
  | CodeBlock of string
  | HtmlBlock of string
  | ListBlock of MarkdownListKind * list<MarkdownParagrphs>
  | QuotedBlock of MarkdownParagrphs
  | Span of MarkdownSpans
  | HorizontalRule 

and MarkdownParagrphs = list<MarkdownParagrph>

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

  type ParagraphSpansInfo = private PS of MarkdownParagrph
  type ParagraphLeafInfo = private PL of MarkdownParagrph
  type ParagraphNestedInfo = private PN of MarkdownParagrph

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

  let ParagraphSpans (PS(par), spans) = 
    match par with 
    | Heading(a, _) -> Heading(a, spans)
    | Paragraph(_) -> Paragraph(spans)
    | Span(_) -> Span(spans)
    | _ -> invalidArg "" "Incorrect ParagraphSpansInfo."

  let ParagraphLeaf (PL(par)) = par

  let ParagraphNested (PN(par), pars) =
    match par with 
    | ListBlock(a, _) -> ListBlock(a, pars)
    | QuotedBlock(_) -> QuotedBlock(List.concat pars)
    | _ -> invalidArg "" "Incorrect ParagraphNestedInfo."
