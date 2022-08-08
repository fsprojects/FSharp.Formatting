// --------------------------------------------------------------------------------------
// F# Markdown (MarkdownParser.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.Parser

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open FSharp.Patterns
open FSharp.Collections
open FSharp.Formatting.Common

// --------------------------------------------------------------------------------------
// Parsing of Markdown - first part handles inline formatting
// --------------------------------------------------------------------------------------

/// Splits a link formatted as `http://link "title"` into a link part
/// and an optional title part (may be wrapped using quote or double-quotes)
let getLinkAndTitle (StringPosition.TrimBoth (input, _n)) =
    let url, title =
        if input.Length = 0 then
            "", None
        else
            let c = input.[input.Length - 1]

            if c = '\'' || c = '"' then
                let start = input.IndexOf(c)

                input.Substring(0, start).Trim(),
                Some(
                    input
                        .Substring(start + 1, input.Length - 2 - start)
                        .Trim()
                )
            else
                input, None

    url.TrimStart('<').TrimEnd('>'), title

/// Succeeds when the specified character list starts with an escaped
/// character - in that case, returns the character and the tail of the list
let inline (|EscapedChar|_|) input =
    match input with
    | '\\' :: (('*'
    | '\\'
    | '`'
    | '_'
    | '{'
    | '}'
    | '['
    | ']'
    | '('
    | ')'
    | '>'
    | '#'
    | '.'
    | '!'
    | '+'
    | '-'
    | '$') as c) :: rest -> Some(c, rest)
    | _ -> None

/// Escape dollar inside a LaTex inline math span.
let inline (|EscapedLatexInlineMathChar|_|) input =
    match input with
    | '\\' :: (('$') as c) :: rest -> Some(c, rest)
    | _ -> None

/// Matches a list if it starts with a sub-list that is delimited
/// using the specified delimiters. Returns a wrapped list and the rest.
///
/// This is similar to `List.Delimited`, but it skips over escaped characters.
let (|DelimitedMarkdown|_|) bracket input =
    let _startl, endl = bracket, bracket
    // Like List.partitionUntilEquals, but skip over escaped characters
    let rec loop acc =
        function
        | EscapedChar (x, xs) -> loop (x :: '\\' :: acc) xs
        | input when List.startsWith endl input ->
            let rest = List.skip bracket.Length input

            match rest with
            | []
            | (' '
            | '\r'
            | '\n'
            | '.'
            | '?'
            | '!') :: _ -> Some(List.rev acc, input)
            | rest ->
                let head = List.take bracket.Length input
                loop [ yield! head; yield! acc ] rest

        | x :: xs -> loop (x :: acc) xs
        | [] -> None
    // If it starts with 'startl', let's search for 'endl'
    if List.startsWith bracket input then
        match loop [] (List.skip bracket.Length input) with
        | Some (pre, post) -> Some(pre, List.skip bracket.Length post)
        | None -> None
    else
        None

/// This is similar to `List.Delimited`, but it skips over Latex inline math characters.
let (|DelimitedLatexDisplayMath|_|) bracket input =
    let _startl, endl = bracket, bracket
    // Like List.partitionUntilEquals, but skip over escaped characters
    let rec loop acc =
        function
        | EscapedLatexInlineMathChar (x, xs) -> loop (x :: '\\' :: acc) xs
        | input when List.startsWith endl input -> Some(List.rev acc, input)
        | x :: xs -> loop (x :: acc) xs
        | [] -> None
    // If it starts with 'startl', let's search for 'endl'
    if List.startsWith bracket input then
        match loop [] (List.skip bracket.Length input) with
        | Some (pre, post) -> Some(pre, List.skip bracket.Length post)
        | None -> None
    else
        None

/// This is similar to `List.Delimited`, but it skips over Latex inline math characters.
let (|DelimitedLatexInlineMath|_|) bracket input =
    let _startl, endl = bracket, bracket
    // Like List.partitionUntilEquals, but skip over escaped characters
    let rec loop acc =
        function
        | EscapedLatexInlineMathChar (x, xs) -> loop (x :: '\\' :: acc) xs
        | input when List.startsWith endl input -> Some(List.rev acc, input)
        | x :: xs -> loop (x :: acc) xs
        | [] -> None
    // If it starts with 'startl', let's search for 'endl'
    if List.startsWith bracket input then
        match loop [] (List.skip bracket.Length input) with
        | Some (pre, post) -> Some(pre, List.skip bracket.Length post)
        | None -> None
    else
        None

/// Recognizes an indirect link written using `[body][key]` or just `[key]`
/// The key can be preceeded by a space or other single whitespace thing.
let (|IndirectLink|_|) =
    function
    | List.BracketDelimited '[' ']' (body, '\r' :: '\n' :: (List.BracketDelimited '[' ']' (List.AsString link, rest))) ->
        Some(body, link, "\r\n[" + link + "]", rest)
    | List.BracketDelimited '['
                            ']'
                            (body,
                             ((' '
                             | '\n') as c) :: (List.BracketDelimited '[' ']' (List.AsString link, rest))) ->
        Some(body, link, c.ToString() + "[" + link + "]", rest)
    | List.BracketDelimited '[' ']' (body, List.BracketDelimited '[' ']' (List.AsString link, rest)) ->
        Some(body, link, "[" + link + "]", rest)
    | List.BracketDelimited '[' ']' (body, rest) -> Some(body, "", "", rest)
    | _ -> None

/// Recognize a direct link written using `[body](http://url "with title")`
let (|DirectLink|_|) =
    function
    | List.BracketDelimited '[' ']' (body, List.BracketDelimited '(' ')' (link, rest)) -> Some(body, link, rest)
    | _ -> None

/// Recognizes an automatic link written using `http://url` or `https://url`
let (|AutoLink|_|) input =
    let linkFor (scheme: string) =
        let prefix = scheme.ToCharArray() |> Array.toList

        match input with
        | List.DelimitedWith prefix [ ' ' ] (List.AsString link, rest, _s, _e) -> Some(scheme + link, ' ' :: rest)
        | List.StartsWith prefix (List.AsString link) -> Some(link, [])
        | _ -> None

    [ "http://"; "https://" ] |> Seq.tryPick linkFor

/// Recognizes some form of emphasis using `**bold**` or `*italic*`
/// (both can be also marked using underscore).
/// TODO: This does not handle nested emphasis well.
let (|Emphasised|_|) =
    function
    | (('_'
    | '*') :: _tail) as input ->
        match input with
        | DelimitedMarkdown [ '_'; '_'; '_' ] (body, rest)
        | DelimitedMarkdown [ '*'; '*'; '*' ] (body, rest) ->
            Some(body, Emphasis >> List.singleton >> (fun s -> Strong(s, None)), rest)
        | DelimitedMarkdown [ '_'; '_' ] (body, rest)
        | DelimitedMarkdown [ '*'; '*' ] (body, rest) -> Some(body, Strong, rest)
        | DelimitedMarkdown [ '_' ] (body, rest)
        | DelimitedMarkdown [ '*' ] (body, rest) -> Some(body, Emphasis, rest)
        | _ -> None
    | _ -> None

let (|HtmlEntity|_|) input =
    match input with
    | '&' :: _ ->
        // regex from reference implementation: https://github.com/commonmark/commonmark.js/blob/da1db1e/lib/common.js#L10
        let re =
            "^&" // beginning expect '&'
            + "(?:" // start non-capturing group
            + "#x[a-f0-9]{1,8}" // hex
            + "|#[0-9]{1,8}" // or decimal
            + "|[a-z][a-z0-9]{1,31}" // or name
            + ")" // end non-capturing group
            + ";" // expect ';'

        let match' = Regex.Match(Array.ofList input |> String, re)

        if match'.Success then
            let entity = match'.Value
            let _, rest = List.splitAt entity.Length input
            Some(entity, rest)
        else
            None
    | _ -> None


/// Defines a context for the main `parseParagraphs` function
type ParsingContext =
    { Links: Dictionary<string, string * option<string>>
      Newline: string
      IsFirst: bool
      CurrentRange: MarkdownRange option
      ParseOptions: MarkdownParseOptions }
    member x.ParseCodeAsOther = (x.ParseOptions &&& MarkdownParseOptions.ParseCodeAsOther) <> enum 0

    member x.ParseNonCodeAsOther = (x.ParseOptions &&& MarkdownParseOptions.ParseNonCodeAsOther) <> enum 0

    member x.AllowYamlFrontMatter = (x.ParseOptions &&& MarkdownParseOptions.AllowYamlFrontMatter) <> enum 0

/// Parses a body of a paragraph and recognizes all inline tags.
let rec parseChars acc input (ctx: ParsingContext) =
    seq {

        // Zero or one literals, depending whether there is some accumulated input and update the ctx
        let accLiterals =
            Lazy<_>.Create
                (fun () ->
                    if List.isEmpty acc then
                        ([], ctx)
                    else
                        let range =
                            match ctx.CurrentRange with
                            | Some (n) -> Some({ n with EndColumn = n.StartColumn + acc.Length })
                            | None -> None

                        let ctx =
                            { ctx with
                                CurrentRange =
                                    match ctx.CurrentRange with
                                    | Some (n) -> Some({ n with StartColumn = n.StartColumn + acc.Length })
                                    | None -> None }

                        let text = String(List.rev acc |> Array.ofList)
                        ([ Literal(text, range) ], ctx))

        match input with
        // Recognizes explicit line-break at the end of line
        | ' ' :: ' ' :: '\r' :: '\n' :: rest
        | ' ' :: ' ' :: ('\n'
        | '\r') :: rest ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield HardLineBreak(ctx.CurrentRange)
            yield! parseChars [] rest ctx

        | HtmlEntity (entity, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield Literal(entity, ctx.CurrentRange)
            yield! parseChars [] rest ctx

        | '&' :: rest -> yield! parseChars (';' :: 'p' :: 'm' :: 'a' :: '&' :: acc) rest ctx

        // Ignore escaped characters that might mean something else
        | EscapedChar (c, rest) -> yield! parseChars (c :: acc) rest ctx

        // Inline code delimited either using double `` or single `
        // (if there are spaces around, then body can contain more backticks)
        | List.DelimitedWith [ '`'; ' ' ] [ ' '; '`' ] (body, rest, s, e)
        | List.DelimitedNTimes '`' (body, rest, s, e) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let rng =
                match ctx.CurrentRange with
                | Some (n) ->
                    Some
                        { n with
                            StartColumn = n.StartColumn + s
                            EndColumn = n.EndColumn - e }
                | None -> None

            yield InlineCode(String(Array.ofList body).Trim(), rng)
            yield! parseChars [] rest ctx

        // Display Latex inline math mode
        | DelimitedLatexDisplayMath [ '$'; '$' ] (body, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield LatexDisplayMath(String(Array.ofList body).Trim(), ctx.CurrentRange)
            yield! parseChars [] rest ctx

        // Inline Latex inline math mode
        | DelimitedLatexInlineMath [ '$' ] (body, rest) ->
            let (value, ctx) = accLiterals.Value

            let ctx =
                { ctx with
                    CurrentRange =
                        match ctx.CurrentRange with
                        | Some (n) -> Some({ n with StartColumn = n.StartColumn + 1 })
                        | None -> None }

            yield! value
            let code = String(Array.ofList body).Trim()

            yield
                LatexInlineMath(
                    code,
                    match ctx.CurrentRange with
                    | Some (n) -> Some({ n with EndColumn = n.StartColumn + code.Length })
                    | None -> None
                )

            yield! parseChars [] rest ctx

        // Inline link wrapped as <http://foo.bar>
        | List.DelimitedWith [ '<' ] [ '>' ] (List.AsString link, rest, _s, _e) when
            Seq.forall (Char.IsWhiteSpace >> not) link
            && (link.Contains("@") || link.Contains("://"))
            ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield DirectLink([ Literal(link, ctx.CurrentRange) ], link, None, ctx.CurrentRange)
            yield! parseChars [] rest ctx
        // Not an inline link - leave as an inline HTML tag
        | List.DelimitedWith [ '<' ] [ '>' ] (tag, rest, _s, _e) ->
            yield! parseChars ('>' :: (List.rev tag) @ '<' :: acc) rest ctx

        // Recognize direct link [foo](http://bar) or indirect link [foo][bar] or auto link http://bar
        | DirectLink (body, link, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let link, title = getLinkAndTitle (String(Array.ofList link), MarkdownRange.zero)

            yield DirectLink(parseChars [] body ctx |> List.ofSeq, link, title, ctx.CurrentRange)
            yield! parseChars [] rest ctx
        | IndirectLink (body, link, original, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let key =
                if String.IsNullOrEmpty(link) then
                    String(body |> Array.ofSeq)
                else
                    link

            yield IndirectLink(parseChars [] body ctx |> List.ofSeq, original, key, ctx.CurrentRange)
            yield! parseChars [] rest ctx
        | AutoLink (link, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield DirectLink([ Literal(link, ctx.CurrentRange) ], link, None, ctx.CurrentRange)
            yield! parseChars [] rest ctx

        // Recognize image - this is a link prefixed with the '!' symbol
        | '!' :: DirectLink (body, link, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let link, title = getLinkAndTitle (String(Array.ofList link), MarkdownRange.zero)

            yield DirectImage(String(Array.ofList body), link, title, ctx.CurrentRange)
            yield! parseChars [] rest ctx
        | '!' :: IndirectLink (body, link, original, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let key =
                if String.IsNullOrEmpty(link) then
                    String(body |> Array.ofSeq)
                else
                    link

            yield IndirectImage(String(Array.ofList body), original, key, ctx.CurrentRange)
            yield! parseChars [] rest ctx

        // Handle emphasised text
        | Emphasised (body, f, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            let body = parseChars [] body ctx |> List.ofSeq
            yield f (body, ctx.CurrentRange)
            yield! parseChars [] rest ctx
        // Encode '<' char if it is not link or inline HTML
        | '<' :: rest -> yield! parseChars (';' :: 't' :: 'l' :: '&' :: acc) rest ctx
        | '>' :: rest -> yield! parseChars (';' :: 't' :: 'g' :: '&' :: acc) rest ctx
        | x :: xs -> yield! parseChars (x :: acc) xs ctx
        | [] ->
            let (value, _ctx) = accLiterals.Value
            yield! value
    }

/// Parse body of a paragraph into a list of Markdown inline spans
let parseSpans (StringPosition.TrimBoth (s, n)) ctx =
    let ctx = { ctx with CurrentRange = Some(n) }

    parseChars [] (s.ToCharArray() |> List.ofArray) ctx |> List.ofSeq

let rec trimSpaces numSpaces (s: string) =
    if numSpaces <= 0 then
        s
    elif s.StartsWith(" ") then
        trimSpaces (numSpaces - 1) (s.Substring(1))
    elif s.StartsWith("\t") then
        trimSpaces (numSpaces - 4) (s.Substring(1))
    else
        s

// --------------------------------------------------------------------------------------
// Parsing of Markdown - second part handles paragraph-level formatting (headings, etc.)
// --------------------------------------------------------------------------------------

/// Recognizes heading, either prefixed with #s or followed by === or --- line
let (|Heading|_|) lines =
    match lines with
    | ((StringPosition.TrimBoth header) as line1) :: ((StringPosition.TrimEnd (StringPosition.EqualsRepeated ("=",
                                                                                                              MarkdownRange.zero))) as line2) :: rest ->
        Some(1, header, [ line1; line2 ], rest)
    | ((StringPosition.TrimBoth header) as line1) :: ((StringPosition.TrimEnd (StringPosition.EqualsRepeated ("-",
                                                                                                              MarkdownRange.zero))) as line2) :: rest ->
        Some(2, header, [ line1; line2 ], rest)
    | (StringPosition.StartsWithRepeated "#" (n, StringPosition.TrimBoth (header, ln)) as line1) :: rest ->
        let header =
            // Drop "##" at the end, but only when it is preceded by some whitespace
            // (For example "## Hello F#" should be "Hello F#")
            if header.EndsWith "#" then
                let noHash = header.TrimEnd [| '#' |]

                if noHash.Length > 0 && Char.IsWhiteSpace(noHash.Chars(noHash.Length - 1)) then
                    noHash
                else
                    header
            else
                header

        Some(n, (header, ln), [ line1 ], rest)
    | _rest -> None

let (|YamlFrontmatter|_|) lines =
    match lines with
    | ("---", p) :: rest ->
        let yaml = rest |> List.takeWhile (fun (l, _) -> l <> "---")

        let yamlTextLines = yaml |> List.map fst

        let rest =
            rest
            |> List.skipWhile (fun (l, _) -> l <> "---")
            |> (function
            | (("---", _) :: t) -> t
            | l -> l)

        Some(yamlTextLines, MarkdownRange.mergeRanges (p :: List.map snd yaml), rest)
    | _ -> None

/// Recognizes a horizontal rule written using *, _ or -
let (|HorizontalRule|_|) (line: string, _n: MarkdownRange) =
    let rec loop ((h, a, u) as arg) i =
        if (h >= 3 || a >= 3 || u >= 3) && i = line.Length then
            Some(line.[0])
        elif i = line.Length then
            None
        elif Char.IsWhiteSpace line.[i] then
            loop arg (i + 1)
        elif line.[i] = '-' && a = 0 && u = 0 then
            loop (h + 1, a, u) (i + 1)
        elif line.[i] = '*' && h = 0 && u = 0 then
            loop (h, a + 1, u) (i + 1)
        elif line.[i] = '_' && a = 0 && h = 0 then
            loop (h, a, u + 1) (i + 1)
        else
            None

    loop (0, 0, 0) 0

/// Recognizes a code block - lines starting with four spaces (including blank)
let (|NestedCodeBlock|_|) lines =
    match lines with
    | Lines.TakeCodeBlock (_numspaces, (Lines.TrimBlank lines as takenLines), rest) when lines <> [] ->
        let code =
            [ for (l, _n) in lines ->
                  if String.IsNullOrEmpty l then
                      ""
                  else
                      trimSpaces 4 l ]

        Some(code @ [ "" ], takenLines, rest, None, "", "")
    | _ -> None

/// Recognizes a fenced code block - starting and ending with at least ``` or ~~~
let (|FencedCodeBlock|_|) lines =
    match lines with
    | (StringPosition.StartsWithNTimesTrimIgnoreStartWhitespace "~" (Let "~" (start, num), indent, header) as takenLine) :: lines
    //    when num > 2
    | (StringPosition.StartsWithNTimesTrimIgnoreStartWhitespace "`" (Let "`" (start, num), indent, header) as takenLine) :: lines when
        num > 2
        ->
        let mutable fenceString = String.replicate num start

        if header.Contains(start) then
            None // info string cannot contain backspaces
        else
            let codeLines, rest =
                lines
                |> List.partitionUntil (fun line ->
                    match [ line ] with
                    // end cannot contain info string afterwards (see http://spec.commonmark.org/0.23/#example-104)
                    // end must be indended with less then 4 spaces: http://spec.commonmark.org/0.23/#example-95
                    | StringPosition.StartsWithNTimesTrimIgnoreStartWhitespace start (n, i, h) :: _ when
                        n >= num && i < 4 && String.IsNullOrWhiteSpace h
                        ->
                        fenceString <- String.replicate n start
                        true
                    | _ -> false)

            let handleIndent (codeLine: string) =
                if codeLine.Length <= indent && String.IsNullOrWhiteSpace codeLine then
                    ""
                elif
                    codeLine.Length > indent
                    && String.IsNullOrWhiteSpace(codeLine.Substring(0, indent))
                then
                    codeLine.Substring(indent, codeLine.Length - indent)
                else
                    codeLine.TrimStart()

            let codeWithoutIndent = [ for (codeLine, _n) in codeLines -> handleIndent codeLine ]

            // langString is the part after ``` and ignoredString is the rest until the line ends.
            let langString, ignoredString =
                if String.IsNullOrWhiteSpace header then
                    "", ""
                else
                    let splits = header.Split((null: char array), StringSplitOptions.RemoveEmptyEntries)

                    match splits |> Seq.tryFind (fun _ -> true) with
                    | None -> "", ""
                    | Some langString ->
                        let ignoredString = header.Substring(header.IndexOf(langString) + langString.Length)

                        langString,
                        (if String.IsNullOrWhiteSpace ignoredString then
                             ""
                         else
                             ignoredString)

            // Handle the ending line
            let takenLines2, codeWithoutIndent, rest =
                match rest with
                | ((hd, n) as takenLine2) :: tl ->
                    let idx = hd.IndexOf(fenceString)

                    if idx > -1 && idx + fenceString.Length <= hd.Length then
                        let _pre = hd.Substring(0, idx)
                        let after = hd.Substring(idx + fenceString.Length)

                        [ takenLine2 ],
                        codeWithoutIndent @ [ "" ],
                        (if String.IsNullOrWhiteSpace after then
                             tl
                         else
                             (after, n) :: tl)
                    else
                        [ takenLine2 ], codeWithoutIndent @ [ "" ], tl
                | _ -> [], codeWithoutIndent, rest

            Some(
                codeWithoutIndent,
                (takenLine :: codeLines @ takenLines2),
                rest,
                Some fenceString,
                langString,
                ignoredString
            )
    | _ -> None

/// Matches when the input starts with a number. Returns the
/// rest of the input, following the last number.
let (|SkipSomeNumbers|_|) (input: string, _n: MarkdownRange) =
    match List.ofSeq input with
    | x :: xs when Char.IsDigit x ->
        let _, rest = List.partitionUntil (Char.IsDigit >> not) xs

        Some(input.Length - rest.Length, rest)
    | _ -> None

/// Recognizes a staring of a list (either 1. or +, *, -).
/// Returns the rest of the line, together with the indent.
let (|ListStart|_|) =
    function
    | StringPosition.TrimStartAndCount (startIndent,
                                        _spaces,
                                        // NOTE: a tab character after +, * or - isn't supported by the reference implementation
                                        // (it will be parsed as paragraph for 0.22)
                                        (StringPosition.StartsWithAny [ "+ "; "* "; "- " (*; "+\t"; "*\t"; "-\t"*)  ] as item)) ->
        let range = snd item

        let li = ((fst item).Substring(2), { range with StartColumn = range.StartColumn + 2 })

        let (StringPosition.TrimStartAndCount (startIndent2, _spaces2, _)) = li

        let endIndent =
            startIndent
            + 2
            +
            // Handle case of code block
            if startIndent2 >= 5 then
                1
            else
                startIndent2

        Some(Unordered, startIndent, endIndent, li)
    | StringPosition.TrimStartAndCount (startIndent,
                                        _spaces,
                                        (SkipSomeNumbers (skipNumCount, '.' :: ' ' :: List.AsString item))) ->
        let (StringPosition.TrimStartAndCount (startIndent2, _spaces2, _)) = (item, MarkdownRange.zero)

        let endIndent =
            startIndent
            + 2
            + skipNumCount
            +
            // Handle case of code block
            if startIndent2 >= 5 then
                1
            else
                startIndent2

        Some(Ordered, startIndent, endIndent, (item, MarkdownRange.zero))
    | _ -> None

/// Splits input into lines until whitespace or starting of a list and the rest.
let (|LinesUntilListOrWhite|) lines =
    lines
    |> List.partitionUntil (function
        | ListStart _
        | StringPosition.WhiteSpace -> true
        | _ -> false)

/// Splits input into lines until not-indented line or starting of a list and the rest.
let (|LinesUntilListOrUnindented|) lines =
    lines
    |> List.partitionUntilLookahead (function
        | (ListStart _
        | StringPosition.Unindented) :: _
        | StringPosition.WhiteSpace :: StringPosition.WhiteSpace :: _ -> true
        | _ -> false)

/// Recognizes a list item until the next list item (possibly nested) or end of a list.
/// The parameter specifies whether the previous line was simple (single-line not
/// separated by a white line - simple items are not wrapped in <p>)
let (|ListItem|_|) prevSimple lines =
    match lines with
    // Take remaining lines that belong to the same item
    // (everything until an empty line or start of another list item)
    //
    // Then take more things that belong to the item -
    // the value 'more' will contain indented paragraphs
    | (ListStart (kind, startIndent, endIndent, item) as takenLine) :: LinesUntilListOrWhite (continued,
                                                                                              (LinesUntilListOrUnindented (more,
                                                                                                                           rest) as next)) ->
        let simple =
            match item with
            | StringPosition.TrimStartAndCount (_, spaces, _) when spaces >= 4 ->
                // Code Block
                false
            | _ ->
                match next, rest with
                | StringPosition.WhiteSpace :: _, (ListStart _) :: _ -> false
                | (ListStart _) :: _, _ -> true
                | [], _ -> true
                | [ StringPosition.WhiteSpace ], _ -> true
                | StringPosition.WhiteSpace :: StringPosition.WhiteSpace :: _, _ -> true
                | _, StringPosition.Unindented :: _ -> prevSimple
                | _, _ -> false

        let lines =
            [ yield item
              for (line, n) in continued do
                  yield (line.Trim(), n)
              for (line, n) in more do
                  let trimmed = trimSpaces endIndent line
                  yield (trimmed, { n with StartColumn = n.StartColumn + line.Length - trimmed.Length }) ]
        //let trimmed = line.TrimStart()
        //if trimmed.Length >= line.Length - endIndent then yield trimmed
        //else yield line.Substring(endIndent) ]
        Some(startIndent, (simple, kind, lines), (takenLine :: continued @ more), rest)
    | _ -> None

/// Recognizes a list - returns list items with information about
/// their indents - these need to be turned into a tree structure later.
let rec (|ListItems|_|) prevSimple lines =
    match lines with
    | ListItem prevSimple (indent, ((nextSimple, _, _) as info), takenLines, rest) ->
        match rest with
        | ((HorizontalRule _) as takenLine2) :: _ -> Some([ indent, info ], takenLines @ [ takenLine2 ], rest)
        | ListItems nextSimple (items, takenLines2, rest) ->
            Some((indent, info) :: items, (takenLines @ takenLines2), rest)
        | _ -> Some([ indent, info ], takenLines, rest)
    | _ -> None


// Code for parsing pipe tables

// Splits table row into deliminated parts escaping inline code and latex
let rec pipeTableFindSplits (delim: char array) (line: char list) =
    let cLstToStr (x: char list) =
        x |> Array.ofList |> System.String.Concat

    let rec ptfs delim line =
        match line with
        | DelimitedLatexDisplayMath [ '$'; '$' ] (_body, rest) -> ptfs delim rest
        | DelimitedLatexInlineMath [ '$' ] (_body, rest) -> ptfs delim rest
        | List.DelimitedWith [ '`'; ' ' ] [ ' '; '`' ] (_body, rest, _s, _e) -> ptfs delim rest
        | List.DelimitedNTimes '`' (_body, rest, _s, _e) -> ptfs delim rest
        | x :: rest when Array.exists ((=) x) delim -> Some rest
        | '\\' :: _ :: rest
        | _ :: rest -> ptfs delim rest
        | [] -> None

    let rest = ptfs delim line

    match rest with
    | None -> [ cLstToStr line ]
    | Some _x when line = [] -> [ "" ]
    | Some x ->
        let chunkSize = List.length line - List.length x - 1

        cLstToStr (Seq.take chunkSize line |> Seq.toList) :: pipeTableFindSplits delim x





/// Recognizes alignment specified in the passed separator line.
let (|TableCellSeparator|_|) =
    function
    | StringPosition.StartsAndEndsWith (":", ":") (StringPosition.EqualsRepeated ("-", MarkdownRange.zero)) ->
        Some(AlignCenter)
    | StringPosition.StartsWith ":" (StringPosition.EqualsRepeated ("-", MarkdownRange.zero)) -> Some(AlignLeft)
    | StringPosition.StartsAndEndsWith ("", ":") (StringPosition.EqualsRepeated ("-", MarkdownRange.zero)) ->
        Some(AlignRight)
    | StringPosition.EqualsRepeated ("-", MarkdownRange.zero) -> Some(AlignDefault)
    | _ -> None

/// Recognizes row of pipe table.
/// The function takes number of expected columns and array of delimiters.
/// Returns list of strings between delimiters.
let (|PipeTableRow|_|) (size: option<int>) delimiters (line: string, n: MarkdownRange) =
    let parts =
        pipeTableFindSplits delimiters (line.ToCharArray() |> Array.toList)
        |> List.toArray
        |> Array.map (fun s -> (s.Trim(), n))

    let n = parts.Length

    let m = if size.IsNone then 1 else size.Value

    let x =
        if String.IsNullOrEmpty(fst parts.[0]) && n > m then
            1
        else
            0

    let y =
        if String.IsNullOrEmpty(fst parts.[n - 1]) && n - x > m then
            n - 2
        else
            n - 1

    if n = 1 || (size.IsSome && y - x + 1 <> m) then
        None
    else
        Some(parts.[x..y] |> Array.toList)


/// Recognizes separator row of pipe table.
/// Returns list of alignments.
let (|PipeSeparatorRow|_|) size =
    function
    | PipeTableRow size [| '|'; '+' |] parts ->
        let alignments = parts |> List.choose (|TableCellSeparator|_|)

        if parts.Length <> alignments.Length then
            None
        else
            (Some alignments)
    | _ -> None

/// Recognizes pipe table
let (|PipeTableBlock|_|) input =
    let rec getTableRows size acc takenLinesAcc lines =
        match lines with
        | (PipeTableRow size [| '|' |] columns) as takenLine :: rest ->
            getTableRows size (List.map (fun l -> [ l ]) columns :: acc) (takenLine :: takenLinesAcc) rest
        | rest -> (List.rev acc, List.rev takenLinesAcc, rest)

    match input with
    | (PipeSeparatorRow None alignments) as takenLine :: rest ->
        let rows, takenLines, others = getTableRows (Some alignments.Length) [] [] rest

        Some((None, alignments, rows), takenLine :: takenLines, others)
    | ((PipeTableRow None [| '|' |] headers) as takenLine) :: rest ->
        match rest with
        | ((PipeSeparatorRow (Some headers.Length) alignments) as takenLine2) :: rest ->
            let rows, takenLines, others = getTableRows (Some headers.Length) [] [] rest

            let header_paragraphs = headers |> List.map (fun l -> [ l ])
            Some((Some(header_paragraphs), alignments, rows), takenLine :: takenLine2 :: takenLines, others)
        | _ -> None
    | _ -> None

// Code for parsing emacs tables

/// Recognizes one line of emacs table. It can be line with content or separator line.
/// The function takes positions of grid columns (if known) and expected grid separator.
/// Passed function is used to check whether all parts within grid are valid.
/// Retuns tuple (position of grid columns, text between grid columns).
let (|EmacsTableLine|_|)
    (grid: option<int[]>)
    (c: char)
    (check: string * MarkdownRange -> bool)
    (line: string, _n: MarkdownRange)
    =
    let p =
        if grid.IsSome then
            grid.Value
        else
            Array.FindAll([| 0 .. line.Length - 1 |], (fun i -> line.[i] = c))

    let n = p.Length - 1

    if n < 2 || line.Length <= p.[n] || Array.exists (fun i -> line.[i] <> c) p then
        None
    else
        let parts =
            [ 1..n ]
            |> List.map (fun i ->
                let rng =
                    { StartLine = n
                      StartColumn = 0
                      EndLine = n
                      EndColumn = p.[i] - p.[i - 1] - 1 }

                line.Substring(p.[i - 1] + 1, p.[i] - p.[i - 1] - 1), rng)

        if List.forall check parts then
            Some(p, parts)
        else
            None

/// Recognizes emacs table
let (|EmacsTableBlock|_|) (lines) =
    let isCellSep s =
        match s with
        | StringPosition.EqualsRepeated ("-", MarkdownRange.zero) _ -> true
        | _ -> false

    let isAlignedCellSep = (|TableCellSeparator|_|) >> Option.isSome

    let isHeadCellSep s =
        match s with
        | StringPosition.EqualsRepeated ("=", MarkdownRange.zero) _ -> true
        | _ -> false

    let isText (_s: string, _n: MarkdownRange) = true

    match lines with
    | ((EmacsTableLine None '+' isAlignedCellSep (grid, parts)) as takenLine) :: rest ->
        let alignments = List.choose (|TableCellSeparator|_|) parts
        // iterate over rows and go from state to state
        // headers - the content of head row (initially none)
        // prevRow - content of the processed rows
        // cur - list of paragraphs in the current row (list of empty lists after each separator line)
        // flag indicates whether current row is empty (similar to List.forall (List.isEmpty) cur)
        let emptyCur = List.replicate<(string * MarkdownRange) list> (grid.Length - 1) []

        let rec loop
            flag
            takenLines2
            headers
            (prevRows: (string * MarkdownRange) list list list)
            (cur: (string * MarkdownRange) list list)
            lines
            =
            match lines with
            | ((EmacsTableLine (Some grid) '|' isText (_, parts)) as takenLine2) :: others ->
                loop
                    false
                    (takenLine2 :: takenLines2)
                    headers
                    prevRows
                    (List.zip parts cur |> List.map (fun ((h, n), t) -> (h.TrimEnd(), n) :: t))
                    others
            | ((EmacsTableLine (Some grid) '+' isCellSep _) as takenLine2) :: others ->
                loop true (takenLine2 :: takenLines2) headers (List.map (List.rev) cur :: prevRows) emptyCur others
            | ((EmacsTableLine (Some grid) '+' isHeadCellSep _) as takenLine2) :: others when Option.isNone headers ->
                loop true (takenLine2 :: takenLines2) (Some(List.map (List.rev) cur)) prevRows emptyCur others
            | others when flag ->
                Some((headers, alignments, List.rev prevRows), takenLine :: List.rev takenLines2, others)
            | _ -> None

        loop true [] None [] emptyCur rest
    | _ -> None

/// Recognizes a start of a blockquote
let (|BlockquoteStart|_|) (line: string, n: MarkdownRange) =
    let regex =
        "^ {0,3}" // Up to three leading spaces
        + ">" // Blockquote character
        + "\s?" // Maybe one whitespace character
        + "(.*)" // Capture everything else

    let match' = Regex.Match(line, regex)

    if match'.Success then
        let group = match'.Groups.Item(1)

        Some(
            group.Value,
            { n with
                StartColumn = n.StartColumn + group.Index
                EndColumn = n.StartColumn + group.Index + group.Length }
        )
    else
        None

/// Takes lines that belong to a continuing paragraph until
/// a white line or start of other paragraph-item is found
let (|TakeParagraphLines|_|) input =
    match
        List.partitionWhileLookahead
            (function
            | Heading _ -> false
            | FencedCodeBlock _ -> false
            | BlockquoteStart _ :: _ -> false
            | StringPosition.WhiteSpace :: _ -> false
            | _ -> true)
            input
        with
    | matching, rest when matching <> [] -> Some(matching, rest)
    | _ -> None

/// Recognize nested HTML block
/// TODO: This is too simple - takes paragraph that starts with <
let (|HtmlBlock|_|) (lines: (string * MarkdownRange) list) =
    match lines with
    | (first, _n) :: _ when first.StartsWith("<") ->
        match lines with
        | TakeParagraphLines (html, rest) -> Some(html, html, rest)
        | _ -> None
    | _ -> None

/// "Markdown allows you to be lazy and only put the > before the first line of a hard-wrapped paragraph"
// Continues taking lines until a whitespace line or start of a blockquote
let (|LinesUntilBlockquoteEnds|) input =
    input
    |> List.partitionUntilLookahead (fun next ->
        match next with
        | BlockquoteStart _ :: _ -> true
        | Heading _ -> true
        | StringPosition.WhiteSpace :: _ -> true
        | _ -> false)

/// Recognizes blockquote - continues taking paragraphs
/// starting with '>' until there is something else
let rec (|Blockquote|_|) lines =
    match lines with
    | EmptyBlockquote (takenLines, Lines.TrimBlankStart (takenLines2, rest)) ->
        Some([ ("", MarkdownRange.zero) ], takenLines @ takenLines2, rest)
    | (BlockquoteStart (quoteLine) as takenLine) :: LinesUntilBlockquoteEnds (continued,
                                                                              Lines.TrimBlankStart (takenLines2, rest)) ->
        let moreQuoteLines, moreTakenLines, rest =
            match rest with
            | Blockquote (lines, takenLines, rest) -> lines, takenLines, rest
            | _ -> [], [], rest

        Some(quoteLine :: continued @ moreQuoteLines, takenLine :: continued @ takenLines2 @ moreTakenLines, rest)
    | _ -> None

/// Recognizes a special case: an empty blockquote line should terminate
/// the blockquote if the next line is not a blockquote
and (|EmptyBlockquote|_|) lines =
    match lines with
    | BlockquoteStart (StringPosition.WhiteSpace) :: Blockquote (_) -> None
    | (BlockquoteStart (StringPosition.WhiteSpace) as takenLine) :: rest -> Some([ takenLine ], rest)
    | _ -> None

/// Recognizes Latex block
///   1. indented paragraph starting with "$$$".  This is F#-literate-specific, not part of the usual
///      ipynb way of putting latex in markdown. The "raw markdown" version of this (takenLines) has
///      a \begin{equation} \end{equation} wrapping instead of "$$$"
///
///   2. Single line latex starting with `$$`.
///
///   3. Block delimited by \begin{equation} \end{equation}.
///
///   4. Block delimited by \begin{align} \end{align}.
///
/// See formats accepted at https://stackoverflow.com/questions/13208286/how-to-write-latex-in-ipython-notebook
let (|LatexBlock|_|) (lines: (string * MarkdownRange) list) =
    match lines with
    | (first, n) as _takenLine1 :: rest when (first.TrimEnd()) = "$$$" ->
        match rest with
        | TakeParagraphLines (body, rest) ->
            Some("equation", body, ((@"\begin{equation}", n) :: body @ [ (@"\end{equation}", n) ]), rest)
        | _ -> None
    | ((first, n) as takenLine) :: rest when
        first.TrimEnd().StartsWith("$$")
        && first.TrimEnd().EndsWith("$$")
        && first.TrimEnd().Length >= 4
        ->
        let text = first.TrimEnd()
        Some("equation", [ (text.[2 .. text.Length - 3], n) ], [ takenLine ], rest)
    | ((first, _n) as takenLine) :: rest when (first.TrimEnd()) = @"\begin{equation}" ->
        let body = rest |> List.takeWhile (fun s -> fst s <> @"\end{equation}")

        let res = rest |> List.skipWhile (fun s -> fst s <> @"\end{equation}")

        match res with
        | _ :: rest -> Some("equation", body, takenLine :: body, rest)
        | [] -> None
    | ((first, _n) as takenLine) :: rest when (first.TrimEnd()) = @"\begin{align}" ->
        let body = rest |> List.takeWhile (fun s -> fst s <> @"\end{align}")

        let res = rest |> List.skipWhile (fun s -> fst s <> @"\end{align}")

        match res with
        | _ :: rest -> Some("align", body, takenLine :: body, rest)
        | [] -> None
    | _ -> None

/// Recognize a definition of a link as in `[key]: http://url ...`
let (|LinkDefinition|_|) s =
    match s with
    | ((StringPosition.StartsWithWrapped ("[", "]:") (wrapped, StringPosition.TrimBoth link)
    | StringPosition.StartsWithWrapped (" [", "]:") (wrapped, StringPosition.TrimBoth link)
    | StringPosition.StartsWithWrapped ("  [", "]:") (wrapped, StringPosition.TrimBoth link)
    | StringPosition.StartsWithWrapped ("   [", "]:") (wrapped, StringPosition.TrimBoth link)) as line) :: rest ->
        Some((wrapped, link), [ line ], rest)
    | _ -> None

let updateCurrentRange lines =
    match lines with
    | [] -> None
    | (_, l) :: _ -> Some(l)

/// Parse a list of lines into a sequence of markdown paragraphs
let rec parseParagraphs (ctx: ParsingContext) (lines: (string * MarkdownRange) list) =
    seq {
        let ctx = { ctx with CurrentRange = updateCurrentRange lines }

        let frontMatter, (Lines.TrimBlankStart (_, moreLines)) =
            if ctx.IsFirst && ctx.AllowYamlFrontMatter then
                match lines with
                | YamlFrontmatter (yaml, loc, rest) -> Some(YamlFrontmatter(yaml, Some loc)), rest
                | _ -> None, lines
            else
                None, lines

        match frontMatter with
        | None -> ()
        | Some p -> yield p

        let ctx = { ctx with CurrentRange = updateCurrentRange moreLines }

        let ctx = { ctx with IsFirst = false }

        match moreLines with
        // Recognize various kinds of standard paragraphs
        | LinkDefinition ((key, link), takenLines, Lines.TrimBlankStart (takenLines2, lines)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                if ctx.Links.ContainsKey(key) then
                    failwithf "Cannot define a link to the reference %s twice." key

                ctx.Links.Add(key, getLinkAndTitle link)

            yield! parseParagraphs ctx lines

        | NestedCodeBlock (code,
                           takenLines,
                           Lines.TrimBlankStart (takenLines2, lines),
                           fenceString,
                           langString,
                           ignoredLine)
        | FencedCodeBlock (code,
                           takenLines,
                           Lines.TrimBlankStart (takenLines2, lines),
                           fenceString,
                           langString,
                           ignoredLine) ->
            if ctx.ParseCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let code = code |> String.concat ctx.Newline
                yield CodeBlock(code, None, fenceString, langString, ignoredLine, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | Blockquote (body, takenLines, Lines.TrimBlankStart (takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield
                    QuotedBlock(
                        parseParagraphs ctx (body @ [ ("", MarkdownRange.zero) ]) |> List.ofSeq,
                        ctx.CurrentRange
                    )

            yield! parseParagraphs ctx rest

        | EmacsTableBlock ((headers, alignments, rows), takenLines, Lines.TrimBlankStart (takenLines2, rest))
        | PipeTableBlock ((headers, alignments, rows), takenLines, Lines.TrimBlankStart (takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let headParagraphs =
                    if headers.IsNone then
                        None
                    else
                        Some(headers.Value |> List.map (fun i -> parseParagraphs ctx i |> List.ofSeq))

                let rows = rows |> List.map (List.map (fun i -> parseParagraphs ctx i |> List.ofSeq))

                yield TableBlock(headParagraphs, alignments, rows, ctx.CurrentRange)

            yield! parseParagraphs ctx rest

        | HorizontalRule (c) as takenLine :: (Lines.TrimBlankStart (takenLines2, lines)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLine :: takenLines2, ctx.CurrentRange)
            else
                yield HorizontalRule(c, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | LatexBlock (env, body, takenLines, Lines.TrimBlankStart (takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield LatexBlock(env, body |> List.map fst, ctx.CurrentRange)

            yield! parseParagraphs ctx rest


        // Recognize list of list items and turn it into nested lists
        | ListItems true (items, takenLines, Lines.TrimBlankStart (takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let tree = Tree.ofIndentedList items

                // Nest all items that have another kind (i.e. UL vs. OL)
                let rec nestUnmatchingItems items =
                    match items with
                    | Node ((_, baseKind, _), _) :: _ ->
                        items
                        |> List.nestUnderLastMatching (fun (Node ((_, kind, _), _)) -> kind = baseKind)
                        |> List.map (fun (Node (info, children), nested) ->
                            let children = nestUnmatchingItems children
                            Node(info, children @ nested))
                    | [] -> []

                // Turn tree into nested list definitions
                let rec formatTree (nodes: Tree<bool * MarkdownListKind * (string * MarkdownRange) list> list) =
                    let kind =
                        match nodes with
                        | Node ((_, kind, _), _) :: _ -> kind
                        | _ -> Unordered

                    let items =
                        [ for (Node ((simple, _, body), nested)) in nodes ->
                              [ let rng = body |> List.map snd |> MarkdownRange.mergeRanges

                                if not simple then
                                    yield! parseParagraphs ctx body
                                else
                                    yield
                                        MarkdownParagraph.Span(
                                            parseSpans (body |> List.map fst |> String.concat ctx.Newline, rng) ctx,
                                            ctx.CurrentRange
                                        )

                                if nested <> [] then
                                    yield formatTree nested ] ]

                    ListBlock(kind, items, ctx.CurrentRange)

                // Make sure all items of the list have are either simple or not.
                let rec unifySimpleProperty
                    (nodes: Tree<bool * MarkdownListKind * (string * MarkdownRange) list> list)
                    =
                    let containsNonSimple =
                        tree
                        |> Seq.exists (function
                            | Node ((false, _, _), _) -> true
                            | _ -> false)

                    if containsNonSimple then
                        nodes
                        |> List.map (function
                            | Node ((_, kind, content), nested) ->
                                Node((false, kind, content), unifySimpleProperty nested))
                    else
                        nodes

                yield tree |> unifySimpleProperty |> formatTree

            yield! parseParagraphs ctx rest

        // Recognize remaining types of paragraphs
        | Heading (n, body, takenLines, Lines.TrimBlankStart (takenLines2, lines)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield Heading(n, parseSpans body ctx, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | HtmlBlock (code, takenLines, Lines.TrimBlankStart (takenLines2, lines)) when
            (let all = String.concat ctx.Newline (code |> List.map fst)

             not (all.StartsWith("<http://"))
             && not (all.StartsWith("<ftp://"))
             && not (all.Contains("@")))
            ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let all = String.concat ctx.Newline (code |> List.map fst)

                yield InlineHtmlBlock(all, None, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | TakeParagraphLines ((Lines.TrimParagraphLines lines as takenLines), Lines.TrimBlankStart (takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield
                    Paragraph(
                        parseSpans
                            (lines |> List.map fst |> String.concat ctx.Newline,
                             lines |> List.map snd |> MarkdownRange.mergeRanges)
                            ctx,
                        ctx.CurrentRange
                    )

            yield! parseParagraphs ctx rest

        | Lines.TrimBlankStart (_takenLines2, []) -> ()

        | _ -> failwithf "Unexpectedly stopped!\n%A" moreLines
    }
