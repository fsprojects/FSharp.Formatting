module FSharp.Markdown.Tests.CommonMarkSpecTest

open System.IO
open System.Diagnostics
let (++) a b = Path.Combine(a, b)
let testdir = __SOURCE_DIRECTORY__ ++ Path.Combine("..", "..", "tests")

open FSharp.Data
type CommonMarkSpecJson = JsonProvider<"../../tests/commonmark_spec.json">

let sample = CommonMarkSpecJson.GetSamples()
let sections =
  sample
  |> Seq.groupBy (fun s -> s.Section)

open FsUnit
open NUnit.Framework
open FSharp.Markdown

let enabledSections =
  [ "Fenced code blocks"
    "Indented code blocks"
    "Paragraphs"
    "Precedence"
    "Tabs" ]

let getTests () =
  sample
  |> Seq.mapi(fun i s ->
    let test = TestCaseData(s.Section, s.Markdown, match s.Html with Some html -> html | None -> "")
    if enabledSections |> List.contains s.Section |> not then
      test.Ignore("section is not enabled")
    elif s.Html.IsNone then
      test.Ignore("html was not given in the test json")
    else test)

[<Test>]
[<TestCaseSource("getTests")>]
let ``Commonmark specification`` (section:string) (markdown : string) (html : string) =
  printfn "Markdown: '%s'" markdown
  (Markdown.TransformHtml(markdown, "\n"))
  |> shouldEqual html

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
  |> shouldEqual html


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
  |> shouldEqual html
  
[<Test>]
let ``manual markdown test: use tabs for defining a list`` () =
  let markdown = "+ this is a list item
\tindented with tabs

+ this is a list item
  indented with spaces
"
  let html = """<ul>
<li>
<p>this is a list item
indented with tabs</p>
</li>
<li>
<p>this is a list item
indented with spaces</p>
</li>
</ul>
"""
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: test if we support continuation lines`` () =
  let markdown = "+ this is a list item
with a continuation line

+ this is a list item
  indented with spaces
"
  let html = """<ul>
<li>
<p>this is a list item
with a continuation line</p>
</li>
<li>
<p>this is a list item
indented with spaces</p>
</li>
</ul>
"""
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: test if we can handle paragraph ending with two spaces`` () =
  let markdown = "this is a paragraph ending with two spaces\t  
with a continuation line
"
  let html = "<p>this is a paragraph ending with two spaces\t<br />
with a continuation line</p>
"
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: test that we don't trim tab character at the end`` () =
  let markdown = "this is a paragraph ending with tab  \t
with a continuation line
"
  let html = "<p>this is a paragraph ending with tab  \t
with a continuation line</p>
"
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: test code block (with tabs) in list`` () =
  let markdown = "- \t  Code Block
"
  let html = "<ul>
<li>
<pre><code>  Code Block
</code></pre>
</li>
</ul>
"
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: test code block (with spaces) in list`` () =
  let markdown = "-       Code Block
"
  let html = "<ul>
<li>
<pre><code>  Code Block
</code></pre>
</li>
</ul>
"
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: blockquote with continuation`` () =
  let markdown = "> blockquote
with continuation
"
  let html = "<blockquote>
<p>blockquote
with continuation</p>
</blockquote>
"
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html

[<Test>]
let ``manual markdown test: blockquote without continuation`` () =
  let markdown = "> blockquote
# without continuation
"
  let html = "<blockquote>
<p>blockquote</p>
</blockquote>
<h1>without continuation</h1>
"
  (Markdown.TransformHtml(markdown))
  |> shouldEqual html