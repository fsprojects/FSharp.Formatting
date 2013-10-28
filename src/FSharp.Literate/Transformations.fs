namespace FSharp.Literate

open System
open System.IO
open System.Web
open System.Reflection
open System.Collections.Generic

open FSharp.Patterns
open FSharp.CodeFormat
open FSharp.Markdown

module Transformations = 
  // ----------------------------------------------------------------------------------------------
  // Replace all code snippets (assume F#) with their nicely formatted versions
  // ----------------------------------------------------------------------------------------------

  /// Iterate over Markdown document and extract all F# code snippets that we want
  /// to colorize. We skip snippets that specify non-fsharp langauge e.g. [lang=csharp].
  let rec private collectCodeSnippets par = seq {
    match par with
    | CodeBlock(String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.TrimStart code)) 
        when cmds.ContainsKey("lang") && cmds.["lang"] <> "fsharp" -> ()
    | CodeBlock(String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.TrimStart code)) 
    | CodeBlock(Let (dict []) (cmds, code)) ->
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
  let rec private replaceCodeSnippets (codeLookup:IDictionary<_, _>) = function
    | CodeBlock(String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.TrimStart code)) 
        when cmds.ContainsKey("hide") -> None
    | CodeBlock(String.StartsWithWrapped ("[", "]") (ParseCommands cmds, String.TrimStart code)) 
    | CodeBlock(Let (dict []) (cmds, code)) ->
        if (cmds.ContainsKey("lang")) && cmds.["lang"] <> "fsharp" then 
          Some (EmbedParagraphs(LanguageTaggedCode(cmds.["lang"], code)))
        else
          Some (EmbedParagraphs(FormattedCode(codeLookup.[code])))

    // Recursively process nested paragraphs, other nodes return without change
    | Matching.ParagraphNested(pn, nested) ->
        let pars = List.map (List.choose (replaceCodeSnippets codeLookup)) nested
        Matching.ParagraphNested(pn, pars) |> Some
    | other -> Some other


  /// Walk over literate document and replace F# code snippets with 
  /// their formatted representation (of `LiterateParagraph` type)
  let formatCodeSnippets path ctx (doc:LiterateDocument) =
    let name = Path.GetFileNameWithoutExtension(path)

    // Extract all CodeBlocks and pass them to F# snippets
    let codes = doc.Paragraphs |> Seq.collect collectCodeSnippets |> Array.ofSeq
    if codes.Length = 0 then doc else

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
    let snippetLookup = 
      [ for (_, code), (Snippet(_, fs)) in Array.zip codes snippets -> 
          code, fs ] |> dict
    
    // Replace code blocks with formatted snippets in the document
    let newPars = doc.Paragraphs |> List.choose (replaceCodeSnippets snippetLookup)
    doc.With(paragraphs = newPars, errors = Seq.append doc.Errors errors)

  // ----------------------------------------------------------------------------------------------
  // Generate references from indirect links
  // ----------------------------------------------------------------------------------------------

  /// Given Markdown document, get the keys of all IndirectLinks 
  /// (to be used when generating paragraph with all references)
  let rec private collectReferences = 
    // Collect IndirectLinks in a span
    let rec collectSpanReferences span = seq { 
      match span with
      | IndirectLink(_, _, key) -> yield key
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
  let private replaceReferences (refIndex:IDictionary<string, int>) =
    // Replace IndirectLinks with a nice link given a single span element
    let rec replaceSpans = function
      | IndirectLink(body, original, key) ->
          [ yield IndirectLink(body, original, key)
            match refIndex.TryGetValue(key) with
            | true, i -> 
                yield Literal "&#160;["
                yield DirectLink([Literal (string i)], ("#rf" + DateTime.Now.ToString("yyMMddhh"), None))
                yield Literal "]"
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
  let private generateRefParagraphs (definedLinks:IDictionary<_, string * string option>) refs =     
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
            yield [Span [ Literal (sprintf "[%d] " i)
                          DirectLink([Literal (name.Trim())], (link, Some title))
                          Literal (" - " + auth)] ] 
          else
            yield [Span [ Literal (sprintf "[%d] " i)
                          DirectLink([Literal title], (link, Some title))]]  ]

    // Return the document together with dictionary for looking up indices
    let id = DateTime.Now.ToString("yyMMddhh")
    [ Paragraph [AnchorLink id];
      Heading(3, [Literal "References"])
      ListBlock(MarkdownListKind.Unordered, refList) ], refLookup

  /// Turn all indirect links into a references 
  /// and add paragraph to the document
  let generateReferences (doc:LiterateDocument) =
    let refs = doc.Paragraphs |> Seq.collect collectReferences
    let refPars, refLookup = generateRefParagraphs doc.DefinedLinks refs 
    let newDoc = doc.Paragraphs |> List.choose (replaceReferences refLookup)
    doc.With(paragraphs = newDoc @ refPars)

  // ----------------------------------------------------------------------------------------------
  // Replace all special 'LiterateParagraph' elements with ordinary HTML/Latex
  // ----------------------------------------------------------------------------------------------

  /// Collect all code snippets in the document (so that we can format all of them)
  /// The resulting dictionary has Choice as the key, so that we can distinguish 
  /// between moved snippets and ordinary snippets
  let rec private collectCodes par = seq {
    match par with 
    | Matching.LiterateParagraph(HiddenCode(Some id, lines)) -> 
        yield Choice2Of2(id), lines
    | Matching.LiterateParagraph(FormattedCode(lines)) -> 
        yield Choice1Of2(lines), lines
    | Matching.ParagraphNested(pn, nested) ->
        yield! Seq.collect (Seq.collect collectCodes) nested
    | _ -> () }


  /// Replace all special 'LiterateParagraph' elements recursively using the given lookup dictionary
  let rec private replaceSpecialCodes ctx (formatted:IDictionary<_, _>) = function
    | Matching.LiterateParagraph(special) -> 
        match special with
        | HiddenCode _ -> None
        | CodeReference ref -> Some (formatted.[Choice2Of2 ref])
        | FormattedCode lines -> Some (formatted.[Choice1Of2 lines])
        | LanguageTaggedCode(lang, code) -> 
            let inlined = 
              match ctx.OutputKind with
              | OutputKind.Html ->
                  sprintf "<pre lang=\"%s\">%s</pre>" lang (HttpUtility.HtmlEncode code)
              | OutputKind.Latex ->
                  sprintf "\\begin{lstlisting}\n%s\n\\end{lstlisting}" code
            Some(InlineBlock(inlined))
    // Traverse all other structrues recursively
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
      | OutputKind.Html -> CodeFormat.FormatHtml(snippets, ctx.Prefix, ctx.GenerateLineNumbers, false)
      | OutputKind.Latex -> CodeFormat.FormatLatex(snippets, ctx.GenerateLineNumbers)
    let lookup = 
      [ for (key, _), fmtd in Seq.zip replacements formatted.Snippets -> 
          key, InlineBlock(fmtd.Content) ] |> dict 

    // Replace original snippets with formatted HTML/Latex and return document
    let newParagraphs = List.choose (replaceSpecialCodes ctx lookup) doc.Paragraphs
    doc.With(paragraphs = newParagraphs, formattedTips = formatted.ToolTip)
