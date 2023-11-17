module FSharp.Literate.Tests.DocContent

open System.IO
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
