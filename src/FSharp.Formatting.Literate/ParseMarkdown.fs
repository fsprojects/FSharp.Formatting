namespace FSharp.Formatting.Literate

open FSharp.Formatting.Markdown

module internal ParseMarkdown =
  /// Parse the specified Markdown document and return it
  /// as LiterateDocument (without processing code snippets)
  let parseMarkdown file text parseOptions =
    let parseOptions = defaultArg parseOptions MarkdownParseOptions.None
    let doc = Markdown.Parse(text, parseOptions=parseOptions)
    LiterateDocument(doc.Paragraphs, "", doc.DefinedLinks, LiterateSource.Markdown text, file, [| |])