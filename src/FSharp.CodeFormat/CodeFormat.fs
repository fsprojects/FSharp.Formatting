// --------------------------------------------------------------------------------------
// F# CodeFormat (Main.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.CodeFormat

/// Represents an indivudal formatted snippet with title
type FormattedSnippet = {
    /// Returns the title of the snippet (or 'Unnamed') if not given
    Title : string
    /// Returns the formatted content code for the snipet
    Content : string
}

/// Represents formatted snippets
[<StructuredFormatDisplay("{Display}")>]
type FormattedContent = {
    /// Returns the processed snippets as an array
    Snippets : FormattedSnippet []
    /// Returns string with ToolTip elements for all the snippets
    ToolTip : string
} with
    member private self.Display =
        sprintf "{ Snippets =\n%A\nTooltip=\n%A\n}" self.Snippets self.ToolTip


type CssStyledConent = {
    StyleSheet : string
    Snippets : FormattedSnippet []
} with
    member self.Render =
        let styleit (x:FormattedSnippet) =
            sprintf """
<div>
%s

%s
</div>
"""             self.StyleSheet x.Content

        self.Snippets |> Array.map styleit



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
    static member FormatHtml (snippets, prefix, addLines, addErrors) =
        CodeFormat.FormatHtml(
            snippets, prefix, "<pre class=\"fssnip\">", 
            "</pre>", addLines, addErrors
        )


    static member FormatCss(snippets, prefix, openTag, closeTag, addLines, addErrors) =
        let snip, tip = Css.format addLines addErrors prefix openTag closeTag openTag closeTag snippets 
        let snip = [| for t, h in snip -> { Title = t; Content = h } |]
        { Snippets = snip; StyleSheet =  Css.defaultSheet }.Render


    static member FormatCss (snippets, prefix, addLines, addErrors) =
        CodeFormat.FormatCss(
            snippets, prefix, "<pre class=\"fssnip\">", 
            "</pre>", addLines, addErrors
        )

    static member FormatCss (snippets, prefix) =
        CodeFormat.FormatCss(
            snippets, prefix, "<pre class=\"fssnip\">", 
            "</pre>", true, true
        )


    /// Formats the snippets parsed using the CodeFormatAgent as HTML
    /// The parameters specify prefix for HTML tags, whether lines should
    /// be added to outputs and whether errors should be printed.
    static member FormatHtml(snippets, prefix, openTag, closeTag, addLines, addErrors) =
        let snip, tip = Html.format addLines addErrors prefix openTag closeTag openTag closeTag snippets 
        let snip = [| for t, h in snip -> { Title = t; Content = h } |]
        { Snippets = snip; ToolTip =  tip }

    /// <summary>
    /// Formats the snippets parsed using the CodeFormatAgent as HTML
    /// The parameters specify prefix for HTML tags, whether lines should
    /// be added to outputs and whether errors should be printed.
    /// </summary>
    /// <param name="snippets"> snippets parsed using the CodeFormatAgent as HTML </param>
    /// <param name="prefix"> prefix for HTML tags  </param>
    /// <param name="openTag"></param>
    /// <param name="closeTag"></param>
    /// <param name="openLinesTag"></param>
    /// <param name="closeLinesTag"></param>
    /// <param name="addLines"> Add lines to outputs </param>
    /// <param name="addErrors"> Add errors to outputs </param>
    static member FormatHtml (snippets, prefix, openTag, closeTag, openLinesTag, closeLinesTag, addLines, addErrors) =
        let snip, tip = Html.format addLines addErrors prefix openTag closeTag openLinesTag closeLinesTag snippets 
        let snip = [| for t, h in snip -> { Title = t; Content = h } |]
        { Snippets = snip; ToolTip =  tip }

    /// Formats the snippets parsed using the CodeFormatAgent as HTML
    /// using the specified ID prefix and default settings.
    static member FormatHtml (snippets, prefix) =
        CodeFormat.FormatHtml (snippets, prefix, true, false)

    /// Formats the snippets parsed using the CodeFormatAgent as LaTeX
    /// The parameters specify prefix for LaTeX tags, whether lines should
    /// be added to outputs.
    static member FormatLatex (snippets, addLines) =
        CodeFormat.FormatLatex (snippets, @"\begin{Verbatim}", @"\end{Verbatim}", addLines)

    /// Formats the snippets parsed using the CodeFormatAgent as LaTeX
    /// The parameters specify whether lines should
    /// be added to outputs.
    static member FormatLatex (snippets, openTag, closeTag, addLines) =
        let snip, tip = Latex.format addLines openTag closeTag snippets
        let snip = [| for t, h in snip -> { Title = t; Content = h } |]
        { Snippets = snip; ToolTip =  tip }

    /// Formats the snippets parsed using the CodeFormatAgent as LaTeX
    /// using the default settings.
    static member FormatLatex (snippets) =
        CodeFormat.FormatLatex (snippets, true)

