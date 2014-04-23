namespace FSharp.Literate

open System.IO
open System.Reflection
open FSharp.Markdown
open FSharp.CodeFormat

// --------------------------------------------------------------------------------------
// Public API
// --------------------------------------------------------------------------------------

/// This type provides three simple methods for calling the literate programming tool.
/// The `ProcessMarkdown` and `ProcessScriptFile` methods process a single Markdown document
/// and F# script, respectively. The `ProcessDirectory` method handles an entire directory tree
/// (ooking for `*.fsx` and `*.md` files).
type Literate private () = 

  /// Build default options context for formatting literate document
  static let formattingContext templateFile format prefix lineNumbers includeSource replacements layoutRoots =
    { TemplateFile = templateFile 
      Replacements = defaultArg replacements []
      GenerateLineNumbers = defaultArg lineNumbers true
      IncludeSource = defaultArg includeSource false
      Prefix = defaultArg prefix "fs"
      OutputKind = defaultArg format OutputKind.Html
      LayoutRoots = defaultArg layoutRoots [] }
  
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
      
  /// Apply the specified transformations to a document
  static let transform references doc =
    let doc = 
      if references <> Some true then doc
      else Transformations.generateReferences doc
    doc              
      
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

  static member WriteHtml(doc:LiterateDocument, ?prefix, ?lineNumbers) =
    let ctx = formattingContext None (Some OutputKind.Html) prefix lineNumbers None None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteHtml(MarkdownDocument(doc.Paragraphs @ [InlineBlock doc.FormattedTips], doc.DefinedLinks))

  static member WriteLatex(doc:LiterateDocument, ?prefix, ?lineNumbers) =
    let ctx = formattingContext None (Some OutputKind.Latex) prefix lineNumbers None None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks))

  static member WriteHtml(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers) =
    let ctx = formattingContext None (Some OutputKind.Html) prefix lineNumbers None None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteHtml(MarkdownDocument(doc.Paragraphs @ [InlineBlock doc.FormattedTips], doc.DefinedLinks), writer)

  static member WriteLatex(doc:LiterateDocument, writer:TextWriter, ?prefix, ?lineNumbers) =
    let ctx = formattingContext None (Some OutputKind.Latex) prefix lineNumbers None None None
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    Markdown.WriteLatex(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), writer)

  // ------------------------------------------------------------------------------------
  // Processing functions that handle templating etc.
  // ------------------------------------------------------------------------------------

  /// Process Markdown document
  static member ProcessMarkdown
    ( input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions, 
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?layoutRoots ) = 
    let doc = 
      Literate.ParseMarkdownFile
        ( input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions, 
          ?references = references )
    let ctx = formattingContext templateFile format prefix lineNumbers includeSource replacements layoutRoots
    Templating.processFile doc (defaultOutput output input format) ctx


  /// Process F# Script file
  static member ProcessScriptFile
    ( input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions, 
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?layoutRoots ) = 
    let doc = 
      Literate.ParseScriptFile
        ( input, ?formatAgent=formatAgent, ?compilerOptions=compilerOptions, 
          ?references = references, ?fsiEvaluator = fsiEvaluator )
    let ctx = formattingContext templateFile format prefix lineNumbers includeSource replacements layoutRoots
    Templating.processFile doc (defaultOutput output input format) ctx


  /// Process directory containing a mix of Markdown documents and F# Script files
  static member ProcessDirectory
    ( inputDirectory, ?templateFile, ?outputDirectory, ?format, ?formatAgent, ?prefix, ?compilerOptions, 
      ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?layoutRoots ) = 

    // Call one or the other process function with all the arguments
    let processScriptFile file output = 
      Literate.ProcessScriptFile
        ( file, ?templateFile = templateFile, output = output, ?format = format, 
          ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions, 
          ?lineNumbers = lineNumbers, ?references = references, ?fsiEvaluator = fsiEvaluator, ?replacements = replacements, 
          ?includeSource = includeSource, ?layoutRoots = layoutRoots )
    let processMarkdown file output = 
      Literate.ProcessMarkdown
        ( file, ?templateFile = templateFile, output = output, ?format = format, 
          ?formatAgent = formatAgent, ?prefix = prefix, ?compilerOptions = compilerOptions, 
          ?lineNumbers = lineNumbers, ?references = references, ?replacements = replacements, 
          ?includeSource = includeSource, ?layoutRoots = layoutRoots )
     
    /// Recursively process all files in the directory tree
    let rec processDirectory indir outdir = 
      // Create output directory if it does not exist
      if Directory.Exists(outdir) |> not then
        try Directory.CreateDirectory(outdir) |> ignore 
        with _ -> failwithf "Cannot create directory '%s'" outdir

      let fsx = [ for f in Directory.GetFiles(indir, "*.fsx") -> processScriptFile, f ]
      let mds = [ for f in Directory.GetFiles(indir, "*.md") -> processMarkdown, f ]
      for func, file in fsx @ mds do
        let name = Path.GetFileNameWithoutExtension(file)
        let ext = (match format with Some OutputKind.Latex -> "tex" | _ -> "html")
        let output = Path.Combine(outdir, sprintf "%s.%s" name ext)

        // Update only when needed
        let changeTime = File.GetLastWriteTime(file)
        let generateTime = File.GetLastWriteTime(output)
        if changeTime > generateTime then
          printfn "Generating '%s.%s'" name ext
          func file output

    let outputDirectory = defaultArg outputDirectory inputDirectory
    processDirectory inputDirectory outputDirectory