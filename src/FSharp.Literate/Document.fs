namespace FSharp.Literate

open FSharp.Markdown
open FSharp.CodeFormat
open System.Collections.Generic

// --------------------------------------------------------------------------------------
// Special paragraphs used in literate documents
// --------------------------------------------------------------------------------------

type LiterateParagraph =
  | CodeReference of string
  | HiddenCode of string option * Line list
  | FormattedCode of Line list
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
type LiterateDocument(paragraphs, formattedTips, links, source, errors) =
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
  member x.With(?paragraphs, ?formattedTips, ?definedLinks, ?source, ?errors) =
    LiterateDocument
      ( defaultArg paragraphs x.Paragraphs, defaultArg formattedTips x.FormattedTips,
        defaultArg definedLinks x.DefinedLinks,
        defaultArg source x.Source, defaultArg errors x.Errors )

// --------------------------------------------------------------------------------------
// Pattern matching helpers
// --------------------------------------------------------------------------------------

module Matching =
  let (|LiterateParagraph|_|) = function
    | EmbedParagraphs(:? LiterateParagraph as lp) -> Some lp | _ -> None