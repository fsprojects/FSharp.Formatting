namespace FSharp.Literate

open System.IO
open System.Globalization
open FSharp.Literate
open RazorEngine.Templating
open FSharp.CodeFormat
open FSharp.Markdown

// --------------------------------------------------------------------------------------
// Processing Markdown documents
// --------------------------------------------------------------------------------------
module internal Formatting =

  /// Format document with the specified output kind
  let format doc outputKind = 
    match outputKind with
    | OutputKind.Latex -> Markdown.WriteLatex(doc)
    | OutputKind.Html -> Markdown.WriteHtml(doc)

  /// Try find first-level heading in the paragraph collection
  let findHeadings paragraphs (outputKind:OutputKind) =              
    paragraphs |> Seq.tryPick (function 
      | (Heading(1, text)) ->           
          let doc = MarkdownDocument([Span(text)], dict [])
          Some(format doc outputKind)
      | _ -> None)

  /// Given literate document, get a new MarkdownDocument that represents the 
  /// entire source code of the specified document (with possible `fsx` formatting)
  let getSourceDocument (doc:LiterateDocument) =
    match doc.Source with
    | LiterateSource.Markdown text ->
        doc.With(paragraphs = [CodeBlock text])
    | LiterateSource.Script snippets ->
        let paragraphs = 
          [ for Snippet(name, lines) in snippets do
              if snippets.Length > 1 then
                yield Heading(3, [Literal name])
              yield EmbedParagraphs(FormattedCode(lines)) ]
        doc.With(paragraphs = paragraphs)

// --------------------------------------------------------------------------------------
// Generates file using HTML or CSHTML (Razor) template
// --------------------------------------------------------------------------------------

module Templating =

  /// Replace {parameter} in the input string with 
  /// values defined in the specified list
  let private replaceParameters (contentTag:string) (parameters:seq<string * string>) input = 
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

  /// Depending on the template file, use either Razor engine
  /// or simple Html engine with {replacements} to format the document
  let private generateFile contentTag parameters templateOpt output layoutRoots =
    match templateOpt with
    | Some (file:string) when file.EndsWith("cshtml", true, CultureInfo.InvariantCulture) -> 
        let razor = RazorRender(layoutRoots, [])
        let props = [ "Properties", dict parameters ]
        let generated = razor.ProcessFile(file, props)
        File.WriteAllText(output, generated)      
    | _ ->
        let templateOpt = templateOpt |> Option.map File.ReadAllText
        File.WriteAllText(output, replaceParameters contentTag parameters templateOpt)

  // ------------------------------------------------------------------------------------
  // Formate literate document
  // ------------------------------------------------------------------------------------

  let processFile (doc:LiterateDocument) output ctx = 

    // If we want to include the source code of the script, then process
    // the entire source and generate replacement {source} => ...some html...
    let sourceReplacements =
      if ctx.IncludeSource then 
        let doc = 
          Formatting.getSourceDocument doc
          |> Transformations.replaceLiterateParagraphs ctx 
        let content = Formatting.format doc.MarkdownDocument ctx.OutputKind
        [ "source", content ]
      else []

    // Get page title (either heading or file name)
    let pageTitle = 
      let name = Path.GetFileNameWithoutExtension(output)
      defaultArg (Formatting.findHeadings doc.Paragraphs ctx.OutputKind) name

    // To avoid clashes in templating use {contents} for Latex and older {document} for HTML
    let contentTag = 
      match ctx.OutputKind with 
      | OutputKind.Html -> "document" 
      | OutputKind.Latex -> "contents" 

    // Replace all special elements with ordinary Html/Latex Markdown
    let doc = Transformations.replaceLiterateParagraphs ctx doc
    let formattedDocument = Formatting.format doc.MarkdownDocument ctx.OutputKind
    let tipsHtml = doc.FormattedTips

    // Construct new Markdown document and write it
    let parameters = 
      ctx.Replacements @ sourceReplacements @
      [ "page-title", pageTitle
        "page-source", doc.SourceFile
        contentTag, formattedDocument
        "tooltips", tipsHtml ]
    generateFile contentTag parameters ctx.TemplateFile output ctx.LayoutRoots