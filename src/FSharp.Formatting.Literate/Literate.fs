namespace FSharp.Formatting.Literate

open System
open System.Collections.Generic
open System.IO
open System.Runtime.CompilerServices
open FSharp.Formatting.Markdown
open FSharp.Formatting.Templating

/// <summary>
/// This type provides three simple methods for calling the literate programming tool.
/// The <c>ConvertMarkdownFile</c> and <c>ConvertScriptFile</c> methods process a single Markdown document
/// and F# script, respectively. The <c>ConvertDirectory</c> method handles an entire directory tree
/// (looking for <c>*.fsx</c> and <c>*.md</c> files).
/// </summary>
///
/// <namespacedoc>
///   <summary>Functionality to support literate programming for F# scripts</summary>
/// </namespacedoc>
type Literate private () =

    /// Build default options context for formatting literate document
    static let makeFormattingContext
        (outputKind: OutputKind)
        prefix
        lineNumbers
        generateAnchors
        substitutions
        crefResolver
        mdlinkResolver
        tokenKindToCss
        =
        let defines = [ outputKind.Extension; outputKind.Extension.ToUpperInvariant() ]

        { Substitutions = substitutions
          GenerateLineNumbers = defaultArg lineNumbers true
          Prefix = defaultArg prefix "fs"
          ConditionalDefines = defines
          OutputKind = outputKind
          GenerateHeaderAnchors = defaultArg generateAnchors false
          MarkdownDirectLinkResolver = mdlinkResolver
          CodeReferenceResolver = crefResolver
          TokenKindToCss = tokenKindToCss }

    /// Lookup a specified key in a dictionary, possibly
    /// ignoring newlines or spaces in the key.
    static let (|LookupKey|_|) (dict: IDictionary<_, _>) (key: string) =
        [ key; key.Replace("\r\n", ""); key.Replace("\r\n", " "); key.Replace("\n", ""); key.Replace("\n", " ") ]
        |> List.tryPick (fun key ->
            match dict.TryGetValue(key) with
            | true, v -> Some v
            | _ -> None)

    /// When generating LaTeX, we need to save all files locally
    static let rec downloadSpanImages (saver, links) para =
        match para with
        | IndirectImage(body, _, LookupKey links (link, title), range)
        | DirectImage(body, link, title, range) -> DirectImage(body, saver link, title, range)
        | MarkdownPatterns.SpanNode(s, spans) ->
            MarkdownPatterns.SpanNode(s, List.map (downloadSpanImages (saver, links)) spans)
        | MarkdownPatterns.SpanLeaf(l) -> MarkdownPatterns.SpanLeaf(l)

    static let rec downloadImages ctx (pars: MarkdownParagraphs) : MarkdownParagraphs =
        pars
        |> List.map (function
            | MarkdownPatterns.ParagraphSpans(s, spans) ->
                MarkdownPatterns.ParagraphSpans(s, List.map (downloadSpanImages ctx) spans)
            | MarkdownPatterns.ParagraphNested(o, pars) ->
                MarkdownPatterns.ParagraphNested(o, List.map (downloadImages ctx) pars)
            | MarkdownPatterns.ParagraphLeaf p -> MarkdownPatterns.ParagraphLeaf p)


    static let parsingContext evaluator fscOptions definedSymbols onError =
        let definedSymbols = defaultArg definedSymbols []

        let extraDefines =
            [ // When formatting for tooltips or executing snippets we always include the 'prepare' define.
              // This allows (*** condition: prepare ***) for code elements that are only active
              // when formatting or executing.
              "prepare" ]

        { CompilerOptions = fscOptions
          Evaluator = evaluator
          ConditionalDefines = (definedSymbols @ extraDefines)
          OnError = onError }

    /// Get default output file name, given various information
    static let defaultOutput outputPath input (outputKind: OutputKind) =
        match outputPath with
        | Some out -> out
        | None -> Path.ChangeExtension(input, outputKind.Extension)

    static let downloadImagesForDoc imageSaver (doc: LiterateDocument) =
        match imageSaver with
        | None -> doc
        | Some saver ->
            let pars = doc.Paragraphs |> downloadImages (saver, doc.DefinedLinks)

            doc.With(paragraphs = pars)

    /// <summary>
    /// Parse an F# script file (<c>.fsx</c>) to a <see cref="T:FSharp.Formatting.Literate.LiterateDocument"/>,
    /// type-checking it via the F# Compiler Service.
    /// </summary>
    /// <param name="path">The path to the <c>.fsx</c> script file to parse.</param>
    /// <param name="fscOptions">Additional F# compiler options (e.g. extra <c>-r:</c> references).</param>
    /// <param name="definedSymbols">Conditional-compilation symbols to define.</param>
    /// <param name="references">A map of reference labels to URLs, used to resolve Markdown-style links.</param>
    /// <param name="fsiEvaluator">An optional FSI evaluator; when supplied, code snippets are executed and their output included.</param>
    /// <param name="parseOptions">Markdown parse options. Defaults to <see cref="F:FSharp.Formatting.Markdown.MarkdownParseOptions.AllowYamlFrontMatter"/>.</param>
    /// <param name="rootInputFolder">The root folder used to resolve relative paths within the script. Defaults to the directory containing <paramref name="path"/>.</param>
    /// <param name="onError">A callback invoked with each error/warning message encountered during parsing or type-checking. Defaults to <c>ignore</c>.</param>
    static member ParseAndCheckScriptFile
        (
            path: string,
            ?fscOptions,
            ?definedSymbols,
            ?references,
            ?fsiEvaluator,
            ?parseOptions,
            ?rootInputFolder,
            ?onError
        ) =
        let onError = defaultArg onError ignore
        let ctx = parsingContext fsiEvaluator fscOptions definedSymbols onError

        let rootInputFolder = Some(defaultArg rootInputFolder (Path.GetDirectoryName(path)))

        ParseScript(parseOptions, ctx).ParseAndCheckScriptFile(path, File.ReadAllText path, rootInputFolder, onError)
        |> Transformations.generateReferences references
        |> Transformations.formatCodeSnippets path ctx
        |> Transformations.evaluateCodeSnippets ctx

    /// <summary>
    /// Parse a string containing F# script source code (<c>.fsx</c> syntax) to a
    /// <see cref="T:FSharp.Formatting.Literate.LiterateDocument"/>,
    /// type-checking it via the F# Compiler Service.
    /// </summary>
    /// <param name="content">The F# script source code to parse.</param>
    /// <param name="path">An optional file path used in diagnostics and to resolve relative references. When not supplied, defaults to <c>script.fsx</c> in <paramref name="rootInputFolder"/> (or just <c>script.fsx</c>).</param>
    /// <param name="fscOptions">Additional F# compiler options (e.g. extra <c>-r:</c> references).</param>
    /// <param name="definedSymbols">Conditional-compilation symbols to define.</param>
    /// <param name="references">A map of reference labels to URLs, used to resolve Markdown-style links.</param>
    /// <param name="fsiEvaluator">An optional FSI evaluator; when supplied, code snippets are executed and their output included.</param>
    /// <param name="parseOptions">Markdown parse options. Defaults to <see cref="F:FSharp.Formatting.Markdown.MarkdownParseOptions.AllowYamlFrontMatter"/>.</param>
    /// <param name="rootInputFolder">The root folder used to resolve relative paths within the script.</param>
    /// <param name="onError">A callback invoked with each error/warning message encountered during parsing or type-checking. Defaults to <c>ignore</c>.</param>
    static member ParseScriptString
        (
            content,
            ?path,
            ?fscOptions,
            ?definedSymbols,
            ?references,
            ?fsiEvaluator,
            ?parseOptions,
            ?rootInputFolder,
            ?onError
        ) =
        let onError = defaultArg onError ignore
        let ctx = parsingContext fsiEvaluator fscOptions definedSymbols onError

        let filePath =
            match path with
            | Some s -> s
            | None ->
                match rootInputFolder with
                | None -> "script.fsx"
                | Some r -> Path.Combine(r, "script.fsx")

        ParseScript(parseOptions, ctx).ParseAndCheckScriptFile(filePath, content, rootInputFolder, onError)
        |> Transformations.generateReferences references
        |> Transformations.formatCodeSnippets filePath ctx
        |> Transformations.evaluateCodeSnippets ctx

    /// <summary>
    ///  Parse Markdown document to LiterateDocument
    /// </summary>
    /// <param name="path"></param>
    /// <param name="fscOptions"></param>
    /// <param name="definedSymbols"></param>
    /// <param name="references"></param>
    /// <param name="fsiEvaluator"></param>
    /// <param name="parseOptions">Defaults to MarkdownParseOptions.AllowYamlFrontMatter</param>
    /// <param name="rootInputFolder"></param>
    /// <param name="onError"></param>
    static member ParseMarkdownFile
        (
            path: string,
            ?fscOptions,
            ?definedSymbols,
            ?references,
            ?fsiEvaluator,
            ?parseOptions,
            ?rootInputFolder,
            ?onError
        ) =
        let onError = defaultArg onError ignore
        let ctx = parsingContext fsiEvaluator fscOptions definedSymbols onError

        let rootInputFolder = Some(defaultArg rootInputFolder (Path.GetDirectoryName(path)))

        ParseMarkdown.parseMarkdown path rootInputFolder (File.ReadAllText path) parseOptions
        |> Transformations.generateReferences references
        |> Transformations.formatCodeSnippets path ctx
        |> Transformations.evaluateCodeSnippets ctx

    /// <summary>
    ///  Parse string as a markdown document
    /// </summary>
    /// <param name="content"></param>
    /// <param name="path">optional file path for debugging purposes</param>
    /// <param name="fscOptions"></param>
    /// <param name="definedSymbols"></param>
    /// <param name="references"></param>
    /// <param name="fsiEvaluator"></param>
    /// <param name="parseOptions">Defaults to MarkdownParseOptions.AllowYamlFrontMatter</param>
    /// <param name="rootInputFolder"></param>
    /// <param name="onError"></param>
    static member ParseMarkdownString
        (
            content,
            ?path,
            ?fscOptions,
            ?definedSymbols,
            ?references,
            ?fsiEvaluator,
            ?parseOptions,
            ?rootInputFolder,
            ?onError
        ) =
        let onError = defaultArg onError ignore
        let ctx = parsingContext fsiEvaluator fscOptions definedSymbols onError

        let filePath =
            match path with
            | Some s -> s
            | None ->
                match rootInputFolder with
                | None -> "C:\\document.md"
                | Some r -> Path.Combine(r, "document.md")

        ParseMarkdown.parseMarkdown filePath rootInputFolder content parseOptions
        |> Transformations.generateReferences references
        |> Transformations.formatCodeSnippets filePath ctx
        |> Transformations.evaluateCodeSnippets ctx

    /// <summary>
    /// Parse pynb string as literate document
    /// </summary>
    /// <param name="content"></param>
    /// <param name="path">optional file path for debugging purposes</param>
    /// <param name="definedSymbols"></param>
    /// <param name="references"></param>
    /// <param name="parseOptions">Defaults to MarkdownParseOptions.AllowYamlFrontMatter</param>
    /// <param name="rootInputFolder"></param>
    /// <param name="onError"></param>
    static member ParsePynbString
        (content, ?path, ?definedSymbols, ?references, ?parseOptions, ?rootInputFolder, ?onError)
        =
        let onError = defaultArg onError ignore
        let ctx = parsingContext None None definedSymbols onError

        let filePath =
            match path with
            | Some s -> s
            | None ->
                match rootInputFolder with
                | None -> "script.fsx"
                | Some r -> Path.Combine(r, "script.fsx")

        let content = ParsePynb.pynbStringToFsx content

        ParseScript(parseOptions, ctx).ParseAndCheckScriptFile(filePath, content, rootInputFolder, onError)
        |> Transformations.generateReferences references
        |> Transformations.formatCodeSnippets filePath ctx
        |> Transformations.evaluateCodeSnippets ctx

    // ------------------------------------------------------------------------------------
    // Simple writing functions
    // ------------------------------------------------------------------------------------

    /// Format the literate document as HTML without using a template
    static member ToHtml
        (
            doc: LiterateDocument,
            ?prefix,
            ?lineNumbers,
            ?generateAnchors,
            ?substitutions,
            ?crefResolver,
            ?mdlinkResolver,
            ?tokenKindToCss
        ) =
        let substitutions = defaultArg substitutions []
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)

        let ctx =
            makeFormattingContext
                OutputKind.Html
                prefix
                lineNumbers
                generateAnchors
                []
                crefResolver
                mdlinkResolver
                tokenKindToCss

        let doc = Transformations.replaceLiterateParagraphs ctx doc

        let doc =
            MarkdownDocument(
                doc.Paragraphs
                @ [ InlineHtmlBlock(doc.FormattedTips, None, MarkdownRange.zero) ],
                doc.DefinedLinks
            )

        let sb = System.Text.StringBuilder()
        use wr = new StringWriter(sb)

        HtmlFormatting.formatAsHtml
            wr
            ctx.GenerateHeaderAnchors
            true
            doc.DefinedLinks
            substitutions
            Environment.NewLine
            crefResolver
            mdlinkResolver
            doc.Paragraphs

        sb.ToString()

    /// Write the literate document as HTML without using a template
    static member WriteHtml
        (
            doc: LiterateDocument,
            writer: TextWriter,
            ?prefix,
            ?lineNumbers,
            ?generateAnchors,
            ?substitutions,
            ?crefResolver,
            ?mdlinkResolver,
            ?tokenKindToCss
        ) =
        let substitutions = defaultArg substitutions []
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)

        let ctx =
            makeFormattingContext
                OutputKind.Html
                prefix
                lineNumbers
                generateAnchors
                []
                crefResolver
                mdlinkResolver
                tokenKindToCss

        let doc = Transformations.replaceLiterateParagraphs ctx doc

        let paragraphs =
            doc.Paragraphs
            @ [ InlineHtmlBlock(doc.FormattedTips, None, MarkdownRange.zero) ],
            doc.DefinedLinks

        let doc = MarkdownDocument(paragraphs)

        HtmlFormatting.formatAsHtml
            writer
            ctx.GenerateHeaderAnchors
            true
            doc.DefinedLinks
            substitutions
            Environment.NewLine
            crefResolver
            mdlinkResolver
            doc.Paragraphs

    /// Format the literate document as Latex without using a template
    static member ToLatex
        (doc: LiterateDocument, ?prefix, ?lineNumbers, ?generateAnchors, ?substitutions, ?crefResolver, ?mdlinkResolver)
        =
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)

        let ctx =
            makeFormattingContext
                OutputKind.Latex
                prefix
                lineNumbers
                generateAnchors
                []
                crefResolver
                mdlinkResolver
                None

        let doc = Transformations.replaceLiterateParagraphs ctx doc

        Markdown.ToLatex(
            MarkdownDocument(doc.Paragraphs, doc.DefinedLinks),
            ?substitutions = substitutions,
            ?lineNumbers = lineNumbers
        )

    /// Write the literate document as Latex without using a template
    static member WriteLatex
        (
            doc: LiterateDocument,
            writer: TextWriter,
            ?prefix,
            ?lineNumbers,
            ?generateAnchors,
            ?substitutions,
            ?crefResolver,
            ?mdlinkResolver
        ) =
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)

        let ctx =
            makeFormattingContext
                OutputKind.Latex
                prefix
                lineNumbers
                generateAnchors
                []
                crefResolver
                mdlinkResolver
                None

        let doc = Transformations.replaceLiterateParagraphs ctx doc

        Markdown.WriteLatex(
            MarkdownDocument(doc.Paragraphs, doc.DefinedLinks),
            writer,
            ?substitutions = substitutions,
            ?lineNumbers = lineNumbers
        )

    /// Formate the literate document as an iPython notebook
    static member ToPynb(doc: LiterateDocument, ?substitutions, ?crefResolver, ?mdlinkResolver) =
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)
        let substitutions = defaultArg substitutions []
        let ctx = makeFormattingContext OutputKind.Pynb None None None substitutions crefResolver mdlinkResolver None
        let doc = Transformations.replaceLiterateParagraphs ctx doc
        Markdown.ToPynb(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), substitutions = substitutions)

    /// Formate the literate document as an .fsx script
    static member ToFsx(doc: LiterateDocument, ?substitutions, ?crefResolver, ?mdlinkResolver) =
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)
        let substitutions = defaultArg substitutions []
        let ctx = makeFormattingContext OutputKind.Fsx None None None substitutions crefResolver mdlinkResolver None
        let doc = Transformations.replaceLiterateParagraphs ctx doc
        Markdown.ToFsx(MarkdownDocument(doc.Paragraphs, doc.DefinedLinks), substitutions = substitutions)

    /// Parse and transform a markdown document
    static member internal ParseAndTransformMarkdownFile
        (
            input,
            output,
            outputKind,
            prefix,
            fscOptions,
            lineNumbers,
            references,
            substitutions,
            generateAnchors,
            imageSaver,
            rootInputFolder,
            crefResolver,
            mdlinkResolver,
            parseOptions,
            onError,
            filesWithFrontMatter: FrontMatterFile array
        ) =

        let parseOptions =
            match outputKind with
            | OutputKind.Markdown
            | OutputKind.Fsx
            | OutputKind.Pynb -> parseOptions ||| MarkdownParseOptions.ParseCodeAsOther
            //||| MarkdownParseOptions.ParseNonCodeAsOther
            | _ -> parseOptions

        let doc =
            Literate.ParseMarkdownFile(
                input,
                ?fscOptions = fscOptions,
                ?references = references,
                parseOptions = parseOptions,
                ?rootInputFolder = rootInputFolder,
                ?onError = onError
            )

        let ctx =
            makeFormattingContext
                outputKind
                prefix
                lineNumbers
                generateAnchors
                substitutions
                crefResolver
                mdlinkResolver
                None

        let doc = downloadImagesForDoc imageSaver doc
        let docModel = Formatting.transformDocument filesWithFrontMatter doc output ctx
        docModel

    /// Parse and transform an F# script file
    static member internal ParseAndTransformScriptFile
        (
            input,
            output,
            outputKind,
            prefix,
            fscOptions,
            lineNumbers,
            references,
            fsiEvaluator,
            substitutions,
            generateAnchors,
            imageSaver,
            rootInputFolder,
            crefResolver,
            mdlinkResolver,
            onError,
            filesWithFrontMatter: FrontMatterFile array
        ) =

        let parseOptions =
            match outputKind with
            | OutputKind.Fsx
            | OutputKind.Pynb -> MarkdownParseOptions.ParseCodeAsOther
            | _ -> MarkdownParseOptions.None

        let doc =
            Literate.ParseAndCheckScriptFile(
                input,
                ?fscOptions = fscOptions,
                ?references = references,
                ?fsiEvaluator = fsiEvaluator,
                parseOptions = parseOptions,
                ?rootInputFolder = rootInputFolder,
                ?onError = onError
            )

        let ctx =
            makeFormattingContext
                outputKind
                prefix
                lineNumbers
                generateAnchors
                substitutions
                crefResolver
                mdlinkResolver
                None

        let doc = downloadImagesForDoc imageSaver doc
        let docModel = Formatting.transformDocument filesWithFrontMatter doc output ctx
        docModel

    /// Parse and transform a pynb document
    static member internal ParseAndTransformPynbFile
        (
            input,
            output,
            outputKind,
            prefix,
            fscOptions,
            lineNumbers,
            references,
            substitutions,
            generateAnchors,
            imageSaver,
            rootInputFolder,
            crefResolver,
            mdlinkResolver,
            onError,
            filesWithFrontMatter: FrontMatterFile array
        ) =

        let parseOptions =
            match outputKind with
            | OutputKind.Fsx
            | OutputKind.Pynb -> (MarkdownParseOptions.ParseCodeAsOther)
            | _ -> MarkdownParseOptions.None

        let fsx = ParsePynb.pynbToFsx input

        let doc =
            Literate.ParseScriptString(
                fsx,
                ?fscOptions = fscOptions,
                ?references = references,
                parseOptions = parseOptions,
                ?rootInputFolder = rootInputFolder,
                ?onError = onError
            )

        let ctx =
            makeFormattingContext
                outputKind
                prefix
                lineNumbers
                generateAnchors
                substitutions
                crefResolver
                mdlinkResolver
                None

        let doc = downloadImagesForDoc imageSaver doc
        let docModel = Formatting.transformDocument filesWithFrontMatter doc output ctx
        docModel

    /// Convert a markdown file into HTML or another output kind
    static member ConvertMarkdownFile
        (
            input,
            ?template,
            ?output,
            ?outputKind,
            ?prefix,
            ?fscOptions,
            ?lineNumbers,
            ?references,
            ?substitutions,
            ?generateAnchors,
            ?imageSaver,
            ?rootInputFolder,
            ?crefResolver,
            ?mdlinkResolver,
            ?onError,
            ?filesWithFrontMatter
        ) =

        let outputKind = defaultArg outputKind OutputKind.Html
        let output = defaultOutput output input outputKind
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)
        let substitutions = defaultArg substitutions []
        let filesWithFrontMatter = defaultArg filesWithFrontMatter Array.empty

        let res =
            Literate.ParseAndTransformMarkdownFile(
                input,
                output = output,
                outputKind = outputKind,
                prefix = prefix,
                fscOptions = fscOptions,
                lineNumbers = lineNumbers,
                references = references,
                generateAnchors = generateAnchors,
                imageSaver = imageSaver,
                substitutions = substitutions,
                rootInputFolder = rootInputFolder,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver,
                parseOptions = MarkdownParseOptions.AllowYamlFrontMatter,
                onError = onError,
                filesWithFrontMatter = filesWithFrontMatter
            )

        SimpleTemplating.UseFileAsSimpleTemplate(res.Substitutions, template, output)

    /// <summary>Convert a script file into HTML or another output kind</summary>
    /// <example>
    ///   <code>
    ///     Literate.ConvertScriptFile("script.fsx", template)
    ///   </code>
    /// </example>
    static member ConvertScriptFile
        (
            input,
            ?template,
            ?output,
            ?outputKind,
            ?prefix,
            ?fscOptions,
            ?lineNumbers,
            ?references,
            ?fsiEvaluator,
            ?substitutions,
            ?generateAnchors,
            ?imageSaver,
            ?rootInputFolder,
            ?crefResolver,
            ?mdlinkResolver,
            ?onError,
            ?filesWithFrontMatter
        ) =

        let outputKind = defaultArg outputKind OutputKind.Html
        let output = defaultOutput output input outputKind
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)
        let substitutions = defaultArg substitutions []
        let filesWithFrontMatter = defaultArg filesWithFrontMatter Array.empty

        let res =
            Literate.ParseAndTransformScriptFile(
                input,
                output = output,
                outputKind = outputKind,
                prefix = prefix,
                fscOptions = fscOptions,
                lineNumbers = lineNumbers,
                references = references,
                fsiEvaluator = fsiEvaluator,
                substitutions = substitutions,
                generateAnchors = generateAnchors,
                imageSaver = imageSaver,
                rootInputFolder = rootInputFolder,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver,
                onError = onError,
                filesWithFrontMatter = filesWithFrontMatter
            )

        SimpleTemplating.UseFileAsSimpleTemplate(res.Substitutions, template, output)

    /// Convert a pynb notebook file into HTML or another output kind
    static member ConvertPynbFile
        (
            input,
            ?template,
            ?output,
            ?outputKind,
            ?prefix,
            ?fscOptions,
            ?lineNumbers,
            ?references,
            ?substitutions,
            ?generateAnchors,
            ?imageSaver,
            ?rootInputFolder,
            ?crefResolver,
            ?mdlinkResolver,
            ?onError,
            ?filesWithFrontMatter
        ) =

        let outputKind = defaultArg outputKind OutputKind.Html
        let output = defaultOutput output input outputKind
        let crefResolver = defaultArg crefResolver (fun _ -> None)
        let mdlinkResolver = defaultArg mdlinkResolver (fun _ -> None)
        let substitutions = defaultArg substitutions []
        let filesWithFrontMatter = defaultArg filesWithFrontMatter Array.empty

        let res =
            Literate.ParseAndTransformPynbFile(
                input,
                output = output,
                outputKind = outputKind,
                prefix = prefix,
                fscOptions = fscOptions,
                lineNumbers = lineNumbers,
                references = references,
                substitutions = substitutions,
                generateAnchors = generateAnchors,
                imageSaver = imageSaver,
                rootInputFolder = rootInputFolder,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver,
                onError = onError,
                filesWithFrontMatter = filesWithFrontMatter
            )

        SimpleTemplating.UseFileAsSimpleTemplate(res.Substitutions, template, output)


[<assembly: InternalsVisibleTo("fsdocs")>]
[<assembly: InternalsVisibleTo("FSharp.Formatting.TestHelpers")>]
[<assembly: InternalsVisibleTo("FSharp.Literate.Tests")>]
do ()
