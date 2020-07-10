(**
F# Formatting: Literate programming
===================================

The `FSharp.Formatting.Literate` namespace implements 
a literate programming model for F# script files (`*.fsx` files) or Markdown
documents (`*.md` files) containing F# snippets.

Two inputs are accepted:

 - Documents that are valid F# script files (`*.fsx`) and contain special
   comments with documentation and commands for generating HTML output

 - Documents that are Markdown documents (`*.md`) and contain blocks of 
   F# code (indented by four spaces as usual in Markdown)

### F# Script files

The following example shows most of the features that can be used in a literate
F# script file. Most of the features should be quite self-explanatory:

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
|   `(*** define: snippet-name ***)`       | Define a named snippet  |
|   `(*** hide ***)`       | Hide the subsequent snippet  |
|   `(*** include-output ***)`       | The output of the preceeding snippet   |
|   `(*** include-otuput: snippet-name ***)`       | The output of the snippet  |
|   `(*** include-it ***)`       | The formatted result of the preceeding snippet |
|   `(*** include-it: snippet-name ***)`       | The formatted result of the snippet  |
|   `(*** include-value: value-name ***)`       | The formatted value  |
|   `(*** include: snippet-name ***)`       | Include the code of the snippet |
|   `(*** raw ***)`       | The subsequent code is treated as raw text |

The command `define` defines a named snippet (such as `final-sample`) and removes the command together with 
the following F# code block from the main document. The snippet can then
be inserted elsewhere in the document using `include`. This makes it
possible to write documents without the ordering requirements of the
F# language.

The command `hide` specifies that the following F# code block (until the next comment or command) should be 
omitted from the output.

Other commands are explained in [evaluation](evaluation.html).

### Markdown documents

In the Markdown mode, the entire file is a valid Markdown document, which may
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
The commands are written on the first line of the snippet, wrapped in `[...]`:

 - The `hide` command specifies that the F# snippet should not be included in the
   final document. This can be used to include code that is needed to type-check
   the code, but is not visible to the reader.

 - The `module=Foo` command can be used to specify F# `module` where the snippet
   is placed. Use this command if you need multiple versions of the same snippet
   or if you need to separate code from different snippets.

 - The `lang=foo` command specifies the language of the snippet. If the language
   is other than `fsharp`, the snippet is copied to the output as `<pre>` HTML
   tag without any processing.

Typical literate setup
----------------------
*)

(*** hide ***)
#nowarn "211"
#I "../../src/FSharp.Formatting/bin/Release/netstandard2.0"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Formatting.Markdown.dll"
#r "FSharp.Formatting.CodeFormat.dll"
#r "FSharp.Formatting.Literate.dll"
(**
For literate programming support in your project, install the `FSharp.Formatting` nuget package.

Now we can open `FSharp.Formatting.Literate` and use the `Literate` type to process individual
documents or entire directories.

### Processing individual files

Use the two static methods to turn single documents into HTML
as follows:
*)
open System.IO
open FSharp.Formatting.Literate

let source = __SOURCE_DIRECTORY__
let template = Path.Combine(source, "template.html")

let script = Path.Combine(source, "../docs/script.fsx")
Literate.ConvertScriptFile(script, template)

let doc = Path.Combine(source, "../docs/document.md")
Literate.ConvertMarkdown(doc, template)

(**
For HTML, you can optionally provide a template. Two sample templates
are included: for a [single file](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/templates/template-file.html)
and for a [project](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/templates/template-project.html),
but you can use your own. If no template is provided, the result is simply the HTML body
of the document with HTML for tool tips appended to the end.

The template should include two parameters that will be replaced with the actual
HTML: `{{document}} will be replaced with the formatted document;
`{{tooltips}}` will be replaced with (hidden) `<div>` elements containing code for tool tips that appear
when you place mouse pointer over an identifier. Optionally, you can also use 
`{{page-title}}` which will be replaced with the text in a first-level heading.
The template should also reference `style.css` and `tips.js` that define CSS style
and JavaScript functions used by the generated HTML (see sample [stylesheet](https://github.com/fsprojects/FSharp.Formatting/blob/master/src/FSharp.Formatting.CodeFormat/files/style.css)
and [script](https://github.com/fsprojects/FSharp.Formatting/blob/master/src/FSharp.Formatting.CodeFormat/files/tips.js) on GitHub).

### Processing entire directories

If you have multiple script files and Markdown documents (this time, they need to have
the `*.md` file extension) in a single directory, you can run the tool on a directory.
It will also automatically check that files are re-generated only when they were changed,
and also copy over any other files. Processing is recursive, making this call a form of static
site generation.

The following sample also uses optional parameter `parameters` to specify additional
keywords that will be replaced in the template file (this matches the `template-project.html`
file which is included as a sample in the package):
*)

// Load the template & specify project information
let projTemplate = source + "template-project.html"
let projInfo =
  [ "authors", "Tomas Petricek"
    "github-link", "https://github.com/fsprojects/FSharp.Formatting"
    "project-name", "F# Formatting" ]

(**
You can also convert entire directories of content.

- Content that is not `*.fsx` or `*.md` is copied across 

- If a file `_template.html` exists then is used as the template for that directory and all sub-content.

- Any file or directory beginning with `.` is ignored.

- A set of parameter substitutions can be provided operative across all files.
*)

// Process all files and save results to 'output' directory
Literate.ConvertDirectory
  (source, projTemplate, source + "\\output", parameters=projInfo)

(**
The sample template `template-project.html` has been used to generate this documentation
and it includes additional parameters for specifying various information about F#
projects.

 * [Sample Markdown file](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/demo.md)
   produces the following [LaTeX output](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/output/demo-doc.tex)
   and [HTML output](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/output/demo-doc.html)

## Generating LaTeX output

The methods used above (`ConvertScriptFile`, `ConvertMarkdown` as well as `ConvertDirectory`) 
produce HTML output by default, but they can be also used to produce LaTeX output. This is done
by setting the named parameter `format` to one of the two `OutputKind` cases. The following
example shows how to call the methods to generate LaTeX documents:
*)
let templateTex = Path.Combine(source, "template.tex")

let scriptTex = Path.Combine(source, "../docs/script.fsx")
Literate.ConvertScriptFile(scriptTex, templateTex, format=OutputKind.Latex)

let docTex = Path.Combine(source, "../docs/document.md")
Literate.ConvertMarkdown(docTex, templateTex, format=OutputKind.Latex)

Literate.ConvertDirectory(source, templateTex, source + "\\output",  format=OutputKind.Latex)

(**
Note that the `template.tex` file needs to contain `{content}` as the key where the body
of the document is placed (this differs from `{document}` used in the HTML format to avoid
collision with standard Latex `{document}` tag). The project comes with two samples (also 
available as part of the NuGet package). The sample Latex (and HTML) outputs look as follows:

 * [Sample F# script file](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/demo.fsx)
   produces the following [LaTeX output](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/output/demo-script.tex)
   and [HTML output](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/literate/output/demo-script.html)

## Generating iPython Notebook output

> NOTE: This feature is experimental and not all features of markdown or notebooks is supported

The methods used above (`ConvertScriptFile`, `ConvertMarkdown` as well as `ConvertDirectory`) 
can also produce iPython Notebook output. This is done
by setting the named parameter `format` to `OutputKind.Pynb`:
*)

// Process script file, Markdown document and a directory
let scriptPynb = Path.Combine(source, "../docs/script.fsx")
Literate.ConvertScriptFile(scriptPynb, format=OutputKind.Pynb)

let docPynb = Path.Combine(source, "../docs/document.md")
Literate.ConvertMarkdown(docPynb, format=OutputKind.Pynb)

Literate.ConvertDirectory( source, source + "/output", format=OutputKind.Pynb)

(**

## Optional parameters

All of the three methods discussed in the previous two sections take a number of optional
parameters that can be used to tweak how the formatting works or even to specify a different
version of the F# compiler:

 - `prefix` - a string that is added to all automatically generated `id` attributes
   in the generated HTML document (to avoid collisions with other HTML elements)
 - `compilerOptions` - this can be used to pass any additional command line 
   parameters to the F# compiler (you can use any standard parameters of `fsc.exe`)
 - `lineNumbers` - if `true` then the generated F# snippets include line numbers.
 - `references` - if `true` then the script automatically adds a "References" 
   section with all indirect links that are defined and used in the document.
 - `parameters` - a list of key-value pairs containing additional parameters
   that should be replaced in the tempalte HTML file.
 - `includeSource` - when `true`, parameter `{{source}}` will be replaced with a 
   `<pre>` tag containing the original source code of the F# Script or Markdown document.
 - `errorHandler` - a function that is used to report errors from the F# compiler 
   (if not specified, errors are printed to the standard output)
 - `generateAnchors` - when `true`, the generated HTML will automatically include
   anchors for all headings (and so you can click on headings to get a link
   to a section). The default value is `false`.
 - `customizeDocument` - Allows you to customize the document before writing it 
   to the output file. This gives you the opportunity to use your own
   code formatting code, for example to support syntax highlighting for another language. 

*)
