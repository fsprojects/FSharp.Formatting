module fsdocs.Tests.DocContent

open System.IO
open fsdocs
open NUnit.Framework
open FsUnitTyped
open FSharp.Formatting.Literate

let (</>) a b = Path.Combine(a, b)

let tempDir =
    let dir = Path.GetTempPath() </> "fsdocs-tool-tests" </> "content"
    Directory.CreateDirectory dir |> ignore
    dir

let writeTemp (name: string) (content: string) =
    let path = tempDir </> name
    File.WriteAllText(path, content)
    path

let internal options: ContentOptions =
    {
        LineNumbers = None
        Evaluate = false
        Substitutions = []
        RootKeys = [ FSharp.Formatting.Templating.ParamKeys.root ]
        OnError = ignore
    }

/// A title taken from a heading is formatted HTML and ends with the line ending of the writer,
/// which is '\r\n' on Windows.
let withLfEndings (s: string) = s.Replace("\r\n", "\n")

/// The title 'fsdocs build' gives the HTML page of this file.
let internal titleFromModel (inputFile: string) =
    let model =
        Content.computeModel
            options
            (Some tempDir)
            inputFile
            OutputKind.Html
            (Path.GetFileNameWithoutExtension inputFile + ".html")
            (fun _ _ -> None)
            (fun _ -> None)
            [||]
            None

    model.Title

[<Test>]
let ``scanTitle of markdown with title in front matter`` () =
    let file =
        writeTemp
            "title-front-matter.md"
            """---
title: The Real Title
category: Docs
categoryindex: 1
index: 1
---

# Some heading
"""

    let title, source = Content.scanTitle file
    title |> shouldEqual "The Real Title"
    source |> shouldEqual TitleSource.FrontMatter
    title |> shouldEqual (titleFromModel file)

[<Test>]
let ``scanTitle of markdown without title uses the formatted first heading`` () =
    let file =
        writeTemp
            "title-heading.md"
            """---
category: Docs
categoryindex: 1
index: 2
---

Some intro text.

# Using `inline code` in a heading

## Not the title
"""

    let title, source = Content.scanTitle file

    withLfEndings title
    |> shouldEqual "Using <code>inline code</code> in a heading\n"

    source |> shouldEqual TitleSource.Heading
    title |> shouldEqual (titleFromModel file)

[<Test>]
let ``scanTitle of markdown without heading uses the file name`` () =
    let file = writeTemp "no-heading.md" "Just a paragraph.\n"
    let title, source = Content.scanTitle file
    title |> shouldEqual "no-heading"
    source |> shouldEqual TitleSource.FileName
    title |> shouldEqual (titleFromModel file)

[<Test>]
let ``scanTitle of script skips command comments and finds the heading in a later comment`` () =
    let file =
        writeTemp
            "title-script.fsx"
            """(**
---
category: Docs
categoryindex: 1
index: 3
---
*)
(*** condition: prepare ***)
#nowarn "211"
(**
# Literate *Scripts* with `code`

Body text.
*)
let x = 1
(**
# A second heading is not the title
*)
"""

    let title, source = Content.scanTitle file

    withLfEndings title
    |> shouldEqual "Literate <em>Scripts</em> with <code>code</code>\n"

    source |> shouldEqual TitleSource.Heading
    title |> shouldEqual (titleFromModel file)

[<Test>]
let ``scanTitle of script with title in front matter`` () =
    let file =
        writeTemp
            "title-script-front-matter.fsx"
            """(**
---
title: Script Title
category: Docs
categoryindex: 1
index: 4
---
*)
(**
# Not this one
*)
let y = 2
"""

    let title, source = Content.scanTitle file
    title |> shouldEqual "Script Title"
    source |> shouldEqual TitleSource.FrontMatter
    title |> shouldEqual (titleFromModel file)

[<Test>]
let ``markdownBlocksOfScript returns comments in order without commands`` () =
    let file =
        writeTemp
            "blocks.fsx"
            """(**
first
*)
(*** hide ***)
let a = 1
(** second *)
let b = 2
(**
third
*)
"""

    Content.markdownBlocksOfScript file
    |> List.map (fun s -> s.Trim())
    |> shouldEqual [ "first"; "second"; "third" ]

[<Test>]
let ``scanTitle of notebook uses the heading of the markdown cell`` () =
    let file =
        writeTemp
            "title-notebook.ipynb"
            """{
 "cells": [
  {
   "cell_type": "markdown",
   "metadata": {},
   "source": [
    "---\n",
    "category: Docs\n",
    "categoryindex: 1\n",
    "index: 5\n",
    "---\n",
    "\n",
    "# Notebook `Title`\n"
   ]
  },
  {
   "cell_type": "code",
   "execution_count": null,
   "metadata": { "polyglot_notebook": { "kernelName": "fsharp" } },
   "outputs": [],
   "source": [ "let z = 3\n" ]
  }
 ],
 "metadata": { "kernelspec": { "name": ".net-fsharp", "language": "F#" } },
 "nbformat": 4,
 "nbformat_minor": 5
}
"""

    let title, source = Content.scanTitle file
    withLfEndings title |> shouldEqual "Notebook <code>Title</code>\n"
    source |> shouldEqual TitleSource.Heading
    title |> shouldEqual (titleFromModel file)

[<Test>]
let ``navigation factory excludes index pages and marks the active page`` () =
    let page path title category index =
        {
            NavPage.InputPath = path
            OutputPath = Path.GetFileNameWithoutExtension path + ".html"
            Title = title
            Category = Some category
            CategoryIndex = Some 1
            Index = Some index
        }

    let pages =
        [ page "/docs/index.md" "Home" "Docs" 1; page "/docs/b.md" "B & co" "Docs" 2; page "/docs/a.md" "A" "Docs" 1 ]

    let render = Content.getNavigationEntriesFactory (tempDir, pages, false)
    let html = render "../" (Some "/docs/b.md")
    html |> shouldContainText "href=\"../a.html\""
    html |> shouldNotContainText "Home"
    html |> shouldContainText "B &amp; co"
    html |> shouldContainText "nav-item active"
    html.IndexOf("A") < html.IndexOf("B &amp; co") |> shouldEqual true
