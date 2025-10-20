(**

*)
#r "nuget: FSharp.Formatting,1.0.0"
(**
[![Binder](img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/fsharp.formatting/gh-pages?filepath=codeformat.ipynb)&emsp;
[![Script](img/badge-script.svg)](https://fsprojects.github.io/FSharp.Formatting//codeformat.fsx)&emsp;
[![Notebook](img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Formatting//codeformat.ipynb)

# Code formatting

This page demonstrates how to use `FSharp.Formatting.CodeFormat` to tokenize
F# source code, obtain information about the source code (mainly tooltips
from the type-checker) and how to turn the code into a nicely formatted HTML.

First, we need to load the assembly and open the necessary namespaces:

*)
open FSharp.Formatting.CodeFormat
open System.Reflection
(**
If you want to process multiple snippets, it is a good idea to keep the
formatting agent around if possible. The agent needs to load the F# compiler
(which needs to load various files itself) and so this takes a long time.

## Processing F# source

The formatting agent provides a [CodeFormatAgent.ParseAndCheckSource](https://fsharp.github.io/fsharp-core-docs/reference/fsharp-formatting-codeformat-codeformatagent#ParseAndCheckSource) method (together with an asynchronous
version for use from F# and also a version that returns a .NET `Task` for C#).
To call the method, we define a simple F# code as a string:

*)
let source =
    """
    let hello () =
      printfn "Hello world"
  """

let snippets, diagnostics =
    CodeFormatter.ParseAndCheckSource("C:\\snippet.fsx", source, None, None, ignore)
(**
When calling the method, you need to specify a file name and the actual content
of the script file. The file does not have to physically exist. It is used by the
F# compiler to resolve relative references (e.g. `#r`) and to automatically name
the module including all code in the file.

You can also specify additional parameters, such as `*.dll` references, by passing
a third argument with compiler options (e.g. `"-r:Foo.dll -r:Bar.dll"`).

This operation might take some time, so it is a good idea to use an asynchronous
variant of the method. It returns two arrays - the first contains F# snippets
in the source code and the second contains any errors reported by the compiler.
A single source file can include multiple snippets using the same formatting tags
as those used on [fssnip.net](http://www.fssnip.net) as documented in the
[about page](http://www.fssnip.net/pages/About).

## Working with returned tokens

Each returned snippet is essentially just a collection of lines, and each line
consists of a sequence of tokens. The following snippet prints basic information
about the tokens of our sample snippet:

*)
// Get the first snippet and obtain list of lines
let (Snippet (title, lines)) = snippets |> Seq.head

// Iterate over all lines and all tokens on each line
for (Line (_, tokens)) in lines do
    for token in tokens do
        match token with
        | TokenSpan.Token (kind, code, tip) ->
            printf "%s" code

            tip
            |> Option.iter (fun spans -> printfn "%A" spans)
        | TokenSpan.Omitted _
        | TokenSpan.Output _
        | TokenSpan.Error _ -> ()

    printfn ""
(**
The `TokenSpan.Token` is the most important kind of token. It consists of a kind
(identifier, keyword, etc.), the original F# code and tooltip information.
The tooltip is further formatted using a simple document format, but we simply
print the value using the F# pretty printing, so the result looks as follows:

    let hello[Literal "val hello : unit -> unit"; ...] () =
      printfn[Literal "val printfn : TextWriterFormat<'T> -> 'T"; ...] "Hello world"

The `Omitted` token is generated if you use the special `(*[omit:...]*)` command.
The `Output` token is generated if you use the `// [fsi:...]` command to format
output returned by F# interactive. The `Error` command wraps code that should be
underlined with a red squiggle if the code contains an error.

## Generating HTML output

Finally, the `CodeFormat` type also includes a method [CodeFormat.FormatHtml](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-codeformat-codeformat.html#FormatHtml) that can be used
to generate nice HTML output from an F# snippet. This is used, for example, on
[F# Snippets](http://www.fssnip.net). The following example shows how to call it:

*)
let prefix = "fst"
let html = CodeFormat.FormatHtml(snippets, prefix)

// Print all snippets, in case there is more of them
for snip in html.Snippets do
    printfn "%s" snip.Content

// Print HTML code that is generated for ToolTips
printfn "%s" html.ToolTip
(**
If the input contains multiple snippets separated using the `//[snippet:...]` comment, e.g.:

<table class="pre"><tr><td class="lines"><pre class="fssnip">
<span class="l">1: </span>
<span class="l">2: </span>
<span class="l">3: </span>
<span class="l">4: </span>
<span class="l">5: </span>
<span class="l">6: </span>
<span class="l">7: </span>
</pre>
</td>
<td class="snippet"><pre class="fssnip"><span class="c">// [snippet: First sample]</span>
<span class="i">printf</span> <span class="s">"The answer is: %A"</span> <span class="n">42</span>
<span class="c">// [/snippet]</span>
<span class="c">// [snippet: Second sample]</span>
<span class="i">printf</span> <span class="s">"Hello world!"</span>
<span class="c">// [/snippet]</span>
</pre>
</td>
</tr>
</table>
then the formatter returns multiple HTML blocks. However, the generated tooltips
are shared by all snippets (to save space) and so they are returned separately.

*)

