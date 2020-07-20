namespace FSharp.Formatting.Literate

open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate.Evaluation

/// Specifies a context that is passed to functions
/// that need to use the F# compiler
type internal CompilerContext =
  { /// An instance of the F# code formatting agent
    FormatAgent : CodeFormatAgent

    /// F# interactive evaluator
    Evaluator : IFsiEvaluator option

    /// Command line options for the F# compiler
    CompilerOptions : string option

    /// Defined symbols for the F# compiler
    ConditionalDefines : string list
  }

/// Defines the possible output types from literate script (HTML, Latex, Pynb)
[<RequireQualifiedAccess>]
type OutputKind =
  /// Requests HTML output
  | Html

  /// Requests LaTeX output
  | Latex

  /// Requests Notebook output
  | Pynb

  /// Requests F# Script output
  | Fsx
  member x.Extension =
      match x with
      | Fsx -> "fsx"
      | Latex -> "tex"
      | Html -> "html"
      | Pynb -> "ipynb"


/// Defines input type for output generator
type internal LiterateDocModel =
  {
     ContentTag   : string
     Parameters   : (string * string) list
  }

/// Specifies a context that is passed to functions that generate the output
type LiterateProcessingContext =
  { /// Short prefix code added to all HTML 'id' elements
    Prefix : string

    /// Additional parameters to be made in the template file
    Replacements : (string * string) list

    /// Generate line numbers for F# snippets?
    GenerateLineNumbers : bool

    /// Auto-generate anchors for headers
    GenerateHeaderAnchors : bool

    /// The output format
    OutputKind : OutputKind

    /// Conditional defines for the processing
    ConditionalDefines: string list

    /// Function assigning CSS class to given token kind. If not specified, default mapping will be used
    TokenKindToCss : (TokenKind -> string) option
  }
