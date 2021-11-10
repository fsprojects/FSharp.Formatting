// --------------------------------------------------------------------------------------
// F# CodeFormat (LatexFormatting.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.CodeFormat.Pynb

open System
open FSharp.Formatting.CodeFormat

/// Generate Pynb code cell text with the specified snippets
let formatSnippetsAsPynb (snippets: Snippet []) =
    [| for (Snippet (key, lines)) in snippets do
           let str =
               [| for (Line (originalLine, _spans)) in lines -> originalLine |]
               |> String.concat Environment.NewLine

           yield key, str |]
