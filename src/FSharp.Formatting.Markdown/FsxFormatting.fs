// --------------------------------------------------------------------------------------
// Format a document as a .fsx
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.FsxFormatting

open MarkdownUtils

/// Like MarkdownUtils.isCode, but does not treat InlineHtmlBlock as code
let isFsxCode =
    (function
    | CodeBlock _ -> true
    | _ -> false)

/// Like MarkdownUtils.splitParagraphs, but does not treat InlineHtmlBlock as code
let splitFsxParagraphs paragraphs =
    let firstCode = paragraphs |> List.tryFindIndex isFsxCode

    match firstCode with
    | Some 0 ->
        let code = paragraphs.[0]
        let codeLines = getCode code
        let otherParagraphs = paragraphs.[1..]

        // Collect the code output(s) that follows this cell if any
        let codeOutput = otherParagraphs |> List.takeWhile isCodeOutput |> List.map getCodeOutput

        let otherParagraphs = otherParagraphs |> List.skipWhile isCodeOutput

        Choice1Of2(codeLines, codeOutput, getExecutionCount code), otherParagraphs

    | Some _
    | None ->
        let markdownParagraphs = paragraphs |> List.takeWhile (isFsxCode >> not)

        let otherParagraphs = paragraphs |> List.skipWhile (isFsxCode >> not)

        Choice2Of2 markdownParagraphs, otherParagraphs

/// Write a list of MarkdownParagraph values to a TextWriter
let rec formatParagraphs ctx paragraphs =
    match paragraphs with
    | [] -> []
    | _ ->
        let k, otherParagraphs = splitFsxParagraphs paragraphs

        let cell =
            match k with
            | Choice1Of2(code, codeOutput, _executionCount) ->
                let output = String.concat ctx.Newline (List.map fst (List.truncate 10 codeOutput))

                let output =
                    if codeOutput.Length > 10 then
                        output + ctx.Newline + "..."
                    else
                        output

                let code2 = adjustFsxCodeForConditionalDefines (ctx.DefineSymbol, ctx.Newline) code

                code2
                + (match codeOutput with
                   | [] -> ""
                   | _out -> "(* output: \n" + output + "*)")
            | Choice2Of2 markdown ->
                "(**"
                + ctx.Newline
                + (markdown |> List.collect (formatParagraph ctx) |> String.concat ctx.Newline)
                + ctx.Newline
                + "*)"

        let others = formatParagraphs ctx otherParagraphs
        let cells = cell :: others
        cells

let formatAsFsx links substitutions newline crefResolver mdlinkResolver paragraphs =
    let ctx =
        { Links = links
          Substitutions = substitutions
          Newline = newline
          CodeReferenceResolver = crefResolver
          MarkdownDirectLinkResolver = mdlinkResolver
          DefineSymbol = "FSX" }

    let paragraphs = applySubstitutionsInMarkdown ctx paragraphs

    formatParagraphs ctx paragraphs |> String.concat newline
