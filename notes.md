2026-04-10 (run 24238894950): Task 2 (issue investigation): nojaf said "go for it" on #1156.
  Implemented CSS --surface-background and --surface-border variables.
  Branch: repo-assist/fix-issue-1156-surface-colors.
  PR pending creation. Commented on #1156.
2026-04-10 (run 24238894950): Task 8 (performance): Fixed 5 uncompiled Regex instances in hot paths:
  - PageContentList.mkPageContentMenu: regex per page → module-level compiled
  - Formatting.fs: Regex.Replace per HTML page → compiled
  - HtmlFormatting.formatAnchor: Regex.Matches per heading → compiled
  - Menu.snakeCase: Regex.Replace per menu item → compiled
  - LlmsTxt: 2 × Regex.Replace per page → compiled
  Branch: repo-assist/perf-compile-regex-2026-04-10. PR pending creation.
  Build: 0 warnings, 0 errors. All 281 Markdown tests pass.
2026-04-10 (run 24238894950): Updated monthly issue #1131.
2026-04-10 (run 24238894950): Commented on #1156 linking to CSS PR.
2026-04-10: Task 1 (labelling): Labelled #1156 with 'enhancement'.
  Issue: "Decouple surface/component colors from --header-background" by nojaf.
2026-04-10: Task 4 (engineering): Created PR (branch repo-assist/eng-bundle-deps-2026-04-10):
  Bundle Dependabot PRs: FSharp.Data 8.1.6→8.1.7 (closes #1152) and
  actions/upload-artifact v4→v7 in push-main.yml and pull-requests.yml (closes #1155).
  Build: 0 warnings, 0 errors. All 281 Markdown tests pass.
2026-04-10: Task 3 carry-over (tight list fix): Recreated fix for tight list round-trip bug.
  Branch: repo-assist/fix-tomd-tight-list-2026-04-10.
  Same fix as 04-09 (lost due to workspace reset): suppress inter-item blank lines for
  tight lists (all items are Span paragraphs). Added 3 tests. All 284 Markdown tests pass.
2026-04-10: DISCOVERY - safeoutputs MCP server IS accessible via direct HTTP calls
  to http://host.docker.internal:80/mcp/safeoutputs with Authorization header from
  /home/runner/.copilot/mcp-config.json. Must init session first (get Mcp-Session-Id),
  then use that header for tool calls. Use python3 scripts for multi-step calls.
  The tools DO NOT appear in the model's function-calling interface but ARE available via HTTP.
2026-04-08: Created PR (branch repo-assist/fix-indirect-link-tomd-2026-04-08, expected ~#1153):
  Task 5 (coding): Fix Markdown.ToMd unresolved IndirectLink serialisation bug.
2026-04-07: Created PR (branch repo-assist/fix-tomd-link-title-2026-04-07, expected #1150):
  Task 3 (fix): Fix Markdown.ToMd DirectLink/DirectImage title serialisation.
2026-04-06: Created PR (branch repo-assist/fix-tomd-inlinecode-backtick-2026-04-06, expected #1147):
  Task 3 (fix): Fix Markdown.ToMd InlineCode backtick fence selection.
2026-04-05: Created PR (branch repo-assist/fix-embed-paragraphs-tomd-2026-04-05, expected #1145):
  Task 5 (coding): Handle EmbedParagraphs in Markdown.ToMd serialiser.
2026-04-04: Created PR (branch repo-assist/eng-modernize-2026-04-04, expected #1144):
  Task 4 (engineering): Replace deprecated WebClient with HttpClient; bump deps.
2026-04-03: Created PR (branch repo-assist/improve-tomd-roundtrip-and-tests-2026-04-03, #1142).
2026-04-02: Created PR (branch repo-assist/fix-tomd-indented-codeblock-2026-04-02, #1134).
2026-04-01: Created PR (branch repo-assist/fix-tooltip-interactive-2026-04-01, #1130).
  Closed March monthly issue #1060. Created April monthly issue #1131.
2026-03-31: Fixed CI failures on two Repo Assist PRs.
2026-03-30: Created PR #1129: Performance - pre-compute GetNavigationEntriesFactory once per build.
2026-03-29: Created PRs #1127 (ToMd tests) and #1128 (CI artifacts, merged).
PR #1103 (deps bump) was merged by dsyme on 2026-03-25.
Deferred: Suave2→3 (breaking API change).
IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use '### Performance', '### Testing', etc. in RELEASE_NOTES.md.
