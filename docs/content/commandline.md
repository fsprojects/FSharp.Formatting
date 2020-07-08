F# Formatting: Command line tool
================================

To use F# Formatting tools via the command line, you can use the
`fsformatting` dotnet tool.

    dotnet tool install FSharp.Formatting.CommandTool

The format of the command line interface is as follows. Command line arguments are case-sensitive.

    [lang=text]
    fsformatting --help 
    fsformatting [command] [function] [options]


The convert command
----------------------------

The `convert` command processes a directory containing a mix of Markdown documents `*.md` and F# Script files `*.fsx`
according to the concept of [Literate Programming](literate.html).

    [lang=text]
    fsformatting convert --template template.html --format latex --parameters "page-author" "Tomas Petricek"

### Required options

  * `--input` - Input directory containing `*.fsx` and `*.md` files.

### Other Options

  * `--output` -  Output directory, defaults to input directory.
  * `--template` -  Template file for formatting. For HTML should contain `{{document}}` and `{{tooltips}}` tags.
  * `--format` -  Output format either `latex`, `html` or `ipynb`, defaults to `html`.
  * `--prefix` -  Prefix for formatting, defaults to `fs`.
  * `--compilerOptions` -  Compiler options passed when evaluating snippets.
  * `--lineNumbers` -  Line number option, defaults to `true`.
  * `--references` -  Turn all indirect links into references, defaults to `false`.
  * `--fsieval` - Use the default FsiEvaluator, defaults to `false`.
  * `--parameters` -  A whitespace separated list of string pairs as text replacement patterns for the format template file.
  * `--includeSource` -  Include sourcecode in documentation, defaults to `false`.
  * `--help` -  Display the specific help message for `convert`.
  * `--waitForKey` -  Wait for key before exit.

The generate command
--------------------

The `generate` command builds the [library documentation](http://fsprojects.github.io/FSharp.Formatting/metadata.html) by reading 
the meta-data from the `*.dll` files of the package and using the XML comments from matching `*.xml` files produced by the F# compiler.

    [lang=text]
    fsformatting generate --dlls lib1.dll "lib 2.dll" --output "../api-docs" --template docs/reference/template.html

### Required options

  * `--dlls` -  List of `dll` input files.
  * `--output` -  Output directory.
  * `--template` -  Template file for formatting, should containt `{{document}}` tag

### Other options

  * `--parameters` -  Property settings for simple template instantiation.
  * `--xmlFile` -  Single XML file to use for all `dll` files, otherwise using `file.xml` for each `file.dll`.
  * `--sourceRepo` -  Source repository URL; silently ignored, if a source repository folder is not provided.
  * `--sourceFolder` -  Source repository folder; silently ignored, if a source repository URL is not provided.
  * `--libDirs` - Search directory list for library references.
  * `--help` -  Display the specific help message for `generate`.
  * `--waitForKey` -  Wait for key before exit.

### Examples

For the example above:

1. The example commandline is executed in the working directory that contains the target files `lib1.dll` and `lib 2.dll` as well as the
corresponding meta-data files `lib1.xml` and `lib 2.xml`, which are the result of a previous build process of your project.

2. The output directory is in this example within the parent directory of your working directory.

3. The example assumes that the necessary `template.html` file is present and contains the `{{document}}` tag
   for substitutions.
   
You can add further substitutions using the `--parameters` list. 
For example, you can experiment with the [template file of the FSharp.Formatting project](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/tools/reference/template.html). 

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
	  
	  
				   
