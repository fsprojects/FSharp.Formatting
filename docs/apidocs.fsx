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

(*** hide ***)
let root = "C:\\"

(**
F# Formatting: API Documentation generation 
====================================

The [command-line tool `fsdocs`](commandline.html) or the namespace `FSharp.Formatting.ApiDocs` can be used to generate documentation 
for F# libraries with XML comments. 

 - You can use Markdown instead of XML in `///` comments
 - The HTML is built by instantiating a template
 - An ApiDocs model is available if you want to integrate with your own approach to templating.

The documentation is normally built using

    [lang=text]
    fsdocs build

The output will be placed in `output\reference` by default, and the template used is
`docs\_template.html`.


Searchable API docs
-----------------

When using the command-line tool a Lunr search index is automatically generated in `index.json`.

To add a search box to your `_template.html`, load `fsdocs-search.js` found in this repo
(see the relevant sections of [`_template.html` used here](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/_template.html),
for example this HTML code:

    ...
    <div id="header">
      <div class="searchbox">
        <label for="search-by">
          <i class="fas fa-search"></i>
        </label>
        <input data-search-input="" id="search-by" type="search" placeholder="Search..." />
        <span data-search-clear="">
          <i class="fas fa-times"></i>
        </span>
      </div>
    </div>
    ...

Adding Go to GitHub source links
-----------------
You can automatically add GitHub links to each functions, values and class members for further reference.
You need to specify two more arguments: `sourceRepo` to the GitHub repository 
and `sourceFolder` to the folder where your DLLs are built.

It is assumed that `sourceRepo` and `sourceFolder` have synchronized contents.

Adding cross-type links to modules and types in the same assembly
-----------------
You can automatically add cross-type links to the documentation pages of other modules and types in the same assembly.
You can do this in two different ways:

* Add a [markdown inline link](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#links) were the link
title is the name of the type you want to link.


     /// this will generate a link to [Foo.Bar] documentation


* Add a [Markdown inline code](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#code) (using
back-ticks) where the code is the name of the type you want to link.


     /// This will also generate a link to `Foo.Bar` documentation


You can use either the full name (including namespace and module) or the simple name of a type.
If more than one type is found with the same name the link will not be generated.
If a type with the given name is not found in the same assembly the link will not be generated.
*)

/// Contains two types [Bar] and [Foo.Baz]
module Foo = 
    
    /// Bar is just an `int` and belongs to module [Foo]
    type Bar = int
    
    /// Baz contains a `Foo.Bar` as its `id`
    type Baz = { id: Bar }

    /// This function operates on `Baz` types.
    let f (b:Baz) = 
        b.id * 42

/// Referencing [Foo3] will not generate a link as there is no type with the name `Foo3`
module Foo3 =
    
    /// This is not the same type as `Foo.Bar`
    type Bar = double

    /// Using the simple name for [Bar] will fail to create a link because the name is duplicated in 
    /// [Foo.Bar] and in [Foo3.Bar]. In this case, using the full name works.
    let f2 b =
         b * 50

(**
Excluding APIs from the docs
-----------------

If you want to exclude modules or functions from the API docs you can use the `[omit]` tag.
It needs to be set on a separate tripple-slashed line, but it could be either the first or the last:

*)
/// [omit]
/// Some actual comment
module Bar = 
   let a = 42
(**
Classic XML documentation comments
----------------------------------

By default `FSharp.Formatting` will expect Markdown documentation comments, to parse XML comments
pass the named argument `mdcomments` with value `false`.

An example of an XML documentation comment:
*)
/// <summary>
/// Some actual comment
/// <para>Another paragraph</para>
/// </summary>
module Foo2 = 
   let a = 42
(**
Note that currently our code is not handling `<parameter>` and `<result> tags, this is 
not so much of a problem given that FSharp.Formatting infers the signature via reflection.



Building library documentation programmatically
------------------------------

First, we need to load the assembly and open necessary namespaces:
*)

#r "FSharp.Formatting.ApiDocs.dll"
open FSharp.Formatting.ApiDocs
open System.IO

(**
Building the library documentation is easy - you just need to call
`ApiDocs.Generate` from your FAKE script or from F# Interactive.
Assuming `root` is the root directory for your project, you can write:
*)

ApiDocs.GenerateHtml
  ( [ Path.Combine(root, "bin/YourLibrary.dll") ], 
    outDir=Path.Combine(root, "output"),
    template=Path.Combine(root, "templates", "template.html") )
