[![Binder](img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/fsharp.formatting/gh-pages?filepath=literate.ipynb)&emsp;
[![Script](img/badge-script.svg)](https://fsprojects.github.io/FSharp.Formatting//literate.fsx)&emsp;
[![Notebook](img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Formatting//literate.ipynb)

# Literate Scripts

The following example shows most of the features that can be used in a literate
F# script file with `.fsx` extension. Most of the features should be quite self-explanatory:

```fsharp
(**
# First-level heading
Some more documentation using `Markdown`.
*)

let helloWorld() = printfn "Hello world!"

(**
## Second-level heading
With some more documentation
*)

let numbers = [ 0 .. 99 ]
(*** include-value: numbers ***)

List.sum numbers
(*** include-it ***)

```

The F# script files are processed as follows:

* A multi-line comment starting with `(**` and ending with `*)` is
turned into text and is processed using the F# Markdown processor
(which supports standard Markdown commands).
  

* A single-line comment starting with `(***` and ending with `***)`
is treated as a special command. The command can consist of
`key`, `key: value` or `key=value` pairs.
  

Literate Command | Description
:--- | :---
`(** ... *)` | Markdown
`(*** condition: prepare ***)` | Utilise a code snippet when analyzing for tooltips or executing for outputs
`(*** condition: ipynb ***)` | Include a code snippet when making a .ipynb notebook
`(*** condition: tex ***)` | Include a code snippet when making a .tex output
`(*** condition: html ***)` | Include a code snippet when making HTML output
`(*** hide ***)` | Hide the subsequent snippet
`(*** raw ***)` | The subsequent code is treated as raw text


### Naming and including snippets

The command `define` defines a named snippet (such as `final-sample`) and removes the command together with
the following F# code block from the main document. The snippet can then
be referred to in 'include'. This makes it
possible to write documents without the ordering requirements of the
F# language.

Literate Command | Description
:--- | :---
`(*** define: snippet-name ***)` | Define a named snippet
`(*** include: snippet-name ***)` | Include the code of the named snippet


### Naming and including outputs

Literate Command | Description
:--- | :---
`(*** define-output: output-name ***)` | Define a name for the outputs of the preceding snippet
`(*** include-output ***)` | The console output of the preceding snippet
`(*** include-output: output-name ***)` | The console output of the snippet (named with define-output)
`(*** include-fsi-output ***)` | The F# Interactive output of the preceding snippet
`(*** include-fsi-output: output-name ***)` | The F# Interactive output of the snippet (named with define-output)
`(*** include-fsi-merged-output ***)` | The merge of console output and F# Interactive output of the preceding snippet
`(*** include-fsi-merged-output: output-name ***)` | The merge of console output and F# Interactive output of the snippet (named with define-output)
`(*** include-it ***)` | The formatted result of the preceding snippet
`(*** include-it: output-name ***)` | The formatted result of the snippet (named with define-output)
`(*** include-it-raw ***)` | The unformatted result of the preceding snippet
`(*** include-it-raw: output-name ***)` | The unformatted result of the snippet (named with define-output)
`(*** include-value: value-name ***)` | The formatted value, an F# identifier name


#### Hiding code snippets

The command `hide` specifies that the following F# code block (until the next comment or command) should be
omitted from the output.

#### Evaluating and formatting results

The commands to evaluate and format results are explained in [evaluation](evaluation.html).
You must build your documentation with evaluation turned on using `--eval`.

#### Substitutions

Substitutions are applied to content, see [content](content.html).

### Literate Markdown Documents

For files with `.md` extension, the entire file is a Markdown document, which may
contain F# code snippets (but also other code snippets). As usual, snippets are
indented with four spaces. In addition, the snippets can be annotated with special
commands. Some of them are demonstrated in the following example:

# First-level heading

    [hide]
    let print s = printfn "%s" s

Some more documentation using `Markdown`.

    [module=Hello]
    let helloWorld() = print "Hello world!"

## Second-level heading
With some more documentation

    [lang=csharp]
    Console.WriteLine("Hello world!");

When processing the document, all F# snippets are copied to a separate file that
is type-checked using the F# compiler (to obtain colours and tooltips).
The commands are written on the first line of the named snippet, wrapped in `[...]`:

* The `hide` command specifies that the F# snippet should not be included in the
final document. This can be used to include code that is needed to type-check
the code, but is not visible to the reader.
  

* The `module=Foo` command can be used to specify F# `module` where the snippet
is placed. Use this command if you need multiple versions of the same snippet
or if you need to separate code from different snippets.
  

* The `lang=foo` command specifies the language of the named snippet. If the language
is other than `fsharp`, the snippet is copied to the output as `<pre>` HTML
tag without any processing.
  

### LaTeX in Literate Scripts and Markdown Documents

Literate Scripts may contain LaTeX sections in Markdown using these forms:

0 Single line latex starting with `$$`.
  

1 A block delimited by `\begin{equation}...\end{equation}` or `\begin{align}...\end{align}`.
  

2 An indented paragraph starting with `$$$`.  This is F#-literate-specific and corresponds to
`\begin{equation}...\end{equation}`.
  

For example

$$\frac{x}{y}$$

\begin{equation}
   \frac{d}{dx} \left. \left( x \left( \left. \frac{d}{dy} x y \; \right|_{y=3} \right) \right) \right|_{x=2}
\end{equation}

Becomes

\begin{equation}
\frac{x}{y}
\end{equation}

\begin{equation}
   \frac{d}{dx} \left. \left( x \left( \left. \frac{d}{dy} x y \; \right|_{y=3} \right) \right) \right|_{x=2}
\end{equation}

The LaTeX will also be used in HTML and iPython notebook outputs.

### Making literate scripts work for different outputs

Literate scripts and markdown can be turned into LaTex, Python Notebooks and F# scripts.

A header may be needed to get the code to load, a typical example is this:

    (*** condition: prepare ***)
    #nowarn "211"
    #I "../src/FSharp.Formatting/bin/Release/netstandard2.1"
    #r "FSharp.Formatting.Common.dll"
    #r "FSharp.Formatting.Markdown.dll"
    #r "FSharp.Formatting.CodeFormat.dll"
    #r "FSharp.Formatting.Literate.dll"
    (*** condition: fsx ***)
#if FSX
    #r "nuget: FSharp.Formatting,1.0.0"
#endif // FSX
    (*** condition: ipynb ***)
#if IPYNB
    #r "nuget: FSharp.Formatting,1.0.0"
#endif // IPYNB

### Processing literate files programmatically

To process files use the two static methods to turn single documents into HTML
as follows using functionality from the [Literate](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-literate-literate.html) type:

```fsharp
open System.IO
open FSharp.Formatting.Literate

let source = __SOURCE_DIRECTORY__
let template = Path.Combine(source, "template.html")

let script = Path.Combine(source, "../docs/script.fsx")

Literate.ConvertScriptFile(script, template)

let doc = Path.Combine(source, "../docs/document.md")

Literate.ConvertMarkdownFile(doc, template)
```

The following sample also uses the optional parameter `parameters` to specify additional
keywords that will be replaced in the template file (this matches the `template-project.html`
file which is included as a sample in the package):

```fsharp
// Load the template & specify project information
let projTemplate = source + "template-project.html"

let projInfo =
    [ "fsdocs-authors", "Tomas Petricek"
      "fsdocs-source-link", "https://github.com/fsprojects/FSharp.Formatting"
      "fsdocs-collection-name", "F# Formatting" ]
```

The methods used above ([Literate.ConvertScriptFile](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-literate-literate.html#ConvertScriptFile), [Literate.ConvertMarkdownFile](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-literate-literate.html#ConvertMarkdownFile))
produce HTML output by default, but they can be also used to produce LaTeX output. This is done
by setting the output kind. The following
example shows how to call the methods to generate LaTeX documents:

```fsharp
let templateTex = Path.Combine(source, "template.tex")

let scriptTex = Path.Combine(source, "../docs/script.fsx")

Literate.ConvertScriptFile(scriptTex, templateTex, outputKind = OutputKind.Latex)

let docTex = Path.Combine(source, "../docs/document.md")

Literate.ConvertMarkdownFile(docTex, templateTex, outputKind = OutputKind.Latex)
```

The methods used above (`ConvertScriptFile`, `ConvertMarkdownFile`)
can also produce iPython Notebook output. This is done
by setting the named parameter `format` to `OutputKind.Pynb`:

```fsharp
// Process script file, Markdown document and a directory
let scriptPynb = Path.Combine(source, "../docs/script.fsx")

Literate.ConvertScriptFile(scriptPynb, outputKind = OutputKind.Pynb)

let docPynb = Path.Combine(source, "../docs/document.md")

Literate.ConvertMarkdownFile(docPynb, outputKind = OutputKind.Pynb)
```

All of the three methods discussed in the previous two sections take a number of optional
parameters that can be used to tweak how the formatting works


