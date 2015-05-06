#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Markdown.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#load "../Common/MarkdownUnit.fs"
#load "Setup.fs"
#else
module FSharp.Literate.Tests.Eval
#endif

open FsUnit
open System.IO
open FSharp.Markdown
open FSharp.Literate
open NUnit.Framework
open FSharp.Literate.Tests.Setup
open FSharp.Markdown.Unit

// --------------------------------------------------------------------------------------
// Test FSI evaluator
// --------------------------------------------------------------------------------------

[<Test>]
let ``Can parse and format literate F# script with evaluation`` () =
  let content = """
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

  let doc = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = getFsiEvaluator())

  doc.Errors |> Seq.length |> shouldEqual 0
  // Contains formatted code and markdown
  doc.Paragraphs |> shouldMatchPar (function
    | Matching.LiterateParagraph(FormattedCode(_)) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | Paragraph [Strong [Literal "hello"]] -> true | _ -> false)

  // Contains transformed output
  doc.Paragraphs |> shouldMatchPar (function
    | CodeBlock ("42", _, _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | CodeBlock ("85", _, _) -> true | _ -> false)
  doc.Paragraphs |> shouldMatchPar (function
    | CodeBlock (">>12343<<", _, _) -> true | _ -> false)

[<Test>]
let ``Can evaluate hidden code snippets`` () =
  let content = """
(*** hide,define-output: test ***)
printfn "42"
(*** include-output: test ***)
"""
  let doc = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = getFsiEvaluator())
  let html = Literate.WriteHtml(doc)
  html.Contains("42") |> shouldEqual true
  html.Contains(">printfn<") |> shouldEqual false

[<Test>]
let ``Can parse and format literate F# script with custom evaluator`` () =
  let content = """
let test = [1;2;3]
(*** include-value:test ***)"""

  // Create evaluator & register simple formatter for lists
  let fsiEvaluator = FSharp.Literate.FsiEvaluator()
  fsiEvaluator.RegisterTransformation(fun (o, ty) ->
    if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
      let items =
        [ for it in Seq.cast<obj> (unbox o) -> [ Paragraph[Literal (it.ToString())] ] ]
      Some [ ListBlock(MarkdownListKind.Ordered, items) ]
    else None)

  let doc = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = fsiEvaluator)
  doc.Paragraphs
  |> shouldMatchPar (function
      | ListBlock(Ordered, items) ->
          items = [ [Paragraph [Literal "1"]]; [Paragraph [Literal "2"]]; [Paragraph [Literal "3"]] ]
      | _ -> false)

[<Test>]
let ``All embedded code snippets should be wrapped in a table`` () =
  let content = """
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
  let doc = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = getFsiEvaluator())
  let html = Literate.WriteHtml(doc)
  html.Split([| "<table class=\"pre\">" |], System.StringSplitOptions.None).Length
  |> shouldEqual 5


[<Test>]
let ``Can disable evaluation on an entire script file`` () =
  let content = """
(*** define-output:t ***)
printfn "%d" (40 + 2)
(*** include-output:t ***)
"""
  let doc1 = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = getFsiEvaluator())
  let html1 = Literate.WriteHtml(doc1)
  html1.Contains("42") |> shouldEqual true

  let doc2 = Literate.ParseScriptString("(*** do-not-eval-file ***)\n" + content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = getFsiEvaluator())
  let html2 = Literate.WriteHtml(doc2)
  html2.Contains("42") |> shouldEqual false


[<Test>]
let ``Can #load script with fsi.AddPrinter (without failing)`` () =
  // Generate a script file that uses 'fsi.AddPrinter' in the TEMP folder
  let file =  """namespace FsLab
module Demo =
  let test = 42
module FsiAutoShow = 
  fsi.AddPrinter(fun (n:int) -> n.ToString())"""
  let path = Path.GetTempFileName() + ".fsx"
  File.WriteAllText(path, file)

  // Eval script that #loads the script file and uses something from it
  let content = """
(*** define-output:t ***)
#load @"[PATH]" 
printfn "%d" FsLab.Demo.test
(*** include-output:t ***)""".Replace("[PATH]", path)
  let fsie = getFsiEvaluator()
  fsie.EvaluationFailed.Add(printfn "%A")
  let doc1 = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = fsie)
  let html1 = Literate.WriteHtml(doc1)
  html1.Contains("42") |> shouldEqual true
  File.Delete(path)

[<Test>]
let ``Can specify `fsi` object that ignores all printers`` () =
  // Generate a script file that uses 'fsi.AddPrinter' in the TEMP folder
  let file =  """namespace FsLab
module Demo =
  let mutable test = "Not executed"
  fsi.AddPrinter(fun (n:int) -> 
    test <- "Executed"
    "")"""
  let path = Path.GetTempFileName() + ".fsx"
  File.WriteAllText(path, file)

  // Eval script that #loads the script file and uses something from it
  let content = """
(*** define-output:t1 ***)
#load @"[PATH]" 
1
(*** include-it:t1 ***)
(*** define-output:t2 ***)
FsLab.Demo.test
(*** include-it:t2 ***)""".Replace("[PATH]", path)
  let fsie = FSharp.Literate.FsiEvaluator(fsiObj = FsiEvaluatorConfig.CreateNoOpFsiObject())
  let doc1 = Literate.ParseScriptString(content, "." @@ "A.fsx", getFormatAgent(), fsiEvaluator = fsie)
  let html1 = Literate.WriteHtml(doc1)
  html1.Contains("Not executed") |> shouldEqual true
  html1.Contains("Executed") |> shouldEqual false
  File.Delete(path)

[<Test>]
let ``Can #load script relative to the script being evaluated`` () =
  // Generate a script file in a sub-folder of the TEMP folder
  let file =  """module Test
let test1 = 42
let test2 = 43"""
  let path = Path.GetTempFileName()
  File.Delete(path)
  Directory.CreateDirectory(path) |> ignore
  File.WriteAllText(Path.Combine(path, "test.fsx"), file)
  
  // Eval script that #loads the script file and uses relative path
  let content = """
(*** define-output:t ***)
#load @"[PATH]/test.fsx" 
printfn "%d" Test.test1
(*** include-output:t ***)
(** some _markdown_  *)
(*** define-output:t2 ***)
#load @"[PATH]/test.fsx" 
printfn "%d" Test.test2
(*** include-output:t2 ***)""".Replace("[PATH]", Path.GetFileName(path))
  // Path where this script is located (though we don't actually need to save it there)
  let scriptPath = Path.Combine(Path.GetDirectoryName(path), "main.fsx")

  let fsie = getFsiEvaluator()
  fsie.EvaluationFailed.Add(printfn "%A")
  let doc1 = Literate.ParseScriptString(content, scriptPath, getFormatAgent(), fsiEvaluator = fsie)
  let html1 = Literate.WriteHtml(doc1)
  html1.Contains("42") |> shouldEqual true
  html1.Contains(">markdown<") |> shouldEqual true
  html1.Contains("43") |> shouldEqual true
  Directory.Delete(path, true)

  
  
