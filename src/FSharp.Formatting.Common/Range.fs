namespace FSharp.Formatting.Common

type MarkdownRange = { StartLine : int; StartColumn : int; EndLine : int; EndColumn : int }

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module MarkdownRange =
    let Zero = { StartLine = 0; StartColumn = 0; EndLine = 0; EndColumn = 0 }

    let MergeRanges (ranges:MarkdownRange list) =
        let startRange = ranges |> List.minBy (fun r -> r.StartLine * 10 + r.StartColumn)
        let endRange = ranges |> List.maxBy (fun r -> r.EndLine * 10 + r.EndColumn)
        { StartLine = startRange.StartLine; StartColumn = startRange.StartColumn; EndLine = endRange.EndLine; EndColumn = endRange.EndColumn }