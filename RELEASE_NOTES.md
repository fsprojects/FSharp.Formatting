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