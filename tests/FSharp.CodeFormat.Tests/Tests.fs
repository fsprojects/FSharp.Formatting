#if INTERACTIVE
System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../bin/FSharp.Formatting.Common.dll"
#r "../../bin/FSharp.CodeFormat.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net45/FsUnit.NUnit.dll"
#else

[<NUnit.Framework.TestFixture>]
module FSharp.CodeFormat.Tests
#endif

open FsUnit
open NUnit.Framework
open FSharp.CodeFormat
open FSharp.CodeFormat.Constants
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
    content |> shouldContainText (sprintf "<span class=\"%s\">let</span>" CSS.Keyword)
    content |> shouldContainText ">hello<"
    content |> shouldContainText (sprintf "<span class=\"%s\">10</span>" CSS.Number)
    tooltip |> shouldContainText "val hello : int" 

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
  errors.Length |> shouldBeGreaterThan 0
  let (SourceError(_, _, _, msg)) = errors.[0] 
  msg |> shouldContainText "✘"

[<Test>]
let ``Plain string is in span of 's' class when it's the last token in the line``() = 
  getContent """let _ = "str" """ |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" CSS.String)

[<Test>]
let ``Plain string is in span of 's' class, there are several other tokens next to it``() = 
  let content = getContent """let _ = "str", 1 """
  content |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" CSS.String)
  content |> shouldNotContainText (sprintf  "<span class=\"%s\">,</span>" CSS.String)
  content |> shouldContainText ","

[<Test>]
let ``Plain string is in span of 's' class, there is punctuation next to it``() = 
  let content = getContent """let _ = ("str")"""
  content |> shouldContainText (sprintf "<span class=\"%s\">(</span>" CSS.Punctuation)
  content |> shouldContainText (sprintf  "<span class=\"%s\">&quot;str&quot;</span>" CSS.String)
  content |> shouldContainText (sprintf "<span class=\"%s\">)</span>" CSS.Punctuation)

[<Test>]
let ``Modules and types are in spans of 't' class``() = 
  let content = getContent """
module Module =
  type Type() = class end
"""
  content |> shouldContainText (sprintf "class=\"%s\">Module</span>" CSS.Module)
  content |> shouldContainText (sprintf "class=\"%s\">Type</span>" CSS.ReferenceType)

[<Test>]
let ``Functions and methods are in spans of 'f' class``() = 
  let content = getContent """
module M =
    type T() =
        let func1 x = ()
        member __.Method x = ()
    let func2 x y = x + y
"""
  content |> shouldContainText (sprintf "class=\"%s\">func1</span>" CSS.Function )
  content |> shouldContainText (sprintf "class=\"%s\">Method</span>" CSS.Function)
  content |> shouldContainText (sprintf "class=\"%s\">func2</span>" CSS.Function )

[<Test>]
let ``Printf formatters are in spans of 'pf' class``() = 
  let content = getContent """let _ = sprintf "a %A b %0.3fD" """
  content |> shouldContainText (sprintf "class=\"%s\">&quot;a </span>" CSS.String)
  content |> shouldContainText (sprintf "class=\"%s\">%%A</span>" CSS.Printf     )
  content |> shouldContainText (sprintf "class=\"%s\"> b </span>" CSS.String     )
  content |> shouldContainText (sprintf "class=\"%s\">%%0.3f</span>" CSS.Printf  )
  content |> shouldContainText (sprintf "class=\"%s\">D&quot;</span>" CSS.String )

[<Test>][<Ignore "FCS doesn't currently have semantic highlighting for escaped chars in a string">]
let ``Escaped characters are in spans of 'esc' class``() = 
  let content = getContent """let _ = sprintf "a \n\tD\uA0A0 \t" """
  content |> shouldContainText (sprintf "class=\"%s\">&quot;a </span>" CSS.String)
  content |> shouldContainText (sprintf "class=\"%s\">\\n</span>" CSS.Escaped)
  content |> shouldContainText (sprintf "class=\"%s\">\\t</span>" CSS.Escaped)
  content |> shouldContainText (sprintf "class=\"%s\">D</span>" CSS.String)
  content |> shouldContainText (sprintf "class=\"%s\">\\uA0A0</span>" CSS.Escaped)
  content |> shouldContainText (sprintf "class=\"%s\"> </span>" CSS.String)
  content |> shouldContainText (sprintf "class=\"%s\">\\t</span>" CSS.Escaped)