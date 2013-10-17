﻿(*** hide ***)
let root = "C:\\"

(**
F# Formatting: Library documentation
====================================

The library `FSharp.MetadataFormat.dll` is a replacement for the `FsHtmlTool`
which is available in F# PowerPack and can be used to generate documentation 
for F# libraries with XML comments. The F# Formatting re-implementation has
a couple of extensions:

 - You can use Markdown instead of XML in `///` comments
 - The HTML is generated using Razor, so it is easy to change the templates

Building library documentation
------------------------------

First, we need to load the assembly and open necessary namespaces:
*)

#r "../bin/FSharp.MetadataFormat.dll"
open FSharp.MetadataFormat
open System.IO

(**
Building the library documentation is easy - you just need to call
`MetadataFormat.Generate` from your FAKE script or from F# Interactive.
The method takes three (required) parameters - the path to your `dll`,
output directory and directory with Razor templates.
Assuming `root` is the root directory for your project, you can write:
*)

MetadataFormat.Generate
  ( Path.Combine(root, "bin/YourLibrary.dll"), 
    Path.Combine(root, "output"),
    Path.Combine(root, "templates") )
    
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
Work in progress!
-----------------

This library is under active development and it is not completely functional.
For more examples of how it can be used, see the 
[build script](https://github.com/BlueMountainCapital/FSharp.DataFrame/blob/master/tools/build.fsx) 
for F# DataFrame. The project also includes nice 
[Razor templates](https://github.com/BlueMountainCapital/FSharp.DataFrame/tree/master/tools/reference) 
that you can use.

*)
