namespace FSharp.Literate

open System
open System.IO
open System.Web
open System.Reflection
open System.Collections.Generic

open CSharpFormat
open FSharp.Patterns
open FSharp.CodeFormat
open FSharp.Markdown

/// [omit]
module Transformations =
  // ----------------------------------------------------------------------------------------------
  // Replace all code snippets (assume F#) with their nicely formatted versions
  // ----------------------------------------------------------------------------------------------

  /// Iterate over Markdown document and extract all F# code snippets that we want
  /// to colorize. We skip snippets that specify non-fsharp langauge e.g. [lang=csharp].
  let rec collectCodeSnippets par = seq {
    match par with
    | CodeBlock((String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.SkipSingleLine code)), language, _, _)
        when (not (String.IsNullOrWhiteSpace(language)) && language <> "fsharp") || (cmds.ContainsKey("lang") && cmds.["lang"] <> "fsharp") -> ()
    | CodeBlock((String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.SkipSingleLine code)), _, _, _)
    | CodeBlock(Let (dict []) (cmds, code), _, _, _) ->
        let modul =
          match cmds.TryGetValue("module") with
          | true, v -> Some v | _ -> None
        yield modul, code
    | Matching.ParagraphLeaf _ -> ()
    | Matching.ParagraphNested(_, pars) ->
        for ps in pars do
          for p in ps do yield! collectCodeSnippets p
    | Matching.ParagraphSpans(_, spans) -> () }


  /// Replace CodeBlock elements with formatted HTML that was processed by the F# snippets tool
  /// (The dictionary argument is a map from original code snippets to formatted HTML snippets.)
  let rec replaceCodeSnippets path (codeLookup:IDictionary<_, _>) = function
    | CodeBlock ((String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.SkipSingleLine code)), language, _, r)
    | CodeBlock(Let (dict []) (cmds, code), language, _, r) ->
        if cmds.ContainsKey("hide") then None else
        let code =
          if cmds.ContainsKey("file") && cmds.ContainsKey("key") then
            // Get snippet from an external file
            let file = Path.Combine(Path.GetDirectoryName(path), cmds.["file"])
            let startTag, endTag = "[" + cmds.["key"] + "]", "[/" + cmds.["key"] + "]"
            let lines = File.ReadAllLines(file)
            let startIdx = lines |> Seq.findIndex (fun l -> l.Contains startTag)
            let endIdx = lines |> Seq.findIndex (fun l -> l.Contains endTag)
            lines.[startIdx + 1 .. endIdx - 1]
            |> String.removeSpaces
            |> String.concat "\n"
          else code
        let lang =
          match language with
          | String.WhiteSpace when cmds.ContainsKey("lang") -> cmds.["lang"]
          | language -> language
        if not (String.IsNullOrWhiteSpace(lang)) && lang <> "fsharp" then
          Some (EmbedParagraphs(LanguageTaggedCode(lang, code), r))
        else
          Some (EmbedParagraphs(FormattedCode(codeLookup.[code]), r))

    // Recursively process nested paragraphs, other nodes return without change
    | Matching.ParagraphNested(pn, nested) ->
        let pars = List.map (List.choose (replaceCodeSnippets path codeLookup)) nested
        Matching.ParagraphNested(pn, pars) |> Some
    | other -> Some other


  /// Walk over literate document and replace F# code snippets with
  /// their formatted representation (of `LiterateParagraph` type)
  let formatCodeSnippets path ctx (doc:LiterateDocument) =
    let name = Path.GetFileNameWithoutExtension(path)

    // Extract all CodeBlocks and pass them to F# snippets
    let codes = doc.Paragraphs |> Seq.collect collectCodeSnippets |> Array.ofSeq
    let snippetLookup, errors =
      if codes.Length = 0 then dict [], [||] else
        // If there are some F# snippets, we build an F# source file
        let blocks = codes |> Seq.mapi (fun index -> function
          | Some modul, code ->
              // Generate module & add indentation
              "module " + modul + " =\n" +
              "// [snippet:" + (string index) + "]\n" +
              "    " + code.Replace("\n", "\n    ") + "\n" +
              "// [/snippet]"
          | None, code ->
              "// [snippet:" + (string index) + "]\n" +
              code + "\n" +
              "// [/snippet]" )
        let modul = "module " + (new String(name |> Seq.filter Char.IsLetter |> Seq.toArray))
        let source = modul + "\r\n" + (String.concat "\n\n" blocks)

        // Process F# script file & build lookup table for replacement
        let snippets, errors =
          ctx.FormatAgent.ParseSource
            ( Path.ChangeExtension(path, ".fsx"), source,
              ?options = ctx.CompilerOptions, ?defines = ctx.DefinedSymbols )
        [ for (_, code), (Snippet(_, fs)) in Array.zip codes snippets ->
            code, fs ] |> dict, errors

    // Replace code blocks with formatted snippets in the document
    let newPars = doc.Paragraphs |> List.choose (replaceCodeSnippets path snippetLookup)
    doc.With(paragraphs = newPars, errors = Seq.append doc.Errors errors)

  // ----------------------------------------------------------------------------------------------
  // Generate references from indirect links
  // ----------------------------------------------------------------------------------------------

  /// Given Markdown document, get the keys of all IndirectLinks
  /// (to be used when generating paragraph with all references)
  let rec collectReferences =
    // Collect IndirectLinks in a span
    let rec collectSpanReferences span = seq {
      match span with
      | IndirectLink(_, _, key, _) -> yield key
      | Matching.SpanLeaf _ -> ()
      | Matching.SpanNode(_, spans) ->
          for s in spans do yield! collectSpanReferences s }
    // Collect IndirectLinks in a paragraph
    let rec loop par = seq {
      match par with
      | Matching.ParagraphLeaf _ -> ()
      | Matching.ParagraphNested(_, pars) ->
          for ps in pars do
            for p in ps do yield! loop p
      | Matching.ParagraphSpans(_, spans) ->
          for s in spans do yield! collectSpanReferences s }
    loop


  /// Given Markdown document, add a number using the given index to all indirect
  /// references. For example, [article][ref] becomes [article][ref] [1](#rfxyz)
  let replaceReferences (refIndex:IDictionary<string, int>) =
    // Replace IndirectLinks with a nice link given a single span element
    let rec replaceSpans = function
      | IndirectLink(body, original, key, r) ->
          [ yield IndirectLink(body, original, key, r)
            match refIndex.TryGetValue(key) with
            | true, i ->
                yield Literal("&#160;[", r)
                yield DirectLink([Literal (string i, r)], "#rf" + DateTime.Now.ToString("yyMMddhh"), None, r)
                yield Literal("]", r)
            | _ -> () ]
      | Matching.SpanLeaf(sl) -> [Matching.SpanLeaf(sl)]
      | Matching.SpanNode(nd, spans) ->
          [ Matching.SpanNode(nd, List.collect replaceSpans spans) ]
    // Given a paragraph, process it recursively and transform all spans
    let rec loop = function
      | Matching.ParagraphNested(pn, nested) ->
          Matching.ParagraphNested(pn, List.map (List.choose loop) nested) |> Some
      | Matching.ParagraphSpans(ps, spans) ->
          Matching.ParagraphSpans(ps, List.collect replaceSpans spans) |> Some
      | Matching.ParagraphLeaf(pl) -> Matching.ParagraphLeaf(pl) |> Some
    loop


  /// Given all links defined in the Markdown document and a list of all links
  /// that are accessed somewhere from the document, generate References paragraph
  let generateRefParagraphs (definedLinks:IDictionary<_, string * string option>) refs =
    // For all unique references in the document,
    // get the link & title from definitions
    let refs =
      refs |> set |> Seq.choose (fun ref ->
        match definedLinks.TryGetValue(ref) with
        | true, (link, Some title) -> Some (ref, link, title)
        | _ -> None)
      |> Seq.sort |> Seq.mapi (fun i v -> i+1, v)
    // Generate dictionary with a number for all references
    let refLookup = dict [ for (i, (r, _, _)) in refs -> r, i ]

    // Generate Markdown blocks paragraphs representing Reference <li> items
    let refList =
      [ for i, (ref, link, title) in refs do
          let colon = title.IndexOf(":")
          if colon > 0 then
            let auth = title.Substring(0, colon)
            let name = title.Substring(colon + 1, title.Length - 1 - colon)
            yield [Span([ Literal (sprintf "[%d] " i, None)
                          DirectLink([Literal (name.Trim(), None)], link, Some title, None)
                          Literal (" - " + auth, None)], None) ]
          else
            yield [Span([ Literal (sprintf "[%d] " i, None)
                          DirectLink([Literal(title, None)], link, Some title, None)], None)]  ]

    // Return the document together with dictionary for looking up indices
    let id = DateTime.Now.ToString("yyMMddhh")
    [ Paragraph([AnchorLink(id, None)], None)
      Heading(3, [Literal("References", None)], None)
      ListBlock(MarkdownListKind.Unordered, refList, None) ], refLookup

  /// Turn all indirect links into a references
  /// and add paragraph to the document
  let generateReferences (doc:LiterateDocument) =
    let refs = doc.Paragraphs |> Seq.collect collectReferences
    let refPars, refLookup = generateRefParagraphs doc.DefinedLinks refs
    let newDoc = doc.Paragraphs |> List.choose (replaceReferences refLookup)
    doc.With(paragraphs = newDoc @ refPars)

  // ----------------------------------------------------------------------------------------------
  // Transformation that collects evaluation results for F# snippets in the document
  // ----------------------------------------------------------------------------------------------

  /// Represents key in a dictionary with evaluation results
  type EvalKey = OutputRef of string | ValueRef of string

  /// Unparse a Line list to a string - for evaluation by fsi.
  let unparse (lines: Line list) =
    let joinLine (Line spans) =
      spans
      |> Seq.map (fun span -> match span with Token (_,s,_) -> s | Omitted (s1,s2) -> s2 | _ -> "")
      |> String.concat ""
    lines
    |> Seq.map joinLine
    |> String.concat "\n"

  /// Evaluate all the snippets in a literate document, returning the results.
  /// The result is a map of string * bool to FsiEvaluationResult. The bool indicates
  /// whether the result is a top level variable (i.e. include-value) or a reference to
  /// some output (i.e. define-output and include-output). This just to put each of those
  /// names in a separate scope.
  let rec evalBlocks (fsi:IFsiEvaluator) file acc (paras:MarkdownParagraphs) =
    match paras with
    | Matching.LiterateParagraph(para)::paras ->
      match para with
      | LiterateCode(snip, opts) ->
          let acc =
            if opts.Evaluate then
              let text = unparse snip
              let result = fsi.Evaluate(text, false, Some file)
              match opts.OutputName with
              | Some n -> (OutputRef n, result)::acc
              | _ -> acc
            else acc
          evalBlocks fsi file acc paras

      | FormattedCode(snip) ->
          // Need to eval because subsequent code might refer it, but we don't need result
          let text = unparse snip
          let result = fsi.Evaluate(text, false, Some file)
          evalBlocks fsi file acc paras

      | ValueReference(ref) ->
          let result = fsi.Evaluate(ref, true, Some file)
          evalBlocks fsi file ((ValueRef ref,result)::acc) paras
      | _ -> evalBlocks fsi file acc paras
    | para::paras -> evalBlocks fsi file acc paras
    | [] -> acc

  /// Given an evaluator and document, evaluate all code snippets and return a map with
  /// their results - the key is `ValueRef(name)` for all value references and
  /// `OutputRef(name)` for all references to the snippet console output
  let evalAllSnippets fsi (doc:LiterateDocument) =
    evalBlocks fsi doc.SourceFile [] doc.Paragraphs |> Map.ofList


  // ---------------------------------------------------------------------------------------------
  // Evaluate all snippets and replace evaluation references with the results
  // ---------------------------------------------------------------------------------------------

  let rec replaceEvaluations ctx (evaluationResults:Map<_, IFsiEvaluationResult>) = function
    | Matching.LiterateParagraph(special) ->
        let (|EvalFormat|_|) = function
          | OutputReference(ref) -> Some(evaluationResults.TryFind(OutputRef ref), ref, FsiEmbedKind.Output)
          | ItValueReference(ref) -> Some(evaluationResults.TryFind(OutputRef ref), ref, FsiEmbedKind.ItValue)
          | ValueReference(ref) -> Some(evaluationResults.TryFind(ValueRef ref), ref, FsiEmbedKind.Value)
          | _ -> None
        match special with
        | EvalFormat(Some result, _, kind) -> ctx.Evaluator.Value.Format(result, kind)
        | EvalFormat(None, ref, _) -> [ CodeBlock("Could not find reference '" + ref + "'", "", "", None) ]
        | other -> [ EmbedParagraphs(other, None) ]

    // Traverse all other structrues recursively
    | Matching.ParagraphNested(pn, nested) ->
        let nested = List.map (List.collect (replaceEvaluations ctx evaluationResults)) nested
        [ Matching.ParagraphNested(pn, nested) ]
    | par -> [ par ]

  /// Transform the specified literate document & evaluate all F# snippets
  let evaluateCodeSnippets ctx (doc:LiterateDocument) =
    match ctx.Evaluator with
    | Some fsi ->
        let evaluationResults = evalAllSnippets fsi doc
        let newParagraphs = List.collect (replaceEvaluations ctx evaluationResults) doc.Paragraphs
        doc.With(paragraphs = newParagraphs)
    | None -> doc

  // ----------------------------------------------------------------------------------------------
  // Replace all special 'LiterateParagraph' elements with ordinary HTML/Latex
  // ----------------------------------------------------------------------------------------------

  /// Collect all code snippets in the document (so that we can format all of them)
  /// The resulting dictionary has Choice as the key, so that we can distinguish
  /// between moved snippets and ordinary snippets
  let rec collectCodes par = seq {
    match par with
    | Matching.LiterateParagraph(LiterateCode(lines, { Visibility = NamedCode id })) ->
        yield Choice2Of2(id), lines
    | Matching.LiterateParagraph(LiterateCode(lines, _))
    | Matching.LiterateParagraph(FormattedCode(lines)) ->
        yield Choice1Of2(lines), lines
    | Matching.ParagraphNested(pn, nested) ->
        yield! Seq.collect (Seq.collect collectCodes) nested
    | _ -> () }


  /// Replace all special 'LiterateParagraph' elements recursively using the given lookup dictionary
  let rec replaceSpecialCodes ctx (formatted:IDictionary<_, _>) = function
    | Matching.LiterateParagraph(special) ->
        match special with
        | RawBlock lines -> Some (InlineBlock(unparse lines, None))
        | LiterateCode(_, { Visibility = (HiddenCode | NamedCode _) }) -> None
        | FormattedCode lines
        | LiterateCode(lines, _) -> Some (formatted.[Choice1Of2 lines])
        | CodeReference ref -> Some (formatted.[Choice2Of2 ref])
        | OutputReference _
        | ItValueReference _
        | ValueReference _ ->
            failwith "Output, it-value and value references should be replaced by FSI evaluator"
        | LanguageTaggedCode(lang, code) ->
            let inlined =
              match ctx.OutputKind with
              | OutputKind.Html ->
                  let sb = new System.Text.StringBuilder()
                  let writer = new System.IO.StringWriter(sb)
                  writer.Write("<table class=\"pre\">")
                  writer.Write("<tr>")
                  if ctx.GenerateLineNumbers then
                    // Split the formatted code into lines & emit line numbers in <td>
                    // (Similar to formatSnippets in FSharp.CodeFormat\HtmlFormatting.fs)
                    let lines = code.Trim('\r', '\n').Replace("\r\n", "\n").Replace("\n\r", "\n").Replace("\r", "\n").Split('\n')
                    let numberLength = lines.Length.ToString().Length
                    let linesLength = lines.Length
                    writer.Write("<td class=\"lines\"><pre class=\"fssnip\">")
                    for index in 0..linesLength-1 do
                      let lineStr = (index + 1).ToString().PadLeft(numberLength)
                      writer.WriteLine("<span class=\"l\">{0}: </span>", lineStr)
                    writer.Write("</pre>")
                    writer.WriteLine("</td>")

                  writer.Write("<td class=\"snippet\">")

                  match SyntaxHighlighter.FormatCode(lang, code) with
                  | true, code -> Printf.fprintf writer "<pre class=\"fssnip highlighted\"><code lang=\"%s\">%s</code></pre>" lang code
                  | false, code -> Printf.fprintf writer "<pre class=\"fssnip\"><code lang=\"%s\">%s</code></pre>" lang code

                  writer.Write("</td></tr></table>")
                  sb.ToString()

              | OutputKind.Latex ->
                  sprintf "\\begin{lstlisting}\n%s\n\\end{lstlisting}" code
            Some(InlineBlock(inlined, None))
    // Traverse all other structures recursively
    | Matching.ParagraphNested(pn, nested) ->
        let nested = List.map (List.choose (replaceSpecialCodes ctx formatted)) nested
        Some(Matching.ParagraphNested(pn, nested))
    | par -> Some par


  /// Replace all special 'LiterateParagraph' elements with ordinary HTML/Latex
  let replaceLiterateParagraphs ctx (doc:LiterateDocument) =
    let replacements = Seq.collect collectCodes doc.Paragraphs
    let snippets = [| for _, r in replacements -> Snippet("", r) |]

    // Format all snippets and build lookup dictionary for replacements
    let formatted =
      match ctx.OutputKind with
      | OutputKind.Html ->
          let openTag = "<pre class=\"fssnip highlighted\"><code lang=\"fsharp\">"
          let closeTag = "</code></pre>"
          let openLinesTag = "<pre class=\"fssnip\">"
          let closeLinesTag = "</pre>"
          CodeFormat.FormatHtml
            ( snippets, ctx.Prefix, openTag, closeTag,
              openLinesTag, closeLinesTag, ctx.GenerateLineNumbers, false, ?tokenKindToCss = ctx.TokenKindToCss)
      | OutputKind.Latex -> CodeFormat.FormatLatex(snippets, ctx.GenerateLineNumbers)
    let lookup =
      [ for (key, _), fmtd in Seq.zip replacements formatted.Snippets ->
          key, InlineBlock(fmtd.Content, None) ] |> dict

    // Replace original snippets with formatted HTML/Latex and return document
    let newParagraphs = List.choose (replaceSpecialCodes ctx lookup) doc.Paragraphs
    doc.With(paragraphs = newParagraphs, formattedTips = formatted.ToolTip)
