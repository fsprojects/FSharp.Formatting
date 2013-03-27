// --------------------------------------------------------------------------------------
// F# Markdown (LatexFormatting.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module FSharp.Markdown.Latex

open System.IO
open System.Web
open System.Collections.Generic
open FSharp.Patterns
open FSharp.Collections

/// LaTEX special chars
/// from http://tex.stackexchange.com/questions/34580/escape-character-in-latex
let specialChars =
    [|  // This line comes first to avoid double replacing
        @"\", @"\textbackslash";
        "#", @"\#";
        "$", @"\$";
        "%", @"\%";
        "&", @"\&";
        "_", @"\_";
        "{", @"\{";
        "}", @"\}";
        "~",@"\textasciitilde";
        "^", @"\textasciicircum" |]
    
let latexEncode s =
    specialChars |> Array.fold (fun (acc:string) (k, v) -> acc.Replace(k, v)) (HttpUtility.HtmlDecode s)

/// Lookup a specified key in a dictionary, possibly
/// ignoring newlines or spaces in the key.
let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) = 
  [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); 
    key.Replace("\n", ""); key.Replace("\n", " ") ]
  |> Seq.tryPick (fun key ->
    match dict.TryGetValue(key) with
    | true, v -> Some v 
    | _ -> None)

/// Context passed around while formatting the LaTEX
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
  | Literal(str) -> ctx.Writer.Write(latexEncode str)
  | HardLineBreak -> bigBreak ctx ()

  | IndirectLink(body, _, LookupKey ctx.Links (link, _)) 
  | DirectLink(body, (link, _))
  | IndirectLink(body, link, _) ->
      ctx.Writer.Write(@"\href{")
      ctx.Writer.Write(latexEncode link)
      ctx.Writer.Write("}{")
      formatSpans ctx body
      ctx.Writer.Write("}")

  | IndirectImage(body, _, LookupKey ctx.Links (link, _)) 
  | DirectImage(body, (link, _)) 
  | IndirectImage(body, link, _) ->
      // Use the technique introduced at
      // http://stackoverflow.com/q/14014827
      ctx.Writer.WriteLine(@"\begin{figure}[htbp]\centering")
      ctx.Writer.Write(@"\includegraphics{")
      ctx.Writer.Write(latexEncode link)
      ctx.Writer.WriteLine("}")
      ctx.Writer.Write(@"\caption{")
      ctx.Writer.Write(latexEncode body)
      ctx.Writer.WriteLine("}")
      ctx.Writer.WriteLine(@"\end{figure}")

  | Strong(body) -> 
      ctx.Writer.Write(@"\textbf{")
      formatSpans ctx body
      ctx.Writer.Write("}")
  | InlineCode(body) -> 
      ctx.Writer.Write(@"\texttt{")
      ctx.Writer.Write(latexEncode body)
      ctx.Writer.Write("}")
  | Emphasis(body) -> 
      ctx.Writer.Write(@"\emph{")
      formatSpans ctx body
      ctx.Writer.Write("}")

/// Write list of MarkdownSpan values to a TextWriter
and formatSpans ctx = List.iter (formatSpan ctx)

/// Write a MarkdownParagrpah value to a TextWriter
let rec formatParagraph (ctx:FormattingContext) paragraph =
  match paragraph with
  | Heading(n, spans) -> 
      let level = 
        match n with
        | 1 -> @"\section*"
        | 2 -> @"\subsection*"
        | 3 -> @"\subsubsection*"
        | 4 -> @"\paragraph"
        | 5 -> @"\subparagraph"
        | _ -> ""
      ctx.Writer.Write(level + "{")
      formatSpans ctx spans
      ctx.Writer.WriteLine("}")
  | Paragraph(spans) ->
      // What is paragraph indent doing?
      ctx.ParagraphIndent()
      bigBreak ctx ()
      for span in spans do 
        formatSpan ctx span
  | HorizontalRule ->
      // Reference from http://tex.stackexchange.com/q/19579/9623
      ctx.Writer.WriteLine(@"\noindent\makebox[\linewidth]{\rule{\linewidth}{0.4pt}}")
  | CodeBlock(code) ->
      ctx.Writer.WriteLine(@"\begin{lstlisting}")
      ctx.Writer.WriteLine(code)
      ctx.Writer.WriteLine(@"\end{lstlisting}")
  | TableBlock(headers, alignments, rows) ->
      let aligns = alignments |> List.map (function
        | AlignLeft -> "|l"
        | AlignRight -> "|r"
        | AlignCenter -> "|c"
        | AlignDefault -> "|")
      ctx.Writer.WriteLine(@"\begin{longtable}{0|}\hline", aligns)
      headers
      |> Option.fold (fun acc e -> e::acc) rows
      |> List.iter(fun row ->
           for cell in row do
             match cell with
             | p::ps ->
               formatParagraph { ctx with LineBreak = noBreak ctx } p
               for p in ps do
                 ctx.Writer.Write(" & ")
                 formatParagraph { ctx with LineBreak = noBreak ctx } p
             | [] -> ()
           ctx.Writer.WriteLine(@"\hline"))
      ctx.Writer.WriteLine(@"\end{longtable}")

  | ListBlock(kind, items) ->
      let tag = if kind = Ordered then "enumerate" else "itemize"
      ctx.Writer.WriteLine(@"\begin{" + tag + "}")
      for body in items do
        ctx.Writer.Write(@"\item ")
        body |> List.iterInterleaved 
                  (formatParagraph { ctx with LineBreak = noBreak ctx }) 
                  (noBreak ctx)
        ctx.Writer.WriteLine()
      ctx.Writer.WriteLine(@"\end{" + tag + "}")
  | QuotedBlock(body) ->
      ctx.ParagraphIndent()
      ctx.Writer.WriteLine(@"\begin{quote}")
      formatParagraphs { ctx with ParagraphIndent = fun () -> ctx.ParagraphIndent(); ctx.Writer.Write("  ") } body
      ctx.ParagraphIndent()
      ctx.Writer.WriteLine(@"\end{quote}")
  | Span spans -> 
      formatSpans ctx spans
  | HtmlBlock(code) ->
      // To be safe, put them into verbatim
      // Further processing follows later
      ctx.Writer.Write(@"\begin{lstlisting}")
      ctx.Writer.Write(code)
      ctx.Writer.WriteLine(@"\end{lstlisting}")
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
