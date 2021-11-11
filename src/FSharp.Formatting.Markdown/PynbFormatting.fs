// --------------------------------------------------------------------------------------
// Format a document as a .ipynb
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.PynbFormatting

open FSharp.Formatting.PynbModel
open MarkdownUtils

let formatCodeOutput executionCount (output: string, kind) : Output =
    let lines = output.Split([| '\n'; '\r' |])

    { data = OutputData(kind, lines)
      execution_count = executionCount
      metadata = ""
      output_type = "execute_result" }

/// Write a list of MarkdownParagraph values to a TextWriter
let rec formatParagraphs ctx paragraphs =
    match paragraphs with
    | [] -> []
    | _ ->
        let k, otherParagraphs = splitParagraphs paragraphs

        let cell =
            match k with
            | Choice1Of2 (code, codeOutput, executionCount) ->
                codeCell
                    [| adjustFsxCodeForConditionalDefines (ctx.DefineSymbol, ctx.Newline) code |]
                    executionCount
                    (List.map (formatCodeOutput executionCount) codeOutput |> Array.ofList)
            | Choice2Of2 markdown -> markdownCell (Array.ofList (List.collect (formatParagraph ctx) markdown))

        let others = formatParagraphs ctx otherParagraphs
        let cells = cell :: others
        cells

let formatAsPynb links replacements newline crefResolver mdlinkResolver paragraphs =
    let ctx =
        { Links = links
          Substitutions = replacements
          Newline = newline
          CodeReferenceResolver = crefResolver
          MarkdownDirectLinkResolver = mdlinkResolver
          DefineSymbol = "IPYNB" }

    let paragraphs = applySubstitutionsInMarkdown ctx paragraphs

    let cells = formatParagraphs ctx paragraphs

    let notebook = { Notebook.Default with cells = Array.ofList cells }

    notebook.ToString()
