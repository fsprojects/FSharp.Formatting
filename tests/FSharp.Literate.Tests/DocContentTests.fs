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
            fsiEvaluator = None,
            substitutions = [],
            saveImages = None,
            watch = false,
            root = "https://github.com",
            crefResolver = (fun _ -> None)
        )

    let docModels = content.Convert(rootInputFolderAsGiven, None, [])
    let globals = []

    for (_thing, action) in docModels do
        action globals

    // Check simple1.fsx --> simple1.html substititions
    let html1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.html")
    let html2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.html")
    html1 |> shouldContainText "simple2.html"
    html2 |> shouldContainText "simple1.html"

    // Check simple1.fsx --> simple1.ipynb substititions
    let ipynb1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.ipynb")
    let ipynb2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.ipynb")
    ipynb1 |> shouldContainText "simple2.ipynb"
    ipynb2 |> shouldContainText "simple1.ipynb"

    // Check fsx contents
    let _fsx1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.fsx")
    let _fsx2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.fsx")

    // Check md contents
    let md1 = File.ReadAllText(rootOutputFolderAsGiven </> "simple1.md")
    let md2 = File.ReadAllText(rootOutputFolderAsGiven </> "simple2.md")
    md1 |> shouldContainText "simple2.md"
    md2 |> shouldContainText "simple1.md"
