#if INTERACTIVE
#r "../../bin/FSharp.CodeFormat.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net45/FsUnit.NUnit.dll"
#else
module FSharp.CodeFormat.Tests
#endif

open FsUnit
open NUnit.Framework
open FSharp.CodeFormat
open FsUnitTyped

// --------------------------------------------------------------------------------------
// Initialization - find F# compiler dll, setup formatting agent
// --------------------------------------------------------------------------------------

let agent = CodeFormat.CreateAgent()

// Check that snippet constains a specific span
let containsSpan f snips = 
  snips |> Seq.exists (fun (Snippet(_, lines)) ->
    lines |> Seq.exists (fun (Line spans) -> spans |> Seq.exists f))

// Check that tool tips contains a specified token
let (|ToolTipWithLiteral|_|) text tips = 
  if Seq.exists (function Literal(tip) -> tip.Contains(text) | _ -> false) tips
  then Some () else None
  
// --------------------------------------------------------------------------------------
// Test that some basic things work
// --------------------------------------------------------------------------------------

[<Test>]
let ``Simple code snippet is formatted with tool tips``() = 
  let source = """let hello = 10"""
  let snips, errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
  
  errors |> shouldEqual [| |]
  snips |> containsSpan (function
    | Token(_, "hello", Some (ToolTipWithLiteral "val hello : int")) -> true
    | _ -> false)
  |> shouldEqual true

let getContentAndToolTip (source: string) =
  let snips, _errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
  let res = CodeFormat.FormatHtml(snips, "fstips")
  (Seq.head res.Snippets).Content, res.ToolTip

let getContent = getContentAndToolTip >> fst

[<Test>]
let ``Simple code snippet is formatted as HTML``() = 
    let content, tooltip = getContentAndToolTip """let hello = 10"""
    content |> should contain "<span class=\"k\">let</span>"
    content |> should contain ">hello<"
    content |> should contain "<span class=\"n\">10</span>"
    tooltip |> should contain "val hello : int" 

[<Test>]
let ``Non-unicode characters do not cause exception`` () =
  // TODO:  This does not return any snippet text because of F.C.S. error
  // https://github.com/fsharp/FSharp.Compiler.Service/issues/291
  // But at least, it should not cause a crash.
  let source = """
// [snippet:16]
✘ let add I J = I+J
// [/snippet]"""
  let snips, errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
  errors.Length |> should (be greaterThan) 0
  let (SourceError(_, _, _, msg)) = errors.[0] 
  msg |> should contain "✘"

[<Test>]
let ``Plain string is in span of 's' class when it's the last token in the line``() = 
  getContent """let _ = "str" """ |> should contain "<span class=\"s\">&quot;str&quot;</span>"

[<Test>]
let ``Plain string is in span of 's' class, there are several other tokens next to it``() = 
  let content = getContent """let _ = "str", 1 """
  content |> shouldContainText "<span class=\"s\">&quot;str&quot;</span>"
  content |> should not' (contain "<span class=\"s\">,</span>")
  content |> shouldContainText (",")

[<Test>]
let ``Plain string is in span of 's' class, there is single char next to it``() = 
  let content = getContent """let _ = ("str")"""
  content |> shouldContainText "> (<"
  content |> shouldContainText "<span class=\"s\">&quot;str&quot;</span>"
  content |> shouldContainText ">)"

[<Test>]
let ``Modules and types are in spans of 't' class``() = 
  let content = getContent """
module Module =
  type Type() = class end
"""
  content |> shouldContainText "class=\"t\">Module</span>"
  content |> shouldContainText "class=\"t\">Type</span>"

[<Test>]
let ``Functions and methods are in spans of 'f' class``() = 
  let content = getContent """
module M =
    type T() =
        let func1 x = ()
        member __.Method x = ()
    let func2 x y = x + y
"""
  content |> shouldContainText "class=\"f\">func1</span>"
  content |> shouldContainText "class=\"f\">Method</span>"
  content |> shouldContainText "class=\"f\">func2</span>"

[<Test>]
let ``Printf formatters are in spans of 'pf' class``() = 
  let content = getContent """let _ = sprintf "a %A b %0.3fD" """
  content |> shouldContainText "class=\"s\">&quot;a </span>"
  content |> shouldContainText "class=\"pf\">%A</span>"
  content |> shouldContainText "class=\"s\"> b </span>"
  content |> shouldContainText "class=\"pf\">%0.3f</span>"
  content |> shouldContainText "class=\"s\">D&quot;</span>"

[<Test>]
let ``Escaped characters are in spans of 'e' class``() = 
  let content = getContent """let _ = sprintf "a \n\tD\uA0A0 \t" """
  content |> shouldContainText "class=\"s\">&quot;a </span>"
  content |> shouldContainText "class=\"e\">\\n</span>"
  content |> shouldContainText "class=\"e\">\\t</span>"
  content |> shouldContainText "class=\"s\">D</span>"
  content |> shouldContainText "class=\"e\">\\uA0A0</span>"
  content |> shouldContainText "class=\"s\"> </span>"
  content |> shouldContainText "class=\"e\">\\t</span>"