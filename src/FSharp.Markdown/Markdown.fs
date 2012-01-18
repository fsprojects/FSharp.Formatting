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
  | Unknown

and MarkdownParagrphs = list<MarkdownParagrph>