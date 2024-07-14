module internal FSharp.Formatting.ApiDocs.CommentFormatter

// TODO: This file is a copy of what I used in my FSharp.Docs experiments
// We should re-base it with the latest version available in FsAutoComplete at
// https://github.com/ionide/FsAutoComplete/blob/main/src/FsAutoComplete.Core/TipFormatter.fs
// I didn't do it yet, because I wanted to first experiement with FSharp.Formatting code base and send a draft PR
// to start the discussion

open System
open System.Text.RegularExpressions
open FSharp.Formatting.Markdown

let inline newLine<'T> = "\n"

let private tagPattern (tagName: string) =
    sprintf
        """(?'void_element'<%s(?'void_attributes'\s+[^\/>]+)?\/>)|(?'non_void_element'<%s(?'non_void_attributes'\s+[^>]+)?>(?'non_void_innerText'(?:(?!<%s>)(?!<\/%s>)[\s\S])*)<\/%s\s*>)"""
        tagName
        tagName
        tagName
        tagName
        tagName

type private TagInfo =
    | VoidElement of attributes: Map<string, string>
    | NonVoidElement of innerText: string * attributes: Map<string, string>

[<NoEquality; NoComparison>]
type private FormatterInfo =
    { TagName: string
      Formatter: TagInfo -> string option }

let private extractTextFromQuote (quotedText: string) =
    quotedText.Substring(1, quotedText.Length - 2)

let private extractMemberText (text: string) =
    let pattern = "(?'member_type'[a-z]{1}:)?(?'member_text'.*)"
    let m = Regex.Match(text, pattern, RegexOptions.IgnoreCase)

    if m.Groups.["member_text"].Success then
        m.Groups.["member_text"].Value
    else
        text

let private getAttributes (attributes: Group) =
    if attributes.Success then
        let pattern = """(?'key'\S+)=(?'value''[^']*'|"[^"]*")"""

        Regex.Matches(attributes.Value, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Groups.["key"].Value, extractTextFromQuote m.Groups.["value"].Value)
        |> Map.ofSeq
    else
        Map.empty

let rec private applyFormatter (info: FormatterInfo) text =
    let pattern = tagPattern info.TagName

    match Regex.Match(text, pattern, RegexOptions.IgnoreCase) with
    | m when m.Success ->
        if m.Groups.["void_element"].Success then
            let attributes = getAttributes m.Groups.["void_attributes"]

            let replacement = VoidElement attributes |> info.Formatter

            match replacement with
            | Some replacement ->
                text.Replace(m.Groups.["void_element"].Value, replacement)
                // Re-apply the formatter, because perhaps there is more
                // of the current tag to convert
                |> applyFormatter info

            | None ->
                // The formatter wasn't able to convert the tag
                // Return as it is and don't re-apply the formatter
                // otherwise it will create an infinity loop
                text

        else if m.Groups.["non_void_element"].Success then
            let innerText = m.Groups.["non_void_innerText"].Value
            let attributes = getAttributes m.Groups.["non_void_attributes"]

            let replacement = NonVoidElement(innerText, attributes) |> info.Formatter

            match replacement with
            | Some replacement ->
                // Re-apply the formatter, because perhaps there is more
                // of the current tag to convert
                text.Replace(m.Groups.["non_void_element"].Value, replacement)
                |> applyFormatter info

            | None ->
                // The formatter wasn't able to convert the tag
                // Return as it is and don't re-apply the formatter
                // otherwise it will create an infinity loop
                text
        else
            // Should not happend but like that we are sure to handle all possible cases
            text
    | _ -> text

let private codeBlock =
    { TagName = "code"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, attributes) ->
            let lang =
                match Map.tryFind "lang" attributes with
                | Some lang -> lang

                | None -> ""

            let formattedText =
                if innerText.StartsWith("\n") then

                    sprintf "```%s%s```" lang innerText
                else
                    sprintf "```%s\n%s```" lang innerText

            newLine + formattedText + newLine |> Some

    }
    |> applyFormatter

let private codeInline =
    { TagName = "c"
      Formatter =
        function
        | VoidElement _ -> None
        | NonVoidElement(innerText, _) -> "<code>" + innerText + "</code>" |> Some }
    |> applyFormatter

let private paragraph =
    { TagName = "para"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, _) -> "<p>" + innerText + "</p>" |> Some }
    |> applyFormatter

let private block =
    { TagName = "block"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, _) -> newLine + innerText + newLine |> Some }
    |> applyFormatter

let private see =
    let getCRef (attributes: Map<string, string>) = Map.tryFind "cref" attributes

    let getHref (attributes: Map<string, string>) = Map.tryFind "href" attributes

    { TagName = "see"
      Formatter =
        function
        | VoidElement attributes ->
            match getCRef attributes with
            | Some cref ->
                // TODO: Add config to generates command
                "<code>" + extractMemberText cref + "</code>" |> Some

            | None -> None

        | NonVoidElement(innerText, attributes) ->
            if String.IsNullOrWhiteSpace innerText then
                match getCRef attributes with
                | Some cref ->
                    // TODO: Add config to generates command
                    "<code>" + extractMemberText cref + "</code>" |> Some

                | None -> None
            else
                match getHref attributes with
                | Some href -> sprintf "[%s](%s)" innerText href |> Some

                | None -> "<code>" + innerText + "</code>" |> Some }
    |> applyFormatter

let private paramRef =
    let getName (attributes: Map<string, string>) = Map.tryFind "name" attributes

    { TagName = "paramref"
      Formatter =
        function
        | VoidElement attributes ->
            match getName attributes with
            | Some name -> "<code>" + name + "</code>" |> Some

            | None -> None

        | NonVoidElement(innerText, attributes) -> None

    }
    |> applyFormatter

let private typeParamRef =
    let getName (attributes: Map<string, string>) = Map.tryFind "name" attributes

    { TagName = "typeparamref"
      Formatter =
        function
        | VoidElement attributes ->
            match getName attributes with
            | Some name -> "<code>" + name + "</code>" |> Some

            | None -> None

        | NonVoidElement(innerText, attributes) -> None }
    |> applyFormatter

type private Term = string
type private Definition = string

type private ListStyle =
    | Bulleted
    | Numbered
    | Tablered

/// ItemList allow a permissive representation of an Item.
/// In theory, TermOnly should not exist but we added it so part of the documentation doesn't disappear
/// TODO: Allow direct text support without <description> and <term> tags
type private ItemList =
    /// A list where the items are just contains in a <description> element
    | DescriptionOnly of string
    /// A list where the items are just contains in a <term> element
    | TermOnly of string
    /// A list where the items are a term followed by a definition (ie in markdown: * <TERM> - <DEFINITION>)
    | Definitions of Term * Definition

let private itemListToStringAsMarkdownList (item: ItemList) =
    match item with
    | DescriptionOnly description -> description
    | TermOnly term -> "<strong>" + term + "</strong>"
    | Definitions(term, description) -> "<strong>" + term + "</strong> - " + description

let private list =
    let getType (attributes: Map<string, string>) = Map.tryFind "type" attributes

    let tryGetInnerTextOnNonVoidElement (text: string) (tagName: string) =
        match Regex.Match(text, tagPattern tagName, RegexOptions.IgnoreCase) with
        | m when m.Success ->
            if m.Groups.["non_void_element"].Success then
                Some m.Groups.["non_void_innerText"].Value
            else
                None
        | _ -> None

    let tryGetNonVoidElement (text: string) (tagName: string) =
        match Regex.Match(text, tagPattern tagName, RegexOptions.IgnoreCase) with
        | m when m.Success ->
            if m.Groups.["non_void_element"].Success then
                Some(m.Groups.["non_void_element"].Value, m.Groups.["non_void_innerText"].Value)
            else
                None
        | _ -> None

    let tryGetDescription (text: string) =
        tryGetInnerTextOnNonVoidElement text "description"

    let tryGetTerm (text: string) =
        tryGetInnerTextOnNonVoidElement text "term"

    let rec extractItemList (res: ItemList list) (text: string) =
        match Regex.Match(text, tagPattern "item", RegexOptions.IgnoreCase) with
        | m when m.Success ->
            let newText = text.Substring(m.Value.Length)

            if m.Groups.["non_void_element"].Success then
                let innerText = m.Groups.["non_void_innerText"].Value
                let description = tryGetDescription innerText
                let term = tryGetTerm innerText

                let currentItem: ItemList option =
                    match description, term with
                    | Some description, Some term -> Definitions(term, description) |> Some
                    | Some description, None -> DescriptionOnly description |> Some
                    | None, Some term -> TermOnly term |> Some
                    | None, None -> None

                match currentItem with
                | Some currentItem -> extractItemList (res @ [ currentItem ]) newText
                | None -> extractItemList res newText
            else
                extractItemList res newText
        | _ -> res

    let rec extractColumnHeader (res: string list) (text: string) =
        match Regex.Match(text, tagPattern "listheader", RegexOptions.IgnoreCase) with
        | m when m.Success ->
            let newText = text.Substring(m.Value.Length)

            if m.Groups.["non_void_element"].Success then
                let innerText = m.Groups.["non_void_innerText"].Value

                let rec extractAllTerms (res: string list) (text: string) =
                    match tryGetNonVoidElement text "term" with
                    | Some(fullString, innerText) ->
                        let escapedRegex = Regex(Regex.Escape(fullString))
                        let newText = escapedRegex.Replace(text, "", 1)
                        extractAllTerms (res @ [ innerText ]) newText
                    | None -> res

                extractColumnHeader (extractAllTerms [] innerText) newText
            else
                extractColumnHeader res newText
        | _ -> res

    let rec extractRowsForTable (res: (string list) list) (text: string) =
        match Regex.Match(text, tagPattern "item", RegexOptions.IgnoreCase) with
        | m when m.Success ->
            let newText = text.Substring(m.Value.Length)

            if m.Groups.["non_void_element"].Success then
                let innerText = m.Groups.["non_void_innerText"].Value

                let rec extractAllTerms (res: string list) (text: string) =
                    match tryGetNonVoidElement text "term" with
                    | Some(fullString, innerText) ->
                        let escapedRegex = Regex(Regex.Escape(fullString))
                        let newText = escapedRegex.Replace(text, "", 1)
                        extractAllTerms (res @ [ innerText ]) newText
                    | None -> res

                extractRowsForTable (res @ [ extractAllTerms [] innerText ]) newText
            else
                extractRowsForTable res newText
        | _ -> res

    { TagName = "list"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, attributes) ->
            let listStyle =
                match getType attributes with
                | Some "bullet" -> Bulleted
                | Some "number" -> Numbered
                | Some "table" -> Tablered
                | Some _
                | None -> Bulleted

            match listStyle with
            | Bulleted ->
                let items =
                    extractItemList [] innerText
                    |> List.map (fun item -> "<li>" + itemListToStringAsMarkdownList item + "</li>")
                    |> String.concat newLine

                "<ul>" + newLine + items + newLine + "</ul>"

            | Numbered ->
                let items =
                    extractItemList [] innerText
                    |> List.map (fun item -> "<li>" + itemListToStringAsMarkdownList item + "</li>")
                    |> String.concat newLine

                "<ol>" + newLine + items + newLine + "</ol>"

            | Tablered ->
                let columnHeaders = extractColumnHeader [] innerText
                let rows = extractRowsForTable [] innerText

                let columnHeadersText =
                    columnHeaders
                    |> List.mapi (fun index header -> "<th>" + header + "</th>")
                    |> String.concat ""

                let itemsText =
                    rows
                    |> List.map (fun columns ->
                        let rowContent =
                            columns
                            |> List.mapi (fun index column -> "<td>" + column + "</td>")
                            |> String.concat newLine

                        "<tr>" + newLine + rowContent + newLine + "</tr>")
                    |> String.concat newLine

                "<table>"
                + newLine
                + "<thead><tr>"
                + newLine
                + columnHeadersText
                + newLine
                + "</tr></thead>"
                + newLine
                + "<tbody>"
                + newLine
                + itemsText
                + newLine
                + "</tbody>"
                + newLine
                + "</table>"
            |> Some }
    |> applyFormatter

/// <summary>
/// Unescape XML special characters
///
/// For example, this allows to print '>' in the tooltip instead of '&gt;'
/// </summary>
let private unescapeSpecialCharacters (text: string) =
    text
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&quot;", "\"")
        .Replace("&apos;", "'")
        .Replace("&amp;", "&")

let private summary =
    { TagName = "summary"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, _) ->
            """<div class="docs-summary">""" + newLine + innerText + newLine + "</div>"
            |> Some

    }
    |> applyFormatter

let private example =
    { TagName = "example"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, _) ->
            newLine
            + """<div class="docs-example">"""
            + newLine
            + """<p><strong>Example</strong></p>"""
            + newLine
            + innerText
            + newLine
            + "</div>"
            + newLine
            + newLine
            |> Some

    }
    |> applyFormatter

let private removeSummaryTag =
    { TagName = "summary"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, _) -> innerText |> Some

    }
    |> applyFormatter

let private removeParamElement =
    { TagName = "param"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(_, _) ->
            // Returning an empty string will completely remove the element
            Some ""

    }
    |> applyFormatter

let private removeRemarkTag =
    { TagName = "remark"
      Formatter =
        function
        | VoidElement _ -> None

        | NonVoidElement(innerText, _) -> innerText |> Some

    }
    |> applyFormatter

/// <summary>
/// Format the given doc comments text to HTML
/// </summary>
/// <param name="text"></param>
/// <returns></returns>
let format (text: string) =
    text
    |> removeSummaryTag
    |> removeParamElement
    |> removeRemarkTag
    |> example
    |> paragraph
    |> block
    |> codeBlock
    |> codeInline // Important: Apply code inline after the codeBlock as we are generating <code> tags
    |> see
    |> paramRef
    |> typeParamRef
    |> list
    |> unescapeSpecialCharacters
    |> Markdown.ToHtml

/// <summary>
/// Extract and format only the summary tag
/// </summary>
/// <param name="text"></param>
/// <returns></returns>
let formatSummaryOnly (text: string) =
    let pattern = tagPattern "summary"

    // Match all the param tags
    match Regex.Match(text, pattern, RegexOptions.IgnoreCase) with
    | m when m.Success ->
        if m.Groups.["void_element"].Success then
            ""
        else if m.Groups.["non_void_element"].Success then
            m.Groups.["non_void_innerText"].Value |> format

        else
            // Should not happen but we are forced to handle it by F# compiler
            ""

    | _ -> ""

/// <summary>
/// Try to extract a specific param tag and format
/// </summary>
/// <returns>
/// Return the formatted param tag doc if found.
///
/// Otherwise, it returns <c>None</c>
/// </returns>
let tryFormatParam (parameterName: string) (text: string) =
    let pattern = tagPattern "param"

    // Match all the param tags
    Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
    // Try find the param tag that has name attribute equal to the parameterName
    |> Seq.tryFind (fun m ->
        if m.Groups.["void_element"].Success then
            false
        else if m.Groups.["non_void_element"].Success then
            let attributes = getAttributes m.Groups.["non_void_attributes"]

            match Map.tryFind "name" attributes with
            | Some name -> name = parameterName

            | None -> false
        else
            // Should not happen but we are forced to handle it by F# compiler
            false)
    // Extract the inner text of the param tag
    |> Option.map (fun m -> m.Groups.["non_void_innerText"].Value |> format)

let tryFormatReturnsOnly (text: string) =
    let pattern = tagPattern "returns"

    match Regex.Match(text, pattern, RegexOptions.IgnoreCase) with
    | m when m.Success ->
        if m.Groups.["void_element"].Success then
            None
        else if m.Groups.["non_void_element"].Success then
            m.Groups.["non_void_innerText"].Value |> format |> Some

        else
            // Should not happen but we are forced to handle it by F# compiler
            None

    | _ -> None
