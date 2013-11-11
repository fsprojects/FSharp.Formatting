namespace FSharp.Literate

open FSharp.Markdown

module ParseMarkdown =
  /// Parse the specified Markdown document and return it
  /// as `LiterateDocument` (without processing code snippets)
  let parseMarkdown file text =
    let doc = Markdown.Parse(text)
    LiterateDocument(doc.Paragraphs, "", doc.DefinedLinks, LiterateSource.Markdown text, file, [])