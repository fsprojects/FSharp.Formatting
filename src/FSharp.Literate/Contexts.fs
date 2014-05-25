namespace FSharp.Literate
open FSharp.CodeFormat

/// Specifies a context that is passed to functions 
/// that need to use the F# compiler
type CompilerContext = 
  { // An instance of the F# code formatting agent
    FormatAgent : CodeFormatAgent 
    // F# interactive evaluator
    Evaluator : IFsiEvaluator option
    // Command line options for the F# compiler
    CompilerOptions : string option
    // Defined symbols for the F# compiler
    DefinedSymbols : string option }


/// Defines the two possible output types from literate script: HTML and LaTeX.
[<RequireQualifiedAccess>]
type OutputKind = Html | Latex


/// Specifies a context that is passed to functions that generate the output
type ProcessingContext = 
  { // Path to the template file
    TemplateFile : string option
    // Short prefix code added to all HTML 'id' elements
    Prefix : string 
    // Additional replacements to be made in the template file
    Replacements : list<string * string> 
    // Generate line numbers for F# snippets?
    GenerateLineNumbers : bool 
    // Include the source file in the generated output as '{source}'
    IncludeSource : bool
    // The output format
    OutputKind : OutputKind
    // Where to look for Razor templates, when using Razor
    LayoutRoots : seq<string> }
