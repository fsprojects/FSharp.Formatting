module FSharp.Formatting.Common.Utils
#if NETSTANDARD_2_1_OR_GREATER
#else
open System

type String with
    member x.StartsWith c = x.StartsWith(string<char> c)

    member x.EndsWith c =
        x.EndsWith(string<char> c, StringComparison.Ordinal)

    member x.Contains c = x.Contains(string<char> c)
#endif
