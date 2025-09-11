[![Binder](img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/fsharp.formatting/gh-pages?filepath=evaluation.ipynb)&emsp;
[![Script](img/badge-script.svg)](https://fsprojects.github.io/FSharp.Formatting//evaluation.fsx)&emsp;
[![Notebook](img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Formatting//evaluation.ipynb)

# Embedding script output

For literate F# scripts, you may embed the result of running the script as part of the literate output.
This is a feature of the functions discussed in [literate programming](literate.html) and
it is implemented using the [F# Compiler service](http://fsharp.github.io/FSharp.Compiler.Service/).

## Including Console Output

To include the Console output use `include-output`:

```fsharp
let test = 40 + 2

printf "A result is: %d" test
(*** include-output ***)

```

The script defines a variable `test` and then prints it. The console output is included
in the output.

To include the a formatted value use `include-it`:

```fsharp
[ 0 .. 99 ]

(*** include-it ***)

```

To include the meta output of F# Interactive processing such as type signatures use `(*** include-fsi-output ***)`:

```fsharp
let test = 40 + 3

(*** include-fsi-output ***)

```

To include both console output and F# Interactive output blended use `(*** include-fsi-merged-output ***)`.

```fsharp
let test = 40 + 4
(*** include-fsi-merged-output ***)

```

You can use the same commands with a named snippet:

```fsharp
(*** include-it: test ***)
(*** include-fsi-output: test ***)
(*** include-output: test ***)

```

You can use the `include-value` command to format a specific value:

```fsharp
let value1 = [ 0 .. 50 ]
let value2 = [ 51 .. 100 ]
(*** include-value: value1 ***)

```

## Using AddPrinter and AddHtmlPrinter

You can use `fsi.AddPrinter`, `fsi.AddPrintTransformer` and `fsi.AddHtmlPrinter` to extend the formatting of objects.

## Emitting Raw Text

To emit raw text in F# literate scripts use the following:

```fsharp
(**
	(*** raw ***)
	Some raw text.
*)

```

which would emit

<pre>
Some raw text.
</pre>
directly into the document.

## F# Formatting as a Library:  Specifying the Evaluator and Formatting

If using F# Formatting as a library the embedding of F# output requires specifying an additional parameter to the
parsing functions discussed in [literate programming documentation](literate.html).
Assuming you have all the references in place, you can now create an instance of
[FsiEvaluator](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-literate-evaluation-fsievaluator.html) that represents a wrapper for F# interactive and pass it to all the
functions that parse script files or process script files:

```fsharp
open FSharp.Formatting.Literate
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.Markdown

// Sample literate content
let content =
    """
let a = 10
(*** include-value:a ***)"""

// Create evaluator and parse script
let fsi = FsiEvaluator()

let doc = Literate.ParseScriptString(content, fsiEvaluator = fsi)

Literate.ToHtml(doc)
```

When the `fsiEvaluator` parameter is specified, the script is evaluated and so you
can use additional commands such as `include-value`. When the evaluator is **not** specified,
it is not created automatically, so the functionality is not available (this way,
you won't accidentally run unexpected code!)

If you specify the `fsiEvaluator` parameter, but don't want a specific snippet to be evaluated
(because it might throw an exception, for example), you can use the `(*** do-not-eval ***)`
command.

The constructor of [FsiEvaluator](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-literate-evaluation-fsievaluator.html) takes command line parameters for `fsi.exe` that can
be used to specify, for example, defined symbols and other attributes for F# Interactive.

You can also subscribe to the `EvaluationFailed` event which is fired whenever the evaluation
of an expression fails. You can use that to do tests that verify that all of the code in your
documentation executes without errors.

## F# Formatting as a Library: Custom formatting functions

As mentioned earlier, values are formatted using a simple `"%A"` formatter by default.
However, you can specify a formatting function that provides nicer formatting for values
of certain types. For example, let's say that we would want to format F# lists such as
`[1; 2; 3]` as HTML ordered lists `<ol>`.

This can be done by calling [FsiEvaluator.RegisterTransformation](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-literate-evaluation-fsievaluator.html#RegisterTransformation) on the `FsiEvaluator` instance:

```fsharp
// Create evaluator & register simple formatter for lists
let fsiEvaluator = FsiEvaluator()

fsiEvaluator.RegisterTransformation(fun (o, ty, _executionCount) ->
    // If the type of value is an F# list, format it nicely
    if ty.IsGenericType
       && ty.GetGenericTypeDefinition() = typedefof<list<_>> then
        let items =
            // Get items as objects and create a paragraph for each item
            [ for it in Seq.cast<obj> (unbox o) -> [ Paragraph([ Literal(it.ToString(), None) ], None) ] ]
        // Return option value (success) with ordered list
        Some [ ListBlock(MarkdownListKind.Ordered, items, None) ]
    else
        None)
```

The function is called with two arguments - `o` is the value to be formatted, and `ty`
is the static type of the value (as inferred by the F# compiler). The sample checks
that the type of the value is a list (containing values of any type), and then it
casts all values in the list to `obj` (for simplicity). Then, we generate Markdown
blocks representing an ordered list. This means that the code will work for both
LaTeX and HTML formatting - but if you only need one, you can simply produce HTML and
embed it in `InlineHtmlBlock`.

To use the new `FsiEvaluator`, we can use the same style as earlier. This time, we format
a simple list containing strings:

```fsharp
let listy =
    """
### Formatting demo
let test = ["one";"two";"three"]
(*** include-value:test ***)"""

let docOl = Literate.ParseScriptString(listy, fsiEvaluator = fsiEvaluator)

Literate.ToHtml(docOl)
```

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

