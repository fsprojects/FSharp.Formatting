namespace FSharp.Formatting.Literate

open System
open System.IO
open System.Collections.Generic

open FSharp.Formatting.CSharpFormat
open FSharp.Patterns
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.Markdown

module internal Transformations =
    // ----------------------------------------------------------------------------------------------
    // Replace all code snippets (assume F#) with their nicely formatted versions
    // ----------------------------------------------------------------------------------------------

    /// Iterate over Markdown document and extract all F# code snippets that we want
    /// to colorize. We skip snippets that specify non-fsharp langauge e.g. [lang=csharp].
    ///
    /// Note: this processes snipppets within markdown, not snippets coming from .fsx
    let rec collectCodeSnippets par =
        seq {
            match par with
            | CodeBlock(code, _executionCount, _fence, language, _, _) ->
                match code with
                | String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.SkipSingleLine _code) when
                    (not (String.IsNullOrWhiteSpace(language)) && language <> "fsharp")
                    || (cmds.ContainsKey("lang") && cmds.["lang"] <> "fsharp")
                    ->
                    ()
                | String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.SkipSingleLine code)
                | Let (dict []) (cmds, code) ->
                    let modul =
                        match cmds.TryGetValue("module") with
                        | true, v -> Some v
                        | _ -> None

                    yield modul, code
            | MarkdownPatterns.ParagraphLeaf _ -> ()
            | MarkdownPatterns.ParagraphNested(_, pars) ->
                for ps in pars do
                    for p in ps do
                        yield! collectCodeSnippets p
            | MarkdownPatterns.ParagraphSpans(_, _spans) -> ()
        }


    /// Replace CodeBlock elements with referenced code snippets.
    /// (The dictionary argument is a map from original code snippets to formatted HTML snippets.)
    ///
    /// Note: this processes snipppets within markdown, not snippets coming from .fsx
    let rec replaceCodeSnippets (path: string) (codeLookup: IDictionary<_, _>) para =
        match para with
        | CodeBlock(code, _executionCount, _fence, language, _, range) ->
            match code with
            | String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.SkipSingleLine code)
            | Let (dict []) (cmds, code) ->
                if cmds.ContainsKey("hide") then
                    None
                else
                    let code =
                        match cmds.TryGetValue "file", cmds.TryGetValue "key" with
                        | (true, fileVal), (true, keyVal) ->
                            // Get snippet from an external file
                            let file = Path.Combine(Path.GetDirectoryName(path), fileVal)

                            let startTag, endTag = "[" + keyVal + "]", "[/" + keyVal + "]"

                            let lines = File.ReadAllLines(file)

                            let startIdx = lines |> Array.findIndex (fun l -> l.Contains startTag)

                            let endIdx = lines |> Array.findIndex (fun l -> l.Contains endTag)

                            lines.[startIdx + 1 .. endIdx - 1]
                            |> Array.toList
                            |> String.removeSpaces
                            |> String.concat "\n"
                        | _ -> code

                    let lang =
                        match language with
                        | String.WhiteSpace when cmds.ContainsKey("lang") -> cmds.["lang"]
                        | language -> language

                    if not (String.IsNullOrWhiteSpace(lang)) && lang <> "fsharp" then
                        let popts = { Condition = None }
                        Some(EmbedParagraphs(LanguageTaggedCode(lang, code, popts), range))
                    else
                        let opts =
                            { Evaluate = false
                              ExecutionCount = None
                              OutputName = code
                              Visibility = LiterateCodeVisibility.VisibleCode }

                        let popts = { Condition = None }
                        Some(EmbedParagraphs(LiterateCode(codeLookup.[code], opts, popts), range))
            | _ -> Some para

        // Recursively process nested paragraphs, other nodes return without change
        | MarkdownPatterns.ParagraphNested(pn, nested) ->
            let pars = List.map (List.choose (replaceCodeSnippets path codeLookup)) nested

            MarkdownPatterns.ParagraphNested(pn, pars) |> Some
        | other -> Some other


    /// Walk over literate document and replace F# code snippets with
    /// their formatted representation (of LiterateParagraph type)
    ///
    /// Note: this processes snipppets within markdown, not snippets coming from .fsx
    let formatCodeSnippets (path: string) (ctx: CompilerContext) (doc: LiterateDocument) =
        let name = Path.GetFileNameWithoutExtension(path)

        // Extract all CodeBlocks and pass them to F# snippets
        let codes = doc.Paragraphs |> Seq.collect collectCodeSnippets |> Array.ofSeq

        let codeLookup, diagnostics =
            if codes.Length = 0 then
                dict [], [||]
            else
                // If there are some F# snippets, we build an F# source file
                let blocks =
                    codes
                    |> Seq.mapi (fun index ->
                        function
                        | Some modul, code ->
                            // Generate module & add indentation
                            "module "
                            + modul
                            + " =\n"
                            + "// [snippet:"
                            + (string<int> index)
                            + "]\n"
                            + "    "
                            + code.Replace("\n", "\n    ")
                            + "\n"
                            + "// [/snippet]"
                        | None, code -> "// [snippet:" + (string<int> index) + "]\n" + code + "\n" + "// [/snippet]")

                let modul = "module " + (new String(name |> Seq.filter Char.IsLetter |> Seq.toArray))

                let source = modul + "\r\n" + (String.concat "\n\n" blocks)

                // Process F# script file & build lookup table for replacement
                let defines =
                    match ctx.ConditionalDefines with
                    | [] -> None
                    | l -> Some(String.concat "," l)

                let snippets, diagnostics =
                    CodeFormatter.ParseAndCheckSource(
                        Path.ChangeExtension(path, ".fsx"),
                        source,
                        ctx.CompilerOptions,
                        defines,
                        ctx.OnError
                    )

                let results =
                    [ for (_, id), (Snippet(_, code)) in Array.zip codes snippets -> id, code ]
                    |> dict

                results, diagnostics

        // Replace code blocks with formatted snippets in the document
        let newPars = doc.Paragraphs |> List.choose (replaceCodeSnippets path codeLookup)

        doc.With(paragraphs = newPars, diagnostics = Array.append doc.Diagnostics diagnostics)

    // ----------------------------------------------------------------------------------------------
    // Generate references from indirect links
    // ----------------------------------------------------------------------------------------------

    /// Given Markdown document, get the keys of all IndirectLinks
    /// (to be used when generating paragraph with all references)
    let rec collectReferences =
        // Collect IndirectLinks in a span
        let rec collectSpanReferences span =
            seq {
                match span with
                | IndirectLink(_, _, key, _) -> yield key
                | MarkdownPatterns.SpanLeaf _ -> ()
                | MarkdownPatterns.SpanNode(_, spans) ->
                    for s in spans do
                        yield! collectSpanReferences s
            }
        // Collect IndirectLinks in a paragraph
        let rec loop par =
            seq {
                match par with
                | MarkdownPatterns.ParagraphLeaf _ -> ()
                | MarkdownPatterns.ParagraphNested(_, pars) ->
                    for ps in pars do
                        for p in ps do
                            yield! loop p
                | MarkdownPatterns.ParagraphSpans(_, spans) ->
                    for s in spans do
                        yield! collectSpanReferences s
            }

        loop

    /// Given Markdown document, add a number using the given index to all indirect
    /// references. For example, [article][ref] becomes [article][ref] [1](#rfxyz)
    let replaceReferences (refIndex: IDictionary<string, int>) =
        // Replace IndirectLinks with a nice link given a single span element
        let rec replaceSpans =
            function
            | IndirectLink(body, original, key, r) ->
                [ yield IndirectLink(body, original, key, r)
                  match refIndex.TryGetValue(key) with
                  | true, i ->
                      yield Literal("&#160;[", r)

                      yield
                          DirectLink([ Literal(string<int> i, r) ], "#rf" + DateTime.Now.ToString("yyMMddhh"), None, r)

                      yield Literal("]", r)
                  | _ -> () ]
            | MarkdownPatterns.SpanLeaf(sl) -> [ MarkdownPatterns.SpanLeaf(sl) ]
            | MarkdownPatterns.SpanNode(nd, spans) -> [ MarkdownPatterns.SpanNode(nd, List.collect replaceSpans spans) ]
        // Given a paragraph, process it recursively and transform all spans
        let rec loop =
            function
            | MarkdownPatterns.ParagraphNested(pn, nested) ->
                MarkdownPatterns.ParagraphNested(pn, List.map (List.choose loop) nested) |> Some
            | MarkdownPatterns.ParagraphSpans(ps, spans) ->
                MarkdownPatterns.ParagraphSpans(ps, List.collect replaceSpans spans) |> Some
            | MarkdownPatterns.ParagraphLeaf(pl) -> MarkdownPatterns.ParagraphLeaf(pl) |> Some

        loop

    /// Given all links defined in the Markdown document and a list of all links
    /// that are accessed somewhere from the document, generate References paragraph
    let generateReferenceParagraphs (definedLinks: IDictionary<_, string * string option>) refs =
        // For all unique references in the document,
        // get the link & title from definitions
        let refs =
            refs
            |> set
            |> Seq.choose (fun ref ->
                match definedLinks.TryGetValue(ref) with
                | true, (link, Some title) -> Some(ref, link, title)
                | _ -> None)
            |> Seq.sort
            |> Seq.mapi (fun i v -> i + 1, v)
        // Generate dictionary with a number for all references
        let refLookup = dict [ for (i, (r, _, _)) in refs -> r, i ]

        // Generate Markdown blocks paragraphs representing Reference <li> items
        let refList =
            [ for i, (_ref, link, title) in refs do
                  let colon = title.IndexOf(':')

                  if colon > 0 then
                      let auth = title.Substring(0, colon)

                      let name = title.Substring(colon + 1, title.Length - 1 - colon)

                      yield
                          [ Span(
                                [ Literal(sprintf "[%d] " i, None)
                                  DirectLink([ Literal(name.Trim(), None) ], link, Some title, None)
                                  Literal(" - " + auth, None) ],
                                None
                            ) ]
                  else
                      yield
                          [ Span(
                                [ Literal(sprintf "[%d] " i, None)
                                  DirectLink([ Literal(title, None) ], link, Some title, None) ],
                                None
                            ) ] ]

        // Return the document together with dictionary for looking up indices
        let id = DateTime.Now.ToString("yyMMddhh")

        [ Paragraph([ AnchorLink(id, None) ], None)
          Heading(3, [ Literal("References", None) ], None)
          ListBlock(MarkdownListKind.Unordered, refList, None) ],
        refLookup

    /// Turn all indirect links into a references
    /// and add paragraph to the document
    let generateReferences references (doc: LiterateDocument) =
        let references = defaultArg references false

        if references then
            let refs = doc.Paragraphs |> Seq.collect collectReferences

            let refPars, refLookup = generateReferenceParagraphs doc.DefinedLinks refs

            let newDoc = doc.Paragraphs |> List.choose (replaceReferences refLookup)

            doc.With(paragraphs = newDoc @ refPars)
        else
            doc

    // ----------------------------------------------------------------------------------------------
    // Transformation that collects evaluation results for F# snippets in the document
    // ----------------------------------------------------------------------------------------------

    /// Represents key in a dictionary with evaluation results
    type EvalKey =
        | OutputRef of string
        | ValueRef of string

    /// Unparse a Line list to a string - for evaluation by fsi.
    let unparse (lines: Line list) =
        let joinLine (Line(originalLine, _spans)) = originalLine
        //spans
        //|> Seq.map (fun span -> match span with TokenSpan.Token (_,s,_) -> s | TokenSpan.Omitted (s1,s2) -> s2 | _ -> "")
        //|> String.concat ""
        lines |> List.map joinLine |> String.concat Environment.NewLine

    /// Evaluate all the snippets in a literate document, returning the results.
    /// The result is a map of string * bool to FsiEvaluationResult. The bool indicates
    /// whether the result is a top level variable (i.e. include-value) or a reference to
    /// some output (i.e. define-output and include-output). This just to put each of those
    /// names in a separate scope.
    let rec evalBlocks
        (ctx: CompilerContext)
        (fsi: IFsiEvaluator)
        executionCountRef
        file
        acc
        (paras: MarkdownParagraphs)
        =
        match paras with
        | MarkdownPatterns.LiterateParagraph(para) :: paras ->

            // Do not evaluate blocks that don't match the conditional define, typically "condition: eval" or
            // "condition: formatting".
            //
            // None of the "output" conditions ("ipynb", "tex", "fsx", "html") will be defined at this point.
            match para.ParagraphOptions with
            | { Condition = Some define } when not (ctx.ConditionalDefines |> List.contains define) ->
                evalBlocks ctx fsi executionCountRef file acc paras
            | _ ->
                match para with
                | LiterateCode(snip, opts, _popts) ->
                    let acc =
                        if opts.Evaluate then
                            let text = unparse snip
                            let result = fsi.Evaluate(text, false, Some file)
                            incr executionCountRef
                            let executionCount = executionCountRef.Value

                            (OutputRef opts.OutputName, (result, executionCount)) :: acc
                        else
                            acc

                    evalBlocks ctx fsi executionCountRef file acc paras

                | ValueReference(ref, _popts) ->
                    let result = fsi.Evaluate(ref, true, Some file)
                    incr executionCountRef
                    let executionCount = executionCountRef.Value

                    let acc = (ValueRef ref, (result, executionCount)) :: acc

                    evalBlocks ctx fsi executionCountRef file acc paras
                | _ -> evalBlocks ctx fsi executionCountRef file acc paras
        | _para :: paras -> evalBlocks ctx fsi executionCountRef file acc paras
        | [] -> acc

    /// Given an evaluator and document, evaluate all code snippets and return a map with
    /// their results - the key is ValueRef(name) for all value references and
    /// OutputRef(name) for all references to the snippet console output
    let evalAllSnippets ctx fsi (doc: LiterateDocument) =
        evalBlocks ctx fsi (ref 0) doc.SourceFile [] doc.Paragraphs |> Map.ofList


    /// Replace evaluation references with the results
    let rec replaceEvaluations (ctx: CompilerContext) (results: Map<_, IFsiEvaluationResult * int>) para =
        match para with
        | MarkdownPatterns.LiterateParagraph(special) ->
            match special with
            | FsiMergedOutputReference(ref, _popts)
            | FsiOutputReference(ref, _popts)
            | OutputReference(ref, _popts)
            | ItValueReference(ref, _popts)
            | ItRawReference(ref, _popts)
            | ValueReference(ref, _popts) ->
                let key =
                    (match special with
                     | ValueReference _ -> ValueRef ref
                     | _ -> OutputRef ref)

                match results.TryFind(key), ctx.Evaluator with
                | Some(result, executionCount), Some evaluator ->
                    let kind =
                        match special with
                        | FsiMergedOutputReference _ -> FsiEmbedKind.FsiMergedOutput
                        | FsiOutputReference _ -> FsiEmbedKind.FsiOutput
                        | OutputReference _ -> FsiEmbedKind.ConsoleOutput
                        | ItValueReference _ -> FsiEmbedKind.ItValue
                        | ItRawReference _ -> FsiEmbedKind.ItRaw
                        | ValueReference _ -> FsiEmbedKind.Value
                        | _ -> failwith "unreachable"

                    evaluator.Format(result, kind, executionCount)
                | _ ->
                    let output = "Could not find reference '" + ref + "'"
                    [ OutputBlock(output, "text/plain", None) ]

            | LiterateCode(lines, opts, popts) when results.ContainsKey(OutputRef opts.OutputName) ->
                let _, executionCount = results.[OutputRef opts.OutputName]

                let opts =
                    { opts with
                        ExecutionCount = Some executionCount }

                [ EmbedParagraphs(LiterateCode(lines, opts, popts), None) ]
            | _ -> [ EmbedParagraphs(special, None) ]

        // Traverse all other structrues recursively
        | MarkdownPatterns.ParagraphNested(pn, nested) ->
            let nested = List.map (List.collect (replaceEvaluations ctx results)) nested

            [ MarkdownPatterns.ParagraphNested(pn, nested) ]
        | par -> [ par ]

    /// Transform the specified literate document & evaluate all F# snippets
    let evaluateCodeSnippets ctx (doc: LiterateDocument) =
        match ctx.Evaluator with
        | Some fsi ->
            let evaluationResults = evalAllSnippets ctx fsi doc

            let newParagraphs = List.collect (replaceEvaluations ctx evaluationResults) doc.Paragraphs

            doc.With(paragraphs = newParagraphs)
        | None -> doc

    // ----------------------------------------------------------------------------------------------
    // Replace all special 'LiterateParagraph' elements with ordinary HTML/Latex
    // ----------------------------------------------------------------------------------------------

    /// Collect all code snippets in the document (so that we can format all of them)
    /// The resulting dictionary has Choice as the key, so that we can distinguish
    /// between moved snippets and ordinary snippets
    let rec collectLiterateCode par =
        [ match par with
          | MarkdownPatterns.LiterateParagraph(para) ->
              //// Remove "condition: ipynb" etc. from output unless the condition is satisfied
              //match para.ParagraphOptions with
              //| { Condition=Some define } when define <> "prepare" -> ()
              //| _ ->
              match para with
              | LiterateCode(lines, ({ Visibility = LiterateCodeVisibility.NamedCode id } as opts), _popts) ->
                  yield Choice2Of2(id), (lines, opts.ExecutionCount)
              | LiterateCode(lines, opts, _popts) -> yield Choice1Of2(lines), (lines, opts.ExecutionCount)
              | _ -> ()
          | MarkdownPatterns.ParagraphNested(_pn, nested) ->
              for ps in nested do
                  for p in ps do
                      yield! collectLiterateCode p
          | _ -> () ]


    /// Replace all special 'LiterateParagraph' elements recursively using the given lookup dictionary
    let replaceHtmlTaggedCode (ctx: LiterateProcessingContext) (lang: string) (code: string) =
        let sb = new System.Text.StringBuilder()
        let writer = new System.IO.StringWriter(sb)
        writer.Write("<table class=\"pre\">")
        writer.Write("<tr>")

        if ctx.GenerateLineNumbers then
            // Split the formatted code into lines & emit line numbers in <td>
            // (Similar to formatSnippets in FSharp.Formatting.CodeFormat\HtmlFormatting.fs)
            let lines =
                code.Trim('\r', '\n').Replace("\r\n", "\n").Replace("\n\r", "\n").Replace("\r", "\n").Split('\n')

            let numberLength = lines.Length.ToString().Length
            let linesLength = lines.Length
            writer.Write("<td class=\"lines\"><pre class=\"fssnip\">")

            for index in 0 .. linesLength - 1 do
                let lineStr = (index + 1).ToString().PadLeft(numberLength)

                writer.WriteLine("<span class=\"l\">{0}: </span>", lineStr)

            writer.Write("</pre>")
            writer.WriteLine("</td>")

        writer.Write("<td class=\"snippet\">")

        match SyntaxHighlighter.FormatCode(lang, code) with
        | true, code ->
            Printf.fprintf writer "<pre class=\"fssnip highlighted\"><code lang=\"%s\">%s</code></pre>" lang code
        | false, code -> Printf.fprintf writer "<pre class=\"fssnip\"><code lang=\"%s\">%s</code></pre>" lang code

        writer.Write("</td></tr></table>")
        sb.ToString()

    /// Replace all special 'LiterateParagraph' elements recursively using the given lookup dictionary
    let rec replaceLiterateParagraph (ctx: LiterateProcessingContext) (formatted: IDictionary<_, _>) para =
        match para with
        | MarkdownPatterns.LiterateParagraph(special) ->
            // Remove "condition: ipynb" etc. from output unless the condition is satisfied
            match special.ParagraphOptions with
            | { Condition = Some define } when not (ctx.ConditionalDefines |> List.contains define) -> None
            | _ ->
                // Remove "(** hide ***)" from output unless the condition is satisfied
                match special with
                | LiterateCode(_, { Visibility = LiterateCodeVisibility.HiddenCode }, _) -> None
                | _ ->
                    // Remove "(** define: name ***)" from output, they should be referenced elsewhere
                    match special with
                    | LiterateCode(_, { Visibility = LiterateCodeVisibility.NamedCode _ }, _) -> None
                    | _ ->
                        match special with
                        | RawBlock(lines, _) -> Some(InlineHtmlBlock(unparse lines, None, None))
                        | LiterateCode(lines, _, _) -> Some(formatted.[Choice1Of2 lines])
                        | CodeReference(ref, _) ->
                            match formatted.TryGetValue(Choice2Of2 ref) with
                            | true, v -> Some v
                            | false, _ ->
                                failwithf
                                    "Could not find named code snippet '%s'. Check that it is defined with '(** define:%s ***)'."
                                    ref
                                    ref
                        | FsiMergedOutputReference _
                        | FsiOutputReference _
                        | OutputReference _
                        | ItValueReference _
                        | ItRawReference _
                        | ValueReference _ ->
                            let msg = "Warning: Output, it-value and value references require --eval"

                            printfn "%s" msg
                            Some(InlineHtmlBlock(msg, None, None))
                        | LanguageTaggedCode(lang, code, _) ->
                            let inlined =
                                match ctx.OutputKind with
                                | OutputKind.Html -> replaceHtmlTaggedCode ctx lang code
                                | OutputKind.Latex -> sprintf "\\begin{lstlisting}\n%s\n\\end{lstlisting}" code
                                | OutputKind.Pynb -> code
                                | OutputKind.Fsx -> code
                                | OutputKind.Markdown -> code

                            Some(InlineHtmlBlock(inlined, None, None))
        // Traverse all other structures recursively
        | MarkdownPatterns.ParagraphNested(pn, nested) ->
            let nested = List.map (List.choose (replaceLiterateParagraph ctx formatted)) nested

            Some(MarkdownPatterns.ParagraphNested(pn, nested))
        | par -> Some par

    /// Replace all special 'LiterateParagraph' elements with ordinary HTML/Latex
    let replaceLiterateParagraphs ctx (doc: LiterateDocument) =
        let codes = doc.Paragraphs |> List.collect collectLiterateCode

        // Strip #if SYMBOL / #endif // SYMBOL marker lines from code before syntax-highlighting,
        // so that they don't appear in the formatted output.
        let markerLines =
            ctx.ConditionalDefines
            |> List.collect (fun sym -> [ sprintf "#if %s" sym; sprintf "#endif // %s" sym ])
            |> Set.ofList

        let stripDefineLines (lines: Line list) =
            lines
            |> List.filter (fun (Line(originalLine, _)) -> not (markerLines.Contains(originalLine.Trim())))

        let snippets = [| for _, (lines, _) in codes -> Snippet("", stripDefineLines lines) |]

        // Format all snippets and build lookup dictionary for parameters
        let formatted =
            match ctx.OutputKind with
            | OutputKind.Html ->
                let openTag = "<pre class=\"fssnip highlighted\"><code lang=\"fsharp\">"

                let closeTag = "</code></pre>"
                let openLinesTag = "<pre class=\"fssnip\">"
                let closeLinesTag = "</pre>"

                CodeFormat.FormatHtml(
                    snippets,
                    ctx.Prefix,
                    addErrors = false,
                    openTag = openTag,
                    closeTag = closeTag,
                    openLinesTag = openLinesTag,
                    closeLinesTag = closeLinesTag,
                    lineNumbers = ctx.GenerateLineNumbers,
                    ?tokenKindToCss = ctx.TokenKindToCss
                )
            | OutputKind.Latex ->
                CodeFormat.FormatLatex(snippets, lineNumbers = ctx.GenerateLineNumbers, openTag = "", closeTag = "")
            | OutputKind.Pynb -> CodeFormat.FormatFsx(snippets)
            | OutputKind.Fsx -> CodeFormat.FormatFsx(snippets)
            | OutputKind.Markdown -> CodeFormat.FormatFsx(snippets)

        let lookup =
            [ for (key, (_, executionCount)), fmtd in Seq.zip codes formatted.Snippets ->
                  let block =
                      match ctx.OutputKind with
                      | OutputKind.Html -> InlineHtmlBlock(fmtd.Content, executionCount, None)
                      | OutputKind.Fsx
                      | OutputKind.Markdown
                      | OutputKind.Latex
                      | OutputKind.Pynb ->
                          CodeBlock(
                              code = fmtd.Content,
                              executionCount = executionCount,
                              fence = Some "```",
                              language = "fsharp",
                              ignoredLine = "",
                              range = None
                          )

                  key, block ]
            |> dict

        // Replace original snippets with formatted HTML/Latex and return document
        let newParagraphs = doc.Paragraphs |> List.choose (replaceLiterateParagraph ctx lookup)

        doc.With(paragraphs = newParagraphs, formattedTips = formatted.ToolTip)
