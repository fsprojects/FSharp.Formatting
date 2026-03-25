module FSharp.Literate.Tests.DocContent

open System.IO
open FSharp.Formatting.Literate
open FSharp.Formatting.Templating
open fsdocs
open NUnit.Framework
open FsUnitTyped
open FSharp.Formatting.Literate

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

    StringAssert.Contains("<meta name=\"description\"", meta)
    StringAssert.Contains("Great description about StringAnalyzer!", meta)
    StringAssert.Contains("fsharp, analyzers, tooling", meta)

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

// --------------------------------------------------------------------------------------
// Tests for FrontMatterFile.ParseFromLines
// --------------------------------------------------------------------------------------

[<Test>]
let ``FrontMatterFile.ParseFromLines parses standard YAML front-matter`` () =
    let lines =
        seq {
            "---"
            "category: Basics"
            "categoryindex: 1"
            "index: 2"
            "---"
            "# Title"
        }

    let result = FrontMatterFile.ParseFromLines "test.md" lines
    result |> shouldNotEqual None
    let fm = result.Value
    fm.FileName |> shouldEqual "test.md"
    fm.Category |> shouldEqual "Basics"
    fm.CategoryIndex |> shouldEqual 1
    fm.Index |> shouldEqual 2

[<Test>]
let ``FrontMatterFile.ParseFromLines preserves colons in category values`` () =
    // Regression test for the fix in PR #1105 — previously only the part before the
    // second colon was kept, so "F#: Intro" would be captured as "F#".
    let lines =
        seq {
            "---"
            "category: F#: An Introduction"
            "categoryindex: 1"
            "index: 1"
            "---"
        }

    let result = FrontMatterFile.ParseFromLines "test.md" lines
    result |> shouldNotEqual None
    result.Value.Category |> shouldEqual "F#: An Introduction"

[<Test>]
let ``FrontMatterFile.ParseFromLines returns None when category is missing`` () =
    let lines =
        seq {
            "---"
            "categoryindex: 1"
            "index: 2"
            "---"
        }

    FrontMatterFile.ParseFromLines "test.md" lines |> shouldEqual None

[<Test>]
let ``FrontMatterFile.ParseFromLines returns None when categoryindex is missing`` () =
    let lines =
        seq {
            "---"
            "category: Basics"
            "index: 1"
            "---"
        }

    FrontMatterFile.ParseFromLines "test.md" lines |> shouldEqual None

[<Test>]
let ``FrontMatterFile.ParseFromLines returns None when index is missing`` () =
    let lines =
        seq {
            "---"
            "category: Basics"
            "categoryindex: 1"
            "---"
        }

    FrontMatterFile.ParseFromLines "test.md" lines |> shouldEqual None

[<Test>]
let ``FrontMatterFile.ParseFromLines returns None when categoryindex is non-numeric`` () =
    let lines =
        seq {
            "---"
            "category: Basics"
            "categoryindex: abc"
            "index: 1"
            "---"
        }

    FrontMatterFile.ParseFromLines "test.md" lines |> shouldEqual None

[<Test>]
let ``FrontMatterFile.ParseFromLines parses fsx-style front-matter`` () =
    let lines =
        seq {
            "(**"
            "category: Scripting"
            "categoryindex: 2"
            "index: 3"
            "*)"
        }

    let result = FrontMatterFile.ParseFromLines "test.fsx" lines
    result |> shouldNotEqual None
    let fm = result.Value
    fm.FileName |> shouldEqual "test.fsx"
    fm.Category |> shouldEqual "Scripting"
    fm.CategoryIndex |> shouldEqual 2
    fm.Index |> shouldEqual 3

[<Test>]
let ``FrontMatterFile.ParseFromLines trims whitespace from category`` () =
    let lines =
        seq {
            "---"
            "category:  Basics with spaces  "
            "categoryindex: 1"
            "index: 1"
            "---"
        }

    let result = FrontMatterFile.ParseFromLines "test.md" lines
    result |> shouldNotEqual None
    result.Value.Category |> shouldEqual "Basics with spaces"

[<Test>]
let ``FrontMatterFile.ParseFromLines returns None for empty input`` () =
    FrontMatterFile.ParseFromLines "test.md" Seq.empty |> shouldEqual None

// --------------------------------------------------------------------------------------
// Tests for GetNavigationEntries — flat and nested categories
// --------------------------------------------------------------------------------------

/// Builds a minimal LiterateDocModel for use in navigation tests.
let private makeNavModel path (category: string option) (categoryIndex: int option) (index: int option) title =
    { Title = title
      Substitutions = []
      IndexText = None
      Category = category
      CategoryIndex = categoryIndex
      Index = index
      OutputPath = path
      OutputKind = OutputKind.Html
      IsActive = false }

/// Constructs a DocContent instance suitable for navigation-only tests
/// (no actual I/O or evaluation required).
let private makeDocContentForNav () =
    DocContent(
        Path.GetTempPath(),
        Map.empty,
        lineNumbers = None,
        evaluate = false,
        substitutions = [],
        saveImages = None,
        watch = false,
        root = "/",
        crefResolver = (fun _ -> None),
        onError = failwith
    )

/// Returns a temp directory path that contains no template files.
let private noTemplateDir () = Path.GetTempPath()

[<Test>]
let ``GetNavigationEntries with no category emits Documentation header`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/doc1.md", false, makeNavModel "doc1.html" None None (Some 1) "Doc One"
          "/input/doc2.md", false, makeNavModel "doc2.html" None None (Some 2) "Doc Two" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    result |> shouldContainText "Documentation"
    result |> shouldContainText "doc1.html"
    result |> shouldContainText "doc2.html"

[<Test>]
let ``GetNavigationEntries with flat categories emits nav-header for each category`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/doc1.md", false, makeNavModel "doc1.html" (Some "API") (Some 1) (Some 1) "Doc One"
          "/input/doc2.md", false, makeNavModel "doc2.html" (Some "Guides") (Some 2) (Some 1) "Doc Two" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    result |> shouldContainText "nav-header"
    result |> shouldContainText "API"
    result |> shouldContainText "Guides"
    result |> shouldNotContainText "nav-sub-header"

[<Test>]
let ``GetNavigationEntries with nested categories emits nav-sub-header`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/arrays.md", false, makeNavModel "arrays.html" (Some "Collections/Arrays") (Some 1) (Some 1) "Arrays"
          "/input/lists.md", false, makeNavModel "lists.html" (Some "Collections/Lists") (Some 2) (Some 1) "Lists" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    result |> shouldContainText "nav-header"
    result |> shouldContainText "Collections"
    result |> shouldContainText "nav-sub-header"
    result |> shouldContainText "Arrays"
    result |> shouldContainText "Lists"

[<Test>]
let ``GetNavigationEntries with nested categories does not emit parent name as nav-sub-header`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/arrays.md", false, makeNavModel "arrays.html" (Some "Collections/Arrays") (Some 1) (Some 1) "Arrays" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    // "Collections" is the parent → nav-header, not nav-sub-header
    result |> shouldContainText """class="nav-header"""
    // Verify that Collections appears somewhere in the output
    result |> shouldContainText "Collections"

[<Test>]
let ``GetNavigationEntries with nested categories orders parents by minimum CategoryIndex`` () =
    let content = makeDocContentForNav ()

    // Reference group has categoryindex: 1; Collections has categoryindex: 2
    // So Reference should appear first in the navigation
    let models =
        [ "/input/arrays.md", false, makeNavModel "arrays.html" (Some "Collections/Arrays") (Some 2) (Some 1) "Arrays"
          "/input/intro.md", false, makeNavModel "intro.html" (Some "Reference/Intro") (Some 1) (Some 1) "Intro" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    let refPos = result.IndexOf("Reference")
    let colPos = result.IndexOf("Collections")
    refPos |> shouldBeGreaterThan -1
    colPos |> shouldBeGreaterThan -1
    // Reference (min catIdx 1) should come before Collections (min catIdx 2)
    refPos |> shouldBeSmallerThan colPos

[<Test>]
let ``GetNavigationEntries with mixed nested and flat categories renders both`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/arrays.md", false, makeNavModel "arrays.html" (Some "Collections/Arrays") (Some 1) (Some 1) "Arrays"
          "/input/getting-started.md",
          false,
          makeNavModel "getting-started.html" (Some "Collections") (Some 1) (Some 2) "Getting Started" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    result |> shouldContainText "nav-header"
    result |> shouldContainText "Collections"
    result |> shouldContainText "Arrays"
    result |> shouldContainText "Getting Started"

[<Test>]
let ``GetNavigationEntries marks current page as active`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/doc1.md", false, makeNavModel "doc1.html" (Some "API") (Some 1) (Some 1) "Doc One"
          "/input/doc2.md", false, makeNavModel "doc2.html" (Some "API") (Some 1) (Some 2) "Doc Two" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, Some "/input/doc1.md", false)
    // doc1's nav-item should have the "active" class
    result |> shouldContainText "active"

[<Test>]
let ``GetNavigationEntries with ignoreUncategorized excludes uncategorized docs`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/doc1.md", false, makeNavModel "doc1.html" (Some "API") (Some 1) (Some 1) "Doc One"
          "/input/doc2.md", false, makeNavModel "doc2.html" None None None "Uncategorized Doc" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, true)
    result |> shouldContainText "Doc One"
    result |> shouldNotContainText "Uncategorized Doc"

[<Test>]
let ``GetNavigationEntries index files are excluded from navigation`` () =
    let content = makeDocContentForNav ()

    let models =
        [ "/input/index.md", false, makeNavModel "index.html" (Some "API") (Some 1) (Some 1) "Index Page"
          "/input/doc1.md", false, makeNavModel "doc1.html" (Some "API") (Some 1) (Some 2) "Doc One" ]

    let result = content.GetNavigationEntries(noTemplateDir (), models, None, false)
    // index.md should be excluded from navigation
    result |> shouldNotContainText "Index Page"
    result |> shouldContainText "Doc One"
