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
API Documentation generation 
====================================

The [command-line tool `fsdocs`](commandline.html) can be used to generate documentation 
for F# libraries with XML comments.  The documentation is normally built using `fsdocs build` and developed using `fsdocs watch`. For
the former the output will be placed in `output\reference` by default.

## Templates

The HTML is built by instantiating a template. The template used is the first of:

* `docs/reference/_template.html` 

* `docs/_template.html`

* The default template

Usually the same template can be used as for [other content](content.html).

## Classic XML Doc Comments

XML Doc Comments may use [the normal F# and C# XML doc standards](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/).

In addition, you may also use

* `<a href = "...">` links

* Arbitrary paragraph-level HTML such as `<b>` for bold in XML doc text

* `<namespacesummary>` for giving summary sections for the enclosing namespace

* `<namespaceremarks>` for giving extended remarks for the enclosing namespace

* `<exclude>` and `<omit>` to exclude from XML docs

* `<category>` to give a category for presentation

An example of an XML documentation comment, assuming the code is in namespace `TheNamespace`:
*)
/// <summary>
///   Some actual comment
///   <para>Another paragraph, see  <see cref="T:TheNamespace.SomeType"/>. </para>
/// </summary>
///
/// <param name="x">The input</param>
///
/// <returns>The output</returns>
///
/// <example>
///   Try using
///   <code>
///      open TheNamespace
///      SomeModule.a
///   </code>
/// </example>
///
/// <namespacesummary>A namespace to remember</namespacesummary>
///
/// <namespaceremarks>More on that</namespaceremarks>
///
/// <category>Foo</category>
///

module SomeModule = 
   let someFunction x = 42 + x

/// <summary>
///   A type, see  <see cref="T:TheNamespace.SomeModule"/> and
///  <see cref="T:TheNamespace.SomeModule.someFunction"/>. </para>
/// </summary>
///
type SomeType() =
   member x.P = 1

(**

## Go to Source links

'fsdocs' normally automatically adds GitHub links to each functions, values and class members for further reference.

This is normally done automatically based on the following settings:

    <RepositoryUrl>https://github.com/...</RepositoryUrl>
    <RepositoryBranch>...</RepositoryBranch>
    <RepositoryType>git</RepositoryType>

If your source is not built from the same project where you are building documentation then
you may need these settings:

    <FsDocsSourceRepository>...</FsDocsSourceRepository> -- the URL for the root of the source 
    <FsDocsSourceFolder>...</FsDocsSourceFolder>         -- the root soure folder at time of build

It is assumed that `sourceRepo` and `sourceFolder` have synchronized contents.

## Markdown Comments

You can use Markdown instead of XML in `///` comments. If you do, you should set `<UsesMarkdownComments>` in
your F# project file.

> Note: Markdown Comments are not supported in all F# IDE tooling.

### Adding cross-type links to modules and types in the same assembly

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
### Markdown Comments: Excluding APIs from the docs

If you want to exclude modules or functions from the API docs you can use the `[omit]` tag.
It needs to be set on a separate tripple-slashed line, but it could be either the first or the last:

*)
/// [omit]
/// Some actual comment
module Bar = 
   let a = 42
(**


## Building library documentation programmatically

You can build library documentation programatically. To do this, load the assembly and open necessary namespaces:
*)

#r "FSharp.Formatting.ApiDocs.dll"
open FSharp.Formatting.ApiDocs
open System.IO

(**
Building the library documentation is easy - you just need to call
`ApiDocs.Generate` from your FAKE script or from F# Interactive.
Assuming `root` is the root directory for your project, you can write:
*)

let file = Path.Combine(root, "bin/YourLibrary.dll")
let input = ApiDocInput.FromFile(file) 
ApiDocs.GenerateHtml
    ( [ input ], 
      output=Path.Combine(root, "output"),
      collectionName="YourLibrary",
      template=Path.Combine(root, "templates", "template.html"),
      parameters=[])

