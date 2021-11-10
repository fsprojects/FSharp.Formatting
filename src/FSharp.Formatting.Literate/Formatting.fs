namespace FSharp.Formatting.Literate

open System.IO
open FSharp.Formatting.Literate
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Markdown
open FSharp.Formatting.Templating

module internal Formatting =

    /// Format document with the specified output kind
    let format (doc: MarkdownDocument) generateAnchors outputKind substitutions crefResolver mdlinkResolver =
        match outputKind with
        | OutputKind.Fsx ->
            Markdown.ToFsx(
                doc,
                substitutions = substitutions,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver
            )
        | OutputKind.Markdown ->
            Markdown.ToMd(
                doc,
                substitutions = substitutions,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver
            )
        | OutputKind.Pynb ->
            Markdown.ToPynb(
                doc,
                substitutions = substitutions,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver
            )
        | OutputKind.Latex ->
            Markdown.ToLatex(
                doc,
                substitutions = substitutions,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver
            )
        | OutputKind.Html ->
            let sb = new System.Text.StringBuilder()
            use wr = new StringWriter(sb)

            HtmlFormatting.formatAsHtml
                wr
                generateAnchors
                true
                doc.DefinedLinks
                substitutions
                System.Environment.NewLine
                crefResolver
                mdlinkResolver
                doc.Paragraphs

            sb.ToString()

    /// Try find first-level heading in the paragraph collection
    let findHeadings paragraphs generateAnchors (outputKind: OutputKind) =
        paragraphs
        |> Seq.tryPick (fun para ->
            match para with
            | Heading (1, text, r) ->
                match outputKind with
                | OutputKind.Html
                | OutputKind.Latex ->
                    let doc = MarkdownDocument([ Span(text, r) ], dict [])

                    Some(format doc generateAnchors outputKind [] (fun _ -> None) (fun _ -> None))
                | _ -> None
            | _ -> None)

    /// Given literate document, get a new MarkdownDocument that represents the
    /// entire source code of the specified document (with possible fsx formatting)
    let getSourceDocument (doc: LiterateDocument) =
        match doc.Source with
        | LiterateSource.Markdown text -> doc.With(paragraphs = [ CodeBlock(text, None, None, "", "", None) ])
        | LiterateSource.Script snippets ->
            let mutable count = 0

            let paragraphs =
                [ for Snippet (name, lines) in snippets do
                      if snippets.Length > 1 then
                          yield Heading(3, [ Literal(name, None) ], None)

                      let id =
                          count <- count + 1
                          "cell" + string count

                      let opts =
                          { Evaluate = true
                            ExecutionCount = None
                            OutputName = id
                            Visibility = LiterateCodeVisibility.VisibleCode }

                      let popts = { Condition = None }
                      yield EmbedParagraphs(LiterateCode(lines, opts, popts), None) ]

            doc.With(paragraphs = paragraphs)

    /// Given literate document, get the text for indexing for search
    let getIndexText (doc: LiterateDocument) =
        match doc.Source with
        | LiterateSource.Markdown text -> text
        | LiterateSource.Script snippets ->
            [ for Snippet (_name, lines) in snippets do
                  for (Line (line, _)) in lines do
                      yield line ]
            |> String.concat "\n"

    let transformDocument (doc: LiterateDocument) (outputPath: string) ctx =

        let findInFrontMatter key =
            match doc.Paragraphs with
            | YamlFrontmatter (lines, _) :: _ ->
                lines
                |> List.tryPick (fun line ->
                    let line = line.Trim()

                    if line.StartsWith(key + ":") then
                        let line = line.[(key + ":").Length ..]
                        let line = line.Trim()
                        Some line
                    else
                        None)
            | _ -> None

        let category = findInFrontMatter "category"

        let categoryIndex = findInFrontMatter "categoryindex"
        let index = findInFrontMatter "index"
        let titleFromFrontMatter = findInFrontMatter "title"

        // If we want to include the source code of the script, then process
        // the entire source and generate replacement {source} => ...some html...
        let sourceSubstitutions =
            let relativeSourceFileName =
                match doc.RootInputFolder with
                | None -> Path.GetFileName(doc.SourceFile)
                | Some rootInputFolder -> Path.GetRelativePath(rootInputFolder, doc.SourceFile)

            let relativeSourceFileBaseName = Path.ChangeExtension(relativeSourceFileName, null)

            let relativeSourceFileName = relativeSourceFileName.Replace(@"\", "/")

            let relativeSourceFileBaseName = relativeSourceFileBaseName.Replace(@"\", "/")

            let doc = getSourceDocument doc |> Transformations.replaceLiterateParagraphs ctx

            let source =
                format doc.MarkdownDocument ctx.GenerateHeaderAnchors ctx.OutputKind [] (fun _ -> None) (fun _ -> None)

            [ ParamKeys.``fsdocs-source-filename``, relativeSourceFileName
              ParamKeys.``fsdocs-source-basename``, relativeSourceFileBaseName
              ParamKeys.``fsdocs-source``, source ]

        // Get page title (either heading or file name)
        let pageTitle =
            match titleFromFrontMatter with
            | Some text -> text
            | _ ->
                let name = Path.GetFileNameWithoutExtension(outputPath)

                defaultArg (findHeadings doc.Paragraphs ctx.GenerateHeaderAnchors ctx.OutputKind) name

        // Replace all special elements with ordinary Html/Latex Markdown
        let doc = Transformations.replaceLiterateParagraphs ctx doc

        let substitutions0 =
            [ ParamKeys.``fsdocs-page-title``, pageTitle; ParamKeys.``fsdocs-page-source``, doc.SourceFile ]
            @ ctx.Substitutions @ sourceSubstitutions

        let formattedDocument =
            format
                doc.MarkdownDocument
                ctx.GenerateHeaderAnchors
                ctx.OutputKind
                substitutions0
                ctx.CodeReferenceResolver
                ctx.MarkdownDirectLinkResolver

        let tipsHtml = doc.FormattedTips

        // Construct new Markdown document and write it
        let substitutions =
            substitutions0
            @ [ ParamKeys.``fsdocs-content``, formattedDocument; ParamKeys.``fsdocs-tooltips``, tipsHtml ]

        let indexText =
            (match ctx.OutputKind with
             | OutputKind.Html -> Some(getIndexText doc)
             | _ -> None)

        { OutputPath = outputPath
          OutputKind = ctx.OutputKind
          Title = pageTitle
          Category = category
          CategoryIndex = categoryIndex
          Index = index
          IndexText = indexText
          Substitutions = substitutions }
