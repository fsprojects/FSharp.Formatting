namespace FSharp.Formatting.Literate

open System
open System.Collections.Generic
open System.IO
open System.Runtime.CompilerServices
open FSharp.Formatting.Markdown
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Templating

/// <summary>
/// This type provides three simple methods for calling the literate programming tool.
/// The <c>ConvertMarkdownFile</c> and <c>ConvertScriptFile</c> methods process a single Markdown document
/// and F# script, respectively. The <c>ConvertDirectory</c> method handles an entire directory tree
/// (looking for <c>*.fsx</c> and <c>*.md</c> files).
/// </summary>
///
/// <namespacedoc>
///   <summary>Functionality to support literate programming for F# scripts</summary>
/// </namespacedoc>
type Literate private () =

  /// Build default options context for formatting literate document
  static let formattingContext (outputKind: OutputKind) prefix lineNumbers generateAnchors substitutions tokenKindToCss =
    let defines = [ outputKind.Extension ]
    { Substitutions = defaultArg substitutions []
      GenerateLineNumbers = defaultArg lineNumbers true
      Prefix = defaultArg prefix "fs"
      ConditionalDefines = defines
      OutputKind = outputKind
      GenerateHeaderAnchors = defaultArg generateAnchors false
      TokenKindToCss = tokenKindToCss
    }


  /// Lookup a specified key in a dictionary, possibly
  /// ignoring newlines or spaces in the key.
  static let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) =
    [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " ");
      key.Replace("\n", ""); key.Replace("\n", " ") ]
    |> Seq.tryPick (fun key ->
      match dict.TryGetValue(key) with
      | true, v -> Some v
      | _ -> None)

  /// When generating LaTeX, we need to save all files locally
  static let rec downloadSpanImages (saver, links) para =
    match para with 
    | IndirectImage(body, _, LookupKey links (link, title), range)
    | DirectImage(body, link, title, range) -> DirectImage(body, saver link, title, range)
    | MarkdownPatterns.SpanNode(s, spans) -> MarkdownPatterns.SpanNode(s, List.map (downloadSpanImages (saver, links)) spans)
    | MarkdownPatterns.SpanLeaf(l) -> MarkdownPatterns.SpanLeaf(l)

  static let rec downloadImages ctx (pars:MarkdownParagraphs) : MarkdownParagraphs =
    pars |> List.map (function
      | MarkdownPatterns.ParagraphSpans(s, spans) ->
          MarkdownPatterns.ParagraphSpans(s, List.map (downloadSpanImages ctx) spans)
      | MarkdownPatterns.ParagraphNested(o, pars) ->
          MarkdownPatterns.ParagraphNested(o, List.map (downloadImages ctx) pars)
      | MarkdownPatterns.ParagraphLeaf p -> MarkdownPatterns.ParagraphLeaf p )


  static let parsingContext formatAgent evaluator fscOptions definedSymbols =
    let definedSymbols = defaultArg definedSymbols []
    let extraDefines =
        [ // When formatting for tooltips or executing snippets we always include the 'prepare' define.
          // This allows (*** condition: prepare ***) for code elements that are only active
          // when formatting or executing.
          "prepare"
        ]
    let agent =
      match formatAgent with
      | Some agent -> agent
      | _ -> CodeFormat.CreateAgent()
    { FormatAgent = agent
      CompilerOptions = fscOptions
      Evaluator = evaluator
      ConditionalDefines = (definedSymbols@extraDefines) }

  /// Get default output file name, given various information
  static let defaultOutput outputPath input (outputKind: OutputKind) =
    match outputPath  with
    | Some out -> out
    | None -> Path.ChangeExtension(input, outputKind.Extension)

  static let customizeDoc customizeDocument ctx doc =
    match customizeDocument with
    | Some c -> c ctx doc
    | None -> doc

  static let downloadImagesForDoc imageSaver (doc: LiterateDocument) =
    match imageSaver with
    | None -> doc
    | Some saver ->
        let pars =
            doc.Paragraphs
            |> downloadImages (saver, doc.DefinedLinks)

        doc.With(paragraphs = pars)

  /// Parse F# Script file
  static member ParseAndCheckScriptFile (path, ?formatAgent, ?fscOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator fscOptions definedSymbols
    ParseScript(parseOptions, ctx).ParseAndCheckScriptFile path (File.ReadAllText path)
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets path ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse F# Script file
  static member ParseScriptString (content, ?path, ?formatAgent, ?fscOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator fscOptions definedSymbols
    ParseScript(parseOptions, ctx).ParseAndCheckScriptFile (defaultArg path "C:\\Document.fsx") content
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets (defaultArg path "C:\\Document.fsx") ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse Markdown document
  static member ParseMarkdownFile(path, ?formatAgent, ?fscOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator fscOptions definedSymbols
    ParseMarkdown.parseMarkdown path (File.ReadAllText path) parseOptions
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets path ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse Markdown document
  static member ParseMarkdownString
    (content, ?path, ?formatAgent, ?fscOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator fscOptions definedSymbols
    ParseMarkdown.parseMarkdown (defaultArg path "C:\\Document.md") content parseOptions
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets (defaultArg path "C:\\Document.md") ctx
    |> Transformations.evaluateCodeSnippets ctx

  // ------------------------------------------------------------------------------------
  // Simple writing functions
  // ------------------------------------------------------------------------------------

  /// Format the literate document as HTML without using a template
  static member ToHtml(doc:LiterateDocument, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext OutputKind.Html prefix lineNumbers generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    HtmlFormatting.formatMarkdown wr ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs
    sb.ToString()

  /// Write the literate document as HTML without using a template
  static member WriteHtml(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext OutputKind.Html prefix lineNumbers generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    HtmlFormatting.formatMarkdown writer ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs

  /// Format the literate document as Latex without using a template
  static member ToLatex(doc:LiterateDocument, ?prefix, ?lineNumbers, ?generateAnchors) =
    let ctx = formattingContext OutputKind.Latex prefix lineNumbers generateAnchors None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks))

  /// Write the literate document as Latex without using a template
  static member WriteLatex(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors) =
    let ctx = formattingContext OutputKind.Latex prefix lineNumbers  generateAnchors None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), writer)

  /// Formate the literate document as an iPython notebook 
  static member ToPynb(doc:LiterateDocument, ?substitutions) =
    let ctx = formattingContext OutputKind.Pynb None None None substitutions None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToPynb(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), ?substitutions=substitutions)

  /// Formate the literate document as an .fsx script 
  static member ToFsx(doc:LiterateDocument, ?substitutions) =
    let ctx = formattingContext OutputKind.Fsx None None None substitutions None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToFsx(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), ?substitutions=substitutions)

  /// Replace literate paragraphs with plain paragraphs
  static member FormatLiterateNodes(doc:LiterateDocument, ?outputKind, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let outputKind = defaultArg outputKind OutputKind.Html
    let ctx = formattingContext outputKind prefix lineNumbers generateAnchors None tokenKindToCss
    Transformations.replaceLiterateParagraphs ctx doc

  /// Process the given literate document
  static member internal TransformDocument
    (doc, outputPath, ?outputKind, ?prefix, ?lineNumbers, ?generateAnchors, ?substitutions, ?tokenKindToCss) =
    let outputKind = defaultArg outputKind OutputKind.Html
    let ctx = formattingContext outputKind prefix lineNumbers generateAnchors substitutions tokenKindToCss
    Formatting.transformDocument doc outputPath ctx

  /// Parse and transform a markdown document
  static member internal ParseAndTransformMarkdownFile
    (input, ?output, ?outputKind, ?formatAgent, ?prefix, ?fscOptions,
      ?lineNumbers, ?references, ?substitutions, ?generateAnchors,
      ?customizeDocument, ?tokenKindToCss, ?imageSaver) =

    let outputKind = defaultArg outputKind OutputKind.Html
    let parseOptions =
        match outputKind with
        | OutputKind.Fsx 
        | OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther
        | _ -> MarkdownParseOptions.None

    let doc = Literate.ParseMarkdownFile (input, ?formatAgent=formatAgent, ?fscOptions=fscOptions, ?references=references, parseOptions=parseOptions)
    let ctx = formattingContext outputKind prefix lineNumbers generateAnchors substitutions tokenKindToCss
    let doc = customizeDoc customizeDocument ctx doc
    let doc = downloadImagesForDoc imageSaver doc
    
    let outputPath = defaultOutput output input outputKind
    Formatting.transformDocument doc outputPath ctx

  /// Parse and transform an F# script file
  static member internal ParseAndTransformScriptFile
    (input, ?output, ?outputKind, ?formatAgent, ?prefix, ?fscOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?substitutions,
      ?generateAnchors, ?customizeDocument, ?tokenKindToCss, ?imageSaver) =

    let parseOptions =
        match outputKind with
        | Some OutputKind.Fsx 
        | Some OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther
        | _ -> MarkdownParseOptions.None

    let outputKind = defaultArg outputKind OutputKind.Html
    let doc = Literate.ParseAndCheckScriptFile (input, ?formatAgent=formatAgent, ?fscOptions=fscOptions, ?references=references, ?fsiEvaluator = fsiEvaluator, parseOptions=parseOptions)
    let ctx = formattingContext outputKind prefix lineNumbers generateAnchors substitutions tokenKindToCss
    let doc = customizeDoc customizeDocument ctx doc
    let doc = downloadImagesForDoc imageSaver doc
    let outputPath = defaultOutput output input outputKind
    Formatting.transformDocument doc outputPath ctx

  /// Write a document object into HTML or another output kind
  static member TransformAndOutputDocument
    (doc, output, ?template, ?outputKind, ?prefix, ?lineNumbers, ?generateAnchors, ?substitutions) =
      let res =
          Literate.TransformDocument
              (doc, output, ?outputKind=outputKind, ?prefix=prefix, ?lineNumbers=lineNumbers,
               ?generateAnchors=generateAnchors, ?substitutions=substitutions)
      SimpleTemplating.UseFileAsSimpleTemplate(res.Substitutions, template, output)

  /// Convert a markdown file into HTML or another output kind
  static member ConvertMarkdownFile
    (input, ?template, ?output, ?outputKind, ?formatAgent, ?prefix, ?fscOptions,
      ?lineNumbers, ?references, ?substitutions, ?generateAnchors
      (* ?customizeDocument, *) ) =

      let outputKind = defaultArg outputKind OutputKind.Html
      let output = defaultOutput output input outputKind
      let res =
          Literate.ParseAndTransformMarkdownFile
              (input, output=output, outputKind=outputKind, ?formatAgent=formatAgent, ?prefix=prefix, ?fscOptions=fscOptions,
               ?lineNumbers=lineNumbers, ?references=references, ?generateAnchors=generateAnchors,
               ?substitutions=substitutions (* ?customizeDocument=customizeDocument, *))
      SimpleTemplating.UseFileAsSimpleTemplate(res.Substitutions, template, output)

  /// <summary>Convert a script file into HTML or another output kind</summary>
  /// <example>
  ///   <code>
  ///     Literate.ConvertScriptFile("script.fsx", template)
  ///   </code>
  /// </example>
  static member ConvertScriptFile
    (input, ?template, ?output, ?outputKind, ?formatAgent, ?prefix, ?fscOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?substitutions,
      ?generateAnchors (* ?customizeDocument, *)) =

        let outputKind = defaultArg outputKind OutputKind.Html
        let output=defaultOutput output input outputKind
        let res =
            Literate.ParseAndTransformScriptFile
                (input, output=output, outputKind=outputKind, ?formatAgent=formatAgent, ?prefix=prefix, ?fscOptions=fscOptions,
                 ?lineNumbers=lineNumbers, ?references=references, ?generateAnchors=generateAnchors,
                 ?substitutions=substitutions, (* ?customizeDocument=customizeDocument, *) ?fsiEvaluator=fsiEvaluator)
        SimpleTemplating.UseFileAsSimpleTemplate(res.Substitutions, template, output)


[<assembly: InternalsVisibleTo("fsdocs");
  assembly: InternalsVisibleTo("FSharp.Formatting.TestHelpers")>]
do()
