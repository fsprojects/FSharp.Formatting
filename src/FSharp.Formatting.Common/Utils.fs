module FSharp.Formatting.Common.Utils
#if NETSTANDARD_2_1_OR_GREATER
#else
type System.String with
    member x.StartsWith(c: char) = string c |> x.StartsWith
    member x.EndsWith(c: char) = string c |> x.EndsWith
    member x.Contains(c: char) = string c |> x.Contains
#endif
