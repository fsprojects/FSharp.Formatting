## 2.12.0 (18 October, 2015)
 - Update dependencies to be compatible with FSharp.Compiler.Service >=1.4.0.3

## 2.11.1-alpha1 (14 October, 2015)
 - Adds methods for cross-type links #330 (https://github.com/tpetricek/FSharp.Formatting/pull/330)

## 2.11.0 (28 September, 2015)
 - Fix https://github.com/tpetricek/FSharp.Formatting/issues/271
 - Don't fail as long as we can recover / continue.
 - Fix https://github.com/tpetricek/FSharp.Formatting/issues/201

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
* 2.3.3-beta - Update FSharp.Compiler.Service and add FSharp.Formatting.CommandTool package
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
* 2.4.31 - Basic cache for RazorRender instances in FSharp.Literate
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
