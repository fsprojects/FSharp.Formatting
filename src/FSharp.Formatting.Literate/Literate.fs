namespace FSharp.Formatting.Literate

open System
open System.IO
open System.Reflection
open FSharp.Formatting.Markdown
open FSharp.Formatting.CodeFormat

// --------------------------------------------------------------------------------------
// Public API
// --------------------------------------------------------------------------------------

/// This type provides three simple methods for calling the literate programming tool.
/// The `ProcessMarkdown` and `ProcessScriptFile` methods process a single Markdown document
/// and F# script, respectively. The `ProcessDirectory` method handles an entire directory tree
/// (looking for `*.fsx` and `*.md` files).
type Literate private () =

  /// Build default options context for formatting literate document
  static let formattingContext format prefix lineNumbers includeSource generateAnchors replacements tokenKindToCss =
    { Replacements = defaultArg replacements []
      GenerateLineNumbers = defaultArg lineNumbers true
      IncludeSource = defaultArg includeSource false
      Prefix = defaultArg prefix "fs"
      OutputKind = defaultArg format OutputKind.Html
      GenerateHeaderAnchors = defaultArg generateAnchors false
      TokenKindToCss = tokenKindToCss
    }

  /// Build default options context for parsing literate scripts/documents
  static let parsingContext formatAgent evaluator compilerOptions definedSymbols =
    let agent =
      match formatAgent with
      | Some agent -> agent
      | _ -> CodeFormat.CreateAgent()
    { FormatAgent = agent
      CompilerOptions = compilerOptions
      Evaluator = evaluator
      DefinedSymbols = Option.map (String.concat ",") definedSymbols }

  /// Get default output file name, given various information
  static let defaultOutput output input kind =
    match output, defaultArg kind OutputKind.Html with
    | Some out, _ -> out
    | _, OutputKind.Latex -> Path.ChangeExtension(input, "tex")
    | _, OutputKind.Html -> Path.ChangeExtension(input, "html")
    | _, OutputKind.Pynb -> Path.ChangeExtension(input, "ipynb")

  /// Apply the specified transformations to a document
  static let transform references doc =
    let doc =
      if references <> Some true then doc
      else Transformations.generateReferences doc
    doc

  static let customize customizeDocument ctx doc =
    match customizeDocument with
    | Some c -> c ctx doc
    | None -> doc

  static let replaceParameters (contentTag:string) (parameters:seq<string * string>) input =
    match input with
    | None ->
        // If there is no template, return just document + tooltips
        let lookup = parameters |> dict
        lookup.[contentTag] + "\n\n" + lookup.["tooltips"]
    | Some input ->
        // First replace keys with some uglier keys and then replace them with values
        // (in case one of the keys appears in some other value)
        let id = System.Guid.NewGuid().ToString("d")
        let input = parameters |> Seq.fold (fun (html:string) (key, value) ->
          html.Replace("{" + key + "}", "{" + key + id + "}")) input
        let result = parameters |> Seq.fold (fun (html:string) (key, value) ->
          html.Replace("{" + key + id + "}", value)) input
        result

  static member GenerateFile (references, contentTag, parameters, templateOpt, output, layoutRoots) =
    let templateOpt = templateOpt |> Option.map File.ReadAllText
    File.WriteAllText(output, replaceParameters contentTag parameters templateOpt)

  // ------------------------------------------------------------------------------------
  // Parsing functions
  // ------------------------------------------------------------------------------------

  /// Parse F# Script file
  static member ParseScriptFile
    ( path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator ) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseScript.parseScriptFile path (File.ReadAllText path) ctx
    |> transform references
    |> Transformations.formatCodeSnippets path ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse F# Script file
  static member ParseScriptString
    ( content, ?path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator ) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseScript.parseScriptFile (defaultArg path "C:\\Document.fsx") content ctx
    |> transform references
    |> Transformations.formatCodeSnippets (defaultArg path "C:\\Document.fsx") ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse Markdown document
  static member ParseMarkdownFile
    ( path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator ) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseMarkdown.parseMarkdown path (File.ReadAllText path)
    |> transform references
    |> Transformations.formatCodeSnippets path ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse Markdown document
  static member ParseMarkdownString
    ( content, ?path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator ) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseMarkdown.parseMarkdown (defaultArg path "C:\\Document.md") content
    |> transform references
    |> Transformations.formatCodeSnippets (defaultArg path "C:\\Document.md") ctx
    |> Transformations.evaluateCodeSnippets ctx

  // ------------------------------------------------------------------------------------
  // Simple writing functions
  // ------------------------------------------------------------------------------------

  /// Format the literate document as HTML without using a template
  static member ToHtml(doc:LiterateDocument, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext (Some OutputKind.Html) prefix lineNumbers None generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    Html.formatMarkdown wr ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs
    sb.ToString()

  /// Write the literate document as HTML without using a template
  static member WriteHtml(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext (Some OutputKind.Html) prefix lineNumbers None generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    Html.formatMarkdown writer ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs

  /// Format the literate document as Latex without using a template
  static member ToLatex(doc:LiterateDocument, ?prefix, ?lineNumbers, ?generateAnchors) =
    let ctx = formattingContext (Some OutputKind.Latex) prefix lineNumbers None generateAnchors None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks))

  /// Write the literate document as Latex without using a template
  static member WriteLatex(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors) =
    let ctx = formattingContext (Some OutputKind.Latex) prefix lineNumbers None generateAnchors None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), writer)

  /// Formate the literate document as an iPython notebook without using a template
  static member ToPynb(doc:LiterateDocument) =
    let ctx = formattingContext (Some OutputKind.Pynb) None None None None None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToPynb(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks))

  //static member WritePynb(doc:LiterateDocument, writer:TextWriter) =
  //  let ctx = formattingContext (Some OutputKind.Pynb) None None None None None None
  //  let doc = Transformations.replaceLiterateParagraphs ctx doc
  //  Markdown.WritePynb(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), writer)

  // ------------------------------------------------------------------------------------
  // Replace literate paragraphs with plain paragraphs
  // ------------------------------------------------------------------------------------

  static member FormatLiterateNodes(doc:LiterateDocument, ?format, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext format prefix lineNumbers None generateAnchors None tokenKindToCss
    Transformations.replaceLiterateParagraphs ctx doc

  // ------------------------------------------------------------------------------------
  // Processing functions that handle templating etc.
  // ------------------------------------------------------------------------------------

  /// Process the given literate document
  static member GenerateReplacementsForDocument
    ( doc, output, ?format, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?replacements, ?tokenKindToCss) =
    let ctx = formattingContext format prefix lineNumbers includeSource generateAnchors replacements tokenKindToCss
    Templating.processFile doc output ctx

  /// Process Markdown document
  static member GenerateReplacementsForMarkdown
    ( input, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?generateAnchors, ?customizeDocument, ?tokenKindToCss ) =
    let doc =
      Literate.ParseMarkdownFile
        ( input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions,
          ?references = references )
    let ctx = formattingContext format prefix lineNumbers includeSource generateAnchors replacements tokenKindToCss
    let doc = customize customizeDocument ctx doc
    Templating.processFile doc (defaultOutput output input format) ctx

  /// Process F# Script file
  static member GenerateReplacementsForScriptFile
    ( input,?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource,
      ?generateAnchors, ?customizeDocument, ?tokenKindToCss ) =
    let doc =
      Literate.ParseScriptFile
        ( input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions,
          ?references = references, ?fsiEvaluator = fsiEvaluator )
    let ctx = formattingContext format prefix lineNumbers includeSource generateAnchors replacements tokenKindToCss
    let doc = customize customizeDocument ctx doc
    Templating.processFile doc (defaultOutput output input format) ctx

  /// Process directory containing a mix of Markdown documents and F# Script files
  static member GenerateReplacementsForDirectory
    ( inputDirectory, ?outputDirectory, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?generateAnchors, ?processRecursive, ?customizeDocument, ?tokenKindToCss ) =

    let processRecursive = defaultArg processRecursive true

    // Call one or the other process function with all the arguments
    let processScriptFile file output =
      Literate.GenerateReplacementsForScriptFile
        ( file, output = output, ?format = format,
          ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
          ?lineNumbers = lineNumbers, ?references = references, ?fsiEvaluator = fsiEvaluator, ?replacements = replacements,
          ?includeSource = includeSource, ?generateAnchors = generateAnchors,
          ?customizeDocument = customizeDocument, ?tokenKindToCss = tokenKindToCss )

    let processMarkdown file output =
      Literate.GenerateReplacementsForMarkdown
        ( file, output = output, ?format = format,
          ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
          ?lineNumbers = lineNumbers, ?references = references, ?replacements = replacements,
          ?includeSource = includeSource, ?generateAnchors = generateAnchors, ?customizeDocument = customizeDocument, ?tokenKindToCss = tokenKindToCss)

    /// Recursively process all files in the directory tree
    let rec processDirectory indir outdir =
      // Create output directory if it does not exist
      if Directory.Exists(outdir) |> not then
        try Directory.CreateDirectory(outdir) |> ignore
        with _ -> failwithf "Cannot create directory '%s'" outdir

      let fsx = [ for f in Directory.GetFiles(indir, "*.fsx") -> processScriptFile, f ]
      let mds = [ for f in Directory.GetFiles(indir, "*.md") -> processMarkdown, f ]
      let res =
        [ for func, file in fsx @ mds do
            let name = Path.GetFileNameWithoutExtension(file)
            let ext = (match format with Some OutputKind.Latex -> "tex" | _ -> "html")
            let output = Path.Combine(outdir, sprintf "%s.%s" name ext)

            // Update only when needed
            let changeTime = File.GetLastWriteTime(file)
            let generateTime = File.GetLastWriteTime(output)
            if changeTime > generateTime then
              yield output,(func file output) ]
      let resRec =
        [ if processRecursive then
            for d in Directory.EnumerateDirectories(indir) do
              let name = Path.GetFileName(d)
              yield! processDirectory (Path.Combine(indir, name)) (Path.Combine(outdir, name))
        ]
      res @ resRec


    let outputDirectory = defaultArg outputDirectory inputDirectory
    processDirectory inputDirectory outputDirectory

  static member ProcessDocument
    ( doc, output, ?templateFile, ?format, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?replacements, ?layoutRoots, ?assemblyReferences) =
      let res =
          Literate.GenerateReplacementsForDocument
              (doc, output, ?format = format, ?prefix = prefix, ?lineNumbers = lineNumbers,
               ?includeSource = includeSource, ?generateAnchors = generateAnchors, ?replacements = replacements)
      Literate.GenerateFile(assemblyReferences, res.ContentTag, res.Parameters, templateFile, output, defaultArg layoutRoots [])

  static member ProcessMarkdown
    ( input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?layoutRoots, ?generateAnchors,
      ?assemblyReferences, ?customizeDocument ) =

      let res =
          Literate.GenerateReplacementsForMarkdown
              (input ,?output = output, ?format = format, ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
               ?lineNumbers = lineNumbers, ?references = references, ?includeSource = includeSource, ?generateAnchors = generateAnchors,
               ?replacements = replacements, ?customizeDocument = customizeDocument )
      let output = defaultOutput output input format
      Literate.GenerateFile(assemblyReferences, res.ContentTag, res.Parameters, templateFile, output, defaultArg layoutRoots [])

  static member ProcessScriptFile
    ( input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?layoutRoots,
      ?generateAnchors, ?assemblyReferences, ?customizeDocument ) =
        let res =
            Literate.GenerateReplacementsForScriptFile
                (input ,?output = output, ?format = format, ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                 ?lineNumbers = lineNumbers, ?references = references, ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                 ?replacements = replacements, ?customizeDocument = customizeDocument, ?fsiEvaluator = fsiEvaluator )
        let output = defaultOutput output input format
        Literate.GenerateFile(assemblyReferences, res.ContentTag, res.Parameters, templateFile, output, defaultArg layoutRoots [])

  static member ProcessDirectory
    ( inputDirectory, ?templateFile, ?outputDirectory, ?format, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?layoutRoots, ?generateAnchors,
      ?assemblyReferences, ?processRecursive, ?customizeDocument  ) =
        let outputDirectory = defaultArg outputDirectory inputDirectory

        let res =
            Literate.GenerateReplacementsForDirectory
                (inputDirectory, outputDirectory, ?format = format, ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                 ?lineNumbers = lineNumbers, ?references = references, ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                 ?replacements = replacements, ?customizeDocument = customizeDocument, ?processRecursive = processRecursive, ?fsiEvaluator = fsiEvaluator)

        for (path, res) in res do
            Literate.GenerateFile(assemblyReferences, res.ContentTag, res.Parameters, templateFile, path, defaultArg layoutRoots [])
