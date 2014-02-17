(**
F# Formatting: Literate programming
===================================

The `FSharp.Formatting` package comes a library `FSharp.Literate.dll` that implements the 
idea of _literate programming_. The script uses the F# Markdown processor and code
formatter to generate nice HTML pages from F# script files (`*.fsx` files) or Markdown
documents (`*.md` files) containing F# snippets.

The next section of the article discusses the two options and introduces some special
commands that you can use when writing your script files. The second section shows
how to use the literate programming library from your F# projects.

Literate programming 
--------------------

The script can work in two different modes, depending on the kind of input
you want to write (and turn into HTML):

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
    
    (*** include: final-sample ***)

    (** 
    ## Second-level heading
    With some more documentation
    *)
    
    (*** define: final-sample ***)
    let helloWorld() = printfn "Hello world!"

The F# script files is processed as follows:

 - A multi-line comment starting with `(**` and ending with `*)` is 
   turned into text and is processed using the F# Markdown processor 
   (which supports standard Markdown commands).

 - A single-line comment starting with `(***` and ending with `***)` 
   is treated as a special command. The command can consist of 
   `key: value` or `key=value` pairs or just `key` command.

Two of the supported commands are `define`, which defines a named
snippet (such as `final-sample`) and removes the command together with 
the following F# code block from the main document. The snippet can then
be inserted elsewhere in the document using `include`. This makes it
possible to write documents without the ordering requirements of the
F# language.

Another command is `hide` (without a value) which specifies that the
following F# code block (until the next comment or command) should be 
omitted from the output.

### Markdown documents

In the Markdown mode, the entire file is a valid Markdown document, which may
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
is type-checked using the F# compiler (to obtain colours and tool tips).
The commands are written on the first line of the snippet, wrapped in `[...]`:

 - The `hide` command specifies that the F# snippet should not be included in the
   final document. This can be used to include code that is needed to type-check
   the code, but is not visible to the reader.

 - The `module=Foo` command can be used to specify F# `module` where the snippet
   is placed. Use this command if you need multiple versions of the same snippet
   or if you need to separate code from different snippets.

 - The `lang=foo` command specifies that the language of the snippet. If the language
   is other than `fsharp`, the snippet is copied to the output as `<pre>` HTML
   tag without any processing.

Typical literate setup
----------------------
*)

(*** hide ***)
#nowarn "211"
#I "../../bin"

(**
The typical way to setup literate programming support in your project is to reference
`FSharp.Formatting` using NuGet and then add a simple script file (e.g. `build.fsx`) that
calls the literate programming tools and generates the HTML (or LaTeX) output from your
samples. You can find an [example of such file](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/build.fsx)
on GitHub (and it is also copied with NuGet).

The typical `build.fsx` script first needs to reference `FSharp.Literate.dll`. Assuming
you're using version 2.0.2, the reference should look something like this:
*)
#I "../packages/FSharp.Formatting.2.0.2/lib/net40"
#r "FSharp.CodeFormat.dll"
#r "FSharp.Literate.dll"
open FSharp.Literate
open System.IO
(**
The first line tells F# interactive to automatically search for `*.dll` assemblies
in the directory where F# formatting binaries are located. The next two lines references
the library with all the important functionality.

Now we can open `FSharp.Literate` and use the `Literate` type to process individual
documents or entire directories.

### Processing individual files

The `Literate` type has two static methods `ProcessScriptFile` and `ProcessMarkdown`
that turn an F# script file and Markdown document, respectively, into an HTML file.
If you wish to specify the HTML file structure, you can provide a template. Two sample templates
are included: for a [single file](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/templates/template-file.html)
and for a [project](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/templates/template-project.html),
but you can use your own. If no template is provided, the result is simply the HTML body
of the document with HTML for tool tips appended to the end.

The template should include two parameters that will be replaced with the actual
HTML: `{document}` will be replaced with the formatted document; `{tooltips}` will be
replaced with (hidden) `<div>` elements containing code for tool tips that appear
when you place mouse pointer over an identifier. Optionally, you can also use 
`{page-title}` which will be replaced with the text in a first-level heading.
The template should also reference `style.css` and `tips.js` that define CSS style
and JavaScript functions used by the generated HTML (see sample [stylesheet](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/content/style.css)
and [script](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/content/tips.js) on GitHub).

Assuming you have `template.html` in the current directory, you can write:
*)

let source = __SOURCE_DIRECTORY__
let template = Path.Combine(source, "template.html")

(**
Then you can use the two static methods to turn single documents into HTML
as follows:
*)

let script = Path.Combine(source, "../docs/script.fsx")
Literate.ProcessScriptFile(script, template)

let doc = Path.Combine(source, "../docs/document.md")
Literate.ProcessMarkdown(doc, template)

(**
This sample uses `*.md` extension for Markdown documents, but this is not required when
using `ProcessMarkdown`. You can use any extension you wish. By default, the methods
will generate file with the same name (but with the `.html` extension). You can change
this by addint a third parameter with the output file name. There is a number of 
additional parameters you can specify - these are discussed below.

### Processing entire directories

If you have multiple script files and Markdown documents (this time, they need to have
the `*.md` file extension) in a single directory, you can run the tool on a directory.
It will also automatically check that files are re-generated only when they were changed.
The following sample also uses optional parameter `replacements` to specify additional
keywords that will be replaced in the template file (this matches the `template-project.html`
file which is included as a sample in the package):
*)

// Load the template & specify project information
let projTemplate = source + "template-project.html"
let projInfo =
  [ "page-description", "F# Literate programming"
    "page-author", "Tomas Petricek"
    "github-link", "https://github.com/tpetricek/FSharp.Formatting"
    "project-name", "F# Formatting" ]

// Process all files and save results to 'output' directory
Literate.ProcessDirectory
  (source, projTemplate, source + "\\output", replacements = projInfo)

(**
The sample template `template-project.html` has been used to generate this documentation
and it includes additional parameters for specifying various information about F#
projects.

## Generating LaTeX output

The methods used above (`ProcessScriptFile`, `ProcessMarkdown` as well as `ProcessDirectory`) 
produce HTML output by default, but they can be also used to produce Latex output. This is done
by setting the named parameter `format` to one of the two `OutputKind` cases. The following
example shows how to call the methods to generate Latex documents:
*)
// Template file containing the {content} tag and possibly others
let texTemplate = Path.Combine(source, "template.tex")

// Process script file, Markdown document and a directory
let scriptTex = Path.Combine(source, "../docs/script.fsx")
Literate.ProcessScriptFile(scriptTex, texTemplate, format = OutputKind.Latex)

let docTex = Path.Combine(source, "../docs/document.md")
Literate.ProcessMarkdown(docTex, template, format = OutputKind.Latex)

Literate.ProcessDirectory
  ( source, texTemplate, source + "\\output", 
    format = OutputKind.Latex, replacements = projInfo)

(**
Note that the `template.tex` file needs to contain `{content}` as the key where the body
of the document is placed (this differs from `{document}` used in the HTML format to avoid
collision with standard Latex `{document}` tag). The project comes with two samples (also 
available as part of the NuGet package). The sample
Latex outputs (compiled to a PDF file) look as follows:

 * [Sample Markdown file](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/demo.md)
   produces the following [formatted PDF file](https://github.com/tpetricek/FSharp.Formatting/raw/master/literate/outputs/demo.pdf)
 * [Sample F# script file](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/test.fsx)
   produces the following [formatted PDF file](https://github.com/tpetricek/FSharp.Formatting/raw/master/literate/outputs/test.pdf)

## Optional parameters

All of the three methods discussed in the previous two sections take a number of optional
parameters that can be used to tweak how the formatting works or even to specify a different
version of the F# compiler:

 - `fsharpCompiler` - a `System.Reflection.Assembly` object that represents the 
   `FSharp.Compiler.dll` assembly that should be used for processing the snippets
   (specify this if you want to use custom version of the compiler!)
 - `prefix` - a string that is added to all automatically generated `id` attributes
   in the generated HTML document (to avoid collisions with other HTML elements)
 - `compilerOptions` - this can be used to pass any additional command line 
   parameters to the F# compiler (you can use any standard parameters of `fsc.exe`)
 - `lineNumbers` - if `true` then the generated F# snippets include line numbers.
 - `references` - if `true` then the script automatically adds "References" 
   section with all indirect links that are defined & used in the document.
 - `replacements` - a list of key-value pairs containing additional parameters
   that should be replaced in the tempalte HTML file.
 - `includeSource` - when `true`, parameter `{source}` will be replaced with a 
   `<pre>` tag containing the original source code of the F# Script or Markdown document.
 - `errorHandler` - a function that is used to report errors from the F# compiler 
   (if not specified, errors are printed to the standard output)
*)
