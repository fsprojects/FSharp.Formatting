# Changelog

## [Unreleased]

### Added
* Add "Copy" button to all code blocks in generated documentation, making it easy to copy code samples to the clipboard. [#72](https://github.com/fsprojects/FSharp.Formatting/issues/72)
* Add `<FsDocsAllowExecutableProject>true</FsDocsAllowExecutableProject>` project file setting to include executable projects (OutputType=Exe/WinExe) in API documentation generation. [#918](https://github.com/fsprojects/FSharp.Formatting/issues/918)
* Add `{{fsdocs-logo-alt}}` substitution (configurable via `<FsDocsLogoAlt>` MSBuild property, defaults to `Logo`) for accessible alt text on the header logo image. [#626](https://github.com/fsprojects/FSharp.Formatting/issues/626)

### Fixed
* Fix `_menu_template.html` and `_menu-item_template.html` being copied to the output directory. [#803](https://github.com/fsprojects/FSharp.Formatting/issues/803)
* Fix `ApiDocMember.Details.ReturnInfo.ReturnType` returning `None` for properties that have both a getter and a setter. [#734](https://github.com/fsprojects/FSharp.Formatting/issues/734)
* Improve error message when a named code snippet is not found (e.g. `(*** include:name ***)` with undefined name now reports the missing name clearly). [#982](https://github.com/fsprojects/FSharp.Formatting/pull/982)
* HTML-encode XML doc text nodes and unresolved `<see cref>` values to prevent HTML injection and fix broken output when docs contain characters like `<`, `>`, or backticks in generic type notation. [#748](https://github.com/fsprojects/FSharp.Formatting/issues/748)
* Add uppercase output kind extension (e.g. `HTML`, `IPYNB`) to `ConditionalDefines` so that `#if HTML` and `(*** condition: HTML ***)` work alongside their lowercase variants. [#693](https://github.com/fsprojects/FSharp.Formatting/issues/693)
* Strip `#if SYMBOL` / `#endif // SYMBOL` marker lines from `LiterateCode` source before syntax-highlighting so they do not appear in formatted output. [#693](https://github.com/fsprojects/FSharp.Formatting/issues/693)
* Fix incorrect column ranges for inline spans (links, images, inline code) in the Markdown parser — spans and subsequent literals now report correct `StartColumn`/`EndColumn` values. [#744](https://github.com/fsprojects/FSharp.Formatting/issues/744)
* Normalize `--projects` paths to absolute paths before passing to the project cracker, fixing failures when relative paths are supplied. [#793](https://github.com/fsprojects/FSharp.Formatting/issues/793)

### Changed
* Update FCS to 43.10.100. [#935](https://github.com/fsprojects/FSharp.Formatting/pull/966)
* Reduce dark mode header border contrast to match the visual subtlety of light mode borders. [#885](https://github.com/fsprojects/FSharp.Formatting/issues/885)

## 21.0.0 - 2025-11-12

Stable release

## 21.0.0-beta-005 - 2025-04-23

### Added
* Add --ignoreuncategorized flag. [#953](https://github.com/fsprojects/FSharp.Formatting/pull/953)

## 21.0.0-beta-004 - 2024-11-20

### Changed
* Update FCS to 43.9.100. [#945](https://github.com/fsprojects/FSharp.Formatting/pull/945)

## 21.0.0-beta-003 - 2024-08-06

### Changed
* Update FCS to 43.8.301. [#935](https://github.com/fsprojects/FSharp.Formatting/pull/935)

## 21.0.0-beta-002 - 2024-06-19

### Changed
* Shrink API docs example heading font size a bit. [#923](https://github.com/fsprojects/FSharp.Formatting/pull/923)
* Improve overall API doc content alignment consistency in various scenarios. [#923](https://github.com/fsprojects/FSharp.Formatting/pull/923)

## 21.0.0-beta-001 - 2024-06-06

### Added
* Add expand/collapse-all button for API doc details. [#920](https://github.com/fsprojects/FSharp.Formatting/pull/920)

### Changed
* HTML structure of generated API documentation. [#919](https://github.com/fsprojects/FSharp.Formatting/pull/919)

## 20.0.1 - 2024-05-31
* Details improvements. [#917](https://github.com/fsprojects/FSharp.Formatting/pull/917)

## 20.0.0 - 2024-02-14

Stable release

## 20.0.0-beta-002 - 2024-02-08

### Fixed
* Avoid theme flicker in dark mode. [#901](https://github.com/fsprojects/FSharp.Formatting/pull/901)

## 20.0.0-beta-001 - 2024-01-31

### Changed
* Marking development of v20 as complete.

## 20.0.0-alpha-019 - 2024-01-29

### Fixed
* Use dvh for full viewport height . [#899](https://github.com/fsprojects/FSharp.Formatting/pull/899)

## 20.0.0-alpha-018 - 2024-01-10

### Fixed
* Add -webkit-text-size-adjust. [#889](https://github.com/fsprojects/FSharp.Formatting/issues/889)

## 20.0.0-alpha-017 - 2024-01-09

### Fixed
* Set default font-size for code. [#889](https://github.com/fsprojects/FSharp.Formatting/issues/889)

## 20.0.0-alpha-016 - 2023-12-07

### Fixed
* Use empty replacement for `{{fsdocs-meta-tags}` in API doc pages. [#892](https://github.com/fsprojects/FSharp.Formatting/pull/892)

## 20.0.0-alpha-015 - 2023-12-06

### Fixed
* Namespace description overflows content box. [#886](https://github.com/fsprojects/FSharp.Formatting/issues/886)

### Added
* SEO-optimization for new theme. Allow `description` and `keywords` in frontmatter. Introduce `{{fsdocs-meta-tags}}`. [#869](https://github.com/fsprojects/FSharp.Formatting/issues/869)

## 20.0.0-alpha-014 - 2023-11-22

### Added
* Added the ability to use ipynb files as inputs [#874](https://github.com/fsprojects/FSharp.Formatting/pull/874)

### Fixed
* Fsx outputs no longer treat inline html as F# code. Inline html blocks are now enclosed inside literate comments. 

## 20.0.0-alpha-013 - 2023-11-21

### Added
* Add more options to customize colors.

### Removed
- `--fsdocs-theme-toggle-light-color` and `--fsdocs-theme-toggle-dark-color` are now deprecated. Use `--header-link-color` instead.
- `<FsDocsCollectionNameLink>`

### Changed
- Update FCS to 43.8.100

## 20.0.0-alpha-012 - 2023-11-17

### Added
* Add more options to customize colors.

## 20.0.0-alpha-011 - 2023-11-16

### Fixed
* Take `<UseArtifactsOutput>` into account during the project restore check.

## 20.0.0-alpha-010 - 2023-11-15

### Fixed
* Update styling for blockquote.
* Update search documentation.
* Tweak dark theme colors.
* Loosen the header link search restriction.
* Improve custom theme icon.
* Correct fsdocs-menu-item-active-class value.
* Fix example of Right-Side navigation.

### Changed
* Allow for more fine-grained styling control.

## 20.0.0-alpha-009 - 2023-11-11

### Fixed
* Return original prop when no Directory.Build.props is used as fallback.

## 20.0.0-alpha-008 - 2023-11-10

### Fixed
* Add dynamic `max-width` to tooltip.
* Overflow long namespace names in overview table.

## 20.0.0-alpha-007 - 2023-11-10

### Fixed
* Smaller scrollbars on mobile devices

### Added
* Use property values from the current `Directory.Build.props` file as fallback. [#865](https://github.com/fsprojects/FSharp.Formatting/issues/865)

## 20.0.0-alpha-006 - 2023-11-09

### Added
* Revisited search using [fusejs](https://www.fusejs.io/)

## 20.0.0-alpha-005 - 2023-11-09

### Changed
* Improve API doc styling.

### Fixed
* Make mobile menu scrollable.

## 20.0.0-alpha-004 - 2023-11-08

### Fixed
* Don't use font ligatures, the can confuse newcomers of F#.
* Replace `{{fsdocs-list-of-namespaces}}` with an empty string if no API docs are present.
* Improve default styling of `blockquote`
* Add some padding for level 3 and 4 headers in 'on this page' section.

## 20.0.0-alpha-003 - 2023-11-06

### Changed
* default template style changes (`#fsdocs-page-menu` outside `main`, link around project name, overflow ellipsis for menu items)

## 20.0.0-alpha-002 - 2023-11-03

### Fixed
* `{{root}}` is now available as substitution in `_body.html` and `_head.html`.

## 20.0.0-alpha-001 - 2023-11-03

### Removed
* `FsDocsNavbarPosition` is no longer respected. Use CSS variables instead. See [docs](https://fsprojects.github.io/FSharp.Formatting/templates/leftside/styling.html).
* ⚠️ Search was completely removed and will be revisited in future versions.

### Changed
* The default template was updated and is not compatible with previous versions.

### Added
* Dark mode is available out of the box.
* `{{fsdocs-head-extra}}` can included additional html before the closing `</head>` when `_head.html` exists.
* `{{fsdocs-body-extra}}` can included additional html before the closing `</body>` when `_body.html` exists.
* MSBuild property `<FsDocsFaviconSource>` can be used to configure the favicon.
* `active` class is added to the active menu item in `{{fsdocs-list-of-documents}}`.
If menu templating is used, `{{fsdocs-menu-header-active-class}}` and `{{fsdocs-menu-item-active-class}}` are avaiable.
* `{{fsdocs-page-content-list}}` contains an unordered list of the header (`h1` till `h4`) of the current page. (if available)

## 19.1.1 - 2023-10-10

* Fix code rendering on firefox. [#851](https://github.com/fsprojects/FSharp.Formatting/pull/851)

## 19.1.0 - 2023-09-15

* Only reload css file when changed. [#845](https://github.com/fsprojects/FSharp.Formatting/pull/845)
* Add previous and next page url substitutions. [#846](https://github.com/fsprojects/FSharp.Formatting/pull/846)

## 19.0.0 - 2023-08-22

* Update FCS to 43.7.400

## 18.1.1 - 2023-08-02

* Pass `--multiemit-` as default option for `FsiEvaluator`. 

## 18.1.0 - 2023-04-13

* Collapsible ApiDocs member info [#778](https://github.com/fsprojects/FSharp.Formatting/issues/778). The issue was fixed collaboratively in an [Amplifying F# session](https://amplifying-fsharp.github.io/) with a recording that can be found [here](https://amplifying-fsharp.github.io/sessions/2023/03/31/).

## 18.0.0 - 2023-03-29

* Update FCS to 43.7.200
* Target `net6.0` for `fsdocs-tool` [#799](https://github.com/fsprojects/FSharp.Formatting/issues/799)

## 17.4.1 - 2023-03-29

* Update ipynb output metadata [#809](https://github.com/fsprojects/FSharp.Formatting/issues/809)

## 17.4.0 - 2023-03-09

* One FSI evaluator per docs file [#737](https://github.com/fsprojects/FSharp.Formatting/issues/737)

## 17.3.0 - 2023-03-06

* Better test project detection [#800](https://github.com/fsprojects/FSharp.Formatting/issues/800)

## 17.2.3 - 2023-02-21

* Fix external docs link [#794](https://github.com/fsprojects/FSharp.Formatting/issues/794)

## 17.2.2 - 2023-01-16

* Improvement for `<seealso/>` [#789](https://github.com/fsprojects/FSharp.Formatting/issues/789)

## 17.2.1 - 2023-01-14

* Fix support for `<exclude/>` [#786](https://github.com/fsprojects/FSharp.Formatting/issues/786)

## 17.2.0 - 2022-12-28

* Resolve markdown links in raw html [#769](https://github.com/fsprojects/FSharp.Formatting/issues/769)

## 17.1.0 - 2022-11-22

* [Add syntax highlighting to API docs](https://github.com/fsprojects/FSharp.Formatting/pull/780)

## 17.0.0 - 2022-11-17

* Update to .NET 7.0.100

## 16.1.1 - 2022-09-07

* [Fix arguments naming and escape operator name in usageHtml](https://github.com/fsprojects/FSharp.Formatting/pull/765/)

## 16.1.0 - 2022-08-30

* Update to .NET 6.0.400
* Update to Ionide.ProjInfo 0.60

## 16.0.4 - 2022-08-30

* [Fix indexers in output](https://github.com/fsprojects/FSharp.Formatting/pull/767)

## 16.0.3 - 2022-08-30
* [Fix link translation when using relative input path](https://github.com/fsprojects/FSharp.Formatting/issues/764)

## 16.0.2 - 2022-08-23
* [Improves markdown emphasis parsing.](https://github.com/fsprojects/FSharp.Formatting/pull/763)

## 16.0.1 - 2023-08-16
* Custom templating for menus 

## 15.0.3 - 2023-08-15
* Fixes Markdown parser gets multiple-underscores-inside-italics wrong [#389](https://github.com/fsprojects/FSharp.Formatting/issues/389)

## 15.0.2 - 2023-08-05
* Trim the `--fscoptions` before passing them as `otherflags`. ([comment #616](https://github.com/fsprojects/FSharp.Formatting/issues/616#issuecomment-1200877765))

## 15.0.1 - 2023-07-01
* fix https://github.com/fsprojects/FSharp.Formatting/issues/749

## 15.0.0 - 2022-03-20
* Update to .NET 6

## 14.0.1 - 2021-11-11
* Fixes 703, 700 - `--strict` is now considerably stricter, and more diagnostics being shown

## 14.0.0 - 2021-11-10

* Fix [Getting ReturnType from ApiDocMember without Html already embedded](https://github.com/fsprojects/FSharp.Formatting/issues/708)

## 13.0.1 - 2021-11-10

* Skip the output folder when processing

## 13.0.0 - 2021-11-10

* Remove unused TransformAndOutputDocument from API
* Fixes Can't yet format InlineHtmlBlock #723
* Fixes `<code>` blocks are emitting `<pre>` blocks with escapes no longer escaped #712

## 12.0.2 - 2021-11-10

* Remove front-matter output from notebooks

## 12.0.1 - 2021-11-10

* Improve package description

## 12.0.0 - 2021-11-07

* [Allow input-->output link translation](https://github.com/fsprojects/FSharp.Formatting/pull/718)

## 11.5.1 - 2021-10-30

* [Allow user-set ids for xmldoc example nodes](https://github.com/fsprojects/FSharp.Formatting/pull/704)

## 11.5.0 - 2021-10-30

* [Remove MSBuild assemblies from library nugets](https://github.com/fsprojects/FSharp.Formatting/pull/715)

## 11.4.4 - 2021-10-11

* [Websocket CPU efficiency improvements](https://github.com/fsprojects/FSharp.Formatting/pull/711)

## 11.4.3 - 2021-08-17

* Style blockquotes

## 11.4.2 - 2021-07-29

* [Download links broken](https://github.com/fsprojects/FSharp.Formatting/issues/696)
* [Duplicating HTML tags for FSX and IPYNB output](https://github.com/fsprojects/FSharp.Formatting/issues/695)

## 11.4.1 - 2021-07-23

* [Fixed navbar scrolling](https://github.com/fsprojects/FSharp.Formatting/issues/672#issuecomment-885532640)

## 11.4.0 - 2021-07-22

* [Fixed some CSS](https://github.com/fsprojects/FSharp.Formatting/pull/688/)

## 11.3.0 - 2021-07-22

* [Bump to FSharp.Compiler.Service 40.0](https://github.com/fsprojects/FSharp.Formatting/pull/682)
* [Fix bottom margin in default CSS](https://github.com/fsprojects/FSharp.Formatting/pull/687)
* [Improve github and signature links](https://github.com/fsprojects/FSharp.Formatting/pull/681)
* [Fix typo in location for custom CSS](https://github.com/fsprojects/FSharp.Formatting/pull/684)

## 11.2.0 - 2021-05-17

* scrollable navbar #677 by nhirschey 
* Show field type for record fields #674
* Add --ignoreprojects flag  #676 by chengh42 

## 11.1.0 - 2021-04-15
* Add frontmatter, category, categoryindex, index, title

## 11.0.4 - 2021-04-15
* testing package publish

## 11.0.3 - 2021-04-14
* testing package publish

## 11.0.2 - 2021-04-14
* add favicon.ico to template and use F# logo as default favicon for generated sites

## 11.0.1 - 2021-04-14
* update to Ionide.ProjInfo
* use computed args for references in API doc generation
* Fix #616
* Fix #662
* Fix #646

## 10.1.1 - 2021-04-13
* Switch to cleaner default styling based on DiffSharp styles
* Change `fsdocs-menu` to `fsdocs-nav`

## 10.0.8 - 2021-04-13
* Add cref copy buttons by default

## 10.0.7 - 2021-04-13
* Fix more formatting and switch to `fsdocs-member-usage` instead of `fsdocs-member-name`

## 10.0.2 - 2021-04-13
* Permit `cref:T:System.Console` code references in markdown content

## 10.0.1 - 2021-04-12
* Apply substitutions to content
* Add `fsdocs-source-filename` and `fsdocs-source-basename` substitutions

## 9.0.4 - 2021-03-24
* Trim spaces from examples (TrimEnd only)

## 9.0.3 - 2021-03-24
* Trim spaces from examples

## 9.0.1 - 2021-02-11
* Proper fix for elide multi-language docs from navigation and site search index

## 9.0.0 - 2021-02-11
* Rename --property flag to --properties
* Elide multi-language docs from navigation and site search index

## 8.0.1 - 2021-01-21
* [Prevent CLI parameters from being discarded](https://github.com/fsprojects/FSharp.Formatting/pull/634)
* [Update Dockerfile and NuGet.config for binder](https://github.com/fsprojects/FSharp.Formatting/pull/636)

## 8.0.0 - 2021-01-14
* [update FCS, allow fsdocs to roll forward to net5.0](https://github.com/fsprojects/FSharp.Formatting/pull/621)
* [Refactor the templating engine and the command tool cache](https://github.com/fsprojects/FSharp.Formatting/pull/615)
* [Refactor the project cracker](https://github.com/fsprojects/FSharp.Formatting/pull/618)
* [Retry project cracking when there isn't a targetPath](https://github.com/fsprojects/FSharp.Formatting/pull/613)
* [Add include-it-raw literate command](https://github.com/fsprojects/FSharp.Formatting/pull/624)
* Add more complete info on how to upgrade
* [CommandTool: add hot reload to the watch command](https://github.com/fsprojects/FSharp.Formatting/pull/629)

## 7.2.9 - 2020-09-22

* Document how to do math in XML comments
* Add --strict flag to fsdocs for stricter checking
* Add --property flag to fsdocs to pass properties to dotnet msbuild
* Better diangostics and logging for fsdocs

## 7.2.8 - 2020-09-09

* [ApiDocs: examples not showing for types and modules](https://github.com/fsprojects/FSharp.Formatting/issues/599)

* Comma-separate interface list in API docs

* Remove untyped Sections from ApiDocComment since individual supported sections are now available

## 7.2.7 - 2020-09-09

* [ApiDocs: cref to members are not resolving to best possible link](https://github.com/fsprojects/FSharp.Formatting/issues/598)

* [ApiDocs: namespace docs are showing in module/type summaries as well](https://github.com/fsprojects/FSharp.Formatting/issues/597)

## 7.2.6 - 2020-08-07

* In ApiDocsModel, separate out the parameter, summary, remarks sections etc.

* In ApiDocsModel, integrate the parameter types with the parameter docs (when using XML docs)

* In HTML generation for API docs, locate the github link top right 

* In ApiDocsModel.Generate, optionally give warnings when XML doc is missing or parameter names are incorrect. Activate using <FsDocsWarnOnMissingDocs>

* In ApiDocsModel, change "Parameters" to "Substitutions"

* Fix formatting of (most) custom operators

* Fix formatting of op_XYZ binary and unary operators 

## 7.2.5 - 2020-08-06

* change `<namespacesummary>...<namespacesummary>` to `<namespacedoc> <summary>... </summary> </namespacedoc>`

* change `<categoryindex>3<categoryindex>` to `<category index="3">...</category>`

## 7.2.4 - 2020-08-06

* support `<namespacesummary>...<namespacesummary>`

* support `<namespaceremarks>...<namespaceremarks>`

* support `<note>...<note>`

* support `<category>...</category>`

* support `<exclude />`

* allow  `<a href="..." >` in XML doc comments

* allow  `<paramref name="..." >` in XML doc comments

* document XML doc things supported

## 7.2.2 - 2020-08-05

* instruct about settings

## 7.2.1 - 2020-08-05

* fix images in nuget

## 7.2.0 - 2020-08-05

* include templates

## 7.1.8 - 2020-08-05

* bump version

## 7.1.6 - 2020-08-04

* bump version

## 7.1.5 - 2020-08-04

* fix navbar position option fixed-left

## 7.1.4 - 2020-08-04

* fixed property computation 

## 7.1.3 - 2020-08-04

* fixed typo for `LICENCE.md`

* all classes to have `fsdocs-` prefix

## 7.1.2 - 2020-08-04

* fixed all classes to have `fsdocs-` prefix

* added documentation on styling

## 7.1.1 - 2020-08-04

* fixed root

## 7.1.0 - 2020-08-04

* add text content of markdown and scripts to generated search index

* overhaul the substitution names used by FSharp.Formatting and expected in the template. The table is in the docs and below

* generate {{fsdocs-list-of-documents}} substitution and use it in both API docs and content

* generate {{fsdocs-list-of-namespaces}} substitution and use it in both API docs and content

* fix link model so {{root}} is always respected

* Add `qualify` parameter that asks to qualify all names by the collection name e.g. FSharp.Core

* Respect per-project settings, e.g. if one nuget package has a different set of authors or home page to another

* Add documentation about styling

* Allow fixed-left and fixed-right positions for the navbar

* Add `{{fsdocs-logo-link}}` parameter to default template

* Add `{{fsdocs-logo-link}}` parameter to default template

* generate HTML giving hyperlinks for types with cross-links

* switch to left bootstrap nav bar in template for a table of contents

* improve sizings

* move to one copy of template in docs/_template.html 

* ApiDocsTypeDefinition and ApiDocsModule merged to ApiDocsEntity

* Default template now expects logo in img/logo.png

* Improvements in default HTML generation

* ApiDocComment.Blurb renamed to ApiDocComment.Summary and only populated with summary text for things read from XML

* simplify tool instructions

* add info about upgrading

## 6.1.0 - 2020-07-21
* fix mistake in laying down `extras` directory 

## 6.0.9 - 2020-07-21
* put extra content in `extras` directory in nuget package and include Dockerfile and NuGet.config

## 6.0.8 - 2020-07-21
* show extended type in generated docs for extension members
* include fsdocs-styles.css, fsdocs-search.js, fsdocs-tips.js in built site 'content' directory by default
* use default template from nuget package by default

## 6.0.7 - 2020-07-20
* fix formatting of generic parameters so they don't show inference variables for members

## 6.0.6 - 2020-07-20
* fix default styling

## 6.0.5 - 2020-07-20
* improve display in FSharp.Formatting API docs and add more information

## 6.0.4 - 2020-07-20
* Watch defaults to `tmp/watch`

## 6.0.3 - 2020-07-20
* Add `(*** include-fsi-output **)`
* Add `(*** include-fsi-merged-output **)`
* Add server to `dotnet watch` and by default switch to local host
* Always inject `fsi.AddPrinter`, `fsi.AddHtmlPrinter` etc. into the programming model for literate scripts

## 6.0.2 - 2020-07-19

* Remove the `api` command from the command line tool (`build` generalises it)
* Add missing search.js

## 6.0.1 - 2020-07-19

* build the Lunr `index.json` from every execution of `fsdocs build`
* Make the search index entries available as part of the ApiDocs model
* Add search box to generated docs
* Add `ApiDocs` prefix to all types in `ApiDocsModel`
* Remove `Details` from `ApiDocsModel`

## 5.0.5 - 2020-07-14

* Correct behaviour of '--clean'

## 5.0.4 - 2020-07-14

* Fix emit of odd character in latex output

## 5.0.3 - 2020-07-14

* Paket update and remove workaround code
* add '--clean' to fsdocs 

## 5.0.2 - 2020-07-14
* Update to FCS v36.0
* Add .ipynb output option for documents
* Add .fsx output option for documents
* Literate.WriteHtml --> Literate.ToHtml/Literate.WriteHtml overloads
* MetadataFormat.Generate --> ApiDocs.GenerateHtml/ApiDocs.GenerateModel overloads
* Fix Literate.* to do approximate (non-razor) templating/  
* Remove Razor support
* HTML templates now use `{{prop-name}}`
* FSharp.CodeFormat --> FSharp.Formatting.CodeFormat
* FSharp.Markdown --> FSharp.Formatting.Markdown
* FSharp.Literate --> FSharp.Formatting.Literate and FSharp.Formatting.Literate.Evaluation
* FSharp.MetadataFormat --> FSharp.Formatting.ApiDocs
* FSharp.ApiDocs uses HTML substitution for templating, no Razor
* Add "include-it" and "include-output" with implied reference to the immediately preceding snippet
* For command line tool
Rename fsformatting to fsdocs
Update command line parser
"fsformatting literate process-directory" --> "fsdocs convert"
"fsformatting metadata-format generate" --> "fsdocs api"
"--dllFiles" --> "--dlls"
"--outDir" --> "--output"
"--outputDirectory" --> "--output"
"--output" is optional (defaults to 'output')
"--inputDirectory" --> "--input"
Add --nonpublic
Add --xmlComments
Automatically populate metadata from project settings.
* Add `fsdocs build` command to the documentation generator that has lots of sensible defaults.

## 4.1.0 - 2020-06-01
* Support preview F# language features.
* Add support for customizing assigned CSS class.

## 4.0.1 - 2020-05-12
* Add .NET Core support for all libraries.
* Update to FSCS v35.0.
* Add helpers for CustomOperationAttribute.

## 4.0.0-rc2 - 2020-04-24
* Update to FSCS v34.1.

## 4.0.0-rc2 - 2020-04-24
* Update to FSCS v34.

## 4.0.0-rc2 - 2020-04-24
* Fix packaging issues.

## 4.0.0-rc2 - 2020-04-24
* Add .NET Core support for all libraries.
* Update to latest FSharp.Compiler.Service

## 3.1.0 - 2019-04-12
* remove beta tag since it is already widely used

## 3.0.0-beta01 - 2016-08-01
* Update to latest FSharp.Compiler.Service
* No longer filter FSHarp.Core based on optdata/sigdata (it is now always bundled)

## 3.0.0-beta01 - 2016-08-01
* FSharp.Formatting.Literate for netstandard2.0

## 3.0.0-beta01 - 2016-08-01
* Fix usage formatting - https://github.com/fsprojects/FSharp.Formatting/issues/472

## 3.0.0-beta01 - 2016-08-01
* Added support for attributes on modules, types and members
* Updated razor templates to show attributes and added a warning for obsolete API

## 3.0.0-beta01 - 2016-08-01
* Upgrade FSharp.Compiler.Service to be compatible with FAKE 5

## 3.0.0-beta01 - 2016-08-01
* Fix some links on the website - https://github.com/fsprojects/FSharp.Formatting/pull/458
* Another link on the website - https://github.com/fsprojects/FSharp.Formatting/pull/454
* Support highlighting for paket.dependencies `storage` keyword - https://github.com/fsprojects/FSharp.Formatting/pull/451
* In order to upgrade follow instructions at https://fsprojects.github.io/FSharp.Formatting/upgrade_from_v2_to_v3.html

## 3.0.0-beta01 - 2016-08-01
* Improve Stacktrace on Script file processing

## 3.0.0-beta01 - 2016-08-01
* Fix System.ValueType dep.

## 3.0.0-beta01 - 2016-08-01
* Include razor component.

## 3.0.0-beta01 - 2016-08-01
* Always generate anchors when using command line tool.

## 3.0.0-beta01 - 2016-08-01
* Don't hide errors in fsformatting tool (Literate).
* Improve error message by using inner exceptions.

## 3.0.0-beta01 - 2016-08-01
* Don't hide errors in fsformatting tool.

## 3.0.0-beta01 - 2016-08-01
* MarkdownSpan and MarkdownParagraph now use named DUs
* Add range to MarkdownParagraph and MarkdownSpan (https://github.com/fsprojects/FSharp.Formatting/pull/411)
* FSharp.Formatting no longer has a strong dependency on Razor (https://github.com/fsprojects/FSharp.Formatting/pull/425)
* FSharp.Formatting no longer depends on VFPT.Core (https://github.com/fsprojects/FSharp.Formatting/pull/432)
* Add beta packages to AppVeyor feed.
* Update FSharp.Compiler.Service component.

## 2.14.4 - 2016-06-02
* Use `#I __SOURCE_DIRECTORY__` in the loads script (more reliable)

## 2.14.3 - 2016-05-26
* Fixes issues with comments and keywords in Paket highlighter (#408)
* Fix tooltip flickering in CSS (#406)
* End blockquote on a line with an empty blockquote (fix #355) (#400)

## 2.14.2 - 2016-04-06
* Add code to parse table rows correctly (#394)
* Also fixes (#388) Markdown parser doesn't recognize inline code `x | y` inside table cell

## 2.14.1 - 2016-04-05
* Temporarily pin FSharp.Compiler.Service (#395)
* Cache is new keyword in Paket (#392)

## 2.13.6 - 2016-02-29
* Added TypeScript to the CSharpFormat project (#386)

## 2.13.5 - 2016-01-25
* Fixes issues in PaketFormat (#381) - colorize HTTP and file prefix
* Reliable getTypeLink (#380) - avoid crashes

## 2.13.4 - 2016-01-20
* Colors paket keywords (#379)

## 2.13.3 - 2016-01-18
* Adds PaketFormat to not color URLs as comments in Paket files (#349)

## 2.13.2 - 2016-01-12
* Improve the load script to fix FsLab issue (https://github.com/fslaborg/FsLab/issues/98)

## 2.13.1 - 2016-01-12
* Make logging to file optional using environment variable

## 2.13.0 - 2015-12-29
* Be compatible with the common-mark spec for 'Fenced code blocks' and 'Indented code blocks'.
See https://github.com/fsprojects/FSharp.Formatting/pull/343.
Please follow-up by adding support for more sections of the spec!
Just add the section to https://github.com/fsprojects/FSharp.Formatting/blob/master/tests/FSharp.Markdown.Tests/CommonMarkSpecTest.fs#L20
and fix the newly enabled tests.
* Add CompiledName to members with F# specific naming (https://github.com/fsprojects/FSharp.Formatting/pull/372)

## 2.12.1 - 2015-12-24
* update dependencies
* Upgrade the CommandTool to F# 4 and bundle FSharp.Core with sigdata and optdata.
* Fix crash when a fenced code block starts with an empty line (https://github.com/fsprojects/FSharp.Formatting/pull/361)
* Support for all known xml elements (https://github.com/fsprojects/FSharp.Formatting/pull/331)

## 2.12.0 - 2015-10-18
* Update dependencies to be compatible with FSharp.Compiler.Service >=1.4.0.3

## 2.11.1-alpha1 - 2015-10-14
* Adds methods for cross-type links #330 (https://github.com/fsprojects/FSharp.Formatting/pull/330)

## 2.11.0 - 2015-09-28
* Fix https://github.com/fsprojects/FSharp.Formatting/issues/271
* Don't fail as long as we can recover / continue.
* Fix https://github.com/fsprojects/FSharp.Formatting/issues/201

## 2.10.3 - 2015-09-12
* Require compatible F# Compiler Service in Nuspec (fix #337)

## 2.10.2 - 2015-09-11
* Fix load script (wrap logging setup in try catch properly)

## 2.10.1 - 2015-09-11
* paket update && fix compilation (#338)
* Wrap logging setup in try catch

## 2.10.0 - 2015-07-26
* Add detailed logging and new FSharp.Formatting.Common.dll file
* Fix bug in C# code formatting tool (FormatHtml)

## 2.9.10 - 2015-06-27
* Support multiple snippets in Literate.Parse (This is obsolete, but needed for www.fssnip.net.)

## 2.9.9 - 2015-06-22
* Fix HTML escaping of code blocks with unknown languages (#321, #322)

## 2.9.6 - 2015-05-08
* Generate 'fssnip' class for non-F# <pre> tags for consistency

## 2.9.5 - 2015-05-06
* Provide an option to disable `fsi.AddPrinter` (#311)
* Generated line numbers for HTML are the same for F# and non-F#

## 2.9.4 - 2015-04-30
* Use `otherFlags` parameter (#308)
* Format code in Markdown comments (#307, #36)

## 2.9.3 - 2015-04-28
* Simplify using FCS interaction using Yaaf.Scripting (#305)
* Do not load dependencies when initializing evaluator
* Undo require exact version of F# Compiler Service

## 2.9.2 - 2015-04-24
* Require exact version of F# Compiler Service

## 2.9.1 - 2015-04-21
* Add back RazorEngine.dll (#302)

## 2.9.0 - 2015-04-20
* Properly encode '>' entities (#84)
* Generate line numbers for non-F# code (#227)
* Support headings on the same line as comment (#147)
* Fixes in HTML encoding of non-F# code snippets (#249, #213)
* Remove Razor mono workaround (#279)
* Add a public API to process a customized LiterateDocument (#282)
* Add an API to process a customized LiterateDocument (#289)
* Use template path if it is rooted (#281)
* Enable evaluation tests for literate scripts
* Create `fsi` object without `FSharp.Compiler.Interactive.Settings.dll`
* Fix #229 (Key already exists exception when parsing an assembly)
* Update to Visual Studio 2013 (only)
* Update LaTeX colors using CSS Light theme (#278)
