﻿(**
F# Formatting: Literate programming
===================================

The `FSharp.Formatting` package comes with a [simple script][lit] that implements the 
idea of _literate programming_. The script uses the F# Markdown processor and code
formatter to generate nice HTML pages from F# script files (`*.fsx` files) or Markdown
documents (`*.md` files) containing F# snippets.

The next section of the article discusses the two options and introduces some special
commands that you can use when writing your script files. The second section shows
how to use the literate programming script from your F# projects. You can also
look at some implementation notes generated from the [script itself](literate.html).

  [lit]: https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/literate.fsx

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

Using the script
----------------
*)

(*** hide ***)
// NOTE: This is hidden in the output. It makes the tool work even if we
// provide invalid path later on (in order to document the typical scenario)
#I "../bin"
#load "../literate/literate.fsx"

(**
Using the literate programming script is very easy. If you install the `FSharp.Formatting`
package using NuGet, it will automatically install the `literate.fsx` file (if you 
do not want to use nuget, you can just copy the latest version of the file
from [GitHub](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/literate.fsx)
and modify it as you need).

Assuming you installed a version 1.0.4 of the package, you can load the 
script as follows (this assumes you're calling it from another script file
such as `tools\build.fsx` in your solution directory):
*)
#I "../packages/FSharp.Formatting.1.0.4/lib/net40"
#load "../packages/FSharp.Formatting.1.0.4/literate/literate.fsx"
open FSharp.Literate
open System.IO
(**
The first line tells F# interactive to automatically search for `*.dll` assemblies
in the directory where `FSharp.CodeFormat.dll` and `FSharp.Markdown.dll` are located.
This is required by the second line, which loads the script.

Now we can open `FSharp.Literate` and use the `Literate` type to process individual
documents or entire directories.

### Processing individual files

The `Literate` type has two static methods `ProcessScriptFile` and `ProcessMarkdown`
that turn an F# script file and Markdown document, respectively, into an HTML file.
To specify the HTML file structure, you need to provide a template. Two sample templates
are included: for a [single file](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/templates/template-file.html)
and for a [project](https://github.com/tpetricek/FSharp.Formatting/blob/master/literate/templates/template-project.html),
but you can use your own.

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
Now we also support PDF output via LaTeX format. You can turn single documents into LaTeX
using `template.tex` as follows:
*)

let template = Path.Combine(source, "template.tex")

let script = Path.Combine(source, "../docs/script.fsx")
Literate.ProcessScriptFile(script, template, format = Latex)

let doc = Path.Combine(source, "../docs/document.md")
Literate.ProcessMarkdown(doc, template, format = Latex)

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
let template = source + "template-project.html"
let projInfo =
  [ "page-description", "F# Literate programming"
    "page-author", "Tomas Petricek"
    "github-link", "https://github.com/tpetricek/FSharp.Formatting"
    "project-name", "F# Formatting" ]

// Process all files and save results to 'output' directory
Literate.ProcessDirectory
  (source, template, Html, source + "\\output", replacements = projInfo)

(**
The sample template `template-project.html` has been used to generate this documentation
and it includes additional parameters for specifying various information about F#
projects.

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
