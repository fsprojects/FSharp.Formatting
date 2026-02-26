namespace FSharp.Formatting.Markdown

/// Represents a source location within a Markdown document, identified by start and end positions.
[<Struct>]
type MarkdownRange =
    {
        /// The 1-based line number of the start of the element.
        StartLine: int
        /// The 0-based column offset of the start of the element.
        StartColumn: int
        /// The 1-based line number of the end of the element.
        EndLine: int
        /// The 0-based column offset of the end of the element.
        EndColumn: int
    }


/// Utility functions for working with <see cref="MarkdownRange"/> values.
module MarkdownRange =

    /// A zero/empty range (all positions set to 0).
    let zero =
        { StartLine = 0
          StartColumn = 0
          EndLine = 0
          EndColumn = 0 }

    /// Returns the smallest range that contains all ranges in the given list.
    let mergeRanges (ranges: MarkdownRange list) =
        let startRange = ranges |> List.minBy (fun r -> r.StartLine, r.StartColumn)

        let endRange = ranges |> List.maxBy (fun r -> r.EndLine, r.EndColumn)

        { StartLine = startRange.StartLine
          StartColumn = startRange.StartColumn
          EndLine = endRange.EndLine
          EndColumn = endRange.EndColumn }
