// --------------------------------------------------------------------------------------
// F# Markdown (WikiMediaFormatting.fs)
// --------------------------------------------------------------------------------------

module FSharp.Markdown.WikiMedia

open System
open System.IO
open System.Collections.Generic
open System.Text.RegularExpressions
open FSharp.Patterns
open FSharp.Collections

// --------------------------------------------------------------------------------------
// Formats Markdown documents as an WikiMedia file
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

/// Generates a unique string out of given input
type UniqueNameGenerator() =
    let generated = new System.Collections.Generic.Dictionary<string, int>()

    member __.GetName(name : string) =
        let ok, i = generated.TryGetValue name
        if ok then
            generated.[name] <- i + 1
            sprintf "%s-%d" name i
        else
            generated.[name] <- 1
            name

/// Context passed around while formatting the HTML
type FormattingContext =
  { LineBreak : unit -> unit
    Newline : string
    Writer : TextWriter
    Links : IDictionary<string, string * option<string>>
    UniqueNameGenerator : UniqueNameGenerator }

/// Write MarkdownSpan value to a TextWriter
let rec formatSpan (ctx:FormattingContext) = function
  | LatexInlineMath(body)
  | LatexDisplayMath(body) ->
      // use mathjax grammar, for detail, check: http://www.mathjax.org/
      ctx.Writer.Write("<math>" + (htmlEncode body) + "</math>")

  | AnchorLink(id) -> ctx.Writer.Write("[[Link|" + id + "]]") 
  | EmbedSpans(cmd) -> formatSpans ctx (cmd.Render())
  | Literal(str) -> ctx.Writer.Write(str)
  | HardLineBreak -> ctx.Writer.WriteLine("<br/>")
  | IndirectLink(body, _, LookupKey ctx.Links (link, title)) 
  | DirectLink(body, (link, title)) ->  
       ctx.Writer.Write("[")
       ctx.Writer.Write(link)
       ctx.Writer.Write(" ")
       formatSpans ctx body
       ctx.Writer.Write("]")
  | IndirectLink(body, original, _) ->
      ctx.Writer.Write("[[Link|")
      ctx.Writer.Write(original)
      //formatSpans ctx body
      ctx.Writer.Write("]]")
  | IndirectImage(body, _, LookupKey ctx.Links (link, title)) 
  | DirectImage(body, (link, title)) -> 
      ctx.Writer.Write("[[Image:")
      ctx.Writer.Write(htmlEncodeQuotes link)
      ctx.Writer.Write("|")
      //ctx.Writer.Write(htmlEncodeQuotes body)
      title |> Option.iter (htmlEncodeQuotes >> ctx.Writer.Write) 
      ctx.Writer.Write("]]")
  | IndirectImage(body, original, _) ->
      ctx.Writer.Write("[[Image:")
      ctx.Writer.Write(htmlEncodeQuotes original)
      ctx.Writer.Write("|")
      ctx.Writer.Write(htmlEncodeQuotes body)

  | Strong(body) -> 
      ctx.Writer.Write("''")
      formatSpans ctx body
      ctx.Writer.Write("''")
  | InlineCode(body) -> 
      ctx.Writer.Write("<code>")
      ctx.Writer.Write(htmlEncode body)
      ctx.Writer.Write("</code>")
  | Emphasis(body) -> 
      ctx.Writer.Write("'''")
      formatSpans ctx body
      ctx.Writer.Write("'''")

/// Write list of MarkdownSpan values to a TextWriter
and formatSpans ctx = List.iter (formatSpan ctx)

/// generate anchor name from Markdown text
let formatAnchor (ctx:FormattingContext) (spans:MarkdownSpans) =
    let extractWords (text:string) =
        Regex.Matches(text, @"\w+")
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Value)

    let rec gather (span:MarkdownSpan) : seq<string> = 
        seq {
            match span with
            | Literal str -> yield! extractWords str
            | Strong body -> yield! gathers body
            | Emphasis body -> yield! gathers body
            | DirectLink (body,_) -> yield! gathers body
            | _ -> ()
        }

    and gathers (spans:MarkdownSpans) = Seq.collect gather spans

    spans 
    |> gathers 
    |> String.concat "-"
    |> fun name -> if String.IsNullOrWhiteSpace name then "header" else name
    |> ctx.UniqueNameGenerator.GetName

/// Write a MarkdownParagraph value to a TextWriter
let rec formatParagraph (ctx:FormattingContext) paragraph =
  match paragraph with
  | LatexBlock(lines) ->
    // use mathjax grammar, for detail, check: http://www.mathjax.org/
    let body = String.concat ctx.Newline lines
    ctx.Writer.Write("<math>" + (htmlEncode body) + "</math>")

  | EmbedParagraphs(cmd) -> formatParagraphs ctx (cmd.Render())
  | Heading(n, spans) ->
      let wrapper = String.replicate (n - 1) "=" 
      ctx.Writer.Write(wrapper)
      formatSpans ctx spans
      ctx.Writer.Write(wrapper)
  | Paragraph(spans) ->
      ctx.LineBreak()
      for span in spans do 
        formatSpan ctx span
      ctx.LineBreak()
  | HorizontalRule(_) ->
      ctx.Writer.Write("----")
  | CodeBlock(code, _, _) ->
      ctx.LineBreak()
      ctx.Writer.Write("<pre>")
      ctx.Writer.Write(htmlEncode code)
      ctx.Writer.Write(ctx.Newline)
      ctx.Writer.Write("</pre>")
      ctx.LineBreak()
  | TableBlock(headers, _, rows) ->
      ctx.LineBreak()
      ctx.Writer.Write("{|")

      let writeRow prefix row = 
          for col in row do
              ctx.Writer.Write("!!") 
              formatParagraphs ctx col
              ctx.Writer.WriteLine(" ")

      headers |> Option.iter (fun header -> 
         ctx.Writer.Write("!")
         writeRow "!!" header 
      )

      rows |> List.iter (fun row -> 
        ctx.Writer.WriteLine("|-")
        writeRow "|" row
      )
      
      ctx.Writer.Write("|}")
      ctx.LineBreak()
  | ListBlock(kind, items) ->
      let tag = if kind = Ordered then "#" else "*"

      for item in items do
          ctx.Writer.Write(tag)
          formatParagraphs ctx item
          ctx.LineBreak()

  | QuotedBlock(body) ->
      ctx.Writer.Write("<blockquote>")
      formatParagraphs ctx body
      ctx.Writer.Write("</blockquote>")
  | Span spans -> 
      formatSpans ctx spans
  | InlineBlock(code) ->
      ctx.Writer.Write(code)
  ctx.LineBreak()

/// Write a list of MarkdownParagraph values to a TextWriter
and formatParagraphs ctx paragraphs = 
  let length = List.length paragraphs
  let smallCtx = ctx
  let bigCtx = { ctx with LineBreak = (fun () -> ctx.Writer.WriteLine("<br/>")) }
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
      LineBreak = (fun () -> writer.Write("<br/>"))
      UniqueNameGenerator = new UniqueNameGenerator() }
