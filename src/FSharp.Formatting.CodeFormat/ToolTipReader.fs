// --------------------------------------------------------------------------------------
// F# CodeFormat (ToolTipReader.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
module private FSharp.Formatting.CodeFormat.ToolTipReader

open System
open System.IO
open System.Text
open System.Web

open FSharp.Collections

open FSharp.Compiler
open FSharp.Compiler.SourceCodeServices

// --------------------------------------------------------------------------------------
// Implements formatting of tool tips 
// --------------------------------------------------------------------------------------

/// Turn string into a sequence of lines interleaved with line breaks
let formatMultilineString (lines:string[]) = 
  [ for line in lines do
      yield HardLineBreak
      yield Literal line ]
  |> List.tail

/// Format comment in the tool tip
let private formatComment xmlDoc =
  match xmlDoc with
  | FSharpXmlDoc.Text(unprocessedLines, _processedLines) -> 
      [ Emphasis (formatMultilineString unprocessedLines)
        HardLineBreak ]
  | _ ->
      // TODO: For 'XmlCommentSignature' we could get documentation 
      // from 'xml' files, but we don't know where to get them...
      []

/// Format the element of a tool tip (comment, overloads, etc.)
let private formatElement tooltip =
  match tooltip with
  | FSharpToolTipElement.None -> []
  //| FSharpToolTipElement.(it, comment) -> 
  //    [ yield! formatMultilineString it
  //      yield HardLineBreak
  //      yield! formatComment comment ]
  | FSharpToolTipElement.Group(items) -> 
      // Trim the items to at most 10 displayed in a tool tip
      let items, trimmed = 
        if items.Length <= 10 then items, false
        else items |> Seq.take 10 |> List.ofSeq, true
      [ for it in items do
          yield! formatMultilineString (it.MainDescription.Split('\n'))
          yield HardLineBreak
          yield! formatComment it.XmlDoc

          // Add note with the number of omitted overloads
          if trimmed then 
            let msg = sprintf "(+%d other overloads)" (items.Length - 10)
            yield Literal "   "
            yield Emphasis [Literal (msg) ]
            yield HardLineBreak ]
  //| FSharpToolTipElement.SingleParameter(_paramType,_doc,_name) -> 
  //  [   yield ToolTipSpan.Literal _paramType
  //      yield ToolTipSpan.HardLineBreak
  //      yield! formatComment _doc     
  //  ]
  | FSharpToolTipElement.CompositionError(_err) -> []

/// Format entire tool tip as a value of type ToolTipSpans      
let private formatTip tip = 
  let spans = 
    match tip with
    | FSharpToolTipText([single]) -> formatElement single
    | FSharpToolTipText(items) -> 
        [ yield Literal "Multiple items"
          yield HardLineBreak
          for first, item in Seq.mapi (fun i it -> i = 0, it) items do
            if not first then 
              yield HardLineBreak
              yield Literal "--------------------"
              yield HardLineBreak
            yield! formatElement item ]

  // Remove unnecessary line breaks
  spans 
  |> List.skipWhile ((=) HardLineBreak) |> List.rev
  |> List.skipWhile ((=) HardLineBreak) |> List.rev

/// Format a tool tip, but first make sure that there is actually 
/// some text in the tip. Returns None if no information is available
let tryFormatTip = function
  | FSharpToolTipText(elems) 
      when elems |> List.forall (function 
        FSharpToolTipElement.None -> true | _ -> false) -> None
  | tip -> Some(formatTip tip)