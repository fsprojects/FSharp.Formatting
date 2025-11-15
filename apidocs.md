[![Binder](img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/fsharp.formatting/gh-pages?filepath=apidocs.ipynb)&emsp;
[![Script](img/badge-script.svg)](https://fsprojects.github.io/FSharp.Formatting//apidocs.fsx)&emsp;
[![Notebook](img/badge-notebook.svg)](https://fsprojects.github.io/FSharp.Formatting//apidocs.ipynb)

# API Documentation Generation

The [command-line tool `fsdocs`](commandline.html) can be used to generate documentation
for F# libraries with XML comments.  The documentation is normally built using `fsdocs build` and developed using `fsdocs watch`. For
the former the output will be placed in `output\reference` by default.

## Selected projects

`fsdocs` automatically selects the projects and "cracks" the project files for information

* Projects with `GenerateDocumentationFile` and without `IsTestProject` are selected.

* If Projects use `TargetFrameworks` (not `TargetFramework`, singular) only the first target framework will be used to build the docs.

    <PropertyGroup>
      <GenerateDocumentationFile>true</GenerateDocumentationFile>
    </PropertyGroup>

## Templates

The HTML is built by instantiating a template. The template used is the first of:

* `docs/reference/_template.html`
  

* `docs/_template.html`
  

* The default template
  

Usually, the same template can be used as for [other content](content.html).

## Classic XML Doc Comments

XML Doc Comments may use [the normal F# and C# XML doc standards](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/).

The tags that form the core of the XML doc specification are:

```fsharp
<c>	<para>	<see>*	<value>
<code>	<param>*	<seealso>*
<example>	<paramref>	<summary>
<exception>*	<permission>*	<typeparam>*
<include>*	<remarks>	<typeparamref>
<list>	<inheritdoc>	<returns>

```

In addition, you may also use the [Recommended XML doc extensions for F# documentation tooling](https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1031-xmldoc-extensions.md).

* `<a href = "...">` links
  

* Arbitrary paragraph-level HTML such as `<b>` for bold in XML doc text
  

* `<namespacedoc>` giving documentation for the enclosing namespace
  

* `<exclude/>` to exclude from XML docs
  

* `<category>` to give a category for an entity or member. An optional `index` attribute can be specified
to help sort the list of categories.
  

* `\(...\)` for inline math and `$$...$$` and `\[...\]`for math environments, see [http://docs.mathjax.org.
Some](http://docs.mathjax.org.
Some) escaping of characters (e.g. `&lt;`, `&gt;`) may be needed to form valid XML
  

An example of an XML documentation comment, assuming the code is in the namespace `TheNamespace`:

```fsharp
/// <summary>
///   A module
/// </summary>
///
/// <namespacedoc>
///   <summary>A namespace to remember</summary>
///
///   <remarks>More on that</remarks>
/// </namespacedoc>
///
module SomeModule =
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
    /// <category>Foo</category>
    let someFunction x = 42 + x

/// <summary>
///   A type, see  <see cref="T:TheNamespace.SomeModule"/> and
///  <see cref="M:TheNamespace.SomeModule.someFunction"/>.
/// </summary>
///
type SomeType() =
    member x.P = 1
```

Like types, members are referred to by xml doc sig.  These must currently be precise as the F#
compiler doesn't elaborate these references from simpler names:

```fsharp
type Class2() =
    member this.Property = "more"
    member this.Method0() = "more"
    member this.Method1(c: string) = "more"
    member this.Method2(c: string, o: obj) = "more"

/// <see cref="P:TheNamespace.Class2.Property" />
/// and <see cref="M:TheNamespace.Class2.OtherMethod0" />
/// and <see cref="M:TheNamespace.Class2.Method1(System.String)" />
/// and <see cref="M:TheNamespace.Class2.Method2(System.String,System.Object)" />
let referringFunction1 () = "result"
```

Generic types are referred to by .NET compiled name, e.g.

```fsharp
type GenericClass2<'T>() =
    member this.Property = "more"

    member this.NonGenericMethod(_c: 'T) = "more"

    member this.GenericMethod(_c: 'T, _o: 'U) = "more"

/// See <see cref="T:TheNamespace.GenericClass2`1" />
/// and <see cref="P:TheNamespace.GenericClass2`1.Property" />
/// and <see cref="M:TheNamespace.GenericClass2`1.NonGenericMethod(`0)" />
/// and <see cref="M:TheNamespace.GenericClass2`1.GenericMethod``1(`0,``0)" />
let referringFunction2 () = "result"
```

### Cross-referencing with &lt;seealso&gt;

Use `<seealso cref="..."/>` within `<summary>` to create cross-references.

For example:

```fsharp
module Forest =

    /// <summary>
    /// Find at most <c>limit</c> foxes in current forest
    ///
    /// See also: <seealso cref="M:App.Forest.findSquirrels(System.Int32)"/>
    /// </summary>
    let findFoxes (limit : int) = []

    /// <summary>
    /// Find at most <c>limit</c> squirrels in current forest
    ///
    /// See also: <seealso cref="M:App.Forest.findFoxes(System.Int32)"/>
    /// </summary>
    let findSquirrels (limit : int) = []
```

You can find the correct value for `cref` in the generated `.xml` documentation file (this will be generated alongside the assembly's `.dll``).

You can also omit the `cref`'s arguments, and `fsdocs` will make an attempt to find the first member that matches.

For example:

```fsharp
    /// See also: <seealso cref="M:App.Forest.findSquirrels"/>

```

If the member cannot be found, a link to the containing module/type will be used instead.

### Classic XMl Doc Comments: Excluding APIs from the docs

If you want to exclude modules or functions from the API docs, you can use the `<exclude/>` tag.
It needs to be set on a separate triple-slashed line, and can either appear on its own or as part
of an existing `<summary>` (for example, you may wish to hide existing documentation while it's in progress).
The `<exclude/>` tag can be the first or last line in these cases.

Some examples:

```fsharp
/// <exclude/>
module BottleKids1 =
    let a = 42

// Ordinary comment
/// <exclude/>
module BottleKids2 =
    let a = 43

/// <exclude/>
/// BottleKids3 provides improvements over BottleKids2
module BottleKids3 =
    let a = 44

/// BottleKids4 implements several new features over BottleKids3
/// <exclude/>
module BottleKids4 =
    let a = 45

/// <exclude/>
/// <summary>
/// BottleKids5 is all you'll ever need in terms of bottles or kids.
/// </summary>
module BottleKids5 =
    let a = 46
```

Note that the comments for `BottleKids3` (and `BottleKids4`) will generate a warning. This is because
the `<exclude/>` tag will be parsed as part of the `summary` text, and so the documentation generator
can't be completely sure you meant to exclude the item, or whether it was a valid part of the documentation.
It will assume the exclusion was intended, but you may want to use explicit `<summary>` tags to remove
the warning.

The warning will be of the following format:

```fsharp
Warning: detected "<exclude/>" in text of "<summary>" for "M:YourLib.BottleKids4". Please see https://fsprojects.github.io/FSharp.Formatting/apidocs.html#Classic-XML-Doc-Comments

```

You will find that `[omit]` also works, but is considered part of the Markdown syntax and is
deprecated for XML Doc comments. This will also produce a warning, such as this:

```fsharp
The use of `[omit]` and other commands in XML comments is deprecated, please use XML extensions, see https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1031-xmldoc-extensions.md

```

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

You can use Markdown instead of XML in `///` comments. If you do, you should set `<UsesMarkdownComments>true</UsesMarkdownComments>` in
your F# project file.

> Note: Markdown Comments are not supported in all F# IDE tooling.
> 

### Adding cross-type links to modules and types in the same assembly

You can automatically add cross-type links to the documentation pages of other modules and types in the same assembly.
You can do this in two different ways:

* Add a [markdown inline link](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#links) were the link
title is the name of the type you want to link.
  

  ```fsharp
  /// This will generate a link to [Foo.Bar] documentation

  ```
  

* Add a [Markdown inline code](https://github.com/adam-p/markdown-here/wiki/Markdown-Cheatsheet#code) (using
back-ticks) where the code is the name of the type you want to link.
  

  ```fsharp
  /// This will also generate a link to `Foo.Bar` documentation

  ```
  

You can use either the full name (including namespace and module) or the simple name of a type.
If more than one type is found with the same name, the link will not be generated.
If a type with the given name is not found in the same assembly, the link will not be generated.

```fsharp
/// Contains two types [Bar] and [Foo.Baz]
module Foo =

    /// Bar is just an `int` and belongs to module [Foo]
    type Bar = int

    /// Baz contains a `Foo.Bar` as its `id`
    type Baz = { id: Bar }

    /// This function operates on `Baz` types.
    let f (b: Baz) = b.id * 42

/// Referencing [Foo3] will not generate a link as there is no type with the name `Foo3`
module Foo3 =

    /// This is not the same type as `Foo.Bar`
    type Bar = double

    /// Using the simple name for [Bar] will fail to create a link because the name is duplicated in
    /// [Foo.Bar] and in [Foo3.Bar]. In this case, using the full name works.
    let f2 b = b * 50
```

### Markdown Comments: Excluding APIs from the docs

If you want to exclude modules or functions from the API docs you can use the `[omit]` tag.
It needs to be set on a separate triple-slashed line, but it could be either the first or the last:

Example as last line:

```fsharp
/// Some actual comment
/// [omit]
module Bar =
    let a = 42
```

Example as the first line:

```fsharp
/// [omit]
/// Some actual comment
module Bar2 =
    let a = 42
```

## Building library documentation programmatically

You can build library documentation programmatically using the functionality
in the [ApiDocs](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-apidocs-apidocs.html) type. To do this, load the assembly and open the necessary namespaces:

```fsharp
#r "FSharp.Formatting.ApiDocs.dll"

open FSharp.Formatting.ApiDocs
open System.IO
```

For example the [ApiDocs.GenerateHtml](https://fsprojects.github.io/FSharp.Formatting/reference/fsharp-formatting-apidocs-apidocs.html#GenerateHtml) method:

```fsharp
let file = Path.Combine(root, "bin/YourLibrary.dll")

let input = ApiDocInput.FromFile(file)

ApiDocs.GenerateHtml(
    [ input ],
    output = Path.Combine(root, "output"),
    collectionName = "YourLibrary",
    template = Path.Combine(root, "templates", "template.html"),
    substitutions = []
)
```

### Adding extra dependencies

When building a library programmatically, you might require a reference to an additional assembly.
You can pass this using the `otherFlags` argument.

```fsharp
let projectAssembly = Path.Combine(root, "bin/X.dll")

let projectInput = ApiDocInput.FromFile(projectAssembly)

ApiDocs.GenerateHtml(
    [ projectInput ],
    output = Path.Combine(root, "output"),
    collectionName = "Project X",
    template = Path.Combine(root, "templates", "template.html"),
    substitutions = [],
    otherFlags = [ "-r:/root/ProjectY/bin/Debug/net6.0/Y.dll" ]
)
```

or use `libDirs` to include all assemblies from an entire folder.
Tip: A combination of `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` in the fsproj file and setting `libDirs` to the compilation output path leads to only one folder with all dependencies referenced.
This might be easier, especially for large projects with many dependencies.

```fsharp
ApiDocs.GenerateHtml(
    [ projectInput ],
    output = Path.Combine(root, "output"),
    collectionName = "Project X",
    template = Path.Combine(root, "templates", "template.html"),
    substitutions = [],
    libDirs = [ "ProjectX/bin/Debug/netstandard2.0" ]
)
```

## Rebasing Links

The `root` parameter is used for the base of page and image links in the generated documentation. By default, it is derived from the project's `<PackageProjectUrl>` property.

In some instances, you may wish to override the value for `root` (perhaps for local testing). To do this, you can use the command-line argument `--parameters root <base>`.

For example:

dotnet fsdocs build --output public/docs --parameters root ../


