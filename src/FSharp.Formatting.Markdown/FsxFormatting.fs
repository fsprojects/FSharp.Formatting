// --------------------------------------------------------------------------------------
// Format a document as a .fsx
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.FsxFormatting

open MarkdownUtils

/// Write a list of MarkdownParagraph values to a TextWriter
let rec formatParagraphs ctx paragraphs = 
  match paragraphs with
  [] -> []
  | _ ->
  let k, otherParagraphs = splitParagraphs paragraphs
  let cell =
      match k with 
      | Choice1Of2 (code,  codeOutput, _executionCount) -> 
          let output = String.concat ctx.Newline (List.map fst (List.truncate 10 codeOutput))
          let output = if codeOutput.Length > 10 then output + ctx.Newline + "..." else output
          let code2 = formatFsxCode ctx code
          code2 + (match codeOutput with [] -> "" | _out -> "(* output: \n" + output + "*)")
      | Choice2Of2 markdown ->
          "(**" + ctx.Newline + ( markdown |> List.collect (formatParagraph ctx) |> String.concat ctx.Newline) + ctx.Newline + "*)"
  let others =  formatParagraphs ctx otherParagraphs
  let cells = cell :: others
  cells
 
let formatAsFsx links replacements newline paragraphs = 
  formatParagraphs { Links = links; Substitutions=replacements; Newline=newline; DefineSymbol="FSX" } paragraphs
  |> String.concat newline
