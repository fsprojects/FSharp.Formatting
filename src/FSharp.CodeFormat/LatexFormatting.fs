// --------------------------------------------------------------------------------------
// F# CodeFormat (LatexFormatting.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module FSharp.CodeFormat.Latex

open System
open System.IO
open System.Web
open System.Text
open System.Collections.Generic
open FSharp.CodeFormat

/// LaTEX special chars
/// from http://tex.stackexchange.com/questions/34580/escape-character-in-latex
let specialChars =
    [|  // This line comes first to avoid double replacing
        // It also accommodates \r, \n, \t, etc.
        @"\", @"<\textbackslash>";
        "#", @"\#";
        "$", @"\$";
        "%", @"\%";
        "&", @"\&";
        "_", @"\_";
        "{", @"\{";
        "}", @"\}";
        @"<\textbackslash>", @"{\textbackslash}";
        "~",@"{\textasciitilde}";
        "^", @"{\textasciicircum}" |]
    
let latexEncode s =
    specialChars |> Array.fold (fun (acc:string) (k, v) -> acc.Replace(k, v)) (HttpUtility.HtmlDecode s)

/// Represents context used by the formatter
type FormattingContext = 
  { AddLines : bool
    Writer : TextWriter 
    OpenTag : string
    CloseTag : string }

/// Format token spans such as tokens, omitted code etc.
let rec formatTokenSpans (ctx:FormattingContext) = List.iter (function
  | Error(_, _, body) ->
      formatTokenSpans ctx body

  | Output(body) ->
      ctx.Writer.Write(@"\fsi{")
      ctx.Writer.Write(latexEncode body)
      ctx.Writer.Write("}")

  | Omitted(body, _) ->
      ctx.Writer.Write(@"\omi{")
      ctx.Writer.Write(latexEncode body)
      ctx.Writer.Write("}")
      
  | Token(kind, body, _) ->
      let tag = 
        match kind with
        | TokenKind.Comment -> @"\com"
        | TokenKind.Default -> ""
        | TokenKind.Identifier -> @"\id"
        | TokenKind.Inactive -> @"\inact"
        | TokenKind.Keyword -> @"\kwd"
        | TokenKind.Number -> @"\num"
        | TokenKind.Operator -> @"\ops"
        | TokenKind.Preprocessor -> @"\prep"
        | TokenKind.String -> @"\str"

      if kind <> TokenKind.Default then
        // Colorize token & add tool tip
        ctx.Writer.Write(tag)
        ctx.Writer.Write("{")
        ctx.Writer.Write(latexEncode body)
        ctx.Writer.Write("}")
      else       
        ctx.Writer.Write(latexEncode body) )

/// Generate LaTEX with the specified snippets
let formatSnippets (ctx:FormattingContext) (snippets:Snippet[]) =
 [| for (Snippet(title, lines)) in snippets do
      // Generate snippet to a local StringBuilder
      let mainStr = StringBuilder()
      let ctx = { ctx with Writer = new StringWriter(mainStr) }

      // Generate <pre> tag for the snippet
      if String.IsNullOrEmpty(ctx.OpenTag) |> not then
        ctx.Writer.Write(ctx.OpenTag)

      // Line numbers belong to the tag
      if ctx.AddLines then
        ctx.Writer.WriteLine(@"[commandchars=\\\{\}, numbers=left]")
      else
        ctx.Writer.WriteLine(@"[commandchars=\\\{\}]")

      // Print all lines of the snippet
      lines |> List.iter (fun (Line spans) ->
        // Write tokens & end of the line
        formatTokenSpans ctx spans
        ctx.Writer.WriteLine())

      // Close the <pre> tag for this snippet          
      if String.IsNullOrEmpty(ctx.CloseTag) |> not then
        ctx.Writer.WriteLine(ctx.CloseTag)

      ctx.Writer.Close() 
      // Title is important for dictionary lookup
      yield title, mainStr.ToString() |]

/// Format snippets and return LaTEX for <pre> tags together
/// (to be added to the end of document)
let format addLines openTag closeTag (snippets:Snippet[]) = 
  let ctx =  { AddLines = addLines; Writer = null;
               OpenTag = openTag; CloseTag = closeTag }
  
  // Generate main LaTEX for snippets, tooltip isn't important to this format
  formatSnippets ctx snippets, ""