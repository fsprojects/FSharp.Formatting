namespace FSharp.Formatting.Literate

open System
open System.IO
open System.Text.RegularExpressions
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
            let sb = System.Text.StringBuilder()
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
            | Heading(1, text, r) ->
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
                [ for Snippet(name, lines) in snippets do
                      if snippets.Length > 1 then
                          yield Heading(3, [ Literal(name, None) ], None)

                      let id =
                          count <- count + 1
                          "cell" + string<int> count

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
            [ for Snippet(_name, lines) in snippets do
                  for Line(line, _) in lines do
                      yield line ]
            |> String.concat "\n"

    let transformDocument
        // This array was sorted in BuildCommand.fs
        (filesWithFrontMatter: FrontMatterFile array)
        (doc: LiterateDocument)
        (outputPath: string)
        ctx
        =

        let findInFrontMatter key =
            match doc.Paragraphs with
            | YamlFrontmatter(lines, _) :: _ ->
                lines
                |> List.tryPick (fun line ->
                    let line = line.Trim()

                    if line.StartsWith(key + ":", StringComparison.Ordinal) then
                        let line = line.[(key + ":").Length ..]
                        let line = line.Trim()
                        Some line
                    else
                        None)
            | _ -> None

        let mkValidIndex (value: string) =
            match System.Int32.TryParse value with
            | true, i -> Some i
            | false, _ -> None

        let category = findInFrontMatter "category"
        let categoryIndex = findInFrontMatter "categoryindex" |> Option.bind mkValidIndex
        let index = findInFrontMatter "index" |> Option.bind mkValidIndex
        let titleFromFrontMatter = findInFrontMatter "title"

        // If we want to include the source code of the script, then process
        // the entire source and generate replacement {source} => ...some html...
        let sourceSubstitutions =
            let relativeSourceFileName =
                match doc.RootInputFolder with
                | None -> Path.GetFileName(doc.SourceFile)
                | Some rootInputFolder ->
#if NETSTANDARD2_1_OR_GREATER
                    Path.GetRelativePath(rootInputFolder, doc.SourceFile)
#else
                    if
                        doc.SourceFile.StartsWith(rootInputFolder + string Path.DirectorySeparatorChar)
                        || doc.SourceFile.StartsWith(rootInputFolder + "/")
                        || doc.SourceFile.StartsWith(rootInputFolder + "\\")
                    then
                        doc.SourceFile.Substring(rootInputFolder.Length + 1)
                    else
                        failwith $"need to make {doc.SourceFile} relative to {rootInputFolder}"
#endif
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

        // construct previous and next urls
        let nextPreviousPageSubstitutions =
            let getLinksFromCurrentPageIdx currentPageIdx =
                match currentPageIdx with
                | None -> []
                | Some currentPageIdx ->
                    let previousPage =
                        filesWithFrontMatter
                        |> Array.tryItem (currentPageIdx - 1)
                        |> Option.bind (fun { FileName = fileName } ->
                            ctx.MarkdownDirectLinkResolver fileName
                            |> Option.map (fun link -> ParamKeys.``fsdocs-previous-page-link``, link))
                        |> Option.toList

                    let nextPage =
                        filesWithFrontMatter
                        |> Array.tryItem (currentPageIdx + 1)
                        |> Option.bind (fun { FileName = fileName } ->
                            ctx.MarkdownDirectLinkResolver fileName
                            |> Option.map (fun link -> ParamKeys.``fsdocs-next-page-link``, link))
                        |> Option.toList

                    previousPage @ nextPage

            match index, categoryIndex with
            | None, None
            | None, Some _ ->
                // Typical uses case here is the main index page.
                // If there is no frontmatter there, we want to propose the first available page
                filesWithFrontMatter
                |> Array.tryHead
                |> Option.bind (fun { FileName = fileName } ->
                    ctx.MarkdownDirectLinkResolver fileName
                    |> Option.map (fun link -> ParamKeys.``fsdocs-next-page-link``, link))
                |> Option.toList

            | Some currentPageIdx, None ->
                let currentPageIdx =
                    filesWithFrontMatter
                    |> Array.tryFindIndex (fun { Index = idx } -> idx = currentPageIdx)

                getLinksFromCurrentPageIdx currentPageIdx
            | Some currentPageIdx, Some currentCategoryIdx ->
                let currentPageIdx =
                    filesWithFrontMatter
                    |> Array.tryFindIndex (fun { Index = idx; CategoryIndex = cIdx } ->
                        cIdx = currentCategoryIdx && idx = currentPageIdx)

                getLinksFromCurrentPageIdx currentPageIdx

        let substitutions0 =
            [ yield ParamKeys.``fsdocs-page-title``, pageTitle
              yield ParamKeys.``fsdocs-page-source``, doc.SourceFile
              yield ParamKeys.``fsdocs-body-class``, "content"
              yield! ctx.Substitutions
              yield! sourceSubstitutions
              yield! nextPreviousPageSubstitutions ]



        let formattedDocument =
            format
                doc.MarkdownDocument
                ctx.GenerateHeaderAnchors
                ctx.OutputKind
                substitutions0
                ctx.CodeReferenceResolver
                ctx.MarkdownDirectLinkResolver

        let headingTexts, pageHeaders = FSharp.Formatting.Common.PageContentList.mkPageContentMenu formattedDocument

        let tipsHtml = doc.FormattedTips

        // Construct new Markdown document and write it
        let substitutions =
            substitutions0
            @ [ ParamKeys.``fsdocs-content``, formattedDocument
                ParamKeys.``fsdocs-tooltips``, tipsHtml
                ParamKeys.``fsdocs-page-content-list``, pageHeaders ]

        let indexText =
            (match ctx.OutputKind with
             | OutputKind.Html ->
                 // Strip the html tags
                 let fullText = Regex.Replace(formattedDocument, "<.*?>", "")
                 Some(IndexText(fullText, headingTexts))
             | _ -> None)

        { OutputPath = outputPath
          OutputKind = ctx.OutputKind
          Title = pageTitle
          Category = category
          CategoryIndex = categoryIndex
          Index = index
          IndexText = indexText
          Substitutions = substitutions
          // No don't know this until later.
          // See DocContent.GetNavigationEntries
          IsActive = false }
