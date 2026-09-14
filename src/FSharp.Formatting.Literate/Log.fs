namespace FSharp.Formatting.Literate

/// The logger of the FSharp.Formatting.Literate assembly.
[<AutoOpen>]
module internal Log =
    let logger = FSharp.Formatting.Common.CategoryLogger "FSharp.Formatting.Literate"
