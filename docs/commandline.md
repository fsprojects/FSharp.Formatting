F# Formatting: Command line tool
================================

To use F# Formatting tools via the command line, you can use the `fsdocs` dotnet tool.

    dotnet tool install FSharp.Formatting.CommandTool
    fsdocs [command] [options]

The build commands
----------------------------

The `fsdocs build`  command processes a directory containing a mix of Markdown documents `*.md` and F# Script files `*.fsx`
according to the rules of [Literate Programming](literate.html), and also generates API docs for projects
in the solution according to the rules of [API doc generation](metadata.html)

    [lang=text]
    fsdocs build

### Content

The expected structure for a docs directory is

    docs\**\*.md                          -- markdown with embedded code, converted to html and optionally tex/ipynb
    docs\**\*.fsx                         -- fsx scripts converted to html and optionally tex/ipynb
    docs\**\*                             -- other content, copied over
    docs\**\_template.html                -- specifies the default template for this directory and its contents
    docs\reference\_template.html         -- optionally specifies the default template for reference docs

The output goes in `output/` by default.  Typically a `--parameters` argument is needed for substitutions in the template, e.g.

The following substitutions are defined based on metadata that may be present in project files.
The first metadata value detected across project files is used, it is assumed these values will
be the same across all projects.

|  Susbtitution name     | .fsproj entry               |
|:-----------------------|:----------------------------|
|   {{project-name}}     | Name of .sln or containing directory |
|   {{root}}             | <PackageProjectUrl>         |
|   {{authors}}          | <Authors>                   |
|   {{repository-url}}   | <RepositoryUrl>             | 
|   {{package-license}}  | <PackageLicenseExpression>  | 
|   {{package-tags}}     | <PackageTags>               |
|   {{copyright}}        | <Copyright>                 |

### Options

  * `--projects` - The project files to process. Defaults to the packable projects in the solution in the current directory, else all packable projects.
  * `--input` - Input directory containing `*.fsx` and `*.md` files and other content, defaults to `docs`.
  * `--output` -  Output directory, defaults to `output`
  * `--template` -  Default template file for formatting. For HTML should contain `{{document}}` and `{{tooltips}}` tags.
  * `--nonPublic` -  Generate docs for non-public members
  * `--xmlComments` -  Generate docs assuming XML comments not markdown comments in source code
  * `--eval` - Use the default FsiEvaluator to actually evaluate code in documentation, defaults to `false`.
  * `--generateNotebooks` -  Include conversion from scripts to `ipynb`
  * `--parameters` -  A whitespace separated list of string pairs as extra text replacement patterns for the format template file.
  * `--noLineNumbers` -  Line number option, defaults to `true`.
  * `--help` -  Display the specific help message for `convert`.

The watch commands
----------------------------

The `fsdocs watch` command does the same as `fsdocs build` but in "watch" mode, waiting for changes.

    [lang=text]
    fsdocs watch

The same parameters are accepted.  Restarting may be necesssary on changes to project files.

The convert command
----------------------------

The `fsdocs convert` command processes a directory containing a mix of Markdown documents `*.md` and F# Script files `*.fsx`
according to the concept of [Literate Programming](literate.html).

    [lang=text]
    fsdocs convert --input docs/scripts --format latex --parameters "authors" "Tomas Petricek"

### Options

  * `--input` - Input directory containing `*.fsx` and `*.md` files. Required,
  * `--output` -  Output directory, defaults to `output`
  * `--template` -  Default template file for formatting. For HTML should contain `{{document}}` and `{{tooltips}}` tags.
  * `--format` -  Output format either `latex`, `html` or `ipynb`, defaults to `html`.
  * `--prefix` -  Prefix for formatting, defaults to `fs`.
  * `--compilerOptions` -  Compiler options passed when evaluating snippets.
  * `--noLineNumbers` -  Line number option, defaults to `true`.
  * `--references` -  Turn all indirect links into references, defaults to `false`.
  * `--eval` - Use the default FsiEvaluator to actually evaluate code in documentation, defaults to `false`.
  * `--parameters` -  A whitespace separated list of string pairs as text replacement patterns for the format template file.
  * `--includeSource` -  Include sourcecode in documentation for substitution as `{{source}}`, defaults to `false`.
  * `--help` -  Display the specific help message for `convert`.
  * `--waitForKey` -  Wait for key before exit.

The api command
--------------------

The `fsdocs api` command builds the [library documentation](http://fsprojects.github.io/FSharp.Formatting/metadata.html) by reading 
the meta-data from the `*.dll` files of the package and using the XML comments from matching `*.xml` files produced by the F# compiler.

    [lang=text]
    fsdocs api --dlls lib1.dll "lib 2.dll" --output "../api-docs" --template docs/reference/_template.html

### Required options

  * `--dlls` -  List of `dll` input files.
  * `--template` -  Template file for formatting, the file should contain `{{document}}` tag

### Other options

  * `--output` -  Output directory, defaults to `output`
  * `--parameters` -  Property settings for simple template instantiation.
  * `--xmlFile` -  Single XML file to use for all `dll` files, otherwise using `file.xml` for each `file.dll`.
  * `--sourceRepo` -  Source repository URL; silently ignored, if a source repository folder is not provided.
  * `--sourceFolder` -  Source repository folder; silently ignored, if a source repository URL is not provided.
  * `--nonPublic` -  Generate docs for non-public members
  * `--xmlComments` -  Generate docs assuming XML comments not markdown comments in source code
  * `--libDirs` - Search directory list for library references.
  * `--help` -  Display the specific help message for `apidocs`.
  * `--waitForKey` -  Wait for key before exit.

### Examples

For the example above:

1. The example commandline is executed in the working directory that contains the target files `lib1.dll` and `lib 2.dll` as well as the
corresponding meta-data files `lib1.xml` and `lib 2.xml`, which are the result of a previous build process of your project.

2. The output directory is in this example within the parent directory of your working directory.

3. The example assumes that the necessary `_template.html` file is present and contains the `{{document}}` tag
   for content substitution.
   
You can add further substitutions using the `--parameters` list. 

Tou can experiment with the [template file of the FSharp.Formatting project](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/reference/_template.html). 

<div></div>

    [lang=text]
    fsdocs api
      --dlls lib1.dll "lib 2.dll" 
      --output "../api-docs" 
      --template template.html
      --parameters
          "authors" "Your name(s)"
	      "github-link" "http://github.com/yourname/project"
          "project-name" "your project name"
	      "root" "http://yourname.github.io/project"
	  
	  
				   
