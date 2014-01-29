#if INTERACTIVE
#r "../../bin/FSharp.CodeFormat.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.CodeFormat.Tests
#endif

open System
open System.IO
open System.Reflection
open FsUnit
open NUnit.Framework
open FSharp.CodeFormat

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

[<Test>]
let ``Simple code snippet is formatted as HTML``() = 
  let source = """let hello = 10"""
  let snips, errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
  let res = CodeFormat.FormatHtml(snips, "fstips")
  
  let actual = res.Snippets |> Seq.head
  actual.Content |> should contain "<span class=\"k\">let</span>"
  actual.Content |> should contain ">hello<"
  actual.Content |> should contain "<span class=\"n\">10</span>"
  res.ToolTip |> should contain "val hello : int"
