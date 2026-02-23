(**
---
category: Documentation
categoryindex: 1
index: 6
---
*)
(*** condition: prepare ***)
#nowarn "211"
#I "../src/FSharp.Formatting/bin/Release/netstandard2.1"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Formatting.Markdown.dll"
#r "FSharp.Formatting.CodeFormat.dll"
#r "FSharp.Formatting.Literate.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Formatting,{{fsdocs-package-version}}"
#endif // FSX
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Formatting,{{fsdocs-package-version}}"
#endif // IPYNB


(**
[![Binder](img/badge-binder.svg)](https://mybinder.org/v2/gh/fsprojects/fsharp.formatting/gh-pages?filepath={{fsdocs-source-basename}}.ipynb)&emsp;
[![Script](img/badge-script.svg)]({{root}}/{{fsdocs-source-basename}}.fsx)&emsp;
[![Notebook](img/badge-notebook.svg)]({{root}}/{{fsdocs-source-basename}}.ipynb)

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

## Literate Scripts and Markdown Content

The input directory may contain [literate scripts](literate.html) and markdown content.

## Other Content

Content that is not `*.fsx` or `*.md` is copied across.

## Default Styling Content

By default additional content such as `fsdocs-search.js`, `fsdocs-tips.js` and `fsdocs-default.css` are included in 
the `content` directory of the output.  This can be suppressed with `--nodefaultcontent` or by having your own
copy of this content in your `content` directory.

## Ignored Content

Any file or directory beginning with `.` is ignored.

## Front matter

Each content file can have an optional frontmatter.  This determines the navigation bar title, categorization ordering and meta tags.

For markdown, the format is:
```
---
title: Some Title
category: Some Category
categoryindex: 2
index: 3
description: Some description
keywords: tag1, tag2, tag3
---
```
For F# scripts the frontmatter is in this form:

    (**
    ---
    title: A Literate Script
    category: Examples
    categoryindex: 2
    index: 1
    description: Some description
    keywords: tag1, tag2, tag3
    ---
    *)

All entries are optional.
The `categoryindex` determines the ordering of categories.
The `index` determines the ordering of within each category.
The `title` is used in the navigation bar instead of any title inferred from the document.
The `description` is used in `<meta name="description"` as part of the `{{fsdocs-meta-tags}}` substitution.
The `keywords` are also used in a meta tag as part of `{{fsdocs-meta-tags}}`. Separate them using a `,`.

## Link Translation for Inputs

If an input is used in markdown as a target of a markdown direct link, then that is replaced by the output file. For example:

    [Some Text](some-file.md)

becomes

    [Some Text](some-file.html)

if `some-file.md` is one of the inputs.

## Multi-language Content

Versions of content in other languages should go in two-letter coded sub-directories, e.g.

    docs/ja/...
    docs/de/...

These will be elided from the main table-of-contents and search indexes.  (Currently no language-specific
table of contents is built, nor language-specific site search indexes).

## Templates and Substitutions

Templates are used for HTML (`_template.html`), LaTeX (`_template.tex`), Notebooks (`_template.ipynb)`
and F# script outputs (`_template.fsx`).

The following substitutions determine the primary (non-styling) content of your site.
For example `{{fsdocs-content}}` is replaced with the generated content in each file.

Substitutions are applied when generating content from HTML templates, IPYNB templates, FSX templates.
They are also applied to content apart from Markdown inline code `` `...` ``, Markdown LaTeX and
generated outputs.

See [Styling](styling.html) for information about template parameters and styling beyond the default template.

|  Substitution name            | Generated content |
|:------------------------------|:--------------------------------------------------------------|
| `root`                        | `<PackageProjectUrl>` else `/` followed by `fsdocs-collection-name`. |
| `fsdocs-collection-name`      | Name of .sln, single .fsproj or containing directory          |
| `fsdocs-content`              | Main page content                                             |
| `fsdocs-list-of-namespaces`   | HTML `<li>` list of namespaces with links                     |
| `fsdocs-list-of-documents`    | HTML `<li>` list of documents with  titles and links          |
| `fsdocs-page-title`           | First h1 heading in literate file. Generated for API docs     |
| `fsdocs-source`               | Original literate script or markdown source                   |
| `fsdocs-source-filename`      | Name of original input source, relative to the `docs` root           |
| `fsdocs-source-basename`      | Name of original input source, excluding its extensions, relative to the `docs` root  |
| `fsdocs-tooltips`             | Generated hidden div elements for tooltips                    |
| `fsdocs-watch-script`         | The websocket script used in watch mode to trigger hot reload |
| `fsdocs-previous-page-link`   | A relative link to the previous page based on the frontmatter index data |
| `fsdocs-next-page-link`       | A relative link to the next page based on the frontmatter index data |
| `fsdocs-head-extra`           | Additional html content loaded from the `_head.html` file if present in the `--input` folder |
| `fsdocs-body-extra`           | Additional html content loaded from the `_body.html` file if present in the `--input` folder |
| `fsdocs-body-class`           | A css class value to help distinguish between `content` and `api-docs` |
| `fsdocs-meta-tags`            | A set of additional HTML meta tags, present when description and/or keywords are present in the frontmatter |

The following substitutions are extracted from your project files and may or may not be used by the default
template:

|  Substitution name                   | Value                          |
|:-------------------------------------|:-------------------------------|
| `fsdocs-copyright`                   | `<Copyright>`                  |
| `fsdocs-package-project-url`         | `<PackageProjectUrl>`          |
| `fsdocs-package-license-expression`  | `<PackageLicenseExpression>`   |
| `fsdocs-package-tags`                | `<PackageTags>`                |
| `fsdocs-package-version`             | `<Version>`                    |

For the `fsdocs` tool, additional substitutions can be specified using `--parameters`.

## Cross References to API Docs

Markdown content can contain cross-references to API Docs.  Use inline
markdown code snippets of the special form `` `cref:T:MyNamespace.MyType` `` where `T:MyNamespace.MyType`
is a method, property or type xml doc sig reference, see [API Docs](apidocs.html).
This can include any cross-references resolved by fsdocs.

The generated API documentation includes buttons to copy the XML and Markdown forms of API doc references.

For example, within this project,

- the text `` `cref:T:FSharp.Formatting.Markdown.MarkdownParagraph` `` resolves to the link `cref:T:FSharp.Formatting.Markdown.MarkdownParagraph`

- the text ``` `cref:T:System.Console` ``` resolves to the link `cref:T:System.Console`

- the text ``` `cref:M:System.Console.WriteLine` ``` resolves to the link `cref:M:System.Console.WriteLine`

- the text ``` `cref:M:System.Console.WriteLine(System.String)` ``` resolves to the link `cref:M:System.Console.WriteLine(System.String)`

- the text ``` ``cref:T:FSharp.Control.FSharpAsync`1`` ``` resolves to the link ``cref:T:FSharp.Control.FSharpAsync`1``

- the text ``` `cref:T:FSharp.Control.FSharpAsync` ``` resolves to the link `cref:T:FSharp.Control.FSharpAsync`

- the text ``` ``cref:T:FSharp.Core.array`1`` ``` resolves to the link ``cref:T:FSharp.Core.array`1``

- the text ``` `cref:T:FSharp.Core.OptionModule` ``` resolves to the link `cref:T:FSharp.Core.OptionModule`

- the text ```` ```cref:M:FSharp.Collections.ListModule.Append``1``` ```` resolves to the link ```cref:M:FSharp.Collections.ListModule.Append``1```

> NOTE: These cases act as tests - if the links above do not work, then that indicates a bug or a change in the
> external link. [Please report it](https://github.com/fsprojects/FSharp.Formatting/issues/new).

Determining xmldoc sig references is not simple.  The API doc generated pages come with
buttons to copy out the XmlDoc signature.

## Generating HTML Output

HTML is generated by default. You can also add a `_template.html`.  This should contain `{{fsdocs-content}}`,  `{{fsdocs-tooltips}}`
and other placeholders. Substitutions are
applied to this template.
If a file `_template.html` exists, then it's used as the template for HTML generation for that directory and all sub-content.

## Generating LaTeX output

To generate .tex output for each script and markdown file, add a `_template.tex`. Substitutions are
applied to this template. The file is either empty or contains `{{fsdocs-content}}` as the key where the body
of the document is placed.

## Generating iPython Notebook output

To generate .ipynb output for each script and markdown file, add a `_template.ipynb`, usually empty. Substitutions are
applied to this template.

To add a `mybinder` badge to your generated notebook, ensure you have a `Dockerfile` and `NuGet.config`
in your `docs` directory and use text like this:

    [![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=literate.ipynb)

## Generating Script outputs

To generate .fsx output for each script and markdown file, add a `_template.fsx`, usually empty. Substitutions are
applied to this template. It is either empty or contains `{{fsdocs-content}}` as the key where the body
of the script is placed.

*)
