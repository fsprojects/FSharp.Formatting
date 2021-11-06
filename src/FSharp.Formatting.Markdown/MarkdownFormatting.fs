// --------------------------------------------------------------------------------------
// Format a document as a .md
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.MarkdownFormatting

open MarkdownUtils

let rec formatParagraphs ctx (paragraphs: MarkdownParagraph list) =
    paragraphs |> Seq.collect (formatParagraph ctx)

let formatAsMarkdown links replacements newline crefResolver mdlinkResolver paragraphs =
    let ctx =
        { Links = links
          Substitutions = replacements
          Newline = newline
          CodeReferenceResolver = crefResolver
          MarkdownDirectLinkResolver = mdlinkResolver
          DefineSymbol = "MD" }

    let paragraphs = applySubstitutionsInMarkdown ctx paragraphs

    formatParagraphs ctx paragraphs |> String.concat newline
