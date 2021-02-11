// --------------------------------------------------------------------------------------
// Format a document as a .md
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.MarkdownFormatting

open MarkdownUtils

let rec formatParagraphs ctx (paragraphs:MarkdownParagraph list) = 
  paragraphs |> Seq.collect (formatParagraph ctx)
 
let formatAsMd links replacements newline paragraphs = 
  formatParagraphs { Links = links; Substitutions=replacements; Newline=newline; DefineSymbol="MD" } paragraphs
  |> String.concat newline
