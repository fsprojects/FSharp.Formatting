// --------------------------------------------------------------------------------------
// F# Markdown (Main.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.Markdown

open System
open System.IO
open System.Collections.Generic

open FSharp.Patterns
open FSharp.Markdown.Parser
open FSharp.Markdown.Html
open FSharp.Formatting.Common

// --------------------------------------------------------------------------------------
// Expose Markdown transformer functions as an overloaded static method
// --------------------------------------------------------------------------------------

/// Representation of a Markdown document - the representation of Paragraphs
/// uses an F# discriminated union type and so is best used from F#.
type MarkdownDocument(paragraphs, links) =
  /// Returns a list of paragraphs in the document
  member x.Paragraphs : MarkdownParagraphs = paragraphs

  /// Returns a dictionary containing explicitly defined links
  member x.DefinedLinks : IDictionary<string, string * option<string>> = links

/// Static class that provides methods for formatting 
/// and transforming Markdown documents.
type Markdown =
  /// Parse the specified text into a MarkdownDocument. Line breaks in the
  /// inline HTML (etc.) will be stored using the specified string.
  static member Parse(text, ?newline) =
    let newline = defaultArg newline Environment.NewLine
    use reader = new StringReader(text)
    let lines = 
      [ let line = ref ""
        let mutable lineNo = 1
        while (line := reader.ReadLine(); line.Value <> null) do
          yield (line.Value, { StartLine = lineNo; StartColumn = 0; EndLine = lineNo; EndColumn = line.Value.Length })
          lineNo <- lineNo + 1
        if text.EndsWith(newline) then
          yield ("", { StartLine = lineNo; StartColumn = 0; EndLine = lineNo; EndColumn = 0 }) ]
      //|> Utils.replaceTabs 4
    let links = Dictionary<_, _>()
    //let (Lines.TrimBlank lines) = lines
    let ctx : ParsingContext = { Newline = newline; Links = links; CurrentRange = Some(MarkdownRange.zero) }
    let paragraphs =
      lines
      |> FSharp.Collections.List.skipWhile (fun (s, n) -> String.IsNullOrWhiteSpace s)
      |> parseParagraphs ctx
      |> List.ofSeq
    MarkdownDocument(paragraphs, links)

  /// Transform the provided MarkdownDocument into HTML
  /// format and write the result to a given writer.
  static member WriteAsHtml(doc:MarkdownDocument, writer, ?newline) = 
    let newline = defaultArg newline Environment.NewLine
    formatMarkdown writer false newline false doc.DefinedLinks doc.Paragraphs

  /// Transform Markdown text into HTML format. The result
  /// will be written to the provided TextWriter.
  static member WriteAsHtml(markdownText: string, writer:TextWriter, ?newline) = 
    let doc = Markdown.Parse(markdownText, ?newline=newline)
    Markdown.WriteAsHtml(doc, writer, ?newline=newline)

  /// Transform the provided MarkdownDocument into HTML
  /// format and return the result as a string.
  static member ToHtmlString(doc:MarkdownDocument, ?newline) = 
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Markdown.WriteAsHtml(doc, wr, ?newline=newline)
    sb.ToString()

  /// Transform Markdown document into HTML format. 
  /// The result will be returned as a string.
  static member ToHtmlString(markdownText: string, ?newline) =
    let doc = Markdown.Parse(markdownText, ?newline=newline)
    Markdown.ToHtmlString(doc, ?newline=newline)

  /// Transform the provided MarkdownDocument into LaTeX
  /// format and write the result to a given writer.
  static member WriteAsLatex(doc:MarkdownDocument, writer, ?newline) = 
    let newline = defaultArg newline Environment.NewLine
    Latex.formatMarkdown writer newline doc.DefinedLinks doc.Paragraphs

  /// Transform Markdown document into LaTeX format. The result
  /// will be written to the provided TextWriter.
  static member WriteAsLatex(markdownText, writer:TextWriter, ?newline) = 
    let doc = Markdown.Parse(markdownText, ?newline=newline)
    Markdown.WriteAsLatex(doc, writer, ?newline=newline)

  /// Transform the provided MarkdownDocument into LaTeX
  /// format and return the result as a string.
  static member ToLatexString(doc:MarkdownDocument, ?newline) = 
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Markdown.WriteAsLatex(doc, wr, ?newline=newline)
    sb.ToString()

  /// Transform Markdown text into LaTeX format. The result will be returned as a string.
  static member ToLatexString(markdownText: string, ?newline) =
    let doc = Markdown.Parse(markdownText, ?newline=newline)
    Markdown.ToLatexString(doc, ?newline=newline)

  /// Transform the provided MarkdownDocument into Pynb and return the result as a string.
  static member ToPynbString(doc: MarkdownDocument) =
    //let newline = defaultArg newline Environment.NewLine
    Pynb.formatMarkdownAsPynb doc.DefinedLinks doc.Paragraphs

  /// Transform the provided markdown text into Pynb and return the result as a string.
  static member ToPynbString(markdownText: string) =
    let doc = Markdown.Parse(markdownText)
    Markdown.ToPynbString(doc)

