namespace FSharp.Formatting.Literate

open System
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.Templating

/// Specifies a context that is passed to functions
/// that need to use the F# compiler
type internal CompilerContext =
    {
        /// F# interactive evaluator
        Evaluator: IFsiEvaluator option

        /// Command line options for the F# compiler
        CompilerOptions: string option

        /// Defined symbols for the F# compiler
        ConditionalDefines: string list

        /// Reporting errors
        OnError: string -> unit
    }

/// Defines the possible output types from literate script (HTML, Latex, Pynb)
[<RequireQualifiedAccess; Struct>]
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
    | Markdown

    member x.Extension =
        match x with
        | Fsx -> "fsx"
        | Latex -> "tex"
        | Markdown -> "md"
        | Html -> "html"
        | Pynb -> "ipynb"

type IndexText = IndexText of fullContent: string * headings: string list

/// Defines the output of processing a literate doc
type internal LiterateDocModel =
    {
        /// The extracted title of the document (first h1 header if not in front matter)
        Title: string

        /// The replacement parameters
        Substitutions: Substitutions

        /// The text for search index generation (empty for notebooks and latex)
        IndexText: IndexText option

        /// The category in the front matter
        Category: string option

        /// The category index in the front matter (determines the order of categories)
        CategoryIndex: int option

        /// The index in the front matter (Determines the order of files within a category)
        Index: int option

        /// The relative output path
        OutputPath: string

        /// The kind of output generated
        OutputKind: OutputKind

        /// Used for the navigation section, to indicate the list item as active
        IsActive: bool
    }

    // Get the URI for the resource when it is part of an overall site
    // Here 'OutputPath' is assumed to be relative to some 'output' directory.
    member x.Uri(root) =
        let uri = x.OutputPath.Replace("\\", "/")

        let uri =
            if uri.StartsWith("./", StringComparison.Ordinal) then
                uri.[2..]
            else
                uri

        sprintf "%s%s" root uri

/// Specifies a context that is passed to functions that generate the output
type internal LiterateProcessingContext =
    {
        /// Short prefix code added to all HTML 'id' elements
        Prefix: string

        /// Additional substitutions to be made in the template file
        Substitutions: Substitutions

        /// Generate line numbers for F# snippets?
        GenerateLineNumbers: bool

        /// Auto-generate anchors for headers
        GenerateHeaderAnchors: bool

        /// The output format
        OutputKind: OutputKind

        /// Helper to resolve URL referenecs in markdown, e.g. 'index.md' --> 'index.html' when doing HTML output
        MarkdownDirectLinkResolver: string -> string option

        /// Helper to resolve `cref:T:TypeName` references in markdown
        CodeReferenceResolver: string -> (string * string) option

        /// Conditional defines for the processing
        ConditionalDefines: string list

        TokenKindToCss: (TokenKind -> string) option
    }
