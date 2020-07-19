namespace FSharp.Formatting.Literate

open System
open System.IO
open System.Globalization
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.Markdown
open FSharp.Formatting.CodeFormat

// --------------------------------------------------------------------------------------
// Public API
// --------------------------------------------------------------------------------------

/// This type provides three simple methods for calling the literate programming tool.
/// The `ConvertMarkdown` and `ConvertScriptFile` methods process a single Markdown document
/// and F# script, respectively. The `ConvertDirectory` method handles an entire directory tree
/// (looking for `*.fsx` and `*.md` files).
type Literate private () =

  /// Build default options context for formatting literate document
  static let formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss =
    let outputKind = defaultArg outputKind OutputKind.Html
    let defines = [ outputKind.Extension ]
    { Replacements = defaultArg parameters []
      GenerateLineNumbers = defaultArg lineNumbers true
      IncludeSource = defaultArg includeSource false
      Prefix = defaultArg prefix "fs"
      ConditionalDefines = defines
      OutputKind = outputKind
      GenerateHeaderAnchors = defaultArg generateAnchors false
      TokenKindToCss = tokenKindToCss
    }

  /// Build default options context for parsing literate scripts/documents
  static let parsingContext formatAgent evaluator compilerOptions definedSymbols =
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
      CompilerOptions = compilerOptions
      Evaluator = evaluator
      ConditionalDefines = (definedSymbols@extraDefines) }

  /// Get default output file name, given various information
  static let defaultOutput output input kind =
    match output, defaultArg kind OutputKind.Html with
    | Some out, _ -> out
    | _, outputKind -> Path.ChangeExtension(input, outputKind.Extension)

  static let customize customizeDocument ctx doc =
    match customizeDocument with
    | Some c -> c ctx doc
    | None -> doc

  // ------------------------------------------------------------------------------------
  // Parsing functions
  // ------------------------------------------------------------------------------------

  /// Parse F# Script file
  static member ParseScriptFile (path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseScript(parseOptions, ctx).ParseScriptFile path (File.ReadAllText path)
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets path ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse F# Script file
  static member ParseScriptString (content, ?path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseScript(parseOptions, ctx).ParseScriptFile (defaultArg path "C:\\Document.fsx") content
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets (defaultArg path "C:\\Document.fsx") ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse Markdown document
  static member ParseMarkdownFile(path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseMarkdown.parseMarkdown path (File.ReadAllText path) parseOptions
    |> Transformations.generateReferences references
    |> Transformations.formatCodeSnippets path ctx
    |> Transformations.evaluateCodeSnippets ctx

  /// Parse Markdown document
  static member ParseMarkdownString
    (content, ?path, ?formatAgent, ?compilerOptions, ?definedSymbols, ?references, ?fsiEvaluator, ?parseOptions) =
    let ctx = parsingContext formatAgent fsiEvaluator compilerOptions definedSymbols
    ParseMarkdown.parseMarkdown (defaultArg path "C:\\Document.md") content parseOptions
    |> Transformations.generateReferences references
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
    HtmlFormatting.formatMarkdown wr ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs
    sb.ToString()

  /// Write the literate document as HTML without using a template
  static member WriteHtml(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext (Some OutputKind.Html) prefix lineNumbers None generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    HtmlFormatting.formatMarkdown writer ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs

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

  /// Formate the literate document as an iPython notebook 
  static member ToPynb(doc:LiterateDocument, ?parameters) =
    let ctx = formattingContext (Some OutputKind.Pynb) None None None None parameters None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToPynb(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), ?parameters=parameters)

  /// Formate the literate document as an .fsx script 
  static member ToFsx(doc:LiterateDocument, ?parameters) =
    let ctx = formattingContext (Some OutputKind.Fsx) None None None None parameters None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToFsx(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), ?parameters=parameters)

  /// Replace literate paragraphs with plain paragraphs
  static member FormatLiterateNodes(doc:LiterateDocument, ?outputKind, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext outputKind prefix lineNumbers None generateAnchors None tokenKindToCss
    Transformations.replaceLiterateParagraphs ctx doc

  /// Process the given literate document
  static member internal TransformDocument
    (doc, output, ?outputKind, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?parameters, ?tokenKindToCss) =
    let ctx = formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss
    Formatting.transformDocument doc output ctx

  /// Parse and transform a markdown document
  static member internal ParseAndTransformMarkdown
    (input, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?parameters, ?includeSource, ?generateAnchors, ?customizeDocument, ?tokenKindToCss) =

    let parseOptions =
        match outputKind with
        | Some OutputKind.Fsx 
        | Some OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther
        | _ -> MarkdownParseOptions.None

    let doc = Literate.ParseMarkdownFile (input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions, ?references=references, parseOptions=parseOptions)
    let ctx = formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss
    let doc = customize customizeDocument ctx doc
    Formatting.transformDocument doc (defaultOutput output input outputKind) ctx

  /// Parse and transform an F# script file
  static member internal ParseAndTransformScriptFile
    (input, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?parameters, ?includeSource,
      ?generateAnchors, ?customizeDocument, ?tokenKindToCss) =

    let parseOptions =
        match outputKind with
        | Some OutputKind.Fsx 
        | Some OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther
        | _ -> MarkdownParseOptions.None

    let doc = Literate.ParseScriptFile (input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions, ?references=references, ?fsiEvaluator = fsiEvaluator, parseOptions=parseOptions)
    let ctx = formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss
    let doc = customize customizeDocument ctx doc
    Formatting.transformDocument doc (defaultOutput output input outputKind) ctx

  /// Convert a document file into HTML or another output kind
  static member ConvertDocument
    (doc, output, ?template, ?outputKind, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?parameters) =
      let res =
          Literate.TransformDocument
              (doc, output, ?outputKind=outputKind, ?prefix=prefix, ?lineNumbers=lineNumbers,
               ?includeSource=includeSource, ?generateAnchors=generateAnchors, ?parameters=parameters)
      HtmlFile.UseFileAsSimpleTemplate(res.ContentTag, res.Parameters, template, output)

  /// Convert a markdown file into HTML or another output kind
  static member ConvertMarkdown
    (input, ?template, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?parameters, ?includeSource, ?generateAnchors,
      ?customizeDocument) =

      let res =
          Literate.ParseAndTransformMarkdown
              (input ,?output=output, ?outputKind=outputKind, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
               ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
               ?parameters=parameters, ?customizeDocument=customizeDocument)
      let output=defaultOutput output input outputKind
      HtmlFile.UseFileAsSimpleTemplate(res.ContentTag, res.Parameters, template, output)

  /// Convert a script file into HTML or another output kind
  static member ConvertScriptFile
    (input, ?template, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?parameters, ?includeSource,
      ?generateAnchors, ?customizeDocument) =
        let res =
            Literate.ParseAndTransformScriptFile
                (input ,?output=output, ?outputKind=outputKind, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
                 ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
                 ?parameters=parameters, ?customizeDocument=customizeDocument, ?fsiEvaluator=fsiEvaluator)
        let output=defaultOutput output input outputKind
        HtmlFile.UseFileAsSimpleTemplate(res.ContentTag, res.Parameters, template, output)

  /// Convert markdown, script and other content into a static site
  static member ConvertDirectory
    (inputDirectory, ?htmlTemplate, ?outputDirectory, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?parameters, ?includeSource, ?generateAnchors,
      ?recursive, ?customizeDocument, ?tokenKindToCss) =
        let outputDirectory=defaultArg outputDirectory inputDirectory
        let recursive = defaultArg recursive true

        let processFile (inputFile: string) outputKind template outputDirectory =
            let name = Path.GetFileName(inputFile)
            if name.StartsWith(".") then 
                printfn "skipping file %s" inputFile
            elif name.StartsWith "_template." then 
                ()
            else
                let isFsx = inputFile.EndsWith(".fsx", true, CultureInfo.InvariantCulture) 
                let isMd = inputFile.EndsWith(".md", true, CultureInfo.InvariantCulture)

                // A _template.tex or _template.pynb is needed to generate those files
                match outputKind, template with
                | OutputKind.Pynb, None -> ()
                | OutputKind.Latex, None -> ()
                | OutputKind.Fsx, None -> ()
                | _ ->
                let ext = outputKind.Extension
                let outputFile =
                    if isFsx || isMd then
                        let basename = Path.GetFileNameWithoutExtension(inputFile)
                        Path.Combine(outputDirectory, sprintf "%s.%s" basename ext)
                    else
                        Path.Combine(outputDirectory, name)

                // Update only when needed - template or file has changed
                let fileChangeTime = File.GetLastWriteTime(inputFile)
                let templateChangeTime = match template with None -> new DateTime(1,1,1) | Some t -> File.GetLastWriteTime(t)
                let changeTime = max fileChangeTime templateChangeTime
                let generateTime = File.GetLastWriteTime(outputFile)
                if changeTime > generateTime then
                    if isFsx then
                        printfn "converting %s --> %s" inputFile outputFile
                        let inputFile = Path.GetFullPath(inputFile)
                        let model =
                          Literate.ParseAndTransformScriptFile
                            (inputFile, output = outputFile, outputKind = outputKind,
                              ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                              ?lineNumbers = lineNumbers, ?references=references, ?fsiEvaluator = fsiEvaluator, ?parameters = parameters,
                              ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                              ?customizeDocument = customizeDocument, ?tokenKindToCss = tokenKindToCss)

                        HtmlFile.UseFileAsSimpleTemplate(model.ContentTag, model.Parameters, template, outputFile)
                    elif isMd then
                        printfn "converting %s --> %s" inputFile outputFile
                        let inputFile = Path.GetFullPath(inputFile)
                        let model =
                          Literate.ParseAndTransformMarkdown
                            (inputFile, output = outputFile, outputKind = outputKind,
                              ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                              ?lineNumbers = lineNumbers, ?references=references, ?parameters = parameters,
                              ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                              ?customizeDocument=customizeDocument, ?tokenKindToCss = tokenKindToCss)
                        HtmlFile.UseFileAsSimpleTemplate(model.ContentTag, model.Parameters, template, outputFile)
                    else 
                        printfn "copying %s --> %s" inputFile outputFile
                        File.Copy(inputFile, outputFile, true)

                /// Recursively process all files in the directory tree
        let rec processDirectory (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate) indir outdir =

          // Look for the presence of the _template.* files to activate the
          // generation of the content.
          let possibleNewHtmlTemplate = Path.Combine(indir, "_template.html")
          let htmlTemplate = if (try File.Exists(possibleNewHtmlTemplate) with _ -> false) then Some possibleNewHtmlTemplate else htmlTemplate
          let possibleNewPynbTemplate = Path.Combine(indir, "_template.ipynb")
          let pynbTemplate = if (try File.Exists(possibleNewPynbTemplate) with _ -> false) then Some possibleNewPynbTemplate else pynbTemplate
          let possibleNewFsxTemplate = Path.Combine(indir, "_template.fsx")
          let fsxTemplate = if (try File.Exists(possibleNewFsxTemplate) with _ -> false) then Some possibleNewFsxTemplate else fsxTemplate
          let possibleNewLatexTemplate = Path.Combine(indir, "_template.tex")
          let texTemplate = if (try File.Exists(possibleNewLatexTemplate) with _ -> false) then Some possibleNewLatexTemplate else texTemplate

          // Create output directory if it does not exist
          if Directory.Exists(outdir) |> not then
            try Directory.CreateDirectory(outdir) |> ignore
            with _ -> failwithf "Cannot create directory '%s'" outdir

          let inputs = Directory.GetFiles(indir, "*") 
          for input in inputs do
            processFile input OutputKind.Html htmlTemplate outdir
            processFile input OutputKind.Latex texTemplate outdir
            processFile input OutputKind.Pynb pynbTemplate outdir
            processFile input OutputKind.Fsx fsxTemplate outdir

          if recursive then
            for subdir in Directory.EnumerateDirectories(indir) do
                let name = Path.GetFileName(subdir)
                if name.StartsWith "." then
                    printfn "skipping directory %s" subdir
                else
                    processDirectory (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate) (Path.Combine(indir, name)) (Path.Combine(outdir, name))

        processDirectory (htmlTemplate, None, None, None) inputDirectory outputDirectory

