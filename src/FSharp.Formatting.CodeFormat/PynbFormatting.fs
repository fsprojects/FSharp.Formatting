// --------------------------------------------------------------------------------------
// F# CodeFormat (LatexFormatting.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.CodeFormat.Pynb

open System
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Common.PynbModel

let pynbEncode s = s
    //specialChars |> Array.fold (fun (acc:string) (k, v) -> acc.Replace(k, v)) (HttpUtility.HtmlDecode s)

let rec formatTokenSpans (tokens: TokenSpans) =
    [| for tok in tokens do
        match tok with
        | TokenSpan.Error(_, _, body) ->
            yield! formatTokenSpans body

        | TokenSpan.Output(body) ->
            yield pynbEncode body

        | TokenSpan.Omitted(_body, _) -> ()
      
        | TokenSpan.Token(_kind, body, _) ->
            yield pynbEncode body |]

/// Generate Pynb code cell text with the specified snippets
let format (snippets:Snippet[]) =
    [| for (Snippet(key, lines)) in snippets do
        let str = [| for (Line spans) in lines -> formatTokenSpans spans |> String.concat "" |]  |> String.concat Environment.NewLine
        yield key, str |]

