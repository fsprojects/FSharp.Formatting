module FSharp.Markdown.Tests.CommonMarkSpecTest

open FsUnit
open NUnit.Framework
open FSharp.Markdown
open System.IO
open System.Diagnostics
let (++) a b = Path.Combine(a, b)
let testdir = __SOURCE_DIRECTORY__ ++ Path.Combine("..", "..", "tests")

let specHtmlFile = testdir ++ "commonmark_spec.htm"
let findTestCasesRegexString = """<pre><code class="language-markdown">(?<markdown>(((?!(>|<))(\w|\W|\s))|(<span class="space"> </span>))*)</code></pre>(\w|(?!(>|<))\W|\s)*<pre><code class="language-html">(?<html>(((?!(>|<))(\w|\W|\s))|(<span class="space"> </span>))*)</code></pre>"""
let findTestCasesRegex = System.Text.RegularExpressions.Regex(findTestCasesRegexString)
let getTestCases specHtmlFile =
  let specHtml = System.IO.File.ReadAllText(specHtmlFile)

  let matches = findTestCasesRegex.Matches(specHtml)
  let normalizeString (s:string) =
    s.Replace("→", "\t")
     .Replace("<span class=\"space\"> </span>", " ")

  seq {
    for g in matches do
      if g.Success then
        let markdown = g.Groups.["markdown"].Value
        let html = g.Groups.["html"].Value
        yield normalizeString markdown, System.Net.WebUtility.HtmlDecode(normalizeString html)
  }

let getTest () =
  getTestCases specHtmlFile
  |> Seq.mapi (fun i (markdown, html) -> TestCaseData(i, markdown, html))

[<Test>]
[<TestCaseSource("getTest")>]
let ``Run specification test`` (i : int) (markdown : string) (html : string) =
  (Markdown.TransformHtml markdown).Replace("\r\n", "\n")
  |> should equal html