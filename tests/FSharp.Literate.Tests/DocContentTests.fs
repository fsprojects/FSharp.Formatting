module FSharp.Literate.Tests.DocContent

open System.IO
open FSharp.Formatting.Literate
open FSharp.Formatting.Templating
open fsdocs
open NUnit.Framework
open FsUnitTyped

do FSharp.Formatting.TestHelpers.enableLogging ()

// --------------------------------------------------------------------------------------
// Test FSI evaluator
// --------------------------------------------------------------------------------------

let (</>) a b = Path.Combine(a, b)

[<Test>]
let ``Can build doc content`` () =
    let rootOutputFolderAsGiven = __SOURCE_DIRECTORY__ </> "output1"
    let rootInputFolderAsGiven = __SOURCE_DIRECTORY__ </> "files"

    if Directory.Exists(rootOutputFolderAsGiven) then
        Directory.Delete(rootOutputFolderAsGiven, true)

    let content =
        DocContent(
            rootOutputFolderAsGiven,
            Map.empty,
            lineNumbers = None,
            evaluate = false,
            substitutions = [],
            saveImages = None,
            watch = false,
            root = "https://github.com",
            crefResolver = (fun _ -> None),
            onError = failwith
        )

    let docModels = content.Convert(rootInputFolderAsGiven, None, [])
    let globals = []

    for (_thing, action) in docModels do
        action globals

    // Check simple1.fsx --> simple1.html substititions
    // Check simple2.md --> simple2.html substititions
    let html1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.html")
    let html2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.html")
    let html3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.html")
    html1 |> shouldContainText """href="simple1.html">"""
    html1 |> shouldContainText """href="simple2.html">"""
    html1 |> shouldContainText """href="simple3.html">"""
    html2 |> shouldContainText """href="simple1.html">"""
    html2 |> shouldContainText """href="simple2.html">"""
    html2 |> shouldContainText """href="simple3.html">"""
    html3 |> shouldContainText """href="simple1.html">"""
    html3 |> shouldContainText """href="simple2.html">"""
    html3 |> shouldContainText """href="simple3.html">"""

    // Check simple1.fsx --> simple1.ipynb substititions
    // Check simple2.md --> simple1.ipynb substititions
    let ipynb1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.ipynb")
    let ipynb2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.ipynb")
    let ipynb3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.ipynb")
    ipynb1 |> shouldContainText "simple2.ipynb"
    ipynb1 |> shouldContainText "simple3.ipynb"
    ipynb2 |> shouldContainText "simple1.ipynb"
    ipynb3 |> shouldContainText "simple1.ipynb"

    // Check fsx exists
    // Check simple1.fsx --> simple1.fsx substititions
    // Check simple2.md --> simple1.fsx substititions
    let fsx1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.fsx")
    let fsx2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.fsx")
    let fsx3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.fsx")
    fsx1 |> shouldContainText "simple2.fsx"
    fsx1 |> shouldContainText "simple3.fsx"
    fsx2 |> shouldContainText "simple1.fsx"
    fsx3 |> shouldContainText "simple1.fsx"

    // Check md contents
    // Check simple1.fsx --> simple1.md substititions
    // Check simple2.md --> simple1.md substititions
    let md1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.md")
    let md2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.md")
    let md3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.md")
    md1 |> shouldContainText "simple2.md"
    md1 |> shouldContainText "simple3.md"
    md2 |> shouldContainText "simple1.md"
    md3 |> shouldContainText "simple1.md"


    // Check in-folder1.fsx --> in-folder1.html substititions
    let f1html1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.html")
    let f2html2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.html")
    f1html1 |> shouldContainText """href="../folder2/in-folder2.html">"""
    f2html2 |> shouldContainText """href="../folder1/in-folder1.html">"""

    // Check in-folder1.fsx --> in-folder1.ipynb substititions
    let f1ipynb1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.ipynb")
    let f2ipynb2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.ipynb")
    f1ipynb1 |> shouldContainText """../folder2/in-folder2.ipynb"""
    f2ipynb2 |> shouldContainText """../folder1/in-folder1.ipynb"""

    // Check fsx exists
    let f1fsx1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.fsx")
    let f2fsx2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.fsx")
    f1fsx1 |> shouldContainText """../folder2/in-folder2.fsx"""
    f2fsx2 |> shouldContainText """../folder1/in-folder1.fsx"""

    // Check md contents
    let f1md1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.md")
    let f2md2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.md")
    f1md1 |> shouldContainText """../folder2/in-folder2.md"""
    f2md2 |> shouldContainText """../folder1/in-folder1.md"""


// Same as above with relative input folder
[<Test>]
let ``Can build doc content using relative input path`` () =
    let rootOutputFolderAsGiven = __SOURCE_DIRECTORY__ </> "output1"

    let relativeInputFolderAsGiven =
        Path.GetRelativePath(System.Environment.CurrentDirectory, __SOURCE_DIRECTORY__ </> "files")

    if Directory.Exists(rootOutputFolderAsGiven) then
        Directory.Delete(rootOutputFolderAsGiven, true)

    let content =
        DocContent(
            rootOutputFolderAsGiven,
            Map.empty,
            lineNumbers = None,
            evaluate = false,
            substitutions = [],
            saveImages = None,
            watch = false,
            root = "https://github.com",
            crefResolver = (fun _ -> None),
            onError = failwith
        )

    let docModels = content.Convert(relativeInputFolderAsGiven, None, [])
    let globals = []

    for (_thing, action) in docModels do
        action globals

    // Check simple1.fsx --> simple1.html substititions
    // Check simple2.md --> simple2.html substititions
    let html1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.html")
    let html2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.html")
    let html3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.html")
    html1 |> shouldContainText """href="simple1.html">"""
    html1 |> shouldContainText """href="simple2.html">"""
    html1 |> shouldContainText """href="simple3.html">"""
    html2 |> shouldContainText """href="simple1.html">"""
    html2 |> shouldContainText """href="simple2.html">"""
    html2 |> shouldContainText """href="simple3.html">"""
    html3 |> shouldContainText """href="simple1.html">"""
    html3 |> shouldContainText """href="simple2.html">"""
    html3 |> shouldContainText """href="simple3.html">"""

    // Check simple1.fsx --> simple1.ipynb substititions
    // Check simple2.md --> simple1.ipynb substititions
    let ipynb1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.ipynb")
    let ipynb2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.ipynb")
    let ipynb3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.ipynb")
    ipynb1 |> shouldContainText "simple2.ipynb"
    ipynb1 |> shouldContainText "simple3.ipynb"
    ipynb2 |> shouldContainText "simple1.ipynb"
    ipynb3 |> shouldContainText "simple1.ipynb"

    // Check fsx exists
    // Check simple1.fsx --> simple1.fsx substititions
    // Check simple2.md --> simple1.fsx substititions
    let fsx1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.fsx")
    let fsx2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.fsx")
    let fsx3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.fsx")
    fsx1 |> shouldContainText "simple2.fsx"
    fsx1 |> shouldContainText "simple3.fsx"
    fsx2 |> shouldContainText "simple1.fsx"
    fsx3 |> shouldContainText "simple1.fsx"

    // Check md contents
    // Check simple1.fsx --> simple1.md substititions
    // Check simple2.md --> simple1.md substititions
    let md1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.md")
    let md2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.md")
    let md3 = File.ReadAllText(rootOutputFolderAsGiven </> "simple3.md")
    md1 |> shouldContainText "simple2.md"
    md1 |> shouldContainText "simple3.md"
    md2 |> shouldContainText "simple1.md"
    md3 |> shouldContainText "simple1.md"

    // Check in-folder1.fsx --> in-folder1.html substititions
    let f1html1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.html")
    let f2html2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.html")
    f1html1 |> shouldContainText """href="../folder2/in-folder2.html">"""
    f2html2 |> shouldContainText """href="../folder1/in-folder1.html">"""

    // Check in-folder1.fsx --> in-folder1.ipynb substititions
    let f1ipynb1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.ipynb")
    let f2ipynb2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.ipynb")
    f1ipynb1 |> shouldContainText """../folder2/in-folder2.ipynb"""
    f2ipynb2 |> shouldContainText """../folder1/in-folder1.ipynb"""

    // Check fsx exists
    let f1fsx1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.fsx")
    let f2fsx2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.fsx")
    f1fsx1 |> shouldContainText """../folder2/in-folder2.fsx"""
    f2fsx2 |> shouldContainText """../folder1/in-folder1.fsx"""

    // Check md contents
    let f1md1 = File.ReadAllText(rootOutputFolderAsGiven </> "folder1" </> "in-folder1.md")
    let f2md2 = File.ReadAllText(rootOutputFolderAsGiven </> "folder2" </> "in-folder2.md")
    f1md1 |> shouldContainText """../folder2/in-folder2.md"""
    f2md2 |> shouldContainText """../folder1/in-folder1.md"""

[<Test>]
let ``Parses frontmatter correctly `` () =
    let rootOutputFolderAsGiven = __SOURCE_DIRECTORY__ </> "previous-next-output"

    let relativeInputFolderAsGiven =
        Path.GetRelativePath(System.Environment.CurrentDirectory, __SOURCE_DIRECTORY__ </> "previous-next")

    if Directory.Exists(rootOutputFolderAsGiven) then
        Directory.Delete(rootOutputFolderAsGiven, true)

    let content =
        DocContent(
            rootOutputFolderAsGiven,
            Map.empty,
            lineNumbers = None,
            evaluate = false,
            substitutions = [],
            saveImages = None,
            watch = false,
            root = "https://en.wikipedia.org",
            crefResolver = (fun _ -> None),
            onError = failwith
        )

    let docModels = content.Convert(relativeInputFolderAsGiven, None, [])
    let globals = []

    for _thing, action in docModels do
        action globals

    let fellowshipHtml = rootOutputFolderAsGiven </> "fellowship.html" |> File.ReadAllText
    let twoTowersHtml = rootOutputFolderAsGiven </> "two-tower.html" |> File.ReadAllText
    let returnHtml = rootOutputFolderAsGiven </> "return.html" |> File.ReadAllText

    fellowshipHtml |> shouldContainText "<a href=\"two-tower.html\">Next</a>"
    twoTowersHtml |> shouldContainText "<a href=\"fellowship.html\">Previous</a>"
    twoTowersHtml |> shouldContainText "<a href=\"return.html\">Next</a>"
    returnHtml |> shouldContainText "<a href=\"two-tower.html\">Previous</a>"

[<Test>]
let ``Parses description and keywords from frontmatter `` () =
    let rootOutputFolderAsGiven = __SOURCE_DIRECTORY__ </> "output1"

    let relativeInputFolderAsGiven =
        Path.GetRelativePath(System.Environment.CurrentDirectory, __SOURCE_DIRECTORY__ </> "files")

    if Directory.Exists(rootOutputFolderAsGiven) then
        Directory.Delete(rootOutputFolderAsGiven, true)

    let content =
        DocContent(
            rootOutputFolderAsGiven,
            Map.empty,
            lineNumbers = None,
            evaluate = false,
            substitutions = [],
            saveImages = None,
            watch = false,
            root = "https://github.com",
            crefResolver = (fun _ -> None),
            onError = failwith
        )

    let docModels = content.Convert(relativeInputFolderAsGiven, None, [])

    let seoPageDocModel =
        docModels
        |> List.pick (fun (docInfo, _substitutions) ->
            match docInfo with
            | Some(_, _, docModel) when docModel.Title = "StringAnalyzer" -> Some(docModel)
            | _ -> None)

    let globals = []

    for _thing, action in docModels do
        action globals

    let meta =
        seoPageDocModel.Substitutions
        |> List.find (fst >> ((=) ParamKeys.``fsdocs-meta-tags``))
        |> snd

    Assert.That(meta, Does.Contain("<meta name=\"description\""))
    Assert.That(meta, Does.Contain("Great description about StringAnalyzer!"))
    Assert.That(meta, Does.Contain("fsharp, analyzers, tooling"))

(* Cannot get this test to evaluate the notebook
[<Test>]
let ``ipynb notebook evaluates`` () =
    let rootOutputFolderAsGiven = __SOURCE_DIRECTORY__ </> "ipynb-eval-output"
    let rootInputFolderAsGiven = __SOURCE_DIRECTORY__ </> "ipynb-eval"

    if Directory.Exists(rootOutputFolderAsGiven) then
        Directory.Delete(rootOutputFolderAsGiven, true)

    let content =
        DocContent(
            rootOutputFolderAsGiven,
            Map.empty,
            lineNumbers = None,
            evaluate = true,
            substitutions = [],
            saveImages = None,
            watch = false,
            root = "https://github.com",
            crefResolver = (fun _ -> None),
            onError = failwith
        )

    let docModels = content.Convert(rootInputFolderAsGiven, None, [])
    let globals = []

    for (_thing, action) in docModels do
        action globals

    let ipynbOut = rootOutputFolderAsGiven </> "eval.html" |> File.ReadAllText

    ipynbOut |> shouldContainText "10007"
*)

// --------------------------------------------------------------------------------------
// Tests for GetNavigationEntriesFactory (pre-computed navigation structure, PR #1129)
// --------------------------------------------------------------------------------------

/// Build a minimal LiterateDocModel entry for navigation tests.
let private makeNavDocModel
    (title: string)
    (inputPath: string)
    (category: string option)
    (categoryIndex: int option)
    (index: int option)
    =
    (inputPath,
     false,
     { Title = title
       Substitutions = []
       IndexText = None
       Category = category
       CategoryIndex = categoryIndex
       Index = index
       OutputPath = Path.GetFileNameWithoutExtension(inputPath) + ".html"
       OutputKind = OutputKind.Html
       IsActive = false })

/// DocContent instance whose root URL is empty string (URIs = bare output paths).
let private makeDocContentForNav () =
    DocContent(
        Path.GetTempPath(),
        Map.empty,
        lineNumbers = None,
        evaluate = false,
        substitutions = [],
        saveImages = None,
        watch = false,
        root = "",
        crefResolver = (fun _ -> None),
        onError = failwith
    )

// Use the test source directory as the `input` folder; it contains no
// _menu_template.html / _menu-item_template.html files, so Menu.isTemplatingAvailable
// returns false and we exercise the built-in HTML generation path.
let private navInput = __SOURCE_DIRECTORY__

[<Test>]
let ``GetNavigationEntriesFactory - empty docModels produces empty string`` () =
    let dc = makeDocContentForNav ()
    let factory = dc.GetNavigationEntriesFactory(navInput, [], ignoreUncategorized = false)
    factory None |> shouldEqual ""

[<Test>]
let ``GetNavigationEntriesFactory - single uncategorized model renders Documentation header`` () =
    let dc = makeDocContentForNav ()
    let models = [ makeNavDocModel "Getting Started" "/docs/getting-started.md" None None None ]
    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let html = factory None
    html |> shouldContainText "Documentation"
    html |> shouldContainText "getting-started.html"

[<Test>]
let ``GetNavigationEntriesFactory - None currentPagePath marks no item as active`` () =
    let dc = makeDocContentForNav ()
    let models = [ makeNavDocModel "Page 1" "/docs/page1.md" None None None ]
    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    factory None |> shouldNotContainText "active"

[<Test>]
let ``GetNavigationEntriesFactory - matching currentPagePath marks item as active`` () =
    let dc = makeDocContentForNav ()
    let models = [ makeNavDocModel "Page 1" "/docs/page1.md" None None None ]
    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    factory (Some "/docs/page1.md") |> shouldContainText "active"

[<Test>]
let ``GetNavigationEntriesFactory - non-matching currentPagePath does not mark item as active`` () =
    let dc = makeDocContentForNav ()
    let models = [ makeNavDocModel "Page 1" "/docs/page1.md" None None None ]
    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    factory (Some "/docs/other.md") |> shouldNotContainText "active"

[<Test>]
let ``GetNavigationEntriesFactory - exactly one item is active among multiple pages`` () =
    let dc = makeDocContentForNav ()

    let models =
        [ makeNavDocModel "Page 1" "/docs/page1.md" None None None
          makeNavDocModel "Page 2" "/docs/page2.md" None None None
          makeNavDocModel "Page 3" "/docs/page3.md" None None None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let html = factory (Some "/docs/page2.md")
    // "nav-item active" should appear exactly once
    let activeCount = html.Split([| "nav-item active" |], System.StringSplitOptions.None).Length - 1

    activeCount |> shouldEqual 1

[<Test>]
let ``GetNavigationEntriesFactory - excludes isOtherLang models`` () =
    let dc = makeDocContentForNav ()

    let otherLangModel =
        ("/docs/other-lang.md",
         true, // isOtherLang = true
         { Title = "Other Language Page"
           Substitutions = []
           IndexText = None
           Category = None
           CategoryIndex = None
           Index = None
           OutputPath = "other-lang.html"
           OutputKind = OutputKind.Html
           IsActive = false })

    let factory = dc.GetNavigationEntriesFactory(navInput, [ otherLangModel ], ignoreUncategorized = false)
    factory None |> shouldNotContainText "Other Language Page"

[<Test>]
let ``GetNavigationEntriesFactory - excludes non-HTML output models`` () =
    let dc = makeDocContentForNav ()

    let latexModel =
        ("/docs/report.md",
         false,
         { Title = "LaTeX Report"
           Substitutions = []
           IndexText = None
           Category = None
           CategoryIndex = None
           Index = None
           OutputPath = "report.tex"
           OutputKind = OutputKind.Latex
           IsActive = false })

    let factory = dc.GetNavigationEntriesFactory(navInput, [ latexModel ], ignoreUncategorized = false)
    factory None |> shouldNotContainText "LaTeX Report"

[<Test>]
let ``GetNavigationEntriesFactory - excludes files named index`` () =
    let dc = makeDocContentForNav ()

    let models =
        [ makeNavDocModel "Home" "/docs/index.md" None None None
          makeNavDocModel "Guide" "/docs/guide.md" None None None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let html = factory None
    html |> shouldNotContainText "Home"
    html |> shouldContainText "Guide"

[<Test>]
let ``GetNavigationEntriesFactory - ignoreUncategorized true excludes uncategorized models`` () =
    let dc = makeDocContentForNav ()

    let models =
        [ makeNavDocModel "Categorized Doc" "/docs/cat.md" (Some "Tutorials") None None
          makeNavDocModel "Uncategorized Doc" "/docs/uncat.md" None None None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = true)
    let html = factory None
    html |> shouldContainText "Categorized Doc"
    html |> shouldNotContainText "Uncategorized Doc"

[<Test>]
let ``GetNavigationEntriesFactory - ignoreUncategorized false includes all models`` () =
    let dc = makeDocContentForNav ()

    let models =
        [ makeNavDocModel "Categorized Doc" "/docs/cat.md" (Some "Tutorials") None None
          makeNavDocModel "Uncategorized Doc" "/docs/uncat.md" None None None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let html = factory None
    html |> shouldContainText "Categorized Doc"
    html |> shouldContainText "Uncategorized Doc"

[<Test>]
let ``GetNavigationEntriesFactory - categories are ordered by CategoryIndex`` () =
    let dc = makeDocContentForNav ()

    // Beta has CategoryIndex 2, Alpha has CategoryIndex 1 → Alpha should appear first
    let models =
        [ makeNavDocModel "Beta Doc" "/docs/b.md" (Some "Beta") (Some 2) None
          makeNavDocModel "Alpha Doc" "/docs/a.md" (Some "Alpha") (Some 1) None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let html = factory None
    let alphaIdx = html.IndexOf("Alpha", System.StringComparison.Ordinal)
    let betaIdx = html.IndexOf("Beta", System.StringComparison.Ordinal)
    Assert.That(alphaIdx, Is.LessThan(betaIdx))

[<Test>]
let ``GetNavigationEntriesFactory - items within a category are ordered by Index`` () =
    let dc = makeDocContentForNav ()

    // "Second" has Index 2, "First" has Index 1 → "First" should appear first
    let models =
        [ makeNavDocModel "Second Item" "/docs/second.md" (Some "Guides") None (Some 2)
          makeNavDocModel "First Item" "/docs/first.md" (Some "Guides") None (Some 1) ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let html = factory None
    let firstIdx = html.IndexOf("First Item", System.StringComparison.Ordinal)
    let secondIdx = html.IndexOf("Second Item", System.StringComparison.Ordinal)
    Assert.That(firstIdx, Is.LessThan(secondIdx))

[<Test>]
let ``GetNavigationEntriesFactory - calling factory multiple times returns identical results`` () =
    let dc = makeDocContentForNav ()

    let models =
        [ makeNavDocModel "Page 1" "/docs/page1.md" None None None
          makeNavDocModel "Page 2" "/docs/page2.md" None None None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    factory None |> shouldEqual (factory None)

[<Test>]
let ``GetNavigationEntriesFactory - successive calls with different page paths set correct active state`` () =
    let dc = makeDocContentForNav ()

    let models =
        [ makeNavDocModel "Page 1" "/docs/page1.md" None None None
          makeNavDocModel "Page 2" "/docs/page2.md" None None None ]

    let factory = dc.GetNavigationEntriesFactory(navInput, models, ignoreUncategorized = false)
    let htmlPage1 = factory (Some "/docs/page1.md")
    let htmlPage2 = factory (Some "/docs/page2.md")

    // The two views differ
    htmlPage1 |> shouldNotEqual htmlPage2

    // Each view has exactly one active item
    let countActive (html: string) =
        html.Split([| "nav-item active" |], System.StringSplitOptions.None).Length - 1

    countActive htmlPage1 |> shouldEqual 1
    countActive htmlPage2 |> shouldEqual 1

// --------------------------------------------------------------------------------------
// Tests for LlmsTxt module (FsDocsGenerateLlmsTxt MSBuild property, on by default)
// --------------------------------------------------------------------------------------

open FSharp.Formatting.ApiDocs

let makeEntry t title uri content =
    { uri = uri
      title = title
      content = content
      headings = []
      ``type`` = t }

[<Test>]
let ``LlmsTxt buildContent produces correct header`` () =
    let llmsTxt, llmsFullTxt = LlmsTxt.buildContent "MyProject" [||] false false
    llmsTxt |> shouldContainText "# MyProject\n\n"
    llmsFullTxt |> shouldContainText "# MyProject\n\n"

[<Test>]
let ``LlmsTxt buildContent with no entries produces header only`` () =
    let llmsTxt, llmsFullTxt = LlmsTxt.buildContent "MyProject" [||] false false
    llmsTxt |> shouldEqual "# MyProject\n\n"
    llmsFullTxt |> shouldEqual "# MyProject\n\n"

[<Test>]
let ``LlmsTxt buildContent separates Docs and API Reference sections`` () =
    let entries =
        [| makeEntry "content" "Getting Started" "https://example.com/docs/getting-started" "Some intro text"
           makeEntry "apiDocs" "MyModule.MyType" "https://example.com/reference/mytype" "Type docs" |]

    let llmsTxt, _ = LlmsTxt.buildContent "MyProject" entries false false
    llmsTxt |> shouldContainText "## Docs"
    llmsTxt |> shouldContainText "## API Reference"

    llmsTxt
    |> shouldContainText "- [Getting Started](https://example.com/docs/getting-started)"

    llmsTxt
    |> shouldContainText "- [MyModule.MyType](https://example.com/reference/mytype)"

[<Test>]
let ``LlmsTxt llms.txt does not include content body`` () =
    let entries =
        [| makeEntry "content" "Getting Started" "https://example.com/docs/getting-started" "Detailed page content here" |]

    let llmsTxt, _ = LlmsTxt.buildContent "MyProject" entries false false
    llmsTxt |> shouldNotContainText "Detailed page content here"

[<Test>]
let ``LlmsTxt llms-full.txt includes content body`` () =
    let entries =
        [| makeEntry "content" "Getting Started" "https://example.com/docs/getting-started" "Detailed page content here" |]

    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    llmsFullTxt |> shouldContainText "Detailed page content here"

[<Test>]
let ``LlmsTxt llms-full.txt skips blank content`` () =
    let entries = [| makeEntry "apiDocs" "MyModule" "https://example.com/reference/mymodule" "   " |]

    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    // Full file uses heading format per entry
    llmsFullTxt
    |> shouldContainText "### [MyModule](https://example.com/reference/mymodule)"
    // Blank content should not produce extra blank lines beyond the heading line
    llmsFullTxt.Contains("   ") |> shouldEqual false

[<Test>]
let ``LlmsTxt omits Docs section when no content entries exist`` () =
    let entries = [| makeEntry "apiDocs" "MyModule" "https://example.com/reference/mymodule" "" |]
    let llmsTxt, _ = LlmsTxt.buildContent "MyProject" entries false false
    llmsTxt |> shouldNotContainText "## Docs"
    llmsTxt |> shouldContainText "## API Reference"

[<Test>]
let ``LlmsTxt omits API Reference section when no apiDocs entries exist`` () =
    let entries = [| makeEntry "content" "Guide" "https://example.com/docs/guide" "" |]
    let llmsTxt, _ = LlmsTxt.buildContent "MyProject" entries false false
    llmsTxt |> shouldContainText "## Docs"
    llmsTxt |> shouldNotContainText "## API Reference"

[<Test>]
let ``LlmsTxt llms-full.txt uses heading format per entry`` () =
    let entries = [| makeEntry "content" "Getting Started" "https://example.com/docs/getting-started" "Some content" |]

    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false

    llmsFullTxt
    |> shouldContainText "### [Getting Started](https://example.com/docs/getting-started)"

    llmsFullTxt |> shouldNotContainText "- [Getting Started]"

[<Test>]
let ``LlmsTxt llms-full.txt decodes HTML entities in content`` () =
    let entries =
        [| makeEntry
               "content"
               "Guide"
               "https://example.com/docs/guide"
               "use &quot;double quotes&quot; and &gt; greater-than" |]

    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    llmsFullTxt |> shouldContainText "use \"double quotes\" and > greater-than"
    llmsFullTxt |> shouldNotContainText "&quot;"

[<Test>]
let ``LlmsTxt llms-full.txt strips eval warning lines from content`` () =
    let content = "Some text\nWarning: Output, it-value and value references require --eval\nMore text"

    let entries = [| makeEntry "content" "Guide" "https://example.com/docs/guide" content |]
    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    llmsFullTxt |> shouldNotContainText "--eval"
    llmsFullTxt |> shouldContainText "Some text"
    llmsFullTxt |> shouldContainText "More text"

[<Test>]
let ``LlmsTxt llms.txt excludes per-member API entries (URIs with hash)`` () =
    let entries =
        [| makeEntry "apiDocs" "MyModule" "https://example.com/reference/mymodule.html" "module docs"
           makeEntry
               "apiDocs"
               "MyModule.myFunction"
               "https://example.com/reference/mymodule.html#myFunction"
               "member docs" |]

    let llmsTxt, _ = LlmsTxt.buildContent "MyProject" entries false false

    llmsTxt
    |> shouldContainText "- [MyModule](https://example.com/reference/mymodule.html)"

    llmsTxt |> shouldNotContainText "myFunction"

[<Test>]
let ``LlmsTxt llms-full.txt includes per-member API entries`` () =
    let entries =
        [| makeEntry "apiDocs" "MyModule" "https://example.com/reference/mymodule.html" "module docs"
           makeEntry
               "apiDocs"
               "MyModule.myFunction"
               "https://example.com/reference/mymodule.html#myFunction"
               "member docs" |]

    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    llmsFullTxt |> shouldContainText "myFunction"

[<Test>]
let ``LlmsTxt normalises multi-line titles to single-line`` () =
    let entries = [| makeEntry "content" "Fantomas\n" "https://example.com/docs/index.html" "Some content" |]

    let llmsTxt, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    // Title must be on a single line — no embedded newline in the link text
    llmsTxt |> shouldContainText "- [Fantomas](https://example.com/docs/index.html)"

    llmsFullTxt
    |> shouldContainText "### [Fantomas](https://example.com/docs/index.html)"

    llmsTxt |> shouldNotContainText "Fantomas\n"

[<Test>]
let ``LlmsTxt collapses excessive blank lines in content`` () =
    let content = "First paragraph\n\n\n\n\nSecond paragraph"

    let entries = [| makeEntry "content" "Guide" "https://example.com/docs/guide" content |]
    let _, llmsFullTxt = LlmsTxt.buildContent "MyProject" entries false false
    // Should not contain 3 or more consecutive newlines
    llmsFullTxt.Contains("\n\n\n") |> shouldEqual false
    llmsFullTxt |> shouldContainText "First paragraph"
    llmsFullTxt |> shouldContainText "Second paragraph"
