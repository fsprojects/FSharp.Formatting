## 13.0.0

* Remove unused TransformAndOutputDocument from API
* Fixes Can't yet format InlineHtmlBlock #723
* Fixes `<code>` blocks are emitting <pre> blocks with escapes no longer escaped #712

## 12.0.2

* Remove front-matter output from notebooks

## 12.0.1

* Improve package description

## 12.0.0

* [Allow input-->output link translation](https://github.com/fsprojects/FSharp.Formatting/pull/718)

## 11.5.1

* [Allow user-set ids for xmldoc example nodes](https://github.com/fsprojects/FSharp.Formatting/pull/704)

## 11.5.0

* [Remove MSBuild assemblies from library nugets](https://github.com/fsprojects/FSharp.Formatting/pull/715)

## 11.4.4

* [Websocket CPU efficiency improvements](https://github.com/fsprojects/FSharp.Formatting/pull/711)

## 11.4.3

* Style blockquotes

## 11.4.2

* [Download links broken](https://github.com/fsprojects/FSharp.Formatting/issues/696)
* [Duplicating HTML tags for FSX and IPYNB output](https://github.com/fsprojects/FSharp.Formatting/issues/695)

## 11.4.1

* [Fixed navbar scrolling](https://github.com/fsprojects/FSharp.Formatting/issues/672#issuecomment-885532640)

## 11.4.0

* [Fixed some CSS](https://github.com/fsprojects/FSharp.Formatting/pull/688/)

## 11.3.0

* [Bump to FSharp.Compiler.Service 40.0](https://github.com/fsprojects/FSharp.Formatting/pull/682)
* [Fix bottom margin in default CSS](https://github.com/fsprojects/FSharp.Formatting/pull/687)
* [Improve github and signature links](https://github.com/fsprojects/FSharp.Formatting/pull/681)
* [Fix typo in location for custom CSS](https://github.com/fsprojects/FSharp.Formatting/pull/684)

## 11.2.0

* scrollable navbar #677 by nhirschey 
* Show field type for record fields #674
* Add --ignoreprojects flag  #676 by chengh42 

## 11.1.0
* Add frontmatter, category, categoryindex, index, title

## 11.0.4
* testing package publish

## 11.0.3
* testing package publish

## 11.0.2
* add favicon.ico to template and use F# logo as default favicon for generated sites

## 11.0.1
* update to Ionide.ProjInfo
* use computed args for references in API doc generation
* Fix #616
* Fix #662
* Fix #646

## 10.1.1
* Switch to cleaner default styling based on DiffSharp styles
* Change `fsdocs-menu` to `fsdocs-nav`

## 10.0.8
* Add cref copy buttons by default

## 10.0.7
* Fix more formatting and switch to `fsdocs-member-usage` instead of `fsdocs-member-name`

## 10.0.2
* Permit `cref:T:System.Console` code references in markdown content

## 10.0.1
* Apply substitutions to content
* Add `fsdocs-source-filename` and `fsdocs-source-basename` substitutions

## 9.0.4
* Trim spaces from examples (TrimEnd only)

## 9.0.3
* Trim spaces from examples

## 9.0.1
* Proper fix for elide multi-language docs from navigation and site search index

## 9.0.0
* Rename --property flag to --properties
* Elide multi-language docs from navigation and site search index

## 8.0.1
* [Prevent CLI parameters from being discarded](https://github.com/fsprojects/FSharp.Formatting/pull/634)
* [Update Dockerfile and NuGet.config for binder](https://github.com/fsprojects/FSharp.Formatting/pull/636)

## 8.0.0
* [update FCS, allow fsdocs to roll forward to net5.0](https://github.com/fsprojects/FSharp.Formatting/pull/621)
* [Refactor the templating engine and the command tool cache](https://github.com/fsprojects/FSharp.Formatting/pull/615)
* [Refactor the project cracker](https://github.com/fsprojects/FSharp.Formatting/pull/618)
* [Retry project cracking when there isn't a targetPath](https://github.com/fsprojects/FSharp.Formatting/pull/613)
* [Add include-it-raw literate command](https://github.com/fsprojects/FSharp.Formatting/pull/624)
* Add more complete info on how to upgrade
* [CommandTool: add hot reload to the watch command](https://github.com/fsprojects/FSharp.Formatting/pull/629)

## 7.2.9

* Document how to do math in XML comments
* Add --strict flag to fsdocs for stricter checking
* Add --property flag to fsdocs to pass properties to dotnet msbuild
* Better diangostics and logging for fsdocs

## 7.2.8

* [ApiDocs: examples not showing for types and modules](https://github.com/fsprojects/FSharp.Formatting/issues/599)

* Comma-separate interface list in API docs

* Remove untyped Sections from ApiDocComment since individual supported sections are now available

## 7.2.7

* [ApiDocs: cref to members are not resolving to best possible link](https://github.com/fsprojects/FSharp.Formatting/issues/598)

* [ApiDocs: namespace docs are showing in module/type summaries as well](https://github.com/fsprojects/FSharp.Formatting/issues/597)

## 7.2.6

- In ApiDocsModel, separate out the parameter, summary, remarks sections etc.

- In ApiDocsModel, integrate the parameter types with the parameter docs (when using XML docs)

- In HTML generation for API docs, locate the github link top right 

- In ApiDocsModel.Generate, optionally give warnings when XML doc is missing or parameter names are incorrect. Activate using <FsDocsWarnOnMissingDocs>

- In ApiDocsModel, change "Parameters" to "Substitutions"

- Fix formatting of (most) custom operators

- Fix formatting of op_XYZ binary and unary operators 


## 7.2.5

- change `<namespacesummary>...<namespacesummary>` to `<namespacedoc> <summary>... </summary> </namespacedoc>`

- change `<categoryindex>3<categoryindex>` to `<category index="3">...</category>`

## 7.2.4

- support `<namespacesummary>...<namespacesummary>`

- support `<namespaceremarks>...<namespaceremarks>`

- support `<note>...<note>`

- support `<category>...</category>`

- support `<exclude />`

- allow  `<a href="..." >` in XML doc comments

- allow  `<paramref name="..." >` in XML doc comments

- document XML doc things supported

## 7.2.2

- instruct about settings

## 7.2.1

- fix images in nuget

## 7.2.0

- include templates

## 7.1.8

- bump version

## 7.1.6

- bump version

## 7.1.5

- fix navbar position option fixed-left

## 7.1.4

- fixed property computation 

## 7.1.3

- fixed typo for `LICENCE.md`

- all classes to have `fsdocs-` prefix

## 7.1.2

- fixed all classes to have `fsdocs-` prefix

- added documentation on styling

## 7.1.1

- fixed root

## 7.1.0

- add text content of markdown and scripts to generated search index

- overhaul the substitution names used by FSharp.Formatting and expected in the template. The table is in the docs and below

- generate {{fsdocs-list-of-documents}} substitution and use it in both API docs and content

- generate {{fsdocs-list-of-namespaces}} substitution and use it in both API docs and content

- fix link model so {{root}} is always respected

- Add `qualify` parameter that asks to qualify all names by the collection name e.g. FSharp.Core

- Respect per-project settings, e.g. if one nuget package has a different set of authors or home page to another

- Add documentation about styling

- Allow fixed-left and fixed-right positions for the navbar

- Add `{{fsdocs-logo-link}}` parameter to default template

- Add `{{fsdocs-logo-link}}` parameter to default template

- generate HTML giving hyperlinks for types with cross-links

- switch to left bootstrap nav bar in template for a table of contents

- improve sizings

- move to one copy of template in docs/_template.html 

- ApiDocsTypeDefinition and ApiDocsModule merged to ApiDocsEntity

- Default template now expects logo in img/logo.png

- Improvements in default HTML generation

- ApiDocComment.Blurb renamed to ApiDocComment.Summary and only populated with summary text for things read from XML

- simplify tool instructions

- add info about upgrading

## 6.1.0
- fix mistake in laying down `extras` directory 

## 6.0.9
- put extra content in `extras` directory in nuget package and include Dockerfile and NuGet.config

## 6.0.8
- show extended type in generated docs for extension members
- include fsdocs-styles.css, fsdocs-search.js, fsdocs-tips.js in built site 'content' directory by default
- use default template from nuget package by default

## 6.0.7
- fix formatting of generic parameters so they don't show inference variables for members

## 6.0.6
- fix default styling

## 6.0.5
- improve display in FSharp.Formatting API docs and add more information

## 6.0.4
- Watch defaults to `tmp/watch`

## 6.0.3
- Add `(*** include-fsi-output **)`
- Add `(*** include-fsi-merged-output **)`
- Add server to `dotnet watch` and by default switch to local host
- Always inject `fsi.AddPrinter`, `fsi.AddHtmlPrinter` etc. into the programming model for literate scripts

## 6.0.2

- Remove the `api` command from the command line tool (`build` generalises it)
- Add missing search.js

## 6.0.1

- build the Lunr `index.json` from every execution of `fsdocs build`
- Make the search index entries available as part of the ApiDocs model
- Add search box to generated docs
- Add `ApiDocs` prefix to all types in `ApiDocsModel`
- Remove `Details` from `ApiDocsModel`

## 5.0.5

- Correct behaviour of '--clean'

## 5.0.4

- Fix emit of odd character in latex output

## 5.0.3

- Paket update and remove workaround code
- add '--clean' to fsdocs 

## 5.0.2
- Update to FCS v36.0
- Add .ipynb output option for documents
- Add .fsx output option for documents
- Literate.WriteHtml --> Literate.ToHtml/Literate.WriteHtml overloads
- MetadataFormat.Generate --> ApiDocs.GenerateHtml/ApiDocs.GenerateModel overloads
- Fix Literate.* to do approximate (non-razor) templating/  
- Remove Razor support
- HTML templates now use `{{prop-name}}`
- FSharp.CodeFormat --> FSharp.Formatting.CodeFormat
- FSharp.Markdown --> FSharp.Formatting.Markdown
- FSharp.Literate --> FSharp.Formatting.Literate and FSharp.Formatting.Literate.Evaluation
- FSharp.MetadataFormat --> FSharp.Formatting.ApiDocs
- FSharp.ApiDocs uses HTML substitution for templating, no Razor
- Add "include-it" and "include-output" with implied reference to the immediately preceding snippet
- For command line tool
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
- Add `fsdocs build` command to the documentation generator that has lots of sensible defaults.

## 4.1.0
- Support preview F# language features.
- Add support for customizing assigned CSS class.

## 4.0.1
- Add .NET Core support for all libraries.
- Update to FSCS v35.0.
- Add helpers for CustomOperationAttribute.

## 4.0.0-alpha04
- Update to FSCS v34.1.

## 4.0.0-alpha03
- Update to FSCS v34.

## 4.0.0-alpha02
- Fix packaging issues.

## 4.0.0-alpha01
- Add .NET Core support for all libraries.
- Update to latest FSharp.Compiler.Service

## 3.1.0
- remove beta tag since it is already widely used

## 3.0.0-beta14 
 - Update to latest FSharp.Compiler.Service
 - No longer filter FSHarp.Core based on optdata/sigdata (it is now always bundled)
 
## 3.0.0-beta13 
 - FSharp.Formatting.Literate for netstandard2.0

## 3.0.0-beta12 (29, July, 2018)
 - Fix usage formatting - https://github.com/fsprojects/FSharp.Formatting/issues/472

## 3.0.0-beta11 (06, May, 2018)
 - Added support for attributes on modules, types and members
 - Updated razor templates to show attributes and added a warning for obsolete API

## 3.0.0-beta10 (08, April, 2018)
 - Upgrade FSharp.Compiler.Service to be compatible with FAKE 5

## 3.0.0-beta09 (04, February, 2018
 - Fix some links on the website - https://github.com/fsprojects/FSharp.Formatting/pull/458
 - Another link on the website - https://github.com/fsprojects/FSharp.Formatting/pull/454
 - Support highlighting for paket.dependencies `storage` keyword - https://github.com/fsprojects/FSharp.Formatting/pull/451
 - In order to upgrade follow instructions at https://fsprojects.github.io/FSharp.Formatting/upgrade_from_v2_to_v3.html

## 3.0.0-beta08 (03 December, 2017)
 - Improve Stacktrace on Script file processing
 
## 3.0.0-beta07 (07 August, 2017)
 - Fix System.ValueType dep.
 
## 3.0.0-beta06 (16 June, 2017)
 - Include razor component.
 
## 3.0.0-beta05 (11 June, 2017)
 - Always generate anchors when using command line tool.
 
## 3.0.0-beta04 (28 May, 2017)
 - Don't hide errors in fsformatting tool (Literate).
 - Improve error message by using inner exceptions.

## 3.0.0-beta03 (27 May, 2017)
 - Don't hide errors in fsformatting tool.

## 3.0.0-beta02 (26 May, 2017)
 - MarkdownSpan and MarkdownParagraph now use named DUs
 - Add range to MarkdownParagraph and MarkdownSpan (https://github.com/fsprojects/FSharp.Formatting/pull/411)
 - FSharp.Formatting no longer has a strong dependency on Razor (https://github.com/fsprojects/FSharp.Formatting/pull/425)
 - FSharp.Formatting no longer depends on VFPT.Core (https://github.com/fsprojects/FSharp.Formatting/pull/432)
 - Add beta packages to AppVeyor feed.
 - Update FSharp.Compiler.Service component.

## 2.14.4 (3 June, 2016)
 - Use `#I __SOURCE_DIRECTORY__` in the loads script (more reliable)

## 2.14.3 (26 May, 2016)
 - Fixes issues with comments and keywords in Paket highlighter (#408)
 - Fix tooltip flickering in CSS (#406)
 - End blockquote on a line with an empty blockquote (fix #355) (#400)

## 2.14.2 (6 April, 2016)
 - Add code to parse table rows correctly (#394)
 - Also fixes (#388) Markdown parser doesn't recognize inline code `x | y` inside table cell

## 2.14.1 (5 April, 2016)
 - Temporarily pin FSharp.Compiler.Service (#395)
 - Cache is new keyword in Paket (#392)

## 2.13.6 (29 February, 2016)
 - Added TypeScript to the CSharpFormat project (#386)

## 2.13.5 (25 January, 2016)
 - Fixes issues in PaketFormat (#381) - colorize HTTP and file prefix
 - Reliable getTypeLink (#380) - avoid crashes

## 2.13.4 (20 January, 2016)
 - Colors paket keywords (#379)

## 2.13.3 (18 January, 2016)
 - Adds PaketFormat to not color URLs as comments in Paket files (#349)

## 2.13.2 (12 January, 2016)
 - Improve the load script to fix FsLab issue (https://github.com/fslaborg/FsLab/issues/98)

## 2.13.1 (12 January, 2016)
 - Make logging to file optional using environment variable

## 2.13.0 (30 December, 2015)
 - Be compatible with the common-mark spec for 'Fenced code blocks' and 'Indented code blocks'.
   See https://github.com/fsprojects/FSharp.Formatting/pull/343.
   Please follow-up by adding support for more sections of the spec!
   Just add the section to https://github.com/fsprojects/FSharp.Formatting/blob/master/tests/FSharp.Markdown.Tests/CommonMarkSpecTest.fs#L20
   and fix the newly enabled tests.
 - Add CompiledName to members with F# specific naming (https://github.com/fsprojects/FSharp.Formatting/pull/372)

## 2.12.1 (24 December, 2015)
 - update dependencies
 - Upgrade the CommandTool to F# 4 and bundle FSharp.Core with sigdata and optdata.
 - Fix crash when a fenced code block starts with an empty line (https://github.com/fsprojects/FSharp.Formatting/pull/361)
 - Support for all known xml elements (https://github.com/fsprojects/FSharp.Formatting/pull/331)

## 2.12.0 (18 October, 2015)
 - Update dependencies to be compatible with FSharp.Compiler.Service >=1.4.0.3

## 2.11.1-alpha1 (14 October, 2015)
 - Adds methods for cross-type links #330 (https://github.com/fsprojects/FSharp.Formatting/pull/330)

## 2.11.0 (28 September, 2015)
 - Fix https://github.com/fsprojects/FSharp.Formatting/issues/271
 - Don't fail as long as we can recover / continue.
 - Fix https://github.com/fsprojects/FSharp.Formatting/issues/201

## 2.10.3 (12 September, 2015)
 - Require compatible F# Compiler Service in Nuspec (fix #337)

## 2.10.2 (12 September, 2015)
 - Fix load script (wrap logging setup in try catch properly)

## 2.10.1 (12 September, 2015)
 - paket update && fix compilation (#338)
 - Wrap logging setup in try catch

## 2.10.0 (26 July, 2015)
 - Add detailed logging and new FSharp.Formatting.Common.dll file
 - Fix bug in C# code formatting tool (FormatHtml)

## 2.9.10 (27 June, 2015)
 - Support multiple snippets in Literate.Parse (This is obsolete, but needed for www.fssnip.net.)

## 2.9.9 (22 June, 2015)
 - Fix HTML escaping of code blocks with unknown languages (#321, #322)

## 2.9.6 (8 May, 2015)
 - Generate 'fssnip' class for non-F# <pre> tags for consistency

## 2.9.5 (6 May, 2015)
 - Provide an option to disable `fsi.AddPrinter` (#311)
 - Generated line numbers for HTML are the same for F# and non-F#

## 2.9.4 (30 April, 2015)
 - Use `otherFlags` parameter (#308)
 - Format code in Markdown comments (#307, #36)

## 2.9.3 (29 April, 2015)
 - Simplify using FCS interaction using Yaaf.Scripting (#305)
 - Do not load dependencies when initializing evaluator
 - Undo require exact version of F# Compiler Service

## 2.9.2 (24 April, 2015)
 - Require exact version of F# Compiler Service

## 2.9.1 (21 April, 2015)
 - Add back RazorEngine.dll (#302)

## 2.9.0 (20 April, 2015)
 - Properly encode '>' entities (#84)
 - Generate line numbers for non-F# code (#227)
 - Support headings on the same line as comment (#147)
 - Fixes in HTML encoding of non-F# code snippets (#249, #213)
 - Remove Razor mono workaround (#279)
 - Add a public API to process a customized LiterateDocument (#282)
 - Add an API to process a customized LiterateDocument (#289)
 - Use template path if it is rooted (#281)
 - Enable evaluation tests for literate scripts
 - Create `fsi` object without `FSharp.Compiler.Interactive.Settings.dll`
 - Fix #229 (Key already exists exception when parsing an assembly)
 - Update to Visual Studio 2013 (only)
 - Update LaTeX colors using CSS Light theme (#278)

## 2.8.0 (and before 20 April, 2015)
* 1.0.15 - Added latex support, tables and better formatting with line numbers
* 2.0.0 - New project structure, adding MetadataFormat
* 2.0.1 - Fixed handling of # in headers
* 2.0.2 - Change tool tip font for better readability
* 2.0.3 - Fixed Markdown escaping, nested modules and types in FsHtmlDoc
* 2.0.4 - Support escaping in inline code
* 2.1.0-beta - Metadata and literate formatting now support Razor, include templates in NuGet package
* 2.1.1-beta - Fix logo in nuget package
* 2.1.2-beta - Fix nuget package
* 2.1.3-beta - Fix the Root property for templating
* 2.1.4 - Includes templates, support Razor for literate templates, bugs fixed
* 2.1.5 - Improve default templates for open-source projects
* 2.1.6 - Improve default templates for open-source projects (again)
* 2.2.0 - Refactor literate tools
* 2.2.1 - Remove (now unused) error handler parameter in literate (use ParseScript instead)
* 2.2.2 - Nicer CSS style for API reference docs
* 2.2.3 - Better recognition of links
* 2.2.4-beta - Experimental - get snippet from file, some evaluation stuff
* 2.2.5-beta - Add formatting for non-fsharp code, remove indents when importing snippet
* 2.2.6-beta - Add page-source parameter for Razor tempalting
* 2.2.7-beta - Generate docs for some F# types, formatting improvements
* 2.2.8-beta - Add inline Latex support (thanks to Xiang Zhang!)
* 2.2.9-beta - Update templates, support multiple DLLs in metadata format
* 2.2.10-beta - Avoid locking assembly files in AssemblyResolve event
* 2.2.11-beta - Generate links to source code, change default font, move styles to styles folder in package
* 2.2.12-beta - Better compatibility for the default font style
* 2.3.1-beta - Using new compiler services API, improved docs
* 2.3.2-beta - Update to FSharp.Compiler.Service v0.0.10
* 2.3.3-beta - Update FSharp.Compiler.Service and add fsdocs-tool package
* 2.3.4-beta - Fix dependency in NuGet package
* 2.3.5-beta - Omit non-public members from metadata docs by default
* 2.3.6-beta - Update documentation, fixes for Mono compatibility
* 2.3.7-beta - Add auto-formatting for links and output sample usage for DU cases
* 2.3.8-beta - Update FSharp.Compiler.Service to v0.0.17
* 2.3.9-beta - Update FSharp.Compiler.Service to v0.0.20, include inherited members when the base type was ommited from the documentation, fix properties displaying as methods, fix functions with unit input rendering incorrectly
* 2.3.10-beta - Support output embedding in literate scripts
* 2.3.11-beta - Support output embedding in command line tool
* 2.4.0 - Incrementing version and stop using the beta versioning
* 2.4.1 - Support for generating docs for type providers
* 2.4.2 - Improved static parameter support, evaluation and math mode, support XML docs
* 2.4.3 - Fix documentation, nicer github source links template
* 2.4.4 - Live referesh in command line
* 2.4.5 - Include tool tips when generating HTML using WriteHtml
* 2.4.6 - Use --quiet (by default) to avoid calling default printer
* 2.4.7 - Report errors from FsiEvaluator and stylesheet tweaks
* 2.4.8 - Support do-not-eval and expose StdErr from eval failed event
* 2.4.9 - Improve LaTeX formatting and make evaluator customizable
* 2.4.10 - Automatically wrap LaTeX code in math mode blocks
* 2.4.11 - Improved handling of end comments and http/https links at end of lines
* 2.4.12 - Be more flexible about URL generating, protect against exceptions, fix bugs and typos
* 2.4.13 - Fix evaluation bug; Return characters for horizontal lines
* 2.4.14 - Update NuGet dependencies
* 2.4.15 - Support combination of commands (e.g. hide, define-output), add do-not-eval-file
* 2.4.16 - Improve formatting of literate scripts (generate tables around pre)
* 2.4.17 - Fix comment parsing when whitespace
* 2.4.18 - Expose literate paragraph transformation
* 2.4.19 - Update command line tool to .NET 4
* 2.4.20 - Add operator formatting to JavaScript langauge
* 2.4.21 - Update to the most recent F# Compiler Service
* 2.4.22 - Require specific versions in NuGet dependencies
* 2.4.23 - Support generation of anchors in HTML documents
* 2.4.24 - Include stylesheet for anchors & enable this for F# Formatting docs
* 2.4.25 - Version with fixed dependencies, released using Paket!
* 2.4.25 - Use Razor caching to drastically improve performance.
* 2.4.26 - Proper release after nuget incident.
* 2.4.27 - Better mono support and new logo.
* 2.4.28 - Fix dependencies
* 2.4.29 - Revert
* 2.4.30 - Fsharp.Formatting MetadataFormat no longer crashes on C# dlls
* 2.4.31 - Basic cache for RazorRender instances in FSharp.Formatting.Literate
* 2.4.32 - Fixed regressions introduced by latest FCS
* 2.4.33 - Fix cache using incomplete key
* 2.4.34 - Better C# support / Don't depend on broken FCS
* 2.4.36 - Better mono support
* 2.4.37 - Fixed XML file resolution, support "cref", improve XML comment parsing, better logging and C# support
* 2.5.0 - Update to latest FSharp.Compiler.Service
* 2.5.1 - Fix handling of codeblocks inside and after lists
* 2.6.0 - Bundle RazorEngine and System.Web.Razor to avoid dependency clashes
* 2.6.1 - Support for Github flavoured markdown code blocks
* 2.6.2 - Update to a new version of RazorEngine.
* 2.6.3 - Better handling of F# snippets with invalid Unicode characters
* 2.7.0 - Update to .NET 4.5 and use VS Power Tools for highlighting; Support categories on namespaces; Fix newlines
* 2.7.1 - Colorize operators in the default template
* 2.7.2 - Improve colours and ILRepack VS Power Tools (fix #261)
* 2.7.3 - Revert ILRepack - fails on Mono (cc #261)
* 2.7.4 - Add simple load script for easy referencing from FSX files
* 2.7.5 - Update to net45 (fix #266 on windows), add search path to load script, fix EntityFramework bug (#270)
* 2.8.0 - Redesgined file caching for Razor, documentation improvements, marking some thing internal
