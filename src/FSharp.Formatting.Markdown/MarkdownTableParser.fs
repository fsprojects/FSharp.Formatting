// --------------------------------------------------------------------------------------
// F# Markdown (MarkdownTableParser.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

/// Active patterns and helpers for parsing Markdown pipe tables and Emacs org-mode
/// tables.  These are used by the block parser (<c>MarkdownBlockParser.fs</c>) but form
/// a self-contained unit and are kept separate to keep file sizes manageable.
module internal FSharp.Formatting.Markdown.TableParser

open System
open FSharp.Patterns
open FSharp.Collections
open FSharp.Formatting.Markdown.InlineParser

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
