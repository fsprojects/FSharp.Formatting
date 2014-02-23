F# Formatting: Command line tool
================================

If you prefer to use F# Formatting tools via command line, you can use the
`FSharp.Formatting.CommandTool` package, which includes an executable `fsformatting.exe`
that gives you access to the most important functionality via a simple command line
interface. This might be a good idea if you prefer to run F# Formatting as a separate
process, e.g. for resource management reasons.

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The F# Formatting tool is available as <a href="https://nuget.org/packages/FSharp.Formatting.CommandTool">FSharp.Formatting.CommandTool on NuGet</a>.
      To install it, run the following command in the <a href="http://docs.nuget.org/docs/start-here/using-the-package-manager-console">Package Manager Console</a>:
      <pre>PM> Install-Package FSharp.Formatting.CommandTool</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Alternatively, you can download the [source as a ZIP file](https://github.com/tpetricek/FSharp.Formatting/zipball/master)
or download the [compiled binaries](https://github.com/tpetricek/FSharp.Formatting/archive/release.zip) as a ZIP.

Using the tool
--------------

The tool option syntax is similar to git or other popular command line tools.
The format of the command line interface is:

    [lang=text]
    fsformatting[.exe] --help 
    fsformatting[.exe] [command] [function] [options]

In order to provide consistency across different shell environments, the command line tool appears to be case-insensitive by matching against lower case strings.

The `[command]` directive maps to the corresponding library namespace:

* `literate` - Selects namespace `FSharp.Literate`
* `metadataFormat` - Selects namespace `FSharp.MetadataFormat`

Currently, the command line tools exposes the functions `ProcessDirectory` of namespace `FSharp.Literate`
(for literate programming using F#) and `Generate` of namespace `FSharp.MetadataFormat` (for generating
library documentation from XML comments). 

The `--help` option as single specifier displays the help message for all valid `[command] [function] [options]` combinations.

Literate programming command
----------------------------

The method `FSharp.Literate.ProcessDirectory` processes a directory containing a mix of Markdown documents `*.md` and F# Script files `*.fsx`
according to the concept of [Literate Programming](literate.html).

    [lang=text]
    fsformatting[.exe] literate --processDirectory [options]

### Required options

  * `--inputDirectory` - Input directory containing `*.fsx` and `*.md` files.

### Other Options

  * `--templateFile` -  Template file for formatting.
  * `--outputDirectory` -  Output directory, defaults to input directory.
  * `--format` -  Output format either `latex` or `html`, defaults to `html`.
  * `--prefix` -  Prefix for formatting, defaults to `fs`.
  * `--compilerOptions` -  Compiler Options.
  * `--lineNumbers` -  Line number option, defaults to `true`.
  * `--references` -  Turn all indirect links into references, defaults to `false`.
  * `--fsieval` - Use the default FsiEvaluator, defaults to `false`.
  * `--replacements` -  A whitespace separated list of string pairs as text replacement patterns for the format template file.
  * `--includeSource` -  Include sourcecode in documentation, defaults to `false`.
  * `--layoutRoots` -  Search directory list for the Razor Engine.
  * `--help` -  Display the specific help message for `literate --processDirectory`.
  * `--waitForKey` -  Wait for key before exit.

### Example

    [lang=text]
    fsformatting literate 
      --processDirectory --templateFile template-project.html 
      --format latex --replacements "page-author" "Tomas Petricek"

Library documentation command
-----------------------------

The `FSharp.MetadataFormat.Generate` method builds the [library documentation](http://tpetricek.github.io/FSharp.Formatting/metadata.html) by reading 
the meta-data from a `*.dll` files of the package and using the XML comments from matching `*.xml` files produced by the F# compiler.

    [lang=text]
    fsformatting[.exe] metadataFormat --generate [options]

### Required options

  * `--dllFiles` -  List of `dll` input files.
  * `--outDir` -  Output directory.
  * `--layoutRoots` -  Search directory list for the Razor Engine templates.

### Other options

  * `--parameters` -  Property settings for the Razor Engine.
  * `--namespaceTemplate` -  Namespace template file for formatting, defaults to `namespaces.cshtml`.
  * `--moduleTemplate` -  Module template file for formatting, defaults to `module.cshtml`.
  * `--typeTemplate` -  Type template file for formatting, defaults to `type.cshtml`.
  * `--xmlFile` -  Single XML file to use for all `dll` files, otherwise using `file.xml` for each `file.dll`.
  * `--sourceRepo` -  Source repository URL; silently ignored, if a source repository folder is not provided.
  * `--sourceFolder` -  Source repository folder; silently ignored, if a source repository URL is not provided.
  * `--libDirs` - Search directory list for library references.
  * `--help` -  Display the specific help message for `metadataFormat --generate`.
  * `--waitForKey` -  Wait for key before exit.

### Example

    [lang=text]
    fsformatting metadataFormat 
      --generate --dllFiles lib1.dll "lib 2.dll" 
      --outDir "../api-docs" --layoutRoots templates
