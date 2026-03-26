# Changelog

## [Unreleased]

### Fixed
* Add regression test confirming that types whose name matches their enclosing namespace are correctly included in generated API docs. [#944](https://github.com/fsprojects/FSharp.Formatting/issues/944)
* Fix crash (`failwith "tbd - IndirectImage"`) when `Markdown.ToMd` is called on a document containing reference-style images with bracket syntax. The indirect image is now serialised as `![alt](url)` when the reference is resolved, or in bracket notation when it is not. [#1094](https://github.com/fsprojects/FSharp.Formatting/pull/1094)
* Fix `Markdown.ToMd` serialising `*emphasis*` (italic) spans as `**...**` (bold) instead of `*...*`. [#1102](https://github.com/fsprojects/FSharp.Formatting/pull/1102)
* Fix `Markdown.ToMd` serialising ordered list items with 0-based numbering and no period (e.g. `0 first`) instead of 1-based with a period (e.g. `1. first`). [#1102](https://github.com/fsprojects/FSharp.Formatting/pull/1102)
* Fix `Markdown.ToMd` serialising a multi-paragraph blockquote as multiple separate blockquotes. The blank separator between paragraphs inside a `QuotedBlock` is now emitted as `>` (an empty blockquote line) instead of a plain blank line, so re-parsing the output yields a single `QuotedBlock` with all paragraphs intact. Also eliminates `> ` lines with trailing whitespace that the previous code produced.

### Changed
* Tooltips in generated documentation are now interactive: moving the mouse from a code token into the tooltip keeps it visible, so users can hover over, select, and copy text from the tooltip. The tooltip is dismissed when the mouse leaves it without returning to the originating token. A short hide-delay ensures that moving the mouse from the symbol to a tooltip that is not immediately adjacent (e.g. repositioned to stay inside the viewport) does not dismiss it prematurely. [#949](https://github.com/fsprojects/FSharp.Formatting/issues/949) [#1106](https://github.com/fsprojects/FSharp.Formatting/pull/1106)

## [22.0.0-alpha.2] - 2026-03-13

### Added
* Search dialog now auto-focuses the search input when opened, clears on close, and can be triggered with `Ctrl+K` / `Cmd+K` in addition to `/`.
* Add `dotnet fsdocs convert` command to convert a single `.md`, `.fsx`, or `.ipynb` file to HTML (or another output format) without building a full documentation site. [#811](https://github.com/fsprojects/FSharp.Formatting/issues/811)
* `fsdocs convert` now accepts the input file as a positional argument (e.g. `fsdocs convert notebook.ipynb -o notebook.html`). [#1019](https://github.com/fsprojects/FSharp.Formatting/pull/1019)
* `fsdocs convert` infers the output format from the output file extension when `--outputformat` is not specified (e.g. `-o out.md` implies `--outputformat markdown`). [#1019](https://github.com/fsprojects/FSharp.Formatting/pull/1019)
* `fsdocs convert` now accepts `-o` as a shorthand for `--output`. [#1019](https://github.com/fsprojects/FSharp.Formatting/pull/1019)
* Added full XML doc comments (`<summary>`, `<param>`) to `Literate.ParseAndCheckScriptFile` and `Literate.ParseScriptString` to match the documentation style of the other `Literate.Parse*` methods.

### Fixed
* `Literate.ParseScriptString` and `Literate.ParsePynbString` used a hardcoded Windows path (`C:\script.fsx`) as the fallback script filename when neither `path` nor `rootInputFolder` is supplied. The fallback is now a simple platform-neutral `script.fsx`.
* Add regression tests for cross-assembly tooltip resolution (issue [#1085](https://github.com/fsprojects/FSharp.Formatting/issues/1085)): verify that hover tooltips for types whose fields reference types from other assemblies show the correct type names (not `obj`) when `#r` paths resolve correctly.

### Changed
* Tooltip elements (`div.fsdocs-tip`) now use the [Popover API](https://developer.mozilla.org/en-US/docs/Web/API/Popover_API) (Baseline 2024: Chrome 114+, Firefox 125+, Safari 17+). Tooltips are placed in the browser's top layer — no `z-index` needed, always above all other content. Fixes a positioning bug where tooltips appeared offset when the page was scrolled. The previous `display`-toggle fallback has been removed. Tooltips also fade in with a subtle animation. [#422](https://github.com/fsprojects/FSharp.Formatting/issues/422), [#1061](https://github.com/fsprojects/FSharp.Formatting/pull/1061)
* Generated code tokens no longer use inline `onmouseover`/`onmouseout` event handlers. Tooltips are now triggered via `data-fsdocs-tip` / `data-fsdocs-tip-unique` attributes and a delegated event listener in `fsdocs-tips.js`. The `popover` attribute is also added to API-doc tooltip divs so they use the same top-layer path. [#1061](https://github.com/fsprojects/FSharp.Formatting/pull/1061)
* Changed `range` fields in `MarkdownSpan` and `MarkdownParagraph` DU cases from `MarkdownRange option` to `MarkdownRange`, using `MarkdownRange.zero` as the default/placeholder value instead of `None`.
* When no template is provided (e.g. `fsdocs convert` without `--template`), `fsdocs-tip` tooltip divs are no longer included in the output. Tooltips require JavaScript/CSS from a template to function, so omitting them produces cleaner raw output. [#1019](https://github.com/fsprojects/FSharp.Formatting/pull/1019)
* Use [`scrollbar-gutter: stable`](https://developer.mozilla.org/en-US/docs/Web/CSS/scrollbar-gutter) (Baseline 2024) on scroll containers (`main`, `#fsdocs-main-menu`, mobile menu, search dialog) to reserve scrollbar space and prevent layout shifts when content changes height. Also adds the missing `overflow-y: auto` to `main` so pages that exceed the viewport height are independently scrollable. [#1087](https://github.com/fsprojects/FSharp.Formatting/issues/1087), [#1088](https://github.com/fsprojects/FSharp.Formatting/pull/1088)

## [22.0.0-alpha.1] - 2026-03-03

### Added
* Add `ApiDocParameter` and `ApiDocReturnInfo` named record types to replace anonymous records returned by `ApiDocMember.Parameters` and `ApiDocMember.ReturnInfo`, making them usable across assembly boundaries. [#735](https://github.com/fsprojects/FSharp.Formatting/issues/735)
* Add `///` documentation comments to all public types, modules and members, and succinct internal comments, as part of ongoing effort to document the codebase. [#1035](https://github.com/fsprojects/FSharp.Formatting/issues/1035)
* Add "Copy" button to all code blocks in generated documentation, making it easy to copy code samples to the clipboard. [#72](https://github.com/fsprojects/FSharp.Formatting/issues/72)
* Add `<FsDocsAllowExecutableProject>true</FsDocsAllowExecutableProject>` project file setting to include executable projects (OutputType=Exe/WinExe) in API documentation generation. [#918](https://github.com/fsprojects/FSharp.Formatting/issues/918)
* Add `{{fsdocs-logo-alt}}` substitution (configurable via `<FsDocsLogoAlt>` MSBuild property, defaults to `Logo`) for accessible alt text on the header logo image. [#626](https://github.com/fsprojects/FSharp.Formatting/issues/626)
* Add `fsdocs init` command to scaffold a minimal `docs/index.md` (and optionally `_template.html`) for new projects. [#872](https://github.com/fsprojects/FSharp.Formatting/issues/872)
* `IFsiEvaluator` now inherits `IDisposable`; `FsiEvaluator` disposes its underlying FSI session when disposed, preventing session leaks in long-running processes. [#341](https://github.com/fsprojects/FSharp.Formatting/issues/341)
* Display of type constraints (e.g. `'T : equality`, `'T : comparison`, `'T :> IComparable`) in generated API documentation. Constraints are shown inline using the F# compiler's compact `(requires ...)` style by default (e.g. `'T (requires equality)`). Controlled by `<FsDocsTypeConstraints>` in the project file with values `None`, `Short` (default, inline compact form), and `Full` (separate "Constraints:" section). [#591](https://github.com/fsprojects/FSharp.Formatting/issues/591)
* Show inherited members from documented base types in a new "Inherited members" section on type pages (MSDN-style). [#590](https://github.com/fsprojects/FSharp.Formatting/issues/590)
* Add `<FsDocsNoInheritedMembers>true</FsDocsNoInheritedMembers>` project file setting to suppress "Inherited from" sections in generated API docs. [#1039](https://github.com/fsprojects/FSharp.Formatting/pull/1039)
* Generate `llms.txt` and `llms-full.txt` for LLM consumption by default (opt out via `<FsDocsGenerateLlmsTxt>false</FsDocsGenerateLlmsTxt>`); when enabled, markdown output is always generated alongside HTML (even without a user-provided `_template.md`) and `llms.txt` links point to the `.md` files. [#951](https://github.com/fsprojects/FSharp.Formatting/issues/951) [#980](https://github.com/fsprojects/FSharp.Formatting/pull/980)
* Document `--saveimages` flag (`none`|`some`|`all`) with an explanation of each mode, and add a new "Embedding Images" section covering inline Base64 images and `fsi.AddHtmlPrinter` usage for chart/plot output. [#683](https://github.com/fsprojects/FSharp.Formatting/issues/683)

### Fixed
* Strip parameter attribute annotations (e.g. `[<Optional>]`, `[<DefaultParameterValue(null)>]`) from hover tooltips in code snippets — these attributes made tooltips unreadable for methods with many optional parameters. [#858](https://github.com/fsprojects/FSharp.Formatting/issues/858)
* Update `Ionide.ProjInfo` from 0.62.0 to 0.74.2, fixing a URI format exception in `VisualTree.relativePathOf` when paths contain unusual characters; migrate to the new `WorkspaceLoader` API and remove the now-defunct `Ionide.ProjInfo.Sln` package. [#1054](https://github.com/fsprojects/FSharp.Formatting/issues/1054)
* Fix project restore detection for projects with nonstandard artifact locations (e.g. `<UseArtifactsOutput>` or the dotnet/fsharp repo layout): when the MSBuild call to locate `project.assets.json` fails, emit a warning and proceed instead of hard-failing. [#592](https://github.com/fsprojects/FSharp.Formatting/issues/592)
* Fix doc generation failure for members with 5D/6D+ array parameters by correctly formatting array type signatures in XML doc format (e.g. `System.Double[0:,0:,0:,0:,0:]` for a 5D array). [#702](https://github.com/fsprojects/FSharp.Formatting/issues/702)
* Fix `_menu_template.html` and `_menu-item_template.html` being copied to the output directory. [#803](https://github.com/fsprojects/FSharp.Formatting/issues/803)
* Fix `ApiDocMember.Details.ReturnInfo.ReturnType` returning `None` for properties that have both a getter and a setter. [#734](https://github.com/fsprojects/FSharp.Formatting/issues/734)
* Improve error message when a named code snippet is not found (e.g. `(*** include:name ***)` with undefined name now reports the missing name clearly). [#982](https://github.com/fsprojects/FSharp.Formatting/pull/982)
* HTML-encode XML doc text nodes and unresolved `<see cref>` values to prevent HTML injection and fix broken output when docs contain characters like `<`, `>`, or backticks in generic type notation. [#748](https://github.com/fsprojects/FSharp.Formatting/issues/748)
* Add uppercase output kind extension (e.g. `HTML`, `IPYNB`) to `ConditionalDefines` so that `#if HTML` and `(*** condition: HTML ***)` work alongside their lowercase variants. [#693](https://github.com/fsprojects/FSharp.Formatting/issues/693)
* Strip `#if SYMBOL` / `#endif // SYMBOL` marker lines from `LiterateCode` source before syntax-highlighting so they do not appear in formatted output. [#693](https://github.com/fsprojects/FSharp.Formatting/issues/693)
* Improve tolerant cross-reference resolution so that unqualified `<see cref>` attributes (e.g. `<see cref="MyType" />`, `<see cref="MyType.MyMember" />`, `<see cref="GenericType`1.Member" />`) resolve to the correct API documentation page when the referenced type is part of the documented assembly set. [#605](https://github.com/fsprojects/FSharp.Formatting/issues/605)
* Members and functions annotated with `CompilerMessageAttribute(IsHidden=true)` are now automatically excluded from API docs, matching the behaviour of `[omit]` / `<exclude/>`. [#144](https://github.com/fsprojects/FSharp.Formatting/issues/144)
* Fix incorrect column ranges for inline spans (links, images, inline code) in the Markdown parser — spans and subsequent literals now report correct `StartColumn`/`EndColumn` values. [#744](https://github.com/fsprojects/FSharp.Formatting/issues/744)
* Normalize `--projects` paths to absolute paths before passing to the project cracker, fixing failures when relative paths are supplied. [#793](https://github.com/fsprojects/FSharp.Formatting/issues/793)
* Fix incorrect paragraph indentation for loose list items: a paragraph indented at the outer list item's continuation level is now correctly treated as a sibling of surrounding sublists rather than being absorbed into the first sublist item's body. [#347](https://github.com/fsprojects/FSharp.Formatting/issues/347)
* Improve CommonMark compliance for ATX headings: reject `#` not followed by a space (e.g. `#NoSpace` is now a paragraph), reject more than 6 `#` characters as a heading, support 0–3 leading spaces before the opening `#` sequence, and fix empty content when the entire header body is a closing `###` sequence. [#191](https://github.com/fsprojects/FSharp.Formatting/issues/191)
* Improve CommonMark compliance for thematic breaks, setext headings, and paragraph/list/blockquote interaction: thematic breaks now correctly interrupt paragraphs, list items, and lazy blockquote continuations; setext heading underlines now accept 0–3 leading spaces; and thematic breaks with 4+ leading spaces are no longer recognised (they are indented code blocks instead). [#191](https://github.com/fsprojects/FSharp.Formatting/issues/191)

### Changed
* Markdown API docs for members now use section-based layout (per-member `####` headings) instead of a Markdown table, eliminating embedded `<br />` separators, `&#124;` pipe escaping, and improving rendering of multi-line content and code examples. [#725](https://github.com/fsprojects/FSharp.Formatting/issues/725)
* Update FCS to 43.10.100. [#935](https://github.com/fsprojects/FSharp.Formatting/pull/966)
* Reduce dark mode header border contrast to match the visual subtlety of light mode borders. [#885](https://github.com/fsprojects/FSharp.Formatting/issues/885)
* **breaking** Migrate theme color variables to use CSS `light-dark()` function, eliminating the separate `[data-theme=dark]` block of variable overrides and automatically honouring `prefers-color-scheme` media query when the user has not manually set a preference. [#1004](https://github.com/fsprojects/FSharp.Formatting/issues/1004)

## [21.0.0] - 2025-11-12

### Changed
* Stable release.

## [21.0.0-beta-005] - 2025-04-23

### Added
* Add --ignoreuncategorized flag. [#953](https://github.com/fsprojects/FSharp.Formatting/pull/953)

## [21.0.0-beta-004] - 2024-11-20

### Changed
* Update FCS to 43.9.100. [#945](https://github.com/fsprojects/FSharp.Formatting/pull/945)

## [21.0.0-beta-003] - 2024-08-06

### Changed
* Update FCS to 43.8.301. [#935](https://github.com/fsprojects/FSharp.Formatting/pull/935)

## [21.0.0-beta-002] - 2024-06-19

### Changed
* Shrink API docs example heading font size a bit. [#923](https://github.com/fsprojects/FSharp.Formatting/pull/923)
* Improve overall API doc content alignment consistency in various scenarios. [#923](https://github.com/fsprojects/FSharp.Formatting/pull/923)

## [21.0.0-beta-001] - 2024-06-06

### Added
* Add expand/collapse-all button for API doc details. [#920](https://github.com/fsprojects/FSharp.Formatting/pull/920)

### Changed
* HTML structure of generated API documentation. [#919](https://github.com/fsprojects/FSharp.Formatting/pull/919)

## [20.0.1] - 2024-05-31

### Changed
* Details improvements. [#917](https://github.com/fsprojects/FSharp.Formatting/pull/917)

## [20.0.0] - 2024-02-14

### Changed
* Stable release.

## [20.0.0-beta-002] - 2024-02-08

### Fixed
* Avoid theme flicker in dark mode. [#901](https://github.com/fsprojects/FSharp.Formatting/pull/901)

## [20.0.0-beta-001] - 2024-01-31

### Changed
* Marking development of v20 as complete.

## [20.0.0-alpha-019] - 2024-01-29

### Fixed
* Use dvh for full viewport height . [#899](https://github.com/fsprojects/FSharp.Formatting/pull/899)

## [20.0.0-alpha-018] - 2024-01-10

### Fixed
* Add -webkit-text-size-adjust. [#889](https://github.com/fsprojects/FSharp.Formatting/issues/889)

## [20.0.0-alpha-017] - 2024-01-09

### Fixed
* Set default font-size for code. [#889](https://github.com/fsprojects/FSharp.Formatting/issues/889)

## [20.0.0-alpha-016] - 2023-12-07

### Fixed
* Use empty replacement for `{{fsdocs-meta-tags}` in API doc pages. [#892](https://github.com/fsprojects/FSharp.Formatting/pull/892)

## [20.0.0-alpha-015] - 2023-12-06

### Fixed
* Namespace description overflows content box. [#886](https://github.com/fsprojects/FSharp.Formatting/issues/886)

### Added
* SEO-optimization for new theme. Allow `description` and `keywords` in frontmatter. Introduce `{{fsdocs-meta-tags}}`. [#869](https://github.com/fsprojects/FSharp.Formatting/issues/869)

## [20.0.0-alpha-014] - 2023-11-22

### Added
* Added the ability to use ipynb files as inputs [#874](https://github.com/fsprojects/FSharp.Formatting/pull/874)

### Fixed
* Fsx outputs no longer treat inline html as F# code. Inline html blocks are now enclosed inside literate comments. 

## [20.0.0-alpha-013] - 2023-11-21

### Added
* Add more options to customize colors.

### Removed
* `--fsdocs-theme-toggle-light-color` and `--fsdocs-theme-toggle-dark-color` are now deprecated. Use `--header-link-color` instead.
* `<FsDocsCollectionNameLink>`

### Changed
* Update FCS to 43.8.100

## [20.0.0-alpha-012] - 2023-11-17

### Added
* Add more options to customize colors.

## [20.0.0-alpha-011] - 2023-11-16

### Fixed
* Take `<UseArtifactsOutput>` into account during the project restore check.

## [20.0.0-alpha-010] - 2023-11-15

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

## [20.0.0-alpha-009] - 2023-11-11

### Fixed
* Return original prop when no Directory.Build.props is used as fallback.

## [20.0.0-alpha-008] - 2023-11-10

### Fixed
* Add dynamic `max-width` to tooltip.
* Overflow long namespace names in overview table.

## [20.0.0-alpha-007] - 2023-11-10

### Fixed
* Smaller scrollbars on mobile devices

### Added
* Use property values from the current `Directory.Build.props` file as fallback. [#865](https://github.com/fsprojects/FSharp.Formatting/issues/865)

## [20.0.0-alpha-006] - 2023-11-09

### Added
* Revisited search using [fusejs](https://www.fusejs.io/)

## [20.0.0-alpha-005] - 2023-11-09

### Changed
* Improve API doc styling.

### Fixed
* Make mobile menu scrollable.

## [20.0.0-alpha-004] - 2023-11-08

### Fixed
* Don't use font ligatures, the can confuse newcomers of F#.
* Replace `{{fsdocs-list-of-namespaces}}` with an empty string if no API docs are present.
* Improve default styling of `blockquote`
* Add some padding for level 3 and 4 headers in 'on this page' section.

## [20.0.0-alpha-003] - 2023-11-06

### Changed
* default template style changes (`#fsdocs-page-menu` outside `main`, link around project name, overflow ellipsis for menu items)

## [20.0.0-alpha-002] - 2023-11-03

### Fixed
* `{{root}}` is now available as substitution in `_body.html` and `_head.html`.

## [20.0.0-alpha-001] - 2023-11-03

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
* If menu templating is used, `{{fsdocs-menu-header-active-class}}` and `{{fsdocs-menu-item-active-class}}` are avaiable.
* `{{fsdocs-page-content-list}}` contains an unordered list of the header (`h1` till `h4`) of the current page. (if available)

## [19.1.1] - 2023-10-10


### Changed
* Fix code rendering on firefox. [#851](https://github.com/fsprojects/FSharp.Formatting/pull/851)

## [19.1.0] - 2023-09-15


### Changed
* Only reload css file when changed. [#845](https://github.com/fsprojects/FSharp.Formatting/pull/845)
* Add previous and next page url substitutions. [#846](https://github.com/fsprojects/FSharp.Formatting/pull/846)

## [19.0.0] - 2023-08-22


### Changed
* Update FCS to 43.7.400

## [18.1.1] - 2023-08-02


### Changed
* Pass `--multiemit-` as default option for `FsiEvaluator`. 

## [18.1.0] - 2023-04-13


### Changed
* Collapsible ApiDocs member info [#778](https://github.com/fsprojects/FSharp.Formatting/issues/778). The issue was fixed collaboratively in an [Amplifying F# session](https://amplifying-fsharp.github.io/) with a recording that can be found [here](https://amplifying-fsharp.github.io/sessions/2023/03/31/).

## [18.0.0] - 2023-03-29


### Changed
* Update FCS to 43.7.200
* Target `net6.0` for `fsdocs-tool` [#799](https://github.com/fsprojects/FSharp.Formatting/issues/799)

## [17.4.1] - 2023-03-29


### Changed
* Update ipynb output metadata [#809](https://github.com/fsprojects/FSharp.Formatting/issues/809)

## [17.4.0] - 2023-03-09


### Changed
* One FSI evaluator per docs file [#737](https://github.com/fsprojects/FSharp.Formatting/issues/737)

## [17.3.0] - 2023-03-06


### Changed
* Better test project detection [#800](https://github.com/fsprojects/FSharp.Formatting/issues/800)

## [17.2.3] - 2023-02-21


### Changed
* Fix external docs link [#794](https://github.com/fsprojects/FSharp.Formatting/issues/794)

## [17.2.2] - 2023-01-16


### Changed
* Improvement for `<seealso/>` [#789](https://github.com/fsprojects/FSharp.Formatting/issues/789)

## [17.2.1] - 2023-01-14


### Changed
* Fix support for `<exclude/>` [#786](https://github.com/fsprojects/FSharp.Formatting/issues/786)

## [17.2.0] - 2022-12-28


### Changed
* Resolve markdown links in raw html [#769](https://github.com/fsprojects/FSharp.Formatting/issues/769)

## [17.1.0] - 2022-11-22


### Changed
* [Add syntax highlighting to API docs](https://github.com/fsprojects/FSharp.Formatting/pull/780)

## [17.0.0] - 2022-11-17


### Changed
* Update to .NET 7.0.100

## [16.1.1] - 2022-09-07


### Changed
* [Fix arguments naming and escape operator name in usageHtml](https://github.com/fsprojects/FSharp.Formatting/pull/765/)

## [16.1.0] - 2022-08-30


### Changed
* Update to .NET 6.0.400
* Update to Ionide.ProjInfo 0.60

## [16.0.4] - 2022-08-30


### Changed
* [Fix indexers in output](https://github.com/fsprojects/FSharp.Formatting/pull/767)

## [16.0.3] - 2022-08-30

### Changed
* [Fix link translation when using relative input path](https://github.com/fsprojects/FSharp.Formatting/issues/764)

## [16.0.2] - 2022-08-23

### Changed
* [Improves markdown emphasis parsing.](https://github.com/fsprojects/FSharp.Formatting/pull/763)

## [16.0.1] - 2023-08-16

### Changed
* Custom templating for menus 

## [15.0.3] - 2023-08-15

### Changed
* Fixes Markdown parser gets multiple-underscores-inside-italics wrong [#389](https://github.com/fsprojects/FSharp.Formatting/issues/389)

## [15.0.2] - 2023-08-05

### Changed
* Trim the `--fscoptions` before passing them as `otherflags`. ([comment #616](https://github.com/fsprojects/FSharp.Formatting/issues/616#issuecomment-1200877765))

## [15.0.1] - 2023-07-01

### Changed
* fix https://github.com/fsprojects/FSharp.Formatting/issues/749

## [15.0.0] - 2022-03-20

### Changed
* Update to .NET 6

## [14.0.1] - 2021-11-11

### Changed
* Fixes 703, 700 - `--strict` is now considerably stricter, and more diagnostics being shown

## [14.0.0] - 2021-11-10


### Changed
* Fix [Getting ReturnType from ApiDocMember without Html already embedded](https://github.com/fsprojects/FSharp.Formatting/issues/708)

## [13.0.1] - 2021-11-10


### Changed
* Skip the output folder when processing

## [13.0.0] - 2021-11-10


### Changed
* Remove unused TransformAndOutputDocument from API
* Fixes Can't yet format InlineHtmlBlock #723
* Fixes `<code>` blocks are emitting `<pre>` blocks with escapes no longer escaped #712

## [12.0.2] - 2021-11-10


### Changed
* Remove front-matter output from notebooks

## [12.0.1] - 2021-11-10


### Changed
* Improve package description

## [12.0.0] - 2021-11-07


### Changed
* [Allow input-->output link translation](https://github.com/fsprojects/FSharp.Formatting/pull/718)

## [11.5.1] - 2021-10-30


### Changed
* [Allow user-set ids for xmldoc example nodes](https://github.com/fsprojects/FSharp.Formatting/pull/704)

## [11.5.0] - 2021-10-30


### Changed
* [Remove MSBuild assemblies from library nugets](https://github.com/fsprojects/FSharp.Formatting/pull/715)

## [11.4.4] - 2021-10-11


### Changed
* [Websocket CPU efficiency improvements](https://github.com/fsprojects/FSharp.Formatting/pull/711)

## [11.4.3] - 2021-08-17


### Changed
* Style blockquotes

## [11.4.2] - 2021-07-29


### Changed
* [Download links broken](https://github.com/fsprojects/FSharp.Formatting/issues/696)
* [Duplicating HTML tags for FSX and IPYNB output](https://github.com/fsprojects/FSharp.Formatting/issues/695)

## [11.4.1] - 2021-07-23


### Changed
* [Fixed navbar scrolling](https://github.com/fsprojects/FSharp.Formatting/issues/672#issuecomment-885532640)

## [11.4.0] - 2021-07-22


### Changed
* [Fixed some CSS](https://github.com/fsprojects/FSharp.Formatting/pull/688/)

## [11.3.0] - 2021-07-22


### Changed
* [Bump to FSharp.Compiler.Service 40.0](https://github.com/fsprojects/FSharp.Formatting/pull/682)
* [Fix bottom margin in default CSS](https://github.com/fsprojects/FSharp.Formatting/pull/687)
* [Improve github and signature links](https://github.com/fsprojects/FSharp.Formatting/pull/681)
* [Fix typo in location for custom CSS](https://github.com/fsprojects/FSharp.Formatting/pull/684)

## [11.2.0] - 2021-05-17


### Changed
* scrollable navbar #677 by nhirschey 
* Show field type for record fields #674
* Add --ignoreprojects flag  #676 by chengh42 

## [11.1.0] - 2021-04-15

### Changed
* Add frontmatter, category, categoryindex, index, title

## [11.0.4] - 2021-04-15

### Changed
* testing package publish

## [11.0.3] - 2021-04-14

### Changed
* testing package publish

## [11.0.2] - 2021-04-14

### Changed
* add favicon.ico to template and use F# logo as default favicon for generated sites

## [11.0.1] - 2021-04-14

### Changed
* update to Ionide.ProjInfo
* use computed args for references in API doc generation
* Fix #616
* Fix #662
* Fix #646

## [10.1.1] - 2021-04-13

### Changed
* Switch to cleaner default styling based on DiffSharp styles
* Change `fsdocs-menu` to `fsdocs-nav`

## [10.0.8] - 2021-04-13

### Changed
* Add cref copy buttons by default

## [10.0.7] - 2021-04-13

### Changed
* Fix more formatting and switch to `fsdocs-member-usage` instead of `fsdocs-member-name`

## [10.0.2] - 2021-04-13

### Changed
* Permit `cref:T:System.Console` code references in markdown content

## [10.0.1] - 2021-04-12

### Changed
* Apply substitutions to content
* Add `fsdocs-source-filename` and `fsdocs-source-basename` substitutions

## [9.0.4] - 2021-03-24

### Changed
* Trim spaces from examples (TrimEnd only)

## [9.0.3] - 2021-03-24

### Changed
* Trim spaces from examples

## [9.0.1] - 2021-02-11

### Changed
* Proper fix for elide multi-language docs from navigation and site search index

## [9.0.0] - 2021-02-11

### Changed
* Rename --property flag to --properties
* Elide multi-language docs from navigation and site search index

## [8.0.1] - 2021-01-21

### Changed
* [Prevent CLI parameters from being discarded](https://github.com/fsprojects/FSharp.Formatting/pull/634)
* [Update Dockerfile and NuGet.config for binder](https://github.com/fsprojects/FSharp.Formatting/pull/636)

## [8.0.0] - 2021-01-14

### Changed
* [update FCS, allow fsdocs to roll forward to net5.0](https://github.com/fsprojects/FSharp.Formatting/pull/621)
* [Refactor the templating engine and the command tool cache](https://github.com/fsprojects/FSharp.Formatting/pull/615)
* [Refactor the project cracker](https://github.com/fsprojects/FSharp.Formatting/pull/618)
* [Retry project cracking when there isn't a targetPath](https://github.com/fsprojects/FSharp.Formatting/pull/613)
* [Add include-it-raw literate command](https://github.com/fsprojects/FSharp.Formatting/pull/624)
* Add more complete info on how to upgrade
* [CommandTool: add hot reload to the watch command](https://github.com/fsprojects/FSharp.Formatting/pull/629)

## [7.2.9] - 2020-09-22


### Changed
* Document how to do math in XML comments
* Add --strict flag to fsdocs for stricter checking
* Add --property flag to fsdocs to pass properties to dotnet msbuild
* Better diangostics and logging for fsdocs

## [7.2.8] - 2020-09-09


### Changed
* [ApiDocs: examples not showing for types and modules](https://github.com/fsprojects/FSharp.Formatting/issues/599)

* Comma-separate interface list in API docs

* Remove untyped Sections from ApiDocComment since individual supported sections are now available

## [7.2.7] - 2020-09-09


### Changed
* [ApiDocs: cref to members are not resolving to best possible link](https://github.com/fsprojects/FSharp.Formatting/issues/598)

* [ApiDocs: namespace docs are showing in module/type summaries as well](https://github.com/fsprojects/FSharp.Formatting/issues/597)

## [7.2.6] - 2020-08-07


### Changed
* In ApiDocsModel, separate out the parameter, summary, remarks sections etc.

* In ApiDocsModel, integrate the parameter types with the parameter docs (when using XML docs)

* In HTML generation for API docs, locate the github link top right 

* In ApiDocsModel.Generate, optionally give warnings when XML doc is missing or parameter names are incorrect. Activate using <FsDocsWarnOnMissingDocs>

* In ApiDocsModel, change "Parameters" to "Substitutions"

* Fix formatting of (most) custom operators

* Fix formatting of op_XYZ binary and unary operators 

## [7.2.5] - 2020-08-06


### Changed
* change `<namespacesummary>...<namespacesummary>` to `<namespacedoc> <summary>... </summary> </namespacedoc>`

* change `<categoryindex>3<categoryindex>` to `<category index="3">...</category>`

## [7.2.4] - 2020-08-06


### Changed
* support `<namespacesummary>...<namespacesummary>`

* support `<namespaceremarks>...<namespaceremarks>`

* support `<note>...<note>`

* support `<category>...</category>`

* support `<exclude />`

* allow  `<a href="..." >` in XML doc comments

* allow  `<paramref name="..." >` in XML doc comments

* document XML doc things supported

## [7.2.2] - 2020-08-05


### Changed
* instruct about settings

## [7.2.1] - 2020-08-05


### Changed
* fix images in nuget

## [7.2.0] - 2020-08-05


### Changed
* include templates

## [7.1.8] - 2020-08-05


### Changed
* bump version

## [7.1.6] - 2020-08-04


### Changed
* bump version

## [7.1.5] - 2020-08-04


### Changed
* fix navbar position option fixed-left

## [7.1.4] - 2020-08-04


### Changed
* fixed property computation 

## [7.1.3] - 2020-08-04


### Changed
* fixed typo for `LICENCE.md`

* all classes to have `fsdocs-` prefix

## [7.1.2] - 2020-08-04


### Changed
* fixed all classes to have `fsdocs-` prefix

* added documentation on styling

## [7.1.1] - 2020-08-04


### Changed
* fixed root

## [7.1.0] - 2020-08-04


### Changed
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

## [6.1.0] - 2020-07-21

### Changed
* fix mistake in laying down `extras` directory 

## [6.0.9] - 2020-07-21

### Changed
* put extra content in `extras` directory in nuget package and include Dockerfile and NuGet.config

## [6.0.8] - 2020-07-21

### Changed
* show extended type in generated docs for extension members
* include fsdocs-styles.css, fsdocs-search.js, fsdocs-tips.js in built site 'content' directory by default
* use default template from nuget package by default

## [6.0.7] - 2020-07-20

### Changed
* fix formatting of generic parameters so they don't show inference variables for members

## [6.0.6] - 2020-07-20

### Changed
* fix default styling

## [6.0.5] - 2020-07-20

### Changed
* improve display in FSharp.Formatting API docs and add more information

## [6.0.4] - 2020-07-20

### Changed
* Watch defaults to `tmp/watch`

## [6.0.3] - 2020-07-20

### Changed
* Add `(*** include-fsi-output **)`
* Add `(*** include-fsi-merged-output **)`
* Add server to `dotnet watch` and by default switch to local host
* Always inject `fsi.AddPrinter`, `fsi.AddHtmlPrinter` etc. into the programming model for literate scripts

## [6.0.2] - 2020-07-19


### Changed
* Remove the `api` command from the command line tool (`build` generalises it)
* Add missing search.js

## [6.0.1] - 2020-07-19


### Changed
* build the Lunr `index.json` from every execution of `fsdocs build`
* Make the search index entries available as part of the ApiDocs model
* Add search box to generated docs
* Add `ApiDocs` prefix to all types in `ApiDocsModel`
* Remove `Details` from `ApiDocsModel`

## [5.0.5] - 2020-07-14


### Changed
* Correct behaviour of '--clean'

## [5.0.4] - 2020-07-14


### Changed
* Fix emit of odd character in latex output

## [5.0.3] - 2020-07-14


### Changed
* Paket update and remove workaround code
* add '--clean' to fsdocs 

## [5.0.2] - 2020-07-14

### Changed
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
* Rename fsformatting to fsdocs
* Update command line parser
* "fsformatting literate process-directory" --> "fsdocs convert"
* "fsformatting metadata-format generate" --> "fsdocs api"
* "--dllFiles" --> "--dlls"
* "--outDir" --> "--output"
* "--outputDirectory" --> "--output"
* "--output" is optional (defaults to 'output')
* "--inputDirectory" --> "--input"
* Add --nonpublic
* Add --xmlComments
* Automatically populate metadata from project settings.
* Add `fsdocs build` command to the documentation generator that has lots of sensible defaults.

## [4.1.0] - 2020-06-01

### Changed
* Support preview F# language features.
* Add support for customizing assigned CSS class.

## [4.0.1] - 2020-05-12

### Changed
* Add .NET Core support for all libraries.
* Update to FSCS v35.0.
* Add helpers for CustomOperationAttribute.

## [4.0.0-rc2] - 2020-04-24

### Changed
* Update to FSCS v34.1.

## [4.0.0-rc2] - 2020-04-24

### Changed
* Update to FSCS v34.

## [4.0.0-rc2] - 2020-04-24

### Changed
* Fix packaging issues.

## [4.0.0-rc2] - 2020-04-24

### Changed
* Add .NET Core support for all libraries.
* Update to latest FSharp.Compiler.Service

## [3.1.0] - 2019-04-12

### Changed
* remove beta tag since it is already widely used

## [3.0.0-beta01] - 2016-08-01

### Changed
* Update to latest FSharp.Compiler.Service
* No longer filter FSHarp.Core based on optdata/sigdata (it is now always bundled)

## [3.0.0-beta01] - 2016-08-01

### Changed
* FSharp.Formatting.Literate for netstandard2.0

## [3.0.0-beta01] - 2016-08-01

### Changed
* Fix usage formatting - https://github.com/fsprojects/FSharp.Formatting/issues/472

## [3.0.0-beta01] - 2016-08-01

### Changed
* Added support for attributes on modules, types and members
* Updated razor templates to show attributes and added a warning for obsolete API

## [3.0.0-beta01] - 2016-08-01

### Changed
* Upgrade FSharp.Compiler.Service to be compatible with FAKE 5

## [3.0.0-beta01] - 2016-08-01

### Changed
* Fix some links on the website - https://github.com/fsprojects/FSharp.Formatting/pull/458
* Another link on the website - https://github.com/fsprojects/FSharp.Formatting/pull/454
* Support highlighting for paket.dependencies `storage` keyword - https://github.com/fsprojects/FSharp.Formatting/pull/451
* In order to upgrade follow instructions at https://fsprojects.github.io/FSharp.Formatting/upgrade_from_v2_to_v3.html

## [3.0.0-beta01] - 2016-08-01

### Changed
* Improve Stacktrace on Script file processing

## [3.0.0-beta01] - 2016-08-01

### Changed
* Fix System.ValueType dep.

## [3.0.0-beta01] - 2016-08-01

### Changed
* Include razor component.

## [3.0.0-beta01] - 2016-08-01

### Changed
* Always generate anchors when using command line tool.

## [3.0.0-beta01] - 2016-08-01

### Changed
* Don't hide errors in fsformatting tool (Literate).
* Improve error message by using inner exceptions.

## [3.0.0-beta01] - 2016-08-01

### Changed
* Don't hide errors in fsformatting tool.

## [3.0.0-beta01] - 2016-08-01

### Changed
* MarkdownSpan and MarkdownParagraph now use named DUs
* Add range to MarkdownParagraph and MarkdownSpan (https://github.com/fsprojects/FSharp.Formatting/pull/411)
* FSharp.Formatting no longer has a strong dependency on Razor (https://github.com/fsprojects/FSharp.Formatting/pull/425)
* FSharp.Formatting no longer depends on VFPT.Core (https://github.com/fsprojects/FSharp.Formatting/pull/432)
* Add beta packages to AppVeyor feed.
* Update FSharp.Compiler.Service component.

## [2.14.4] - 2016-06-02

### Changed
* Use `#I __SOURCE_DIRECTORY__` in the loads script (more reliable)

## [2.14.3] - 2016-05-26

### Changed
* Fixes issues with comments and keywords in Paket highlighter (#408)
* Fix tooltip flickering in CSS (#406)
* End blockquote on a line with an empty blockquote (fix #355) (#400)

## [2.14.2] - 2016-04-06

### Changed
* Add code to parse table rows correctly (#394)
* Also fixes (#388) Markdown parser doesn't recognize inline code `x | y` inside table cell

## [2.14.1] - 2016-04-05

### Changed
* Temporarily pin FSharp.Compiler.Service (#395)
* Cache is new keyword in Paket (#392)

## [2.13.6] - 2016-02-29

### Changed
* Added TypeScript to the CSharpFormat project (#386)

## [2.13.5] - 2016-01-25

### Changed
* Fixes issues in PaketFormat (#381) - colorize HTTP and file prefix
* Reliable getTypeLink (#380) - avoid crashes

## [2.13.4] - 2016-01-20

### Changed
* Colors paket keywords (#379)

## [2.13.3] - 2016-01-18

### Changed
* Adds PaketFormat to not color URLs as comments in Paket files (#349)

## [2.13.2] - 2016-01-12

### Changed
* Improve the load script to fix FsLab issue (https://github.com/fslaborg/FsLab/issues/98)

## [2.13.1] - 2016-01-12

### Changed
* Make logging to file optional using environment variable

## [2.13.0] - 2015-12-29

### Changed
* Be compatible with the common-mark spec for 'Fenced code blocks' and 'Indented code blocks'.
* See https://github.com/fsprojects/FSharp.Formatting/pull/343.
* Please follow-up by adding support for more sections of the spec.
* Add the section to https://github.com/fsprojects/FSharp.Formatting/blob/master/tests/FSharp.Markdown.Tests/CommonMarkSpecTest.fs#L20.
* Fix the newly enabled tests.
* Add CompiledName to members with F# specific naming (https://github.com/fsprojects/FSharp.Formatting/pull/372)

## [2.12.1] - 2015-12-24

### Changed
* update dependencies
* Upgrade the CommandTool to F# 4 and bundle FSharp.Core with sigdata and optdata.
* Fix crash when a fenced code block starts with an empty line (https://github.com/fsprojects/FSharp.Formatting/pull/361)
* Support for all known xml elements (https://github.com/fsprojects/FSharp.Formatting/pull/331)

## [2.12.0] - 2015-10-18

### Changed
* Update dependencies to be compatible with FSharp.Compiler.Service >=1.4.0.3

## [2.11.1-alpha1] - 2015-10-14

### Changed
* Adds methods for cross-type links #330 (https://github.com/fsprojects/FSharp.Formatting/pull/330)

## [2.11.0] - 2015-09-28

### Changed
* Fix https://github.com/fsprojects/FSharp.Formatting/issues/271
* Don't fail as long as we can recover / continue.
* Fix https://github.com/fsprojects/FSharp.Formatting/issues/201

## [2.10.3] - 2015-09-12

### Changed
* Require compatible F# Compiler Service in Nuspec (fix #337)

## [2.10.2] - 2015-09-11

### Changed
* Fix load script (wrap logging setup in try catch properly)

## [2.10.1] - 2015-09-11

### Changed
* paket update && fix compilation (#338)
* Wrap logging setup in try catch

## [2.10.0] - 2015-07-26

### Changed
* Add detailed logging and new FSharp.Formatting.Common.dll file
* Fix bug in C# code formatting tool (FormatHtml)

## [2.9.10] - 2015-06-27

### Changed
* Support multiple snippets in Literate.Parse (This is obsolete, but needed for www.fssnip.net.)

## [2.9.9] - 2015-06-22

### Changed
* Fix HTML escaping of code blocks with unknown languages (#321, #322)

## [2.9.6] - 2015-05-08

### Changed
* Generate 'fssnip' class for non-F# <pre> tags for consistency

## [2.9.5] - 2015-05-06

### Changed
* Provide an option to disable `fsi.AddPrinter` (#311)
* Generated line numbers for HTML are the same for F# and non-F#

## [2.9.4] - 2015-04-30

### Changed
* Use `otherFlags` parameter (#308)
* Format code in Markdown comments (#307, #36)

## [2.9.3] - 2015-04-28

### Changed
* Simplify using FCS interaction using Yaaf.Scripting (#305)
* Do not load dependencies when initializing evaluator
* Undo require exact version of F# Compiler Service

## [2.9.2] - 2015-04-24

### Changed
* Require exact version of F# Compiler Service

## [2.9.1] - 2015-04-21

### Changed
* Add back RazorEngine.dll (#302)

## [2.9.0] - 2015-04-20

### Changed
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
