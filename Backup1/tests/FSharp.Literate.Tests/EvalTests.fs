#if INTERACTIVE
#I "../../bin/"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Markdown.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#load "../Common/MarkdownUnit.fs"
#load "Setup.fs"
#else
module FSharp.Literate.Tests.Eval
#endif

open FsUnit
open FSharp.Literate
open NUnit.Framework
open FSharp.Literate.Tests.Setup
(*
//[test]
Literate.ParseMarkdownString("""
a

    [lang=csharp,file=EvalTests.fs,key=test]

b""", __SOURCE_DIRECTORY__ + "\\Test.fsx").Paragraphs
//[/test]

let content = """
(** **hello** *)
let test = 42"""
let doc = Literate.ParseScriptString(content, __SOURCE_DIRECTORY__ + "\\Test.fsx", formatAgent)

Evaluation.eval 0 doc

let fsi = Evaluation.FsiEvaluator()
let res = fsi.Evaluate("=1 + 2")
let res = fsi.Evaluate("=1 + 3")
let res = fsi.Evaluate("=\"hi\"")

let res = fsi.Evaluate("= printfn \"hi\"; 1")
fsi.Evaluate("failwith \"byte\"")
*)