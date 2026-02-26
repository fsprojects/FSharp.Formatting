namespace FSharp.Formatting.Markdown

/// <summary>
/// Represents a source range in a Markdown document, identified by start and end line/column positions.
/// Used to track where each parsed element originated in the source text.
/// </summary>
[<Struct>]
type MarkdownRange =
    {
        /// The 1-based line number where the element starts
        StartLine: int
        /// The 0-based column index where the element starts
        StartColumn: int
        /// The 1-based line number where the element ends
        EndLine: int
        /// The 0-based column index where the element ends
        EndColumn: int
    }


/// Helper functions for working with <see cref="MarkdownRange"/> values
module MarkdownRange =

    /// A zero range used as a default/placeholder when no real range is available
    let zero =
        { StartLine = 0
          StartColumn = 0
          EndLine = 0
          EndColumn = 0 }

    /// Merges a list of ranges into one spanning range that covers all of them
    let mergeRanges (ranges: MarkdownRange list) =
        let startRange = ranges |> List.minBy (fun r -> r.StartLine, r.StartColumn)

        let endRange = ranges |> List.maxBy (fun r -> r.EndLine, r.EndColumn)

        { StartLine = startRange.StartLine
          StartColumn = startRange.StartColumn
          EndLine = endRange.EndLine
          EndColumn = endRange.EndColumn }
