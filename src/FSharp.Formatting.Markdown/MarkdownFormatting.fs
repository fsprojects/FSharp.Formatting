// --------------------------------------------------------------------------------------
// Format a document as a .md
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.MarkdownFormatting

open MarkdownUtils

let rec formatParagraphs ctx (paragraphs:MarkdownParagraph list) = 
  paragraphs |> Seq.collect (formatParagraph ctx)
 
let formatAsMd links replacements newline crefResolver paragraphs = 
  let ctx = { Links = links; Substitutions=replacements; Newline=newline; ResolveApiDocReference=crefResolver; DefineSymbol="MD" }
  let paragraphs = applySubstitutionsInMarkdown ctx paragraphs
  formatParagraphs ctx paragraphs
  |> String.concat newline
