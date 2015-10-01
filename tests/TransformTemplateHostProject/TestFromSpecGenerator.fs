// --------------------------------------------------------------------------------------
// WARNING: THIS FILE IS AUTO-GENERATED
// ANY CHANGE WILL BE LOST
// PLEASE EDIT THE TestFromSpecGenerator.tt File
// --------------------------------------------------------------------------------------
module FSharp.Markdown.Tests.TestSpecification

open FsUnit
open NUnit.Framework
open FSharp.Markdown
open System.IO
open System.Diagnostics

let runTest markdown html =
  (Markdown.TransformHtml markdown).Replace("\r\n", "\n")
  |> should equal html
  ()

[<Test>]
let ``CommonMark Test (2)`` () =
  runTest
    "	foo	baz		bim\n"
    "<pre><code>foo	baz		bim\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (3)`` () =
  runTest
    "  	foo	baz		bim\n"
    "<pre><code>foo	baz		bim\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (4)`` () =
  runTest
    "    a	a\n    ὐ	a\n"
    "<pre><code>a	a\nὐ	a\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (5)`` () =
  runTest
    "  - foo\n\n	bar\n"
    "<ul>\n<li>\n<p>foo</p>\n<p>bar</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (6)`` () =
  runTest
    "&gt;	foo	bar\n"
    "<blockquote>\n<p>foo	bar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (7)`` () =
  runTest
    "    foo\n	bar\n"
    "<pre><code>foo\nbar\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (8)`` () =
  runTest
    "- `one\n- two`\n"
    "<ul>\n<li>`one</li>\n<li>two`</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (9)`` () =
  runTest
    "***\n---\n___\n"
    "<hr />\n<hr />\n<hr />\n"

[<Test>]
let ``CommonMark Test (10)`` () =
  runTest
    "+++\n"
    "<p>+++</p>\n"

[<Test>]
let ``CommonMark Test (11)`` () =
  runTest
    "===\n"
    "<p>===</p>\n"

[<Test>]
let ``CommonMark Test (12)`` () =
  runTest
    "--\n**\n__\n"
    "<p>--\n**\n__</p>\n"

[<Test>]
let ``CommonMark Test (13)`` () =
  runTest
    " ***\n  ***\n   ***\n"
    "<hr />\n<hr />\n<hr />\n"

[<Test>]
let ``CommonMark Test (14)`` () =
  runTest
    "    ***\n"
    "<pre><code>***\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (15)`` () =
  runTest
    "Foo\n    ***\n"
    "<p>Foo\n***</p>\n"

[<Test>]
let ``CommonMark Test (16)`` () =
  runTest
    "_____________________________________\n"
    "<hr />\n"

[<Test>]
let ``CommonMark Test (17)`` () =
  runTest
    " - - -\n"
    "<hr />\n"

[<Test>]
let ``CommonMark Test (18)`` () =
  runTest
    " **  * ** * ** * **\n"
    "<hr />\n"

[<Test>]
let ``CommonMark Test (19)`` () =
  runTest
    "-     -      -      -\n"
    "<hr />\n"

[<Test>]
let ``CommonMark Test (20)`` () =
  runTest
    "- - - -    \n"
    "<hr />\n"

[<Test>]
let ``CommonMark Test (21)`` () =
  runTest
    "_ _ _ _ a\n\na------\n\n---a---\n"
    "<p>_ _ _ _ a</p>\n<p>a------</p>\n<p>---a---</p>\n"

[<Test>]
let ``CommonMark Test (22)`` () =
  runTest
    " *-*\n"
    "<p><em>-</em></p>\n"

[<Test>]
let ``CommonMark Test (23)`` () =
  runTest
    "- foo\n***\n- bar\n"
    "<ul>\n<li>foo</li>\n</ul>\n<hr />\n<ul>\n<li>bar</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (24)`` () =
  runTest
    "Foo\n***\nbar\n"
    "<p>Foo</p>\n<hr />\n<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (25)`` () =
  runTest
    "Foo\n---\nbar\n"
    "<h2>Foo</h2>\n<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (26)`` () =
  runTest
    "* Foo\n* * *\n* Bar\n"
    "<ul>\n<li>Foo</li>\n</ul>\n<hr />\n<ul>\n<li>Bar</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (27)`` () =
  runTest
    "- Foo\n- * * *\n"
    "<ul>\n<li>Foo</li>\n<li>\n<hr />\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (28)`` () =
  runTest
    "# foo\n## foo\n### foo\n#### foo\n##### foo\n###### foo\n"
    "<h1>foo</h1>\n<h2>foo</h2>\n<h3>foo</h3>\n<h4>foo</h4>\n<h5>foo</h5>\n<h6>foo</h6>\n"

[<Test>]
let ``CommonMark Test (29)`` () =
  runTest
    "####### foo\n"
    "<p>####### foo</p>\n"

[<Test>]
let ``CommonMark Test (30)`` () =
  runTest
    "#5 bolt\n\n#foobar\n"
    "<p>#5 bolt</p>\n<p>#foobar</p>\n"

[<Test>]
let ``CommonMark Test (31)`` () =
  runTest
    "\\## foo\n"
    "<p>## foo</p>\n"

[<Test>]
let ``CommonMark Test (32)`` () =
  runTest
    "# foo *bar* \\*baz\\*\n"
    "<h1>foo <em>bar</em> *baz*</h1>\n"

[<Test>]
let ``CommonMark Test (33)`` () =
  runTest
    "#                  foo                     \n"
    "<h1>foo</h1>\n"

[<Test>]
let ``CommonMark Test (34)`` () =
  runTest
    " ### foo\n  ## foo\n   # foo\n"
    "<h3>foo</h3>\n<h2>foo</h2>\n<h1>foo</h1>\n"

[<Test>]
let ``CommonMark Test (35)`` () =
  runTest
    "    # foo\n"
    "<pre><code># foo\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (36)`` () =
  runTest
    "foo\n    # bar\n"
    "<p>foo\n# bar</p>\n"

[<Test>]
let ``CommonMark Test (37)`` () =
  runTest
    "## foo ##\n  ###   bar    ###\n"
    "<h2>foo</h2>\n<h3>bar</h3>\n"

[<Test>]
let ``CommonMark Test (38)`` () =
  runTest
    "# foo ##################################\n##### foo ##\n"
    "<h1>foo</h1>\n<h5>foo</h5>\n"

[<Test>]
let ``CommonMark Test (39)`` () =
  runTest
    "### foo ###     \n"
    "<h3>foo</h3>\n"

[<Test>]
let ``CommonMark Test (40)`` () =
  runTest
    "### foo ### b\n"
    "<h3>foo ### b</h3>\n"

[<Test>]
let ``CommonMark Test (41)`` () =
  runTest
    "# foo#\n"
    "<h1>foo#</h1>\n"

[<Test>]
let ``CommonMark Test (42)`` () =
  runTest
    "### foo \\###\n## foo #\\##\n# foo \\#\n"
    "<h3>foo ###</h3>\n<h2>foo ###</h2>\n<h1>foo #</h1>\n"

[<Test>]
let ``CommonMark Test (43)`` () =
  runTest
    "****\n## foo\n****\n"
    "<hr />\n<h2>foo</h2>\n<hr />\n"

[<Test>]
let ``CommonMark Test (44)`` () =
  runTest
    "Foo bar\n# baz\nBar foo\n"
    "<p>Foo bar</p>\n<h1>baz</h1>\n<p>Bar foo</p>\n"

[<Test>]
let ``CommonMark Test (45)`` () =
  runTest
    "## \n#\n### ###\n"
    "<h2></h2>\n<h1></h1>\n<h3></h3>\n"

[<Test>]
let ``CommonMark Test (46)`` () =
  runTest
    "Foo *bar*\n=========\n\nFoo *bar*\n---------\n"
    "<h1>Foo <em>bar</em></h1>\n<h2>Foo <em>bar</em></h2>\n"

[<Test>]
let ``CommonMark Test (47)`` () =
  runTest
    "Foo\n-------------------------\n\nFoo\n=\n"
    "<h2>Foo</h2>\n<h1>Foo</h1>\n"

[<Test>]
let ``CommonMark Test (48)`` () =
  runTest
    "   Foo\n---\n\n  Foo\n-----\n\n  Foo\n  ===\n"
    "<h2>Foo</h2>\n<h2>Foo</h2>\n<h1>Foo</h1>\n"

[<Test>]
let ``CommonMark Test (49)`` () =
  runTest
    "    Foo\n    ---\n\n    Foo\n---\n"
    "<pre><code>Foo\n---\n\nFoo\n</code></pre>\n<hr />\n"

[<Test>]
let ``CommonMark Test (50)`` () =
  runTest
    "Foo\n   ----      \n"
    "<h2>Foo</h2>\n"

[<Test>]
let ``CommonMark Test (51)`` () =
  runTest
    "Foo\n    ---\n"
    "<p>Foo\n---</p>\n"

[<Test>]
let ``CommonMark Test (52)`` () =
  runTest
    "Foo\n= =\n\nFoo\n--- -\n"
    "<p>Foo\n= =</p>\n<p>Foo</p>\n<hr />\n"

[<Test>]
let ``CommonMark Test (53)`` () =
  runTest
    "Foo  \n-----\n"
    "<h2>Foo</h2>\n"

[<Test>]
let ``CommonMark Test (54)`` () =
  runTest
    "Foo\\\n----\n"
    "<h2>Foo\\</h2>\n"

[<Test>]
let ``CommonMark Test (55)`` () =
  runTest
    "`Foo\n----\n`\n\n&lt;a title=&quot;a lot\n---\nof dashes&quot;/&gt;\n"
    "<h2>`Foo</h2>\n<p>`</p>\n<h2>&lt;a title=&quot;a lot</h2>\n<p>of dashes&quot;/&gt;</p>\n"

[<Test>]
let ``CommonMark Test (56)`` () =
  runTest
    "&gt; Foo\n---\n"
    "<blockquote>\n<p>Foo</p>\n</blockquote>\n<hr />\n"

[<Test>]
let ``CommonMark Test (57)`` () =
  runTest
    "- Foo\n---\n"
    "<ul>\n<li>Foo</li>\n</ul>\n<hr />\n"

[<Test>]
let ``CommonMark Test (58)`` () =
  runTest
    "Foo\nBar\n---\n\nFoo\nBar\n===\n"
    "<p>Foo\nBar</p>\n<hr />\n<p>Foo\nBar\n===</p>\n"

[<Test>]
let ``CommonMark Test (59)`` () =
  runTest
    "---\nFoo\n---\nBar\n---\nBaz\n"
    "<hr />\n<h2>Foo</h2>\n<h2>Bar</h2>\n<p>Baz</p>\n"

[<Test>]
let ``CommonMark Test (60)`` () =
  runTest
    "\n====\n"
    "<p>====</p>\n"

[<Test>]
let ``CommonMark Test (61)`` () =
  runTest
    "---\n---\n"
    "<hr />\n<hr />\n"

[<Test>]
let ``CommonMark Test (62)`` () =
  runTest
    "- foo\n-----\n"
    "<ul>\n<li>foo</li>\n</ul>\n<hr />\n"

[<Test>]
let ``CommonMark Test (63)`` () =
  runTest
    "    foo\n---\n"
    "<pre><code>foo\n</code></pre>\n<hr />\n"

[<Test>]
let ``CommonMark Test (64)`` () =
  runTest
    "&gt; foo\n-----\n"
    "<blockquote>\n<p>foo</p>\n</blockquote>\n<hr />\n"

[<Test>]
let ``CommonMark Test (65)`` () =
  runTest
    "\\&gt; foo\n------\n"
    "<h2>&gt; foo</h2>\n"

[<Test>]
let ``CommonMark Test (66)`` () =
  runTest
    "    a simple\n      indented code block\n"
    "<pre><code>a simple\n  indented code block\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (67)`` () =
  runTest
    "  - foo\n\n    bar\n"
    "<ul>\n<li>\n<p>foo</p>\n<p>bar</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (68)`` () =
  runTest
    "1.  foo\n\n    - bar\n"
    "<ol>\n<li>\n<p>foo</p>\n<ul>\n<li>bar</li>\n</ul>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (69)`` () =
  runTest
    "    &lt;a/&gt;\n    *hi*\n\n    - one\n"
    "<pre><code>&lt;a/&gt;\n*hi*\n\n- one\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (70)`` () =
  runTest
    "    chunk1\n\n    chunk2\n  \n \n \n    chunk3\n"
    "<pre><code>chunk1\n\nchunk2\n\n\n\nchunk3\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (71)`` () =
  runTest
    "    chunk1\n      \n      chunk2\n"
    "<pre><code>chunk1\n  \n  chunk2\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (72)`` () =
  runTest
    "Foo\n    bar\n\n"
    "<p>Foo\nbar</p>\n"

[<Test>]
let ``CommonMark Test (73)`` () =
  runTest
    "    foo\nbar\n"
    "<pre><code>foo\n</code></pre>\n<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (74)`` () =
  runTest
    "# Header\n    foo\nHeader\n------\n    foo\n----\n"
    "<h1>Header</h1>\n<pre><code>foo\n</code></pre>\n<h2>Header</h2>\n<pre><code>foo\n</code></pre>\n<hr />\n"

[<Test>]
let ``CommonMark Test (75)`` () =
  runTest
    "        foo\n    bar\n"
    "<pre><code>    foo\nbar\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (76)`` () =
  runTest
    "\n    \n    foo\n    \n\n"
    "<pre><code>foo\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (77)`` () =
  runTest
    "    foo  \n"
    "<pre><code>foo  \n</code></pre>\n"

[<Test>]
let ``CommonMark Test (78)`` () =
  runTest
    "```\n&lt;\n &gt;\n```\n"
    "<pre><code>&lt;\n &gt;\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (79)`` () =
  runTest
    "~~~\n&lt;\n &gt;\n~~~\n"
    "<pre><code>&lt;\n &gt;\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (80)`` () =
  runTest
    "```\naaa\n~~~\n```\n"
    "<pre><code>aaa\n~~~\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (81)`` () =
  runTest
    "~~~\naaa\n```\n~~~\n"
    "<pre><code>aaa\n```\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (82)`` () =
  runTest
    "````\naaa\n```\n``````\n"
    "<pre><code>aaa\n```\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (83)`` () =
  runTest
    "~~~~\naaa\n~~~\n~~~~\n"
    "<pre><code>aaa\n~~~\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (84)`` () =
  runTest
    "```\n"
    "<pre><code></code></pre>\n"

[<Test>]
let ``CommonMark Test (85)`` () =
  runTest
    "`````\n\n```\naaa\n"
    "<pre><code>\n```\naaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (86)`` () =
  runTest
    "&gt; ```\n&gt; aaa\n\nbbb\n"
    "<blockquote>\n<pre><code>aaa\n</code></pre>\n</blockquote>\n<p>bbb</p>\n"

[<Test>]
let ``CommonMark Test (87)`` () =
  runTest
    "```\n\n  \n```\n"
    "<pre><code>\n  \n</code></pre>\n"

[<Test>]
let ``CommonMark Test (88)`` () =
  runTest
    "```\n```\n"
    "<pre><code></code></pre>\n"

[<Test>]
let ``CommonMark Test (89)`` () =
  runTest
    " ```\n aaa\naaa\n```\n"
    "<pre><code>aaa\naaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (90)`` () =
  runTest
    "  ```\naaa\n  aaa\naaa\n  ```\n"
    "<pre><code>aaa\naaa\naaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (91)`` () =
  runTest
    "   ```\n   aaa\n    aaa\n  aaa\n   ```\n"
    "<pre><code>aaa\n aaa\naaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (92)`` () =
  runTest
    "    ```\n    aaa\n    ```\n"
    "<pre><code>```\naaa\n```\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (93)`` () =
  runTest
    "```\naaa\n  ```\n"
    "<pre><code>aaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (94)`` () =
  runTest
    "   ```\naaa\n  ```\n"
    "<pre><code>aaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (95)`` () =
  runTest
    "```\naaa\n    ```\n"
    "<pre><code>aaa\n    ```\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (96)`` () =
  runTest
    "``` ```\naaa\n"
    "<p><code></code>\naaa</p>\n"

[<Test>]
let ``CommonMark Test (97)`` () =
  runTest
    "~~~~~~\naaa\n~~~ ~~\n"
    "<pre><code>aaa\n~~~ ~~\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (98)`` () =
  runTest
    "foo\n```\nbar\n```\nbaz\n"
    "<p>foo</p>\n<pre><code>bar\n</code></pre>\n<p>baz</p>\n"

[<Test>]
let ``CommonMark Test (99)`` () =
  runTest
    "foo\n---\n~~~\nbar\n~~~\n# baz\n"
    "<h2>foo</h2>\n<pre><code>bar\n</code></pre>\n<h1>baz</h1>\n"

[<Test>]
let ``CommonMark Test (100)`` () =
  runTest
    "```ruby\ndef foo(x)\n  return 3\nend\n```\n"
    "<pre><code class=\"language-ruby\">def foo(x)\n  return 3\nend\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (101)`` () =
  runTest
    "~~~~    ruby startline=3 $%@#$\ndef foo(x)\n  return 3\nend\n~~~~~~~\n"
    "<pre><code class=\"language-ruby\">def foo(x)\n  return 3\nend\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (102)`` () =
  runTest
    "````;\n````\n"
    "<pre><code class=\"language-;\"></code></pre>\n"

[<Test>]
let ``CommonMark Test (103)`` () =
  runTest
    "``` aa ```\nfoo\n"
    "<p><code>aa</code>\nfoo</p>\n"

[<Test>]
let ``CommonMark Test (104)`` () =
  runTest
    "```\n``` aaa\n```\n"
    "<pre><code>``` aaa\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (105)`` () =
  runTest
    "&lt;table&gt;\n  &lt;tr&gt;\n    &lt;td&gt;\n           hi\n    &lt;/td&gt;\n  &lt;/tr&gt;\n&lt;/table&gt;\n\nokay.\n"
    "<table>\n  <tr>\n    <td>\n           hi\n    </td>\n  </tr>\n</table>\n<p>okay.</p>\n"

[<Test>]
let ``CommonMark Test (106)`` () =
  runTest
    " &lt;div&gt;\n  *hello*\n         &lt;foo&gt;&lt;a&gt;\n"
    " <div>\n  *hello*\n         <foo><a>\n"

[<Test>]
let ``CommonMark Test (107)`` () =
  runTest
    "&lt;/div&gt;\n*foo*\n"
    "</div>\n*foo*\n"

[<Test>]
let ``CommonMark Test (108)`` () =
  runTest
    "&lt;DIV CLASS=&quot;foo&quot;&gt;\n\n*Markdown*\n\n&lt;/DIV&gt;\n"
    "<DIV CLASS=\"foo\">\n<p><em>Markdown</em></p>\n</DIV>\n"

[<Test>]
let ``CommonMark Test (109)`` () =
  runTest
    "&lt;div id=&quot;foo&quot;\n  class=&quot;bar&quot;&gt;\n&lt;/div&gt;\n"
    "<div id=\"foo\"\n  class=\"bar\">\n</div>\n"

[<Test>]
let ``CommonMark Test (110)`` () =
  runTest
    "&lt;div id=&quot;foo&quot; class=&quot;bar\n  baz&quot;&gt;\n&lt;/div&gt;\n"
    "<div id=\"foo\" class=\"bar\n  baz\">\n</div>\n"

[<Test>]
let ``CommonMark Test (111)`` () =
  runTest
    "&lt;div&gt;\n*foo*\n\n*bar*\n"
    "<div>\n*foo*\n<p><em>bar</em></p>\n"

[<Test>]
let ``CommonMark Test (112)`` () =
  runTest
    "&lt;div id=&quot;foo&quot;\n*hi*\n"
    "<div id=\"foo\"\n*hi*\n"

[<Test>]
let ``CommonMark Test (113)`` () =
  runTest
    "&lt;div class\nfoo\n"
    "<div class\nfoo\n"

[<Test>]
let ``CommonMark Test (114)`` () =
  runTest
    "&lt;div *???-&amp;&amp;&amp;-&lt;---\n*foo*\n"
    "<div *???-&&&-<---\n*foo*\n"

[<Test>]
let ``CommonMark Test (115)`` () =
  runTest
    "&lt;div&gt;&lt;a href=&quot;bar&quot;&gt;*foo*&lt;/a&gt;&lt;/div&gt;\n"
    "<div><a href=\"bar\">*foo*</a></div>\n"

[<Test>]
let ``CommonMark Test (116)`` () =
  runTest
    "&lt;table&gt;&lt;tr&gt;&lt;td&gt;\nfoo\n&lt;/td&gt;&lt;/tr&gt;&lt;/table&gt;\n"
    "<table><tr><td>\nfoo\n</td></tr></table>\n"

[<Test>]
let ``CommonMark Test (117)`` () =
  runTest
    "&lt;div&gt;&lt;/div&gt;\n``` c\nint x = 33;\n```\n"
    "<div></div>\n``` c\nint x = 33;\n```\n"

[<Test>]
let ``CommonMark Test (118)`` () =
  runTest
    "&lt;a href=&quot;foo&quot;&gt;\n*bar*\n&lt;/a&gt;\n"
    "<a href=\"foo\">\n*bar*\n</a>\n"

[<Test>]
let ``CommonMark Test (119)`` () =
  runTest
    "&lt;Warning&gt;\n*bar*\n&lt;/Warning&gt;\n"
    "<Warning>\n*bar*\n</Warning>\n"

[<Test>]
let ``CommonMark Test (120)`` () =
  runTest
    "&lt;i class=&quot;foo&quot;&gt;\n*bar*\n&lt;/i&gt;\n"
    "<i class=\"foo\">\n*bar*\n</i>\n"

[<Test>]
let ``CommonMark Test (121)`` () =
  runTest
    "&lt;/ins&gt;\n*bar*\n"
    "</ins>\n*bar*\n"

[<Test>]
let ``CommonMark Test (122)`` () =
  runTest
    "&lt;del&gt;\n*foo*\n&lt;/del&gt;\n"
    "<del>\n*foo*\n</del>\n"

[<Test>]
let ``CommonMark Test (123)`` () =
  runTest
    "&lt;del&gt;\n\n*foo*\n\n&lt;/del&gt;\n"
    "<del>\n<p><em>foo</em></p>\n</del>\n"

[<Test>]
let ``CommonMark Test (124)`` () =
  runTest
    "&lt;del&gt;*foo*&lt;/del&gt;\n"
    "<p><del><em>foo</em></del></p>\n"

[<Test>]
let ``CommonMark Test (125)`` () =
  runTest
    "&lt;pre language=&quot;haskell&quot;&gt;&lt;code&gt;\nimport Text.HTML.TagSoup\n\nmain :: IO ()\nmain = print $ parseTags tags\n&lt;/code&gt;&lt;/pre&gt;\n"
    "<pre language=\"haskell\"><code>\nimport Text.HTML.TagSoup\n\nmain :: IO ()\nmain = print $ parseTags tags\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (126)`` () =
  runTest
    "&lt;script type=&quot;text/javascript&quot;&gt;\n// JavaScript example\n\ndocument.getElementById(&quot;demo&quot;).innerHTML = &quot;Hello JavaScript!&quot;;\n&lt;/script&gt;\n"
    "<script type=\"text/javascript\">\n// JavaScript example\n\ndocument.getElementById(\"demo\").innerHTML = \"Hello JavaScript!\";\n</script>\n"

[<Test>]
let ``CommonMark Test (127)`` () =
  runTest
    "&lt;style\n  type=&quot;text/css&quot;&gt;\nh1 {color:red;}\n\np {color:blue;}\n&lt;/style&gt;\n"
    "<style\n  type=\"text/css\">\nh1 {color:red;}\n\np {color:blue;}\n</style>\n"

[<Test>]
let ``CommonMark Test (128)`` () =
  runTest
    "&lt;style\n  type=&quot;text/css&quot;&gt;\n\nfoo\n"
    "<style\n  type=\"text/css\">\n\nfoo\n"

[<Test>]
let ``CommonMark Test (129)`` () =
  runTest
    "&gt; &lt;div&gt;\n&gt; foo\n\nbar\n"
    "<blockquote>\n<div>\nfoo\n</blockquote>\n<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (130)`` () =
  runTest
    "- &lt;div&gt;\n- foo\n"
    "<ul>\n<li>\n<div>\n</li>\n<li>foo</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (131)`` () =
  runTest
    "&lt;style&gt;p{color:red;}&lt;/style&gt;\n*foo*\n"
    "<style>p{color:red;}</style>\n<p><em>foo</em></p>\n"

[<Test>]
let ``CommonMark Test (132)`` () =
  runTest
    "&lt;!-- foo --&gt;*bar*\n*baz*\n"
    "<!-- foo -->*bar*\n<p><em>baz</em></p>\n"

[<Test>]
let ``CommonMark Test (133)`` () =
  runTest
    "&lt;script&gt;\nfoo\n&lt;/script&gt;1. *bar*\n"
    "<script>\nfoo\n</script>1. *bar*\n"

[<Test>]
let ``CommonMark Test (134)`` () =
  runTest
    "&lt;!-- Foo\n\nbar\n   baz --&gt;\n"
    "<!-- Foo\n\nbar\n   baz -->\n"

[<Test>]
let ``CommonMark Test (135)`` () =
  runTest
    "&lt;?php\n\n  echo '&gt;';\n\n?&gt;\n"
    "<?php\n\n  echo '>';\n\n?>\n"

[<Test>]
let ``CommonMark Test (136)`` () =
  runTest
    "&lt;!DOCTYPE html&gt;\n"
    "<!DOCTYPE html>\n"

[<Test>]
let ``CommonMark Test (137)`` () =
  runTest
    "&lt;![CDATA[\nfunction matchwo(a,b)\n{\n  if (a &lt; b &amp;&amp; a &lt; 0) then {\n    return 1;\n\n  } else {\n\n    return 0;\n  }\n}\n]]&gt;\n"
    "<![CDATA[\nfunction matchwo(a,b)\n{\n  if (a < b && a < 0) then {\n    return 1;\n\n  } else {\n\n    return 0;\n  }\n}\n]]>\n"

[<Test>]
let ``CommonMark Test (138)`` () =
  runTest
    "  &lt;!-- foo --&gt;\n\n    &lt;!-- foo --&gt;\n"
    "  <!-- foo -->\n<pre><code>&lt;!-- foo --&gt;\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (139)`` () =
  runTest
    "  &lt;div&gt;\n\n    &lt;div&gt;\n"
    "  <div>\n<pre><code>&lt;div&gt;\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (140)`` () =
  runTest
    "Foo\n&lt;div&gt;\nbar\n&lt;/div&gt;\n"
    "<p>Foo</p>\n<div>\nbar\n</div>\n"

[<Test>]
let ``CommonMark Test (141)`` () =
  runTest
    "&lt;div&gt;\nbar\n&lt;/div&gt;\n*foo*\n"
    "<div>\nbar\n</div>\n*foo*\n"

[<Test>]
let ``CommonMark Test (142)`` () =
  runTest
    "Foo\n&lt;a href=&quot;bar&quot;&gt;\nbaz\n"
    "<p>Foo\n<a href=\"bar\">\nbaz</p>\n"

[<Test>]
let ``CommonMark Test (143)`` () =
  runTest
    "&lt;div&gt;\n\n*Emphasized* text.\n\n&lt;/div&gt;\n"
    "<div>\n<p><em>Emphasized</em> text.</p>\n</div>\n"

[<Test>]
let ``CommonMark Test (144)`` () =
  runTest
    "&lt;div&gt;\n*Emphasized* text.\n&lt;/div&gt;\n"
    "<div>\n*Emphasized* text.\n</div>\n"

[<Test>]
let ``CommonMark Test (145)`` () =
  runTest
    "&lt;table&gt;\n\n&lt;tr&gt;\n\n&lt;td&gt;\nHi\n&lt;/td&gt;\n\n&lt;/tr&gt;\n\n&lt;/table&gt;\n"
    "<table>\n<tr>\n<td>\nHi\n</td>\n</tr>\n</table>\n"

[<Test>]
let ``CommonMark Test (146)`` () =
  runTest
    "&lt;table&gt;\n\n  &lt;tr&gt;\n\n    &lt;td&gt;\n      Hi\n    &lt;/td&gt;\n\n  &lt;/tr&gt;\n\n&lt;/table&gt;\n"
    "<table>\n  <tr>\n<pre><code>&lt;td&gt;\n  Hi\n&lt;/td&gt;\n</code></pre>\n  </tr>\n</table>\n"

[<Test>]
let ``CommonMark Test (147)`` () =
  runTest
    "[foo]: /url &quot;title&quot;\n\n[foo]\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (148)`` () =
  runTest
    "   [foo]: \n      /url  \n           'the title'  \n\n[foo]\n"
    "<p><a href=\"/url\" title=\"the title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (149)`` () =
  runTest
    "[Foo*bar\\]]:my_(url) 'title (with parens)'\n\n[Foo*bar\\]]\n"
    "<p><a href=\"my_(url)\" title=\"title (with parens)\">Foo*bar]</a></p>\n"

[<Test>]
let ``CommonMark Test (150)`` () =
  runTest
    "[Foo bar]:\n&lt;my url&gt;\n'title'\n\n[Foo bar]\n"
    "<p><a href=\"my%20url\" title=\"title\">Foo bar</a></p>\n"

[<Test>]
let ``CommonMark Test (151)`` () =
  runTest
    "[foo]: /url '\ntitle\nline1\nline2\n'\n\n[foo]\n"
    "<p><a href=\"/url\" title=\"\ntitle\nline1\nline2\n\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (152)`` () =
  runTest
    "[foo]: /url 'title\n\nwith blank line'\n\n[foo]\n"
    "<p>[foo]: /url 'title</p>\n<p>with blank line'</p>\n<p>[foo]</p>\n"

[<Test>]
let ``CommonMark Test (153)`` () =
  runTest
    "[foo]:\n/url\n\n[foo]\n"
    "<p><a href=\"/url\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (154)`` () =
  runTest
    "[foo]:\n\n[foo]\n"
    "<p>[foo]:</p>\n<p>[foo]</p>\n"

[<Test>]
let ``CommonMark Test (155)`` () =
  runTest
    "[foo]: /url\\bar\\*baz &quot;foo\\&quot;bar\\baz&quot;\n\n[foo]\n"
    "<p><a href=\"/url%5Cbar*baz\" title=\"foo&quot;bar\\baz\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (156)`` () =
  runTest
    "[foo]\n\n[foo]: url\n"
    "<p><a href=\"url\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (157)`` () =
  runTest
    "[foo]\n\n[foo]: first\n[foo]: second\n"
    "<p><a href=\"first\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (158)`` () =
  runTest
    "[FOO]: /url\n\n[Foo]\n"
    "<p><a href=\"/url\">Foo</a></p>\n"

[<Test>]
let ``CommonMark Test (159)`` () =
  runTest
    "[ΑΓΩ]: /φου\n\n[αγω]\n"
    "<p><a href=\"/%CF%86%CE%BF%CF%85\">αγω</a></p>\n"

[<Test>]
let ``CommonMark Test (160)`` () =
  runTest
    "[foo]: /url\n"
    ""

[<Test>]
let ``CommonMark Test (161)`` () =
  runTest
    "[\nfoo\n]: /url\nbar\n"
    "<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (162)`` () =
  runTest
    "[foo]: /url &quot;title&quot; ok\n"
    "<p>[foo]: /url &quot;title&quot; ok</p>\n"

[<Test>]
let ``CommonMark Test (163)`` () =
  runTest
    "[foo]: /url\n&quot;title&quot; ok\n"
    "<p>&quot;title&quot; ok</p>\n"

[<Test>]
let ``CommonMark Test (164)`` () =
  runTest
    "    [foo]: /url &quot;title&quot;\n\n[foo]\n"
    "<pre><code>[foo]: /url &quot;title&quot;\n</code></pre>\n<p>[foo]</p>\n"

[<Test>]
let ``CommonMark Test (165)`` () =
  runTest
    "```\n[foo]: /url\n```\n\n[foo]\n"
    "<pre><code>[foo]: /url\n</code></pre>\n<p>[foo]</p>\n"

[<Test>]
let ``CommonMark Test (166)`` () =
  runTest
    "Foo\n[bar]: /baz\n\n[bar]\n"
    "<p>Foo\n[bar]: /baz</p>\n<p>[bar]</p>\n"

[<Test>]
let ``CommonMark Test (167)`` () =
  runTest
    "# [Foo]\n[foo]: /url\n&gt; bar\n"
    "<h1><a href=\"/url\">Foo</a></h1>\n<blockquote>\n<p>bar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (168)`` () =
  runTest
    "[foo]: /foo-url &quot;foo&quot;\n[bar]: /bar-url\n  &quot;bar&quot;\n[baz]: /baz-url\n\n[foo],\n[bar],\n[baz]\n"
    "<p><a href=\"/foo-url\" title=\"foo\">foo</a>,\n<a href=\"/bar-url\" title=\"bar\">bar</a>,\n<a href=\"/baz-url\">baz</a></p>\n"

[<Test>]
let ``CommonMark Test (169)`` () =
  runTest
    "[foo]\n\n&gt; [foo]: /url\n"
    "<p><a href=\"/url\">foo</a></p>\n<blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (170)`` () =
  runTest
    "aaa\n\nbbb\n"
    "<p>aaa</p>\n<p>bbb</p>\n"

[<Test>]
let ``CommonMark Test (171)`` () =
  runTest
    "aaa\nbbb\n\nccc\nddd\n"
    "<p>aaa\nbbb</p>\n<p>ccc\nddd</p>\n"

[<Test>]
let ``CommonMark Test (172)`` () =
  runTest
    "aaa\n\n\nbbb\n"
    "<p>aaa</p>\n<p>bbb</p>\n"

[<Test>]
let ``CommonMark Test (173)`` () =
  runTest
    "  aaa\n bbb\n"
    "<p>aaa\nbbb</p>\n"

[<Test>]
let ``CommonMark Test (174)`` () =
  runTest
    "aaa\n             bbb\n                                       ccc\n"
    "<p>aaa\nbbb\nccc</p>\n"

[<Test>]
let ``CommonMark Test (175)`` () =
  runTest
    "   aaa\nbbb\n"
    "<p>aaa\nbbb</p>\n"

[<Test>]
let ``CommonMark Test (176)`` () =
  runTest
    "    aaa\nbbb\n"
    "<pre><code>aaa\n</code></pre>\n<p>bbb</p>\n"

[<Test>]
let ``CommonMark Test (177)`` () =
  runTest
    "aaa     \nbbb     \n"
    "<p>aaa<br />\nbbb</p>\n"

[<Test>]
let ``CommonMark Test (178)`` () =
  runTest
    "  \n\naaa\n  \n\n# aaa\n\n  \n"
    "<p>aaa</p>\n<h1>aaa</h1>\n"

[<Test>]
let ``CommonMark Test (179)`` () =
  runTest
    "&gt; # Foo\n&gt; bar\n&gt; baz\n"
    "<blockquote>\n<h1>Foo</h1>\n<p>bar\nbaz</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (180)`` () =
  runTest
    "&gt;# Foo\n&gt;bar\n&gt; baz\n"
    "<blockquote>\n<h1>Foo</h1>\n<p>bar\nbaz</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (181)`` () =
  runTest
    "   &gt; # Foo\n   &gt; bar\n &gt; baz\n"
    "<blockquote>\n<h1>Foo</h1>\n<p>bar\nbaz</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (182)`` () =
  runTest
    "    &gt; # Foo\n    &gt; bar\n    &gt; baz\n"
    "<pre><code>&gt; # Foo\n&gt; bar\n&gt; baz\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (183)`` () =
  runTest
    "&gt; # Foo\n&gt; bar\nbaz\n"
    "<blockquote>\n<h1>Foo</h1>\n<p>bar\nbaz</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (184)`` () =
  runTest
    "&gt; bar\nbaz\n&gt; foo\n"
    "<blockquote>\n<p>bar\nbaz\nfoo</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (185)`` () =
  runTest
    "&gt; foo\n---\n"
    "<blockquote>\n<p>foo</p>\n</blockquote>\n<hr />\n"

[<Test>]
let ``CommonMark Test (186)`` () =
  runTest
    "&gt; - foo\n- bar\n"
    "<blockquote>\n<ul>\n<li>foo</li>\n</ul>\n</blockquote>\n<ul>\n<li>bar</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (187)`` () =
  runTest
    "&gt;     foo\n    bar\n"
    "<blockquote>\n<pre><code>foo\n</code></pre>\n</blockquote>\n<pre><code>bar\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (188)`` () =
  runTest
    "&gt; ```\nfoo\n```\n"
    "<blockquote>\n<pre><code></code></pre>\n</blockquote>\n<p>foo</p>\n<pre><code></code></pre>\n"

[<Test>]
let ``CommonMark Test (189)`` () =
  runTest
    "&gt; foo\n    - bar\n"
    "<blockquote>\n<p>foo\n- bar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (190)`` () =
  runTest
    "&gt;\n"
    "<blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (191)`` () =
  runTest
    "&gt;\n&gt;  \n&gt; \n"
    "<blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (192)`` () =
  runTest
    "&gt;\n&gt; foo\n&gt;  \n"
    "<blockquote>\n<p>foo</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (193)`` () =
  runTest
    "&gt; foo\n\n&gt; bar\n"
    "<blockquote>\n<p>foo</p>\n</blockquote>\n<blockquote>\n<p>bar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (194)`` () =
  runTest
    "&gt; foo\n&gt; bar\n"
    "<blockquote>\n<p>foo\nbar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (195)`` () =
  runTest
    "&gt; foo\n&gt;\n&gt; bar\n"
    "<blockquote>\n<p>foo</p>\n<p>bar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (196)`` () =
  runTest
    "foo\n&gt; bar\n"
    "<p>foo</p>\n<blockquote>\n<p>bar</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (197)`` () =
  runTest
    "&gt; aaa\n***\n&gt; bbb\n"
    "<blockquote>\n<p>aaa</p>\n</blockquote>\n<hr />\n<blockquote>\n<p>bbb</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (198)`` () =
  runTest
    "&gt; bar\nbaz\n"
    "<blockquote>\n<p>bar\nbaz</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (199)`` () =
  runTest
    "&gt; bar\n\nbaz\n"
    "<blockquote>\n<p>bar</p>\n</blockquote>\n<p>baz</p>\n"

[<Test>]
let ``CommonMark Test (200)`` () =
  runTest
    "&gt; bar\n&gt;\nbaz\n"
    "<blockquote>\n<p>bar</p>\n</blockquote>\n<p>baz</p>\n"

[<Test>]
let ``CommonMark Test (201)`` () =
  runTest
    "&gt; &gt; &gt; foo\nbar\n"
    "<blockquote>\n<blockquote>\n<blockquote>\n<p>foo\nbar</p>\n</blockquote>\n</blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (202)`` () =
  runTest
    "&gt;&gt;&gt; foo\n&gt; bar\n&gt;&gt;baz\n"
    "<blockquote>\n<blockquote>\n<blockquote>\n<p>foo\nbar\nbaz</p>\n</blockquote>\n</blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (203)`` () =
  runTest
    "&gt;     code\n\n&gt;    not code\n"
    "<blockquote>\n<pre><code>code\n</code></pre>\n</blockquote>\n<blockquote>\n<p>not code</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (204)`` () =
  runTest
    "A paragraph\nwith two lines.\n\n    indented code\n\n&gt; A block quote.\n"
    "<p>A paragraph\nwith two lines.</p>\n<pre><code>indented code\n</code></pre>\n<blockquote>\n<p>A block quote.</p>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (205)`` () =
  runTest
    "1.  A paragraph\n    with two lines.\n\n        indented code\n\n    &gt; A block quote.\n"
    "<ol>\n<li>\n<p>A paragraph\nwith two lines.</p>\n<pre><code>indented code\n</code></pre>\n<blockquote>\n<p>A block quote.</p>\n</blockquote>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (206)`` () =
  runTest
    "- one\n\n two\n"
    "<ul>\n<li>one</li>\n</ul>\n<p>two</p>\n"

[<Test>]
let ``CommonMark Test (207)`` () =
  runTest
    "- one\n\n  two\n"
    "<ul>\n<li>\n<p>one</p>\n<p>two</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (208)`` () =
  runTest
    " -    one\n\n     two\n"
    "<ul>\n<li>one</li>\n</ul>\n<pre><code> two\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (209)`` () =
  runTest
    " -    one\n\n      two\n"
    "<ul>\n<li>\n<p>one</p>\n<p>two</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (210)`` () =
  runTest
    "   &gt; &gt; 1.  one\n&gt;&gt;\n&gt;&gt;     two\n"
    "<blockquote>\n<blockquote>\n<ol>\n<li>\n<p>one</p>\n<p>two</p>\n</li>\n</ol>\n</blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (211)`` () =
  runTest
    "&gt;&gt;- one\n&gt;&gt;\n  &gt;  &gt; two\n"
    "<blockquote>\n<blockquote>\n<ul>\n<li>one</li>\n</ul>\n<p>two</p>\n</blockquote>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (212)`` () =
  runTest
    "-one\n\n2.two\n"
    "<p>-one</p>\n<p>2.two</p>\n"

[<Test>]
let ``CommonMark Test (213)`` () =
  runTest
    "- foo\n\n  bar\n\n- foo\n\n\n  bar\n\n- ```\n  foo\n\n\n  bar\n  ```\n\n- baz\n\n  + ```\n    foo\n\n\n    bar\n    ```\n"
    "<ul>\n<li>\n<p>foo</p>\n<p>bar</p>\n</li>\n<li>\n<p>foo</p>\n</li>\n</ul>\n<p>bar</p>\n<ul>\n<li>\n<pre><code>foo\n\n\nbar\n</code></pre>\n</li>\n<li>\n<p>baz</p>\n<ul>\n<li>\n<pre><code>foo\n\n\nbar\n</code></pre>\n</li>\n</ul>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (214)`` () =
  runTest
    "1.  foo\n\n    ```\n    bar\n    ```\n\n    baz\n\n    &gt; bam\n"
    "<ol>\n<li>\n<p>foo</p>\n<pre><code>bar\n</code></pre>\n<p>baz</p>\n<blockquote>\n<p>bam</p>\n</blockquote>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (215)`` () =
  runTest
    "123456789. ok\n"
    "<ol start=\"123456789\">\n<li>ok</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (216)`` () =
  runTest
    "1234567890. not ok\n"
    "<p>1234567890. not ok</p>\n"

[<Test>]
let ``CommonMark Test (217)`` () =
  runTest
    "0. ok\n"
    "<ol start=\"0\">\n<li>ok</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (218)`` () =
  runTest
    "003. ok\n"
    "<ol start=\"3\">\n<li>ok</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (219)`` () =
  runTest
    "-1. not ok\n"
    "<p>-1. not ok</p>\n"

[<Test>]
let ``CommonMark Test (220)`` () =
  runTest
    "- foo\n\n      bar\n"
    "<ul>\n<li>\n<p>foo</p>\n<pre><code>bar\n</code></pre>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (221)`` () =
  runTest
    "  10.  foo\n\n           bar\n"
    "<ol start=\"10\">\n<li>\n<p>foo</p>\n<pre><code>bar\n</code></pre>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (222)`` () =
  runTest
    "    indented code\n\nparagraph\n\n    more code\n"
    "<pre><code>indented code\n</code></pre>\n<p>paragraph</p>\n<pre><code>more code\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (223)`` () =
  runTest
    "1.     indented code\n\n   paragraph\n\n       more code\n"
    "<ol>\n<li>\n<pre><code>indented code\n</code></pre>\n<p>paragraph</p>\n<pre><code>more code\n</code></pre>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (224)`` () =
  runTest
    "1.      indented code\n\n   paragraph\n\n       more code\n"
    "<ol>\n<li>\n<pre><code> indented code\n</code></pre>\n<p>paragraph</p>\n<pre><code>more code\n</code></pre>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (225)`` () =
  runTest
    "   foo\n\nbar\n"
    "<p>foo</p>\n<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (226)`` () =
  runTest
    "-    foo\n\n  bar\n"
    "<ul>\n<li>foo</li>\n</ul>\n<p>bar</p>\n"

[<Test>]
let ``CommonMark Test (227)`` () =
  runTest
    "-  foo\n\n   bar\n"
    "<ul>\n<li>\n<p>foo</p>\n<p>bar</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (228)`` () =
  runTest
    "-\n  foo\n-\n  ```\n  bar\n  ```\n-\n      baz\n"
    "<ul>\n<li>foo</li>\n<li>\n<pre><code>bar\n</code></pre>\n</li>\n<li>\n<pre><code>baz\n</code></pre>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (229)`` () =
  runTest
    "-\n\n  foo\n"
    "<ul>\n<li></li>\n</ul>\n<p>foo</p>\n"

[<Test>]
let ``CommonMark Test (230)`` () =
  runTest
    "- foo\n-\n- bar\n"
    "<ul>\n<li>foo</li>\n<li></li>\n<li>bar</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (231)`` () =
  runTest
    "- foo\n-   \n- bar\n"
    "<ul>\n<li>foo</li>\n<li></li>\n<li>bar</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (232)`` () =
  runTest
    "1. foo\n2.\n3. bar\n"
    "<ol>\n<li>foo</li>\n<li></li>\n<li>bar</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (233)`` () =
  runTest
    "*\n"
    "<ul>\n<li></li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (234)`` () =
  runTest
    " 1.  A paragraph\n     with two lines.\n\n         indented code\n\n     &gt; A block quote.\n"
    "<ol>\n<li>\n<p>A paragraph\nwith two lines.</p>\n<pre><code>indented code\n</code></pre>\n<blockquote>\n<p>A block quote.</p>\n</blockquote>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (235)`` () =
  runTest
    "  1.  A paragraph\n      with two lines.\n\n          indented code\n\n      &gt; A block quote.\n"
    "<ol>\n<li>\n<p>A paragraph\nwith two lines.</p>\n<pre><code>indented code\n</code></pre>\n<blockquote>\n<p>A block quote.</p>\n</blockquote>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (236)`` () =
  runTest
    "   1.  A paragraph\n       with two lines.\n\n           indented code\n\n       &gt; A block quote.\n"
    "<ol>\n<li>\n<p>A paragraph\nwith two lines.</p>\n<pre><code>indented code\n</code></pre>\n<blockquote>\n<p>A block quote.</p>\n</blockquote>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (237)`` () =
  runTest
    "    1.  A paragraph\n        with two lines.\n\n            indented code\n\n        &gt; A block quote.\n"
    "<pre><code>1.  A paragraph\n    with two lines.\n\n        indented code\n\n    &gt; A block quote.\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (238)`` () =
  runTest
    "  1.  A paragraph\nwith two lines.\n\n          indented code\n\n      &gt; A block quote.\n"
    "<ol>\n<li>\n<p>A paragraph\nwith two lines.</p>\n<pre><code>indented code\n</code></pre>\n<blockquote>\n<p>A block quote.</p>\n</blockquote>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (239)`` () =
  runTest
    "  1.  A paragraph\n    with two lines.\n"
    "<ol>\n<li>A paragraph\nwith two lines.</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (240)`` () =
  runTest
    "&gt; 1. &gt; Blockquote\ncontinued here.\n"
    "<blockquote>\n<ol>\n<li>\n<blockquote>\n<p>Blockquote\ncontinued here.</p>\n</blockquote>\n</li>\n</ol>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (241)`` () =
  runTest
    "&gt; 1. &gt; Blockquote\n&gt; continued here.\n"
    "<blockquote>\n<ol>\n<li>\n<blockquote>\n<p>Blockquote\ncontinued here.</p>\n</blockquote>\n</li>\n</ol>\n</blockquote>\n"

[<Test>]
let ``CommonMark Test (242)`` () =
  runTest
    "- foo\n  - bar\n    - baz\n"
    "<ul>\n<li>foo\n<ul>\n<li>bar\n<ul>\n<li>baz</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (243)`` () =
  runTest
    "- foo\n - bar\n  - baz\n"
    "<ul>\n<li>foo</li>\n<li>bar</li>\n<li>baz</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (244)`` () =
  runTest
    "10) foo\n    - bar\n"
    "<ol start=\"10\">\n<li>foo\n<ul>\n<li>bar</li>\n</ul>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (245)`` () =
  runTest
    "10) foo\n   - bar\n"
    "<ol start=\"10\">\n<li>foo</li>\n</ol>\n<ul>\n<li>bar</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (246)`` () =
  runTest
    "- - foo\n"
    "<ul>\n<li>\n<ul>\n<li>foo</li>\n</ul>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (247)`` () =
  runTest
    "1. - 2. foo\n"
    "<ol>\n<li>\n<ul>\n<li>\n<ol start=\"2\">\n<li>foo</li>\n</ol>\n</li>\n</ul>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (248)`` () =
  runTest
    "- # Foo\n- Bar\n  ---\n  baz\n"
    "<ul>\n<li>\n<h1>Foo</h1>\n</li>\n<li>\n<h2>Bar</h2>\nbaz</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (249)`` () =
  runTest
    "- foo\n- bar\n+ baz\n"
    "<ul>\n<li>foo</li>\n<li>bar</li>\n</ul>\n<ul>\n<li>baz</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (250)`` () =
  runTest
    "1. foo\n2. bar\n3) baz\n"
    "<ol>\n<li>foo</li>\n<li>bar</li>\n</ol>\n<ol start=\"3\">\n<li>baz</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (251)`` () =
  runTest
    "Foo\n- bar\n- baz\n"
    "<p>Foo</p>\n<ul>\n<li>bar</li>\n<li>baz</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (252)`` () =
  runTest
    "The number of windows in my house is\n14.  The number of doors is 6.\n"
    "<p>The number of windows in my house is</p>\n<ol start=\"14\">\n<li>The number of doors is 6.</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (253)`` () =
  runTest
    "- foo\n\n- bar\n\n\n- baz\n"
    "<ul>\n<li>\n<p>foo</p>\n</li>\n<li>\n<p>bar</p>\n</li>\n</ul>\n<ul>\n<li>baz</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (254)`` () =
  runTest
    "- foo\n\n\n  bar\n- baz\n"
    "<ul>\n<li>foo</li>\n</ul>\n<p>bar</p>\n<ul>\n<li>baz</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (255)`` () =
  runTest
    "- foo\n  - bar\n    - baz\n\n\n      bim\n"
    "<ul>\n<li>foo\n<ul>\n<li>bar\n<ul>\n<li>baz</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n<pre><code>  bim\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (256)`` () =
  runTest
    "- foo\n- bar\n\n\n- baz\n- bim\n"
    "<ul>\n<li>foo</li>\n<li>bar</li>\n</ul>\n<ul>\n<li>baz</li>\n<li>bim</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (257)`` () =
  runTest
    "-   foo\n\n    notcode\n\n-   foo\n\n\n    code\n"
    "<ul>\n<li>\n<p>foo</p>\n<p>notcode</p>\n</li>\n<li>\n<p>foo</p>\n</li>\n</ul>\n<pre><code>code\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (258)`` () =
  runTest
    "- a\n - b\n  - c\n   - d\n    - e\n   - f\n  - g\n - h\n- i\n"
    "<ul>\n<li>a</li>\n<li>b</li>\n<li>c</li>\n<li>d</li>\n<li>e</li>\n<li>f</li>\n<li>g</li>\n<li>h</li>\n<li>i</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (259)`` () =
  runTest
    "1. a\n\n  2. b\n\n    3. c\n"
    "<ol>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (260)`` () =
  runTest
    "- a\n- b\n\n- c\n"
    "<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (261)`` () =
  runTest
    "* a\n*\n\n* c\n"
    "<ul>\n<li>\n<p>a</p>\n</li>\n<li></li>\n<li>\n<p>c</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (262)`` () =
  runTest
    "- a\n- b\n\n  c\n- d\n"
    "<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n<p>c</p>\n</li>\n<li>\n<p>d</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (263)`` () =
  runTest
    "- a\n- b\n\n  [ref]: /url\n- d\n"
    "<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>d</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (264)`` () =
  runTest
    "- a\n- ```\n  b\n\n\n  ```\n- c\n"
    "<ul>\n<li>a</li>\n<li>\n<pre><code>b\n\n\n</code></pre>\n</li>\n<li>c</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (265)`` () =
  runTest
    "- a\n  - b\n\n    c\n- d\n"
    "<ul>\n<li>a\n<ul>\n<li>\n<p>b</p>\n<p>c</p>\n</li>\n</ul>\n</li>\n<li>d</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (266)`` () =
  runTest
    "* a\n  &gt; b\n  &gt;\n* c\n"
    "<ul>\n<li>a\n<blockquote>\n<p>b</p>\n</blockquote>\n</li>\n<li>c</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (267)`` () =
  runTest
    "- a\n  &gt; b\n  ```\n  c\n  ```\n- d\n"
    "<ul>\n<li>a\n<blockquote>\n<p>b</p>\n</blockquote>\n<pre><code>c\n</code></pre>\n</li>\n<li>d</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (268)`` () =
  runTest
    "- a\n"
    "<ul>\n<li>a</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (269)`` () =
  runTest
    "- a\n  - b\n"
    "<ul>\n<li>a\n<ul>\n<li>b</li>\n</ul>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (270)`` () =
  runTest
    "1. ```\n   foo\n   ```\n\n   bar\n"
    "<ol>\n<li>\n<pre><code>foo\n</code></pre>\n<p>bar</p>\n</li>\n</ol>\n"

[<Test>]
let ``CommonMark Test (271)`` () =
  runTest
    "* foo\n  * bar\n\n  baz\n"
    "<ul>\n<li>\n<p>foo</p>\n<ul>\n<li>bar</li>\n</ul>\n<p>baz</p>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (272)`` () =
  runTest
    "- a\n  - b\n  - c\n\n- d\n  - e\n  - f\n"
    "<ul>\n<li>\n<p>a</p>\n<ul>\n<li>b</li>\n<li>c</li>\n</ul>\n</li>\n<li>\n<p>d</p>\n<ul>\n<li>e</li>\n<li>f</li>\n</ul>\n</li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (273)`` () =
  runTest
    "`hi`lo`\n"
    "<p><code>hi</code>lo`</p>\n"

[<Test>]
let ``CommonMark Test (274)`` () =
  runTest
    "\\!\\&quot;\\#\\$\\%\\&amp;\\'\\(\\)\\*\\+\\,\\-\\.\\/\\:\\;\\&lt;\\=\\&gt;\\?\\@\\[\\\\\\]\\^\\_\\`\\{\\|\\}\\~\n"
    "<p>!&quot;#$%&amp;'()*+,-./:;&lt;=&gt;?@[\\]^_`{|}~</p>\n"

[<Test>]
let ``CommonMark Test (275)`` () =
  runTest
    "\\	\\A\\a\\ \\3\\φ\\«\n"
    "<p>\\	\\A\\a\\ \\3\\φ\\«</p>\n"

[<Test>]
let ``CommonMark Test (276)`` () =
  runTest
    "\\*not emphasized*\n\\&lt;br/&gt; not a tag\n\\[not a link](/foo)\n\\`not code`\n1\\. not a list\n\\* not a list\n\\# not a header\n\\[foo]: /url &quot;not a reference&quot;\n"
    "<p>*not emphasized*\n&lt;br/&gt; not a tag\n[not a link](/foo)\n`not code`\n1. not a list\n* not a list\n# not a header\n[foo]: /url &quot;not a reference&quot;</p>\n"

[<Test>]
let ``CommonMark Test (277)`` () =
  runTest
    "\\\\*emphasis*\n"
    "<p>\\<em>emphasis</em></p>\n"

[<Test>]
let ``CommonMark Test (278)`` () =
  runTest
    "foo\\\nbar\n"
    "<p>foo<br />\nbar</p>\n"

[<Test>]
let ``CommonMark Test (279)`` () =
  runTest
    "`` \\[\\` ``\n"
    "<p><code>\\[\\`</code></p>\n"

[<Test>]
let ``CommonMark Test (280)`` () =
  runTest
    "    \\[\\]\n"
    "<pre><code>\\[\\]\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (281)`` () =
  runTest
    "~~~\n\\[\\]\n~~~\n"
    "<pre><code>\\[\\]\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (282)`` () =
  runTest
    "&lt;http://example.com?find=\\*&gt;\n"
    "<p><a href=\"http://example.com?find=%5C*\">http://example.com?find=\\*</a></p>\n"

[<Test>]
let ``CommonMark Test (283)`` () =
  runTest
    "&lt;a href=&quot;/bar\\/)&quot;&gt;\n"
    "<a href=\"/bar\\/)\">\n"

[<Test>]
let ``CommonMark Test (284)`` () =
  runTest
    "[foo](/bar\\* &quot;ti\\*tle&quot;)\n"
    "<p><a href=\"/bar*\" title=\"ti*tle\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (285)`` () =
  runTest
    "[foo]\n\n[foo]: /bar\\* &quot;ti\\*tle&quot;\n"
    "<p><a href=\"/bar*\" title=\"ti*tle\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (286)`` () =
  runTest
    "``` foo\\+bar\nfoo\n```\n"
    "<pre><code class=\"language-foo+bar\">foo\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (287)`` () =
  runTest
    "&amp;nbsp; &amp;amp; &amp;copy; &amp;AElig; &amp;Dcaron;\n&amp;frac34; &amp;HilbertSpace; &amp;DifferentialD;\n&amp;ClockwiseContourIntegral; &amp;ngE;\n"
    "<p>  &amp; © Æ Ď\n¾ ℋ ⅆ\n∲ ≧̸</p>\n"

[<Test>]
let ``CommonMark Test (288)`` () =
  runTest
    "&amp;#35; &amp;#1234; &amp;#992; &amp;#98765432; &amp;#0;\n"
    "<p># Ӓ Ϡ � �</p>\n"

[<Test>]
let ``CommonMark Test (289)`` () =
  runTest
    "&amp;#X22; &amp;#XD06; &amp;#xcab;\n"
    "<p>&quot; ആ ಫ</p>\n"

[<Test>]
let ``CommonMark Test (290)`` () =
  runTest
    "&amp;nbsp &amp;x; &amp;#; &amp;#x; &amp;ThisIsWayTooLongToBeAnEntityIsntIt; &amp;hi?;\n"
    "<p>&amp;nbsp &amp;x; &amp;#; &amp;#x; &amp;ThisIsWayTooLongToBeAnEntityIsntIt; &amp;hi?;</p>\n"

[<Test>]
let ``CommonMark Test (291)`` () =
  runTest
    "&amp;copy\n"
    "<p>&amp;copy</p>\n"

[<Test>]
let ``CommonMark Test (292)`` () =
  runTest
    "&amp;MadeUpEntity;\n"
    "<p>&amp;MadeUpEntity;</p>\n"

[<Test>]
let ``CommonMark Test (293)`` () =
  runTest
    "&lt;a href=&quot;&amp;ouml;&amp;ouml;.html&quot;&gt;\n"
    "<a href=\"&ouml;&ouml;.html\">\n"

[<Test>]
let ``CommonMark Test (294)`` () =
  runTest
    "[foo](/f&amp;ouml;&amp;ouml; &quot;f&amp;ouml;&amp;ouml;&quot;)\n"
    "<p><a href=\"/f%C3%B6%C3%B6\" title=\"föö\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (295)`` () =
  runTest
    "[foo]\n\n[foo]: /f&amp;ouml;&amp;ouml; &quot;f&amp;ouml;&amp;ouml;&quot;\n"
    "<p><a href=\"/f%C3%B6%C3%B6\" title=\"föö\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (296)`` () =
  runTest
    "``` f&amp;ouml;&amp;ouml;\nfoo\n```\n"
    "<pre><code class=\"language-föö\">foo\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (297)`` () =
  runTest
    "`f&amp;ouml;&amp;ouml;`\n"
    "<p><code>f&amp;ouml;&amp;ouml;</code></p>\n"

[<Test>]
let ``CommonMark Test (298)`` () =
  runTest
    "    f&amp;ouml;f&amp;ouml;\n"
    "<pre><code>f&amp;ouml;f&amp;ouml;\n</code></pre>\n"

[<Test>]
let ``CommonMark Test (299)`` () =
  runTest
    "`foo`\n"
    "<p><code>foo</code></p>\n"

[<Test>]
let ``CommonMark Test (300)`` () =
  runTest
    "`` foo ` bar  ``\n"
    "<p><code>foo ` bar</code></p>\n"

[<Test>]
let ``CommonMark Test (301)`` () =
  runTest
    "` `` `\n"
    "<p><code>``</code></p>\n"

[<Test>]
let ``CommonMark Test (302)`` () =
  runTest
    "``\nfoo\n``\n"
    "<p><code>foo</code></p>\n"

[<Test>]
let ``CommonMark Test (303)`` () =
  runTest
    "`foo   bar\n  baz`\n"
    "<p><code>foo bar baz</code></p>\n"

[<Test>]
let ``CommonMark Test (304)`` () =
  runTest
    "`foo `` bar`\n"
    "<p><code>foo `` bar</code></p>\n"

[<Test>]
let ``CommonMark Test (305)`` () =
  runTest
    "`foo\\`bar`\n"
    "<p><code>foo\\</code>bar`</p>\n"

[<Test>]
let ``CommonMark Test (306)`` () =
  runTest
    "*foo`*`\n"
    "<p>*foo<code>*</code></p>\n"

[<Test>]
let ``CommonMark Test (307)`` () =
  runTest
    "[not a `link](/foo`)\n"
    "<p>[not a <code>link](/foo</code>)</p>\n"

[<Test>]
let ``CommonMark Test (308)`` () =
  runTest
    "`&lt;a href=&quot;`&quot;&gt;`\n"
    "<p><code>&lt;a href=&quot;</code>&quot;&gt;`</p>\n"

[<Test>]
let ``CommonMark Test (309)`` () =
  runTest
    "&lt;a href=&quot;`&quot;&gt;`\n"
    "<p><a href=\"`\">`</p>\n"

[<Test>]
let ``CommonMark Test (310)`` () =
  runTest
    "`&lt;http://foo.bar.`baz&gt;`\n"
    "<p><code>&lt;http://foo.bar.</code>baz&gt;`</p>\n"

[<Test>]
let ``CommonMark Test (311)`` () =
  runTest
    "&lt;http://foo.bar.`baz&gt;`\n"
    "<p><a href=\"http://foo.bar.%60baz\">http://foo.bar.`baz</a>`</p>\n"

[<Test>]
let ``CommonMark Test (312)`` () =
  runTest
    "```foo``\n"
    "<p>```foo``</p>\n"

[<Test>]
let ``CommonMark Test (313)`` () =
  runTest
    "`foo\n"
    "<p>`foo</p>\n"

[<Test>]
let ``CommonMark Test (314)`` () =
  runTest
    "*foo bar*\n"
    "<p><em>foo bar</em></p>\n"

[<Test>]
let ``CommonMark Test (315)`` () =
  runTest
    "a * foo bar*\n"
    "<p>a * foo bar*</p>\n"

[<Test>]
let ``CommonMark Test (316)`` () =
  runTest
    "a*&quot;foo&quot;*\n"
    "<p>a*&quot;foo&quot;*</p>\n"

[<Test>]
let ``CommonMark Test (317)`` () =
  runTest
    "* a *\n"
    "<p>* a *</p>\n"

[<Test>]
let ``CommonMark Test (318)`` () =
  runTest
    "foo*bar*\n"
    "<p>foo<em>bar</em></p>\n"

[<Test>]
let ``CommonMark Test (319)`` () =
  runTest
    "5*6*78\n"
    "<p>5<em>6</em>78</p>\n"

[<Test>]
let ``CommonMark Test (320)`` () =
  runTest
    "_foo bar_\n"
    "<p><em>foo bar</em></p>\n"

[<Test>]
let ``CommonMark Test (321)`` () =
  runTest
    "_ foo bar_\n"
    "<p>_ foo bar_</p>\n"

[<Test>]
let ``CommonMark Test (322)`` () =
  runTest
    "a_&quot;foo&quot;_\n"
    "<p>a_&quot;foo&quot;_</p>\n"

[<Test>]
let ``CommonMark Test (323)`` () =
  runTest
    "foo_bar_\n"
    "<p>foo_bar_</p>\n"

[<Test>]
let ``CommonMark Test (324)`` () =
  runTest
    "5_6_78\n"
    "<p>5_6_78</p>\n"

[<Test>]
let ``CommonMark Test (325)`` () =
  runTest
    "пристаням_стремятся_\n"
    "<p>пристаням_стремятся_</p>\n"

[<Test>]
let ``CommonMark Test (326)`` () =
  runTest
    "aa_&quot;bb&quot;_cc\n"
    "<p>aa_&quot;bb&quot;_cc</p>\n"

[<Test>]
let ``CommonMark Test (327)`` () =
  runTest
    "foo-_(bar)_\n"
    "<p>foo-<em>(bar)</em></p>\n"

[<Test>]
let ``CommonMark Test (328)`` () =
  runTest
    "_foo*\n"
    "<p>_foo*</p>\n"

[<Test>]
let ``CommonMark Test (329)`` () =
  runTest
    "*foo bar *\n"
    "<p>*foo bar *</p>\n"

[<Test>]
let ``CommonMark Test (330)`` () =
  runTest
    "*foo bar\n*\n"
    "<p>*foo bar</p>\n<ul>\n<li></li>\n</ul>\n"

[<Test>]
let ``CommonMark Test (331)`` () =
  runTest
    "*(*foo)\n"
    "<p>*(*foo)</p>\n"

[<Test>]
let ``CommonMark Test (332)`` () =
  runTest
    "*(*foo*)*\n"
    "<p><em>(<em>foo</em>)</em></p>\n"

[<Test>]
let ``CommonMark Test (333)`` () =
  runTest
    "*foo*bar\n"
    "<p><em>foo</em>bar</p>\n"

[<Test>]
let ``CommonMark Test (334)`` () =
  runTest
    "_foo bar _\n"
    "<p>_foo bar _</p>\n"

[<Test>]
let ``CommonMark Test (335)`` () =
  runTest
    "_(_foo)\n"
    "<p>_(_foo)</p>\n"

[<Test>]
let ``CommonMark Test (336)`` () =
  runTest
    "_(_foo_)_\n"
    "<p><em>(<em>foo</em>)</em></p>\n"

[<Test>]
let ``CommonMark Test (337)`` () =
  runTest
    "_foo_bar\n"
    "<p>_foo_bar</p>\n"

[<Test>]
let ``CommonMark Test (338)`` () =
  runTest
    "_пристаням_стремятся\n"
    "<p>_пристаням_стремятся</p>\n"

[<Test>]
let ``CommonMark Test (339)`` () =
  runTest
    "_foo_bar_baz_\n"
    "<p><em>foo_bar_baz</em></p>\n"

[<Test>]
let ``CommonMark Test (340)`` () =
  runTest
    "_(bar)_.\n"
    "<p><em>(bar)</em>.</p>\n"

[<Test>]
let ``CommonMark Test (341)`` () =
  runTest
    "**foo bar**\n"
    "<p><strong>foo bar</strong></p>\n"

[<Test>]
let ``CommonMark Test (342)`` () =
  runTest
    "** foo bar**\n"
    "<p>** foo bar**</p>\n"

[<Test>]
let ``CommonMark Test (343)`` () =
  runTest
    "a**&quot;foo&quot;**\n"
    "<p>a**&quot;foo&quot;**</p>\n"

[<Test>]
let ``CommonMark Test (344)`` () =
  runTest
    "foo**bar**\n"
    "<p>foo<strong>bar</strong></p>\n"

[<Test>]
let ``CommonMark Test (345)`` () =
  runTest
    "__foo bar__\n"
    "<p><strong>foo bar</strong></p>\n"

[<Test>]
let ``CommonMark Test (346)`` () =
  runTest
    "__ foo bar__\n"
    "<p>__ foo bar__</p>\n"

[<Test>]
let ``CommonMark Test (347)`` () =
  runTest
    "__\nfoo bar__\n"
    "<p>__\nfoo bar__</p>\n"

[<Test>]
let ``CommonMark Test (348)`` () =
  runTest
    "a__&quot;foo&quot;__\n"
    "<p>a__&quot;foo&quot;__</p>\n"

[<Test>]
let ``CommonMark Test (349)`` () =
  runTest
    "foo__bar__\n"
    "<p>foo__bar__</p>\n"

[<Test>]
let ``CommonMark Test (350)`` () =
  runTest
    "5__6__78\n"
    "<p>5__6__78</p>\n"

[<Test>]
let ``CommonMark Test (351)`` () =
  runTest
    "пристаням__стремятся__\n"
    "<p>пристаням__стремятся__</p>\n"

[<Test>]
let ``CommonMark Test (352)`` () =
  runTest
    "__foo, __bar__, baz__\n"
    "<p><strong>foo, <strong>bar</strong>, baz</strong></p>\n"

[<Test>]
let ``CommonMark Test (353)`` () =
  runTest
    "foo-__(bar)__\n"
    "<p>foo-<strong>(bar)</strong></p>\n"

[<Test>]
let ``CommonMark Test (354)`` () =
  runTest
    "**foo bar **\n"
    "<p>**foo bar **</p>\n"

[<Test>]
let ``CommonMark Test (355)`` () =
  runTest
    "**(**foo)\n"
    "<p>**(**foo)</p>\n"

[<Test>]
let ``CommonMark Test (356)`` () =
  runTest
    "*(**foo**)*\n"
    "<p><em>(<strong>foo</strong>)</em></p>\n"

[<Test>]
let ``CommonMark Test (357)`` () =
  runTest
    "**Gomphocarpus (*Gomphocarpus physocarpus*, syn.\n*Asclepias physocarpa*)**\n"
    "<p><strong>Gomphocarpus (<em>Gomphocarpus physocarpus</em>, syn.\n<em>Asclepias physocarpa</em>)</strong></p>\n"

[<Test>]
let ``CommonMark Test (358)`` () =
  runTest
    "**foo &quot;*bar*&quot; foo**\n"
    "<p><strong>foo &quot;<em>bar</em>&quot; foo</strong></p>\n"

[<Test>]
let ``CommonMark Test (359)`` () =
  runTest
    "**foo**bar\n"
    "<p><strong>foo</strong>bar</p>\n"

[<Test>]
let ``CommonMark Test (360)`` () =
  runTest
    "__foo bar __\n"
    "<p>__foo bar __</p>\n"

[<Test>]
let ``CommonMark Test (361)`` () =
  runTest
    "__(__foo)\n"
    "<p>__(__foo)</p>\n"

[<Test>]
let ``CommonMark Test (362)`` () =
  runTest
    "_(__foo__)_\n"
    "<p><em>(<strong>foo</strong>)</em></p>\n"

[<Test>]
let ``CommonMark Test (363)`` () =
  runTest
    "__foo__bar\n"
    "<p>__foo__bar</p>\n"

[<Test>]
let ``CommonMark Test (364)`` () =
  runTest
    "__пристаням__стремятся\n"
    "<p>__пристаням__стремятся</p>\n"

[<Test>]
let ``CommonMark Test (365)`` () =
  runTest
    "__foo__bar__baz__\n"
    "<p><strong>foo__bar__baz</strong></p>\n"

[<Test>]
let ``CommonMark Test (366)`` () =
  runTest
    "__(bar)__.\n"
    "<p><strong>(bar)</strong>.</p>\n"

[<Test>]
let ``CommonMark Test (367)`` () =
  runTest
    "*foo [bar](/url)*\n"
    "<p><em>foo <a href=\"/url\">bar</a></em></p>\n"

[<Test>]
let ``CommonMark Test (368)`` () =
  runTest
    "*foo\nbar*\n"
    "<p><em>foo\nbar</em></p>\n"

[<Test>]
let ``CommonMark Test (369)`` () =
  runTest
    "_foo __bar__ baz_\n"
    "<p><em>foo <strong>bar</strong> baz</em></p>\n"

[<Test>]
let ``CommonMark Test (370)`` () =
  runTest
    "_foo _bar_ baz_\n"
    "<p><em>foo <em>bar</em> baz</em></p>\n"

[<Test>]
let ``CommonMark Test (371)`` () =
  runTest
    "__foo_ bar_\n"
    "<p><em><em>foo</em> bar</em></p>\n"

[<Test>]
let ``CommonMark Test (372)`` () =
  runTest
    "*foo *bar**\n"
    "<p><em>foo <em>bar</em></em></p>\n"

[<Test>]
let ``CommonMark Test (373)`` () =
  runTest
    "*foo **bar** baz*\n"
    "<p><em>foo <strong>bar</strong> baz</em></p>\n"

[<Test>]
let ``CommonMark Test (374)`` () =
  runTest
    "*foo**bar**baz*\n"
    "<p><em>foo</em><em>bar</em><em>baz</em></p>\n"

[<Test>]
let ``CommonMark Test (375)`` () =
  runTest
    "***foo** bar*\n"
    "<p><em><strong>foo</strong> bar</em></p>\n"

[<Test>]
let ``CommonMark Test (376)`` () =
  runTest
    "*foo **bar***\n"
    "<p><em>foo <strong>bar</strong></em></p>\n"

[<Test>]
let ``CommonMark Test (377)`` () =
  runTest
    "*foo**bar***\n"
    "<p><em>foo</em><em>bar</em>**</p>\n"

[<Test>]
let ``CommonMark Test (378)`` () =
  runTest
    "*foo **bar *baz* bim** bop*\n"
    "<p><em>foo <strong>bar <em>baz</em> bim</strong> bop</em></p>\n"

[<Test>]
let ``CommonMark Test (379)`` () =
  runTest
    "*foo [*bar*](/url)*\n"
    "<p><em>foo <a href=\"/url\"><em>bar</em></a></em></p>\n"

[<Test>]
let ``CommonMark Test (380)`` () =
  runTest
    "** is not an empty emphasis\n"
    "<p>** is not an empty emphasis</p>\n"

[<Test>]
let ``CommonMark Test (381)`` () =
  runTest
    "**** is not an empty strong emphasis\n"
    "<p>**** is not an empty strong emphasis</p>\n"

[<Test>]
let ``CommonMark Test (382)`` () =
  runTest
    "**foo [bar](/url)**\n"
    "<p><strong>foo <a href=\"/url\">bar</a></strong></p>\n"

[<Test>]
let ``CommonMark Test (383)`` () =
  runTest
    "**foo\nbar**\n"
    "<p><strong>foo\nbar</strong></p>\n"

[<Test>]
let ``CommonMark Test (384)`` () =
  runTest
    "__foo _bar_ baz__\n"
    "<p><strong>foo <em>bar</em> baz</strong></p>\n"

[<Test>]
let ``CommonMark Test (385)`` () =
  runTest
    "__foo __bar__ baz__\n"
    "<p><strong>foo <strong>bar</strong> baz</strong></p>\n"

[<Test>]
let ``CommonMark Test (386)`` () =
  runTest
    "____foo__ bar__\n"
    "<p><strong><strong>foo</strong> bar</strong></p>\n"

[<Test>]
let ``CommonMark Test (387)`` () =
  runTest
    "**foo **bar****\n"
    "<p><strong>foo <strong>bar</strong></strong></p>\n"

[<Test>]
let ``CommonMark Test (388)`` () =
  runTest
    "**foo *bar* baz**\n"
    "<p><strong>foo <em>bar</em> baz</strong></p>\n"

[<Test>]
let ``CommonMark Test (389)`` () =
  runTest
    "**foo*bar*baz**\n"
    "<p><em><em>foo</em>bar</em>baz**</p>\n"

[<Test>]
let ``CommonMark Test (390)`` () =
  runTest
    "***foo* bar**\n"
    "<p><strong><em>foo</em> bar</strong></p>\n"

[<Test>]
let ``CommonMark Test (391)`` () =
  runTest
    "**foo *bar***\n"
    "<p><strong>foo <em>bar</em></strong></p>\n"

[<Test>]
let ``CommonMark Test (392)`` () =
  runTest
    "**foo *bar **baz**\nbim* bop**\n"
    "<p><strong>foo <em>bar <strong>baz</strong>\nbim</em> bop</strong></p>\n"

[<Test>]
let ``CommonMark Test (393)`` () =
  runTest
    "**foo [*bar*](/url)**\n"
    "<p><strong>foo <a href=\"/url\"><em>bar</em></a></strong></p>\n"

[<Test>]
let ``CommonMark Test (394)`` () =
  runTest
    "__ is not an empty emphasis\n"
    "<p>__ is not an empty emphasis</p>\n"

[<Test>]
let ``CommonMark Test (395)`` () =
  runTest
    "____ is not an empty strong emphasis\n"
    "<p>____ is not an empty strong emphasis</p>\n"

[<Test>]
let ``CommonMark Test (396)`` () =
  runTest
    "foo ***\n"
    "<p>foo ***</p>\n"

[<Test>]
let ``CommonMark Test (397)`` () =
  runTest
    "foo *\\**\n"
    "<p>foo <em>*</em></p>\n"

[<Test>]
let ``CommonMark Test (398)`` () =
  runTest
    "foo *_*\n"
    "<p>foo <em>_</em></p>\n"

[<Test>]
let ``CommonMark Test (399)`` () =
  runTest
    "foo *****\n"
    "<p>foo *****</p>\n"

[<Test>]
let ``CommonMark Test (400)`` () =
  runTest
    "foo **\\***\n"
    "<p>foo <strong>*</strong></p>\n"

[<Test>]
let ``CommonMark Test (401)`` () =
  runTest
    "foo **_**\n"
    "<p>foo <strong>_</strong></p>\n"

[<Test>]
let ``CommonMark Test (402)`` () =
  runTest
    "**foo*\n"
    "<p>*<em>foo</em></p>\n"

[<Test>]
let ``CommonMark Test (403)`` () =
  runTest
    "*foo**\n"
    "<p><em>foo</em>*</p>\n"

[<Test>]
let ``CommonMark Test (404)`` () =
  runTest
    "***foo**\n"
    "<p>*<strong>foo</strong></p>\n"

[<Test>]
let ``CommonMark Test (405)`` () =
  runTest
    "****foo*\n"
    "<p>***<em>foo</em></p>\n"

[<Test>]
let ``CommonMark Test (406)`` () =
  runTest
    "**foo***\n"
    "<p><strong>foo</strong>*</p>\n"

[<Test>]
let ``CommonMark Test (407)`` () =
  runTest
    "*foo****\n"
    "<p><em>foo</em>***</p>\n"

[<Test>]
let ``CommonMark Test (408)`` () =
  runTest
    "foo ___\n"
    "<p>foo ___</p>\n"

[<Test>]
let ``CommonMark Test (409)`` () =
  runTest
    "foo _\\__\n"
    "<p>foo <em>_</em></p>\n"

[<Test>]
let ``CommonMark Test (410)`` () =
  runTest
    "foo _*_\n"
    "<p>foo <em>*</em></p>\n"

[<Test>]
let ``CommonMark Test (411)`` () =
  runTest
    "foo _____\n"
    "<p>foo _____</p>\n"

[<Test>]
let ``CommonMark Test (412)`` () =
  runTest
    "foo __\\___\n"
    "<p>foo <strong>_</strong></p>\n"

[<Test>]
let ``CommonMark Test (413)`` () =
  runTest
    "foo __*__\n"
    "<p>foo <strong>*</strong></p>\n"

[<Test>]
let ``CommonMark Test (414)`` () =
  runTest
    "__foo_\n"
    "<p>_<em>foo</em></p>\n"

[<Test>]
let ``CommonMark Test (415)`` () =
  runTest
    "_foo__\n"
    "<p><em>foo</em>_</p>\n"

[<Test>]
let ``CommonMark Test (416)`` () =
  runTest
    "___foo__\n"
    "<p>_<strong>foo</strong></p>\n"

[<Test>]
let ``CommonMark Test (417)`` () =
  runTest
    "____foo_\n"
    "<p>___<em>foo</em></p>\n"

[<Test>]
let ``CommonMark Test (418)`` () =
  runTest
    "__foo___\n"
    "<p><strong>foo</strong>_</p>\n"

[<Test>]
let ``CommonMark Test (419)`` () =
  runTest
    "_foo____\n"
    "<p><em>foo</em>___</p>\n"

[<Test>]
let ``CommonMark Test (420)`` () =
  runTest
    "**foo**\n"
    "<p><strong>foo</strong></p>\n"

[<Test>]
let ``CommonMark Test (421)`` () =
  runTest
    "*_foo_*\n"
    "<p><em><em>foo</em></em></p>\n"

[<Test>]
let ``CommonMark Test (422)`` () =
  runTest
    "__foo__\n"
    "<p><strong>foo</strong></p>\n"

[<Test>]
let ``CommonMark Test (423)`` () =
  runTest
    "_*foo*_\n"
    "<p><em><em>foo</em></em></p>\n"

[<Test>]
let ``CommonMark Test (424)`` () =
  runTest
    "****foo****\n"
    "<p><strong><strong>foo</strong></strong></p>\n"

[<Test>]
let ``CommonMark Test (425)`` () =
  runTest
    "____foo____\n"
    "<p><strong><strong>foo</strong></strong></p>\n"

[<Test>]
let ``CommonMark Test (426)`` () =
  runTest
    "******foo******\n"
    "<p><strong><strong><strong>foo</strong></strong></strong></p>\n"

[<Test>]
let ``CommonMark Test (427)`` () =
  runTest
    "***foo***\n"
    "<p><strong><em>foo</em></strong></p>\n"

[<Test>]
let ``CommonMark Test (428)`` () =
  runTest
    "_____foo_____\n"
    "<p><strong><strong><em>foo</em></strong></strong></p>\n"

[<Test>]
let ``CommonMark Test (429)`` () =
  runTest
    "*foo _bar* baz_\n"
    "<p><em>foo _bar</em> baz_</p>\n"

[<Test>]
let ``CommonMark Test (430)`` () =
  runTest
    "**foo*bar**\n"
    "<p><em><em>foo</em>bar</em>*</p>\n"

[<Test>]
let ``CommonMark Test (431)`` () =
  runTest
    "*foo __bar *baz bim__ bam*\n"
    "<p><em>foo <strong>bar *baz bim</strong> bam</em></p>\n"

[<Test>]
let ``CommonMark Test (432)`` () =
  runTest
    "**foo **bar baz**\n"
    "<p>**foo <strong>bar baz</strong></p>\n"

[<Test>]
let ``CommonMark Test (433)`` () =
  runTest
    "*foo *bar baz*\n"
    "<p>*foo <em>bar baz</em></p>\n"

[<Test>]
let ``CommonMark Test (434)`` () =
  runTest
    "*[bar*](/url)\n"
    "<p>*<a href=\"/url\">bar*</a></p>\n"

[<Test>]
let ``CommonMark Test (435)`` () =
  runTest
    "_foo [bar_](/url)\n"
    "<p>_foo <a href=\"/url\">bar_</a></p>\n"

[<Test>]
let ``CommonMark Test (436)`` () =
  runTest
    "*&lt;img src=&quot;foo&quot; title=&quot;*&quot;/&gt;\n"
    "<p>*<img src=\"foo\" title=\"*\"/></p>\n"

[<Test>]
let ``CommonMark Test (437)`` () =
  runTest
    "**&lt;a href=&quot;**&quot;&gt;\n"
    "<p>**<a href=\"**\"></p>\n"

[<Test>]
let ``CommonMark Test (438)`` () =
  runTest
    "__&lt;a href=&quot;__&quot;&gt;\n"
    "<p>__<a href=\"__\"></p>\n"

[<Test>]
let ``CommonMark Test (439)`` () =
  runTest
    "*a `*`*\n"
    "<p><em>a <code>*</code></em></p>\n"

[<Test>]
let ``CommonMark Test (440)`` () =
  runTest
    "_a `_`_\n"
    "<p><em>a <code>_</code></em></p>\n"

[<Test>]
let ``CommonMark Test (441)`` () =
  runTest
    "**a&lt;http://foo.bar/?q=**&gt;\n"
    "<p>**a<a href=\"http://foo.bar/?q=**\">http://foo.bar/?q=**</a></p>\n"

[<Test>]
let ``CommonMark Test (442)`` () =
  runTest
    "__a&lt;http://foo.bar/?q=__&gt;\n"
    "<p>__a<a href=\"http://foo.bar/?q=__\">http://foo.bar/?q=__</a></p>\n"

[<Test>]
let ``CommonMark Test (443)`` () =
  runTest
    "[link](/uri &quot;title&quot;)\n"
    "<p><a href=\"/uri\" title=\"title\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (444)`` () =
  runTest
    "[link](/uri)\n"
    "<p><a href=\"/uri\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (445)`` () =
  runTest
    "[link]()\n"
    "<p><a href=\"\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (446)`` () =
  runTest
    "[link](&lt;&gt;)\n"
    "<p><a href=\"\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (447)`` () =
  runTest
    "[link](/my uri)\n"
    "<p>[link](/my uri)</p>\n"

[<Test>]
let ``CommonMark Test (448)`` () =
  runTest
    "[link](&lt;/my uri&gt;)\n"
    "<p><a href=\"/my%20uri\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (449)`` () =
  runTest
    "[link](foo\nbar)\n"
    "<p>[link](foo\nbar)</p>\n"

[<Test>]
let ``CommonMark Test (450)`` () =
  runTest
    "[link](&lt;foo\nbar&gt;)\n"
    "<p>[link](<foo\nbar>)</p>\n"

[<Test>]
let ``CommonMark Test (451)`` () =
  runTest
    "[link]((foo)and(bar))\n"
    "<p><a href=\"(foo)and(bar)\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (452)`` () =
  runTest
    "[link](foo(and(bar)))\n"
    "<p>[link](foo(and(bar)))</p>\n"

[<Test>]
let ``CommonMark Test (453)`` () =
  runTest
    "[link](foo(and\\(bar\\)))\n"
    "<p><a href=\"foo(and(bar))\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (454)`` () =
  runTest
    "[link](&lt;foo(and(bar))&gt;)\n"
    "<p><a href=\"foo(and(bar))\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (455)`` () =
  runTest
    "[link](foo\\)\\:)\n"
    "<p><a href=\"foo):\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (456)`` () =
  runTest
    "[link](#fragment)\n\n[link](http://example.com#fragment)\n\n[link](http://example.com?foo=bar&amp;baz#fragment)\n"
    "<p><a href=\"#fragment\">link</a></p>\n<p><a href=\"http://example.com#fragment\">link</a></p>\n<p><a href=\"http://example.com?foo=bar&amp;baz#fragment\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (457)`` () =
  runTest
    "[link](foo\\bar)\n"
    "<p><a href=\"foo%5Cbar\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (458)`` () =
  runTest
    "[link](foo%20b&amp;auml;)\n"
    "<p><a href=\"foo%20b%C3%A4\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (459)`` () =
  runTest
    "[link](&quot;title&quot;)\n"
    "<p><a href=\"%22title%22\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (460)`` () =
  runTest
    "[link](/url &quot;title&quot;)\n[link](/url 'title')\n[link](/url (title))\n"
    "<p><a href=\"/url\" title=\"title\">link</a>\n<a href=\"/url\" title=\"title\">link</a>\n<a href=\"/url\" title=\"title\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (461)`` () =
  runTest
    "[link](/url &quot;title \\&quot;&amp;quot;&quot;)\n"
    "<p><a href=\"/url\" title=\"title &quot;&quot;\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (462)`` () =
  runTest
    "[link](/url &quot;title &quot;and&quot; title&quot;)\n"
    "<p>[link](/url &quot;title &quot;and&quot; title&quot;)</p>\n"

[<Test>]
let ``CommonMark Test (463)`` () =
  runTest
    "[link](/url 'title &quot;and&quot; title')\n"
    "<p><a href=\"/url\" title=\"title &quot;and&quot; title\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (464)`` () =
  runTest
    "[link](   /uri\n  &quot;title&quot;  )\n"
    "<p><a href=\"/uri\" title=\"title\">link</a></p>\n"

[<Test>]
let ``CommonMark Test (465)`` () =
  runTest
    "[link] (/uri)\n"
    "<p>[link] (/uri)</p>\n"

[<Test>]
let ``CommonMark Test (466)`` () =
  runTest
    "[link [foo [bar]]](/uri)\n"
    "<p><a href=\"/uri\">link [foo [bar]]</a></p>\n"

[<Test>]
let ``CommonMark Test (467)`` () =
  runTest
    "[link] bar](/uri)\n"
    "<p>[link] bar](/uri)</p>\n"

[<Test>]
let ``CommonMark Test (468)`` () =
  runTest
    "[link [bar](/uri)\n"
    "<p>[link <a href=\"/uri\">bar</a></p>\n"

[<Test>]
let ``CommonMark Test (469)`` () =
  runTest
    "[link \\[bar](/uri)\n"
    "<p><a href=\"/uri\">link [bar</a></p>\n"

[<Test>]
let ``CommonMark Test (470)`` () =
  runTest
    "[link *foo **bar** `#`*](/uri)\n"
    "<p><a href=\"/uri\">link <em>foo <strong>bar</strong> <code>#</code></em></a></p>\n"

[<Test>]
let ``CommonMark Test (471)`` () =
  runTest
    "[![moon](moon.jpg)](/uri)\n"
    "<p><a href=\"/uri\"><img src=\"moon.jpg\" alt=\"moon\" /></a></p>\n"

[<Test>]
let ``CommonMark Test (472)`` () =
  runTest
    "[foo [bar](/uri)](/uri)\n"
    "<p>[foo <a href=\"/uri\">bar</a>](/uri)</p>\n"

[<Test>]
let ``CommonMark Test (473)`` () =
  runTest
    "[foo *[bar [baz](/uri)](/uri)*](/uri)\n"
    "<p>[foo <em>[bar <a href=\"/uri\">baz</a>](/uri)</em>](/uri)</p>\n"

[<Test>]
let ``CommonMark Test (474)`` () =
  runTest
    "![[[foo](uri1)](uri2)](uri3)\n"
    "<p><img src=\"uri3\" alt=\"[foo](uri2)\" /></p>\n"

[<Test>]
let ``CommonMark Test (475)`` () =
  runTest
    "*[foo*](/uri)\n"
    "<p>*<a href=\"/uri\">foo*</a></p>\n"

[<Test>]
let ``CommonMark Test (476)`` () =
  runTest
    "[foo *bar](baz*)\n"
    "<p><a href=\"baz*\">foo *bar</a></p>\n"

[<Test>]
let ``CommonMark Test (477)`` () =
  runTest
    "*foo [bar* baz]\n"
    "<p><em>foo [bar</em> baz]</p>\n"

[<Test>]
let ``CommonMark Test (478)`` () =
  runTest
    "[foo &lt;bar attr=&quot;](baz)&quot;&gt;\n"
    "<p>[foo <bar attr=\"](baz)\"></p>\n"

[<Test>]
let ``CommonMark Test (479)`` () =
  runTest
    "[foo`](/uri)`\n"
    "<p>[foo<code>](/uri)</code></p>\n"

[<Test>]
let ``CommonMark Test (480)`` () =
  runTest
    "[foo&lt;http://example.com/?search=](uri)&gt;\n"
    "<p>[foo<a href=\"http://example.com/?search=%5D(uri)\">http://example.com/?search=](uri)</a></p>\n"

[<Test>]
let ``CommonMark Test (481)`` () =
  runTest
    "[foo][bar]\n\n[bar]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (482)`` () =
  runTest
    "[link [foo [bar]]][ref]\n\n[ref]: /uri\n"
    "<p><a href=\"/uri\">link [foo [bar]]</a></p>\n"

[<Test>]
let ``CommonMark Test (483)`` () =
  runTest
    "[link \\[bar][ref]\n\n[ref]: /uri\n"
    "<p><a href=\"/uri\">link [bar</a></p>\n"

[<Test>]
let ``CommonMark Test (484)`` () =
  runTest
    "[link *foo **bar** `#`*][ref]\n\n[ref]: /uri\n"
    "<p><a href=\"/uri\">link <em>foo <strong>bar</strong> <code>#</code></em></a></p>\n"

[<Test>]
let ``CommonMark Test (485)`` () =
  runTest
    "[![moon](moon.jpg)][ref]\n\n[ref]: /uri\n"
    "<p><a href=\"/uri\"><img src=\"moon.jpg\" alt=\"moon\" /></a></p>\n"

[<Test>]
let ``CommonMark Test (486)`` () =
  runTest
    "[foo [bar](/uri)][ref]\n\n[ref]: /uri\n"
    "<p>[foo <a href=\"/uri\">bar</a>]<a href=\"/uri\">ref</a></p>\n"

[<Test>]
let ``CommonMark Test (487)`` () =
  runTest
    "[foo *bar [baz][ref]*][ref]\n\n[ref]: /uri\n"
    "<p>[foo <em>bar <a href=\"/uri\">baz</a></em>]<a href=\"/uri\">ref</a></p>\n"

[<Test>]
let ``CommonMark Test (488)`` () =
  runTest
    "*[foo*][ref]\n\n[ref]: /uri\n"
    "<p>*<a href=\"/uri\">foo*</a></p>\n"

[<Test>]
let ``CommonMark Test (489)`` () =
  runTest
    "[foo *bar][ref]\n\n[ref]: /uri\n"
    "<p><a href=\"/uri\">foo *bar</a></p>\n"

[<Test>]
let ``CommonMark Test (490)`` () =
  runTest
    "[foo &lt;bar attr=&quot;][ref]&quot;&gt;\n\n[ref]: /uri\n"
    "<p>[foo <bar attr=\"][ref]\"></p>\n"

[<Test>]
let ``CommonMark Test (491)`` () =
  runTest
    "[foo`][ref]`\n\n[ref]: /uri\n"
    "<p>[foo<code>][ref]</code></p>\n"

[<Test>]
let ``CommonMark Test (492)`` () =
  runTest
    "[foo&lt;http://example.com/?search=][ref]&gt;\n\n[ref]: /uri\n"
    "<p>[foo<a href=\"http://example.com/?search=%5D%5Bref%5D\">http://example.com/?search=][ref]</a></p>\n"

[<Test>]
let ``CommonMark Test (493)`` () =
  runTest
    "[foo][BaR]\n\n[bar]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (494)`` () =
  runTest
    "[Толпой][Толпой] is a Russian word.\n\n[ТОЛПОЙ]: /url\n"
    "<p><a href=\"/url\">Толпой</a> is a Russian word.</p>\n"

[<Test>]
let ``CommonMark Test (495)`` () =
  runTest
    "[Foo\n  bar]: /url\n\n[Baz][Foo bar]\n"
    "<p><a href=\"/url\">Baz</a></p>\n"

[<Test>]
let ``CommonMark Test (496)`` () =
  runTest
    "[foo] [bar]\n\n[bar]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (497)`` () =
  runTest
    "[foo]\n[bar]\n\n[bar]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (498)`` () =
  runTest
    "[foo]: /url1\n\n[foo]: /url2\n\n[bar][foo]\n"
    "<p><a href=\"/url1\">bar</a></p>\n"

[<Test>]
let ``CommonMark Test (499)`` () =
  runTest
    "[bar][foo\\!]\n\n[foo!]: /url\n"
    "<p>[bar][foo!]</p>\n"

[<Test>]
let ``CommonMark Test (500)`` () =
  runTest
    "[foo][ref[]\n\n[ref[]: /uri\n"
    "<p>[foo][ref[]</p>\n<p>[ref[]: /uri</p>\n"

[<Test>]
let ``CommonMark Test (501)`` () =
  runTest
    "[foo][ref[bar]]\n\n[ref[bar]]: /uri\n"
    "<p>[foo][ref[bar]]</p>\n<p>[ref[bar]]: /uri</p>\n"

[<Test>]
let ``CommonMark Test (502)`` () =
  runTest
    "[[[foo]]]\n\n[[[foo]]]: /url\n"
    "<p>[[[foo]]]</p>\n<p>[[[foo]]]: /url</p>\n"

[<Test>]
let ``CommonMark Test (503)`` () =
  runTest
    "[foo][ref\\[]\n\n[ref\\[]: /uri\n"
    "<p><a href=\"/uri\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (504)`` () =
  runTest
    "[]\n\n[]: /uri\n"
    "<p>[]</p>\n<p>[]: /uri</p>\n"

[<Test>]
let ``CommonMark Test (505)`` () =
  runTest
    "[\n ]\n\n[\n ]: /uri\n"
    "<p>[\n]</p>\n<p>[\n]: /uri</p>\n"

[<Test>]
let ``CommonMark Test (506)`` () =
  runTest
    "[foo][]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (507)`` () =
  runTest
    "[*foo* bar][]\n\n[*foo* bar]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\"><em>foo</em> bar</a></p>\n"

[<Test>]
let ``CommonMark Test (508)`` () =
  runTest
    "[Foo][]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">Foo</a></p>\n"

[<Test>]
let ``CommonMark Test (509)`` () =
  runTest
    "[foo] \n[]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (510)`` () =
  runTest
    "[foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (511)`` () =
  runTest
    "[*foo* bar]\n\n[*foo* bar]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\"><em>foo</em> bar</a></p>\n"

[<Test>]
let ``CommonMark Test (512)`` () =
  runTest
    "[[*foo* bar]]\n\n[*foo* bar]: /url &quot;title&quot;\n"
    "<p>[<a href=\"/url\" title=\"title\"><em>foo</em> bar</a>]</p>\n"

[<Test>]
let ``CommonMark Test (513)`` () =
  runTest
    "[[bar [foo]\n\n[foo]: /url\n"
    "<p>[[bar <a href=\"/url\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (514)`` () =
  runTest
    "[Foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><a href=\"/url\" title=\"title\">Foo</a></p>\n"

[<Test>]
let ``CommonMark Test (515)`` () =
  runTest
    "[foo] bar\n\n[foo]: /url\n"
    "<p><a href=\"/url\">foo</a> bar</p>\n"

[<Test>]
let ``CommonMark Test (516)`` () =
  runTest
    "\\[foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p>[foo]</p>\n"

[<Test>]
let ``CommonMark Test (517)`` () =
  runTest
    "[foo*]: /url\n\n*[foo*]\n"
    "<p>*<a href=\"/url\">foo*</a></p>\n"

[<Test>]
let ``CommonMark Test (518)`` () =
  runTest
    "[foo][bar]\n\n[foo]: /url1\n[bar]: /url2\n"
    "<p><a href=\"/url2\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (519)`` () =
  runTest
    "[foo][bar][baz]\n\n[baz]: /url\n"
    "<p>[foo]<a href=\"/url\">bar</a></p>\n"

[<Test>]
let ``CommonMark Test (520)`` () =
  runTest
    "[foo][bar][baz]\n\n[baz]: /url1\n[bar]: /url2\n"
    "<p><a href=\"/url2\">foo</a><a href=\"/url1\">baz</a></p>\n"

[<Test>]
let ``CommonMark Test (521)`` () =
  runTest
    "[foo][bar][baz]\n\n[baz]: /url1\n[foo]: /url2\n"
    "<p>[foo]<a href=\"/url1\">bar</a></p>\n"

[<Test>]
let ``CommonMark Test (522)`` () =
  runTest
    "![foo](/url &quot;title&quot;)\n"
    "<p><img src=\"/url\" alt=\"foo\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (523)`` () =
  runTest
    "![foo *bar*]\n\n[foo *bar*]: train.jpg &quot;train &amp; tracks&quot;\n"
    "<p><img src=\"train.jpg\" alt=\"foo bar\" title=\"train &amp; tracks\" /></p>\n"

[<Test>]
let ``CommonMark Test (524)`` () =
  runTest
    "![foo ![bar](/url)](/url2)\n"
    "<p><img src=\"/url2\" alt=\"foo bar\" /></p>\n"

[<Test>]
let ``CommonMark Test (525)`` () =
  runTest
    "![foo [bar](/url)](/url2)\n"
    "<p><img src=\"/url2\" alt=\"foo bar\" /></p>\n"

[<Test>]
let ``CommonMark Test (526)`` () =
  runTest
    "![foo *bar*][]\n\n[foo *bar*]: train.jpg &quot;train &amp; tracks&quot;\n"
    "<p><img src=\"train.jpg\" alt=\"foo bar\" title=\"train &amp; tracks\" /></p>\n"

[<Test>]
let ``CommonMark Test (527)`` () =
  runTest
    "![foo *bar*][foobar]\n\n[FOOBAR]: train.jpg &quot;train &amp; tracks&quot;\n"
    "<p><img src=\"train.jpg\" alt=\"foo bar\" title=\"train &amp; tracks\" /></p>\n"

[<Test>]
let ``CommonMark Test (528)`` () =
  runTest
    "![foo](train.jpg)\n"
    "<p><img src=\"train.jpg\" alt=\"foo\" /></p>\n"

[<Test>]
let ``CommonMark Test (529)`` () =
  runTest
    "My ![foo bar](/path/to/train.jpg  &quot;title&quot;   )\n"
    "<p>My <img src=\"/path/to/train.jpg\" alt=\"foo bar\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (530)`` () =
  runTest
    "![foo](&lt;url&gt;)\n"
    "<p><img src=\"url\" alt=\"foo\" /></p>\n"

[<Test>]
let ``CommonMark Test (531)`` () =
  runTest
    "![](/url)\n"
    "<p><img src=\"/url\" alt=\"\" /></p>\n"

[<Test>]
let ``CommonMark Test (532)`` () =
  runTest
    "![foo] [bar]\n\n[bar]: /url\n"
    "<p><img src=\"/url\" alt=\"foo\" /></p>\n"

[<Test>]
let ``CommonMark Test (533)`` () =
  runTest
    "![foo] [bar]\n\n[BAR]: /url\n"
    "<p><img src=\"/url\" alt=\"foo\" /></p>\n"

[<Test>]
let ``CommonMark Test (534)`` () =
  runTest
    "![foo][]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"foo\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (535)`` () =
  runTest
    "![*foo* bar][]\n\n[*foo* bar]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"foo bar\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (536)`` () =
  runTest
    "![Foo][]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"Foo\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (537)`` () =
  runTest
    "![foo] \n[]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"foo\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (538)`` () =
  runTest
    "![foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"foo\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (539)`` () =
  runTest
    "![*foo* bar]\n\n[*foo* bar]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"foo bar\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (540)`` () =
  runTest
    "![[foo]]\n\n[[foo]]: /url &quot;title&quot;\n"
    "<p>![[foo]]</p>\n<p>[[foo]]: /url &quot;title&quot;</p>\n"

[<Test>]
let ``CommonMark Test (541)`` () =
  runTest
    "![Foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p><img src=\"/url\" alt=\"Foo\" title=\"title\" /></p>\n"

[<Test>]
let ``CommonMark Test (542)`` () =
  runTest
    "\\!\\[foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p>![foo]</p>\n"

[<Test>]
let ``CommonMark Test (543)`` () =
  runTest
    "\\![foo]\n\n[foo]: /url &quot;title&quot;\n"
    "<p>!<a href=\"/url\" title=\"title\">foo</a></p>\n"

[<Test>]
let ``CommonMark Test (544)`` () =
  runTest
    "&lt;http://foo.bar.baz&gt;\n"
    "<p><a href=\"http://foo.bar.baz\">http://foo.bar.baz</a></p>\n"

[<Test>]
let ``CommonMark Test (545)`` () =
  runTest
    "&lt;http://foo.bar.baz/test?q=hello&amp;id=22&amp;boolean&gt;\n"
    "<p><a href=\"http://foo.bar.baz/test?q=hello&amp;id=22&amp;boolean\">http://foo.bar.baz/test?q=hello&amp;id=22&amp;boolean</a></p>\n"

[<Test>]
let ``CommonMark Test (546)`` () =
  runTest
    "&lt;irc://foo.bar:2233/baz&gt;\n"
    "<p><a href=\"irc://foo.bar:2233/baz\">irc://foo.bar:2233/baz</a></p>\n"

[<Test>]
let ``CommonMark Test (547)`` () =
  runTest
    "&lt;MAILTO:FOO@BAR.BAZ&gt;\n"
    "<p><a href=\"MAILTO:FOO@BAR.BAZ\">MAILTO:FOO@BAR.BAZ</a></p>\n"

[<Test>]
let ``CommonMark Test (548)`` () =
  runTest
    "&lt;http://foo.bar/baz bim&gt;\n"
    "<p>&lt;http://foo.bar/baz bim&gt;</p>\n"

[<Test>]
let ``CommonMark Test (549)`` () =
  runTest
    "&lt;http://example.com/\\[\\&gt;\n"
    "<p><a href=\"http://example.com/%5C%5B%5C\">http://example.com/\\[\\</a></p>\n"

[<Test>]
let ``CommonMark Test (550)`` () =
  runTest
    "&lt;foo@bar.example.com&gt;\n"
    "<p><a href=\"mailto:foo@bar.example.com\">foo@bar.example.com</a></p>\n"

[<Test>]
let ``CommonMark Test (551)`` () =
  runTest
    "&lt;foo+special@Bar.baz-bar0.com&gt;\n"
    "<p><a href=\"mailto:foo+special@Bar.baz-bar0.com\">foo+special@Bar.baz-bar0.com</a></p>\n"

[<Test>]
let ``CommonMark Test (552)`` () =
  runTest
    "&lt;foo\\+@bar.example.com&gt;\n"
    "<p>&lt;foo+@bar.example.com&gt;</p>\n"

[<Test>]
let ``CommonMark Test (553)`` () =
  runTest
    "&lt;&gt;\n"
    "<p>&lt;&gt;</p>\n"

[<Test>]
let ``CommonMark Test (554)`` () =
  runTest
    "&lt;heck://bing.bong&gt;\n"
    "<p>&lt;heck://bing.bong&gt;</p>\n"

[<Test>]
let ``CommonMark Test (555)`` () =
  runTest
    "&lt; http://foo.bar &gt;\n"
    "<p>&lt; http://foo.bar &gt;</p>\n"

[<Test>]
let ``CommonMark Test (556)`` () =
  runTest
    "&lt;foo.bar.baz&gt;\n"
    "<p>&lt;foo.bar.baz&gt;</p>\n"

[<Test>]
let ``CommonMark Test (557)`` () =
  runTest
    "&lt;localhost:5001/foo&gt;\n"
    "<p>&lt;localhost:5001/foo&gt;</p>\n"

[<Test>]
let ``CommonMark Test (558)`` () =
  runTest
    "http://example.com\n"
    "<p>http://example.com</p>\n"

[<Test>]
let ``CommonMark Test (559)`` () =
  runTest
    "foo@bar.example.com\n"
    "<p>foo@bar.example.com</p>\n"

[<Test>]
let ``CommonMark Test (560)`` () =
  runTest
    "&lt;a&gt;&lt;bab&gt;&lt;c2c&gt;\n"
    "<p><a><bab><c2c></p>\n"

[<Test>]
let ``CommonMark Test (561)`` () =
  runTest
    "&lt;a/&gt;&lt;b2/&gt;\n"
    "<p><a/><b2/></p>\n"

[<Test>]
let ``CommonMark Test (562)`` () =
  runTest
    "&lt;a  /&gt;&lt;b2\ndata=&quot;foo&quot; &gt;\n"
    "<p><a  /><b2\ndata=\"foo\" ></p>\n"

[<Test>]
let ``CommonMark Test (563)`` () =
  runTest
    "&lt;a foo=&quot;bar&quot; bam = 'baz &lt;em&gt;&quot;&lt;/em&gt;'\n_boolean zoop:33=zoop:33 /&gt;\n"
    "<p><a foo=\"bar\" bam = 'baz <em>\"</em>'\n_boolean zoop:33=zoop:33 /></p>\n"

[<Test>]
let ``CommonMark Test (564)`` () =
  runTest
    "&lt;responsive-image src=&quot;foo.jpg&quot; /&gt;\n\n&lt;My-Tag&gt;\nfoo\n&lt;/My-Tag&gt;\n"
    "<responsive-image src=\"foo.jpg\" />\n<My-Tag>\nfoo\n</My-Tag>\n"

[<Test>]
let ``CommonMark Test (565)`` () =
  runTest
    "&lt;33&gt; &lt;__&gt;\n"
    "<p>&lt;33&gt; &lt;__&gt;</p>\n"

[<Test>]
let ``CommonMark Test (566)`` () =
  runTest
    "&lt;a h*#ref=&quot;hi&quot;&gt;\n"
    "<p>&lt;a h*#ref=&quot;hi&quot;&gt;</p>\n"

[<Test>]
let ``CommonMark Test (567)`` () =
  runTest
    "&lt;a href=&quot;hi'&gt; &lt;a href=hi'&gt;\n"
    "<p>&lt;a href=&quot;hi'&gt; &lt;a href=hi'&gt;</p>\n"

[<Test>]
let ``CommonMark Test (568)`` () =
  runTest
    "&lt; a&gt;&lt;\nfoo&gt;&lt;bar/ &gt;\n"
    "<p>&lt; a&gt;&lt;\nfoo&gt;&lt;bar/ &gt;</p>\n"

[<Test>]
let ``CommonMark Test (569)`` () =
  runTest
    "&lt;a href='bar'title=title&gt;\n"
    "<p>&lt;a href='bar'title=title&gt;</p>\n"

[<Test>]
let ``CommonMark Test (570)`` () =
  runTest
    "&lt;/a&gt;\n&lt;/foo &gt;\n"
    "</a>\n</foo >\n"

[<Test>]
let ``CommonMark Test (571)`` () =
  runTest
    "&lt;/a href=&quot;foo&quot;&gt;\n"
    "<p>&lt;/a href=&quot;foo&quot;&gt;</p>\n"

[<Test>]
let ``CommonMark Test (572)`` () =
  runTest
    "foo &lt;!-- this is a\ncomment - with hyphen --&gt;\n"
    "<p>foo <!-- this is a\ncomment - with hyphen --></p>\n"

[<Test>]
let ``CommonMark Test (573)`` () =
  runTest
    "foo &lt;!-- not a comment -- two hyphens --&gt;\n"
    "<p>foo &lt;!-- not a comment -- two hyphens --&gt;</p>\n"

[<Test>]
let ``CommonMark Test (574)`` () =
  runTest
    "foo &lt;!--&gt; foo --&gt;\n\nfoo &lt;!-- foo---&gt;\n"
    "<p>foo &lt;!--&gt; foo --&gt;</p>\n<p>foo &lt;!-- foo---&gt;</p>\n"

[<Test>]
let ``CommonMark Test (575)`` () =
  runTest
    "foo &lt;?php echo $a; ?&gt;\n"
    "<p>foo <?php echo $a; ?></p>\n"

[<Test>]
let ``CommonMark Test (576)`` () =
  runTest
    "foo &lt;!ELEMENT br EMPTY&gt;\n"
    "<p>foo <!ELEMENT br EMPTY></p>\n"

[<Test>]
let ``CommonMark Test (577)`` () =
  runTest
    "foo &lt;![CDATA[&gt;&amp;&lt;]]&gt;\n"
    "<p>foo <![CDATA[>&<]]></p>\n"

[<Test>]
let ``CommonMark Test (578)`` () =
  runTest
    "&lt;a href=&quot;&amp;ouml;&quot;&gt;\n"
    "<a href=\"&ouml;\">\n"

[<Test>]
let ``CommonMark Test (579)`` () =
  runTest
    "&lt;a href=&quot;\\*&quot;&gt;\n"
    "<a href=\"\\*\">\n"

[<Test>]
let ``CommonMark Test (580)`` () =
  runTest
    "&lt;a href=&quot;\\&quot;&quot;&gt;\n"
    "<p>&lt;a href=&quot;&quot;&quot;&gt;</p>\n"

[<Test>]
let ``CommonMark Test (581)`` () =
  runTest
    "foo  \nbaz\n"
    "<p>foo<br />\nbaz</p>\n"

[<Test>]
let ``CommonMark Test (582)`` () =
  runTest
    "foo\\\nbaz\n"
    "<p>foo<br />\nbaz</p>\n"

[<Test>]
let ``CommonMark Test (583)`` () =
  runTest
    "foo       \nbaz\n"
    "<p>foo<br />\nbaz</p>\n"

[<Test>]
let ``CommonMark Test (584)`` () =
  runTest
    "foo  \n     bar\n"
    "<p>foo<br />\nbar</p>\n"

[<Test>]
let ``CommonMark Test (585)`` () =
  runTest
    "foo\\\n     bar\n"
    "<p>foo<br />\nbar</p>\n"

[<Test>]
let ``CommonMark Test (586)`` () =
  runTest
    "*foo  \nbar*\n"
    "<p><em>foo<br />\nbar</em></p>\n"

[<Test>]
let ``CommonMark Test (587)`` () =
  runTest
    "*foo\\\nbar*\n"
    "<p><em>foo<br />\nbar</em></p>\n"

[<Test>]
let ``CommonMark Test (588)`` () =
  runTest
    "`code  \nspan`\n"
    "<p><code>code span</code></p>\n"

[<Test>]
let ``CommonMark Test (589)`` () =
  runTest
    "`code\\\nspan`\n"
    "<p><code>code\\ span</code></p>\n"

[<Test>]
let ``CommonMark Test (590)`` () =
  runTest
    "&lt;a href=&quot;foo  \nbar&quot;&gt;\n"
    "<p><a href=\"foo  \nbar\"></p>\n"

[<Test>]
let ``CommonMark Test (591)`` () =
  runTest
    "&lt;a href=&quot;foo\\\nbar&quot;&gt;\n"
    "<p><a href=\"foo\\\nbar\"></p>\n"

[<Test>]
let ``CommonMark Test (592)`` () =
  runTest
    "foo\\\n"
    "<p>foo\\</p>\n"

[<Test>]
let ``CommonMark Test (593)`` () =
  runTest
    "foo  \n"
    "<p>foo</p>\n"

[<Test>]
let ``CommonMark Test (594)`` () =
  runTest
    "### foo\\\n"
    "<h3>foo\\</h3>\n"

[<Test>]
let ``CommonMark Test (595)`` () =
  runTest
    "### foo  \n"
    "<h3>foo</h3>\n"

[<Test>]
let ``CommonMark Test (596)`` () =
  runTest
    "foo\nbaz\n"
    "<p>foo\nbaz</p>\n"

[<Test>]
let ``CommonMark Test (597)`` () =
  runTest
    "foo \n baz\n"
    "<p>foo\nbaz</p>\n"

[<Test>]
let ``CommonMark Test (598)`` () =
  runTest
    "hello $.;'there\n"
    "<p>hello $.;'there</p>\n"

[<Test>]
let ``CommonMark Test (599)`` () =
  runTest
    "Foo χρῆν\n"
    "<p>Foo χρῆν</p>\n"

[<Test>]
let ``CommonMark Test (600)`` () =
  runTest
    "Multiple     spaces\n"
    "<p>Multiple     spaces</p>\n"


