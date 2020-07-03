namespace FSharp.Formatting.Literate

open System
open FSharp.Formatting.Markdown
open FSharp.Formatting.CodeFormat
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Special paragraphs used in literate documents
// --------------------------------------------------------------------------------------

/// Specifies visibility of a code snippet. This can be either ordinary
/// visible code, hidden snippet or named snippet with captured output.
type LiterateCodeVisibility = 
  | VisibleCode
  | HiddenCode
  | NamedCode of string

/// Additional properties of a literate code snippet, embedded in a
/// `LiterateParagraph.LiterateCode`. The properties specify how should
/// a snippet be evaluated and formatted.
type LiterateCodeOptions = 
  { /// Specifies whether the snippet is evalauted while processing
    /// Use (*** do-not-eval ***) command to set this to `false`
    Evaluate : bool

    /// Specifies the name of the output produced by this snippet
    /// Use the (*** define-output:foo ***) command to set this value
    /// Other outputs are named cell1, cell2 etc.
    OutputName : string

    /// Indiciates the execution sequence number of the cell if it has been evaluated
    ExecutionCount: int option

    /// Specifies the visibility of the snippet in the generated HTML
    Visibility : LiterateCodeVisibility
  }

/// Extends `MarkdownParagrap` using the `MarkdownEmbedParagraphs` case with
/// additional kinds of paragraphs that can appear in literate F# scripts
/// (such as various special commands to embed output of a snippet etc.)
type LiterateParagraph =
  /// (*** include:foo ***) - Include formatted snippet from other part of the document here 
  | CodeReference of string

  /// (*** include-output ***) - Include output from previous snippet
  /// (*** include-output:foo ***) - Include output from a snippet here 
  | OutputReference of string 

  /// (*** include-it ***) - Include "it" value from the subsequent snippet here 
  /// (*** include-it:foo ***) - Include "it" value from a snippet here 
  | ItValueReference of string 

  /// (*** include-value:foo ***) - Include the formatting of a specified value here
  | ValueReference of string 

  /// Emebdded literate code snippet. Consists of source lines and options
  | LiterateCode of Line list * LiterateCodeOptions

  /// Ordinary formatted code snippet in non-F# language (tagged with language code)
  | LanguageTaggedCode of string * string

  /// Block simply emitted without any formatting equivalent to <pre> tag in html
  | RawBlock of Line list

  interface MarkdownEmbedParagraphs with
    member x.Render() = 
      failwith "LiterateParagraph elements cannot be directly formatted"

// --------------------------------------------------------------------------------------
// Literate document information
// --------------------------------------------------------------------------------------

/// Represents the source of a literate document. This is esither Markdown (as a `string`)
/// or parsed F# script file consisting of snippets.
type LiterateSource = 
  | Markdown of string
  | Script of Snippet[]

/// Representation of a literate document - the representation of Paragraphs
/// uses an F# discriminated union type and so is best used from F#.
type LiterateDocument(paragraphs, formattedTips, links, source, sourceFile, errors) =
  // Get the content of the document as a structurally comparable tuple
  let asTuple (doc:LiterateDocument) = 
    List.ofSeq doc.DefinedLinks.Keys, List.ofSeq doc.DefinedLinks.Values,
    List.ofSeq doc.Errors, doc.Paragraphs

  /// Returns a list of paragraphs in the document
  member x.Paragraphs : MarkdownParagraphs = paragraphs

  /// Returns a dictionary containing explicitly defined links
  member x.DefinedLinks : IDictionary<string, string * option<string>> = links

  /// Errors
  member x.Errors : seq<SourceError> = errors

  /// Original document source code
  member x.Source : LiterateSource = source

  /// Location where the file was loaded from
  member x.SourceFile : string = sourceFile

  /// Formatted tool tips
  member x.FormattedTips : string = formattedTips

  /// Return as markdown document, throwing away additional stuff
  member x.MarkdownDocument = MarkdownDocument(paragraphs, links)

  // Implement equality & get hash code for testing purposes
  override x.Equals(other) = 
    match other with
    | :? LiterateDocument as other -> (asTuple x).Equals(asTuple other)
    | _ -> false
  override x.GetHashCode() = (asTuple x).GetHashCode()

  /// Clone the document and change some of its properties
  member x.With(?paragraphs, ?formattedTips, ?definedLinks, ?source, ?sourceFile, ?errors) =
    LiterateDocument
      ( defaultArg paragraphs x.Paragraphs, defaultArg formattedTips x.FormattedTips,
        defaultArg definedLinks x.DefinedLinks, defaultArg source x.Source, 
        defaultArg sourceFile x.SourceFile, defaultArg errors x.Errors )

// --------------------------------------------------------------------------------------
// Pattern matching helpers
// --------------------------------------------------------------------------------------

/// Provides active patterns for extracting `LiterateParagraph` values from
/// Markdown documents.
module MarkdownPatterns =
  let (|LiterateParagraph|_|) = function
    | EmbedParagraphs(:? LiterateParagraph as lp, _) -> Some lp | _ -> None