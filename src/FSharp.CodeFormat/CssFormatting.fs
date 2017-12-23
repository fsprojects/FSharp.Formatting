#if INTERACTIVE
#r "../../packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"
#r "../../packages/System.Reflection.Metadata/lib/portable-net45+win8/System.Reflection.Metadata.dll"
#r "System.Core"
#r "System.Data"
#r "System.Web"
#r "../../bin/FSharp.Formatting.Common.dll"
//#load "../../paket-files/matthid/Yaaf.FSharp.Scripting/src/source/Yaaf.FSharp.Scripting/YaafFSharpScripting.fs"
#load "../../src/Common/Collections.fs"
#load "../../src/Common/StringParsing.fs"
#load "Pervasive.fs"
#load "Constants.fs"
#load "CommentFilter.fs"
#load "SourceCode.fs"
#load "ToolTipReader.fs"
#load "CodeFormatAgent.fs"
#load "HtmlFormatting.fs"

#else
module internal FSharp.CodeFormat.Css
#endif
open System
open System.Web
open System.Text
open FSharp.CodeFormat
open FSharp.CodeFormat.Html



/// Stores the Css classes used to construct the
/// style sheet and tooltips
type TooltipStyle = {
    Tooltip : string
    Label : string
    Source : string
    Text : string
} with
    static member Default = {
        Tooltip = "ttip"
        Label = "label"
        Source = "pre"
        Text = "txt"
    }


let sourceStyle srcCssClass additionalCss =
    sprintf """
.%s {
/* Tooltip container */
    position: relative;
    display: inline-block;
    %s
}
"""   srcCssClass additionalCss



/// Styles the popup that contains the intellisense content
let tooltipPopup srcCssClass tipCssClass additionalCss =
    sprintf  """
/* Tooltip text */
div .%s {
    display: none;
    width: fit-content;
    text-align: left;
    padding: 10px 10px 10px 10px;
    background:#475b5f;
    border-radius:4px;
    font:11pt 'Droid Sans', arial, sans-serif;
    color:#d1d1d1;
    /* Position the tooltip text */
    position: absolute;
    z-index: 10;
    %s
}
"""     tipCssClass additionalCss 


/// CSS that triggers when mouse over the token in source
let tooltipHover srcCssClass tipCssClass additionalStyling =
    sprintf """
 /* Show the tooltip text when you mouse over the tooltip container */

span.pre:hover .%s {
    display: inline-block;
    visibility: visible;
    %s
}
"""
        tipCssClass additionalStyling

let styleSheet (inlined:bool) (style & {Source=srcCss;Tooltip=tipCss}:TooltipStyle) (additionalCss:string) =
    let srcStyle = sourceStyle srcCss ""
    let popup = tooltipPopup srcCss tipCss ""
    let hover = tooltipHover srcCss tipCss ""
    let body = additionalCss +  srcStyle + "\n" + popup + "\n" + hover + "\n" 

    if not inlined then body else
    sprintf "<style scoped>\n%s\n</style>" body


let extraCss = """

/* strings --- and stlyes for other string related formats */
.s { color:#60815f; }
/* printf formatters */
.pf { color:#2367a9; }
/* escaped chars */
.e { color:#EA8675; }

/* identifiers --- and styles for more specific identifier types */
.id { color:#d1d1d1; }
/* module */
.m { color:#1156a0; }
/* reference type */
.rt { color:#6146cc; }
/* value type */
.vt { color:#d45f12; }
/* interface */
.if{ color:#ace695; }
/* type argument */
.ta { color:#a4e00e; }
/* disposable */
.d { color:#9b4f72; }
/* property */
.prop { color:#24532c; }
/* punctuation */
.pn { color:#b08e21; }
/* function */
.fn { color:#38d7b6; }
/* active pattern */
.pat { color:#4ec9b0; }
/* union case */
.uc { color:#28de48; }
/* enumeration */
.en { color:#236f2e; }
/* keywords */
.k { color:#20879f; }
/* comment */
.c { color:#49744d; }
/* operators */
.o { color:#be2424; }
/* numbers */
.n { color:#96C71D; }
/* line number */
.l { color:#80b0b0; }
/* mutable var or ref cell */
.mv { color:#bf44bd; font-weight: bold; }
/* inactive code */
.inactive { color:#808080; }
/* preprocessor */
.pp { color:#ed6805; }
/* fsi output */
.fsi { color:#808080; }

/* omitted */
.omitted {
    background:#3c4e52;
  border-radius:5px;
    color:#808080;

}

table.pre, pre.fssnip, pre {
  line-height:13pt;
  border:1px solid #d8d8d8;
  border-collapse:separate;
  white-space:pre;
  font: 9pt 'Droid Sans Mono',consolas,monospace;
  width:fit-content;
  margin:10px 20px 20px 20px;
  background-color:#18353c;
  padding:10px;
  border-radius:5px;
  color:#d1d1d1;
  max-width: none;
}

table.pre pre {
  padding:0px;
  margin:0px;
  border-radius:0px;
  width: 100%;
}
table.pre td {
  padding:0px;
  white-space:normal;
  margin:0px;
}
table.pre td.lines {
  width:30px;
}

pre.fssnip {
  font: 9pt 'Droid Sans Mono',consolas,monospace;
  padding-left: 20px;
}

"""

let defaultSheet = styleSheet true TooltipStyle.Default extraCss

type TooltipContent = {
    Signature : string
    Summary : string
    Fullname : string
    Assembly : string
}


/// Creates the summary item in the popup tooltip
let tipSummary textCss text =
    sprintf """<div class="%s"><p>%s<p></div>""" textCss text


/// Creates the fullname item in the popup tooltip
let tipFullname labelCss name =
    sprintf "<span class=\"%s\">%s</span>" labelCss name


/// Creates a param item in the popup tooltip
let tipParam cssClass labelCss name description =
    sprintf "<span class=\"%s\">parameter - <span class=\"%s\">%s</span>%s</span>"
                cssClass labelCss name description


let tokenAndTip token tokenCss (tipContent:TooltipContent) (tipStyle:TooltipStyle) =
    let summary = tipSummary tipStyle.Text tipContent.Summary
    let fullname = tipFullname tipStyle.Label tipContent.Fullname
    let tiptext = tipContent.Signature + summary + fullname
    let tip =
        sprintf  """<div class="%s">%s</div>""" tipStyle.Tooltip tiptext
    sprintf
        """<div class="%s %s">%s%s</div>""" tipStyle.Source tokenCss token tip 


let tokenAndTipJanky token tokenCss tiptext =
    let tipStyle = TooltipStyle.Default
    let tip =
        if String.IsNullOrWhiteSpace tiptext then String.Empty else
        sprintf  """<div class="%s">%s</div>""" tipStyle.Tooltip tiptext
    sprintf
        """<span class="%s %s">%s%s</span>""" tipStyle.Source tokenCss token tip 


/// Represents context used by the formatter
type CssFormattingContext = {
    Style          : TooltipStyle
    TextBuffer     : StringBuilder
    InlineCss      : bool
    AddLines       : bool
    GenerateErrors : bool
    OpenTag        : string
    CloseTag       : string
    OpenLinesTag   : string
    CloseLinesTag  : string
    FormatTip      : ToolTipSpan list -> bool -> (ToolTipSpan list -> string) -> string 
}

/// Mutable type that formats tool tips and keeps the generated HTML
type CssToolTipFormatter (prefix) = 


    /// Formats tip and returns assignments for 'onmouseover' and 'onmouseout'
    member __.FormatTip (tip:ToolTipSpan list) overlapping formatFunction = 
        let text = formatFunction tip
        if String.IsNullOrWhiteSpace text then String.Empty else
        text

/// Format token spans such as tokens, omitted code etc.
let rec formatTokenSpans (ctx:CssFormattingContext) = List.iter (function
    | Error (_kind, message, body) when ctx.GenerateErrors ->
        let tip = ToolTipReader.formatMultilineString (message.Trim())
        let tipAttributes = ctx.FormatTip tip true formatToolTipSpans
        ctx.TextBuffer {
            append "<span "
            append tipAttributes
            append "class=\"cerr\">"
        } |> ignore
        formatTokenSpans { ctx with FormatTip = fun _ _ _ -> "" } body
        ctx.TextBuffer.Append "</span>"  |> ignore
    | Error (_, _, body) ->
        formatTokenSpans ctx body
    | Output body ->
        ctx.TextBuffer {
            append "<span class=\"fsi\">"
            append (HttpUtility.HtmlEncode body)
            append "</span>"
        } |> ignore
    | Omitted(body, hidden) ->
        let tip = ToolTipReader.formatMultilineString (hidden.Trim())
        let tipAttributes = ctx.FormatTip tip true formatToolTipSpans
        ctx.TextBuffer {
            append "<span "
            append "<span "
            append tipAttributes
            append "class=\"omitted\">"
            append body
            append "</span>"
        } |> ignore
    | Token (kind, body, tip) ->
        // Generate additional attributes for ToolTip
        let tooltipContent = 
            match tip with
            | Some tip -> ctx.FormatTip tip false formatToolTipSpans
            | _ -> ""

        if kind <> TokenKind.Default then
            // Colorize token & add tool tip
            ctx.TextBuffer {
                append (tokenAndTipJanky (HttpUtility.HtmlEncode body) kind.Color tooltipContent)
                //append "<span "
                //append tipAttributes
                //append ("class=\"" + kind.Color + "\">")
                //append (HttpUtility.HtmlEncode body)
                //append "</span>"
            } |> ignore
        else       
            ctx.TextBuffer.Append (HttpUtility.HtmlEncode body)|> ignore
)


/// Generate HTML with the specified snippets
let formatSnippets (ctx:CssFormattingContext) (snippets:Snippet[]) = [|
    for (Snippet(title, lines)) in snippets do
        // Skip empty lines at the beginning and at the end
        let skipEmptyLines =
            Seq.skipWhile (fun (Line spans) -> List.isEmpty spans) >> List.ofSeq
        let lines =
            lines |> skipEmptyLines |> List.rev |> skipEmptyLines |> List.rev

        // Generate snippet to a local StringBuilder
        let mainStr = StringBuilder()
        let ctx = { ctx with TextBuffer = mainStr }

        let numberLength = lines.Length.ToString().Length
        let linesLength = lines.Length
        let emitTag tag = 
            if String.IsNullOrEmpty tag |> not then 
                ctx.TextBuffer.Append tag |> ignore

        // If we're adding lines, then generate two column table 
        // (so that the body can be easily copied)
        if ctx.AddLines then
            ctx.TextBuffer {
                append (sprintf "<table class=\"%s\">" "pre")
                append "<tbody>"
                append "<tr>"
                append "<td class=\"lines\">"
            } |> ignore

            // Generate <pre> tag for the snippet
            emitTag ctx.OpenLinesTag
            // Print all line numbers of the snippet
            for index in 0..linesLength-1 do
                // Add line number to the beginning
                let lineStr = (index + 1).ToString().PadLeft(numberLength)
                ctx.TextBuffer.AppendFormat("<span class=\"l\">{0}: </span>\n", lineStr) |> ignore

            emitTag ctx.CloseLinesTag
            ctx.TextBuffer {
                appendLine "</td>"
                append "<td class=\"snippet\">"
                //append "<td>"
            } |> ignore

        // Print all lines of the snippet inside <pre>..</pre>
        emitTag ctx.OpenTag
        lines |> List.iter (fun (Line spans) ->
            formatTokenSpans ctx spans
            ctx.TextBuffer.AppendLine() |> ignore
        )
        emitTag ctx.CloseTag

        if ctx.AddLines then
            // Close the table if we are adding lines
            ctx.TextBuffer {
                appendLine "</td>"
                appendLine "</tr>"
                append "</tbody>"
                append "</table>"
            } |> ignore
        yield title, mainStr.ToString()
|]

/// Format snippets and return HTML for <pre> tags together
/// wtih HTML for ToolTips (to be added to the end of document)
let format addLines addErrors prefix openTag closeTag openLinesTag closeLinesTag (snippets:Snippet[]) = 
    let tipf = CssToolTipFormatter prefix
    let ctx =  {
        InlineCss      = false
        Style          = TooltipStyle.Default
        AddLines       = addLines 
        GenerateErrors = addErrors
        TextBuffer     = StringBuilder()
        FormatTip      = tipf.FormatTip 
        OpenLinesTag   = openLinesTag
        CloseLinesTag  = closeLinesTag
        OpenTag        = openTag 
        CloseTag       = closeTag 
    }
    // Generate main HTML for snippets
    let snippets = formatSnippets ctx snippets
    // Generate HTML with ToolTip tags
    let tipStr = StringBuilder()
    snippets, tipStr.ToString()


