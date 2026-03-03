// --------------------------------------------------------------------------------------
// F# Markdown (MarkdownBlockParser.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.Markdown.BlockParser

open System
open System.Collections.Generic
open System.Text.RegularExpressions

open FSharp.Patterns
open FSharp.Collections
open FSharp.Formatting.Common
open FSharp.Formatting.Markdown.InlineParser

// --------------------------------------------------------------------------------------
// Parsing of Markdown - second part handles paragraph-level formatting (headings, etc.)
// --------------------------------------------------------------------------------------

/// Checks if a string is a valid CommonMark setext heading underline for the given character.
/// Allows 0–3 leading spaces, then one or more repeated identical characters, then optional
/// trailing whitespace only (4+ leading spaces would be an indented code block, not a heading).
let isSetextUnderline (ch: char) (line: string) =
    let trimmedEnd = line.TrimEnd()
    let leadingSpaces = trimmedEnd.Length - trimmedEnd.TrimStart(' ').Length

    leadingSpaces <= 3
    && (let inner = trimmedEnd.TrimStart(' ')
        inner.Length >= 1 && inner |> Seq.forall ((=) ch))

/// Recognizes heading, either prefixed with #s or followed by === or --- line
let (|Heading|_|) lines =
    match lines with
    | ((StringPosition.TrimBoth header) as line1) :: ((s, _) as line2) :: rest when
        fst header <> "" && isSetextUnderline '=' s
        ->
        Some(1, header, [ line1; line2 ], rest)
    | ((StringPosition.TrimBoth header) as line1) :: ((s, _) as line2) :: rest when
        fst header <> "" && isSetextUnderline '-' s
        ->
        Some(2, header, [ line1; line2 ], rest)
    | ((line1text, ln1) as line1) :: rest ->
        // ATX heading (CommonMark): optional 0–3 leading spaces, then 1–6 '#' characters,
        // then a space or end of line (a tab or other char after '#' is not valid).
        let mutable i = 0

        while i < 3 && i < line1text.Length && line1text.[i] = ' ' do
            i <- i + 1

        let hstart = i

        while i < line1text.Length && line1text.[i] = '#' do
            i <- i + 1

        let n = i - hstart

        if n < 1 || n > 6 || (i < line1text.Length && line1text.[i] <> ' ') then
            None
        else
            let contentStart = if i < line1text.Length then i + 1 else i
            let content = (line1text.Substring(contentStart)).TrimEnd()
            // Remove optional closing sequence of '#' preceded by space (or empty '#'-only content).
            // For example "## Hello F#" keeps the '#' because it is not preceded by a space.
            let header =
                if content.EndsWith('#') then
                    let noHash = content.TrimEnd([| '#' |])

                    if noHash = "" || (noHash.Length > 0 && noHash.[noHash.Length - 1] = ' ') then
                        noHash.Trim()
                    else
                        content.Trim()
                else
                    content.Trim()

            let rawContent = line1text.Substring(contentStart)
            let leadingContentSpaces = rawContent.Length - rawContent.TrimStart(' ').Length
            let headerStart = ln1.StartColumn + contentStart + leadingContentSpaces

            let headerLn =
                { ln1 with
                    StartColumn = headerStart
                    EndColumn = headerStart + header.Length }

            Some(n, (header, headerLn), [ line1 ], rest)
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

/// Recognizes a horizontal rule written using *, _ or -.
/// Per CommonMark: at most 3 leading spaces are allowed (4+ would be an indented code block).
let (|HorizontalRule|_|) (line: string, _n: MarkdownRange) =
    // Count leading spaces; reject if 4 or more (CommonMark spec § 4.1)
    let mutable leadingSpaces = 0

    while leadingSpaces < line.Length && line.[leadingSpaces] = ' ' do
        leadingSpaces <- leadingSpaces + 1

    if leadingSpaces > 3 then
        None
    else

        let rec loop ((h, a, u) as arg) i =
            if (h >= 3 || a >= 3 || u >= 3) && i = line.Length then
                Some(line.[leadingSpaces])
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

        loop (0, 0, 0) leadingSpaces

/// Recognizes a code block - lines starting with four spaces (including blank)
let (|NestedCodeBlock|_|) lines =
    match lines with
    | Lines.TakeCodeBlock(_numspaces, (Lines.TrimBlank lines as takenLines), rest) when lines <> [] ->
        let code = [ for (l, _n) in lines -> if String.IsNullOrEmpty l then "" else trimSpaces 4 l ]

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

                    match splits |> Array.tryFind (fun _ -> true) with
                    | None -> "", ""
                    | Some langString ->
                        let ignoredString =
                            header.Substring(header.IndexOf(langString, StringComparison.Ordinal) + langString.Length)

                        langString,
                        (if String.IsNullOrWhiteSpace ignoredString then
                             ""
                         else
                             ignoredString)

            // Handle the ending line
            let takenLines2, codeWithoutIndent, rest =
                match rest with
                | ((hd, n) as takenLine2) :: tl ->
                    let idx = hd.IndexOf(fenceString, StringComparison.Ordinal)

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
    | StringPosition.TrimStartAndCount(startIndent,
                                       _spaces,
                                       // NOTE: a tab character after +, * or - isn't supported by the reference implementation
                                       // (it will be parsed as paragraph for 0.22)
                                       (StringPosition.StartsWithAny [ "+ "; "* "; "- " (*; "+\t"; "*\t"; "-\t"*) ] as item)) ->
        let range = snd item

        let li =
            ((fst item).Substring(2),
             { range with
                 StartColumn = range.StartColumn + 2 })

        let (StringPosition.TrimStartAndCount(startIndent2, _spaces2, _)) = li

        let endIndent =
            startIndent
            + 2
            +
            // Handle case of code block
            if startIndent2 >= 5 then 1 else startIndent2

        Some(Unordered, startIndent, endIndent, li)
    | StringPosition.TrimStartAndCount(startIndent,
                                       _spaces,
                                       (SkipSomeNumbers(skipNumCount, '.' :: ' ' :: List.AsString item))) ->
        let (StringPosition.TrimStartAndCount(startIndent2, _spaces2, _)) = (item, MarkdownRange.zero)

        let endIndent =
            startIndent
            + 2
            + skipNumCount
            +
            // Handle case of code block
            if startIndent2 >= 5 then 1 else startIndent2

        Some(Ordered, startIndent, endIndent, (item, MarkdownRange.zero))
    | _ -> None

/// Splits input into lines until whitespace, starting of a list, or a thematic break and the rest.
/// A thematic break (e.g. ---) interrupts a list item in CommonMark.
let (|LinesUntilListOrWhite|) lines =
    lines
    |> List.partitionUntil (function
        | ListStart _
        | HorizontalRule _
        | StringPosition.WhiteSpace -> true
        | _ -> false)

/// Splits input into lines until not-indented line or starting of a list and the rest.
let (|LinesUntilListOrUnindented|) lines =
    lines
    |> List.partitionUntilLookahead (function
        | (ListStart _ | StringPosition.Unindented) :: _
        | StringPosition.WhiteSpace :: StringPosition.WhiteSpace :: _ -> true
        | _ -> false)

/// Returns the effective number of leading spaces in a string, treating each tab as 4 spaces.
let private tabAwareLeadingSpaces (s: string) =
    let mutable spaces = 0
    let mutable i = 0

    while i < s.Length && (s.[i] = ' ' || s.[i] = '\t') do
        spaces <- spaces + (if s.[i] = '\t' then 4 else 1)
        i <- i + 1

    spaces

/// Splits input into lines for a loose list item continuation (when a blank line follows
/// the first line of the item). Unlike LinesUntilListOrUnindented, this does not stop at
/// indented list starts — it captures all continuation content at or above the item's
/// content column (endIndent). It stops at truly unindented content (0 leading spaces),
/// double blank lines, or a blank line followed by content with fewer leading spaces than
/// endIndent (indicating the content belongs to an outer list item, not this one).
let (|LinesUntilListOrUnindentedLoose|) endIndent lines =
    lines
    |> List.partitionUntilLookahead (function
        | StringPosition.Unindented :: _
        | StringPosition.WhiteSpace :: StringPosition.WhiteSpace :: _ -> true
        | StringPosition.WhiteSpace :: (s, _) :: _ ->
            let leading = tabAwareLeadingSpaces s
            leading > 0 && leading < endIndent
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
    | (ListStart(kind, startIndent, endIndent, item) as takenLine) :: LinesUntilListOrWhite(continued, next) ->
        // For loose items (blank line follows the first content line), use loose indentation
        // rules: capture all continuation content at this item's indent level, including any
        // nested list starts. For tight items, stop at list starts (original behaviour).
        let more, rest =
            match next with
            | StringPosition.WhiteSpace :: _ ->
                match next with
                | LinesUntilListOrUnindentedLoose endIndent (m, r) -> m, r
            | _ ->
                match next with
                | LinesUntilListOrUnindented(m, r) -> m, r

        let simple =
            match item with
            | StringPosition.TrimStartAndCount(_, spaces, _) when spaces >= 4 ->
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

                  yield
                      (trimmed,
                       { n with
                           StartColumn = n.StartColumn + line.Length - trimmed.Length }) ]
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
    | Some _x when List.isEmpty line -> [ "" ]
    | Some x ->
        let chunkSize = List.length line - List.length x - 1

        cLstToStr (Seq.take chunkSize line |> Seq.toList) :: pipeTableFindSplits delim x





/// Recognizes alignment specified in the passed separator line.
let (|TableCellSeparator|_|) =
    function
    | StringPosition.StartsAndEndsWith (":", ":") (StringPosition.EqualsRepeated("-", MarkdownRange.zero)) ->
        Some(AlignCenter)
    | StringPosition.StartsWith ":" (StringPosition.EqualsRepeated("-", MarkdownRange.zero)) -> Some(AlignLeft)
    | StringPosition.StartsAndEndsWith ("", ":") (StringPosition.EqualsRepeated("-", MarkdownRange.zero)) ->
        Some(AlignRight)
    | StringPosition.EqualsRepeated("-", MarkdownRange.zero) -> Some(AlignDefault)
    | _ -> None

/// Recognizes row of pipe table.
/// The function takes number of expected columns and array of delimiters.
/// Returns list of strings between delimiters.
let (|PipeTableRow|_|) (size: int option) delimiters (line: string, n: MarkdownRange) =
    let parts =
        pipeTableFindSplits delimiters (line.ToCharArray() |> Array.toList)
        |> List.toArray
        |> Array.map (fun s -> (s.Trim(), n))

    let n = parts.Length

    let m = size |> Option.defaultValue 1

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
    (grid: int array option)
    (c: char)
    (check: string * MarkdownRange -> bool)
    (line: string, _n: MarkdownRange)
    =
    let p =
        grid
        |> Option.defaultValue (Array.FindAll([| 0 .. line.Length - 1 |], (fun i -> line.[i] = c)))

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

        if List.forall check parts then Some(p, parts) else None

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
/// a white line or start of other paragraph-item is found.
/// A thematic break (HorizontalRule) also interrupts a paragraph in CommonMark.
let (|TakeParagraphLines|_|) input =
    match
        List.partitionWhileLookahead
            (function
            | Heading _ -> false
            | FencedCodeBlock _ -> false
            | BlockquoteStart _ :: _ -> false
            | StringPosition.WhiteSpace :: _ -> false
            | (HorizontalRule _) :: _ -> false
            | _ -> true)
            input
    with
    | matching, rest when matching <> [] -> Some(matching, rest)
    | _ -> None

/// Recognize nested HTML block
/// TODO: This is too simple - takes paragraph that starts with <
let (|HtmlBlock|_|) (lines: (string * MarkdownRange) list) =
    match lines with
    | (first, _n) :: _ when first.StartsWith('<') ->
        match lines with
        | TakeParagraphLines(html, rest) -> Some(html, html, rest)
        | _ -> None
    | _ -> None

/// "Markdown allows you to be lazy and only put the > before the first line of a hard-wrapped paragraph"
// Continues taking lines until a whitespace line, start of a blockquote, or a thematic break.
// A thematic break (HorizontalRule) ends the lazy continuation in CommonMark.
let (|LinesUntilBlockquoteEnds|) input =
    input
    |> List.partitionUntilLookahead (fun next ->
        match next with
        | BlockquoteStart _ :: _ -> true
        | Heading _ -> true
        | StringPosition.WhiteSpace :: _ -> true
        | (HorizontalRule _) :: _ -> true
        | _ -> false)

/// Recognizes blockquote - continues taking paragraphs
/// starting with '>' until there is something else
let rec (|Blockquote|_|) lines =
    match lines with
    | EmptyBlockquote(takenLines, Lines.TrimBlankStart(takenLines2, rest)) ->
        Some([ ("", MarkdownRange.zero) ], takenLines @ takenLines2, rest)
    | (BlockquoteStart(quoteLine) as takenLine) :: LinesUntilBlockquoteEnds(continued,
                                                                            Lines.TrimBlankStart(takenLines2, rest)) ->
        let moreQuoteLines, moreTakenLines, rest =
            match rest with
            | Blockquote(lines, takenLines, rest) -> lines, takenLines, rest
            | _ -> [], [], rest

        Some(quoteLine :: continued @ moreQuoteLines, takenLine :: continued @ takenLines2 @ moreTakenLines, rest)
    | _ -> None

/// Recognizes a special case: an empty blockquote line should terminate
/// the blockquote if the next line is not a blockquote
and (|EmptyBlockquote|_|) lines =
    match lines with
    | BlockquoteStart(StringPosition.WhiteSpace) :: Blockquote(_) -> None
    | (BlockquoteStart(StringPosition.WhiteSpace) as takenLine) :: rest -> Some([ takenLine ], rest)
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
        | TakeParagraphLines(body, rest) ->
            Some("equation", body, ((@"\begin{equation}", n) :: body @ [ (@"\end{equation}", n) ]), rest)
        | _ -> None
    | ((first, n) as takenLine) :: rest when
        first.TrimEnd().StartsWith("$$", StringComparison.Ordinal)
        && first.TrimEnd().EndsWith("$$", StringComparison.Ordinal)
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
    | ((StringPosition.StartsWithWrapped ("[", "]:") (wrapped, StringPosition.TrimBoth link) | StringPosition.StartsWithWrapped (" [",
                                                                                                                                 "]:") (wrapped,
                                                                                                                                        StringPosition.TrimBoth link) | StringPosition.StartsWithWrapped ("  [",
                                                                                                                                                                                                          "]:") (wrapped,
                                                                                                                                                                                                                 StringPosition.TrimBoth link) | StringPosition.StartsWithWrapped ("   [",
                                                                                                                                                                                                                                                                                   "]:") (wrapped,
                                                                                                                                                                                                                                                                                          StringPosition.TrimBoth link)) as line) :: rest ->
        Some((wrapped, link), [ line ], rest)
    | _ -> None

let updateCurrentRange lines =
    match lines with
    | [] -> MarkdownRange.zero
    | (_, l) :: _ -> l

/// Parse a list of lines into a sequence of markdown paragraphs
let rec parseParagraphs (ctx: ParsingContext) (lines: (string * MarkdownRange) list) =
    seq {
        let ctx =
            { ctx with
                CurrentRange = updateCurrentRange lines }

        let frontMatter, (Lines.TrimBlankStart(_, moreLines)) =
            if ctx.IsFirst && ctx.AllowYamlFrontMatter then
                match lines with
                | YamlFrontmatter(yaml, loc, rest) -> Some(YamlFrontmatter(yaml, loc)), rest
                | _ -> None, lines
            else
                None, lines

        match frontMatter with
        | None -> ()
        | Some p -> yield p

        let ctx =
            { ctx with
                CurrentRange = updateCurrentRange moreLines }

        let ctx = { ctx with IsFirst = false }

        match moreLines with
        // Recognize various kinds of standard paragraphs
        | LinkDefinition((key, link), takenLines, Lines.TrimBlankStart(takenLines2, lines)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                if ctx.Links.ContainsKey(key) then
                    failwithf "Cannot define a link to the reference %s twice." key

                ctx.Links.Add(key, getLinkAndTitle link)

            yield! parseParagraphs ctx lines

        | NestedCodeBlock(code,
                          takenLines,
                          Lines.TrimBlankStart(takenLines2, lines),
                          fenceString,
                          langString,
                          ignoredLine)
        | FencedCodeBlock(code,
                          takenLines,
                          Lines.TrimBlankStart(takenLines2, lines),
                          fenceString,
                          langString,
                          ignoredLine) ->
            if ctx.ParseCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let code = code |> String.concat ctx.Newline
                yield CodeBlock(code, None, fenceString, langString, ignoredLine, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | Blockquote(body, takenLines, Lines.TrimBlankStart(takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield
                    QuotedBlock(
                        parseParagraphs ctx (body @ [ ("", MarkdownRange.zero) ]) |> List.ofSeq,
                        ctx.CurrentRange
                    )

            yield! parseParagraphs ctx rest

        | EmacsTableBlock((headers, alignments, rows), takenLines, Lines.TrimBlankStart(takenLines2, rest))
        | PipeTableBlock((headers, alignments, rows), takenLines, Lines.TrimBlankStart(takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let headParagraphs =
                    headers
                    |> Option.map (fun headers -> headers |> List.map (fun i -> parseParagraphs ctx i |> List.ofSeq))

                let rows = rows |> List.map (List.map (fun i -> parseParagraphs ctx i |> List.ofSeq))

                yield TableBlock(headParagraphs, alignments, rows, ctx.CurrentRange)

            yield! parseParagraphs ctx rest

        | HorizontalRule(c) as takenLine :: (Lines.TrimBlankStart(takenLines2, lines)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLine :: takenLines2, ctx.CurrentRange)
            else
                yield HorizontalRule(c, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | LatexBlock(env, body, takenLines, Lines.TrimBlankStart(takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield LatexBlock(env, body |> List.map fst, ctx.CurrentRange)

            yield! parseParagraphs ctx rest


        // Recognize list of list items and turn it into nested lists
        | ListItems true (items, takenLines, Lines.TrimBlankStart(takenLines2, rest)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let tree = Tree.ofIndentedList items

                // Nest all items that have another kind (i.e. UL vs. OL)
                let rec nestUnmatchingItems items =
                    match items with
                    | Node((_, baseKind, _), _) :: _ ->
                        items
                        |> List.nestUnderLastMatching (fun (Node((_, kind, _), _)) -> kind = baseKind)
                        |> List.map (fun (Node(info, children), nested) ->
                            let children = nestUnmatchingItems children
                            Node(info, children @ nested))
                    | [] -> []

                // Turn tree into nested list definitions
                let rec formatTree (nodes: Tree<bool * MarkdownListKind * (string * MarkdownRange) list> list) =
                    let kind =
                        match nodes with
                        | Node((_, kind, _), _) :: _ -> kind
                        | _ -> Unordered

                    let items =
                        [ for (Node((simple, _, body), nested)) in nodes ->
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
                        |> List.exists (function
                            | Node((false, _, _), _) -> true
                            | _ -> false)

                    if containsNonSimple then
                        nodes
                        |> List.map (function
                            | Node((_, kind, content), nested) ->
                                Node((false, kind, content), unifySimpleProperty nested))
                    else
                        nodes

                yield tree |> unifySimpleProperty |> formatTree

            yield! parseParagraphs ctx rest

        // Recognize remaining types of paragraphs
        | Heading(n, body, takenLines, Lines.TrimBlankStart(takenLines2, lines)) ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                yield Heading(n, parseSpans body ctx, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | HtmlBlock(code, takenLines, Lines.TrimBlankStart(takenLines2, lines)) when
            (let all = String.concat ctx.Newline (code |> List.map fst)

             not (all.StartsWith("<http://", StringComparison.Ordinal))
             && not (all.StartsWith("<ftp://", StringComparison.Ordinal))
             && not (all.Contains '@'))
            ->
            if ctx.ParseNonCodeAsOther then
                yield OtherBlock(takenLines @ takenLines2, ctx.CurrentRange)
            else
                let all = String.concat ctx.Newline (code |> List.map fst)

                yield InlineHtmlBlock(all, None, ctx.CurrentRange)

            yield! parseParagraphs ctx lines

        | TakeParagraphLines((Lines.TrimParagraphLines lines as takenLines), Lines.TrimBlankStart(takenLines2, rest)) ->
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

        | Lines.TrimBlankStart(_takenLines2, []) -> ()

        | _ -> failwithf "Unexpectedly stopped!\n%A" moreLines
    }
