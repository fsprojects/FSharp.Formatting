namespace FSharp.Formatting.ApiDocs

/// The logger of the FSharp.Formatting.ApiDocs assembly.
[<AutoOpen>]
module internal Log =
    let logger = FSharp.Formatting.Common.CategoryLogger "FSharp.Formatting.ApiDocs"
