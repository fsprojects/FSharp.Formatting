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

Creating Content
===================================

The ["fsdocs" tool](commandline.html) allows documentation for a site to be built
from content in a `docs` directory. The expected structure for a `docs` directory is

    [lang=text]
    docs/**/*.md                  -- markdown with embedded code, converted to html and optionally tex/ipynb
    docs/**/*.fsx                 -- fsx scripts converted to html and optionally tex/ipynb
    docs/**/*                     -- other content, copied over
    docs/**/_template.html        -- optional template, specifies the HTML template for this directory and its contents
    docs/**/_template.tex         -- optionally indicates Latex files should be generated
    docs/**/_template.ipynb       -- optionally indicates F# ipynb files should be generated
    docs/**/_template.fsx         -- optionally indicates F# fsx files should be generated (even from markdown)
    docs/reference/_template.html -- optionally specifies the default template for reference docs

Processing is by these two commands:

    dotnet fsdocs build
    dotnet fsdocs watch

The output goes in `output/` by default.  Processing is recursive, making this a form of static site generation.

## Literate Scripts and Markdown

The input directory may contain [literate scripts and markdown](literate.html).

## Other Content

Content that is not `*.fsx` or `*.md` is copied across.

## Default Styling Content

By default additional content such as `fsdocs-search.js`, `fsdocs-tips.js` and `fsdocs-styles.css` are included in the
the `content` directory of the output.  This can be suppressed with `--nodefaultcontent` or by having your own
copy of this content in your `content` directory.

## Ignored Content

Any file or directory beginning with `.` is ignored.

## Templates

Template files are as follows:

- `_template.html` - absent, empty or contain `{{fsdocs-content}}` and `{{fsdocs-tooltips}}` placeholders.
- `_template.tex` - absent, empty or contain `{fsdocs-content}` placeholder.
- `_template.ipynb` - absent, empty or contain `{{fsdocs-content}}` placeholder.
- `_template.fsx` - absent, empty or contain `{{fsdocs-content}}` placeholder.

For example. if a file `_template.html` exists then is used as the template for HTML generation for that directory and all sub-content.
Otherwise the default template from the nuget package is used.

For HTML, if no template is provided, the result is the HTML body
of the document with HTML for tool tips appended to the end.
The template should include two parameters that will be replaced with the actual
HTML: `{{fsdocs-content}}` will be replaced with the formatted document;
`{{fsdocs-tooltips}}` will be replaced with (hidden) `<div>` elements containing code for tool tips that appear
when you place mouse pointer over an identifier. Optionally, you can also use 
`{{fsdocs-page-title}}` which will be replaced with the text in a first-level heading.
The template should also reference `fsdocs-style.css` and `fsdocs-tips.js` that define CSS style
and JavaScript functions used by the generated HTML (see sample [stylesheet](https://github.com/fsprojects/FSharp.Formatting/blob/master/src/FSharp.Formatting.CodeFormat/files/fsdocs-style.css)
and [script](https://github.com/fsprojects/FSharp.Formatting/blob/master/src/FSharp.Formatting.CodeFormat/files/fsdocs-tips.js) on GitHub).

You can experiment with the [template file of this project](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/_template.html). 

The following substitutions are defined based on metadata that may be present in project files.
The first metadata value detected across project files is used, it is assumed these values will
be the same across all projects.

|  Substitution name                  | Value (if not overriden by --parameters)                      |  
|:------------------------------------|:--------------------------------------------------------------|
| {{root}}                        | /                                                           | 
| {{fsdocs-authors}}              | `<Authors>`                                                   | 
| {{fsdocs-collection-name}}      | Name of .sln, single .fsproj or containing directory          | 
| {{fsdocs-collection-name-link}} | `<FsDocsCollectionNameLink>` else `<PackageProjectUrl>`       | 
| {{fsdocs-copyright}}            | `<Copyright>`                                                 | 
| {{fsdocs-content}}              | generated html contents                                       | 
| {{fsdocs-list-of-namespaces}}   | HTML `<li>` list of namespaces with links                     | 
| {{fsdocs-list-of-documents}}    | HTML `<li>` list of documents with  titles and links          | 
| {{fsdocs-logo-src}}             | `<FsDocsLogoSource>` else {{root}}/img/logo.png             | 
| {{fsdocs-logo-link}}            | `<FsDocsLogoLink>` else `<PackageProjectUrl>`                 | 
| {{fsdocs-license-link}}         | `<FsDocsLicenseLink>` else `<PackageProjectUrl>`/blob/master/LICENSE.md          | 
| {{fsdocs-navbar-position}}      | `fixed-left` or `fixed-right` (default ``fixed-right``)       | 
| {{fsdocs-package-project-url}}  | `<PackageProjectUrl>`                                         | 
| {{fsdocs-package-license-expression}}  | `<PackageLicenseExpression>`                           | 
| {{fsdocs-package-tags}}         | `<PackageTags>`                                               | 
| {{fsdocs-package-version}}      | `<Version>`                                                   | 
| {{fsdocs-page-title}}           | First h1 heading in literate file, generated for API docs     | 
| {{fsdocs-release-notes-link}}   | `<FsDocsReleaseNotesLink>` else `<PackageProjectUrl>`/blob/master/RELEASE_NOTES.md  | 
| {{fsdocs-source}}               | original script source                                        | 
| {{fsdocs-tooltips}}             | generated html tooltips contents                              | 
| {{fsdocs-repository-link}}      | `<RepositoryUrl>`                                             | 

## Generating LaTeX output

For Latex, the the `_template.tex` file is either empty of contains `{content}` as the key where the body
of the document is placed. 

To generate .tex output for each script and markdown file, add a `_template.tex`.
It may contain `{{fsdocs-content}}`. 

## Generating iPython Notebook output

To generate .ipynb output for each script and markdown file, add a `_template.ipynb`, usually empty.

To add a `mybinder` badge to your generated notebook, ensure you have a `Dockerfile` and `NuGet.config`
in your `docs` directory and use text like this:

    [![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=literate.ipynb)
    
## Generating Script outputs

To generate .fsx output for each script and markdown file, add a `_template.fsx`, usually empty.
It may contain `{{fsdocs-content}}`.

*)
