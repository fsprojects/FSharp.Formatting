// --------------------------------------------------------------------------------------
// F# Markdown 
// --------------------------------------------------------------------------------------

namespace rec FSharp.Formatting.Markdown

open System.Collections.Generic
open FSharp.Formatting.Templating

module internal MarkdownUtils =
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

    /// Lookup a specified key in a dictionary, possibly
    /// ignoring newlines or spaces in the key.
    let (|LookupKey|_|) (dict:IDictionary<_, _>) (key:string) = 
      [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); 
        key.Replace("\n", ""); key.Replace("\n", " ") ]
      |> Seq.tryPick (fun key ->
        match dict.TryGetValue(key) with
        | true, v -> Some v 
        | _ -> None)

    /// Context passed around while formatting 
    type FormattingContext =
      { Links : IDictionary<string, string * option<string>>
        Newline: string
        /// Additional replacements to be made in the code snippets
        Substitutions : Substitutions
        DefineSymbol: string
      }

    /// Format a MarkdownSpan
    let rec formatSpan (ctx:FormattingContext) span =
      match span with 
      | LatexInlineMath(body, _) -> sprintf "$%s$" body
      | LatexDisplayMath(body, _) -> sprintf "$$%s$$" body
      | EmbedSpans(cmd, _) -> formatSpans ctx (cmd.Render())
      | Literal(str, _) -> str
      | HardLineBreak(_) -> "\n"

      | AnchorLink _ -> ""
      | IndirectLink(body, _, LookupKey ctx.Links (link, _), _) 
      | DirectLink(body, link, _, _)
      | IndirectLink(body, link, _, _) ->
          "[" + formatSpans ctx body + "](" + link + ")"

      | IndirectImage(_body, _, LookupKey ctx.Links (_link, _), _) 
      | IndirectImage(_body, _link, _, _) ->
          failwith "tbd - IndirectImage"
      | DirectImage(_body, _link, _, _) ->
        sprintf "![%s](%s)" _body _link
      | Strong(body, _) -> 
          "**" + formatSpans ctx body + "**"
      | InlineCode(body, _) -> 
          "`" + body + "`"
      | Emphasis(body, _) -> 
          "**" + formatSpans ctx body + "**"

    /// Format a list of MarkdownSpan 
    and formatSpans ctx spans = spans |> List.map (formatSpan ctx) |> String.concat ""

    /// Format a MarkdownParagraph 
    let rec formatParagraph (ctx: FormattingContext) paragraph =
        [  match paragraph with
            | LatexBlock(env, lines, _) ->
                yield sprintf "\\begin{%s}" env
                for line in lines do
                    yield line
                yield sprintf "\\end{%s}"  env
                yield ""

            | Heading(n, spans, _) -> 
                yield (String.replicate n "#") + " " + formatSpans ctx spans
                yield ""
            | Paragraph(spans, _) ->
                yield (String.concat "" [ for span in spans -> formatSpan ctx span ])
                yield ""

            | HorizontalRule(_) ->
                yield "-----------------------"
                yield ""
            | CodeBlock(code, _, _, _, _) ->
                yield code
                yield ""
            | ListBlock (Unordered, paragraphs, _) ->
                yield (String.concat "\n" (paragraphs |> List.collect(fun ps -> [ for p in ps -> String.concat "" (formatParagraph ctx p)])))
            | TableBlock (headers, alignments, rows, _) -> 
            
                match headers with
                 | Some headers -> 
                   yield (String.concat " | " (headers |> List.collect (fun hs -> [for h in hs -> String.concat "" (formatParagraph ctx h)])))
                 | None -> ()

                yield (String.concat " | " [
                    for a in alignments -> 
                      match a with
                       | AlignLeft -> ":---"
                       | AlignCenter -> ":---:"
                       | AlignRight -> "---:"
                       | AlignDefault -> "---"
                ])
                let replaceEmptyWith x s = match s with | "" | null -> x | s -> Some s
                yield String.concat "\n" [
                    for r in rows do
                    [
                      for ps in r do
                      let x = [
                            for p in ps do
                             yield formatParagraph ctx p |> Seq.choose (replaceEmptyWith (Some "")) |> String.concat ""
                          ] 
                      yield x |> Seq.choose (replaceEmptyWith (Some "")) |> String.concat "<br />"
                    ] |> Seq.choose (replaceEmptyWith (Some "&#32;"))  |> String.concat " | "
                ]
                yield "\n"

            | OutputBlock(output, "text/html", _executionCount) ->
                yield (output.Trim())
                yield ""
            | OutputBlock(output, _, _executionCount) ->
                yield "```"
                yield output 
                yield "```"
                yield ""
            | OtherBlock(lines, _) ->
                yield! List.map fst lines
                //yield ""
            | _ ->
                yield (sprintf "// can't yet format %0A to pynb markdown" paragraph)
                yield ""
         ]

    let formatFsxCode ctx (code: string) =
        // Inside literate code blocks we conditionally remove some special lines to get nicer output for
        // load sections for different formats. We remove this:
        //   #if IPYNB
        //   #endif // IPYNB
        let sym = ctx.DefineSymbol
        let sym1 = sprintf "#if %s" sym
        let sym2 = sprintf "#endif // %s" sym
        let lines = code.Replace("\r\n", "\n").Split('\n') |> Array.toList
        let lines = lines |> List.filter (fun line -> line.Trim() <> sym1 && line.Trim() <> sym2 )

        // Inside literate code blocks (not raw blocks) we make replacements for {{xyz}} parameters
        let lines = lines |> List.map (SimpleTemplating.ApplySubstitutionsInText ctx.Substitutions)
        let code2 = String.concat ctx.Newline lines
        code2
