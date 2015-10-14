(*** hide ***)
let root = "C:\\"

(**
F# Formatting: Library documentation
====================================

The library `FSharp.MetadataFormat.dll` is a replacement for the `FsHtmlTool`
which is available in the F# PowerPack and can be used to generate documentation 
for F# libraries with XML comments. The F# Formatting re-implementation has
a couple of extensions:

 - You can use Markdown instead of XML in `///` comments
 - The HTML is generated using Razor, so it is easy to change the templates

Building library documentation
------------------------------

First, we need to load the assembly and open necessary namespaces:
*)

#r "../../bin/FSharp.MetadataFormat.dll"
open FSharp.MetadataFormat
open System.IO

(**
Building the library documentation is easy - you just need to call
`MetadataFormat.Generate` from your FAKE script or from F# Interactive.
The method takes three (required) parameters - the path to your `DLL`,
output directory and directory with Razor templates.
Assuming `root` is the root directory for your project, you can write:
*)

MetadataFormat.Generate
  ( Path.Combine(root, "bin/YourLibrary.dll"), 
    Path.Combine(root, "output"),
    [ Path.Combine(root, "templates") ] )

(**
Adding Go to GitHub source links
-----------------
You can automatically add GitHub links to each functions, values and class members for further reference.
You need to specify two more arguments: `sourceRepo` to the GitHub repository 
and `sourceFolder` to the folder where your DLLs are built.
It is assumed that `sourceRepo` and `sourceFolder` have synchronized contents.
*)

MetadataFormat.Generate
  ( Path.Combine(root, "bin/YourLibrary.dll"), 
    Path.Combine(root, "output"),
    [ Path.Combine(root, "templates") ],
    sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/tree/master",
    sourceFolder = "/path/to/FSharp.Formatting" )
    

(**
Adding cross-type links to modules and types in the same assembly
-----------------
You can automatically add cross-type links to the documentation pages of other modules and types in the same assembly.
You can do this in two different ways:
* Add a [markdown inline link](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#links) were the link
title is the name of the type you want to link.


     /// this will generate a link to [Foo.Bar] documentation


* Add a [Markdown inlide code](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#code) (using
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
module Foo2 =
    
    /// This is not the same type as `Foo.Bar`
    type Bar = double

    /// Using the simple name for [Bar] will fail to create a link because the name is duplicated in 
    /// [Foo.Bar] and in [Foo2.Bar]. In this case, using the full name works.
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
module Foo = 
   let a = 42
(**
Classic XML documentation comments
----------------------------------

By default `FSharp.Formatting` will expect Markdown documentation comments, to parse XML comments
pass the named argument `markDownComments` with value `false`.
*)

MetadataFormat.Generate
  ( Path.Combine(root, "bin/YourLibrary.dll"), 
    Path.Combine(root, "output"),
    [ Path.Combine(root, "templates") ],
    sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/tree/master",
    sourceFolder = "/path/to/FSharp.Formatting", markDownComments = false )
(**
An example of an XML documentation comment:
*)
/// <summary>
/// Some actual comment
/// <para>Another paragraph</para>
/// </summary>
module Foo = 
   let a = 42
(**
Note that currently our code is not handling `<parameter>` and `<result> tags, this is 
not so much of a problem given that FSharp.Formatting infers the signature via reflection.


## Optional parameters

All of the three methods discussed in the previous two sections take a number of optional
parameters that can be used to tweak how the formatting works:


  - `outDir` - specifies the output directory where documentation should be placed
  - `layoutRoots` - a list of paths where Razor templates can be found
  - `parameters` - provides additional parameters to the Razor templates
  - `xmlFile` - can be used to override the default name of the XML file (by default, we assume
     the file has the same name as the DLL)
  - `markDownComments` - specifies if you want to use the Markdown parser for in-code comments.
    With `markDownComments` enabled there is no support for `<see cref="">` links, so `false` is 
    recommended for C# assemblies (if not specified, `true` is used).
  - `typeTemplate` - the templates to be used for normal types (and C# types)
    (if not specified, `"type.cshtml"` is used).
  - `moduleTemplate` - the templates to be used for modules
    (if not specified, `"module.cshtml"` is used).
  - `namespaceTemplate` - the templates to be used for namespaces
    (if not specified, `"namespaces.cshtml"` is used).
  - `assemblyReferences` - The assemblies to use when compiling Razor templates.
    Use this parameter if templates fail to compile with `mcs` on Linux or Mac or
    if you need additional references in your templates
    (if not specified, we use the currently loaded assemblies).
  - `sourceFolder` and `sourceRepo` - When specified, the documentation generator automatically
    generates links to GitHub pages for each entity.
  - `publicOnly` - When set to `false`, the tool will also generate documentation for non-public members
  - `libDirs` - Use this to specify additional paths where referenced DLL files can be found
  - `otherFlags` - Additional flags that are passed to the F# compiler (you can use this if you want to 
    specify references explicitly etc.)
  - `urlRangeHighlight` - A function that can be used to override the default way of generating GitHub links



Work in progress!
-----------------

This library is under active development and it is not completely functional.
For more examples of how it can be used, see the 
[build script](https://github.com/BlueMountainCapital/FSharp.DataFrame/blob/master/tools/build.fsx) 
for F# DataFrame. The project also includes nice 
[Razor templates](https://github.com/BlueMountainCapital/FSharp.DataFrame/tree/master/tools/reference) 
that you can use.

*)
