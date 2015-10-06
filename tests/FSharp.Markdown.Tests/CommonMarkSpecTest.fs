module FSharp.Markdown.Tests.CommonMarkSpecTest

open System.IO
open System.Diagnostics
let (++) a b = Path.Combine(a, b)
let testdir = __SOURCE_DIRECTORY__ ++ Path.Combine("..", "..", "tests")

let specHtmlFile = testdir ++ "commonmark_spec.htm"
let testCasesFile = testdir ++ "commonmark_spec_tests"
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

let createTestData data =
  let sb = new System.Text.StringBuilder()
  for markdown, html in data do
    sb.AppendFormat(":({0})_({1}):", markdown, html) |> ignore
  sb.ToString()

let parseTestData (data:string) =
  let splits = data.Split([|")::("|], System.StringSplitOptions.None)
  splits.[0] <- splits.[0].Substring(2)
  splits.[splits.Length - 1] <- splits.[splits.Length - 1].Substring(0, splits.[splits.Length - 1].Length - 2)
  splits
  |> Seq.map (fun data -> data.Split([|")_("|], System.StringSplitOptions.None))
  |> Seq.choose (function
      | [| left; right|] -> Some (left, right)
      | _ -> failwithf "could not parse test data")
  |> Seq.toList
  
let writeTestCases () =
  getTestCases specHtmlFile
  |> createTestData
  |> (fun data -> File.WriteAllText (testCasesFile, data))

let loadTestCases () =
  File.ReadAllText(testCasesFile)
  |> parseTestData

open FsUnit
open NUnit.Framework
open FSharp.Markdown

let getTests () =
  loadTestCases()
  |> Seq.mapi (fun i (markdown, html) -> TestCaseData(i, markdown, html))

[<Test>]
[<TestCaseSource("getTests")>]
let ``Run specification test`` (i : int) (markdown : string) (html : string) =
  (Markdown.TransformHtml(markdown, "\n"))
  |> should equal html

[<Test>]
let ``manual markdown test: show a blockquote with a code block`` () =
  let markdown = """Blockquotes can contain other Markdown elements, including headers, lists,
and code blocks:

	> ## This is a header.
	> 
	> 1.   This is the first list item.
	> 2.   This is the second list item.
	> 
	> Here's some example code:
	> 
	>     return shell_exec("echo $input | $markdown_script");

Any decent text editor should make email-style quoting easy."""
  let html = """<p>Blockquotes can contain other Markdown elements, including headers, lists,
and code blocks:</p>

<pre><code>&gt; ## This is a header.
&gt; 
&gt; 1.   This is the first list item.
&gt; 2.   This is the second list item.
&gt; 
&gt; Here's some example code:
&gt; 
&gt;     return shell_exec("echo $input | $markdown_script");
</code></pre>

<p>Any decent text editor should make email-style quoting easy.</p>
"""
  (Markdown.TransformHtml(markdown))
  |> should equal html


[<Test>]
let ``manual markdown test: use spaces in the first line of a code block (indent more than 4 spaces)`` () =
  let markdown = """For example, this:

        <div class="footer">
            &copy; 2004 Foo Corporation
        </div>

will turn into:"""
  let html = """<p>For example, this:</p>

<pre><code>    &lt;div class="footer"&gt;
        &amp;copy; 2004 Foo Corporation
    &lt;/div&gt;
</code></pre>

<p>will turn into:</p>
"""
  (Markdown.TransformHtml(markdown))
  |> should equal html
  
[<Test>]
let ``manual markdown test: use tabs for defining a list`` () =
  let markdown = "+ this is a list item
\tindented with tabs

+ this is a list item
  indented with spaces
"
  let html = """<ul>
<li><p>this is a list item
indented with tabs</p></li>
<li><p>this is a list item
indented with spaces</p></li>
</ul>
"""
  (Markdown.TransformHtml(markdown))
  |> should equal html

[<Test>]
let ``manual markdown test: test if we support continuation lines`` () =
  let markdown = "+ this is a list item
with a continuation line

+ this is a list item
  indented with spaces
"
  let html = """<ul>
<li><p>this is a list item
with a continuation line</p></li>
<li><p>this is a list item
indented with spaces</p></li>
</ul>
"""
  (Markdown.TransformHtml(markdown))
  |> should equal html

[<Test>]
let ``manual markdown test: test if we can handle paragraph ending with two spaces`` () =
  let markdown = "this is a paragraph ending with two spaces\t  
with a continuation line
"
  let html = "<p>this is a paragraph ending with two spaces\t<br />
with a continuation line</p>
"
  (Markdown.TransformHtml(markdown))
  |> should equal html

[<Test>]
let ``manual markdown test: test that we don't trim tab character at the end`` () =
  let markdown = "this is a paragraph ending with tab  \t
with a continuation line
"
  let html = "<p>this is a paragraph ending with tab  \t
with a continuation line</p>
"
  (Markdown.TransformHtml(markdown))
  |> should equal html

[<Test>]
let ``manual markdown test: test code block (with tabs) in list`` () =
  let markdown = "- \t  Code Block
"
  let html = "<ul>
<li><pre><code>  Code Block
</code></pre></li>
</ul>"
  (Markdown.TransformHtml(markdown))
  |> should equal html
  
[<Test>]
let ``manual markdown test: test code block (with spaces) in list`` () =
  let markdown = "-       Code Block
"
  let html = "<ul>
<li><pre><code>  Code Block
</code></pre></li>
</ul>"
  (Markdown.TransformHtml(markdown))
  |> should equal html
  
[<Test>]
let ``manual markdown test: blockquote with continuation`` () =
  let markdown = "> blockquote
with continuation
"
  let html = "<blockquote><p>blockquote
with continuation
</p></blockquote>"
  (Markdown.TransformHtml(markdown))
  |> should equal html
  
[<Test>]
let ``manual markdown test: blockquote without continuation`` () =
  let markdown = "> blockquote
# without continuation
"
  let html = "<blockquote><p>blockquote
</p></blockquote>
<h1>without continuation</h1>
"
  (Markdown.TransformHtml(markdown))
  |> should equal html