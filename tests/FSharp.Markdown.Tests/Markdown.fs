#if INTERACTIVE
#r "../../bin/FSharp.Markdown.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Markdown.Tests.Parsing
#endif

open FsUnit
open NUnit.Framework
open FSharp.Markdown

[<Test>]
let ``Headings ending with F# are parsed correctly`` () =
  let doc = """
## Hello F#
Some more""" |> Markdown.Parse

  doc.Paragraphs
  |> shouldEqual [
      Heading(2, [Literal "Hello F#"]); 
      Paragraph [Literal "Some more"]]

[<Test>]
let ``Headings ending with spaces followed by # are parsed correctly`` () =
  let doc = """
## Hello ####
Some more""" |> Markdown.Parse

  doc.Paragraphs
  |> shouldEqual [
      Heading(2, [Literal "Hello"]); 
      Paragraph [Literal "Some more"]]