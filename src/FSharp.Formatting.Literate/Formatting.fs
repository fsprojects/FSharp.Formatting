namespace FSharp.Formatting.Literate

open System.IO
open System.Collections.Concurrent
open System.Globalization
open FSharp.Formatting.Literate
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Markdown

module internal Formatting =

  /// Format document with the specified output kind
  let format (doc: MarkdownDocument) generateAnchors outputKind parameters =
    match outputKind with
    | OutputKind.Fsx -> Markdown.ToFsx(doc, parameters=parameters)
    | OutputKind.Pynb -> Markdown.ToPynb(doc, parameters=parameters)
    | OutputKind.Latex -> Markdown.ToLatex(doc)
    | OutputKind.Html ->
        let sb = new System.Text.StringBuilder()
        use wr = new StringWriter(sb)
        HtmlFormatting.formatMarkdown wr generateAnchors System.Environment.NewLine true doc.DefinedLinks doc.Paragraphs
        sb.ToString()

  /// Try find first-level heading in the paragraph collection
  let findHeadings paragraphs generateAnchors (outputKind:OutputKind) =
    paragraphs |> Seq.tryPick (fun para ->
      match para with
      | Heading(1, text, r) ->
          match outputKind with
          | OutputKind.Html
          | OutputKind.Latex ->
              let doc = MarkdownDocument([Span(text, r)], dict [])
              Some(format doc generateAnchors outputKind [])
          | _ ->
              None
      | _ -> None)

  /// Given literate document, get a new MarkdownDocument that represents the
  /// entire source code of the specified document (with possible `fsx` formatting)
  let getSourceDocument (doc:LiterateDocument) =
    match doc.Source with
    | LiterateSource.Markdown text ->
        doc.With(paragraphs = [CodeBlock (text, None, "", "", None)])
    | LiterateSource.Script snippets ->
        let mutable count = 0
        let paragraphs =
          [ for Snippet(name, lines) in snippets do
              if snippets.Length > 1 then
                yield Heading(3, [Literal(name, None)], None)
              let id = count <- count + 1; "cell" + string count
              let opts =
                  { Evaluate=true
                    ExecutionCount=None
                    OutputName=id
                    Visibility=LiterateCodeVisibility.VisibleCode }
              let popts =
                  { Condition = None }
              yield EmbedParagraphs(LiterateCode(lines, opts, popts), None) ]
        doc.With(paragraphs = paragraphs)

  let transformDocument (doc: LiterateDocument) output ctx =

    // If we want to include the source code of the script, then process
    // the entire source and generate replacement {source} => ...some html...
    let sourceReplacements =
      if ctx.IncludeSource then
        let doc =
          getSourceDocument doc
          |> Transformations.replaceLiterateParagraphs ctx
        let content = format doc.MarkdownDocument ctx.GenerateHeaderAnchors ctx.OutputKind []
        [ "source", content ]
      else []

    // Get page title (either heading or file name)
    let pageTitle =
      let name = Path.GetFileNameWithoutExtension(output)
      defaultArg (findHeadings doc.Paragraphs ctx.GenerateHeaderAnchors ctx.OutputKind) name

    // To avoid clashes in templating use {contents} for Latex and older {document} for HTML
    let contentTag =
      match ctx.OutputKind with
      | OutputKind.Fsx -> "code"
      | OutputKind.Html -> "document"
      | OutputKind.Latex -> "contents"
      | OutputKind.Pynb -> "cells"

    // Replace all special elements with ordinary Html/Latex Markdown
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let parameters0 =
      [ "page-title", pageTitle
        "page-source", doc.SourceFile ]
      @ ctx.Replacements
      @ sourceReplacements
    let formattedDocument = format doc.MarkdownDocument ctx.GenerateHeaderAnchors ctx.OutputKind parameters0
    let tipsHtml = doc.FormattedTips

    // Construct new Markdown document and write it
    let parameters =
      parameters0 @
      [ contentTag, formattedDocument
        "tooltips", tipsHtml ]

    {
      ContentTag   = contentTag
      Parameters   = parameters
    }
