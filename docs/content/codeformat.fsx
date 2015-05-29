(**
F# Formatting: Code formatting
==============================

This page demonstrates how to use `FSharp.CodeFormat.dll` to tokenize 
F# source code, obtain information about the source code (mainly tooltips
from the type-checker) and how to turn the code into a nicely formatted HTML.

First, we need to load the assembly and open necessary namespaces:
*)

#r "../../bin/FSharp.CodeFormat.dll"
open FSharp.CodeFormat
open System.Reflection

(**

Starting a background agent
---------------------------

The `FSharp.CodeFormat` namespace contains `CodeFormat` type which is the
entry point. The static method `CreateAgent` starts a background worker that
can be called to format snippets repeatedly:
*)

let fsharpCompiler = Assembly.Load("FSharp.Compiler")
let formattingAgent = CodeFormat.CreateAgent(fsharpCompiler)

(**
If you want to process multiple snippets, it is a good idea to keep the 
formatting agent around if possible. The agent needs to load the F# compiler
(which needs to load various files itself) and so this takes a long time. As the above
example shows, you can specify which version of `FSharp.Compiler.dll` to use.

Processing F# source
--------------------

The formatting agent provides a `ParseSource` method (together with an asynchronous
version for use from F# and also a version that returns a .NET `Task` for C#).
To call the method, we define a simple F# code as a string:
*)

let source = """
    let hello () = 
      printfn "Hello world"
  """
let snippets, errors = formattingAgent.ParseSource("C:\\snippet.fsx", source)

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


Working with returned tokens
----------------------------

Each returned snippet is essentially just a collection of lines and each line 
consists of a sequence of tokens. The following snippet prints basic information
about the tokens of our sample snippet:
*)

// Get the first snippet and obtain list of lines
let (Snippet(title, lines)) = snippets |> Seq.head

// Iterate over all lines and all tokens on each line
for (Line(tokens)) in lines do
  for token in tokens do
    match token with
    | TokenSpan.Token(kind, code, tip) -> 
        printf "%s" code
        tip |> Option.iter (fun spans ->
          printfn "%A" spans)          
    | TokenSpan.Omitted _ 
    | TokenSpan.Output _ 
    | TokenSpan.Error _ -> ()
  printfn ""

(**
The `TokenSpan.Token` is the most important kind of token. It consists of a kind
(identifier, keyword, etc.), the original F# code and tool tip information.
The tool tip is further formatted using a simple document format, but we simply 
print the value using the F# pretty printing, so the result looks as follows:

    let hello[Literal "val hello : unit -> unit"; ...] () = 
      printfn[Literal "val printfn : TextWriterFormat<'T> -> 'T"; ...] "Hello world"

The `Omitted` token is generated if you use the special `(*[omit:...]*)` command.
The `Output` token is generated if you use the `// [fsi:...]` command to format
output returned by F# interactive. The `Error` command wraps code that should be 
underlined with a red squiggle if the code contains an error.

Generating HTML output
----------------------

Finally, the `CodeFormat` type also includes a method `FormatHtml` that can be used
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
*)

(**
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
*)

(**
then the formatter returns multiple HTML blocks. However, the generated tool tips
are shared by all snippets (to save space) and so they are returned separately.
*)
