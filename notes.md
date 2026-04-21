2026-04-21 (run 24718213220): Task 6: Updated PR #1161 — renamed --surface-* to --panel-* per nojaf+dsyme feedback. CI passes.
  - PR #1176 (list-block-refactor) was CLOSED by dsyme on 2026-04-21. Do not re-propose.
  - Task 2: All open issues commented on, no new human activity requiring new comments.

2026-04-20 (run 24662685228): Task 5+6: Refactor formatListBlock helper + update release PR #1175.
  - Created new PR (branch repo-assist/improve-list-block-refactor-2026-04-20):
    Extracted shared formatListBlock helper in MarkdownUtils.fs - eliminates ~21 duplicate lines
    between ordered/unordered list formatting. 346 tests pass.
    PR #1176 was CLOSED by dsyme on 2026-04-21. Do not re-propose list-block refactor.
  - Updated PR #1175 (release 22.0.1) to include #1174 entries:
    * Remove stale VersionOverride="43.12.201"
    * Replace Enumerable.Cast with Seq.cast
  - PR #1174 (fcs/seq fix) was MERGED by dsyme on 2026-04-20. ✅
  - PR #1173 (Seq allocation fixes) was CLOSED by dsyme on 2026-04-20. Do not re-propose.
  - Task 2: All open issues commented on, no new human activity since last run.

2026-04-19 (run 24627004104): Task 10: Created release prep PR for v22.0.1.
  - Moved all [Unreleased] entries to [22.0.1] - 2026-04-19 in RELEASE_NOTES.md.
  - Moved 3 "Fix" items misplaced in ### Added into ### Fixed.
  - Left empty [Unreleased] section.
  - Build succeeded (0 errors, pre-existing NU1605/NU1608 warnings only).
  - Branch: repo-assist/release-22.0.1-2026-04-19
  Note: PR #1106 also modifies RELEASE_NOTES.md (adds QuotedBlock fix + tooltip);
    if merged before release PR, minor conflict on [Unreleased] section needs resolving.
  Task 3 (bug fix): No new fixable bugs found without duplicating existing open PRs.

2026-04-18 (run 24602767430): Task 4+5: Fix stale FCS VersionOverride + Seq.cast cleanup.
  PR #1174 created. MERGED 2026-04-20.

2026-04-17 (run 24560940897): Task 3 (fix): Rebased perf-avoid-seq-alloc branch onto current main.
  PR #1173 created. CLOSED by dsyme 2026-04-20 (not merged). Do NOT re-propose Seq allocation fixes.

IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names
  (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use Performance, Testing, etc.
  Also: each version block can only have ONE of each subsection type. Duplicates cause IKC0002 build error.
IMPORTANT: safeoutputs MCP uses HTTP calls to http://host.docker.internal:80/mcp/safeoutputs.
  Auth token in /home/runner/.copilot/mcp-config.json as headers.Authorization.
  Must init session first to get Mcp-Session-Id, then use that header for tool calls.
  Use python3 scripts stored in /tmp/gh-aw/agent/ for multi-step calls.
