module FSharp.Formatting.Common.Utils
#if NETSTANDARD_2_1_OR_GREATER
#else
type System.String with
    member x.StartsWith c =
        x.StartsWith(string<char> c, false, System.Globalization.CultureInfo.InvariantCulture)

    member x.EndsWith c =
        x.EndsWith(string<char> c, false, System.Globalization.CultureInfo.InvariantCulture)

    member x.Contains c = x.Contains(string<char> c)
#endif
