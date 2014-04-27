namespace FSharp.Literate

open System
open FSharp.Markdown
open FSharp.CodeFormat
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Special paragraphs used in literate documents
// --------------------------------------------------------------------------------------

type LiterateParagraph =
  /// (*** include:foo ***) - Include formatted snippet from other part of the document here 
  | CodeReference of string
  /// (*** include-output:foo ***) - Include output from a snippet here 
  | OutputReference of string 
  /// (*** include-it:foo ***) - Include "it" value from a snippet here 
  | ItValueReference of string 
  /// (*** include-value:foo ***) - Include the formatting of a specified value here
  | ValueReference of string 

  /// (*** hide ***) or (*** define:foo ***) - Code snippet that is not visible in 
  /// the output (but is needed for type checking, evaluation, or to be included later)
  | HiddenCode of string option * Line list
  /// (*** define-output:foo ***) - Code snippet whose output or "it" value
  /// is used somewehre else in the literate doc via (*** include-output:foo ***)
  /// or via (*** include-it:foo ***)
  | NamedCode of string * Line list
  /// (*** do-not-eval ***) - Code snippet that will not be evaluated
  | DoNotEvalCode of Line list

  /// Ordinary formatted code snippet
  | FormattedCode of Line list
  /// Ordinary formatted code snippet in non-F# language (tagged with language code)
  | LanguageTaggedCode of string * string

  interface MarkdownEmbedParagraphs with
    member x.Render() = 
      failwith "LiterateParagraph elements cannot be directly formatted"

// --------------------------------------------------------------------------------------
// Literate document information
// --------------------------------------------------------------------------------------

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

module Matching =
  let (|LiterateParagraph|_|) = function
    | EmbedParagraphs(:? LiterateParagraph as lp) -> Some lp | _ -> None