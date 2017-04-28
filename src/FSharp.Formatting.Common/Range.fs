namespace FSharp.Formatting.Common

type [<Struct>] MarkdownRange = { 
    StartLine   : int 
    StartColumn : int
    EndLine     : int
    EndColumn   : int 
}


module MarkdownRange =
    
    let zero = { StartLine = 0; StartColumn = 0; EndLine = 0; EndColumn = 0 }

    let mergeRanges (ranges:MarkdownRange list) =
        let startRange = ranges |> List.minBy (fun r -> r.StartLine, r.StartColumn)
        let endRange = ranges |> List.maxBy (fun r -> r.EndLine, r.EndColumn)
        {   StartLine   = startRange.StartLine 
            StartColumn = startRange.StartColumn
            EndLine     = endRange.EndLine 
            EndColumn   = endRange.EndColumn 
        }