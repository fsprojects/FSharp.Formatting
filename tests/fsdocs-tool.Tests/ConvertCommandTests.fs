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
    html |> shouldNotContainText "fsdocs-tip"

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

// --------------------------------------------------------------------------------------
// Unit tests for resource embedding (ConvertHelpers.embedResourcesInHtml)
// --------------------------------------------------------------------------------------

/// Helper: create a unique temp directory for each test.
let private mkTempDir (name: string) =
    let dir = Path.Combine(Path.GetTempPath(), "fsdocs-embed-tests", name)
    Directory.CreateDirectory(dir) |> ignore
    dir

[<Test>]
let ``embedResourcesInHtml inlines local CSS stylesheet`` () =
    let dir = mkTempDir "css"
    let cssFile = dir </> "style.css"
    File.WriteAllText(cssFile, "body { color: red; }")

    let template = dir </> "_template.html"

    File.WriteAllText(
        template,
        """<!DOCTYPE html><html><head><link rel="stylesheet" href="style.css"></head><body>{{fsdocs-content}}</body></html>"""
    )

    let input = dir </> "test.md"
    File.WriteAllText(input, "# Hello")
    let output = dir </> "out.html"

    let cmd = ConvertCommand()
    cmd.input <- input
    cmd.output <- output
    cmd.template <- template
    let result = cmd.Execute()

    result |> shouldEqual 0
    let html = File.ReadAllText(output)
    html |> shouldContainText "<style>body { color: red; }</style>"
    html |> shouldNotContainText "href=\"style.css\""

[<Test>]
let ``embedResourcesInHtml inlines local JavaScript`` () =
    let dir = mkTempDir "js"
    let jsFile = dir </> "app.js"
    File.WriteAllText(jsFile, "console.log('hi');")

    let template = dir </> "_template.html"

    File.WriteAllText(
        template,
        """<!DOCTYPE html><html><head><script src="app.js"></script></head><body>{{fsdocs-content}}</body></html>"""
    )

    let input = dir </> "test.md"
    File.WriteAllText(input, "# Hello")
    let output = dir </> "out.html"

    let cmd = ConvertCommand()
    cmd.input <- input
    cmd.output <- output
    cmd.template <- template
    let result = cmd.Execute()

    result |> shouldEqual 0
    let html = File.ReadAllText(output)
    html |> shouldContainText "<script>console.log('hi');</script>"
    html |> shouldNotContainText "src=\"app.js\""

[<Test>]
let ``embedResourcesInHtml leaves remote URLs unchanged`` () =
    let dir = mkTempDir "remote"

    let template = dir </> "_template.html"

    File.WriteAllText(
        template,
        """<!DOCTYPE html><html><head><link rel="stylesheet" href="https://cdn.example.com/styles.css"><script src="https://cdn.example.com/app.js"></script></head><body>{{fsdocs-content}}</body></html>"""
    )

    let input = dir </> "test.md"
    File.WriteAllText(input, "# Hello")
    let output = dir </> "out.html"

    let cmd = ConvertCommand()
    cmd.input <- input
    cmd.output <- output
    cmd.template <- template
    let result = cmd.Execute()

    result |> shouldEqual 0
    let html = File.ReadAllText(output)
    html |> shouldContainText "https://cdn.example.com/styles.css"
    html |> shouldContainText "https://cdn.example.com/app.js"

[<Test>]
let ``embedResourcesInHtml skips embedding when --no-embed-resources is set`` () =
    let dir = mkTempDir "noembed"
    let cssFile = dir </> "style.css"
    File.WriteAllText(cssFile, "body { color: blue; }")

    let template = dir </> "_template.html"

    File.WriteAllText(
        template,
        """<!DOCTYPE html><html><head><link rel="stylesheet" href="style.css"></head><body>{{fsdocs-content}}</body></html>"""
    )

    let input = dir </> "test.md"
    File.WriteAllText(input, "# Hello")
    let output = dir </> "out.html"

    let cmd = ConvertCommand()
    cmd.input <- input
    cmd.output <- output
    cmd.template <- template
    cmd.noEmbedResources <- true
    let result = cmd.Execute()

    result |> shouldEqual 0
    let html = File.ReadAllText(output)
    html |> shouldContainText "href=\"style.css\""
    html |> shouldNotContainText "<style>body { color: blue; }</style>"
