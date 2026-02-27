// --------------------------------------------------------------------------------------
// Format a document as a .md
// --------------------------------------------------------------------------------------

/// Internal module for re-serializing a parsed Markdown AST back to Markdown text
module internal FSharp.Formatting.Markdown.MarkdownFormatting

open MarkdownUtils

/// Formats a list of paragraphs, collecting all output lines
let rec formatParagraphs ctx (paragraphs: MarkdownParagraph list) =
    paragraphs |> Seq.collect (formatParagraph ctx)

/// Formats a full Markdown document as a Markdown string
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
