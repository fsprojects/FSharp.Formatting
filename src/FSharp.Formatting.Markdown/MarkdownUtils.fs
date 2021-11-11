// --------------------------------------------------------------------------------------
// F# Markdown
// --------------------------------------------------------------------------------------

namespace rec FSharp.Formatting.Markdown

open System.Collections.Generic
open FSharp.Formatting.Templating

module internal MarkdownUtils =
    let isCode =
        (function
        | CodeBlock _
        | InlineHtmlBlock _ -> true
        | _ -> false)

    let isCodeOutput =
        (function
        | OutputBlock _ -> true
        | _ -> false)

    let getExecutionCount =
        (function
        | CodeBlock (executionCount = executionCount)
        | InlineHtmlBlock (executionCount = executionCount) -> executionCount
        | _ -> None)

    let getCode =
        (function
        | CodeBlock (code = code) -> code
        | InlineHtmlBlock (code = code) -> code
        | _ -> failwith "unreachable")

    let getCodeOutput =
        (function
        | OutputBlock (code, kind, _) -> code, kind
        | _ -> failwith "unreachable")

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

            Choice1Of2(codeLines, codeOutput, getExecutionCount code), otherParagraphs

        | Some _
        | None ->
            let markdownParagraphs = paragraphs |> List.takeWhile (isCode >> not)

            let otherParagraphs = paragraphs |> List.skipWhile (isCode >> not)

            Choice2Of2 markdownParagraphs, otherParagraphs

    /// Lookup a specified key in a dictionary, possibly
    /// ignoring newlines or spaces in the key.
    let (|LookupKey|_|) (dict: IDictionary<_, _>) (key: string) =
        [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); key.Replace("\n", ""); key.Replace("\n", " ") ]
        |> Seq.tryPick (fun key ->
            match dict.TryGetValue(key) with
            | true, v -> Some v
            | _ -> None)

    /// Context passed around while formatting
    type FormattingContext =
        { Links: IDictionary<string, string * option<string>>
          Newline: string
          /// Additional replacements to be made in content
          Substitutions: Substitutions
          /// Helper to resolve `cref:T:TypeName` references in markdown
          CodeReferenceResolver: string -> (string * string) option
          /// Helper to resolve `[foo](file.md)` references in markdown (where file.md is producing file.fsx)
          MarkdownDirectLinkResolver: string -> string option
          DefineSymbol: string }

    /// Format a MarkdownSpan
    let rec formatSpan (ctx: FormattingContext) span =
        match span with
        | LatexInlineMath (body, _) -> sprintf "$%s$" body
        | LatexDisplayMath (body, _) -> sprintf "$$%s$$" body
        | EmbedSpans (cmd, _) -> formatSpans ctx (cmd.Render())
        | Literal (str, _) -> str
        | HardLineBreak (_) -> "\n"

        | AnchorLink _ -> ""
        | IndirectLink (body, _, LookupKey ctx.Links (link, _), _)
        | DirectLink (body, link, _, _)
        | IndirectLink (body, link, _, _) -> "[" + formatSpans ctx body + "](" + link + ")"

        | IndirectImage (_body, _, LookupKey ctx.Links (_link, _), _)
        | IndirectImage (_body, _link, _, _) -> failwith "tbd - IndirectImage"
        | DirectImage (_body, _link, _, _) -> sprintf "![%s](%s)" _body _link
        | Strong (body, _) -> "**" + formatSpans ctx body + "**"
        | InlineCode (body, _) -> "`" + body + "`"
        | Emphasis (body, _) -> "**" + formatSpans ctx body + "**"

    /// Format a list of MarkdownSpan
    and formatSpans ctx spans =
        spans |> List.map (formatSpan ctx) |> String.concat ""

    /// Format a MarkdownParagraph
    let rec formatParagraph (ctx: FormattingContext) paragraph =
        [ match paragraph with
          | LatexBlock (env, lines, _) ->
              yield sprintf "\\begin{%s}" env

              for line in lines do
                  yield line

              yield sprintf "\\end{%s}" env
              yield ""

          | Heading (n, spans, _) ->
              yield String.replicate n "#" + " " + formatSpans ctx spans

              yield ""
          | Paragraph (spans, _) ->
              yield String.concat "" [ for span in spans -> formatSpan ctx span ]
              yield ""

          | HorizontalRule (_) ->
              yield "-----------------------"
              yield ""
          | CodeBlock (code = code; fence = fence; language = language) ->
              match fence with
              | None -> ()
              | Some f -> yield f + language

              yield code

              match fence with
              | None -> ()
              | Some f -> yield f

              yield ""
          | ListBlock (Unordered, paragraphsl, _) ->
              for paragraphs in paragraphsl do
                  for (i, paragraph) in List.indexed paragraphs do
                      let lines = formatParagraph ctx paragraph
                      let lines = if lines.IsEmpty then [ "" ] else lines

                      for (j, line) in List.indexed lines do
                          if i = 0 && j = 0 then
                              yield "* " + line
                          else
                              yield "  " + line

                      yield ""
          | ListBlock (Ordered, paragraphsl, _) ->
              for (n, paragraphs) in List.indexed paragraphsl do
                  for (i, paragraph) in List.indexed paragraphs do
                      let lines = formatParagraph ctx paragraph
                      let lines = if lines.IsEmpty then [ "" ] else lines

                      for (j, line) in List.indexed lines do
                          if i = 0 && j = 0 then
                              yield $"{n} " + line
                          else
                              yield "  " + line

                      yield ""
          | TableBlock (headers, alignments, rows, _) ->

              match headers with
              | Some headers ->
                  yield
                      headers
                      |> List.collect (fun hs -> [ for h in hs -> String.concat "" (formatParagraph ctx h) ])
                      |> String.concat " | "

              | None -> ()

              yield
                  [ for a in alignments ->
                        match a with
                        | AlignLeft -> ":---"
                        | AlignCenter -> ":---:"
                        | AlignRight -> "---:"
                        | AlignDefault -> "---" ]
                  |> String.concat " | "

              let replaceEmptyWith x s =
                  match s with
                  | ""
                  | null -> x
                  | s -> Some s

              yield
                  [ for r in rows do
                        [ for ps in r do
                              let x =
                                  [ for p in ps do
                                        yield
                                            formatParagraph ctx p
                                            |> Seq.choose (replaceEmptyWith (Some ""))
                                            |> String.concat "" ]

                              yield x |> Seq.choose (replaceEmptyWith (Some "")) |> String.concat "<br />" ]
                        |> Seq.choose (replaceEmptyWith (Some "&#32;"))
                        |> String.concat " | " ]
                  |> String.concat "\n"

              yield "\n"

          | OutputBlock (output, "text/html", _executionCount) ->
              yield (output.Trim())
              yield ""
          | OutputBlock (output, _, _executionCount) ->
              yield "```"
              yield output
              yield "```"
              yield ""
          | OtherBlock (lines, _) -> yield! List.map fst lines
          | InlineHtmlBlock (code, _, _) ->
              let lines = code.Replace("\r\n", "\n").Split('\n') |> Array.toList
              yield! lines
          //yield ""
          | YamlFrontmatter _ -> ()
          | Span (body = body) -> yield formatSpans ctx body
          | QuotedBlock (paragraphs = paragraphs) ->
              for paragraph in paragraphs do
                  let lines = formatParagraph ctx paragraph

                  for line in lines do
                      yield "> " + line

                  yield ""
          | _ ->
              printfn "// can't yet format %0A to markdown" paragraph
              yield "" ]

    let adjustFsxCodeForConditionalDefines (defineSymbol, newLine) (code: string) =
        // Inside literate code blocks we conditionally remove some special lines to get nicer output for
        // load sections for different formats. We remove this:
        //   #if IPYNB
        //   #endif // IPYNB
        let sym1 = sprintf "#if %s" defineSymbol
        let sym2 = sprintf "#endif // %s" defineSymbol

        let lines = code.Replace("\r\n", "\n").Split('\n') |> Array.toList

        let lines = lines |> List.filter (fun line -> line.Trim() <> sym1 && line.Trim() <> sym2)

        let code2 = String.concat newLine lines
        code2

    let applySubstitutionsInText ctx (text: string) =
        SimpleTemplating.ApplySubstitutionsInText ctx.Substitutions text

    let applyCodeReferenceResolver ctx (code, range) =
        match ctx.CodeReferenceResolver code with
        | None -> InlineCode(code, range)
        | Some (niceName, link) -> DirectLink([ Literal(niceName, range) ], link, None, range)

    let applyDirectLinkResolver ctx link =
        match ctx.MarkdownDirectLinkResolver link with
        | None -> link
        | Some newLink -> newLink

    let mapText (f, _, _) text = f text
    let mapInlineCode (_, f, _) (code, range) = f (code, range)
    let mapDirectLink (fText, _, fLink) text = fLink (fText text)

    let rec mapSpans fs (md: MarkdownSpans) =
        md
        |> List.map (function
            | Literal (text, range) -> Literal(mapText fs text, range)
            | Strong (spans, range) -> Strong(mapSpans fs spans, range)
            | Emphasis (spans, range) -> Emphasis(mapSpans fs spans, range)
            | AnchorLink (link, range) -> AnchorLink(mapText fs link, range)
            | DirectLink (spans, link, title, range) ->
                DirectLink(mapSpans fs spans, mapDirectLink fs link, Option.map (mapText fs) title, range)
            | IndirectLink (spans, original, key, range) -> IndirectLink(mapSpans fs spans, original, key, range)
            | DirectImage (body, link, title, range) ->
                DirectImage(mapText fs body, mapText fs link, Option.map (mapText fs) title, range)
            | IndirectImage (body, original, key, range) -> IndirectImage(mapText fs body, original, key, range)
            | HardLineBreak (range) -> HardLineBreak(range)
            | InlineCode (code, range) -> mapInlineCode fs (code, range)

            // NOTE: substitutions not applied to Latex math, embedded spans or inline code
            | LatexInlineMath (code, range) -> LatexInlineMath(code, range)
            | LatexDisplayMath (code, range) -> LatexDisplayMath(code, range)
            | EmbedSpans (customSpans, range) -> EmbedSpans(customSpans, range))

    let rec mapParagraphs f (md: MarkdownParagraphs) =
        md
        |> List.map (function
            | Heading (size, body, range) -> Heading(size, mapSpans f body, range)
            | Paragraph (body, range) -> Paragraph(mapSpans f body, range)
            | CodeBlock (code, count, fence, language, ignoredLine, range) ->
                CodeBlock(mapText f code, count, fence, language, ignoredLine, range)
            | OutputBlock (output, kind, count) -> OutputBlock(output, kind, count)
            | ListBlock (kind, items, range) -> ListBlock(kind, List.map (mapParagraphs f) items, range)
            | QuotedBlock (paragraphs, range) -> QuotedBlock(mapParagraphs f paragraphs, range)
            | Span (spans, range) -> Span(mapSpans f spans, range)
            | LatexBlock (env, body, range) -> LatexBlock(env, List.map (mapText f) body, range)
            | HorizontalRule (character, range) -> HorizontalRule(character, range)
            | YamlFrontmatter (lines, range) -> YamlFrontmatter(List.map (mapText f) lines, range)
            | TableBlock (headers, alignments, rows, range) ->
                TableBlock(
                    Option.map (List.map (mapParagraphs f)) headers,
                    alignments,
                    List.map (List.map (mapParagraphs f)) rows,
                    range
                )
            | OtherBlock (lines: (string * MarkdownRange) list, range) ->
                OtherBlock(lines |> List.map (fun (line, range) -> (mapText f line, range)), range)
            | InlineHtmlBlock (code, count, range) -> InlineHtmlBlock(mapText f code, count, range)

            // NOTE: substitutions are not currently applied to embedded LiterateParagraph which are in any case eliminated
            // before substitutions are applied.
            | EmbedParagraphs (customParagraphs, range) ->
                //let customParagraphsR = { new MarkdownEmbedParagraphs with member _.Render() = customParagraphs.Render() |> mapParagraphs f }
                EmbedParagraphs(customParagraphs, range))

    let applySubstitutionsInMarkdown ctx md =
        mapParagraphs (applySubstitutionsInText ctx, applyCodeReferenceResolver ctx, applyDirectLinkResolver ctx) md
