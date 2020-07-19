// --------------------------------------------------------------------------------------
// F# CodeFormat (SourceCode.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
namespace rec FSharp.Formatting.CodeFormat

// --------------------------------------------------------------------------------------
// Abstract Syntax representation of formatted source code
// --------------------------------------------------------------------------------------

/// A tool tip consists of a list of items reported from the compiler
type ToolTipSpans = list<ToolTipSpan>

/// A tool tip span can be emphasized text, plain text `Literal` or a line brak
type ToolTipSpan =
    | Emphasis of ToolTipSpans
    | Literal of string
    | HardLineBreak

/// Classifies tokens reported by the FCS
[<RequireQualifiedAccess>]
type TokenKind =
    | Keyword
    | String
    | Comment
    | Identifier
    | Inactive
    | Number
    | Operator
    | Punctuation
    | Preprocessor
    | Module
    | ReferenceType
    | ValueType
    | Interface
    | TypeArgument
    | Property
    | Enumeration
    | UnionCase
    | Function
    | Pattern
    | MutableVar
    | Disposable
    | Printf
    | Escaped
    | Default


/// Represents a kind of error reported from the F# compiler (warning or error)
[<RequireQualifiedAccess>]
type ErrorKind =
  | Error
  | Warning

/// A token in a parsed F# code snippet. Aside from standard tokens reported from
/// the compiler (`Token`), this also includes `Error` (wrapping the underlined
/// tokens), `Omitted` for the special `[omit:...]` tags and `Output` for the special
/// `[output:...]` tag
[<RequireQualifiedAccess>]
type TokenSpan =
  | Token of TokenKind * string * ToolTipSpans option
  | Error of ErrorKind * string * TokenSpans
  | Omitted of string * string
  | Output of string

/// A type alias representing a list of `TokenSpan` values
type TokenSpans = TokenSpan list

/// Represents a line of source code as a list of `TokenSpan` values. This is
/// a single case discriminated union with `Line` constructor.
type Line = Line of originalLine: string * tokenSpans: TokenSpans

/// An F# snippet consists of a snippet title and a list of lines
type Snippet = Snippet of string * Line list

/// Error reported from the F# compiler consists of location (start and end),
/// error kind and the message (wrapped in a single case discriminated union
/// with constructor `SourceError`)
type SourceError =
    /// Error reported from the F# compiler consists of location (start and end),
    /// error kind and the message
    | SourceError of start: (int * int) * finish: (int * int) * errorKind: ErrorKind * message: string