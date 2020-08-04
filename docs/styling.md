# Styling 

By default `fsdocs` uses the following (which are the settings used to build this site):

* the default template in [docs/_template.html](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/_template.html)

* the default styles in [docs/content/fsdocs-style.css](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/content/fsdocs-style.css).

* no custom styles in [docs/content/fsdocs-custom.css](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/content/fsdocs-style.css).

* no styling parameters for the default template (see below)

For your project, you don't need any of these files. However you can add them if you wish, though if
you adjsut them there is no guarantee that your template will continue to work with future versions of F# Formatting.

## Customizing via Styling Parameters

The following [content parameters](content.html) are particularly related to visual styling:

|  Substitution name                  | Value (if not overriden by --parameters)                      |  
|:------------------------------------|:--------------------------------------------------------------|
| {{fsdocs-logo-src}}             | `<FsDocsLogoSource>` else {{root}}/img/logo.png             | 
| {{fsdocs-logo-link}}            | `<FsDocsLogoLink>` else `<PackageProjectUrl>`                 | 
| {{fsdocs-navbar-position}}      | `fixed-left` or `fixed-right` (default ``fixed-right``)       | 

As an example, here is [a page with `fsdocs-navbar-position` set to `fixed-left`](templates/leftside/styling.html).

## Customizing via CSS

You can start styling by creating a file `docs/fsdocs-custom.css` and adding entries to it.  It is loaded by
the standard template.

## Customizing via a new template

You can do advanced styling by creating a new template.  Add a file `docs/_template.html`, likely starting
with the existing default template.

> NOTE: There is no guarantee that your template will continue to work with future versions of F# Formatting.
> If you do develop a good template please consider contributing it back to F# Formatting.


