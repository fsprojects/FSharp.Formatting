namespace FSharp.Formatting.Literate

open System.IO
open FSharp.Formatting.Literate
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Markdown
open FSharp.Formatting.Templating

module internal Formatting =

  /// Format document with the specified output kind
  let format (doc: MarkdownDocument) generateAnchors outputKind substitutions =
    match outputKind with
    | OutputKind.Fsx -> Markdown.ToFsx(doc, substitutions=substitutions)
    | OutputKind.Pynb -> Markdown.ToPynb(doc, substitutions=substitutions)
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

  /// Given literate document, get the text for indexing for search
  let getIndexText(doc:LiterateDocument) =
    match doc.Source with
    | LiterateSource.Markdown text -> text
    | LiterateSource.Script snippets ->
        [ for Snippet(_name, lines) in snippets do
            for (Line(line, _)) in lines do
               yield line ]
        |> String.concat "\n"

  let transformDocument (doc: LiterateDocument) outputPath ctx =

    // If we want to include the source code of the script, then process
    // the entire source and generate replacement {source} => ...some html...
    let sourceSubstitutions =
        let doc =
          getSourceDocument doc
          |> Transformations.replaceLiterateParagraphs ctx
        let source = format doc.MarkdownDocument ctx.GenerateHeaderAnchors ctx.OutputKind []
        [ ParamKeys.``fsdocs-source``, source ]

    // Get page title (either heading or file name)
    let pageTitle =
      let name = Path.GetFileNameWithoutExtension(outputPath)
      defaultArg (findHeadings doc.Paragraphs ctx.GenerateHeaderAnchors ctx.OutputKind) name

    // Replace all special elements with ordinary Html/Latex Markdown
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let substitutions0 =
        [ ParamKeys.``fsdocs-page-title``, pageTitle
          ParamKeys.``fsdocs-page-source``, doc.SourceFile ]
        @ ctx.Substitutions
        @ sourceSubstitutions
    let formattedDocument = format doc.MarkdownDocument ctx.GenerateHeaderAnchors ctx.OutputKind substitutions0
    let tipsHtml = doc.FormattedTips

    // Construct new Markdown document and write it
    let substitutions =
      substitutions0 @
      [ ParamKeys.``fsdocs-content``, formattedDocument
        ParamKeys.``fsdocs-tooltips``, tipsHtml ]

    let indexText = (match ctx.OutputKind with OutputKind.Html -> Some (getIndexText doc) | _ -> None )
    {
      OutputPath = outputPath
      OutputKind = ctx.OutputKind
      Title = pageTitle
      IndexText = indexText
      Substitutions = substitutions
    }
