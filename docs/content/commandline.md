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

Examples and configuration considerations
-----------------------------------------

### Basic Example

According to the previous section, a minimum configuration for generating the library documentation from `*.dll` files
could be configured by: 

    [lang=text]
    fsformatting metadataFormat 
      --generate 
	  --dllFiles lib1.dll "lib 2.dll" 
      --outDir "../api-docs" 
	  --layoutRoots templates

The following underlying configuration assumptions need to be considered when you adapt this example to your own project:

1. The commandline tool is properly installed in a directory of your system´s binaries search path. 
If you decide to call the tool from within the intstallation directory or by using an absolute path, you will have to take the  
path resolution implications of your platform (OS and shell) for the other tool options into account.
After a clean install, the tool´s directory contains all necessary FSharp.Formatting library files, i.e. there would not be unresolved dependencies. 
The installation of the commandline tool only [via NuGet](https://nuget.org/packages/FSharp.Formatting.CommandTool) is sufficient for this purpose. 

	* If you have other requirements which demand the installation of the full FSharp.Formatting package, you should consider the alternative 
	configuration options below.
	* If you are using [FAKE - F# Make](http://fsharp.github.io/FAKE/), a separate installation of the commandline tool is not recommended 
	as FAKE already contains this tool, see further recommendations below.
	 
2. On a Mono platform, the commandline need to be prefixed by the appropriate `mono` command that invokes the necessary .Net v4.0 environment for FSharp.Formatting on your platform. 

3. The example commandline is executed in the working directory that contains the target files `lib1.dll` and `lib 2.dll` as well as the
corresponding meta-data files `lib1.xml` and `lib 2.xml`, which are the result of a previous build process of your project.

4. The output directory is in this example within the parent directory of your working directory.

5. The example assumes that the necessary `template.html` file (and in case, those files it draws in as dependencies) reside in the subdirectory `templates` of your working directory. 
It is implicitely also assumed that this template does not contain substitution parameters. If you want to use this feature, you need to add the desired parameter list. 
For example, if you want to experiment with the [template file of the FSharp.Formatting project](https://github.com/tpetricek/FSharp.Formatting/blob/master/docs/tools/template.html), 
you copy this file into the subdirectory `templates` of your working directory and specify the 
[necessary substitution parameters](https://github.com/tpetricek/FSharp.Formatting/blob/master/docs/tools/generate.fsx#L24) as follows:

<table class="pre"><tr><td><pre lang="text">fsformatting metadataFormat 
  --generate 
  --dllFiles lib1.dll &quot;lib 2.dll&quot; 
  --outDir &quot;../api-docs&quot; 
  --layoutRoots templates
  --parameters &quot;page-author&quot; &quot;Your name(s)&quot;
               &quot;page-description&quot; &quot;A package for ...&quot;
	           &quot;github-link&quot; &quot;http://github.com/yourname/project&quot;
               &quot;project-name&quot; &quot;your project name&quot;
	           &quot;root&quot; &quot;http://yourname.github.io/project&quot;</pre></td></tr></table>	  
	  
	  
Note: depending on the quote evaluation scheme of your OS and shell, you may encounter unexpected errors due to misinterpretation of the string parameters.				   
				   
### Alternative configuration options

Instead of using the commandline tool, you may want to consider to use the [F# interpreter `#!` compatibility](https://visualfsharp.codeplex.com/workitem/25) coming with F# 3.1.2.
This feature allows you on posix systems to execute F# scripts `*.fsx` directly from a posix shell. In order to use this feature e.g. on a typical OS X or Linux system, you add as first line, at first position in your `fsx` file the F# interpreter invocation command `#!/usr/bin/env fsharpi --exec`. Alternatively, you could use the direct call to `fsi.exe`, which is part of the standard F# installation, to run your `generate.fsx` file on Windows via `fsi --exec generate.fsx` and on Mono platforms with `mono fsi.exe --exec generate.fsx` (provided proper path settings).

As template for the `generate.fsx` file, you should refer to the recommended [template file](https://github.com/fsprojects/ProjectScaffold/blob/master/docs/tools/generate.template), that should be easily adapted to your project´s needs. The tradeoff in both cases is that instead of installating the FSharp.Formatting commandline tool, you have to install F# and the FSharp.Formatting library on your system. As the commandline tool contains the FSharp.Formatting library and the Fsharp compiler service, the overhead is much less than it might look at first glance.

If you already use [FAKE](http://fsharp.github.io) in your project, it is recommended to apply the predefined [FAKE commands](http://fsharp.github.io/FAKE/apidocs/fake-fsharpformatting.html) 
for the document creation process. FAKE will install the FSharp.Formatting commandline tool as a dependency. Hence, a separate installation should be omitted in order to avoid 
a cluttered environment on your system. Alternatively, you can also use a [FAKE target definition](https://github.com/tpetricek/FSharp.Formatting/blob/master/build.fsx#L176), 
that invokes the recommended [template file](https://github.com/fsprojects/ProjectScaffold/blob/master/docs/tools/generate.template) as in the previous configuration option.
