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
open System.IO
open System.Web
open System.Text
open System.Collections.Generic
open FSharp.CodeFormat
open FSharp.CodeFormat.Constants
open FSharp.CodeFormat.Html



(*
    - keeps track of css class names that are already being used
    - stores doccoms reformated for css strings
    - can replace the default tooltip style
    - 
*)

(*
<div class="hasTip">Hover over me
  <span class="tooltiptext">Tooltip text</span>
</div> 

*)

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
.%s .%s {
    visibility: hidden;
    width: 200px;
    background-color: black;
    color: #fff;
    text-align: left;
    padding: 10px 10px 10px 10px;
    white-space: pre-wrap;  
    border-radius: 6px;
 
    /* Position the tooltip text */
    position: absolute;
    z-index: 1;
    %s
}
"""     srcCssClass tipCssClass additionalCss 

/// CSS that triggers when mouse over the token in source
let tooltipHover srcCssClass tipCssClass additionalStyling =
    sprintf """
 /* Show the tooltip text when you mouse over the tooltip container */
.%s:hover .%s {
    visibility: visible;
    %s
}
"""     srcCssClass tipCssClass additionalStyling


let styleSheet (inlined:bool) (style & {Source=srcCss;Tooltip=tipCss}:TooltipStyle) (additionalCss:string) =
    let srcStyle = sourceStyle srcCss ""
    let popup = tooltipPopup srcCss tipCss ""
    let hover = tooltipHover srcCss tipCss ""
    let body = srcStyle + "\n" + popup + "\n" + hover + "\n" + additionalCss

    if not inlined then body else
    sprintf "<style scoped>\n%s\n</style>" body


let extraCss = """
table.pre, pre.fssnip, pre {
  line-height:13pt;
  border:1px solid #d8d8d8;
  border-collapse:separate;
  white-space:pre;
  font: 9pt 'Droid Sans Mono',consolas,monospace;
  width:90%;
  margin:10px 20px 20px 20px;
  background-color:#212d30;
  padding:10px;
  border-radius:5px;
  color:#d1d1d1;
  max-width: none;
}
pre.fssnip code {
  font: 9pt 'Droid Sans Mono',consolas,monospace;
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
    let tip = sprintf  """<div class="%s">%s</div>""" tipStyle.Tooltip tiptext
    sprintf
        """<div class="%s %s">%s%s</div>""" tipStyle.Source tokenCss token tip 


let tokenAndTipJanky token tokenCss tiptext =
    let tipStyle = TooltipStyle.Default
    let tip = sprintf  """<div class="%s">%s</div>""" tipStyle.Tooltip tiptext
    sprintf
        """<div class="%s %s">%s%s</div>""" tipStyle.Source tokenCss token tip 


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
        let tipStyle = TooltipStyle.Default
        sprintf  """<div class="%s">%s</div>""" tipStyle.Tooltip (formatFunction tip)


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
        let tipAttributes = 
            match tip with
            | Some tip -> ctx.FormatTip tip false formatToolTipSpans
            | _ -> ""

        if kind <> TokenKind.Default then
            // Colorize token & add tool tip
            ctx.TextBuffer {
                printfn "\n tip attributes - %s\n ^^^ for %s" tipAttributes body

                append (tokenAndTipJanky (HttpUtility.HtmlEncode body) kind.Color tipAttributes)
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
                append (sprintf "<table class=\"%s\">" "x")
                append "<tr>"
                append "<td class=\"lines\">"
            } |> ignore

            // Generate <pre> tag for the snippet
            emitTag ctx.OpenLinesTag
            // Print all line numbers of the snippet
            for index in 0..linesLength-1 do
                // Add line number to the beginning
                let lineStr = (index + 1).ToString().PadLeft(numberLength)
                ctx.TextBuffer.AppendFormat("<span class=\"l\">{0}: </span>", lineStr) |> ignore

            emitTag ctx.CloseLinesTag
            ctx.TextBuffer {
                appendLine "</td>"
                append "<td class=\"snippet\">"
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
    //tipf.WriteTipElements( tipStr )
    snippets, tipStr.ToString()






// create root element to store custom css props
// these props will be used for the 'content' prop in the classes
let root (props:seq<string*string>) =
    let props =
        (StringBuilder(), props)
        ||> Seq.fold(fun (sb:StringBuilder) (prop:string,value:string) ->
            sb.Append("--").Append(prop).Append(": ").Append(value).AppendLine(";") 
        )|> string
    props |> sprintf"""
:root {
    %s
}
"""         


let css = """
@import url(https://fonts.googleapis.com/css?family=Roboto:100,400);

*  {  margin:0; padding:0;}

body     {  background:url(http://img.tapatalk.com/d/12/10/31/6yby7usy.jpg) center center;
            }

#container { background:url(https://lh3.ggpht.com/PQXITv6h0hTZLqlvlni7RSN2rE70QytYeNAtngBc3wKQuq8g5gH28EUDqYKgCPkWfQ=h900-rw) no-repeat;
      background-size:500px 293px;
      width:500px;
      height:293px;
      margin:25px auto;}

.tooltip   {  width:16px;
              height:16px;
              border-radius:10px;
              border:2px solid #fff;
              position:absolute;
              background:rgba(255,255,255,.5);}
  

.tooltip:hover
           {  -webkit-animation-play-state: paused;}

.tooltip:hover .info {visibility:visible;}
  
#first   {   margin: 200px 0 0 200px !important;}

#second   {   margin:75px 0 0 52px !important;}

#third   {   margin:158px 0 0 425px !important;}
              
.info     {   width:200px;
              padding:10px;
              background:rgba(255,255,255,1);
              border-radius:3px;
              position:absolute;
              visibility:hidden;
              margin:-105px 0 0 -100px;
              box-shadow:0 0 50px 0 rgba(0,0,0,.5);}

h3         {  font-family: 'Roboto', sans-serif;
              font-weight:100;
              font-size:20px;
              margin:0 0 5px 0;}

p           {  font-family: 'Roboto', sans-serif;
                font-weight:400;
  font-size:12px;}

.arrow {
  position:absolute;
  margin:10px 0 0 88px;
    width: 0; 
    height: 0; 
    border-left: 10px solid transparent;
    border-right: 10px solid transparent;
    border-top: 10px solid #fff;
}
"""