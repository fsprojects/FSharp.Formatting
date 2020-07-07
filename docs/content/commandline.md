F# Formatting: Command line tool
================================

If you prefer to use F# Formatting tools via the command line, you can use the
`FSharp.Formatting.CommandTool` dotnet tool.

Using the tool
--------------

    dotnet tool install FSharp.Formatting.CommandTool

The format of the command line interface is:

    [lang=text]
    fsformatting --help 
    fsformatting [command] [function] [options]

The command line arguments are case-sensitive.

`convert` command
----------------------------

The `convert` command processes a directory containing a mix of Markdown documents `*.md` and F# Script files `*.fsx`
according to the concept of [Literate Programming](literate.html).

    [lang=text]
    fsformatting[.exe] convert [options]

### Required options

  * `--input` - Input directory containing `*.fsx` and `*.md` files.

### Other Options

  * `--output` -  Output directory, defaults to input directory.
  * `--template` -  Template file for formatting (optional)
  * `--format` -  Output format either `latex`, `html` or `ipynb`, defaults to `html`.
  * `--prefix` -  Prefix for formatting, defaults to `fs`.
  * `--compilerOptions` -  Compiler Options.
  * `--lineNumbers` -  Line number option, defaults to `true`.
  * `--references` -  Turn all indirect links into references, defaults to `false`.
  * `--fsieval` - Use the default FsiEvaluator, defaults to `false`.
  * `--replacements` -  A whitespace separated list of string pairs as text replacement patterns for the format template file.
  * `--includeSource` -  Include sourcecode in documentation, defaults to `false`.
  * `--help` -  Display the specific help message for `convert`.
  * `--waitForKey` -  Wait for key before exit.

### Example

    [lang=text]
    fsformatting convert --template template-project.html --format latex --replacements "page-author" "Tomas Petricek"

`generate` command
-----------------------------

The `generate` command builds the [library documentation](http://fsprojects.github.io/FSharp.Formatting/metadata.html) by reading 
the meta-data from the `*.dll` files of the package and using the XML comments from matching `*.xml` files produced by the F# compiler.

    [lang=text]
    fsformatting[.exe] generate [options]

### Required options

  * `--dlls` -  List of `dll` input files.
  * `--output` -  Output directory.

### Other options

  * `--parameters` -  Property settings for simple template instantiation.
  * `--namespaceTemplate` -  Namespace template file for formatting, defaults to `namespaces.html`.
  * `--moduleTemplate` -  Module template file for formatting, defaults to `module.html`.
  * `--typeTemplate` -  Type template file for formatting, defaults to `type.html`.
  * `--xmlFile` -  Single XML file to use for all `dll` files, otherwise using `file.xml` for each `file.dll`.
  * `--sourceRepo` -  Source repository URL; silently ignored, if a source repository folder is not provided.
  * `--sourceFolder` -  Source repository folder; silently ignored, if a source repository URL is not provided.
  * `--libDirs` - Search directory list for library references.
  * `--help` -  Display the specific help message for `generate`.
  * `--waitForKey` -  Wait for key before exit.

Examples and configuration considerations
-----------------------------------------

### Basic Example

According to the previous section, a minimum configuration for generating the library documentation from `*.dll` files
could be configured by: 

    [lang=text]
    fsformatting generate --generate --dlls lib1.dll "lib 2.dll" --output "../api-docs"

The following underlying configuration assumptions need to be considered when you adapt this example to your own project:

1. The example commandline is executed in the working directory that contains the target files `lib1.dll` and `lib 2.dll` as well as the
corresponding meta-data files `lib1.xml` and `lib 2.xml`, which are the result of a previous build process of your project.

2. The output directory is in this example within the parent directory of your working directory.

3. The example assumes that the necessary `template.html` file (and in case, those files it draws in as dependencies) reside in the subdirectory `templates` of your working directory. 
It is implicitely also assumed that this template does not contain substitution parameters. If you want to use this feature, you need to add the desired parameter list. 
For example, if you want to experiment with the [template file of the FSharp.Formatting project](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/tools/template.html), 
you copy this file into the subdirectory `templates` of your working directory and specify the 
[necessary substitution parameters](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/tools/generate.fsx#L24) as follows:

<div></div>

    [lang=text]
    fsformatting generate
      --dlls lib1.dll "lib 2.dll" 
      --output "../api-docs" 
      --template template.html
      --parameters
          "page-author" "Your name(s)"
          "page-description" "A package for ..."
	      "github-link" "http://github.com/yourname/project"
          "project-name" "your project name"
	      "root" "http://yourname.github.io/project"
	  
	  
Note: depending on the quote evaluation scheme of your OS and shell, you may encounter unexpected errors due to misinterpretation of the string parameters.				   
				   
