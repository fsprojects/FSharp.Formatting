// --------------------------------------------------------------------------------------
// F# CodeFormat (Main.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.CodeFormat

/// Represents an indivudal formatted snippet with title
type FormattedSnippet(title:string, content:string) = 
  /// Returns the title of the snippet (or 'Unnamed') if not given
  member x.Title = title
  /// Returns the formatted content code for the snipet
  member x.Content = content


/// Represents formatted snippets 
type FormattedContent(snippets:FormattedSnippet[], tips:string) = 
  /// Returns the processed snippets as an array
  member x.Snippets = snippets
  /// Returns string with ToolTip elements for all the snippets
  member x.ToolTip = tips


/// Exposes functionality of the F# code formatter with a nice interface
type CodeFormat = 
  /// Returns a new instance of the agent that manages code formatting
  /// using the F# compiler service. The agent requires a reference to 
  /// the 'FSharp.Compiler.dll' assembly. At the moment, the assembly
  /// is shared by all the instances of formatting agent!
  static member CreateAgent() = CodeFormatAgent()

  /// Formats the snippets parsed using the CodeFormatAgent as HTML
  /// The parameters specify prefix for HTML tags, whether lines should
  /// be added to outputs and whether errors should be printed.
  static member FormatHtml(snippets, prefix, addLines, addErrors) =
    CodeFormat.FormatHtml
      ( snippets, prefix, "<pre class=\"fssnip\">", 
        "</pre>", addLines, addErrors )

  /// Formats the snippets parsed using the CodeFormatAgent as HTML
  /// The parameters specify prefix for HTML tags, whether lines should
  /// be added to outputs and whether errors should be printed.
  static member FormatHtml(snippets, prefix, openTag, closeTag, addLines, addErrors) =
    let snip, tip = Html.format addLines addErrors prefix openTag closeTag snippets 
    let snip = [| for t, h in snip -> FormattedSnippet(t, h) |]
    FormattedContent(snip, tip)

  /// Formats the snippets parsed using the CodeFormatAgent as HTML
  /// using the specified ID prefix and default settings.
  static member FormatHtml(snippets, prefix) =
    CodeFormat.FormatHtml(snippets, prefix, true, false)

  /// Formats the snippets parsed using the CodeFormatAgent as LaTeX
  /// The parameters specify prefix for LaTeX tags, whether lines should
  /// be added to outputs.
  static member FormatLatex(snippets, addLines) =
    CodeFormat.FormatLatex(snippets, @"\begin{Verbatim}", @"\end{Verbatim}", addLines)

  /// Formats the snippets parsed using the CodeFormatAgent as LaTeX
  /// The parameters specify whether lines should
  /// be added to outputs.
  static member FormatLatex(snippets, openTag, closeTag, addLines) =
    let snip, tip = Latex.format addLines openTag closeTag snippets
    let snip = [| for t, h in snip -> FormattedSnippet(t, h) |]
    FormattedContent(snip, tip)

  /// Formats the snippets parsed using the CodeFormatAgent as LaTeX
  /// using the default settings.
  static member FormatLatex(snippets) =
    CodeFormat.FormatLatex(snippets, true)

