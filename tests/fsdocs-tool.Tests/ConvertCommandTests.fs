module fsdocs.Tests.ConvertCommand

open System.IO
open fsdocs
open NUnit.Framework
open FsUnitTyped

do FSharp.Formatting.TestHelpers.enableLogging ()

let (</>) a b = Path.Combine(a, b)

let literateTestFiles = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", "FSharp.Literate.Tests", "files"))

// --------------------------------------------------------------------------------------
// Integration tests for ConvertCommand
// --------------------------------------------------------------------------------------

[<Test>]
let ``ConvertCommand converts .md file to HTML`` () =
    let inputFile = literateTestFiles </> "simple2.md"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "simple2-convert.html"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    cmd.outputFormat <- "html"
    let result = cmd.Execute()

    result |> shouldEqual 0
    File.Exists(outputFile) |> shouldEqual true
    let html = File.ReadAllText(outputFile)
    html |> shouldContainText "Heading"
    html |> shouldContainText "Code sample"

[<Test>]
let ``ConvertCommand converts .fsx file to HTML`` () =
    let inputFile = literateTestFiles </> "simple1.fsx"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "simple1-convert.html"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    cmd.outputFormat <- "html"
    let result = cmd.Execute()

    result |> shouldEqual 0
    File.Exists(outputFile) |> shouldEqual true
    let html = File.ReadAllText(outputFile)
    html |> shouldContainText "Heading"
    html |> shouldContainText "Code sample"

[<Test>]
let ``ConvertCommand omits fsdocs-tip divs when no template given`` () =
    let inputFile = literateTestFiles </> "simple1.fsx"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "simple1-no-template.html"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    cmd.outputFormat <- "html"
    // no template set
    let result = cmd.Execute()

    result |> shouldEqual 0
    let html = File.ReadAllText(outputFile)
    // Tooltip trigger spans use data-fsdocs-tip attributes; the assertion checks that
    // the tooltip *div* elements (class="fsdocs-tip") are NOT emitted without a template.
    html |> shouldNotContainText "class=\"fsdocs-tip\""

[<Test>]
let ``ConvertCommand converts .ipynb file to HTML`` () =
    let inputFile = literateTestFiles </> "simple3.ipynb"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "simple3-convert.html"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    cmd.outputFormat <- "html"
    let result = cmd.Execute()

    result |> shouldEqual 0
    File.Exists(outputFile) |> shouldEqual true
    let html = File.ReadAllText(outputFile)
    html |> shouldContainText "Heading"
    html |> shouldContainText "Code sample"

[<Test>]
let ``ConvertCommand converts .md file to Markdown output format`` () =
    let inputFile = literateTestFiles </> "simple2.md"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "simple2-convert.md"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    cmd.outputFormat <- "markdown"
    let result = cmd.Execute()

    result |> shouldEqual 0
    File.Exists(outputFile) |> shouldEqual true

[<Test>]
let ``ConvertCommand infers output format from output file extension`` () =
    let inputFile = literateTestFiles </> "simple2.md"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "simple2-inferred.md"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    // no outputFormat set — should infer "markdown" from .md extension
    let result = cmd.Execute()

    result |> shouldEqual 0
    File.Exists(outputFile) |> shouldEqual true

[<Test>]
let ``ConvertCommand returns error code for non-existent input file`` () =
    let cmd = ConvertCommand()
    cmd.input <- literateTestFiles </> "does-not-exist.md"
    cmd.output <- Path.GetTempPath() </> "fsdocs-tool-tests" </> "out.html"
    let result = cmd.Execute()
    result |> shouldEqual 1

[<Test>]
let ``ConvertCommand returns error code for unsupported file extension`` () =
    let inputFile = literateTestFiles </> "template.html"
    let outputFile = Path.GetTempPath() </> "fsdocs-tool-tests" </> "out.html"
    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)) |> ignore

    let cmd = ConvertCommand()
    cmd.input <- inputFile
    cmd.output <- outputFile
    let result = cmd.Execute()
    result |> shouldEqual 1
