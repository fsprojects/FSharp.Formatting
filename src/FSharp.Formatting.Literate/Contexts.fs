namespace FSharp.Formatting.Literate

open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.Templating

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

  /// Requests Markdown output
  | Md
  member x.Extension =
      match x with
      | Fsx -> "fsx"
      | Latex -> "tex"
      | Md -> "md"
      | Html -> "html"
      | Pynb -> "ipynb"


/// Defines the output of processing a literate doc
type internal LiterateDocModel =
  {
     /// The extracted title of the document (first h1 header)
     Title: string

     /// The replacement paramaters
     Substitutions: Substitutions

     /// The text for search index generation (empty for notebooks and latex)
     IndexText: string option

     /// The relative output path 
     OutputPath: string

     /// The kind of output generated
     OutputKind : OutputKind
  }

  // Get the URI for the resource when it is part of an overall site
  // Here 'OutputPath' is assumed to be relative to some 'output' directory.
  member x.Uri(root) =
      let uri = x.OutputPath.Replace("\\", "/")
      let uri = if uri.StartsWith("./") then uri.[2..] else uri
      sprintf "%s%s" root uri

/// Specifies a context that is passed to functions that generate the output
type internal LiterateProcessingContext =
  { /// Short prefix code added to all HTML 'id' elements
    Prefix : string

    /// Additional substitutions to be made in the template file
    Substitutions : Substitutions

    /// Generate line numbers for F# snippets?
    GenerateLineNumbers : bool

    /// Auto-generate anchors for headers
    GenerateHeaderAnchors : bool

    /// The output format
    OutputKind : OutputKind

    /// Helper to resolve `cref:T:TypeName` references in markdown
    ResolveApiDocReference: string -> (string * string) option

    /// Conditional defines for the processing
    ConditionalDefines: string list

    /// Function assigning CSS class to given token kind. If not specified, default mapping will be used
    TokenKindToCss : (TokenKind -> string) option
  }
