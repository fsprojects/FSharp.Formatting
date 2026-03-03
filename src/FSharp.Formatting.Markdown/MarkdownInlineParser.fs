// --------------------------------------------------------------------------------------
// F# Markdown (MarkdownInlineParser.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.InlineParser

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open FSharp.Patterns
open FSharp.Collections
open FSharp.Formatting.Common

// --------------------------------------------------------------------------------------
// Parsing of Markdown - inline formatting (spans, characters, emphasis, links)
// --------------------------------------------------------------------------------------

/// Splits a link formatted as `http://link "title"` into a link part
/// and an optional title part (may be wrapped using quote or double-quotes)
let getLinkAndTitle (StringPosition.TrimBoth(input, _n)) =
    let url, title =
        if input.Length = 0 then
            "", None
        else
            let c = input.[input.Length - 1]

            if c = '\'' || c = '"' then
                let start = input.IndexOf(c)

                input.Substring(0, start).Trim(), Some(input.Substring(start + 1, input.Length - 2 - start).Trim())
            else
                input, None

    url.TrimStart('<').TrimEnd('>'), title

/// Succeeds when the specified character list starts with an escaped
/// character - in that case, returns the character and the tail of the list
let (|EscapedChar|_|) input =
    match input with
    | '\\' :: (('*' | '\\' | '`' | '_' | '{' | '}' | '[' | ']' | '(' | ')' | '>' | '#' | '.' | '!' | '+' | '-' | '$') as c) :: rest ->
        Some(c, rest)
    | _ -> None

/// Escape dollar inside a LaTex inline math span.
let (|EscapedLatexInlineMathChar|_|) input =
    match input with
    | '\\' :: (('$') as c) :: rest -> Some(c, rest)
    | _ -> None

/// Succeeds when the specificed character list starts with non-escaped punctuation.
let (|Punctuation|_|) input =
    match input with
    | EscapedChar _ -> None
    | _ ->
        // from https://github.com/commonmark/commonmark.js/blob/master/lib/inlines.js#L38
        let re =
            """^[!"#$%&'()*+,\-./:;<=>?@\[\]\\^_`{|}~\xA1\xA7\xAB\xB6\xB7\xBB\xBF\u037E\u0387\u055A-\u055F\u0589\u058A\u05BE\u05C0\u05C3\u05C6\u05F3\u05F4\u0609\u060A\u060C\u060D\u061B\u061E\u061F\u066A-\u066D\u06D4\u0700-\u070D\u07F7-\u07F9\u0830-\u083E\u085E\u0964\u0965\u0970\u0AF0\u0DF4\u0E4F\u0E5A\u0E5B\u0F04-\u0F12\u0F14\u0F3A-\u0F3D\u0F85\u0FD0-\u0FD4\u0FD9\u0FDA\u104A-\u104F\u10FB\u1360-\u1368\u1400\u166D\u166E\u169B\u169C\u16EB-\u16ED\u1735\u1736\u17D4-\u17D6\u17D8-\u17DA\u1800-\u180A\u1944\u1945\u1A1E\u1A1F\u1AA0-\u1AA6\u1AA8-\u1AAD\u1B5A-\u1B60\u1BFC-\u1BFF\u1C3B-\u1C3F\u1C7E\u1C7F\u1CC0-\u1CC7\u1CD3\u2010-\u2027\u2030-\u2043\u2045-\u2051\u2053-\u205E\u207D\u207E\u208D\u208E\u2308-\u230B\u2329\u232A\u2768-\u2775\u27C5\u27C6\u27E6-\u27EF\u2983-\u2998\u29D8-\u29DB\u29FC\u29FD\u2CF9-\u2CFC\u2CFE\u2CFF\u2D70\u2E00-\u2E2E\u2E30-\u2E42\u3001-\u3003\u3008-\u3011\u3014-\u301F\u3030\u303D\u30A0\u30FB\uA4FE\uA4FF\uA60D-\uA60F\uA673\uA67E\uA6F2-\uA6F7\uA874-\uA877\uA8CE\uA8CF\uA8F8-\uA8FA\uA8FC\uA92E\uA92F\uA95F\uA9C1-\uA9CD\uA9DE\uA9DF\uAA5C-\uAA5F\uAADE\uAADF\uAAF0\uAAF1\uABEB\uFD3E\uFD3F\uFE10-\uFE19\uFE30-\uFE52\uFE54-\uFE61\uFE63\uFE68\uFE6A\uFE6B\uFF01-\uFF03\uFF05-\uFF0A\uFF0C-\uFF0F\uFF1A\uFF1B\uFF1F\uFF20\uFF3B-\uFF3D\uFF3F\uFF5B\uFF5D\uFF5F-\uFF65]|\uD800[\uDD00-\uDD02\uDF9F\uDFD0]|\uD801\uDD6F|\uD802[\uDC57\uDD1F\uDD3F\uDE50-\uDE58\uDE7F\uDEF0-\uDEF6\uDF39-\uDF3F\uDF99-\uDF9C]|\uD804[\uDC47-\uDC4D\uDCBB\uDCBC\uDCBE-\uDCC1\uDD40-\uDD43\uDD74\uDD75\uDDC5-\uDDC9\uDDCD\uDDDB\uDDDD-\uDDDF\uDE38-\uDE3D\uDEA9]|\uD805[\uDCC6\uDDC1-\uDDD7\uDE41-\uDE43\uDF3C-\uDF3E]|\uD809[\uDC70-\uDC74]|\uD81A[\uDE6E\uDE6F\uDEF5\uDF37-\uDF3B\uDF44]|\uD82F\uDC9F|\uD836[\uDE87-\uDE8B]"""

        let match' = Regex.Match(Array.ofList input |> String, re)

        if match'.Success then
            let entity = match'.Value
            let _, rest = List.splitAt entity.Length input
            Some(char entity, rest)
        else
            None

let (|NotPunctuation|_|) input =
    match input with
    | Punctuation _ -> None
    | _ -> Some input

module Char =
    let (|WhiteSpace|_|) (input: char list) =
        match input with
        | [] -> Some input
        | x :: _xs -> if Char.IsWhiteSpace x then Some input else None

    let (|NotWhiteSpace|_|) input =
        match input with
        | WhiteSpace _ -> None
        | _ -> Some input

/// Succeeds when the specificed character list starts with a delimeter run.
let (|DelimiterRun|_|) input =
    match input with
    | ('*' | '_') :: _tail as (h :: t) ->
        let run, rest = List.partitionWhile (fun x -> x = h) (h :: t)
        Some(run, rest)
    | _ -> None

/// Succeeds when there's a match to a string of * or _ that could
/// open emphasis.
let (|LeftDelimiterRun|_|) input =
    match input with
    // (1) Not followed by [Unicode whitespace] and
    // (2a) not followed by a [Unicode punctuation character] or
    // (2b) followed by a [Unicode punctuation character] and
    // preceded by [Unicode whitespace] or a [Unicode punctuation character].
    //
    // Passes 1 and 2a.
    | DelimiterRun(_, Char.NotWhiteSpace _) & DelimiterRun(run, NotPunctuation xs) -> Some([], run, xs)
    | _ :: DelimiterRun(_, Char.NotWhiteSpace _) & h :: DelimiterRun(run, NotPunctuation xs) -> Some([ h ], run, xs)
    // Passes 1 and 2b
    | h :: DelimiterRun(run, Punctuation(x, xs)) ->
        match [ h ] with
        | Char.WhiteSpace _
        | Punctuation _ -> Some([ h ], run, x :: xs)
        | _ -> None
    // Passes 1 and 2b when the run is at the start of the line.
    // |CannotStartEmphasis| ensures that we don't match this
    // when we've previously discarded a leading character.
    | DelimiterRun(run, Punctuation(x, xs)) -> Some([], run, x :: xs)
    | _ -> None

/// Succeeds when there's a match to a string of * or _ that could
/// close emphasis.
let (|RightDelimiterRun|_|) input =
    match input with
    // A right-flanking delimiter run is
    // 1. not preceded by [Unicode whitepace]
    // 2. And either
    //    a. not preceded by a [Unicode punctuation character], or
    //    b. preceded by a [Unicode punctuation character] and
    //       followed by [Unicode whitespace] or a [Unicode punctuation character]
    //
    // An escaped character followed by delimiter run matches 1 and 2a.
    | EscapedChar(x, DelimiterRun(run, xs)) -> Some([ '\\'; x ], run, xs)
    | EscapedChar _ -> None
    | Char.NotWhiteSpace _ & x :: DelimiterRun(run, xs) ->
        match input with
        // 1 and 2a
        | NotPunctuation _ -> Some([ x ], run, xs)
        // 1 and 2b
        | Punctuation(x, DelimiterRun(run, Char.WhiteSpace ys)) -> Some([ x ], run, ys)
        // 1 and 2b
        | Punctuation(x, DelimiterRun(run, Punctuation(y, ys))) -> Some([ x ], run, y :: ys)
        | _ -> None
    | _ -> None

/// Matches ['c',LeftDelimiterRun]::xs that should
/// not open emphasis. This is useful because the
/// parser iterates through characters one by one and
/// in this case we need to skip both 'c' and the LeftDelimiterRun.
/// If we only skipped 'c' then we could match LeftDelimiterRun
/// on the next iteration and we do not want that to happen.
let (|CannotOpenEmphasis|_|) input =
    match input with
    // Rule #2: A single `_` character [can open emphasis] iff
    //  it is part of a [left-flanking delimiter run]
    //  and either (a) not part of a [right-flanking delimiter run]
    //  or (b) part of a [right-flanking delimiter run]
    //  preceded by a [Unicode punctuation character].
    | LeftDelimiterRun _ & RightDelimiterRun(pre, [ '_' ], post) ->
        match List.rev pre with
        | Punctuation _ -> None
        | revPre -> Some('_' :: revPre, post)
    // We cannot pass 1 and 2b of the left flanking rule
    // when h is neither white space nor punctuation.
    | h :: DelimiterRun(run, Punctuation(x, xs)) ->
        match [ h ] with
        | Char.WhiteSpace _
        | Punctuation _ -> None
        | _ -> Some(List.rev (h :: run), x :: xs)
    | _ -> None

/// Matches a list if it starts with a sub-list that is delimited
/// using the specified delimiters. Returns a wrapped list and the rest.
///
/// This is similar to `List.Delimited`, but it skips over escaped characters.
let (|DelimitedMarkdown|_|) bracket input =
    let _startl, endl = bracket, bracket
    // Like List.partitionUntilEquals, but skip over escaped characters
    let rec loop acc count =
        function
        | (RightDelimiterRun(pre, [ '_' ], post) as input) when endl = [ '_' ] ->
            match input with
            | LeftDelimiterRun(pre, run, (Punctuation _ as post)) ->
                if count = 0 then
                    Some((List.rev acc) @ pre, run @ post)
                else
                    loop ((List.rev (pre @ run)) @ acc) (count - 1) post
            | LeftDelimiterRun(pre, run, post) -> loop ((List.rev (pre @ run)) @ acc) (count + 1) post
            | _ -> Some((List.rev acc) @ pre, [ '_' ] @ post)
        | RightDelimiterRun(pre, run, post) when endl = run ->
            if count = 0 then
                Some((List.rev acc) @ pre, run @ post)
            else
                loop ((List.rev (pre @ run)) @ acc) (count - 1) post
        | EscapedChar(x, xs) -> loop (x :: '\\' :: acc) count xs
        | LeftDelimiterRun(pre, run, post) when run = endl -> loop ((List.rev (pre @ run)) @ acc) (count + 1) post
        | x :: xs -> loop (x :: acc) count xs
        | [] -> None
    // If it starts with 'startl', let's search for 'endl'
    if List.startsWith bracket input then
        match loop [] 0 (List.skip bracket.Length input) with
        | Some(pre, post) -> Some(pre, List.skip bracket.Length post)
        | None -> None
    else
        None

/// This is similar to `List.Delimited`, but it skips over Latex inline math characters.
let (|DelimitedLatexDisplayMath|_|) bracket input =
    let _startl, endl = bracket, bracket
    // Like List.partitionUntilEquals, but skip over escaped characters
    let rec loop acc =
        function
        | EscapedLatexInlineMathChar(x, xs) -> loop (x :: '\\' :: acc) xs
        | input when List.startsWith endl input -> Some(List.rev acc, input)
        | x :: xs -> loop (x :: acc) xs
        | [] -> None
    // If it starts with 'startl', let's search for 'endl'
    if List.startsWith bracket input then
        match loop [] (List.skip bracket.Length input) with
        | Some(pre, post) -> Some(pre, List.skip bracket.Length post)
        | None -> None
    else
        None

/// This is similar to `List.Delimited`, but it skips over Latex inline math characters.
let (|DelimitedLatexInlineMath|_|) bracket input =
    let _startl, endl = bracket, bracket
    // Like List.partitionUntilEquals, but skip over escaped characters
    let rec loop acc =
        function
        | EscapedLatexInlineMathChar(x, xs) -> loop (x :: '\\' :: acc) xs
        | input when List.startsWith endl input -> Some(List.rev acc, input)
        | x :: xs -> loop (x :: acc) xs
        | [] -> None
    // If it starts with 'startl', let's search for 'endl'
    if List.startsWith bracket input then
        match loop [] (List.skip bracket.Length input) with
        | Some(pre, post) -> Some(pre, List.skip bracket.Length post)
        | None -> None
    else
        None

/// Recognizes an indirect link written using `[body][key]` or just `[key]`
/// The key can be preceeded by a space or other single whitespace thing.
let (|IndirectLink|_|) =
    function
    | List.BracketDelimited '[' ']' (body, '\r' :: '\n' :: (List.BracketDelimited '[' ']' (List.AsString link, rest))) ->
        Some(body, link, "\r\n[" + link + "]", rest)
    | List.BracketDelimited '[' ']' (body,
                                     ((' ' | '\n') as c) :: (List.BracketDelimited '[' ']' (List.AsString link, rest))) ->
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

    [ "http://"; "https://" ] |> List.tryPick linkFor

/// Recognizes some form of emphasis using `**bold**` or `*italic*`
/// (both can be also marked using underscore).
/// TODO: This does not handle nested emphasis well.
let (|Emphasised|_|) =
    function
    | LeftDelimiterRun(pre, run, post) ->
        match run @ post with
        | DelimitedMarkdown [ '_'; '_'; '_' ] (body, rest)
        | DelimitedMarkdown [ '*'; '*'; '*' ] (body, rest) ->
            Some(pre, body, Emphasis >> List.singleton >> (fun s -> Strong(s, MarkdownRange.zero)), rest)
        | DelimitedMarkdown [ '_'; '_' ] (body, rest)
        | DelimitedMarkdown [ '*'; '*' ] (body, rest) -> Some(pre, body, Strong, rest)
        | DelimitedMarkdown [ '_' ] (body, rest)
        | DelimitedMarkdown [ '*' ] (body, rest) -> Some(pre, body, Emphasis, rest)
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
    { Links: Dictionary<string, string * string option>
      Newline: string
      IsFirst: bool
      CurrentRange: MarkdownRange
      ParseOptions: MarkdownParseOptions }

    member x.ParseCodeAsOther = (x.ParseOptions &&& MarkdownParseOptions.ParseCodeAsOther) <> enum 0

    member x.ParseNonCodeAsOther = (x.ParseOptions &&& MarkdownParseOptions.ParseNonCodeAsOther) <> enum 0

    member x.AllowYamlFrontMatter = (x.ParseOptions &&& MarkdownParseOptions.AllowYamlFrontMatter) <> enum 0

/// Advances the StartColumn of the current range in ctx by n characters.
let private advanceCtxBy n ctx =
    { ctx with
        CurrentRange =
            { ctx.CurrentRange with
                StartColumn = ctx.CurrentRange.StartColumn + n } }

/// Computes a span range starting at ctx.StartColumn and spanning n characters.
let private spanRange n ctx =
    { ctx.CurrentRange with
        EndColumn = ctx.CurrentRange.StartColumn + n }

/// Parses a body of a paragraph and recognizes all inline tags.
let rec parseChars acc input (ctx: ParsingContext) =
    seq {

        // Zero or one literals, depending whether there is some accumulated input and update the ctx
        let accLiterals =
            Lazy<_>.Create(fun () ->
                if List.isEmpty acc then
                    ([], ctx)
                else
                    let range =
                        { ctx.CurrentRange with
                            EndColumn = ctx.CurrentRange.StartColumn + acc.Length }

                    let ctx =
                        { ctx with
                            CurrentRange =
                                { ctx.CurrentRange with
                                    StartColumn = ctx.CurrentRange.StartColumn + acc.Length } }

                    let text = String(List.rev acc |> Array.ofList)
                    ([ Literal(text, range) ], ctx))

        match input with
        // Recognizes explicit line-break at the end of line
        | ' ' :: ' ' :: '\r' :: '\n' :: rest
        | ' ' :: ' ' :: ('\n' | '\r') :: rest ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield HardLineBreak(ctx.CurrentRange)
            yield! parseChars [] rest ctx

        | HtmlEntity(entity, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield Literal(entity, ctx.CurrentRange)
            yield! parseChars [] rest ctx

        | '&' :: rest -> yield! parseChars (';' :: 'p' :: 'm' :: 'a' :: '&' :: acc) rest ctx

        // Ignore escaped characters that might mean something else
        | EscapedChar(c, rest) -> yield! parseChars (c :: acc) rest ctx

        // Inline code delimited either using double `` or single `
        // (if there are spaces around, then body can contain more backticks)
        | List.DelimitedWith [ '`'; ' ' ] [ ' '; '`' ] (body, rest, s, e)
        | List.DelimitedNTimes '`' (body, rest, s, e) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let rng =
                { ctx.CurrentRange with
                    StartColumn = ctx.CurrentRange.StartColumn + s
                    EndColumn = ctx.CurrentRange.StartColumn + s + body.Length }

            yield InlineCode(String(Array.ofList body).Trim(), rng)
            yield! parseChars [] rest (advanceCtxBy (s + body.Length + e) ctx)

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
                        { ctx.CurrentRange with
                            StartColumn = ctx.CurrentRange.StartColumn + 1 } }

            yield! value
            let code = String(Array.ofList body).Trim()

            yield
                LatexInlineMath(
                    code,
                    { ctx.CurrentRange with
                        EndColumn = ctx.CurrentRange.StartColumn + code.Length }
                )

            yield! parseChars [] rest ctx

        // Inline link wrapped as <http://foo.bar>
        | List.DelimitedWith [ '<' ] [ '>' ] (List.AsString link, rest, _s, _e) when
            Seq.forall (Char.IsWhiteSpace >> not) link
            && (link.Contains("@") || link.Contains("://"))
            ->
            let (value, ctx) = accLiterals.Value
            yield! value
            let consumed = 1 + link.Length + 1
            yield DirectLink([ Literal(link, spanRange consumed ctx) ], link, None, spanRange consumed ctx)
            yield! parseChars [] rest (advanceCtxBy consumed ctx)
        // Not an inline link - leave as an inline HTML tag
        | List.DelimitedWith [ '<' ] [ '>' ] (tag, rest, _s, _e) ->
            yield! parseChars ('>' :: (List.rev tag) @ '<' :: acc) rest ctx

        // Recognize direct link [foo](http://bar) or indirect link [foo][bar] or auto link http://bar
        | DirectLink(body, linkChars, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let consumed = 2 + body.Length + 2 + linkChars.Length
            let link, title = getLinkAndTitle (String(Array.ofList linkChars), MarkdownRange.zero)
            let bodyCtx = advanceCtxBy 1 ctx // advance past opening '['

            yield DirectLink(parseChars [] body bodyCtx |> List.ofSeq, link, title, spanRange consumed ctx)
            yield! parseChars [] rest (advanceCtxBy consumed ctx)
        | IndirectLink(body, link, original, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let consumed = 2 + body.Length + original.Length
            let bodyCtx = advanceCtxBy 1 ctx // advance past opening '['

            let key =
                if String.IsNullOrEmpty(link) then
                    String(body |> Array.ofSeq)
                else
                    link

            yield IndirectLink(parseChars [] body bodyCtx |> List.ofSeq, original, key, spanRange consumed ctx)
            yield! parseChars [] rest (advanceCtxBy consumed ctx)
        | AutoLink(link, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            let consumed = link.Length
            yield DirectLink([ Literal(link, spanRange consumed ctx) ], link, None, spanRange consumed ctx)
            yield! parseChars [] rest (advanceCtxBy consumed ctx)

        // Recognize image - this is a link prefixed with the '!' symbol
        | '!' :: DirectLink(body, linkChars, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let consumed = 1 + 2 + body.Length + 2 + linkChars.Length
            let link, title = getLinkAndTitle (String(Array.ofList linkChars), MarkdownRange.zero)

            yield DirectImage(String(Array.ofList body), link, title, spanRange consumed ctx)
            yield! parseChars [] rest (advanceCtxBy consumed ctx)
        | '!' :: IndirectLink(body, link, original, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value

            let consumed = 1 + 2 + body.Length + original.Length

            let key =
                if String.IsNullOrEmpty(link) then
                    String(body |> Array.ofSeq)
                else
                    link

            yield IndirectImage(String(Array.ofList body), original, key, spanRange consumed ctx)
            yield! parseChars [] rest (advanceCtxBy consumed ctx)

        // Handle Emphasis
        | CannotOpenEmphasis(revPre, post) -> yield! parseChars (revPre @ acc) post ctx
        | Emphasised(pre, body, f, rest) ->
            let (value, ctx) = accLiterals.Value
            yield! value
            yield! parseChars [] pre ctx
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
let parseSpans (StringPosition.TrimBoth(s, n)) ctx =
    let ctx = { ctx with CurrentRange = n }

    parseChars [] (s.ToCharArray() |> List.ofArray) ctx |> List.ofSeq

let rec trimSpaces numSpaces (s: string) =
    if numSpaces <= 0 then
        s
    elif s.StartsWith ' ' then
        trimSpaces (numSpaces - 1) (s.Substring(1))
    elif s.StartsWith '\t' then
        trimSpaces (numSpaces - 4) (s.Substring(1))
    else
        s
