namespace fsdocs

open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.IO
open System.Net.Http
open System.Reflection

open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Literate
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.Markdown
open FSharp.Formatting.Templating
open FSharp.Formatting.ApiDocs

/// Where the title of a content page was found.
[<Struct>]
type internal TitleSource =
    | FrontMatter
    | Heading
    | FileName

/// A content page as seen by the navigation: the information needed to render
/// the list of documents on every page.
type internal NavPage =
    {
        InputPath: string
        /// The output path relative to the output root
        OutputPath: string
        Title: string
        Category: string option
        CategoryIndex: int option
        Index: int option
    }

    /// The URI of the page within the site, mirrors LiterateDocModel.Uri
    member x.Uri(root: string) =
        let uri = x.OutputPath.Replace("\\", "/")

        let uri =
            if uri.StartsWith("./", StringComparison.Ordinal) then
                uri.[2..]
            else
                uri

        sprintf "%s%s" root uri

/// The values of a content page's front matter and its title, found without processing the page.
type internal ScannedFrontMatter =
    {
        Title: string
        TitleSource: TitleSource
        Category: string option
        CategoryIndex: int option
        Index: int option
    }

/// Per-site settings used when computing the model of a content page.
type internal ContentOptions =
    {
        LineNumbers: bool option
        Evaluate: bool
        Substitutions: Substitutions
        OnError: string -> unit
    }

/// Functions shared by 'fsdocs build' (DocContent) and 'fsdocs watch' for turning
/// markdown, script and notebook files into page models and HTML.
module internal Content =

    /// Files starting with '.' or '_template', and '_menu*_template.html', are never content.
    let isSkippedFileName (name: string) =
        name.StartsWith('.')
        || name.StartsWith("_template", StringComparison.Ordinal)
        || (name.StartsWith("_menu", StringComparison.Ordinal)
            && name.EndsWith("_template.html", StringComparison.Ordinal))

    let isFsxFile (path: string) =
        path.EndsWith(".fsx", StringComparison.OrdinalIgnoreCase)

    let isMdFile (path: string) =
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)

    let isPynbFile (path: string) =
        path.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase)

    /// A content file is one that is converted rather than copied.
    let isContentFile (path: string) =
        isFsxFile path || isMdFile path || isPynbFile path

    let allCultures =
        CultureInfo.GetCultures(CultureTypes.AllCultures)
        |> Array.choose (fun x ->
            if x.TwoLetterISOLanguageName.Length <> 2 then
                None
            else
                Some x.TwoLetterISOLanguageName)
        |> HashSet<string>

    /// Two-letter directory names (e.g. 'ja') count as multi-language and are suppressed from
    /// table-of-content generation and the site search index.
    let isOtherLangFolderName (folderName: string) =
        let indirName = folderName.ToLower()
        indirName.Length = 2 && allCultures.Contains indirName

    /// The output file for an input file, relative to the output root and as a full path.
    let getOutputFileNames
        (rootOutputFolderAsGiven: string)
        (inputFileFullPath: string)
        (outputKind: OutputKind)
        outputFolderRelativeToRoot
        =
        let inputFileName = Path.GetFileName(inputFileFullPath)
        let isFsx = inputFileFullPath.EndsWith(".fsx", true, CultureInfo.InvariantCulture)
        let isMd = inputFileFullPath.EndsWith(".md", true, CultureInfo.InvariantCulture)
        let isPynb = inputFileFullPath.EndsWith(".ipynb", true, CultureInfo.InvariantCulture)
        let ext = outputKind.Extension

        let outputFileRelativeToRoot =
            if isFsx || isMd || isPynb then
                let basename = Path.GetFileNameWithoutExtension(inputFileFullPath)

                Path.Combine(outputFolderRelativeToRoot, sprintf "%s.%s" basename ext)
            else
                Path.Combine(outputFolderRelativeToRoot, inputFileName)

        let outputFileFullPath = Path.GetFullPath(Path.Combine(rootOutputFolderAsGiven, outputFileRelativeToRoot))
        outputFileRelativeToRoot, outputFileFullPath

    /// Resolve a markdown link to an input file (e.g. 'other.md') to the relative URL of its output.
    let makeMarkdownLinkResolver
        (rootOutputFolderAsGiven: string)
        (inputFolderAsGiven, outputFolderRelativeToRoot, fullPathFileMap: Map<(string * OutputKind), string>, outputKind)
        (markdownReference: string)
        =
        let markdownReferenceAsFullInputPathOpt =
            try
                Path.GetFullPath(Path.Combine(inputFolderAsGiven, markdownReference)) |> Some
            with _ ->
                None

        match markdownReferenceAsFullInputPathOpt with
        | None -> None
        | Some markdownReferenceFullInputPath ->
            match fullPathFileMap.TryFind(markdownReferenceFullInputPath, outputKind) with
            | None -> None
            | Some markdownReferenceFullOutputPath ->
                try
                    let outputFolderFullPath =
                        Path.GetFullPath(Path.Combine(rootOutputFolderAsGiven, outputFolderRelativeToRoot))

                    let uri =
                        Uri(outputFolderFullPath + "/").MakeRelativeUri(Uri(markdownReferenceFullOutputPath)).ToString()

                    Some uri
                with _ ->
                    printfn
                        $"Couldn't map markdown reference %s{markdownReference} that seemed to correspond to an input file"

                    None

    /// Read the category, categoryindex and index from the front matter of a content file.
    let parseFrontMatterOfFile (fileName: string) : FrontMatterFile option =
        let ext = Path.GetExtension fileName

        if ext = ".fsx" then
            ParseScript.ParseFrontMatter(fileName)
        elif ext = ".md" then
            File.ReadLines fileName |> FrontMatterFile.ParseFromLines fileName
        elif ext = ".ipynb" then
            ParsePynb.parseFrontMatter fileName
        else
            None

    /// Sort front matter files the way the next/previous page links expect.
    let sortFilesWithFrontMatter (files: FrontMatterFile seq) =
        files
        |> Seq.sortBy (fun { Index = idx; CategoryIndex = cIdx } -> cIdx, idx)
        |> Seq.toArray

    /// The markdown text of the block comments of a script file, in source order and
    /// without the '(**' and '*)' delimiters. Command comments '(*** ... ***)' are skipped.
    let markdownBlocksOfScript (fileName: string) =
        ParseScript.ParseBlockComments fileName
        |> List.choose (fun comment ->
            let comment = comment.Trim()

            if comment.StartsWith("(***", StringComparison.Ordinal) then
                None
            elif comment.StartsWith("(**", StringComparison.Ordinal) then
                let inner = comment.Substring(3)

                let inner =
                    if inner.EndsWith("*)", StringComparison.Ordinal) then
                        inner.Substring(0, inner.Length - 2)
                    else
                        inner

                Some(inner.TrimStart())
            else
                None)

    /// The markdown text of the markdown cells of a notebook, in order.
    let markdownBlocksOfNotebook (fileName: string) =
        let json = System.Text.Json.JsonDocument.Parse(File.ReadAllText fileName)

        json.RootElement.GetProperty("cells").EnumerateArray()
        |> Seq.filter (fun cell ->
            match cell.TryGetProperty("cell_type") with
            | true, cellType -> cellType.GetString() = "markdown"
            | _ -> false)
        |> Seq.choose (fun cell ->
            match ParsePynb.parseCell cell with
            | ParsePynb.Markdown source -> Some source
            | ParsePynb.Code _ -> None)
        |> Seq.toList

    /// Parse markdown blocks the way the literate processing does: the first block may carry
    /// yaml front matter, the paragraphs of all blocks are concatenated.
    let private parseMarkdownBlocks (blocks: string list) =
        blocks
        |> List.mapi (fun i text ->
            let parseOptions =
                if i = 0 then
                    MarkdownParseOptions.AllowYamlFrontMatter
                else
                    MarkdownParseOptions.None

            Markdown.Parse(text, parseOptions = parseOptions).Paragraphs)
        |> List.concat

    /// The front matter values and title for the given paragraphs, computed exactly as
    /// Formatting.transformDocument does: the title is the 'title:' front matter entry, else the
    /// formatted text of the first level-1 heading, else the file name.
    let frontMatterOfParagraphs (paragraphs: MarkdownParagraph list) (fileNameWithoutExtension: string) =
        let findInFrontMatter (key: string) =
            match paragraphs with
            | YamlFrontmatter(lines, _) :: _ ->
                lines
                |> List.tryPick (fun line ->
                    let line = line.Trim()

                    if line.StartsWith(key + ":", StringComparison.Ordinal) then
                        Some(line.[(key + ":").Length ..].Trim())
                    else
                        None)
            | _ -> None

        let mkValidIndex (value: string) =
            match Int32.TryParse value with
            | true, i -> Some i
            | false, _ -> None

        let title, source =
            match findInFrontMatter "title" with
            | Some text -> text, TitleSource.FrontMatter
            | None ->
                match Formatting.findHeadings paragraphs true OutputKind.Html with
                | Some heading -> heading, TitleSource.Heading
                | None -> fileNameWithoutExtension, TitleSource.FileName

        {
            Title = title
            TitleSource = source
            Category = findInFrontMatter "category"
            CategoryIndex = findInFrontMatter "categoryindex" |> Option.bind mkValidIndex
            Index = findInFrontMatter "index" |> Option.bind mkValidIndex
        }

    /// Cheaply determine the front matter and title of a content page without type checking or
    /// evaluating it. The values equal those of the HTML page model computed by computeModel.
    let scanFrontMatter (inputFileFullPath: string) : ScannedFrontMatter =
        let name = Path.GetFileNameWithoutExtension inputFileFullPath

        let blocks =
            if isFsxFile inputFileFullPath then
                markdownBlocksOfScript inputFileFullPath
            elif isMdFile inputFileFullPath then
                [ File.ReadAllText inputFileFullPath ]
            elif isPynbFile inputFileFullPath then
                markdownBlocksOfNotebook inputFileFullPath
            else
                []

        frontMatterOfParagraphs (parseMarkdownBlocks blocks) name

    /// The title of a content page and where it was found.
    let scanTitle (inputFileFullPath: string) : string * TitleSource =
        let scanned = scanFrontMatter inputFileFullPath
        scanned.Title, scanned.TitleSource

    let private evaluateNotebook ipynbFile =
        let args = $"repl --run %s{ipynbFile} --default-kernel fsharp --exit-after-run --output-path %s{ipynbFile}"

        let psi =
            ProcessStartInfo(fileName = "dotnet", arguments = args, UseShellExecute = false, CreateNoWindow = true)

        try
            let p = Process.Start(psi)
            p.WaitForExit()
        with _ ->
            let msg =
                $"Failed to evaluate notebook %s{ipynbFile} using dotnet-repl\n"
                + $"""try running "%s{args}" at the command line and inspect the error"""

            failwith msg

    let private checkDotnetReplInstall () =
        let failmsg = "'dotnet-repl' is not installed. Please install it using 'dotnet tool install dotnet-repl'"

        try
            let psi =
                ProcessStartInfo(
                    fileName = "dotnet",
                    arguments = "tool list --local",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                )

            let p = Process.Start(psi)
            let ol = p.StandardOutput.ReadToEnd()
            p.WaitForExit()
            psi.Arguments <- "tool list --global"
            p.Start() |> ignore
            let og = p.StandardOutput.ReadToEnd()
            let output = $"%s{ol}\n%s{og}"

            if not (output.Contains("dotnet-repl")) then
                failwith failmsg

            p.WaitForExit()
        with _ ->
            failwith failmsg

    /// Compute the model of a content page (script, markdown or notebook) for one output kind.
    /// Evaluates the page when the options ask for it. Does not touch the output folder.
    let computeModel
        (options: ContentOptions)
        (rootInputFolder: string option)
        (inputFileFullPath: string)
        (outputKind: OutputKind)
        (outputFileRelativeToRoot: string)
        (crefResolver: string -> (string * string) option)
        (mdlinkResolver: string -> string option)
        (filesWithFrontMatter: FrontMatterFile array)
        (imageSaverOpt: (string -> string) option)
        : LiterateDocModel =
        let onError = options.OnError

        if isFsxFile inputFileFullPath then
            printfn "  generating model for %s --> %s" inputFileFullPath outputFileRelativeToRoot

            let fsiEvaluator =
                (if options.Evaluate then
                     Some(new FsiEvaluator(onError = onError, options = [| "--multiemit-" |]) :> IFsiEvaluator)
                 else
                     None)

            try
                Literate.ParseAndTransformScriptFile(
                    inputFileFullPath,
                    output = outputFileRelativeToRoot,
                    outputKind = outputKind,
                    prefix = None,
                    fscOptions = None,
                    lineNumbers = options.LineNumbers,
                    references = Some false,
                    fsiEvaluator = fsiEvaluator,
                    substitutions = options.Substitutions,
                    generateAnchors = Some true,
                    imageSaver = imageSaverOpt,
                    rootInputFolder = rootInputFolder,
                    crefResolver = crefResolver,
                    mdlinkResolver = mdlinkResolver,
                    onError = Some onError,
                    filesWithFrontMatter = filesWithFrontMatter
                )
            finally
                fsiEvaluator |> Option.iter (fun e -> e.Dispose())

        elif isMdFile inputFileFullPath then
            printfn "  preparing %s --> %s" inputFileFullPath outputFileRelativeToRoot

            Literate.ParseAndTransformMarkdownFile(
                inputFileFullPath,
                output = outputFileRelativeToRoot,
                outputKind = outputKind,
                prefix = None,
                fscOptions = None,
                lineNumbers = options.LineNumbers,
                references = Some false,
                substitutions = options.Substitutions,
                generateAnchors = Some true,
                imageSaver = imageSaverOpt,
                rootInputFolder = rootInputFolder,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver,
                parseOptions = MarkdownParseOptions.AllowYamlFrontMatter,
                onError = Some onError,
                filesWithFrontMatter = filesWithFrontMatter
            )
        elif isPynbFile inputFileFullPath then
            printfn "  preparing %s --> %s" inputFileFullPath outputFileRelativeToRoot

            if options.Evaluate then
                checkDotnetReplInstall ()
                printfn $"  evaluating %s{inputFileFullPath} with dotnet-repl"
                evaluateNotebook inputFileFullPath

            Literate.ParseAndTransformPynbFile(
                inputFileFullPath,
                output = outputFileRelativeToRoot,
                outputKind = outputKind,
                prefix = None,
                fscOptions = None,
                lineNumbers = options.LineNumbers,
                references = Some false,
                substitutions = options.Substitutions,
                generateAnchors = Some true,
                imageSaver = imageSaverOpt,
                rootInputFolder = rootInputFolder,
                crefResolver = crefResolver,
                mdlinkResolver = mdlinkResolver,
                onError = Some onError,
                filesWithFrontMatter = filesWithFrontMatter
            )
        else
            failwithf "'%s' is not a content file" inputFileFullPath

    /// Render a page model with a template. The page substitutions come last so they win over the globals.
    let renderPage (model: LiterateDocModel) (template: string option) (globals: Substitutions) =
        SimpleTemplating.RenderWithFileTemplate(globals @ model.Substitutions, template)

    /// The search index entries of the given page models.
    let getSearchIndexEntries (root: string) (docModels: (string * bool * LiterateDocModel) list) =
        [|
            for (_inputFile, isOtherLang, model) in docModels do
                if not isOtherLang then
                    match model.IndexText with
                    | Some(IndexText(fullContent, headings)) ->
                        {
                            title = model.Title
                            content = fullContent
                            headings = headings
                            uri = model.Uri(root)
                            ``type`` = "content"
                        }
                    | _ -> ()
        |]

    /// The navigation page of a computed HTML page model.
    let navPageOfModel (inputFileFullPath: string) (model: LiterateDocModel) : NavPage =
        {
            InputPath = inputFileFullPath
            OutputPath = model.OutputPath
            Title = model.Title
            Category = model.Category
            CategoryIndex = model.CategoryIndex
            Index = model.Index
        }

    /// Pre-computes the expensive navigation structure (filter/group/sort) once, returning a
    /// cheap render function that generates nav HTML for any given current page path.
    /// This avoids O(n²) work when building a site with n pages, since the structure
    /// (grouping, sorting, templating check) is the same for every page.
    /// The pages must be HTML pages outside multi-language folders; 'index' pages are excluded here.
    let getNavigationEntriesFactory
        (root: string)
        (input: string, pages: NavPage list, ignoreUncategorized: bool)
        : string option -> string =

        let baseModels =
            [
                for page in pages do
                    if Path.GetFileNameWithoutExtension(page.InputPath) <> "index" then
                        yield (page.InputPath, page)
            ]

        let filteredBase =
            if ignoreUncategorized then
                baseModels |> List.filter (fun (_, model) -> model.Category.IsSome)
            else
                baseModels

        // Pre-sort items within each category (independent of active page)
        let orderGroup items =
            items
            |> List.sortBy (fun (_, model: NavPage) -> Option.defaultValue Int32.MaxValue model.Index)

        // Pre-compute: group by category, sort categories, sort items within each group
        let sortedGroups =
            filteredBase
            |> List.groupBy (fun (_, model) -> model.Category)
            |> List.sortBy (fun (_, items) ->
                match (snd items.[0]).CategoryIndex with
                | Some s ->
                    (try
                        int32 s
                     with _ ->
                         Int32.MaxValue)
                | None -> Int32.MaxValue)
            |> List.map (fun (cat, items) -> cat, orderGroup items)

        // Cache filesystem check — same result for all pages in a build
        let useTemplating = Menu.isTemplatingAvailable input

        // Cheap render function: only sets IsActive and generates HTML (no sorting/grouping)
        fun (currentPagePath: string option) ->
            let modelsByCategory =
                sortedGroups
                |> List.map (fun (cat, items) ->
                    cat,
                    items
                    |> List.map (fun (path, model) ->
                        let isActive =
                            match currentPagePath with
                            | None -> false
                            | Some cp -> cp = path

                        model, isActive))

            if useTemplating then
                let createGroup (isCategoryActive: bool) (header: string) (items: (NavPage * bool) list) : string =
                    let menuItems =
                        items
                        |> List.map (fun (model: NavPage, isActive) ->
                            let link = model.Uri(root)
                            let title = System.Web.HttpUtility.HtmlEncode model.Title

                            {
                                Menu.MenuItem.Link = link
                                Menu.MenuItem.Content = title
                                Menu.MenuItem.IsActive = isActive
                            })

                    Menu.createMenu input isCategoryActive header menuItems

                if modelsByCategory.Length = 1 && (fst modelsByCategory.[0]) = None then
                    let _, items = modelsByCategory.[0]
                    createGroup false "Documentation" items
                else
                    modelsByCategory
                    |> List.map (fun (header, items) ->
                        let header = Option.defaultValue "Other" header
                        let isActive = items |> List.exists snd
                        createGroup isActive header items)
                    |> String.concat "\n"
            else
                [
                    if modelsByCategory.Length = 1 && (fst modelsByCategory.[0]) = None then
                        li [ Class "nav-header" ] [ !!"Documentation" ]

                        for (model, isActive) in snd modelsByCategory.[0] do
                            let link = model.Uri(root)
                            let activeClass = if isActive then "active" else ""

                            li
                                [ Class $"nav-item %s{activeClass}" ]
                                [ a [ Class "nav-link"; (Href link) ] [ encode model.Title ] ]
                    else
                        for (cat, modelsInCategory) in modelsByCategory do
                            let categoryActiveClass = if modelsInCategory |> List.exists snd then "active" else ""

                            match cat with
                            | Some c -> li [ Class $"nav-header %s{categoryActiveClass}" ] [ !!c ]
                            | None -> li [ Class $"nav-header %s{categoryActiveClass}" ] [ !!"Other" ]

                            for (model, isActive) in modelsInCategory do
                                let link = model.Uri(root)
                                let activeClass = if isActive then "active" else ""

                                li
                                    [ Class $"nav-item %s{activeClass}" ]
                                    [ a [ Class "nav-link"; (Href link) ] [ encode model.Title ] ]
                ]
                |> List.map (fun html -> html.ToString())
                |> String.concat "             \n"

/// Convert markdown, script and other content into a static site
type internal DocContent
    (
        rootOutputFolderAsGiven,
        previous: Map<_, _>,
        lineNumbers,
        evaluate,
        substitutions,
        saveImages,
        watch,
        root,
        crefResolver,
        onError
    ) =

    let contentOptions: ContentOptions =
        {
            LineNumbers = lineNumbers
            Evaluate = evaluate
            Substitutions = substitutions
            OnError = onError
        }

    let createImageSaver (rootOutputFolderAsGiven) =
        // Download images so that they can be embedded
        let http = new HttpClient()
        let mutable counter = 0

        fun (url: string) ->
            if
                url.StartsWith("http", StringComparison.Ordinal)
                || url.StartsWith("https", StringComparison.Ordinal)
            then
                counter <- counter + 1
                let ext = Path.GetExtension(url)

                let url2 = sprintf "savedimages/saved%d%s" counter ext

                let fn = sprintf "%s/%s" rootOutputFolderAsGiven url2

                ensureDirectory (sprintf "%s/savedimages" rootOutputFolderAsGiven)
                printfn "downloading %s --> %s" url fn
                let bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult()
                File.WriteAllBytes(fn, bytes)
                url2
            else
                url

    let getOutputFileNames inputFileFullPath outputKind outputFolderRelativeToRoot =
        Content.getOutputFileNames rootOutputFolderAsGiven inputFileFullPath outputKind outputFolderRelativeToRoot

    // Check if a sub-folder is actually the output directory
    let subFolderIsOutput subInputFolderFullPath =
        let subFolderFullPath = Path.GetFullPath(subInputFolderFullPath)
        let rootOutputFolderFullPath = Path.GetFullPath(rootOutputFolderAsGiven)
        (subFolderFullPath = rootOutputFolderFullPath)

    let makeMarkdownLinkResolver args =
        Content.makeMarkdownLinkResolver rootOutputFolderAsGiven args

    /// Prepare the map of input file to output file. This map is used to make substitutions through markdown
    /// source such A.md --> A.html or A.fsx --> A.html.  The substitutions depend on the output kind.
    let prepFile (inputFileFullPath: string) (outputKind: OutputKind) outputFolderRelativeToRoot =
        [
            let inputFileName = Path.GetFileName(inputFileFullPath)

            if not (Content.isSkippedFileName inputFileName) then
                let inputFileFullPath = Path.GetFullPath(inputFileFullPath)

                let _relativeOutputFile, outputFileFullPath =
                    getOutputFileNames inputFileFullPath outputKind outputFolderRelativeToRoot

                yield ((inputFileFullPath, outputKind), outputFileFullPath)
        ]

    /// Likewise prepare the map of input files to output files
    let rec prepFolder (inputFolderAsGiven: string) outputFolderRelativeToRoot =
        [
            let inputs = Directory.GetFiles(inputFolderAsGiven, "*")

            for input in inputs do
                yield! prepFile input OutputKind.Html outputFolderRelativeToRoot
                yield! prepFile input OutputKind.Latex outputFolderRelativeToRoot
                yield! prepFile input OutputKind.Pynb outputFolderRelativeToRoot
                yield! prepFile input OutputKind.Fsx outputFolderRelativeToRoot
                yield! prepFile input OutputKind.Markdown outputFolderRelativeToRoot

            for subInputFolderFullPath in Directory.EnumerateDirectories(inputFolderAsGiven) do
                let subInputFolderName = Path.GetFileName(subInputFolderFullPath)
                let subFolderIsSkipped = subInputFolderName.StartsWith '.'
                let subFolderIsOutput = subFolderIsOutput subInputFolderFullPath

                if not subFolderIsOutput && not subFolderIsSkipped then
                    yield!
                        prepFolder
                            (Path.Combine(inputFolderAsGiven, subInputFolderName))
                            (Path.Combine(outputFolderRelativeToRoot, subInputFolderName))
        ]

    let processFile
        rootInputFolder
        (isOtherLang: bool)
        (inputFileFullPath: string)
        outputKind
        template
        outputFolderRelativeToRoot
        imageSaver
        mdlinkResolver
        (filesWithFrontMatter: FrontMatterFile array)
        =
        [
            let name = Path.GetFileName(inputFileFullPath)

            if name.StartsWith('.') then
                printfn "skipping file %s" inputFileFullPath
            elif not (Content.isSkippedFileName name) then
                let isContent = Content.isContentFile inputFileFullPath

                // A _template.tex or _template.pynb is needed to generate those files
                match outputKind, template with
                | OutputKind.Pynb, None -> ()
                | OutputKind.Latex, None -> ()
                | OutputKind.Fsx, None -> ()
                | OutputKind.Markdown, None -> ()
                | _ ->

                    let imageSaverOpt =
                        match outputKind with
                        | OutputKind.Pynb when saveImages <> Some false -> Some imageSaver
                        | OutputKind.Latex when saveImages <> Some false -> Some imageSaver
                        | OutputKind.Fsx when saveImages = Some true -> Some imageSaver
                        | OutputKind.Html when saveImages = Some true -> Some imageSaver
                        | OutputKind.Markdown when saveImages = Some true -> Some imageSaver
                        | _ -> None

                    let outputFileRelativeToRoot, outputFileFullPath =
                        getOutputFileNames inputFileFullPath outputKind outputFolderRelativeToRoot

                    // Update only when needed - template or file or tool has changed

                    let changed =
                        let fileChangeTime =
                            try
                                File.GetLastWriteTime(inputFileFullPath)
                            with _ ->
                                DateTime.MaxValue

                        let templateChangeTime =
                            match template with
                            | Some t when isContent ->
                                try
                                    let fi = FileInfo(t)
                                    let input = fi.Directory.Name
                                    let headPath = Path.Combine(input, "_head.html")
                                    let bodyPath = Path.Combine(input, "_body.html")

                                    [
                                        yield File.GetLastWriteTime(t)
                                        if Menu.isTemplatingAvailable input then
                                            yield! Menu.getLastWriteTimes input
                                        if File.Exists headPath then
                                            yield File.GetLastWriteTime headPath
                                        if File.Exists bodyPath then
                                            yield File.GetLastWriteTime bodyPath
                                    ]
                                    |> List.max
                                with _ ->
                                    DateTime.MaxValue
                            | _ -> DateTime.MinValue

                        let toolChangeTime =
                            try
                                File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location)
                            with _ ->
                                DateTime.MaxValue

                        let changeTime = fileChangeTime |> max templateChangeTime |> max toolChangeTime

                        let generateTime =
                            try
                                File.GetLastWriteTime(outputFileFullPath)
                            with _ ->
                                System.DateTime.MinValue

                        changeTime > generateTime

                    // If it's changed or we don't know anything about it
                    // we have to compute the model to get the global substitutions right
                    let mainRun = (outputKind = OutputKind.Html)
                    let haveModel = previous.TryFind inputFileFullPath

                    if changed || (watch && mainRun && haveModel.IsNone) then
                        if isContent then
                            let model =
                                Content.computeModel
                                    contentOptions
                                    rootInputFolder
                                    inputFileFullPath
                                    outputKind
                                    outputFileRelativeToRoot
                                    crefResolver
                                    mdlinkResolver
                                    filesWithFrontMatter
                                    imageSaverOpt

                            yield
                                ((if mainRun then
                                      Some(inputFileFullPath, isOtherLang, model)
                                  else
                                      None),
                                 (fun p ->
                                     printfn "  writing %s --> %s" inputFileFullPath outputFileRelativeToRoot
                                     ensureDirectory (Path.GetDirectoryName(outputFileFullPath))

                                     SimpleTemplating.WriteOutputFile(
                                         outputFileFullPath,
                                         Content.renderPage model template p
                                     )))

                        else if mainRun then
                            yield
                                (None,
                                 (fun _p ->
                                     printfn "  copying %s --> %s" inputFileFullPath outputFileRelativeToRoot
                                     ensureDirectory (Path.GetDirectoryName(outputFileFullPath))
                                     // check the file still exists for the incremental case
                                     if (File.Exists inputFileFullPath) then
                                         // ignore errors in watch mode
                                         try
                                             File.Copy(inputFileFullPath, outputFileFullPath, true)
                                             File.SetLastWriteTime(outputFileFullPath, DateTime.Now)
                                         with _ when watch ->
                                             ()))
                    //printfn "skipping unchanged file %s" inputFileFullPath
                    else if mainRun && watch then
                        match haveModel with
                        | None -> ()
                        | Some haveModel -> yield (Some(inputFileFullPath, isOtherLang, haveModel), (fun _ -> ()))
        ]

    let rec processFolder
        (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate, mdTemplate, isOtherLang, rootInputFolder, fullPathFileMap)
        (inputFolderAsGiven: string)
        outputFolderRelativeToRoot
        (filesWithFrontMatter: FrontMatterFile array)
        =
        [
            // Look for the presence of the _template.* files to activate the
            // generation of the content.
            let indirName = Path.GetFileName(inputFolderAsGiven)

            // Two-letter directory names (e.g. 'ja') with 'docs' count as multi-language and are suppressed from table-of-content
            // generation and site search index
            let isOtherLang = isOtherLang || Content.isOtherLangFolderName indirName

            let possibleNewHtmlTemplate = Path.Combine(inputFolderAsGiven, "_template.html")

            let htmlTemplate =
                if File.Exists(possibleNewHtmlTemplate) then
                    Some possibleNewHtmlTemplate
                else
                    htmlTemplate

            let possibleNewPynbTemplate = Path.Combine(inputFolderAsGiven, "_template.ipynb")

            let pynbTemplate =
                if File.Exists(possibleNewPynbTemplate) then
                    Some possibleNewPynbTemplate
                else
                    pynbTemplate

            let possibleNewFsxTemplate = Path.Combine(inputFolderAsGiven, "_template.fsx")

            let fsxTemplate =
                if File.Exists(possibleNewFsxTemplate) then
                    Some possibleNewFsxTemplate
                else
                    fsxTemplate

            let possibleNewMdTemplate = Path.Combine(inputFolderAsGiven, "_template.md")

            let mdTemplate =
                if File.Exists(possibleNewMdTemplate) then
                    Some possibleNewMdTemplate
                else
                    mdTemplate

            let possibleNewLatexTemplate = Path.Combine(inputFolderAsGiven, "_template.tex")

            let texTemplate =
                if File.Exists(possibleNewLatexTemplate) then
                    Some possibleNewLatexTemplate
                else
                    texTemplate

            ensureDirectory (Path.Combine(rootOutputFolderAsGiven, outputFolderRelativeToRoot))

            let inputs = Directory.GetFiles(inputFolderAsGiven, "*")

            let imageSaver = createImageSaver (Path.Combine(rootOutputFolderAsGiven, outputFolderRelativeToRoot))

            // Look for the four different kinds of content
            for input in inputs do
                for (outputKind, template) in
                    [
                        OutputKind.Html, htmlTemplate
                        OutputKind.Latex, texTemplate
                        OutputKind.Pynb, pynbTemplate
                        OutputKind.Fsx, fsxTemplate
                        OutputKind.Markdown, mdTemplate
                    ] do
                    yield!
                        processFile
                            rootInputFolder
                            isOtherLang
                            input
                            outputKind
                            template
                            outputFolderRelativeToRoot
                            imageSaver
                            (makeMarkdownLinkResolver (
                                inputFolderAsGiven,
                                outputFolderRelativeToRoot,
                                fullPathFileMap,
                                outputKind
                            ))
                            filesWithFrontMatter

            for subInputFolderFullPath in Directory.EnumerateDirectories(inputFolderAsGiven) do
                let subInputFolderName = Path.GetFileName(subInputFolderFullPath)
                let subFolderIsSkipped = subInputFolderName.StartsWith '.'
                let subFolderIsOutput = subFolderIsOutput subInputFolderFullPath

                if subFolderIsOutput || subFolderIsSkipped then

                    printfn "  skipping directory %s" subInputFolderFullPath
                else
                    yield!
                        processFolder
                            (htmlTemplate,
                             texTemplate,
                             pynbTemplate,
                             fsxTemplate,
                             mdTemplate,
                             isOtherLang,
                             rootInputFolder,
                             fullPathFileMap)
                            (Path.Combine(inputFolderAsGiven, subInputFolderName))
                            (Path.Combine(outputFolderRelativeToRoot, subInputFolderName))
                            filesWithFrontMatter
        ]

    member _.Convert(rootInputFolderAsGiven, htmlTemplate, extraInputs, ?defaultMdTemplate: string) =

        let inputDirectories = extraInputs @ [ (rootInputFolderAsGiven, ".") ]

        // Maps full input paths to full output paths
        let fullPathFileMap =
            [
                for (rootInputFolderAsGiven, outputFolderRelativeToRoot) in inputDirectories do
                    yield! prepFolder rootInputFolderAsGiven outputFolderRelativeToRoot
            ]
            |> Map.ofList

        // In order to create {{next-page-url}} and {{previous-page-url}}
        // We need to scan all *.fsx and *.md files for their frontmatter.
        let filesWithFrontMatter =
            fullPathFileMap
            |> Map.keys
            |> Seq.map fst
            |> Seq.distinct
            |> Seq.choose Content.parseFrontMatterOfFile
            |> Content.sortFilesWithFrontMatter

        [
            for (rootInputFolderAsGiven, outputFolderRelativeToRoot) in inputDirectories do
                yield!
                    processFolder
                        (htmlTemplate,
                         None,
                         None,
                         None,
                         defaultMdTemplate,
                         false,
                         Some rootInputFolderAsGiven,
                         fullPathFileMap)
                        rootInputFolderAsGiven
                        outputFolderRelativeToRoot
                        filesWithFrontMatter
        ]

    member _.GetSearchIndexEntries(docModels: (string * bool * LiterateDocModel) list) =
        Content.getSearchIndexEntries root docModels

    /// Pre-computes the navigation structure once, returning a cheap render function that
    /// generates nav HTML for any given current page path.
    member _.GetNavigationEntriesFactory
        (input, docModels: (string * bool * LiterateDocModel) list, ignoreUncategorized: bool)
        : string option -> string =

        let pages =
            [
                for (inputFileFullPath, isOtherLang, model) in docModels do
                    if not isOtherLang && model.OutputKind = OutputKind.Html then
                        yield Content.navPageOfModel inputFileFullPath model
            ]

        Content.getNavigationEntriesFactory root (input, pages, ignoreUncategorized)
