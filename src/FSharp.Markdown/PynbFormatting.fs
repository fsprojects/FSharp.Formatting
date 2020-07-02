// --------------------------------------------------------------------------------------
// F# Markdown (LatexFormatting.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

/// [omit]
module FSharp.Markdown.Pynb

open System.Collections.Generic
open FSharp.Patterns
open FSharp.Collections
open FSharp.Formatting.Common.PynbModel

(*
module Array = 
    let filteri (f:int*'a->bool) (xs:'a[]) = 
        xs
        |> Array.mapi (fun i x -> (i,x))
        |> Array.filter f
        |> Array.map snd

type Entry =
    | StartSection
    | EndSection

type SectionType = 
    | Code
    | Cell
    | Markdown
    | Raw
    | Ydec
    | Hide

let linesToNotebook(lines: string[]) =
    // Strip simple lines, remove #if INTERACTIVE and clean up #if NOTEBOOK
    let text2 = 
        [ let mutable isInter = false
          let mutable isNote = false
          for line in lines do
             if line.StartsWith("#time") then ()
             elif line.StartsWith("#nowarn") then ()
             elif isInter then
                if line.StartsWith("#endif") then isInter <- false
             elif isNote then
                if line.StartsWith("#endif") then isNote <- false
                else yield line
             else
                if line.StartsWith("#if INTERACTIVE") then isInter <- true
                elif line.StartsWith("#if NOTEBOOK") then isNote <- true
                else yield line ]
        |> String.concat "\n"

    // Handle (***... *) notation
    let sections1 = 
        [|
            yield Regex(@"^(\(\*\*\* [a-z]+|\(\*\*)",RegexOptions.Multiline), StartSection
            yield Regex(@"[\*]+\)", RegexOptions.Multiline), EndSection
        |] 
        |> Array.collect (fun (x,y) -> [|for m in x.Matches(text2) -> (y, m.Index, m.Length)|])
        |> Array.sortBy(fun (_,index,_) -> index)

    printfn "sections1 = %A" sections1
    let sections2 = 
        [| let mutable kind = EndSection
           for (yKind,yIndex,yLength) in sections1 do
            if kind <> yKind then 
                kind <- yKind
                yield (yKind,(yIndex,yLength))
        |]
        |> Array.pairwise
        |> Array.filteri (fun (i,_) -> i % 2 = 0)

    printfn "sections2 = %A" sections2
    let sections3 = 
        [ for section in sections2 do
            match section with
            | (StartSection,(xIndex,xLength)),(EndSection,(yIndex,yLength)) ->  
                let substring = text2.Substring(xIndex)
                let sectionType,offset = 
                    if substring.StartsWith("(*** hide") then Hide,0
                    elif substring.StartsWith("(**") then Markdown,0
                    //elif substring.StartsWith("(*** raw") then Raw,4
                    //elif substring.StartsWith("(*** hide") then Raw,4
                    //elif substring.StartsWith("(*** ydec") then Ydec,5
                    else failwithf "Section %s" (substring.Substring(0,100))
                (sectionType,xIndex,xLength + offset,yIndex,yLength) 
            | _ -> failwith "should not happen" ]

    printfn "sections3 = %A" sections3
    // Add the code sections
    let sections3b = 
        [ let mutable index = 0
          for (sectionType,xIndex,xLength,yIndex,yLength) in sections3 do   
             yield (Code,index,0,xIndex,0)
             yield (sectionType,xIndex,xLength,yIndex,yLength)
             index <- yIndex + yLength ]

    let sections4 = 
        [ match sections3b with 
          | [] -> yield (Code,0,0,text2.Length,0)
          | xs -> 
              let (_,_,_,yIndex,yLength) = List.last xs
              yield! xs
              yield (Code,yIndex+yLength,0,text2.Length,0) ]
    printfn "sections4 = %A" sections4

    // Merge filter out cell breaks
    let cells = 
        sections4
        |> List.map (fun (t,xIndex,xLength,yIndex,_yLength) ->
            (t,text2.Substring(xIndex + xLength,yIndex - (xIndex + xLength))))
        |> List.filter (fun (t,s) -> not (t = Code && System.String.IsNullOrWhiteSpace s))
        |> List.map (fun (t,s) -> (t, s.Trim(' ', '\n', '\r')))

    // Merge ydec into code, filter out cell breaks, map into Cell type
    let cells2 =
        [| let mutable code = None
           let mutable hide = false
           for (t, s) in cells do
                match t with
                | Ydec
                | Code ->
                    code <- Some((match code with | None -> "" | Some(s) -> s) + "\n" + s)
                | Hide -> 
                    if not hide then 
                        yield! Option.toList (Option.map codeCell code)
                    code <- None
                    hide <- true
                | Cell -> 
                    if not hide then 
                        yield! Option.toList (Option.map codeCell code)
                    code <- None
                    hide <- false
                | Markdown -> 
                    if not hide then 
                        yield! Option.toList (Option.map codeCell code)
                    yield markdownCell s
                    code <- None
                    hide <- false
                | Raw -> 
                    if not hide then 
                        yield! Option.toList (Option.map codeCell code)
                    yield rawCell s
                    code <- None
                    hide <- false
           yield! Option.toList (Option.map codeCell code)
        |]

    {Notebook.Default with cells = cells2}

*)


/// LaTEX special chars
/// from http://tex.stackexchange.com/questions/34580/escape-character-in-latex
let specialChars =
    [|  // This line comes first to avoid double replacing
        // It also accommodates \r, \n, \t, etc.
        @"\", @"<\textbackslash>";
        "#", @"\#";
        "$", @"\$";
        "%", @"\%";
        "&", @"\&";
        "_", @"\_";
        "{", @"\{";
        "}", @"\}";
        @"<\textbackslash>", @"{\textbackslash}";
        "~",@"{\textasciitilde}";
        "^", @"{\textasciicircum}" |]
    
let pynbEncode s =
    specialChars |> Array.fold (fun (acc:string) (k, v) -> acc.Replace(k, v)) (System.Net.WebUtility.HtmlDecode s)

/// Lookup a specified key in a dictionary, possibly
/// ignoring newlines or spaces in the key.
let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) = 
  [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); 
    key.Replace("\n", ""); key.Replace("\n", " ") ]
  |> Seq.tryPick (fun key ->
    match dict.TryGetValue(key) with
    | true, v -> Some v 
    | _ -> None)

/// Context passed around while formatting the LaTEX
type FormattingContext =
  { Links : IDictionary<string, string * option<string>> }

/// Write MarkdownSpan value to a TextWriter
let rec formatSpanAsMarkdown (ctx:FormattingContext) = function 
  | LatexInlineMath(body, _) -> sprintf "$%s$" body
  | LatexDisplayMath(body, _) -> sprintf "$$%s$$" body
  | EmbedSpans(cmd, _) -> formatSpansAsMarkdown ctx (cmd.Render())
  | Literal(str, _) -> pynbEncode str
  | HardLineBreak(_) -> "\n"

  | AnchorLink _ -> ""
  | IndirectLink(body, _, LookupKey ctx.Links (link, _), _) 
  | DirectLink(body, link, _, _)
  | IndirectLink(body, link, _, _) ->
      "[" + formatSpansAsMarkdown ctx body + "](" + link + ")"

  | IndirectImage(body, _, LookupKey ctx.Links (link, _), _) 
  | DirectImage(body, link, _, _) 
  | IndirectImage(body, link, _, _) ->
      failwith "tbd - IndirectImage"
      //// Use the technique introduced at
      //// http://stackoverflow.com/q/14014827
      //if not (System.String.IsNullOrWhiteSpace(body)) then
      //  ctx.Writer.Write(@"\begin{figure}[htbp]\centering")
      //  ctx.LineBreak()
      //ctx.Writer.Write(@"\includegraphics[width=1.0\textwidth]{")
      //ctx.Writer.Write(pynbEncode link)
      //ctx.Writer.Write("}") 
      //ctx.LineBreak()
      //if not (System.String.IsNullOrWhiteSpace(body)) then
      //  ctx.Writer.Write(@"\caption{")
      //  ctx.Writer.Write(pynbEncode body)
      //  ctx.Writer.Write("}")
      //  ctx.LineBreak()
      //  ctx.Writer.Write(@"\end{figure}")
      //  ctx.LineBreak()

  | Strong(body, _) -> 
      "**" + formatSpansAsMarkdown ctx body + "**"
  | InlineCode(body, _) -> 
      "`" + body + "`"
  | Emphasis(body, _) -> 
      "**" + formatSpansAsMarkdown ctx body + "**"

/// Write list of MarkdownSpan values to a TextWriter
and formatSpansAsMarkdown ctx spans = spans |> List.map (formatSpanAsMarkdown ctx) |> String.concat ""

let isCode = (function CodeBlock(_, _, _, _, _) | InlineBlock (_, _, _) -> true | _ -> false)
let isCodeOutput = (function OutputBlock _ -> true | _ -> false)
let getExecutionCount = (function CodeBlock(_, executionCount, _, _, _) | InlineBlock (_, executionCount, _) -> executionCount | _ -> None)
let getCode = (function CodeBlock(code, _, _, _, _) -> code | InlineBlock (code, _, _) -> code | _ -> failwith "unreachable")
let getCodeOutput = (function OutputBlock (code, kind, _) -> code, kind | _ -> failwith "unreachable")
let splitParagraphs paragraphs =
    let firstCode = paragraphs |> List.tryFindIndex isCode
    match firstCode with
    | Some 0 ->
        let code = paragraphs.[0]
        let codeLines = getCode code
        let otherParagraphs = paragraphs.[1..]

        // Collect the code output(s) that follows this cell if any
        let codeOutput = otherParagraphs |> List.takeWhile isCodeOutput |> List.map getCodeOutput
        let otherParagraphs = otherParagraphs |> List.skipWhile isCodeOutput
        Choice1Of2 (codeLines, codeOutput, getExecutionCount code), otherParagraphs

    | Some _ | None ->
        let markdownParagraphs = paragraphs |> List.takeWhile (isCode >> not)
        let otherParagraphs = paragraphs |> List.skipWhile (isCode >> not)
        Choice2Of2 markdownParagraphs, otherParagraphs

/// Write a MarkdownParagraph value to a TextWriter
let rec formatMarkdownParagraph ctx paragraph =
    [|  match paragraph with
        | LatexBlock(env, lines, _) ->
            yield sprintf "\begin{%s}" env
            for line in lines do
                yield line
            yield sprintf "\end{%s}"  env

        | Heading(n, spans, _) -> 
            yield (String.replicate n "#") + formatSpansAsMarkdown ctx spans
        | Paragraph(spans, _) ->
            yield (String.concat "" [ for span in spans -> formatSpanAsMarkdown ctx span ])

        | HorizontalRule(_) ->
            yield "-----------------------"
        | CodeBlock(code, _, _, _, _) ->
            yield code
        | OutputBlock(output, _, _executionCount) ->
            yield output
        | _ ->
            yield (sprintf "// can't yet format %0A to pynb markdown" paragraph)
     |]

(*
        | TableBlock(headers, alignments, rows, _) ->
          let aligns = alignments |> List.map (function
            | AlignRight -> "|r"
            | AlignCenter -> "|c"
            | AlignDefault | AlignLeft -> "|l") |> String.concat ""
          ctx.Writer.Write(@"\begin{tabular}{" + aligns + @"|}\hline")
          ctx.LineBreak()

          let bodyCtx = { ctx with LineBreak = noBreak ctx }
          let formatRow (prefix:string) (postfix:string) row = 
            row |> Seq.iteri (fun i cell ->
              if i <> 0 then ctx.Writer.Write(" & ")
              ctx.Writer.Write(prefix)
              cell |> List.iter (formatParagraph bodyCtx) 
              ctx.Writer.Write(postfix) )
    
          for header in Option.toList headers do
            formatRow @"\textbf{" "}" header
            ctx.Writer.Write(@"\\ \hline\hline")
            ctx.LineBreak()
          for row in rows do
            formatRow "" "" row
            ctx.Writer.Write(@"\\ \hline")
            ctx.LineBreak()
          ctx.Writer.Write(@"\end{tabular}")
          ctx.LineBreak()

      | ListBlock(kind, items, _) ->
          let tag = if kind = Ordered then "enumerate" else "itemize"
          ctx.Writer.Write(@"\begin{" + tag + "}")
          ctx.LineBreak()
          for body in items do
            ctx.Writer.Write(@"\item ")
            body |> List.iter (formatParagraph ctx)
            ctx.LineBreak()
          ctx.Writer.Write(@"\end{" + tag + "}")
          ctx.LineBreak()

      | QuotedBlock(body, _) ->
          ctx.Writer.Write(@"\begin{quote}")
          ctx.LineBreak()
          formatParagraphs ctx body
          ctx.Writer.Write(@"\end{quote}")
          ctx.LineBreak()

      | Span(spans, _) -> 
          formatSpansAsMarkdown ctx spans
      | InlineBlock(code, _) ->
          ctx.Writer.Write(code)
      ctx.LineBreak()
*)

let formatCodeOutput executionCount (output: string, kind) : Output =
    let lines = output.Split([|'\n';'\r'|])
    {
        data= OutputData(kind, lines)
        execution_count = executionCount
        metadata = ""
        output_type = "execute_result"
    }

/// Write a list of MarkdownParagraph values to a TextWriter
let rec formatParagraphs ctx paragraphs = 
  match paragraphs with
  [] -> []
  | _ ->
  let k, otherParagraphs = splitParagraphs paragraphs
  let cell =
      match k with 
      | Choice1Of2 (code,  codeOutput, executionCount) ->
          codeCell [| code |] executionCount (List.map (formatCodeOutput executionCount) codeOutput |> Array.ofList)
      | Choice2Of2 markdown ->
          markdownCell (Array.ofList (List.collect (formatMarkdownParagraph ctx >> Array.toList) markdown))
  let others =  formatParagraphs ctx otherParagraphs
  let cells = cell :: others
  cells
 
let formatMarkdownAsPynb links paragraphs = 
  let cells = formatParagraphs { Links = links } paragraphs
  let notebook = {Notebook.Default with cells = Array.ofList cells}
  notebook.ToString()
