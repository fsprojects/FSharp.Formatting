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


(**
[![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=literate.ipynb)

F# Formatting: Content
===================================

The [command-line tool `fsdocs`](commandline.html) allows documentation for a site to be built
from content in a `docs` directory. The expected structure for a `docs` directory is

    docs/**/*.md                  -- markdown with embedded code, converted to html and optionally tex/ipynb
    docs/**/*.fsx                 -- fsx scripts converted to html and optionally tex/ipynb
    docs/**/*                     -- other content, copied over
    docs/**/_template.html        -- specifies the default HTML template for this directory and its contents
    docs/**/_template.tex         -- specifies Latex files should also be generated
    docs/**/_template.ipynb       -- specifies F# ipynb files should also be generated
    docs/**/_template.fsx         -- specifies F# fsx files should also be generated (even from markdown)
    docs/reference/_template.html -- optionally specifies the default template for reference docs

Processing is by these two commands:

    dotnet fsdocs build
    dotnet fsdocs watch

The output goes in `output/` by default.  Typically a `--parameters` argument is needed for substitutions in the template, e.g.
Processing is recursive, making this call a form of static site generation.

- Content that is not `*.fsx` or `*.md` is copied across 

- If a file `_template.html` exists then is used as the template for that directory and all sub-content.

- Any file or directory beginning with `.` is ignored.

- A set of parameter substitutions can be provided operative across all files.

## Literate Scripts and Markdown

The input may contain [literate scripts and markdown](literate.html).

## Templates

Template files are as follows:

- `_template.html` - absent, empty or contain `{{document}}` and `{{tooltips}}` placeholders.
- `_template.tex` - absent, empty or contain `{content}` placeholder.
- `_template.ipynb` - absent, empty or contain `{{cells}}` placeholder.
- `_template.fsx` - absent, empty or contain `{{code}}` placeholder.

For HTML, if no template is provided, the result is the HTML body
of the document with HTML for tool tips appended to the end.
The template should include two parameters that will be replaced with the actual
HTML: `{{document}}` will be replaced with the formatted document;
`{{tooltips}}` will be replaced with (hidden) `<div>` elements containing code for tool tips that appear
when you place mouse pointer over an identifier. Optionally, you can also use 
`{{page-title}}` which will be replaced with the text in a first-level heading.
The template should also reference `fsdocs-style.css` and `fsdocs-tips.js` that define CSS style
and JavaScript functions used by the generated HTML (see sample [stylesheet](https://github.com/fsprojects/FSharp.Formatting/blob/master/src/FSharp.Formatting.CodeFormat/files/fsdocs-style.css)
and [script](https://github.com/fsprojects/FSharp.Formatting/blob/master/src/FSharp.Formatting.CodeFormat/files/fsdocs-tips.js) on GitHub).

For Latex, the the `_template.tex` file is either empty of contains `{content}` as the key where the body
of the document is placed. 

You can experiment with the [template file of this project](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/_template.html). 

The following substitutions are defined based on metadata that may be present in project files.
The first metadata value detected across project files is used, it is assumed these values will
be the same across all projects.

|  Substitution name     | Source               |
|:-----------------------|:----------------------------|
|   {{page-title}}       | First h1 heading in literate file, generated for API docs  |
|   {{project-name}}     | Name of .sln or containing directory |
|   {{root}}             | `<PackageProjectUrl>`         |
|   {{authors}}          | `<Authors>`                   |
|   {{repository-url}}   | `<RepositoryUrl>`             |
|   {{package-project-url}}  | `<PackageProjectUrl>`  |
|   {{package-license}}  | `<PackageLicenseExpression>`  |
|   {{package-tags}}     | `<PackageTags>`               |
|   {{copyright}}        | `<Copyright>`                 |
|   {{document}}         | generated html contents       |
|   {contents}           | generated latex contents       |
|   {{cells}}            | generated ipynb contents       |
|   {{tooltips}}         | generated html tooltips contents       |
|   {{source}}           | original script source           |

## Generating LaTeX output

To generate .tex output for each script and markdown file, add a `_template.tex`.


## Generating iPython Notebook output

To generate .ipynb output for each script and markdown file, add a `_template.ipynb`, usually empty.

To add a `mybinder` badge to your generated notebook, ensure you have a `Dockerfile` and `NuGet.config`
in your `docs` directory and use text like this:

    [![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=literate.ipynb)

### Generating Script outputs

To generate .fsx output for each script and markdown file, add a `_template.fsx`, usually empty.

*)
