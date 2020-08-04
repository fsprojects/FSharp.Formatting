# Styling 

By default `fsdocs` uses the following (which are the settings used to build this site):

* the default template in [docs/_template.html](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/_template.html)

* the default styles in [docs/content/fsdocs-default.css](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/content/fsdocs-default.css).

* no custom styles in [docs/content/fsdocs-custom.css](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/content/fsdocs-default.css).

* no styling parameters for the default template (see below)

For your project, you don't need any of these files. However you can add them if you wish, though if
you adjsut them there is no guarantee that your template will continue to work with future versions of F# Formatting.

## Customizing via Styling Parameters

The following [content parameters](content.html) are particularly related to visual styling:

|  Substitution name                  | Value (if not overriden by --parameters)                      | 
|:------------------------------------|:--------------------------------------------------------------|
| `fsdocs-authors`              | `<Authors>`                                                   |  
| `fsdocs-collection-name-link` | `<FsDocsCollectionNameLink>`        |  
| `fsdocs-license-link`         | `<FsDocsLicenseLink>`  | 
| `fsdocs-logo-src`             | `<FsDocsLogoSource>` |  
| `fsdocs-logo-link`            | `<FsDocsLogoLink>`   |                
| `fsdocs-navbar-position`      | `<FsDocsNavbarPosition>` (`fixed-left` or `fixed-right`)     |  
| `fsdocs-release-notes-link`   | `<FsDocsReleaseNotesLink>` else `<PackageProjectUrl>/blob/master/RELEASE_NOTES.md`  | 
| `fsdocs-repository-link`      | `<RepositoryUrl>`                                             | 
| `fsdocs-theme`                | `<FsDocsTheme>`, must currently be `default`    | 


As an example, here is [a page with `fsdocs-navbar-position` set to `fixed-left`](templates/leftside/styling.html).

## Customizing via CSS

You can start styling by creating a file `docs/fsdocs-custom.css` and adding entries to it.  It is loaded by
the standard template.  The CSS classes of generated content are:

|  CSS class   | Corresponding Content|  
|:------------------------------------|:--------------------------------------------------------------|
| `.fsdocs-tip`              |   generated tooltips                                                  |  
| `.fsdocs-member-list `      |  generated member lists  |
| `.fsdocs-member-name `      |  generated member names |
| `.fsdocs-member-tooltip `      |  generated tooltips for members |
| `.fsdocs-xmldoc `      |  generated xmldoc sections  |
| `.fsdocs-entity-list `      |  generated entity lists |
| `.fsdocs-member-list `      |  generated member lists |

Some generated elements are given specific HTML ids:

|  HTML Element Id    | Content|  
|:------------------------------------|:--------------------------------------------------------------|
| `#fsdocs-content`              |    The generated content |  
| `#fsdocs-searchbox `      |   The search box |
| `#fsdocs-logo `      |  The logo |
| `#fsdocs-menu `      |  The navigation-bar |

If you write a new theme by CSS styling please contribute it back to FSharp.Formatting.

## Customizing via a new template

You can do advanced styling by creating a new template.  Add a file `docs/_template.html`, likely starting
with the existing default template.

> NOTE: There is no guarantee that your template will continue to work with future versions of F# Formatting.
> If you do develop a good template please consider contributing it back to F# Formatting.


