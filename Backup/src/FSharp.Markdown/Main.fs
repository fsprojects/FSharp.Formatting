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

module private Utils  =
  /// Replace tabs with four spaces - tab will end at the 
  /// first column that is divisible by four.
  let replaceTabs size = List.map (fun (line:string) ->
    if line.IndexOf('\t') = -1 then line else
    let chars = ResizeArray<_>()
    for i in 0 .. line.Length - 1 do
      if line.[i] <> '\t' then chars.Add(line.[i])
      else 
        chars.Add(' ')
        while chars.Count % size <> 0 do chars.Add(' ')
    String(chars.ToArray()) )

// --------------------------------------------------------------------------------------
// Expose Markdown transformer functions as an overloaded static method (C# friendly)
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
  static member Parse(text, newline) =
    use reader = new StringReader(text)
    let lines = 
      [ let line = ref ""
        while (line := reader.ReadLine(); line.Value <> null) do
          yield line.Value ]
      |> Utils.replaceTabs 4
    let links = Dictionary<_, _>()
    let (Lines.TrimBlank lines) = lines
    let ctx : ParsingContext = { Newline = newline; Links = links }
    let paragraphs = lines |> parseParagraphs ctx |> List.ofSeq
    MarkdownDocument(paragraphs, links)

  /// Parse the specified text into a MarkdownDocument.
  static member Parse(text) =
    Markdown.Parse(text, Environment.NewLine)

  /// Transform Markdown document into HTML format. The result
  /// will be written to the provided TextWriter.
  static member TransformHtml(text, writer:TextWriter, newline) = 
    let doc = Markdown.Parse(text, newline)
    formatMarkdown writer newline doc.DefinedLinks doc.Paragraphs

  /// Transform Markdown document into HTML format. The result
  /// will be written to the provided TextWriter.
  static member TransformHtml(text, writer:TextWriter) = 
    Markdown.TransformHtml(text, writer, Environment.NewLine)

  /// Transform Markdown document into HTML format. 
  /// The result will be returned as a string.
  static member TransformHtml(text, newline) =
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Markdown.TransformHtml(text, wr, newline)
    sb.ToString()

  /// Transform Markdown document into HTML format. 
  /// The result will be returned as a string.
  static member TransformHtml(text) =
    Markdown.TransformHtml(text, Environment.NewLine)
  
  /// Transform the provided MakrdownDocument into HTML
  /// format and write the result to a given writer.
  static member WriteHtml(doc:MarkdownDocument, writer, newline) = 
    formatMarkdown writer newline doc.DefinedLinks doc.Paragraphs

  /// Transform the provided MakrdownDocument into HTML
  /// format and return the result as a string.
  static member WriteHtml(doc:MarkdownDocument, newline) = 
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Markdown.WriteHtml(doc, wr, newline)
    sb.ToString()

  /// Transform the provided MakrdownDocument into HTML
  /// format and return the result as a string.
  static member WriteHtml(doc:MarkdownDocument) = 
    Markdown.WriteHtml(doc, Environment.NewLine)

  /// Transform the provided MakrdownDocument into HTML
  /// format and write the result to a given writer.
  static member WriteHtml(doc:MarkdownDocument, writer) = 
    Markdown.WriteHtml(doc, writer, Environment.NewLine)

  // -----------------------------------
  // Now the functions for LaTeX format
  // -----------------------------------

  /// Transform Markdown document into LaTeX format. The result
  /// will be written to the provided TextWriter.
  static member TransformLatex(text, writer:TextWriter, newline) = 
    let doc = Markdown.Parse(text, newline)
    Latex.formatMarkdown writer newline doc.DefinedLinks doc.Paragraphs

  /// Transform Markdown document into LaTeX format. The result
  /// will be written to the provided TextWriter.
  static member TransformLatex(text, writer:TextWriter) = 
    Markdown.TransformLatex(text, writer, Environment.NewLine)

  /// Transform Markdown document into LaTeX format. 
  /// The result will be returned as a string.
  static member TransformLatex(text, newline) =
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Markdown.TransformLatex(text, wr, newline)
    sb.ToString()

  /// Transform Markdown document into LaTeX format. 
  /// The result will be returned as a string.
  static member TransformLatex(text) =
    Markdown.TransformLatex(text, Environment.NewLine)

  /// Transform the provided MarkdownDocument into LaTeX
  /// format and write the result to a given writer.
  static member WriteLatex(doc:MarkdownDocument, writer, newline) = 
    Latex.formatMarkdown writer newline doc.DefinedLinks doc.Paragraphs

  /// Transform the provided MarkdownDocument into LaTeX
  /// format and return the result as a string.
  static member WriteLatex(doc:MarkdownDocument, newline) = 
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Markdown.WriteLatex(doc, wr, newline)
    sb.ToString()

  /// Transform the provided MarkdownDocument into LaTeX
  /// format and return the result as a string.
  static member WriteLatex(doc:MarkdownDocument) = 
    Markdown.WriteLatex(doc, Environment.NewLine)

  /// Transform the provided MarkdownDocument into LaTeX
  /// format and write the result to a given writer.
  static member WriteLatex(doc:MarkdownDocument, writer) = 
    Markdown.WriteLatex(doc, writer, Environment.NewLine)
