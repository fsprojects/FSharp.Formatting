module FSharp.Literate.Tests.Eval

open System.IO
open FSharp.Formatting.Markdown
open FSharp.Formatting.Literate
open FSharp.Formatting.Literate.Evaluation
open FSharp.Literate.Tests.Setup
open FSharp.Formatting.Markdown.Unit
open FsUnit
open FsUnitTyped
open NUnit.Framework

do FSharp.Formatting.TestHelpers.enableLogging ()

// --------------------------------------------------------------------------------------
// Test FSI evaluator
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can parse literate F# script with out of order value access`` () =
    let content =
        """
(***hide***)
let test = 42
let test2 = 43 + test

(** **hello** *)
let test3 = test2 + 15

(*** define-output:test ***)
printf ">>%d<<" 12343

(** a value *)
(*** include-value: test ***)

(** another value *)
(*** include-value: test2 ***)

(** an output *)
(*** include-output: test ***)
"""

    let doc = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = getFsiEvaluator ())

    doc.Diagnostics |> Seq.length |> shouldEqual 0
    // Contains formatted code and markdown
    doc.Paragraphs
    |> shouldMatchPar (function
        | MarkdownPatterns.LiterateParagraph(LiterateCode _) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | Paragraph([ Strong([ Literal("hello", _) ], _) ], _) -> true
        | _ -> false)

    // Contains transformed output - not using 'include-value' and 'include-output' gives odd execution sequence numbers
    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock("42", "text/plain", Some 4) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock("85", "text/plain", Some 5) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock(">>12343<<", "text/plain", Some 3) -> true
        | _ -> false)

[<Test>]
let ``Can parse literate F# script with in order outputs`` () =
    let content =
        """
(**

Hello

*)
let test = 42
let test2 = 43 + test
(*** include-value: test ***)

test2 + 15
(*** include-it ***)

printf ">>%d<<" 12343
(*** include-output ***)

printf ">>%d<<" 12345
test2 + 16
(*** include-output ***)
(*** include-it ***)

"""

    let doc = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = getFsiEvaluator ())

    doc.Diagnostics |> Seq.length |> shouldEqual 0
    // Contains transformed output
    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock("42", "text/plain", Some 2) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock("100", "text/plain", Some 3) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock(">>12343<<", "text/plain", Some 4) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock(">>12345<<", "text/plain", Some 5) -> true
        | _ -> false)

    doc.Paragraphs
    |> shouldMatchPar (function
        | OutputBlock("101", "text/plain", Some 5) -> true
        | _ -> false)

[<Test>]
let ``Can evaluate hidden code snippets`` () =
    let content =
        """
(*** hide,define-output: test ***)
printfn "42"
(*** include-output: test ***)
"""

    let doc = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = getFsiEvaluator ())

    let html = Literate.ToHtml(doc)
    html.Contains("42") |> shouldEqual true
    html.Contains(">printfn<") |> shouldEqual false

[<Test>]
let ``Can parse literate F# script with custom evaluator`` () =
    let content =
        """
let test = [1;2;3]
(*** include-value:test ***)"""

    // Create evaluator & register simple formatter for lists
    let fsiEvaluator = new FSharp.Formatting.Literate.Evaluation.FsiEvaluator(options = [| "--multiemit-" |])

    fsiEvaluator.RegisterTransformation(fun (o, ty, _executionCount) ->
        if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
            let items =
                [ for it in Seq.cast<obj> (unbox o) ->
                      [ Paragraph([ Literal(it.ToString(), MarkdownRange.zero) ], MarkdownRange.zero) ] ]

            Some [ ListBlock(MarkdownListKind.Ordered, items, MarkdownRange.zero) ]
        else
            None)

    let doc = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsiEvaluator)

    doc.Paragraphs
    |> shouldMatchPar (function
        | ListBlock(Ordered, items, _) ->
            items = [ [ Paragraph([ Literal("1", MarkdownRange.zero) ], MarkdownRange.zero) ]
                      [ Paragraph([ Literal("2", MarkdownRange.zero) ], MarkdownRange.zero) ]
                      [ Paragraph([ Literal("3", MarkdownRange.zero) ], MarkdownRange.zero) ] ]
        | _ -> false)

[<Test>]
let ``All embedded code snippets should be wrapped in a table`` () =
    let content =
        """
(**
test 1

    [lang=sql]
    select

test 2

    let a = 1

*)
(*** define-output:t ***)
printfn "hi"
(*** include-output:t ***)
"""

    let doc = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = getFsiEvaluator ())

    let html = Literate.ToHtml(doc)

    html.Split([| "<table class=\"pre\">" |], System.StringSplitOptions.None).Length
    |> shouldEqual 5


[<Test>]
let ``Can disable evaluation on an entire script file`` () =
    let content =
        """
(*** define-output:t ***)
printfn "%d" (40 + 2)
(*** include-output:t ***)
"""

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = getFsiEvaluator ())

    let html1 = Literate.ToHtml(doc1)
    html1.Contains("42") |> shouldEqual true

    let doc2 =
        Literate.ParseScriptString(
            "(*** do-not-eval-file ***)\n" + content,
            "." </> "A.fsx",
            fsiEvaluator = getFsiEvaluator ()
        )

    let html2 = Literate.ToHtml(doc2)
    html2.Contains("42") |> shouldEqual false


[<Test>]
let ``Can #load script with fsi-AddPrinter (without failing)`` () =
    // Generate a script file that uses 'fsi.AddPrinter' in the TEMP folder
    let file =
        """namespace FsLab
module Demo =
  type C() = class end
  type D() = class end
  let test = C()
  let test2 = D()
module FsiAutoShow =
  fsi.AddPrinter(fun (n:Demo.C) -> "QUACK")
  fsi.AddPrintTransformer(fun (n:Demo.D) -> box "SUMMERTIME")
  let others =
      (fsi.FloatingPointFormat,
       fsi.FormatProvider,
       fsi.PrintWidth,
       fsi.PrintDepth,
       fsi.PrintLength,
       fsi.PrintSize,
       fsi.ShowProperties,
       fsi.ShowIEnumerable,
       fsi.ShowDeclarationValues,
       fsi.CommandLineArgs)"""

    let path = Path.GetTempFileName() + ".fsx"
    File.WriteAllText(path, file)

    // Eval script that #loads the script file and uses printer defined in it
    let content =
        """
(*** hide ***)
#load @"[PATH]"
(** *)
FsLab.Demo.test
(*** include-fsi-output ***)
FsLab.Demo.test2
(*** include-it ***)"""
            .Replace("[PATH]", path)

    let fsie = getFsiEvaluator ()
    fsie.EvaluationFailed.Add(printfn "%A")

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    doc1.Diagnostics.Length |> shouldEqual 0
    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "QUACK"
    html1 |> shouldContainText "SUMMERTIME"
    File.Delete(path)

[<Test>]
let ``Can #load script relative to the script being evaluated`` () =
    // Generate a script file in a sub-folder of the TEMP folder
    let file =
        """module Test
let test1 = 42
let test2 = 43"""

    let path = Path.GetTempFileName()
    File.Delete(path)
    Directory.CreateDirectory(path) |> ignore
    File.WriteAllText(Path.Combine(path, "test.fsx"), file)

    // Eval script that #loads the script file and uses relative path
    let content =
        """
(*** define-output:t ***)
#load @"[PATH]/test.fsx"
printfn "%d" Test.test1
(*** include-output:t ***)
(** some _markdown_  *)
(*** define-output:t2 ***)
#load @"[PATH]/test.fsx"
printfn "%d" Test.test2
(*** include-output:t2 ***)"""
            .Replace("[PATH]", Path.GetFileName(path))
    // Path where this script is located (though we don't actually need to save it there)
    let scriptPath = Path.Combine(Path.GetDirectoryName(path), "main.fsx")

    let fsie = getFsiEvaluator ()
    fsie.EvaluationFailed.Add(printfn "%A")

    let doc1 = Literate.ParseScriptString(content, scriptPath, fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1.Contains("42") |> shouldEqual true
    html1.Contains(">markdown<") |> shouldEqual true
    html1.Contains("43") |> shouldEqual true
    Directory.Delete(path, true)



[<Test>]
let ``Can include-it`` () =
    let content =
        """
1000+1000
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "1000"
    html1 |> shouldContainText "2000"

[<Test>]
let ``Can include-fsi-output`` () =
    let content =
        """
let xxxxxxx = 1
(*** include-fsi-output ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "val xxxxxxx: int = 1"

[<Test>]
let ``Can include-html-output`` () =
    let content =
        """
type Html = Html of string
#if HAS_FSI_ADDHTMLPRINTER
fsi.AddHtmlPrinter(fun (Html h) -> seq [], h)
#endif
Html "<b>HELLO</b>"
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "<b>HELLO</b>"

[<Test>]
let ``Can include-html-output-even-without-hash-if-and-print-transformer-works-for-html-too`` () =
    let content =
        """
type Html = Html of string
type Quack = Quack of string
fsi.AddPrintTransformer(fun (Quack h) -> box (Html h))
fsi.AddHtmlPrinter(fun (Html h) -> seq [], "<b>QUACK</b>")
Quack "HELLO"
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "<b>QUACK</b>"

[<Test>]
let ``External images are downloaded if requested`` () =
    let content =
        """
type Html = Html of string
type Quack = Quack of string
fsi.AddPrintTransformer(fun (Quack h) -> box (Html h))
fsi.AddHtmlPrinter(fun (Html h) -> seq [], "<b>QUACK</b>")
Quack "HELLO"
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "<b>QUACK</b>"

[<Test>]
let ``Can include-fsi-merged-output`` () =
    let content =
        """
printfn "HELLO"
let xxxxxxx = [ 0 .. 10 ]
printfn "GOODBYE"
(*** include-fsi-merged-output ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "HELLO"
    html1 |> shouldContainText "GOODBYE"

    html1
    |> shouldContainText "val xxxxxxx: int list = [0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 10]"

    html1 |> shouldContainText "val it: unit = ()"

[<Test>]
let ``Can hide and include-it`` () =
    let content =
        """
(*** hide ***)
1000+1000
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "2000"
    html1 |> shouldNotContainText "1000"

[<Test>]
let ``Can hide and include-it-raw`` () =
    let content =
        """
1000+1000
|> string
(*** include-it-raw ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "2000"
    html1 |> shouldNotContainText "\"2000\""
    html1 |> shouldNotContainText "<code>2000</code>"

[<Test>]
let ``Can include-output`` () =
    let content =
        """
printfn "%sworld" "hello"
let xxxx = 1+1
1000+1000
(*** include-output ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "helloworld"
    html1 |> shouldContainText "val xxxx: int"

    html1 |> shouldContainText """<span class="k">let</span>""" // formatted code

    html1 |> shouldNotContainText "2000"

[<Test>]
let ``Can hide and include-output`` () =
    let content =
        """
(*** hide ***)
printfn "%sworld" "hello"
let xxxx = 1+1
1000+1000
(*** include-output ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "helloworld"
    html1 |> shouldContainText "val xxxx: int"

    html1 |> shouldNotContainText """<span class="k">let</span>""" // formatted code

    html1 |> shouldNotContainText "2000"

[<Test>]
let ``Can include-output-and-it`` () =
    let content =
        """
printfn "%sworld" "hello"
let xxxx = 1+1
1000+1000
(*** include-output ***)
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "helloworld"
    html1 |> shouldContainText "val xxxx: int"
    html1 |> shouldContainText "2000"

[<Test>]
let ``Script is formatted as Pynb with evaluation`` () =
    let fsie = getFsiEvaluator ()

    let content =
        """
(**
Heading
=======

With some [hyperlink](http://tomasp.net)
*)
1000+2000
(*** include-it ***)
let y = 30+3124
printfn "should not %s" "show"
(**

More text

*)
let z = 30+3124
printfn "should show"
(*** include-output ***)

(**

$$$
  \frac{x}{y} > 5.4

*)

(*** condition: prepare ***)
10000 + 20001
(*** include-it ***)
"""

    let md =
        Literate.ParseScriptString(
            content,
            "." </> "A.fsx",
            fsiEvaluator = fsie,
            parseOptions =
                (MarkdownParseOptions.ParseCodeAsOther
                 ||| MarkdownParseOptions.ParseNonCodeAsOther)
        )

    let pynb = Literate.ToPynb(md)
    printfn "----"
    printfn "%s" pynb
    printfn "----"
    pynb |> shouldContainText """With"""
    pynb |> shouldContainText """1000"""
    pynb |> shouldContainText """3000"""
    pynb |> shouldNotContainText """should not show"""
    pynb |> shouldNotContainText """3154"""
    pynb |> shouldContainText """should show"""

    pynb |> shouldContainText """execution_count": 1""" // first cell executed

    pynb |> shouldContainText """execution_count": 2""" // second cell executed

    pynb |> shouldNotContainText """execution_count": null""" // all cells are executed

    pynb |> shouldContainText """\begin{equation}"""
    pynb |> shouldContainText """\end{equation}"""
    pynb |> shouldContainText """30001"""

[<Test>]
let ``Comment with nested (*** ***) and following // line is not truncated`` () =
    let content =
        """
(**
Some text mentioning `(*** include-it ***)` or `(*** include-it-raw ***)`.

A paragraph after nested comment markers.

    [lang=fsharp]
    // a comment inside an indented code block
    let x = 1
*)
let a = 1
"""

    let doc = Literate.ParseScriptString(content, "." </> "A.fsx")

    let html = Literate.ToHtml(doc)
    // The paragraph after the nested markers must not be truncated
    html |> shouldContainText "A paragraph after nested comment markers"
    html |> shouldContainText "include-it-raw"

[<Test>]
let ``Can include-it-raw with AddHtmlPrinter`` () =
    let content =
        """
type Html = Html of string
fsi.AddHtmlPrinter(fun (Html h) -> seq [], h)
Html "<div>RAW_HTML_OUTPUT</div>"
(*** include-it-raw ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "<div>RAW_HTML_OUTPUT</div>"

[<Test>]
let ``AddHtmlPrinter with CSS and JS resources`` () =
    let content =
        """
type Widget = Widget of string
fsi.AddHtmlPrinter(fun (Widget w) ->
    seq [
        "style", ".widget { color: red; }"
        "script", "console.log('widget loaded')"
    ], sprintf "<span class='widget'>%s</span>" w)
Widget "test-widget"
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "test-widget"
    html1 |> shouldContainText ".widget { color: red; }"

[<Test>]
let ``AddHtmlPrinter with base64 image pattern`` () =
    let content =
        """
type MyChart = { Data: int list }
fsi.AddHtmlPrinter(fun (chart: MyChart) ->
    let b64 = System.Convert.ToBase64String([| 0uy; 1uy; 2uy |])
    seq [], sprintf "<img src=\"data:image/png;base64,%s\" />" b64)
{ Data = [1;2;3] }
(*** include-it ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "data:image/png;base64,"
    html1 |> shouldContainText "<img"

[<Test>]
let ``AddHtmlPrinter with include-it-raw emits unescaped HTML`` () =
    let content =
        """
type Tag = Tag of string
fsi.AddHtmlPrinter(fun (Tag t) -> seq [], sprintf "<b>%s</b>" t)
Tag "BOLDTEXT"
(*** include-it-raw ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "<b>BOLDTEXT</b>"

[<Test>]
let ``AddPrintTransformer chained with AddHtmlPrinter and include-it-raw`` () =
    let content =
        """
type Html = Html of string
type Wrapped = Wrapped of string
fsi.AddPrintTransformer(fun (Wrapped s) -> box (Html s))
fsi.AddHtmlPrinter(fun (Html h) -> seq [], sprintf "<em>%s</em>" h)
Wrapped "TRANSFORMED"
(*** include-it-raw ***)
"""

    let fsie = getFsiEvaluator ()

    let doc1 = Literate.ParseScriptString(content, "." </> "A.fsx", fsiEvaluator = fsie)

    let html1 = Literate.ToHtml(doc1)
    html1 |> shouldContainText "<em>TRANSFORMED</em>"
