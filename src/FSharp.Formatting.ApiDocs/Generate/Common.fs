module internal FSharp.Formatting.ApiDocs.Generate.Common

open FSharp.Formatting.ApiDocs
open FSharp.Formatting.HtmlModel
open System.Xml.Linq
open System.Text.RegularExpressions

let formatXmlComment (commentOpt: XElement option) : string =

    match commentOpt with
    | Some comment ->
        let docComment = comment.ToString()

        let pattern = $"""<member name=".*">((?'xml_doc'(?:(?!<member>)(?!<\/member>)[\s\S])*)<\/member\s*>)"""

        let m = Regex.Match(docComment, pattern)

        // Remove the <member> and </member> tags
        if m.Success then
            let xmlDoc = m.Groups.["xml_doc"].Value

            let lines = xmlDoc |> String.splitLines |> Array.toList

            // Remove the non meaning full indentation
            let content =
                lines
                |> List.map (fun line ->
                    // Add a small protection in case the user didn't align all it's tags
                    if line.StartsWith(" ") then
                        line.Substring(1)
                    else
                        line
                )
                |> String.concat "\n"

            CommentFormatter.format content
        else
            CommentFormatter.format docComment

    | None -> ""