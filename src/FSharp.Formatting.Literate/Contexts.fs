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
    DefinedSymbols : string option
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
    Replacements : list<string * string>

    /// Generate line numbers for F# snippets?
    GenerateLineNumbers : bool

    /// Include the source file in the generated output as '{source}'
    IncludeSource : bool

    /// Auto-generate anchors for headers
    GenerateHeaderAnchors : bool

    /// The output format
    OutputKind : OutputKind

    /// Function assigning CSS class to given token kind. If not specified, default mapping will be used
    TokenKindToCss : (TokenKind -> string) option
  }
