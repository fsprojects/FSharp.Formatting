---
category: Documentation
categoryindex: 1
index: 9
---

# Customization and Styling

When using `fsdocs`, there are six levels of extra content development and styling.

1. Don't do any styling or documentation customization and simply write content. This is by far the simplest option to
   maintain.

2. Add content such as an `docs/index.md` to customize the front-page content for your generated docs.
   You can also add content such as `docs/reference/fslib.md` to give a bespoke landing page
   for one of your namespaces, e.g. here assumed to be `namespace FsLib`. This will override any
   generated content.

3. Customize via Styling Parameters

4. Customize via CSS

5. Customize via a new template

6. Customize by generating your own site using your own code

By default `fsdocs` does no styling customization and uses the following defaults. These are the settings used to build
this site.

* Uses the default template
  in [docs/_template.html](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/_template.html)

* Uses the default styles
  in [docs/content/fsdocs-default.css](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/content/fsdocs-default.css).

* Uses no custom styles
  in [docs/content/fsdocs-custom.css](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/content/fsdocs-default.css).

* Uses no styling parameters except those extracted from the project files.

For your project, you don't need any of these files. However, you can add them if you wish, though if
you adjust them there is no guarantee that your template will continue to work with future versions of F# Formatting.

## Customizing via Styling Parameters

The following [content parameters](content.html) are particularly related to visual styling:

| Substitution name           | Value (if not overriden by --parameters)                                           | 
|:----------------------------|:-----------------------------------------------------------------------------------|
| `fsdocs-authors`            | `<Authors>`                                                                        |   
| `fsdocs-license-link`       | `<FsDocsLicenseLink>`                                                              | 
| `fsdocs-logo-src`           | `<FsDocsLogoSource>`                                                               |  
| `fsdocs-logo-alt`           | `<FsDocsLogoAlt>`, defaults to `Logo`                                              |
| `fsdocs-favicon-src`        | `<FsDocsFaviconSource>`                                                            |
| `fsdocs-logo-link`          | `<FsDocsLogoLink>`                                                                 |                
| `fsdocs-release-notes-link` | `<FsDocsReleaseNotesLink>` else `<PackageProjectUrl>/blob/master/RELEASE_NOTES.md` | 
| `fsdocs-repository-link`    | `<RepositoryUrl>`                                                                  | 
| `fsdocs-theme`              | `<FsDocsTheme>`, must currently be `default`                                       | 

These basic entry-level styling parameters can be set in the project file or `Directory.Build.props`.
For example:

```xml
<PropertyGroup>
    <!-- Example ultra-simple styling and generation settings for FsDocs default template-->
    <PackageLicenseUrl>https://github.com/foo/bar/blob/master/License.txt</PackageLicenseUrl>
    <PackageProjectUrl>https://foo.github.io/bar/</PackageProjectUrl>
    <RepositoryUrl>https://github.com/foo/bar/</RepositoryUrl>
    <FsDocsLogoLink>https://fsharp.org</FsDocsLogoLink>
    <FsDocsLogoSource>img/logo.png</FsDocsLogoSource>
    <FsDocsFaviconSource>img/favicon.ico</FsDocsFaviconSource>
    <FsDocsLicenseLink>https://github.com/foo/bar/blob/master/License.txt</FsDocsLicenseLink>
    <FsDocsReleaseNotesLink>https://github.com/foo/bar/blob/master/release-notes.md</FsDocsReleaseNotesLink>
    <FsDocsWarnOnMissingDocs>true</FsDocsWarnOnMissingDocs>
    <FsDocsTheme>default</FsDocsTheme>
</PropertyGroup>
```

As an example, here is [a page with alternative styling](templates/leftside/styling.html).

## Customizing via CSS

You can start styling by creating a file `docs/content/fsdocs-theme.css` and adding entries to it.  
It is loaded by the standard template.

### CSS variables

The default template is heavily based
on [CSS variables](https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties). These can easily be
override to customize the look and feel of the default theme.  
A full list of the overrideable variables can be
found [here](https://github.com/fsprojects/FSharp.Formatting/blob/main/docs/content/fsdocs-default.css).

```css
:root {
    --text-color: red;
}

[data-theme=dark] {
    --text-color: darkred;
}
```

Please be aware that the `dark` mode in the default theme is using the same variables.
When you override a variable, it will also be used in `dark` mode unless redefined in a `[data-theme=dark]` CSS query.

### CSS classes

The API documentation uses a set of fixed CSS classes:

| CSS class                | Corresponding Content                   |  
|:-------------------------|:----------------------------------------|
| `.fsdocs-tip`            | generated tooltips                      |  
| `.fsdocs-xmldoc`         | generated xmldoc sections               |
| `.fsdocs-member-list`    | generated member lists (tables)         |
| `.fsdocs-member-usage`   | usage in generated member lists         |
| `.fsdocs-member-tooltip` | tooltips in generated member lists      |
| `.fsdocs-member-xmldoc`  | documentation in generated member lists |
| `.fsdocs-entity-list`    | generated entity lists                  |
| `.fsdocs-entity-name`    | generated entity lists                  |
| `.fsdocs-entity-xmldoc`  | documentation in generated entity lists |
| `.fsdocs-exception-list` | generated exception lists               |
| `.fsdocs-summary`        | the 'summary' section of an XML doc     |
| `.fsdocs-remarks`        | the 'remarks' section of an XML doc     |
| `.fsdocs-params`         | the 'parameters' section of an XML doc  |
| `.fsdocs-param`          | a 'parameter' section of an XML doc     |
| `.fsdocs-param-name`     | a 'parameter' name of an XML doc        |
| `.fsdocs-returns`        | the 'returns' section of an XML doc     |
| `.fsdocs-example`        | the 'example' section of an XML doc     |
| `.fsdocs-note`           | the 'notes' section of an XML doc       |
| `.fsdocs-para`           | a paragraph of an XML doc               |

Some generated elements are given specific HTML ids:

| HTML element selector       | Content                        |  
|:----------------------------|:-------------------------------|
| `header`                    | The navigation-bar             | 
| `#fsdocs-main-menu`         | The main menu on the left side |
| `#content`                  | The generated content          |  
| `#fsdocs-page-menu`         | The sub menu on the right side |
| `dialog`                    | The search dialog              |
| `dialog input[type=search]` | The search box                 |
| `#fsdocs-logo `             | The logo                       |

If you write a new theme by CSS styling please contribute it back to FSharp.Formatting.

## Customizing via a new template

You can do advanced styling by creating a new template. Add a file `docs/_template.html`, likely starting
with the existing default template.

> NOTE: To enable hot reload during development with `fsdocs watch` in a custom `_template.html` file,
> make sure to add the single line `{{fsdocs-watch-script}}`  to your `<head>` tag.

> NOTE: There is no guarantee that your template will continue to work with future versions of F# Formatting.
> If you do develop a good template please consider contributing it back to F# Formatting.

## Customizing menu items by template

You can add advanced styling to the sidebar generated menu items by creating a new template for it.
`fsdoc` will look for menu templates in the `--input` folder, which defaults to the docs folder.

To customize the generated menu-item headers, use file `_menu_template.html` with starting template:

```html
<li class="nav-header">
    {{fsdocs-menu-header-content}}
</li>
{{fsdocs-menu-items}}
```

Similarly, to customize the individual menu item list, use file `_menu-item_template.html` with the starting template:

```html
<li class="nav-item"><a href="{{fsdocs-menu-item-link}}" class="nav-link">{{fsdocs-menu-item-content}}</a></li>
```

Do note that files must be added before running, or won't be generated.
In case you want to get a unique identifier for a header or menu item, you can use `{{fsdocs-menu-header-id}}`
and `{{fsdocs-menu-item-id}}`, respectively.

## Injecting additional html into the default template

Occasionally, you may find the need to make small customizations to the default template, such as adding a Google
Analytics snippet or including additional style or script tags. To address this scenario, you can create two
files: `_head.html` and/or `_body.html`.

The content within these files will serve as replacements for the `{{fsdocs-head-extra}}` and `{{fsdocs-body-extra}}`
placeholders, which are utilized in the default template.

## Customizing by generating your own site using your own code

The `FSharp.Formatting.ApiDocs` namespace includes a `GenerateModel` that captures
the results of documentation preparation in `ApiDocsModel` and allows you to
generate your own site using your own code.

> NOTE: The ApiDocsModel API is undergoing change and improvement, and there is no guarantee that your bespoke site
> generation will continue to work
> with future versions of F# Formatting.

> NOTE: The `ApiDocsModel` currently includes some generated HTML with some specific style tags.
> In the long term these may be removed from the design of that component.
