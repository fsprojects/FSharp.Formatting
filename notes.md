2026-04-24 (run 24885587464): Task 4+9:
  - Created PR (branch repo-assist/test-tomd-table-html-2026-04-24): 
    7 new ToMd tests: table column alignments (left, center, right, default, mixed)
    and InlineHtmlBlock round-trip. 352/352 Markdown tests pass.
    NOTE: PR number unknown (safeoutputs returned success with patch but no PR number).
    Should be findable via branch name in next run.

2026-04-23 (run 24831099209): Task 10+5:
  - Created issue #1184: deps bundle — FSharp.Core/FCS 202→203, System.Text.Json 10.0.6→10.0.7,
    Microsoft.NET.Test.Sdk 18.4→18.5, FSharp.Data 8.1.10→8.1.11 (619/619 tests pass)
    IMPORTANT: #1184 is an ISSUE not a PR, because Directory.Packages.props is a protected file.
    Contains link for maintainer to click to create the PR. Supersedes Dependabot PRs #1179-#1183.
    When bundle is merged/PR created, PRs #1179-#1183 can be closed.
  - Created PR #1185: fix Markdown.ToMd unresolved indirect links: preserves [body][key] form
    instead of broken [body](original). 347/347 Markdown tests pass.
    (IndirectLink fallback was inconsistent with IndirectImage)

2026-04-22 (run 24774101402): Task 1+3:
  - Labelled #1178 (dotnet-interactive deprecation) with `enhancement`, `help wanted`
  - Commented on #1178: NuGet deprecation won't break Dockerfile immediately (pinned version still installable);
    real concern is .NET 7 SDK EOL; mybinder.org only use case
    NOTE: nhirschey commented 2026-04-22: "leave it for now, until it actually breaks" - do NOT update Dockerfile
  - Updated PR #1161: pushed CSS comment improvement to trigger CI on --panel-background/--panel-border rename commit
    (CI hadn't run on latest commit e5ed887d from previous run)
  - Note: Dependabot had 4 open PRs (#1179-#1182) - bundled in run 24831099209

2026-04-21 (run 24718213220): Task 6: Updated PR #1161 — renamed --surface-* to --panel-* per nojaf+dsyme feedback. CI passes.
  - PR #1176 (list-block-refactor) was CLOSED by dsyme on 2026-04-21. Do not re-propose.
  - Task 2: All open issues commented on, no new human activity requiring new comments.

2026-04-20 (run 24662685228): Task 5+6: Refactor formatListBlock helper + update release PR #1175.
  - PR #1176 was CLOSED by dsyme on 2026-04-21. Do not re-propose list-block refactor.
  - Updated PR #1175 (release 22.0.1) to include #1174 entries
  - PR #1174 (fcs/seq fix) was MERGED by dsyme on 2026-04-20. ✅
  - PR #1173 (Seq allocation fixes) was CLOSED by dsyme on 2026-04-20. Do not re-propose.

2026-04-19 (run 24627004104): Task 10: Created release prep PR for v22.0.1.

2026-04-18 (run 24602767430): Task 4+5: Fix stale FCS VersionOverride + Seq.cast cleanup.
  PR #1174 created. MERGED 2026-04-20.

IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names
  (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use Performance, Testing, etc.
  Also: each version block can only have ONE of each subsection type. Duplicates cause IKC0002 build error.
IMPORTANT: safeoutputs MCP uses HTTP calls to http://host.docker.internal:80/mcp/safeoutputs.
  Auth token in /home/runner/.copilot/mcp-config.json as headers.Authorization.
  Must init session first to get Mcp-Session-Id, then use that header for tool calls.
  Use python3 scripts stored in /tmp/gh-aw/agent/ for multi-step calls.
