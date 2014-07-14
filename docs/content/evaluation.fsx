(**
F# Formatting: Output embedding
===============================

A nice feature of the literate programming package (`FSharp.Literate.dll` in F# Formatting)
is that it lets you embed the result of running the script as part of the literate output.
This is a feature of the functions discussed in [literate programming](literate.html) and
it is implemented using the [F# Compiler service](http://fsharp.github.io/FSharp.Compiler.Service/).

Embedding literate script output
--------------------------------

The functionality is currently available (and tested) for F# script files (`*.fsx`) that
contain special comments to embed the Markdown text. To embed output of a script file, 
a couple of additional special comments is added.

The following snippet (F# Script file) demonstates the functionality:

    (** 
    ### Evaluation demo 
    The following is a simple calculation: *)
    let test = 40 + 2

    (** We can print it as follows: *)
    (*** define-output:test ***)
    printf "Result is: %d" test

    (** The result of the previous snippet is: *)
    (*** include-output:test ***)

    (** And the variable `test` has the following value: *)
    (*** include-value: test ***)

The script defines a variable `test` and then prints it. The comment `(*** define-output:test ***)` 
is used to capture the result of the printing (using the key `test`) so that it can be embedded
later in the script. We refer to it using another special comment written as `(*** include-output:test ***)`.
In addition, it is also possible to include the value of any variable using the comment
`(*** include-value: test ***)`. By default, this is formatted using the `"%A"` formatter in F# (but
the next section shows how to override this behavior). The formatted result of the above snippet looks
as follows:

<blockquote>
<h3>Evaluation demo</h3>
<p>The following is a simple calculation:</p>
<table class="pre"><tr><td class="lines"><pre class="fssnip">
<span class="l">1: </span>
</pre>
</td>
<td class="snippet"><pre class="fssnip">
<span class="k">let</span> <span class="i">test</span> <span class="o">=</span> <span class="n">40</span> <span class="o">+</span> <span class="n">2</span></pre>
</td>
</tr>
</table>
<p>We can print it as follows:</p>
<table class="pre"><tr><td class="lines"><pre class="fssnip">
<span class="l">1: </span>
</pre>
</td>
<td class="snippet"><pre class="fssnip">
<span class="i">printf</span> <span class="s">&quot;</span><span class="s">Result</span><span class="s"> </span><span class="s">is</span><span class="s">:</span><span class="s"> </span><span class="s">%</span><span class="s">d</span><span class="s">&quot;</span> <span class="i">test</span></pre>
</td>
</tr>
</table>
<p>The result of the previous snippet is:</p>
<table class="pre"><tr><td><pre><code>Result is: 42
</code></pre></td></tr></table>
<p>And the variable <code>test</code> has the following value:</p>
<table class="pre"><tr><td><pre><code>42
</code></pre></td></tr></table></blockquote>

In addition to the commands demonstrated in the above sample, you can also use `(*** include-it: test ***)` 
to include the `it` value that was produced by a snippet named `test` using the `(*** define-output: test ***)` 
command.

Specifying the evaluator and formatting 
---------------------------------------
*)

(*** hide ***)
#nowarn "211"
#I "../../bin"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Literate.dll"
#r "FSharp.Markdown.dll"
(**
The embedding of F# output requires specifying an additional parameter to the 
parsing functions discussed in [literate programming documentation](literate.html).
Assuming you have all the references in place, you can now create an instance of
`FsiEvaluator` that represents a wrapper for F# interactive and pass it to all the
functions that parse script files or process script files:

*)
open FSharp.Literate
open FSharp.Markdown

// Sample literate content
let content = """
let a = 10
(*** include-value:a ***)"""

// Create evaluator and parse script
let fsi = FsiEvaluator()
let doc = Literate.ParseScriptString(content, fsiEvaluator = fsi)
Literate.WriteHtml(doc)
(**
When the `fsiEvaluator` parameter is specified, the script is evaluated and so you
can use additional commands such as `include-value`. When the evaluator is *not* specified,
it is not created automatically and so the functionality is not available (this way,
you won't accidentally run unexpected code!)

If you specify the `fsiEvaluator` parameter, but don't want a specific snippet to be evaluated
(because it might throw an exception, for example), you can use the `(*** do-not-eval ***)` 
command.

The constructor of `FsiEvaluator` takes command line parameters for `fsi.exe` that can
be used to specify e.g. defined symbols and other attributes for F# Interactive.

You can also subscribe to the `EvaluationFailed` event which is fired whenever the evaluation
of an expression fails. You can use that to do tests that verify that all the code on your
documentation executes without errors.

Custom formatting functions
---------------------------

As mentioned earlier, values are formatted using a simple `"%A"` formatter by default.
However, you can specify a formatting function that provides a nicer formatting for values
of certain types. For example, let's say that we would want to format F# lists such as
`[1; 2; 3]` as HTML ordered lists `<ol>`. 

This can be done by calling `RegisterTransformation` on the `FsiEvaluator` instance:

*)
// Create evaluator & register simple formatter for lists
let fsiOl = FSharp.Literate.FsiEvaluator()
fsiOl.RegisterTransformation(fun (o, ty) ->
  // If the type of value is an F# list, format it nicely
  if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
    let items = 
      // Get items as objects and create paragraph for each item
      [ for it in Seq.cast<obj> (unbox o) -> 
          [ Paragraph[Literal (it.ToString())] ] ]
    // Return option value (success) with ordered list
    Some [ ListBlock(MarkdownListKind.Ordered, items) ]
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
Literate.WriteHtml(docOl)
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