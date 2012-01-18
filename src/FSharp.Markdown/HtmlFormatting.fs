// --------------------------------------------------------------------------------------
// F# Markdown (HtmlFormatting.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module FSharp.Markdown.Html

open System.IO
open System.Collections.Generic
open FSharp.Patterns
open FSharp.Collections

// --------------------------------------------------------------------------------------
// Formats Markdown documents as an HTML file
// --------------------------------------------------------------------------------------

/// Basic escaping as done by Markdown
let htmlEncode (code:string) = 
  code.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

/// Basic escaping as done by Markdown including quotes
let htmlEncodeQuotes (code:string) = 
  (htmlEncode code).Replace("\"", "&quot;")

/// Lookup a specified key in a dictionary, possibly
/// ignoring newlines or spaces in the key.
let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) = 
  [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); 
    key.Replace("\n", ""); key.Replace("\n", " ") ]
  |> Seq.tryPick (fun key ->
    match dict.TryGetValue(key) with
    | true, v -> Some v 
    | _ -> None)

/// Context passed around while formatting the HTML
type FormattingContext =
  { LineBreak : unit -> unit
    Newline : string
    Writer : TextWriter
    Links : IDictionary<string, string * option<string>>
    ParagraphIndent : unit -> unit }

let bigBreak (ctx:FormattingContext) () =
  ctx.Writer.Write(ctx.Newline + ctx.Newline)
let smallBreak (ctx:FormattingContext) () =
  ctx.Writer.Write(ctx.Newline)
let noBreak (ctx:FormattingContext) () = ()

/// Write MarkdownSpan value to a TextWriter
let rec formatSpan (ctx:FormattingContext) = function 
  | Literal(str) -> ctx.Writer.Write(str)
  | HardLineBreak -> ctx.Writer.Write("<br />")
  | IndirectLink(body, _, LookupKey ctx.Links (link, title)) 
  | DirectLink(body, (link, title)) -> 
      ctx.Writer.Write("<a href=\"")
      ctx.Writer.Write(htmlEncode link)
      match title with 
      | Some title ->
          ctx.Writer.Write("\" title=\"")
          ctx.Writer.Write(htmlEncodeQuotes title)
      | _ -> ()
      ctx.Writer.Write("\">")
      formatSpans ctx body
      ctx.Writer.Write("</a>")

  | IndirectLink(body, original, _) ->
      ctx.Writer.Write("[")
      formatSpans ctx body
      ctx.Writer.Write("]")
      ctx.Writer.Write(original)

  | IndirectImage(body, _, LookupKey ctx.Links (link, title)) 
  | DirectImage(body, (link, title)) -> 
      ctx.Writer.Write("<img src=\"")
      ctx.Writer.Write(htmlEncodeQuotes link)
      ctx.Writer.Write("\" alt=\"")
      ctx.Writer.Write(htmlEncodeQuotes body)
      match title with 
      | Some title ->
          ctx.Writer.Write("\" title=\"")
          ctx.Writer.Write(htmlEncodeQuotes title)
      | _ -> ()
      ctx.Writer.Write("\" />")
  | IndirectImage(body, original, _) ->
      ctx.Writer.Write("[")
      ctx.Writer.Write(body)
      ctx.Writer.Write("]")
      ctx.Writer.Write(original)

  | Strong(body) -> 
      ctx.Writer.Write("<strong>")
      formatSpans ctx body
      ctx.Writer.Write("</strong>")
  | InlineCode(body) -> 
      ctx.Writer.Write("<code>")
      ctx.Writer.Write(htmlEncode body)
      ctx.Writer.Write("</code>")
  | Emphasis(body) -> 
      ctx.Writer.Write("<em>")
      formatSpans ctx body
      ctx.Writer.Write("</em>")

/// Write list of MarkdownSpan values to a TextWriter
and formatSpans ctx = List.iter (formatSpan ctx)

/// Write a MarkdownParagrpah value to a TextWriter
let rec formatParagraph (ctx:FormattingContext) paragraph =
  match paragraph with
  | Heading(n, spans) -> 
      ctx.Writer.Write("<h" + string n + ">")
      formatSpans ctx spans
      ctx.Writer.Write("</h" + string n + ">")
  | Paragraph(spans) ->
      ctx.ParagraphIndent()
      ctx.Writer.Write("<p>")
      for span in spans do 
        formatSpan ctx span
      ctx.Writer.Write("</p>")
  | HorizontalRule ->
      ctx.Writer.Write("<hr />")
  | CodeBlock(code) ->
      ctx.Writer.Write("<pre><code>")
      ctx.Writer.Write(htmlEncode code)
      ctx.Writer.Write(ctx.Newline)
      ctx.Writer.Write("</code></pre>")

  | ListBlock(kind, items) ->
      let tag = if kind = Ordered then "ol" else "ul"
      ctx.Writer.Write("<" + tag + ">" + ctx.Newline)
      for body in items do
        ctx.Writer.Write("<li>")
        body |> List.iterInterleaved 
                  (formatParagraph { ctx with LineBreak = noBreak ctx }) 
                  (fun () -> ctx.Writer.Write(ctx.Newline))
        ctx.Writer.Write("</li>" + ctx.Newline)
      ctx.Writer.Write("</" + tag + ">")
  | QuotedBlock(body) ->
      ctx.ParagraphIndent()
      ctx.Writer.Write("<blockquote>" + ctx.Newline)
      formatParagraphs { ctx with ParagraphIndent = fun () -> ctx.ParagraphIndent(); ctx.Writer.Write("  ") } body
      ctx.ParagraphIndent()
      ctx.Writer.Write("</blockquote>")
  | Span spans -> 
      formatSpans ctx spans
  | HtmlBlock(code) ->
      ctx.Writer.Write(code)
  | Unknown -> ()
  ctx.LineBreak()

/// Write a list of MarkdownParagrpah values to a TextWriter
and formatParagraphs ctx paragraphs = 
  let length = List.length paragraphs
  let smallCtx = { ctx with LineBreak = smallBreak ctx }
  let bigCtx = { ctx with LineBreak = bigBreak ctx }
  for last, paragraph in paragraphs |> Seq.mapi (fun i v -> (i = length - 1), v) do
    formatParagraph (if last then smallCtx else bigCtx) paragraph

/// Format Markdown document and write the result to 
/// a specified TextWriter. Parameters specify newline character
/// and a dictionary with link keys defined in the document.
let formatMarkdown writer newline links = 
  formatParagraphs 
    { Writer = writer
      Links = links
      Newline = newline
      LineBreak = ignore
      ParagraphIndent = ignore }
