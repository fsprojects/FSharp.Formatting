// --------------------------------------------------------------------------------------
// F# CodeFormat (SourceCode.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
namespace FSharp.CodeFormat

// --------------------------------------------------------------------------------------
// Abstract Syntax representation of formatted source code
// --------------------------------------------------------------------------------------

type ToolTipSpans = list<ToolTipSpan>

and ToolTipSpan = 
  | Emphasis of ToolTipSpans
  | Literal of string
  | HardLineBreak

[<RequireQualifiedAccess>]
type TokenKind = 
  | Keyword
  | String
  | Comment
  | Identifier
  | Inactive
  | Number
  | Operator
  | Preprocessor
  | Default

[<RequireQualifiedAccess>]
type ErrorKind = 
  | Error
  | Warning

type TokenSpan = 
  | Token of TokenKind * string * ToolTipSpans option
  | Error of ErrorKind * string * TokenSpans
  | Omitted of string * string
  | Output of string

and TokenSpans = TokenSpan list

type Line = Line of TokenSpans

type Snippet = Snippet of string * Line list

type SourceError = SourceError of (int * int) * (int * int) * ErrorKind * string