2026-04-19 (run 24627004104): Task 10: Created release prep PR for v22.0.1.
  - Moved all [Unreleased] entries to [22.0.1] - 2026-04-19 in RELEASE_NOTES.md.
  - Moved 3 "Fix" items misplaced in ### Added into ### Fixed.
  - Left empty [Unreleased] section.
  - Build succeeded (0 errors, pre-existing NU1605/NU1608 warnings only).
  - Branch: repo-assist/release-22.0.1-2026-04-19
  Note: PR #1106 also modifies RELEASE_NOTES.md (adds QuotedBlock fix + tooltip);
    if merged before release PR, minor conflict on [Unreleased] section needs resolving.
  Task 3 (bug fix): No new fixable bugs found without duplicating existing open PRs.
    - QuotedBlock bug: already covered by open PR #1106
    - publicOnly=false C# test: complex, requires FCS investigation, not safely fixable

2026-04-18 (run 24602767430): Task 4+5: Fix stale FCS VersionOverride + Seq.cast cleanup.
  PR #1174 created. All 5 open Repo Assist PRs: 1174 (fcs/seq), 1173 (perf), 1161 (CSS), 1130 (tooltip), 1106 (blockquote).

2026-04-17 (run 24560940897): Task 3 (fix): Rebased perf-avoid-seq-alloc branch onto current main.
  PR #1173 created. Previous PR #1171 was closed by dsyme (merge conflict with dependabot).
  New branch: repo-assist/perf-avoid-seq-alloc-2026-04-17, rebased cleanly onto current main.

2026-04-16 (run 24505962897): Task 8 (performance): Eliminated Seq allocations in hot parsing paths.
  PR #1171 created (now closed by maintainer, rebased as new PR this run). 520 tests pass.

2026-04-15 (run 24450121955): Task 9 (testing improvements): Added 29 Markdown.ToLatex unit tests.
  Task 3 (bug fix): Fixed level-6 heading bug in LatexFormatting.fs.
  PR #1170 created and MERGED by dsyme 2026-04-17. ✅

2026-04-14 (run 24394692121): Task 6 (maintain PRs):
  PR #1106 was failing CI with IKC0002: duplicate ### Changed subsection in [22.0.0].
  Fixed by merging the duplicate into a single ### Fixed section matching main branch.

2026-04-13: Created PR #1165 (fix-tomd-yaml-frontmatter). MERGED. ✅
2026-04-12: Created PR #1164 (fix-watch-root). MERGED. ✅
2026-04-10: Created PR #1161 (CSS surface colors, open), #1162 (regex compile, MERGED). ✅
2026-04-10: Created PR #1157 (tight list fix). MERGED. ✅

IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names
  (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use Performance, Testing, etc.
  Also: each version block can only have ONE of each subsection type. Duplicates cause IKC0002 build error.
IMPORTANT: safeoutputs MCP uses HTTP calls to http://host.docker.internal:80/mcp/safeoutputs.
  Auth token in /home/runner/.copilot/mcp-config.json as headers.Authorization.
  Must init session first to get Mcp-Session-Id, then use that header for tool calls.
  Use python3 scripts stored in /tmp/gh-aw/agent/ for multi-step calls.
