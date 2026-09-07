namespace FSharp.Formatting.CodeFormat

/// The logger of the FSharp.Formatting.CodeFormat assembly.
[<AutoOpen>]
module internal Log =
    let logger = FSharp.Formatting.Common.CategoryLogger "FSharp.Formatting.CodeFormat"
