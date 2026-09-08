module fsdocs.Tests.Site

open System
open System.IO
open System.Text
open fsdocs
open NUnit.Framework
open FsUnitTyped
open FSharp.Formatting.Literate

let (</>) a b = Path.Combine(a, b)

/// A fresh docs folder with a few pages, an extras folder and a template without API keys.
type internal Fixture() =
    let root =
        Path.GetTempPath()
        </> "fsdocs-tool-tests"
        </> "site-" + Guid.NewGuid().ToString("N")

    let input = root </> "docs"
    let extras = root </> "extras"
    let project = root </> "Lib.fsproj"

    do
        Directory.CreateDirectory input |> ignore
        Directory.CreateDirectory(input </> "sub") |> ignore
        Directory.CreateDirectory extras |> ignore

        File.WriteAllText(
            input </> "_template.html",
            "<html><body><nav>{{fsdocs-list-of-documents}}</nav><main>{{fsdocs-content}}</main><footer>{{fsdocs-collection-name}}</footer></body></html>"
        )

        File.WriteAllText(input </> "index.md", "# Home\n\nWelcome.\n")

        File.WriteAllText(
            input </> "a.md",
            "---\ncategory: Docs\ncategoryindex: 1\nindex: 1\n---\n\n# Page A\n\nText of A.\n"
        )

        File.WriteAllText(
            input </> "b.fsx",
            "(**\n---\ncategory: Docs\ncategoryindex: 1\nindex: 2\n---\n*)\n#load \"lib.fsx\"\n(**\n# Page B\n*)\nlet y = Lib.x + 1\n"
        )

        File.WriteAllText(input </> "lib.fsx", "module Lib\nlet x = 1\n")
        File.WriteAllText(input </> "style.css", "body { }\n")
        File.WriteAllText(input </> ".hidden.md", "# Hidden\n")
        File.WriteAllText(input </> "sub" </> "c.md", "# Page C\n")
        File.WriteAllText(input </> "x.md", "# X from input\n")
        File.WriteAllText(extras </> "x.md", "# X from extras\n")
        File.WriteAllText(extras </> "logo.png", "png")
        File.WriteAllText(project, "Collection v1\n")

    member _.Input = input
    member _.Extras = extras
    member _.Project = project

    member _.Config: SiteConfig =
        let crack () : CrackResult =
            {
                Substitutions =
                    [
                        FSharp.Formatting.Templating.ParamKey "fsdocs-collection-name",
                        (if File.Exists project then
                             File.ReadAllText(project).Trim()
                         else
                             "Test")
                    ]
                SubstitutionDiagnostics = []
                ApiDocInputs = []
                ApiDocOtherFlags = []
                LibDirs = []
                Projects = []
                References = []
                DesignTimeBuilt = false
            }

        {
            Input = input
            ExtraInputs = [ (extras, ".") ]
            DefaultTemplateFolder = None
            Root = "http://localhost:8901/"
            CollectionName = "Test"
            DefaultTemplate = None
            DefaultMdTemplate = None
            GenerateLlmsTxt = false
            IgnoreUncategorized = false
            ContentOptions =
                {
                    LineNumbers = None
                    Evaluate = false
                    Substitutions = []
                    OnError = ignore
                }
            ApiDllPaths = []
            ApiDocsOutputKind = OutputKind.Html
            ApiDocsTemplate = None
            GenerateApi = (fun _ _ -> None)
            Crack = crack
            Resolve = (fun _ -> { crack () with DesignTimeBuilt = true })
            ProjectFiles = [ project ]
            WatchScript = ""
            Diagnostics =
                {
                    ToolVersion = "test"
                    CommandLine = "watch"
                    Command = "watch"
                    Input = input
                    Output = None
                    Root = "http://localhost:8901/"
                    CollectionName = "Test"
                    GenerateLlmsTxt = false
                    IgnoredOptions = []
                    Projects = []
                    Substitutions = []
                    DefaultTemplate =
                        {
                            Tried = []
                            Chosen = None
                            Note = None
                        }
                    DefaultMarkdownTemplate =
                        {
                            Tried = []
                            Chosen = None
                            Note = None
                        }
                    ApiDocsTemplate =
                        {
                            Tried = []
                            Chosen = None
                            Note = None
                        }
                    ApiDocsOutputKind = "Html"
                    Extras =
                        {
                            Tried = []
                            Chosen = None
                            Note = None
                        }
                    HeadTemplate = None
                    BodyTemplate = None
                    MenuTemplatesFound = false
                }
        }

/// Asserts which page models were computed since the previous call, by input file name.
let internal computedSince =
    let seen = System.Collections.Generic.Dictionary<Site, int>()

    fun (site: Site) (expected: string list) ->
        let all = site.ComputedModels

        let offset =
            match seen.TryGetValue site with
            | true, n -> n
            | _ -> 0

        seen.[site] <- all.Length

        all
        |> List.skip offset
        |> List.map (fun (path, _, _) -> Path.GetFileName path)
        |> List.sort
        |> shouldEqual (List.sort expected)

let internal body (result: RenderResult) =
    match result with
    | Rendered r -> Encoding.UTF8.GetString r.Body
    | NotFound -> failwith "not found"
    | Failed ex -> raise ex

[<Test>]
let ``routes follow the build rules`` () =
    let fx = Fixture()
    use site = new Site(fx.Config)

    match site.Resolve "/index.html" with
    | Some(ContentPage r) ->
        r.InputFile |> shouldEqual (fx.Input </> "index.md")
        r.OutputKind |> shouldEqual OutputKind.Html
        r.Template |> shouldEqual (Some(fx.Input </> "_template.html"))
        r.OutputFileRelativeToRoot |> shouldEqual (Path.Combine(".", "index.html"))
    | other -> failwithf "unexpected %A" other

    match site.Resolve "/sub/c.html" with
    | Some(ContentPage r) -> r.InputFile |> shouldEqual (fx.Input </> "sub" </> "c.md")
    | other -> failwithf "unexpected %A" other

    // the input overrides the extras for the same url
    match site.Resolve "/x.html" with
    | Some(ContentPage r) -> r.InputFile |> shouldEqual (fx.Input </> "x.md")
    | other -> failwithf "unexpected %A" other

    site.Resolve "/logo.png"
    |> shouldEqual (Some(StaticFile(fx.Extras </> "logo.png")))

    site.Resolve "/style.css"
    |> shouldEqual (Some(StaticFile(fx.Input </> "style.css")))

    site.Resolve "/index.json" |> shouldEqual (Some SearchIndex)
    // no markdown template and llms.txt is off: no markdown routes, no llms routes
    site.Resolve "/a.md" |> shouldEqual None
    site.Resolve "/llms.txt" |> shouldEqual None
    // dot files, templates and raw sources are not served
    site.Resolve "/.hidden.html" |> shouldEqual None
    site.Resolve "/_template.html" |> shouldEqual None
    site.Resolve "/a.md" |> shouldEqual None
    site.Resolve "/missing.html" |> shouldEqual None
    site.Resolve "/reference/index.html" |> shouldEqual None

    let scan = site.Scan

    scan.Skipped
    |> List.map (fst >> Path.GetFileName)
    |> List.sort
    |> shouldEqual [ ".hidden.md"; "_template.html" ]

    scan.NavPages
    |> List.map (fun p -> Path.GetFileName p.InputPath, p.Title.Trim())
    |> List.sort
    |> shouldEqual
        [
            "a.md", "Page A"
            "b.fsx", "Page B"
            "c.md", "Page C"
            "index.md", "Home"
            "lib.fsx", "lib"
            // build lists a page found in both the extras and the input twice as well
            "x.md", "X from extras"
            "x.md", "X from input"
        ]

[<Test>]
let ``pages are computed once and only invalidated by relevant changes`` () =
    let fx = Fixture()
    use site = new Site(fx.Config)
    computedSince site []

    let a = body (site.Render "/a.html")
    a |> shouldContainText "Text of A."
    a |> shouldContainText "Page B"
    computedSince site [ "a.md" ]

    body (site.Render "/a.html") |> ignore
    computedSince site []

    // a byte-identical rewrite invalidates nothing
    let aFile = fx.Input </> "a.md"
    let original = File.ReadAllText aFile
    File.WriteAllText(aFile, original)
    site.Refresh aFile |> shouldEqual false
    body (site.Render "/a.html") |> ignore
    computedSince site []

    // a css change invalidates no page
    File.WriteAllText(fx.Input </> "style.css", "body { margin: 0 }\n")
    site.Refresh(fx.Input </> "style.css") |> shouldEqual true
    body (site.Render "/a.html") |> ignore
    computedSince site []

    // changing the heading of a recomputes a and updates the nav of other pages without recomputing them
    body (site.Render "/index.html") |> shouldContainText "Page A"
    computedSince site [ "index.md" ]
    File.WriteAllText(aFile, original.Replace("# Page A", "# Page A2"))
    site.Refresh aFile |> shouldEqual true
    body (site.Render "/a.html") |> shouldContainText "Page A2"
    computedSince site [ "a.md" ]
    body (site.Render "/index.html") |> shouldContainText "Page A2"
    computedSince site []

    // a page depends on the scripts it loads
    body (site.Render "/b.html") |> shouldContainText "Page B"
    computedSince site [ "b.fsx" ]
    let lib = fx.Input </> "lib.fsx"
    File.WriteAllText(lib, "module Lib\nlet x = 12\n")
    site.Refresh lib |> shouldEqual true
    body (site.Render "/b.html") |> ignore
    computedSince site [ "b.fsx" ]
    body (site.Render "/index.html") |> ignore
    computedSince site []

    // a template change re-renders without recomputing models
    File.WriteAllText(
        fx.Input </> "_template.html",
        "<html><body><nav>{{fsdocs-list-of-documents}}</nav><main id=\"new\">{{fsdocs-content}}</main></body></html>"
    )

    site.Refresh(fx.Input </> "_template.html") |> shouldEqual true
    body (site.Render "/index.html") |> shouldContainText "id=\"new\""
    computedSince site []

    // a deleted page disappears from the routes and the nav; its front matter is gone, so the
    // next/previous links of the other pages may change and their models are recomputed
    File.Delete aFile
    site.Refresh aFile |> shouldEqual true
    site.Resolve "/a.html" |> shouldEqual None
    site.Render "/a.html" |> shouldEqual NotFound
    body (site.Render "/index.html") |> shouldNotContainText "Page A2"
    computedSince site [ "index.md" ]

    // a new page appears; it is a new markdown link target, so the other pages are recomputed
    let d = fx.Input </> "d.md"
    File.WriteAllText(d, "# Page D\n")
    site.Refresh d |> shouldEqual true
    body (site.Render "/d.html") |> shouldContainText "Page D"
    body (site.Render "/index.html") |> shouldContainText "Page D"
    computedSince site [ "d.md"; "index.md" ]

[<Test>]
let ``search index forces all pages and is invalidated with them`` () =
    let fx = Fixture()
    use site = new Site(fx.Config)
    let json = body (site.Render "/index.json")
    json |> shouldContainText "\"title\":\"Page A"
    json |> shouldContainText "\"uri\":\"http://localhost:8901/sub/c.html\""
    computedSince site [ "a.md"; "b.fsx"; "c.md"; "index.md"; "lib.fsx"; "x.md" ]
    body (site.Render "/index.json") |> ignore
    computedSince site []

    File.WriteAllText(fx.Input </> "index.md", "# Home\n\nWelcome again.\n")
    site.Refresh(fx.Input </> "index.md") |> shouldEqual true
    body (site.Render "/index.json") |> shouldContainText "Welcome again."
    computedSince site [ "index.md" ]

[<Test>]
let ``a broken page fails alone and recovers`` () =
    let fx = Fixture()
    use site = new Site(fx.Config)
    let cFile = fx.Input </> "sub" </> "c.md"
    // an unterminated fenced block is fine for markdown; use a script that fails to parse as a notebook instead
    let nb = fx.Input </> "broken.ipynb"
    File.WriteAllText(nb, "{ not json")
    site.Refresh nb |> shouldEqual true

    match site.Render "/broken.html" with
    | Failed _ -> ()
    | other -> failwithf "expected a failure, got %A" other

    body (site.Render "/sub/c.html") |> shouldContainText "Page C"

    File.WriteAllText(
        nb,
        """{ "cells": [ { "cell_type": "markdown", "metadata": {}, "source": [ "# Fixed\n" ] } ], "metadata": {}, "nbformat": 4, "nbformat_minor": 5 }"""
    )

    site.Refresh nb |> shouldEqual true
    body (site.Render "/broken.html") |> shouldContainText "Fixed"
    File.Delete cFile

[<Test>]
let ``a project file change re-cracks and recomputes the pages`` () =
    let fx = Fixture()
    use site = new Site(fx.Config)
    body (site.Render "/index.html") |> shouldContainText "Collection v1"
    computedSince site [ "index.md" ]

    // a byte-identical rewrite of the project file changes nothing
    File.WriteAllText(fx.Project, "Collection v1\n")
    site.Refresh fx.Project |> shouldEqual false
    body (site.Render "/index.html") |> ignore
    computedSince site []

    File.WriteAllText(fx.Project, "Collection v2\n")
    site.Refresh fx.Project |> shouldEqual true
    body (site.Render "/index.html") |> shouldContainText "Collection v2"
    computedSince site [ "index.md" ]
    body (site.Render "/sub/c.html") |> shouldContainText "Collection v2"

    // A change of the same length that keeps the last write time, as two quick writes get on
    // Windows, where the granularity of the file time is coarser than the write itself
    let writtenAt = File.GetLastWriteTimeUtc fx.Project
    File.WriteAllText(fx.Project, "Collection v3\n")
    File.SetLastWriteTimeUtc(fx.Project, writtenAt)
    site.Refresh fx.Project |> shouldEqual true
    body (site.Render "/index.html") |> shouldContainText "Collection v3"

[<Test>]
let ``next and previous page links only come from the content trees`` () =
    let fx = Fixture()
    // the default template folder shipped with the tool holds pages of its own; they are watched
    // for template changes but must not take part in the page order
    let templateFolder = Path.GetDirectoryName fx.Input </> "template"
    Directory.CreateDirectory templateFolder |> ignore
    File.WriteAllText(templateFolder </> "_template.html", "<html>{{fsdocs-content}}</html>")

    File.WriteAllText(templateFolder </> "z.md", "---\ncategory: Docs\ncategoryindex: 0\nindex: 0\n---\n\n# Z\n")

    File.WriteAllText(fx.Input </> "index.md", "# Home\n\n<a href=\"{{fsdocs-next-page-link}}\">Next</a>\n")

    use site =
        new Site(
            { fx.Config with
                DefaultTemplateFolder = Some templateFolder
            }
        )

    site.Scan.FilesWithFrontMatter
    |> Array.map (fun f -> Path.GetFileName f.FileName)
    |> shouldEqual [| "a.md"; "b.fsx" |]

    body (site.Render "/index.html") |> shouldContainText "href=\"a.html\""
