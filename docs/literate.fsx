(*** condition: prepare ***)
#nowarn "211"
#I "../src/FSharp.Formatting/bin/Release/netstandard2.1"
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
[![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=literate.ipynb)

Literate Scripts and Markdown
===================================

The following example shows most of the features that can be used in a literate
F# script file with `.fsx` extension. Most of the features should be quite self-explanatory:

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

The F# script files is processed as follows:

- A multi-line comment starting with `(**` and ending with `*)` is 
  turned into text and is processed using the F# Markdown processor 
  (which supports standard Markdown commands).

- A single-line comment starting with `(***` and ending with `***)` 
  is treated as a special command. The command can consist of 
  `key`, `key: value` or `key=value` pairs.

|  Literate Command     | Description               |
|:-----------------------|:----------------------------|
|   `(** ... *)`       | Markdown  |
|   `(*** condition: prepare ***)`       | Utilise a code snippet when analyzing for tooltips or executing for outputs |
|   `(*** condition: ipynb ***)`       | Include a code snippet when making a .ipynb notebook  |
|   `(*** condition: tex ***)`       | Include a code snippet when making a .tex output   |
|   `(*** condition: html ***)`       | Include a code snippet when making HTML output   |
|   `(*** hide ***)`       | Hide the subsequent snippet  |
|   `(*** include-output ***)`       | The output of the preceeding snippet   |
|   `(*** include-fsi-output ***)`       | The F# Interactive output of the preceeding snippet   |
|   `(*** include-fsi-merged-output ***)`       | The merge of console output and F# Interactive output of the preceeding snippet   |
|   `(*** include-it ***)`       | The formatted result of the preceeding snippet |
|   `(*** include-it-raw ***)`       | The unformatted result of the preceeding snippet |
|   `(*** include-value: value-name ***)`       | The formatted value  |
|   `(*** raw ***)`       | The subsequent code is treated as raw text |

### Named snippets

The command `define` defines a named snippet (such as `final-sample`) and removes the command together with 
the following F# code block from the main document. The snippet can then
be referred to using these variations. This makes it
possible to write documents without the ordering requirements of the
F# language.

|  Literate Command     | Description               |
|:-----------------------|:----------------------------|
|   `(*** define: snippet-name ***)`       | Define a named snippet  |
|   `(*** include-output: snippet-name ***)`       | The output of the named snippet  |
|   `(*** include-fsi-output: snippet-name ***)`       | The F# Interactive output of the named snippet  |
|   `(*** include-fsi-merged-output: snippet-name ***)`       | The merge of console output and F# Interactive output of the named snippet  |
|   `(*** include-it: snippet-name ***)`       | The formatted result of the named snippet  |
|   `(*** include-it-raw: snippet-name ***)`       | The unformatted result of the named snippet  |
|   `(*** include: snippet-name ***)`       | Include the code of the named snippet |

#### Hiding code snippets

The command `hide` specifies that the following F# code block (until the next comment or command) should be 
omitted from the output.

#### Evaluating and formatting results

The commands to evaluate and format results are explained in [evaluation](evaluation.html).
You must build your documentation with evaluation turned on using `--eval`.

### Literate Markdown Documents

For files with `.md` extension, the entire file is a Markdown document, which may
contain F# code snippets (but also other code snippets). As usual, snippets are
indented with four spaces. In addition, the snippets can be annotated with special
commands. Some of them are demonstrated in the following example: 

    [lang=text]
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
is type-checked using the F# compiler (to obtain colours and tool tips).
The commands are written on the first line of the named snippet, wrapped in `[...]`:

 - The `hide` command specifies that the F# snippet should not be included in the
   final document. This can be used to include code that is needed to type-check
   the code, but is not visible to the reader.

 - The `module=Foo` command can be used to specify F# `module` where the snippet
   is placed. Use this command if you need multiple versions of the same snippet
   or if you need to separate code from different snippets.

 - The `lang=foo` command specifies the language of the named snippet. If the language
   is other than `fsharp`, the snippet is copied to the output as `<pre>` HTML
   tag without any processing.

*)

(**
### LaTeX in Literate Scripts and Markdown Documents

Literate Scripts may contain LaTeX sections in Markdown using these forms:

1. Single line latex starting with `$$`.

2. A block delimited by `\begin{equation}...\end{equation}` or `\begin{align}...\end{align}`. 

3. An indented paragraph starting with `$$$`.  This is F#-literate-specific and corresponds to
   `\begin{equation}...\end{equation}`.

For example

    [lang=text]
    $$\frac{x}{y}$$

    \begin{equation}
       \frac{d}{dx} \left. \left( x \left( \left. \frac{d}{dy} x y \; \right|_{y=3} \right) \right) \right|_{x=2}
    \end{equation}

Becomes

$$\frac{x}{y}$$

\begin{equation}
   \frac{d}{dx} \left. \left( x \left( \left. \frac{d}{dy} x y \; \right|_{y=3} \right) \right) \right|_{x=2}
\end{equation}

The LaTeX will also be used in HTML and iPython notebook outputs.

### Making literate scripts work for different outputs

Literate scripts and markdown can by turned into LaTex, Python Notebooks and F# scripts.

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
    #r "nuget: FSharp.Formatting,{{package-version}}"
    #endif // FSX
    (*** condition: ipynb ***)
    #if IPYNB
    #r "nuget: FSharp.Formatting,{{package-version}}"
    #endif // IPYNB

### Processing literate files programatically

To process file Use the two static methods to turn single documents into HTML
as follows:
*)
open System.IO
open FSharp.Formatting.Literate

let source = __SOURCE_DIRECTORY__
let template = Path.Combine(source, "template.html")

let script = Path.Combine(source, "../docs/script.fsx")
Literate.ConvertScriptFile(script, template)

let doc = Path.Combine(source, "../docs/document.md")
Literate.ConvertMarkdownFile(doc, template)

(**

The following sample also uses optional parameter `parameters` to specify additional
keywords that will be replaced in the template file (this matches the `template-project.html`
file which is included as a sample in the package):
*)

// Load the template & specify project information
let projTemplate = source + "template-project.html"
let projInfo =
  [ "fsdocs-authors", "Tomas Petricek"
    "fsdocs-source-link", "https://github.com/fsprojects/FSharp.Formatting"
    "fsdocs-collection-name", "F# Formatting" ]

(**

The methods used above (`ConvertScriptFile`, `ConvertMarkdownFile`) 
produce HTML output by default, but they can be also used to produce LaTeX output. This is done
by setting the output kind. The following
example shows how to call the methods to generate LaTeX documents:
*)
let templateTex = Path.Combine(source, "template.tex")

let scriptTex = Path.Combine(source, "../docs/script.fsx")
Literate.ConvertScriptFile(scriptTex, templateTex, outputKind=OutputKind.Latex)

let docTex = Path.Combine(source, "../docs/document.md")
Literate.ConvertMarkdownFile(docTex, templateTex, outputKind=OutputKind.Latex)

(**

The methods used above (`ConvertScriptFile`, `ConvertMarkdownFile`) 
can also produce iPython Notebook output. This is done
by setting the named parameter `format` to `OutputKind.Pynb`:
*)

// Process script file, Markdown document and a directory
let scriptPynb = Path.Combine(source, "../docs/script.fsx")
Literate.ConvertScriptFile(scriptPynb, outputKind=OutputKind.Pynb)

let docPynb = Path.Combine(source, "../docs/document.md")
Literate.ConvertMarkdownFile(docPynb, outputKind=OutputKind.Pynb)

(**

All of the three methods discussed in the previous two sections take a number of optional
parameters that can be used to tweak how the formatting works or even to specify a different
version of the F# compiler:

 - `prefix` - a string that is added to all automatically generated `id` attributes
   in the generated HTML document (to avoid collisions with other HTML elements)
 - `fscoptions` - this can be used to pass any additional command line 
   parameters to the F# compiler (you can use any standard parameters of `fsc.exe`)
 - `lineNumbers` - if `true` then the generated F# snippets include line numbers.
 - `references` - if `true` then the script automatically adds a "References" 
   section with all indirect links that are defined and used in the document.
 - `parameters` - a list of key-value pairs containing additional parameters
   that should be replaced in the tempalte HTML file.
 - `errorHandler` - a function that is used to report errors from the F# compiler 
   (if not specified, errors are printed to the standard output)
 - `generateAnchors` - when `true`, the generated HTML will automatically include
   anchors for all headings (and so you can click on headings to get a link
   to a section). The default value is `false`.
 - `customizeDocument` - Allows you to customize the document before writing it 
   to the output file. This gives you the opportunity to use your own
   code formatting code, for example to support syntax highlighting for another language. 

*)
