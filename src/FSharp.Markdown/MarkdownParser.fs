// --------------------------------------------------------------------------------------
// F# Markdown (MarkdownParser.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

module FSharp.Markdown.Parser

open System
open System.IO
open System.Collections.Generic

open FSharp.Patterns
open FSharp.Collections

// --------------------------------------------------------------------------------------
// Parsing of Markdown - first part handles inline formatting
// --------------------------------------------------------------------------------------

/// Splits a link formatted as `http://link "title"` into a link part
/// and an optional title part (may be wrapped using quote or double-quotes)
let getLinkAndTitle (String.TrimBoth input) =
  let url, title =  
    if input.Length = 0 then "", None else
    let c = input.[input.Length - 1]
    if c = '\'' || c = '"' then
      let start = input.IndexOf(c)
      input.Substring(0, start).Trim(), Some(input.Substring(start + 1, input.Length - 2 - start).Trim())
    else input, None
  url.TrimStart('<').TrimEnd('>'), title

/// Recognizes an indirect link written using `[body][key]` or just `[key]`
/// The key can be preceeded by a space or other single whitespace thing.
let (|IndirectLink|_|) = function
  | List.BracketDelimited '[' ']' (body, '\r'::'\n'::(List.BracketDelimited '[' ']' (List.AsString link, rest))) -> 
      Some(body, link, "\r\n[" + link + "]", rest)
  | List.BracketDelimited '[' ']' (body, ((' ' | '\n') as c)::(List.BracketDelimited '[' ']' (List.AsString link, rest))) -> 
      Some(body, link, c.ToString() + "[" + link + "]", rest)
  | List.BracketDelimited '[' ']' (body, List.BracketDelimited '[' ']' (List.AsString link, rest)) ->
      Some(body, link, "[" + link + "]", rest)  
  | List.BracketDelimited '[' ']' (body, rest) ->
      Some(body, "", "", rest)  
  | _ -> None

/// Recognize a direct link written using `[body](http://url "with title")`
let (|DirectLink|_|) = function
  | List.BracketDelimited '[' ']' (body, List.BracketDelimited '(' ')' (link, rest)) ->
      Some(body, link, rest)
  | _ -> None

/// Recognizes some form of emphasis using `**bold**` or `*italic*`
/// (both can be also marked using underscore).
/// TODO: This does not handle nested emphasis well.
let (|Emphasised|_|) = function
  | (('_' | '*') :: tail) as input ->
    match input with
    | List.Delimited ['_'; '_'; '_'] (body, rest) 
    | List.Delimited ['*'; '*'; '*'] (body, rest) -> 
        Some(body, Emphasis >> List.singleton >> Strong, rest)
    | List.Delimited ['_'; '_'] (body, rest) 
    | List.Delimited ['*'; '*'] (body, rest) -> 
        Some(body, Strong, rest)
    | List.Delimited ['_'] (body, rest) 
    | List.Delimited ['*'] (body, rest) -> 
        Some(body, Emphasis, rest)
    | _ -> None
  | _ -> None

/// Parses a body of a paragraph and recognizes all inline tags.
let rec parseChars acc input = seq {
  let accLiteral = lazy Literal(String(List.rev acc |> Array.ofList))
  match input with 
  // Recognizes explicit line-break at the end of line
  | ' '::' '::('\n' | '\r')::rest
  | ' '::' '::'\r'::'\n'::rest ->
      yield accLiteral.Value
      yield HardLineBreak
      yield! parseChars [] rest

  // Encode & as an HTML entity
  | '&'::'a'::'m'::'p'::';'::rest 
  | '&'::rest ->
      yield! parseChars (';'::'p'::'m'::'a'::'&'::acc) rest      

  // Ignore escaped characters that might mean something else
  | '\\'::( ( '*' | '\\' | '`' | '_' | '{' | '}' | '[' | ']' 
            | '(' | ')' | '>' | '#' | '.' | '!' | '+' | '-') as c) ::rest ->
      yield! parseChars (c::acc) rest

  // Inline code delimited either using double `` or single `
  | List.Delimited ['`'; '`'] (body, rest)
  | List.Delimited ['`'] (body, rest) ->
      yield accLiteral.Value
      yield InlineCode(String(Array.ofList body).Trim())
      yield! parseChars [] rest

  // Inline link wrapped as <http://foo.bar>
  | List.DelimitedWith ['<'] ['>'] (List.AsString link, rest) 
        when link.Contains("@") || link.Contains("://") ->
      yield accLiteral.Value
      yield DirectLink([Literal link], (link, None))
      yield! parseChars [] rest
  // Not an inline link - leave as an inline HTML tag
  | List.DelimitedWith ['<'] ['>'] (tag, rest) ->
      yield! parseChars ('>'::(List.rev tag) @ '<' :: acc) rest

  // Recognize direct link [foo](http://bar) or indirect link [foo][bar] 
  | DirectLink (body, link, rest) ->
      yield accLiteral.Value
      let info = getLinkAndTitle (String(Array.ofList link))
      yield DirectLink(parseChars [] body |> List.ofSeq, info)
      yield! parseChars [] rest
  | IndirectLink(body, link, original, rest) ->
      yield accLiteral.Value
      let key = if String.IsNullOrEmpty(link) then String(body |> Array.ofSeq) else link
      yield IndirectLink(parseChars [] body |> List.ofSeq, original, key)
      yield! parseChars [] rest

  // Recognize image - this is a link prefixed with the '!' symbol
  | '!'::DirectLink (body, link, rest) ->
      yield accLiteral.Value
      yield DirectImage(String(Array.ofList body), getLinkAndTitle (String(Array.ofList link)))
      yield! parseChars [] rest
  | '!'::IndirectLink(body, link, original, rest) ->
      yield accLiteral.Value
      let key = if String.IsNullOrEmpty(link) then String(body |> Array.ofSeq) else link
      yield IndirectImage(String(Array.ofList body), original, key)
      yield! parseChars [] rest

  // Handle emphasised text
  | Emphasised (body, f, rest) ->
      yield accLiteral.Value
      let body = parseChars [] body |> List.ofSeq
      yield f(body)
      yield! parseChars [] rest
  // Encode '<' char if it is not link or inline HTML
  | '<'::rest -> 
      yield! parseChars (';'::'t'::'l'::'&'::acc) rest      
  | x::xs -> 
      yield! parseChars (x::acc) xs 
  | [] ->
      yield accLiteral.Value }

/// Parse body of a paragraph into a list of Markdown inline spans      
let parseSpans (String.TrimBoth s) = 
  parseChars [] (s.ToCharArray() |> List.ofArray) |> List.ofSeq


// --------------------------------------------------------------------------------------
// Parsing of Markdown - second part handles paragraph-level formatting (headings, etc.)
// --------------------------------------------------------------------------------------

/// Recognizes heading, either prefixed with #s or followed by === or --- line
let (|Heading|_|) = function
  | (String.TrimBoth header) :: (String.TrimEnd (String.EqualsRepeated "=")) :: rest ->
      Some(1, header, rest)
  | (String.TrimBoth header) :: (String.TrimEnd (String.EqualsRepeated "-")) :: rest ->
      Some(2, header, rest)
  | String.StartsWithRepeated "#" (n, String.TrimEndUsing ['#'] header) :: rest ->
      Some(n, header, rest)
  | rest ->
      None

/// Recognizes a horizontal rule written using *, _ or -
let (|HorizontalRule|_|) (line:string) = 
  let rec loop ((h, a, u) as arg) i =
    if (h >= 3 || a >= 3 || u >= 3) && i = line.Length then Some()
    elif i = line.Length then None
    elif Char.IsWhiteSpace line.[i] then loop arg (i + 1)
    elif line.[i] = '-' && a = 0 && u = 0 then loop (h + 1, a, u) (i + 1)
    elif line.[i] = '*' && h = 0 && u = 0 then loop (h, a + 1, u) (i + 1)
    elif line.[i] = '_' && a = 0 && h = 0 then loop (h, a, u + 1) (i + 1)
    else None
  loop (0, 0, 0) 0

/// Recognizes a code block - lines starting with four spaces (including blank)
let (|CodeBlock|_|) = function
  | Lines.TakeStartingWithOrBlank "    " (Lines.TrimBlank lines, rest) when lines <> [] -> 
      let code =
        [ for l in lines -> 
            if String.IsNullOrWhiteSpace l then "" 
            elif l.Length > 4 then l.Substring(4, l.Length - 4) 
            else l ]
      Some(code, rest)
  | _ -> None

/// Matches when the input starts with a number. Returns the
/// rest of the input, following the last number.
let (|SkipSomeNumbers|_|) (input:string) = 
  match List.ofSeq input with
  | x::xs when Char.IsDigit x -> 
      let _, rest = List.partitionUntil (Char.IsDigit >> not) xs
      Some(rest)
  | _ -> None

/// Recognizes a staring of a list (either 1. or +, *, -).
/// Returns the rest of the line, together with the indent.
let (|ListStart|_|) = function  
  | String.TrimStartAndCount (indent, (String.StartsWithAny ["+ "; "* "; "- "] as item)) -> 
      Some(Unordered, indent, item.Substring(1))
  | String.TrimStartAndCount (indent, (SkipSomeNumbers ('.' :: ' ' :: List.AsString item))) ->
      Some(Ordered, indent, item)
  | _ -> None

/// Splits input into lines until whitespace or starting of a list and the rest.
let (|LinesUntilListOrWhite|) = 
  List.partitionUntil (function
    | ListStart _ | String.WhiteSpace -> true | _ -> false)

/// Splits input into lines until not-indented line or starting of a list and the rest.
let (|LinesUntilListOrUnindented|) =
  List.partitionUntil (function 
    | ListStart _ | String.Unindented -> true | _ -> false)

/// Recognizes a list item until the next list item (possibly nested) or end of a list.
/// The parameter specifies whether the previous line was simple (single-line not 
/// separated by a white line - simple items are not wrapped in <p>)
let (|ListItem|_|) prevSimple = function
  | ListStart(kind, indent, item)::
      // Take remaining lines that belong to the same item
      // (everything until an empty line or start of another list item)
      LinesUntilListOrWhite
        (continued, 
            // Take more things that belong to the item - 
            // the value 'more' will contain indented paragraphs
            (LinesUntilListOrUnindented (more, rest) as next)) ->
      let simple = 
        match next, rest with 
        | String.WhiteSpace::_, (ListStart _)::_ -> false
        | (ListStart _)::_, _ -> true 
        | [], _ -> true 
        | _, String.Unindented::_ -> prevSimple
        | _, _ -> false

      let lines = 
        [ yield item
          for line in continued do
            yield line.Trim()
          for line in more do
            let trimmed = line.TrimStart()
            if trimmed.Length >= line.Length - 4 then yield trimmed
            else yield line.Substring(4) ]
      Some(indent, (simple, kind, lines), rest)
  | _ -> None

/// Recognizes a list - returns list items with information about 
/// their indents - these need to be turned into a tree structure later.
let rec (|ListItems|_|) prevSimple = function
  | ListItem prevSimple (indent, ((nextSimple, _, _) as info), rest) ->
      match rest with 
      | (HorizontalRule _)::_ -> 
          Some([indent, info], rest)
      | ListItems nextSimple (items, rest) ->
          Some((indent, info)::items, rest)
      | _ -> Some([indent, info], rest)
  | _ -> None


// Code for parsing pipe tables

/// Recognizes alignment specified in the passed separator line.
let (|TableCellSeparator|_|) = function
  | String.StartsAndEndsWith (":", ":") (String.EqualsRepeated "-") -> Some(AlignCenter)
  | String.StartsWith ":" (String.EqualsRepeated "-") -> Some(AlignLeft)
  | String.StartsAndEndsWith ("", ":") (String.EqualsRepeated "-") -> Some(AlignRight)
  | String.EqualsRepeated "-" -> Some(AlignDefault)
  | _ -> None

/// Recognizes row of pipe table.
/// The function takes number of expected columns and array of delimiters.
/// Returns list of strings between delimiters.
let (|PipeTableRow|_|) (size:option<int>) delimiters (line:string) =
  let parts = line.Split(delimiters) |> Array.map (fun s -> s.Trim())
  let n = parts.Length
  let m = if size.IsNone then 1 else size.Value
  let x = if String.IsNullOrEmpty parts.[0] && n > m then 1 else 0
  let y = if String.IsNullOrEmpty parts.[n - 1] && n - x > m then n - 2 else n - 1
  if n = 1 || (size.IsSome && y - x + 1 <> m) then None else Some(parts.[x..y] |> Array.toList)

/// Recognizes separator row of pipe table.
/// Returns list of alignments.
let (|PipeSeparatorRow|_|) size = function 
  | PipeTableRow size [|'|'; '+'|] parts ->
      let alignments = parts |> List.choose ( |TableCellSeparator|_| )
      if parts.Length <> alignments.Length then None else (Some alignments)
  | _ -> None 

/// Recognizes pipe table
let (|PipeTableBlock|_|) input =
  let rec getTableRows size acc = function
    | (PipeTableRow size [|'|'|] columns) :: rest ->
        getTableRows size (List.map (fun l -> [l]) columns :: acc) rest
    | rest -> (List.rev acc, rest)
  match input with
  | (PipeSeparatorRow None alignments) :: rest ->
      let rows, others = getTableRows (Some alignments.Length) [] rest
      Some((None, alignments, rows), others)    
  | (PipeTableRow None [|'|'|] headers) :: rest ->
      match rest with
      | (PipeSeparatorRow (Some headers.Length) alignments) :: rest ->
          let rows, others = getTableRows (Some headers.Length) [] rest
          let header_paragraphs = headers |> List.map (fun l -> [l])
          Some((Some(header_paragraphs), alignments, rows), others)
      | _ -> None
  | _ -> None

// Code for parsing emacs tables

/// Recognizes one line of emacs table. It can be line with content or separator line.
/// The function takes positions of grid columns (if known) and expected grid separator.
/// Passed function is used to check whether all parts within grid are valid.
/// Retuns tuple (position of grid columns, text between grid columns).
let (|EmacsTableLine|_|) (grid:option<int []>) (c:char) (check:string -> bool) (line:string) =
  let p = if grid.IsSome then grid.Value else Array.FindAll([|0..line.Length - 1|], fun i -> line.[i] = c) 
  let n = p.Length - 1
  if n < 2 || line.Length <= p.[n] || Array.exists (fun i -> line.[i] <> c) p then None
  else
    let parts = [1..n] |> List.map (fun i -> line.Substring(p.[i - 1] + 1, p.[i] - p.[i - 1] - 1))
    if List.forall check parts then Some(p, parts) else None

/// Recognizes emacs table
let (|EmacsTableBlock|_|) input =
  let isCellSep = String.(|EqualsRepeated|_|) "-" >> Option.isSome
  let isAlignedCellSep = ( |TableCellSeparator|_| ) >> Option.isSome
  let isHeadCellSep = String.(|EqualsRepeated|_|) "=" >> Option.isSome
  let isText (s:string) = true
  match input with
  | (EmacsTableLine None '+' isAlignedCellSep (grid, parts)) :: rest ->
    let alignments = List.choose ( |TableCellSeparator|_| ) parts
    // iterate over rows and go from state to state
    // headers - the content of head row (initially none)
    // prevRow - content of the processed rows
    // cur - list of paragraphs in the current row (list of empty lists after each separator line)
    // flag indicates whether current row is empty (similar to List.forall (List.isEmpty) cur)
    let emptyCur = List.replicate<string list> (grid.Length - 1) []
    let rec loop flag headers prevRows cur = function
      | (EmacsTableLine (Some grid) '|' isText (_, parts)) :: others ->
          loop false headers prevRows (List.zip parts cur |> List.map (fun (h, t) -> h.TrimEnd() :: t)) others
      | (EmacsTableLine (Some grid) '+' isCellSep _) :: others ->
          loop true headers (List.map (List.rev) cur :: prevRows) emptyCur others
      | (EmacsTableLine (Some grid) '+' isHeadCellSep _) :: others when Option.isNone headers ->
          loop true (Some (List.map (List.rev) cur)) prevRows emptyCur others
      | others when flag -> Some((headers, alignments, List.rev prevRows), others)
      | _ -> None
    loop true None [] emptyCur rest
  | _ -> None


/// Recognizes a start of a blockquote
let (|BlockquoteStart|_|) (line:string) = 
  let rec spaces i = 
    // Too many spaces or beyond the end of line
    if i = 4 || i = line.Length then None
    elif line.[i] = '>' then 
      // Blockquote - does it have additional space?
      let start =
        if (i + 1) = line.Length || line.[i+1] <> ' ' then i + 1
        else i + 2
      Some(line.Substring(start))
    elif line.[i] = ' ' then spaces (i + 1)
    else None
  spaces 0

/// Takes lines that belong to a continuing paragraph until 
/// a white line or start of other paragraph-item is found
let (|TakeParagraphLines|_|) input = 
  match List.partitionWhileLookahead (function
    | Heading _ -> false
    | BlockquoteStart _::_ -> false
    | String.WhiteSpace::_ -> false
    | _ -> true) input with
  | matching, rest when matching <> [] -> Some(matching, rest)
  | _ -> None

/// Recognize nested HTML block.
/// Standard markdown allows to include blocks of HTML between balanced tags that are separated
/// from the surrounding text with blank lines, and start and end at the left margin.
/// Also treats as HTML one line paragraphs that starts with open tag bracket and doesn't contain @.
let (|HtmlBlock|_|) input = 
  let rec loop first last acc = function
    | head::tail when not (String.IsNullOrWhiteSpace(head)) ->
        loop (if String.IsNullOrEmpty(first) then head else first) head (head::acc) tail
    | rest -> first, last, List.rev acc, rest
  match loop "" "" [] input with
  | (String.StartsWith "<" s, String.StartsAndEndsWith ("</", ">") tag, html, rest) when s.StartsWith(tag) ->
      Some(html, rest)  
  | (f, _, html, rest) when (List.length html) = 1 && f.StartsWith("<") && not (f.StartsWith("<http://")) &&
                            not (f.StartsWith("<ftp://")) && not (f.Contains("@")) ->
      Some(html, rest)      
  | _ -> None

/// Continues taking lines until a whitespace line or start of a blockquote
let (|LinesUntilBlockquoteOrWhite|) =
  List.partitionUntil (function 
    | BlockquoteStart _ | String.WhiteSpace -> true | _ -> false)

/// Recognizes blockquote - continues taking paragraphs 
/// starting with '>' until there is something else
let rec (|Blockquote|_|) = function
  | BlockquoteStart(line)::LinesUntilBlockquoteOrWhite(continued, Lines.TrimBlankStart rest) ->
      let moreLines, rest = 
        match rest with 
        | Blockquote(lines, rest) -> lines, rest
        | _ -> [], rest
      Some (line::continued @ moreLines, rest)
  | _ -> None

/// Recognize a definition of a link as in `[key]: http://url ...`
let (|LinkDefinition|_|) = function
  | ( String.StartsWithWrapped ("[", "]:") (wrapped, String.TrimBoth link) 
    | String.StartsWithWrapped (" [", "]:") (wrapped, String.TrimBoth link) 
    | String.StartsWithWrapped ("  [", "]:") (wrapped, String.TrimBoth link) 
    | String.StartsWithWrapped ("   [", "]:") (wrapped, String.TrimBoth link) ) :: rest ->
        Some((wrapped, link), rest)
  | _ -> None

/// Defines a context for the main `parseParagraphs` function
type ParsingContext = 
  { Links : Dictionary<string, string * option<string>> 
    Newline : string }

/// Parse a list of lines into a sequence of markdown paragraphs
let rec parseParagraphs (ctx:ParsingContext) lines = seq {
  match lines with
  // Recognize various kinds of standard paragraphs
  | LinkDefinition ((key, link), Lines.TrimBlankStart lines) ->
      ctx.Links.Add(key, getLinkAndTitle link)
      yield! parseParagraphs ctx lines
  | CodeBlock(code, Lines.TrimBlankStart lines) ->
      yield CodeBlock(code |> String.concat ctx.Newline)
      yield! parseParagraphs ctx lines 
  | Blockquote(body, Lines.TrimBlankStart rest) ->
      yield QuotedBlock(parseParagraphs ctx body |> List.ofSeq)
      yield! parseParagraphs ctx rest
  | EmacsTableBlock((headers, alignments, rows), Lines.TrimBlankStart rest) 
  | PipeTableBlock((headers, alignments, rows), Lines.TrimBlankStart rest) ->
      let headParagraphs = 
        if headers.IsNone then None
        else Some(headers.Value |> List.map (fun i -> parseParagraphs ctx i |> List.ofSeq))
      yield TableBlock(headParagraphs, alignments,
        rows |> List.map (List.map (fun i -> parseParagraphs ctx i |> List.ofSeq)))
      yield! parseParagraphs ctx rest 
  | HorizontalRule :: (Lines.TrimBlankStart lines) ->
      yield HorizontalRule
      yield! parseParagraphs ctx lines


  // Recognize list of list items and turn it into nested lists
  | ListItems true (items, Lines.TrimBlankStart rest) ->
      let tree = Tree.ofIndentedList items

      // Nest all items that have another kind (i.e. UL vs. OL)
      let rec nestUnmatchingItems items = 
        match items with
        | Node((_, baseKind, _), _)::_ ->
            items 
            |> List.nestUnderLastMatching (fun (Node((_, kind, _), _)) -> kind = baseKind)
            |> List.map (fun (Node(info, children), nested) ->
                let children = nestUnmatchingItems children
                Node(info, children @ nested))
        | [] -> []

      // Turn tree into nested list definitions
      let rec formatTree (nodes:Tree<bool * MarkdownListKind * string list> list) =
        let kind = match nodes with Node((_, kind, _), _)::_ -> kind | _ -> Unordered
        let items =
          [ for (Node((simple, _, body), nested)) in nodes ->
              [ if not simple then yield! parseParagraphs ctx body
                else yield Span(parseSpans(String.concat ctx.Newline body))
                if nested <> [] then
                  yield formatTree nested ] ]
        ListBlock(kind, items)
      yield formatTree tree
      yield! parseParagraphs ctx rest

  // Recognize remaining types of paragraphs
  | Heading(n, body, Lines.TrimBlankStart lines) ->
      yield Heading(n, parseSpans body)
      yield! parseParagraphs ctx lines 
  | HtmlBlock(html, Lines.TrimBlankStart rest) ->
      yield HtmlBlock(String.concat ctx.Newline html)
      yield! parseParagraphs ctx rest
  | TakeParagraphLines(lines, Lines.TrimBlankStart rest) ->      
      yield Paragraph (parseSpans (String.concat ctx.Newline lines))
      yield! parseParagraphs ctx rest 

  | Lines.TrimBlankStart [] -> () 
  | _ -> failwithf "Unexpectedly stopped!\n%A" lines }