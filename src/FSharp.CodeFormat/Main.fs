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

module internal CodeFormatHelper =
  open Constants

  let defaultTokenMap kind =
    match kind with
    | TokenKind.Comment       -> CSS.Comment
    | TokenKind.Default       -> CSS.Default
    | TokenKind.Identifier    -> CSS.Identifier
    | TokenKind.Inactive      -> CSS.Inactive
    | TokenKind.Keyword       -> CSS.Keyword
    | TokenKind.Number        -> CSS.Number
    | TokenKind.Operator      -> CSS.Operator
    | TokenKind.Preprocessor  -> CSS.Preprocessor
    | TokenKind.String        -> CSS.String
    | TokenKind.Module        -> CSS.Module
    | TokenKind.ReferenceType -> CSS.ReferenceType
    | TokenKind.ValueType     -> CSS.ValueType
    | TokenKind.Function      -> CSS.Function
    | TokenKind.Pattern       -> CSS.Pattern
    | TokenKind.MutableVar    -> CSS.MutableVar
    | TokenKind.Printf        -> CSS.Printf
    | TokenKind.Escaped       -> CSS.Escaped
    | TokenKind.Disposable    -> CSS.Disposable
    | TokenKind.TypeArgument  -> CSS.TypeArgument
    | TokenKind.Punctuation   -> CSS.Punctuation
    | TokenKind.Enumeration   -> CSS.Enumeration
    | TokenKind.Interface     -> CSS.Interface
    | TokenKind.Property      -> CSS.Property
    | TokenKind.UnionCase     -> CSS.UnionCase

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
  static member FormatHtml(snippets, prefix, addLines, addErrors, ?tokenKindToCss) =
    let tokenKindToCss = defaultArg tokenKindToCss CodeFormatHelper.defaultTokenMap
    CodeFormat.FormatHtml
      ( snippets, prefix, "<pre class=\"fssnip\">",
        "</pre>", addLines, addErrors, tokenKindToCss)

  /// Formats the snippets parsed using the CodeFormatAgent as HTML
  /// The parameters specify prefix for HTML tags, whether lines should
  /// be added to outputs and whether errors should be printed.
  static member FormatHtml(snippets, prefix, openTag, closeTag, addLines, addErrors, ?tokenKindToCss) =
    let tokenKindToCss = defaultArg tokenKindToCss CodeFormatHelper.defaultTokenMap
    let snip, tip = Html.format addLines addErrors prefix openTag closeTag openTag closeTag snippets tokenKindToCss
    let snip = [| for t, h in snip -> FormattedSnippet(t, h) |]
    FormattedContent(snip, tip)

  /// Formats the snippets parsed using the CodeFormatAgent as HTML
  /// The parameters specify prefix for HTML tags, whether lines should
  /// be added to outputs and whether errors should be printed.
  static member FormatHtml(snippets, prefix, openTag, closeTag, openLinesTag, closeLinesTag, addLines, addErrors, ?tokenKindToCss) =
    let tokenKindToCss = defaultArg tokenKindToCss CodeFormatHelper.defaultTokenMap
    let snip, tip = Html.format addLines addErrors prefix openTag closeTag openLinesTag closeLinesTag snippets tokenKindToCss
    let snip = [| for t, h in snip -> FormattedSnippet(t, h) |]
    FormattedContent(snip, tip)

  /// Formats the snippets parsed using the CodeFormatAgent as HTML
  /// using the specified ID prefix and default settings.
  static member FormatHtml(snippets, prefix, ?tokenKindToCss) =
    let tokenKindToCss = defaultArg tokenKindToCss CodeFormatHelper.defaultTokenMap
    CodeFormat.FormatHtml(snippets, prefix, true, false, tokenKindToCss)

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

