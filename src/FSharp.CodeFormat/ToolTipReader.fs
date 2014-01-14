// --------------------------------------------------------------------------------------
// F# CodeFormat (ToolTipReader.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
module private FSharp.CodeFormat.ToolTipReader

open System
open System.IO
open System.Text
open System.Web

open FSharp.Collections

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

// --------------------------------------------------------------------------------------
// Implements formatting of tool tips 
// --------------------------------------------------------------------------------------

/// Turn string into a sequence of lines interleaved with line breaks
let formatMultilineString (s:string) = 
  [ for line in s.Split('\n') do
      yield HardLineBreak
      yield Literal line ]
  |> List.tail

/// Format comment in the tool tip
let private formatComment = function
  | XmlCommentText(s) -> 
      [ Emphasis (formatMultilineString s)
        HardLineBreak ]
  | _ ->
      // TODO: For 'XmlCommentSignature' we could get documentation 
      // from 'xml' files, but we don't know where to get them...
      []

/// Format the element of a tool tip (comment, overloads, etc.)
let private formatElement = function
  | ToolTipElementNone -> []
  | ToolTipElement(it, comment) -> 
      [ yield! formatMultilineString it
        yield HardLineBreak
        yield! formatComment comment ]
  | ToolTipElementGroup(items) -> 
      // Trim the items to at most 10 displayed in a tool tip
      let items, trimmed = 
        if items.Length <= 10 then items, false
        else items |> Seq.take 10 |> List.ofSeq, true
      [ for (it, comment) in items do
          yield! formatMultilineString it
          yield HardLineBreak
          yield! formatComment comment 

          // Add note with the number of omitted overloads
          if trimmed then 
            let msg = sprintf "(+%d other overloads)" (items.Length - 10)
            yield Literal "   "
            yield Emphasis [Literal (msg) ]
            yield HardLineBreak ]

  | ToolTipElementCompositionError(err) -> []

/// Format entire tool tip as a value of type ToolTipSpans      
let private formatTip tip = 
  let spans = 
    match tip with
    | ToolTipText([single]) -> formatElement single
    | ToolTipText(items) -> 
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
  | ToolTipText(elems) 
      when elems |> List.forall (function 
        ToolTipElementNone -> true | _ -> false) -> None
  | tip -> Some(formatTip tip)