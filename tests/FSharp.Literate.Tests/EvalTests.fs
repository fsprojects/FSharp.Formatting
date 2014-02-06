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
let content = """
(** hello *)
let test = 42
let test2 = 43 + test

(*** define-output:test ***)
printf "12343"

(** a value *)
(*** include-value: test ***)

(** 
# Some more code *)
let mocode f x =
    let y = x + 3 * (f x)
    y * 5

(** another value *)
(*** include-value: test2 ***)

(** an output *)
(*** include-output: test ***)
"""
let result = Literate.ParseScriptString(content, "C" @@ "A.fsx", formatAgent)
let html = Literate.WriteHtml(result)
html