(*** condition: prepare ***)
#nowarn "211"
#I "../src/FSharp.Formatting/bin/Release/netstandard2.0"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Formatting.Markdown.dll"
#r "FSharp.Formatting.CodeFormat.dll"
#r "FSharp.Formatting.Literate.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Formatting,{{package-version}}"
#endif // FSX
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Formatting,{{package-version}}"
#endif // IPYNB

(**
F# Formatting: Output embedding
===============================

A nice feature of the literate programming package (`FSharp.Formatting.Literate.dll` in F# Formatting)
is that it lets you embed the result of running the script as part of the literate output.
This is a feature of the functions discussed in [literate programming](literate.html) and
it is implemented using the [F# Compiler service](http://fsharp.github.io/FSharp.Compiler.Service/).

Embedding literate script output
--------------------------------

The functionality is currently available (and tested) for F# script files (`*.fsx`) that
contain special comments to embed the Markdown text. To embed output of a script file, 
a couple of additional special comments are added.

The following snippet (F# Script file) demonstates the functionality:

    let test = 40 + 2

    printf "A result is: %d" test
    (*** include-output ***)

You can also use `(*** include-fsi-output ***)` to include the F# interactive output instead,
such as type signatures.

    let test = 40 + 3

    (*** include-fsi-output ***)

To include both console otuput and F# Interactive output use `(*** include-fsi-merged-output ***)`.

    let test = 40 + 4

    (*** include-fsi-merged-output ***)

The script defines a variable `test` and then prints it. The console output is included
in the output.

In addition to the commands demonstrated in the above sample, you can also use
the following variations to include the output and `it` values produced by a named snippet.

    (*** include-it: test ***)
    (*** include-fsi-output: test ***)
    (*** include-output: test ***)

Specifying the evaluator and formatting 
---------------------------------------
*)

(**
The embedding of F# output requires specifying an additional parameter to the 
parsing functions discussed in [literate programming documentation](literate.html).
Assuming you have all the references in place, you can now create an instance of
`FsiEvaluator` that represents a wrapper for F# interactive and pass it to all the
functions that parse script files or process script files:

*)
open FSharp.Formatting.Literate
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.Markdown

// Sample literate content
let content = """
let a = 10
(*** include-value:a ***)"""

// Create evaluator and parse script
let fsi = FsiEvaluator()
let doc = Literate.ParseScriptString(content, fsiEvaluator = fsi)
Literate.ToHtml(doc)
(**
When the `fsiEvaluator` parameter is specified, the script is evaluated and so you
can use additional commands such as `include-value`. When the evaluator is *not* specified,
it is not created automatically and so the functionality is not available (this way,
you won't accidentally run unexpected code!)

If you specify the `fsiEvaluator` parameter, but don't want a specific snippet to be evaluated
(because it might throw an exception, for example), you can use the `(*** do-not-eval ***)` 
command.

The constructor of `FsiEvaluator` takes command line parameters for `fsi.exe` that can
be used to specify, for example, defined symbols and other attributes for F# Interactive.

You can also subscribe to the `EvaluationFailed` event which is fired whenever the evaluation
of an expression fails. You can use that to do tests that verify that all off the code in your
documentation executes without errors.

Emitting Raw Text
-----------------

When writing documents, it is sometimes required to emit completely unaltered text. Up to this point all
of the `commands` have decorated the code or text with some formatting, for example a `pre` element. When working 
with layout or content generation engines such as Jeykll, we sometimes need to emit plain text as declarations to
said engines. This is where the `raw` command is useful.

	(**
		(*** raw ***)
		Some raw text.
	*)

which would emit

<pre>
Some raw text.
</pre>

directly into the document.

Custom formatting functions
---------------------------

As mentioned earlier, values are formatted using a simple `"%A"` formatter by default.
However, you can specify a formatting function that provides a nicer formatting for values
of certain types. For example, let's say that we would want to format F# lists such as
`[1; 2; 3]` as HTML ordered lists `<ol>`. 

This can be done by calling `RegisterTransformation` on the `FsiEvaluator` instance:

*)
// Create evaluator & register simple formatter for lists
let fsiOl = FsiEvaluator()
fsiOl.RegisterTransformation(fun (o, ty, _executionCount) ->
  // If the type of value is an F# list, format it nicely
  if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
    let items = 
      // Get items as objects and create paragraph for each item
      [ for it in Seq.cast<obj> (unbox o) -> 
          [ Paragraph([Literal(it.ToString(), None)], None) ] ]
    // Return option value (success) with ordered list
    Some [ ListBlock(MarkdownListKind.Ordered, items, None) ]
  else None)
(**

The function is called with two arguments - `o` is the value to be formatted and `ty`
is the static type of the value (as inferred by the F# compiler). The sample checks
that the type of the value is a list (containing values of any type) and then it 
casts all values in the list to `obj` (for simplicity). Then we generate Markdown
blocks representing an ordered list. This means that the code will work for both
LaTeX and HTML formatting - but if you only need one, you can simply produce HTML and
embed it in `InlineBlock`.

To use the new `FsiEvaluator`, we can use the same style as earlier. This time, we format
a simple list containing strings:
*)
let listy = """
### Formatting demo
let test = ["one";"two";"three"]
(*** include-value:test ***)"""

let docOl = Literate.ParseScriptString(listy, fsiEvaluator = fsiOl)
Literate.ToHtml(docOl)
(**
The resulting HTML formatting of the document contains the snippet that defines `test`,
followed by a nicely formatted ordered list:

<blockquote>
<h3>Formatting demo</h3>
<table class="pre"><tr><td class="lines"><pre class="fssnip">
<span class="l">1: </span>
</pre>
</td>
<td class="snippet"><pre class="fssnip">
<span class="k">let</span> <spanclass="i">test</span> <span class="o">=</span> [<span class="s">&quot;</span><span class="s">one</span><span class="s">&quot;</span>;<span class="s">&quot;</span><span class="s">two</span><span class="s">&quot;</span>;<span class="s">&quot;</span><span class="s">three</span><span class="s">&quot;</span>]</pre>
</td>
</tr>
</table>
<ol>
<li><p>one</p></li>
<li><p>two</p></li>
<li><p>three</p></li>
</ol>
</blockquote>

*)
