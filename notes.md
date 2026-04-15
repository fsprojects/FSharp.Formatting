2026-04-15 (run 24450121955): Task 9 (testing improvements): Added 29 Markdown.ToLatex unit tests.
  Previously ToLatex had zero direct unit tests.
  Tests cover: headings (all 6 levels), bold, italic, inline code, links, images,
  unordered/ordered lists, code blocks, blockquotes, tables, horizontal rules,
  special char escaping (#%&_), inline math, display math, EmbedParagraphs, empty doc.
  Task 3 (bug fix): Fixed level-6 heading bug in LatexFormatting.fs.
  Bug: '| _ -> ""' in heading match produced invalid LaTeX '{content}' for h6+.
  Fix: '| _ -> @"\subparagraph"' — deepest available LaTeX sectioning command.
  PR created: branch repo-assist/improve-tolatex-tests-2026-04-15
  All 346 markdown tests pass, 143 literate tests pass.
  New Dependabot PRs: #1167 (FCS+FSharp.Core), #1168 (FSharp.Core), #1169 (System.Text.Json).
  These need bundling with #1166 (FSharp.Data).

2026-04-14 (run 24394692121): Task 6 (maintain PRs):
  PR #1106 was failing CI with IKC0002: duplicate ### Changed subsection in [22.0.0].
  Fixed by merging the duplicate into a single ### Fixed section matching main branch.
  Pushed fix to PR #1106 branch.
  Task 2 (issue investigation): All open issues already have Repo Assist comments. No new human activity requiring re-engagement.
  PRs merged since last run: most (#1164, #1162, #1157, #1153, #1150, #1147, #1145, #1144, #1142, #1134, #1127, #1108 etc).
  New Dependabot PR #1166: FSharp.Data 8.1.8.
  Open Repo Assist PRs remaining: #1161 (CSS surface colors), #1130 (tooltip), #1106 (blockquote).

2026-04-13 (run 24339583031): Task 3 (fix): Created PR fix-tomd-yaml-frontmatter-2026-04-13:
  - Fix: Markdown.ToMd was silently dropping YamlFrontmatter paragraphs
  - Added 2 tests: one with populated frontmatter, one with empty block
  - Build: 0 warnings, 0 errors. 283 markdown tests pass, 143 literate tests pass
  - FSharp.ApiDocs.Tests: 80 pre-existing failures on main (unrelated)
  Task 2 (comment): All open non-Repo-Assist issues already have comments.
  No new issues to comment on (cursor at 1064, no newer human issues).

2026-04-12 (run 24304604567): Task 6 (maintain PRs):
  Closed duplicate PRs:
  - #1159 (wrong title: CSS title but perf branch) 
  - #1160 (duplicate perf PR; keeping #1162)
  - #1089 (old --root PR with accumulated merge commits; replaced by fresh rebase)
  Created bundle-deps PR (branch repo-assist/eng-bundle-deps-2026-04-10):
  FSharp.Data 8.1.7 + upload-artifact v7 (closes #1152, #1155). 284 tests pass.
  Task 3 (fix): Created fresh rebase of PR #1089 as new PR (branch repo-assist/fix-issue-924-watch-root-2):
  - Add --root option to fsdocs watch (closes #924)
  - Fix WebSocket hot-reload URL to use window.location.host
  Build: 0 warnings, 0 errors. 319 tests pass.
2026-04-10 (run 24238894950): Task 2 (issue investigation): nojaf said "go for it" on #1156.
  Implemented CSS --surface-background and --surface-border variables.
  PR #1161 (branch repo-assist/fix-issue-1156-surface-colors). Commented on #1156.
2026-04-10 (run 24238894950): Task 8 (performance): Fixed 5 uncompiled Regex instances in hot paths.
  PR #1162 (branch repo-assist/perf-compile-regex-2026-04-10-f88f43f8). 281 tests pass.
2026-04-10: Created bundle-deps branch (later PR created 2026-04-11):
  FSharp.Data 8.1.7 + upload-artifact v7.
2026-04-10: Task 3 carry-over: Created PR #1157 (tight list round-trip fix). 284 tests pass.
IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names
  (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use Performance, Testing, etc.
  Also: each version block can only have ONE of each subsection type. Duplicates cause IKC0002 build error.
IMPORTANT: safeoutputs MCP uses HTTP calls to http://host.docker.internal:80/mcp/safeoutputs.
  Auth token in /home/runner/.copilot/mcp-config.json as headers.Authorization.
  Must init session first to get Mcp-Session-Id, then use that header for tool calls.
  Use python3 scripts stored in /tmp/gh-aw/agent/ for multi-step calls.
