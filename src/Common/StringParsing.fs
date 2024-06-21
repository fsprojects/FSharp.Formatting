// --------------------------------------------------------------------------------------
// F# Markdown (StringParsing.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module internal FSharp.Patterns

open System
open FSharp.Collections
open FSharp.Formatting.Markdown

// --------------------------------------------------------------------------------------
// Active patterns that simplify parsing of strings and lists of strings (lines)
// --------------------------------------------------------------------------------------

module String =
    /// Matches when a string is a whitespace or null
    [<return: Struct>]
    let (|WhiteSpace|_|) (s) =
        if String.IsNullOrWhiteSpace(s) then
            ValueSome()
        else
            ValueNone

    /// Returns a string trimmed from both start and end
    let (|TrimBoth|) (text: string) = text.Trim()

    /// Matches when a string starts with the specified sub-string
    let (|StartsWith|_|) (start: string) (text: string) =
        if text.StartsWith(start, StringComparison.Ordinal) then
            Some(text.Substring(start.Length))
        else
            None

    /// Matches when a string starts with the specified sub-string
    /// The matched string is trimmed from all whitespace.
    let (|StartsWithTrim|_|) (start: string) (text: string) =
        if text.StartsWith(start, StringComparison.Ordinal) then
            Some(text.Substring(start.Length).Trim())
        else
            None

    /// Matches when a string starts with the given value and ends
    /// with a given value (and returns the rest of it)
    let (|StartsAndEndsWith|_|) (starts: string, ends: string) (s: string) =
        if
            s.StartsWith(starts, StringComparison.Ordinal)
            && s.EndsWith(ends, StringComparison.Ordinal)
            && s.Length >= starts.Length + ends.Length
        then
            Some(s.Substring(starts.Length, s.Length - starts.Length - ends.Length))
        else
            None

    /// Matches when a string starts with the given value and ends
    /// with a given value (and returns trimmed body)
    let (|StartsAndEndsWithTrim|_|) args =
        function
        | StartsAndEndsWith args (TrimBoth res) -> Some res
        | _ -> None

    /// Matches when a string starts with a sub-string wrapped using the
    /// opening and closing sub-string specified in the parameter.
    /// For example "[aa]bc" is wrapped in [ and ] pair. Returns the wrapped
    /// text together with the rest.
    let (|StartsWithWrapped|_|) (starts: string, ends: string) (text: string) =
        if text.StartsWith(starts, StringComparison.Ordinal) then
            let id = text.IndexOf(ends, starts.Length, StringComparison.Ordinal)

            if id >= 0 then
                let wrapped = text.Substring(starts.Length, id - starts.Length)

                let rest = text.Substring(id + ends.Length, text.Length - id - ends.Length)

                Some(wrapped, rest)
            else
                None
        else
            None

    /// Ignores everything until a end-line character is detected, returns the remaining string.
    let (|SkipSingleLine|) (text: string) =
        let rec tryEol eolList =
            match eolList with
            | h: string :: t ->
                match text.IndexOf(h, StringComparison.Ordinal) with
                | i when i < 0 -> tryEol t
                | i -> text.Substring(i + h.Length)
            | _ -> text

        let result = tryEol [ "\r\n"; "\n" ]

        let skipped = text.Substring(0, text.Length - result.Length)

        if not <| String.IsNullOrWhiteSpace(skipped) then
            FSharp.Formatting.Common.Log.warnf "skipped '%s' which contains non-whitespace character!" skipped

        if result = text then
            FSharp.Formatting.Common.Log.warnf
                "could not skip a line of %s, because no line-ending character was found!"
                text

        result

    /// Given a list of lines indented with certan number of whitespace
    /// characters (spaces), remove the spaces from the beginning of each line
    /// and return the string as a list of lines
    let removeSpaces (lines: string list) =
        let spaces =
            lines
            |> Seq.choose (fun line ->
                if String.IsNullOrWhiteSpace line |> not then
                    line |> Seq.takeWhile Char.IsWhiteSpace |> Seq.length |> Some
                else
                    None)
            |> fun xs -> if Seq.isEmpty xs then 0 else Seq.min xs

        lines
        |> List.map (fun line ->
            if String.IsNullOrWhiteSpace(line) then
                ""
            else
                line.Substring(spaces))

module StringPosition =
    /// Matches when a string is a whitespace or null
    [<return: Struct>]
    let (|WhiteSpace|_|) (s, _n: MarkdownRange) =
        if String.IsNullOrWhiteSpace(s) then
            ValueSome()
        else
            ValueNone

    /// Matches when a string does starts with non-whitespace
    [<return: Struct>]
    let (|Unindented|_|) (s: string, _n: MarkdownRange) =
        if not (String.IsNullOrWhiteSpace(s)) && s.TrimStart() = s then
            ValueSome()
        else
            ValueNone

    /// Returns a string trimmed from both start and end
    let (|TrimBoth|) (text: string, n: MarkdownRange) =
        let trimmedStart = text.TrimStart()
        let trimmed = trimmedStart.TrimEnd()

        (trimmed,
         { n with
             StartColumn = n.StartColumn + text.Length - trimmedStart.Length
             EndColumn = n.EndColumn - trimmedStart.Length + trimmed.Length })

    /// Returns a string trimmed from the end
    let (|TrimEnd|) (text: string, n: MarkdownRange) =
        let trimmed = text.TrimEnd()

        (trimmed,
         { n with
             EndColumn = n.EndColumn - text.Length + trimmed.Length })

    /// Returns a string trimmed from the start
    let (|TrimStart|) (text: string, n: MarkdownRange) =
        let trimmed = text.TrimStart()

        (trimmed,
         { n with
             StartColumn = n.StartColumn + text.Length - trimmed.Length })

    /// Returns a string trimmed from the end using characters given as a parameter
    let (|TrimEndUsing|) chars (text: string, n: MarkdownRange) =
        let trimmed = text.TrimEnd(Array.ofSeq chars)

        (trimmed,
         { n with
             EndColumn = n.EndColumn - text.Length + trimmed.Length })

    /// Returns a string trimmed from the start together with
    /// the number of skipped whitespace characters
    let (|TrimStartAndCount|) (text: string, n: MarkdownRange) =
        let trimmed = text.TrimStart([| ' '; '\t' |])
        let len = text.Length - trimmed.Length

        len,
        text.Substring(0, len).Replace("\t", "    ").Length,
        (trimmed,
         { n with
             StartColumn = n.StartColumn + text.Length - trimmed.Length })

    /// Matches when a string starts with any of the specified sub-strings
    [<return: Struct>]
    let (|StartsWithAny|_|) (starts: string seq) (text: string, _n: MarkdownRange) =
        if starts |> Seq.exists (fun s -> text.StartsWith(s, StringComparison.Ordinal)) then
            ValueSome()
        else
            ValueNone

    /// Matches when a string starts with the specified sub-string
    let (|StartsWith|_|) (start: string) (text: string, n: MarkdownRange) =
        if text.StartsWith(start, StringComparison.Ordinal) then
            Some(
                text.Substring(start.Length),
                { n with
                    StartColumn = n.StartColumn + text.Length - start.Length }
            )
        else
            None

    /// Matches when a string starts with the specified sub-string
    /// The matched string is trimmed from all whitespace.
    let (|StartsWithTrim|_|) (start: string) (text: string, n: MarkdownRange) =
        if text.StartsWith(start, StringComparison.Ordinal) then
            Some(
                text.Substring(start.Length).Trim(),
                { n with
                    StartColumn = n.StartColumn + text.Length - start.Length }
            )
        else
            None

    /// Matches when a string starts with the specified sub-string (ignoring whitespace at the start)
    /// The matched string is trimmed from all whitespace.
    let (|StartsWithNTimesTrimIgnoreStartWhitespace|_|) (start: string) (text: string, _n: MarkdownRange) =
        if text.Contains(start) then
            let beforeStart = text.Substring(0, text.IndexOf(start, StringComparison.Ordinal))

            if String.IsNullOrWhiteSpace(beforeStart) then
                let startAndRest = text.Substring(beforeStart.Length)

                let startNum =
                    Seq.windowed start.Length startAndRest
                    |> Seq.map (fun chars -> System.String(chars))
                    |> Seq.takeWhile ((=) start)
                    |> Seq.length

                Some(
                    startNum,
                    beforeStart.Length,
                    text.Substring(beforeStart.Length + (start.Length * startNum)).Trim()
                )
            else
                None
        else
            None

    /// Matches when a string starts with the given value and ends
    /// with a given value (and returns the rest of it)
    let (|StartsAndEndsWith|_|) (starts: string, ends: string) (s: string, n: MarkdownRange) =
        if
            s.StartsWith(starts, StringComparison.Ordinal)
            && s.EndsWith(ends, StringComparison.Ordinal)
            && s.Length >= starts.Length + ends.Length
        then
            Some(
                s.Substring(starts.Length, s.Length - starts.Length - ends.Length),
                { n with
                    StartColumn = n.StartColumn + s.Length - starts.Length
                    EndColumn = n.EndColumn - s.Length + ends.Length }
            )
        else
            None

    /// Matches when a string starts with the given value and ends
    /// with a given value (and returns trimmed body)
    let (|StartsAndEndsWithTrim|_|) args =
        function
        | StartsAndEndsWith args (TrimBoth res) -> Some res
        | _ -> None

    /// Matches when a string starts with a non-zero number of complete
    /// repetitions of the specified parameter (and returns the number
    /// of repetitions, together with the rest of the string)
    ///
    ///    let (StartsWithRepeated "/\" (2, " abc")) = "/\/\ abc"
    ///
    let (|StartsWithRepeated|_|) (repeated: string) (text: string, ln: MarkdownRange) =
        let rec loop i =
            if i = text.Length then i
            elif text.[i] <> repeated.[i % repeated.Length] then i
            else loop (i + 1)

        let n = loop 0

        if n = 0 || n % repeated.Length <> 0 then
            None
        else
            Some(n / repeated.Length, (text.Substring(n, text.Length - n), { ln with StartColumn = n }))

    /// Matches when a string starts with a sub-string wrapped using the
    /// opening and closing sub-string specified in the parameter.
    /// For example "[aa]bc" is wrapped in [ and ] pair. Returns the wrapped
    /// text together with the rest.
    let (|StartsWithWrapped|_|) (starts: string, ends: string) (text: string, n: MarkdownRange) =
        if text.StartsWith(starts, StringComparison.Ordinal) then
            let id = text.IndexOf(ends, starts.Length, StringComparison.Ordinal)

            if id >= 0 then
                let wrapped = text.Substring(starts.Length, id - starts.Length)

                let rest = text.Substring(id + ends.Length, text.Length - id - ends.Length)

                Some(
                    wrapped,
                    (rest,
                     { n with
                         StartColumn = id + ends.Length })
                )
            else
                None
        else
            None

    /// Matches when a string consists of some number of
    /// complete repetitions of a specified sub-string.
    [<return: Struct>]
    let (|EqualsRepeated|_|) (repeated, _n: MarkdownRange) =
        function
        | StartsWithRepeated repeated (_n, (v, _)) when (String.IsNullOrWhiteSpace v) -> ValueSome()
        | _ -> ValueNone

module List =
    /// Matches a list if it starts with a sub-list that is delimited
    /// using the specified delimiters. Returns a wrapped list and the rest.
    let inline internal (|DelimitedWith|_|) startl endl input =
        if List.startsWith startl input then
            match List.partitionUntilEquals endl (List.skip startl.Length input) with
            | Some(pre, post) -> Some(pre, List.skip endl.Length post, startl.Length, endl.Length)
            | None -> None
        else
            None

    /// Matches a list if it starts with a sub-list. Returns the list.
    let inline internal (|StartsWith|_|) startl input =
        if List.startsWith startl input then Some input else None

    /// Matches a list if it starts with a sub-list that is delimited
    /// using the specified delimiter. Returns a wrapped list and the rest.
    let inline internal (|Delimited|_|) str = (|DelimitedWith|_|) str str

    let inline internal (|DelimitedNTimes|_|) str input =
        let strs, _items = List.partitionWhile (fun i -> i = str) input

        match strs with
        | _h :: _ -> (|Delimited|_|) (List.init strs.Length (fun _ -> str)) input
        | _ -> None

    /// Matches a list if it starts with a bracketed list. Nested brackets
    /// are skipped (by counting opening and closing brackets) and can be
    /// escaped using the '\' symbol.
    let (|BracketDelimited|_|) startc endc input =
        let rec loop acc count =
            function
            | '\\' :: x :: xs when x = endc -> loop (x :: acc) count xs
            | x :: xs when x = endc && count = 0 -> Some(List.rev acc, xs)
            | x :: xs when x = endc -> loop (x :: acc) (count - 1) xs
            | x :: xs when x = startc -> loop (x :: acc) (count + 1) xs
            | x :: xs -> loop (x :: acc) count xs
            | [] -> None

        match input with
        | x :: xs when x = startc -> loop [] 0 xs
        | _ -> None

    /// Returns a list of characters as a string.
    let (|AsString|) chars = String(Array.ofList chars)

module Lines =
    /// Removes blank lines from the start and the end of a list
    let (|TrimBlank|) lines =
        lines
        |> List.skipWhile (fun (s, _n) -> String.IsNullOrWhiteSpace s)
        |> List.rev
        |> List.skipWhile (fun (s, _n) -> String.IsNullOrWhiteSpace s)
        |> List.rev

    /// Matches when there are some lines at the beginning that are
    /// either empty (or whitespace) or start with the specified string.
    /// Returns all such lines from the beginning until a different line.
    let (|TakeStartingWithOrBlank|_|) (start: string) (input: string list) =
        match
            input
            |> List.partitionWhile (fun s ->
                String.IsNullOrWhiteSpace s || s.StartsWith(start, StringComparison.Ordinal))
        with
        | matching, rest when matching <> [] -> Some(matching, rest)
        | _ -> None

    /// Matches when there are some lines at the beginning that are
    /// either empty (or whitespace) or start with at least 4 spaces (a tab counts as 4 spaces here).
    /// Returns all such lines from the beginning until a different line and
    /// the number of spaces the first line started with.
    let (|TakeCodeBlock|_|) (input: (string * MarkdownRange) list) =
        let spaceNum = 4
        //match input with
        //| h :: _ ->
        //  let head = (input |> List.head).Replace("\t", "    ") |> Seq.toList
        //  let spaces, _ = List.partitionWhile (fun s -> s = ' ') head
        //  spaces.Length
        //| _ -> 0
        let startsWithSpaces (s: string) =
            let normalized = s.Replace("\t", "    ")

            normalized.Length >= spaceNum
            && normalized.Substring(0, spaceNum) = System.String(' ', spaceNum)

        match List.partitionWhile (fun (s, _n) -> String.IsNullOrWhiteSpace s || startsWithSpaces s) input with
        | matching, rest when matching <> [] && spaceNum >= 4 -> Some(spaceNum, matching, rest)
        | _ -> None

    /// Removes whitespace lines from the beginning of the list
    let (|TrimBlankStart|) (lines: (string * MarkdownRange) list) =
        let takenLines = lines |> List.takeWhile (fun (s, _n) -> String.IsNullOrWhiteSpace s)

        let rest = lines |> List.skipWhile (fun (s, _n) -> String.IsNullOrWhiteSpace s)

        takenLines, rest

    /// Trims all lines of the current paragraph
    let (|TrimParagraphLines|) lines =
        lines
        // first remove all whitespace on the beginning of the line
        // then remove all additional spaces at the end, but keep two spaces if existent
        |> List.map (fun (StringPosition.TrimStart(s, n)) ->
            let endsWithTwoSpaces = s.EndsWith("  ", StringComparison.Ordinal)

            let trimmed = s.TrimEnd([| ' ' |]) + if endsWithTwoSpaces then "  " else ""

            (trimmed,
             { n with
                 EndColumn = n.EndColumn - s.Length + trimmed.Length }))

/// Parameterized pattern that assigns the specified value to the
/// first component of a tuple. Usage:
///
///    match str with
///    | Let 1 (n, "one") | Let 2 (n, "two") -> n
///
let (|Let|) a b = (a, b)

open System.Collections.Generic

/// Utility for parsing commands. Commands can be used in different places. We
/// recognize `key1=value, key2=value` and also `key1:value, key2:value`
/// The key of the command should be identifier with just
/// characters in it - otherwise, the parsing fails.
let (|ParseCommands|_|) (str: string) =
    let kvs =
        [ for cmd in str.Split(',') do
              let kv = cmd.Split([| '='; ':' |])

              if kv.Length = 2 then
                  yield kv.[0].Trim(), kv.[1].Trim()
              elif kv.Length = 1 then
                  yield kv.[0].Trim(), "" ]

    let allKeysValid =
        kvs
        |> Seq.forall (fst >> Seq.forall (fun c -> Char.IsLetter c || c = '_' || c = '-'))

    if allKeysValid && kvs <> [] then Some(dict kvs) else None

/// Utility for parsing commands - this deals with a single command.
/// The key of the command should be identifier with just
/// characters in it - otherwise, the parsing fails.
let (|ParseCommand|_|) (cmd: string) =
    let kv = cmd.Split([| '='; ':' |])

    if kv.Length >= 1 && not (Seq.forall Char.IsLetter kv.[0]) then
        None
    elif kv.Length = 2 then
        Some(kv.[0].Trim(), kv.[1].Trim())
    elif kv.Length = 1 then
        Some(kv.[0].Trim(), "")
    else
        None

/// Lookup in a dictionary
let (|Command|_|) k (d: IDictionary<_, _>) =
    match d.TryGetValue(k) with
    | true, v -> Some v
    | _ -> None
