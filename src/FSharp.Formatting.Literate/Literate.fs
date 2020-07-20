namespace FSharp.Formatting.Literate

open System
open System.Collections.Generic
open System.IO
open System.Globalization
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.Markdown
open FSharp.Formatting.CodeFormat

// --------------------------------------------------------------------------------------
// Public API
// --------------------------------------------------------------------------------------

/// This type provides three simple methods for calling the literate programming tool.
/// The `ConvertMarkdownFile` and `ConvertScriptFile` methods process a single Markdown document
/// and F# script, respectively. The `ConvertDirectory` method handles an entire directory tree
/// (looking for `*.fsx` and `*.md` files).
type Literate private () =

  /// Build default options context for formatting literate document
  static let formattingContext (outputKind: OutputKind) prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss =
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


  //let addHtmlPrinter = """
  //  module FsInteractiveService = 
  //    let mutable htmlPrinters = []
  //    let tryFormatHtml o = htmlPrinters |> Seq.tryPick (fun f -> f o)
  //    let htmlPrinterParams = System.Collections.Generic.Dictionary<string, obj>()
  //    do htmlPrinterParams.["html-standalone-output"] <- @html-standalone-output

  //  type __ReflectHelper.ForwardingInteractiveSettings with
  //    member x.HtmlPrinterParameters = FsInteractiveService.htmlPrinterParams
  //    member x.AddHtmlPrinter<'T>(f:'T -> seq<string * string> * string) = 
  //      FsInteractiveService.htmlPrinters <- (fun (value:obj) ->
  //        match value with
  //        | :? 'T as value -> Some(f value)
  //        | _ -> None) :: FsInteractiveService.htmlPrinters"""


  /// Create FSI evaluator - this loads `addHtmlPrinter` and calls the registered
  /// printers when processing outputs. Printed <head> elements are added to the
  /// returned ResizeArray
  //let createFsiEvaluator ctx = 
  //  try
  //    let addHtmlPrinter = addHtmlPrinter.Replace("@html-standalone-output", if ctx.Standalone then "true" else "false")
  //    match (fsi :> IFsiEvaluator).Evaluate(addHtmlPrinter, false, None) with
  //    | :? FsiEvaluationResult as res when res.ItValue.IsSome -> ()
  //    | _ -> failwith "Evaluating addHtmlPrinter code failed"
  //  with e ->
  //    printfn "%A" e
  //    reraise ()

  //  let tryFormatHtml =
  //    match (fsi :> IFsiEvaluator).Evaluate("(FsInteractiveService.tryFormatHtml : obj -> option<seq<string*string>*string>)", true, None) with
  //    | :? FsiEvaluationResult as res -> 
  //        let func = unbox<obj -> option<seq<string*string>*string>> (fst res.Result.Value)
  //        fun (o:obj) -> func o
  //    | _ -> failwith "Failed to get tryFormatHtml function"

    //let head = new ResizeArray<_>()
    //fsi.RegisterTransformation(fun (o, _t) ->
    //  match tryFormatHtml o with
    //  | Some (args, html) -> 
    //      for _k, v in args do if not (head.Contains(v)) then head.Add(v)
    //      Some [InlineBlock("<div class=\"fslab-html-output\">" + html + "</div>")]
    //  | None -> None )

    //fsi :> IFsiEvaluator, head

  // Find or generate a default file that we want to show in browser
  //static let getDefaultFile ctx = function
  //  | [] -> failwith "No script files found!"
  //  | [file, _] -> file // If there is just one file, return it
  //  | generated ->
  //      // If there is custom default or index file, use it
  //      let existingDefault =
  //        Directory.GetFiles(ctx.Root) |> Seq.tryPick (fun f ->
  //          match Path.GetFileNameWithoutExtension(f).ToLower() with
  //          | "default" | "index" -> Some(Path.GetFileNameWithoutExtension(f) + ".html")
  //          | _ -> None)
  //      match existingDefault with
  //      | None ->
  //          // Otherwise, generate simple page with list of all files
  //          let items =
  //            [ for file, title in generated ->
  //                [Paragraph [ DirectLink([Literal title], (file,None)) ]] ]
  //          let pars =
  //            [ Heading(1, [Literal "FSharp Literate Scripts"])
  //              ListBlock(Unordered, items) ]
  //          let doc = LiterateDocument(pars, "", dict[], LiterateSource.Markdown "", "", Seq.empty)
  //          generateFile ctx (ctx.Output @@ "index.html") doc "FSharp Literate Scripts" ""
  //          "index.html"
  //      | Some fn -> fn

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
  static let defaultOutput output input (outputKind: OutputKind) =
    match output  with
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

  static let ensureDirectory path =
    let dir = DirectoryInfo(path)
    if not dir.Exists then dir.Create()

  static let createImageSaver (outputDirectory) =
        // Download images so that they can be embedded
        let wc = new System.Net.WebClient()
        let mutable counter = 0
        fun (url:string) ->
            if url.StartsWith("http") || url.StartsWith("https") then
                counter <- counter + 1
                let ext = Path.GetExtension(url)
                let fn = sprintf "%s/savedimages/saved%d%s" outputDirectory counter ext

                ensureDirectory (sprintf "%s/savedimages" outputDirectory)
                printfn "downloading %s --> %s" url fn
                wc.DownloadFile(url, fn)
                fn
            else url

  static let defaultImageSaver imageSaver outputKind saveImages =
    match outputKind with
    | OutputKind.Pynb when saveImages <> Some false -> Some (imageSaver())
    | OutputKind.Latex when saveImages <> Some false -> Some (imageSaver())
    | OutputKind.Fsx when saveImages = Some true -> Some (imageSaver())
    | OutputKind.Html when saveImages = Some true -> Some (imageSaver())
    | _ -> None

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
    let ctx = formattingContext OutputKind.Html prefix lineNumbers None generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    let sb = new System.Text.StringBuilder()
    use wr = new StringWriter(sb)
    HtmlFormatting.formatMarkdown wr ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs
    sb.ToString()

  /// Write the literate document as HTML without using a template
  static member WriteHtml(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let ctx = formattingContext OutputKind.Html prefix lineNumbers None generateAnchors None tokenKindToCss
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let doc = MarkdownDocument(doc.Paragraphs @ [InlineBlock(doc.FormattedTips, None, None)], doc.DefinedLinks)
    HtmlFormatting.formatMarkdown writer ctx.GenerateHeaderAnchors Environment.NewLine true doc.DefinedLinks doc.Paragraphs

  /// Format the literate document as Latex without using a template
  static member ToLatex(doc:LiterateDocument, ?prefix, ?lineNumbers, ?generateAnchors) =
    let ctx = formattingContext OutputKind.Latex prefix lineNumbers None generateAnchors None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks))

  /// Write the literate document as Latex without using a template
  static member WriteLatex(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers, ?generateAnchors) =
    let ctx = formattingContext OutputKind.Latex prefix lineNumbers None generateAnchors None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), writer)

  /// Formate the literate document as an iPython notebook 
  static member ToPynb(doc:LiterateDocument, ?parameters) =
    let ctx = formattingContext OutputKind.Pynb None None None None parameters None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToPynb(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), ?parameters=parameters)

  /// Formate the literate document as an .fsx script 
  static member ToFsx(doc:LiterateDocument, ?parameters) =
    let ctx = formattingContext OutputKind.Fsx None None None None parameters None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.ToFsx(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), ?parameters=parameters)

  /// Replace literate paragraphs with plain paragraphs
  static member FormatLiterateNodes(doc:LiterateDocument, ?outputKind, ?prefix, ?lineNumbers, ?generateAnchors, ?tokenKindToCss) =
    let outputKind = defaultArg outputKind OutputKind.Html
    let ctx = formattingContext outputKind prefix lineNumbers None generateAnchors None tokenKindToCss
    Transformations.replaceLiterateParagraphs ctx doc

  /// Process the given literate document
  static member internal TransformDocument
    (doc, output, ?outputKind, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?parameters, ?tokenKindToCss) =
    let outputKind = defaultArg outputKind OutputKind.Html
    let ctx = formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss
    Formatting.transformDocument doc output ctx

  /// Parse and transform a markdown document
  static member internal ParseAndTransformMarkdownFile
    (input, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?parameters, ?includeSource, ?generateAnchors,
      ?customizeDocument, ?tokenKindToCss, ?imageSaver) =

    let outputKind = defaultArg outputKind OutputKind.Html
    let parseOptions =
        match outputKind with
        | OutputKind.Fsx 
        | OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther
        | _ -> MarkdownParseOptions.None

    let doc = Literate.ParseMarkdownFile (input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions, ?references=references, parseOptions=parseOptions)
    let ctx = formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss
    let doc = customizeDoc customizeDocument ctx doc
    let doc = downloadImagesForDoc imageSaver doc
    
    Formatting.transformDocument doc (defaultOutput output input outputKind) ctx

  /// Parse and transform an F# script file
  static member internal ParseAndTransformScriptFile
    (input, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?parameters, ?includeSource,
      ?generateAnchors, ?customizeDocument, ?tokenKindToCss, ?imageSaver) =

    let parseOptions =
        match outputKind with
        | Some OutputKind.Fsx 
        | Some OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther ||| MarkdownParseOptions.ParseNonCodeAsOther
        | _ -> MarkdownParseOptions.None

    let outputKind = defaultArg outputKind OutputKind.Html
    let doc = Literate.ParseScriptFile (input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions, ?references=references, ?fsiEvaluator = fsiEvaluator, parseOptions=parseOptions)
    let ctx = formattingContext outputKind prefix lineNumbers includeSource generateAnchors parameters tokenKindToCss
    let doc = customizeDoc customizeDocument ctx doc
    let doc = downloadImagesForDoc imageSaver doc
    Formatting.transformDocument doc (defaultOutput output input outputKind) ctx

  /// Write a document object into HTML or another output kind
  static member TransformAndOutputDocument
    (doc, output, ?template, ?outputKind, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?parameters) =
      let res =
          Literate.TransformDocument
              (doc, output, ?outputKind=outputKind, ?prefix=prefix, ?lineNumbers=lineNumbers,
               ?includeSource=includeSource, ?generateAnchors=generateAnchors, ?parameters=parameters)
      HtmlFile.UseFileAsSimpleTemplate(res.ContentTag, res.Parameters, template, output)

  /// Convert a markdown file into HTML or another output kind
  static member ConvertMarkdownFile
    (input, ?template, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?parameters, ?includeSource, ?generateAnchors,
      ?customizeDocument, ?saveImages) =

      let outputKind = defaultArg outputKind OutputKind.Html
      let output=defaultOutput output input outputKind
      let imageSaverOpt = defaultImageSaver (fun () -> createImageSaver output) outputKind saveImages
      let res =
          Literate.ParseAndTransformMarkdownFile
              (input, output=output, outputKind=outputKind, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
               ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
               ?parameters=parameters, ?customizeDocument=customizeDocument,
               ?imageSaver=imageSaverOpt)
      HtmlFile.UseFileAsSimpleTemplate(res.ContentTag, res.Parameters, template, output)

  /// Convert a script file into HTML or another output kind
  static member ConvertScriptFile
    (input, ?template, ?output, ?outputKind, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?parameters, ?includeSource,
      ?generateAnchors, ?customizeDocument, ?saveImages) =

        let outputKind = defaultArg outputKind OutputKind.Html
        let output=defaultOutput output input outputKind
        let imageSaverOpt = defaultImageSaver (fun () -> createImageSaver output) outputKind saveImages
        let res =
            Literate.ParseAndTransformScriptFile
                (input, output=output, outputKind=outputKind, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
                 ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
                 ?parameters=parameters, ?customizeDocument=customizeDocument, ?fsiEvaluator=fsiEvaluator,
                 ?imageSaver=imageSaverOpt)
        HtmlFile.UseFileAsSimpleTemplate(res.ContentTag, res.Parameters, template, output)

  /// Convert markdown, script and other content into a static site
  static member ConvertDirectory
    (inputDirectory, ?htmlTemplate, ?outputDirectory, ?formatAgent, ?prefix, ?compilerOptions,
      ?lineNumbers, ?references, ?fsiEvaluator, ?parameters, ?includeSource, ?generateAnchors,
      ?recursive, ?customizeDocument, ?tokenKindToCss, ?saveImages) =
        let outputDirectory=defaultArg outputDirectory inputDirectory
        let recursive = defaultArg recursive true

        let imageSaver = createImageSaver outputDirectory

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

                let imageSaverOpt = defaultImageSaver (fun () -> imageSaver) outputKind saveImages

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
                              ?customizeDocument = customizeDocument, ?tokenKindToCss = tokenKindToCss,
                              ?imageSaver=imageSaverOpt)

                        HtmlFile.UseFileAsSimpleTemplate(model.ContentTag, model.Parameters, template, outputFile)

                    elif isMd then
                        printfn "converting %s --> %s" inputFile outputFile
                        let inputFile = Path.GetFullPath(inputFile)
                        let model =
                          Literate.ParseAndTransformMarkdownFile
                            (inputFile, output = outputFile, outputKind = outputKind,
                              ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions,
                              ?lineNumbers = lineNumbers, ?references=references, ?parameters = parameters,
                              ?includeSource = includeSource, ?generateAnchors = generateAnchors,
                              ?customizeDocument=customizeDocument, ?tokenKindToCss = tokenKindToCss,
                              ?imageSaver=imageSaverOpt)
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

