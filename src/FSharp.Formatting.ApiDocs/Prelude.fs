[<AutoOpen>]
module internal FSharp.Formatting.ApiDocs.Prelude

open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

[<RequireQualifiedAccess>]
module Html =
    let wrapInClass cls text = span [ Class cls ] [ !!text ]
    let keyword text = wrapInClass "keyword" text
    let property text = wrapInClass "property" text

    let val' = keyword "val"
    let space = !! "&nbsp;"
    let spaces count = !!(String.replicate count "&nbsp;")
    let comma = keyword ","
    let colon = keyword ":"
    let arrow = keyword "->"
    let dot = keyword "."

    let greaterThan = keyword "&gt;"
    let lessThan = keyword "&lt;"
    let nothing = !! ""
    let equal = keyword "="
    let leftParent = keyword "("
    let rightParent = keyword ")"

    let minify (html: HtmlElement) = !!(html.ToMinifiedHtml())

[<RequireQualifiedAccess>]
module String =

    let normalizeEndOfLine (text: string) = text.Replace("\r\n", "\n")

    let splitBy (c: char) (text: string) = text.Split(c)

    let splitLines (text: string) =
        text |> normalizeEndOfLine |> splitBy '\n'

    let toLower (text: string) = text.ToLower()

    let replace (oldValue: string) (newValue: string) (text: string) = text.Replace(oldValue, newValue)

    let append (value: string) (text: string) = text + value
