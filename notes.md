2026-03-31: Fixed CI failures on two Repo Assist PRs:
- PR #1129 (perf nav): Fixed RELEASE_NOTES.md '### Performance' → '### Changed' (invalid Keep a Changelog subsection with IKC0002 error in Ionide.KeepAChangelog.Tasks 0.3.3).
- PR #1127 (ToMd tests): Fixed ToMd LatexBlock serialisation — single-line equation blocks now emit '$$body$$' instead of '\begin{equation}...'. All 295 markdown tests pass.
2026-03-30: Created PR #1129: Performance - pre-compute GetNavigationEntriesFactory once per build.
  GetNavigationEntries was called O(n) times (once per output page), doing filter/group/sort + File.Exists checks each time.
  Refactored to factory pattern: O(n log n) once + O(n) per page. All 531 tests pass.
2026-03-29: Created two PRs:
- PR #1127 (test-tomd-coverage-2026-03-29): 15 new ToMd round-trip tests. All 281 markdown tests pass.
- PR #1128 (eng-ci-test-artifacts-2026-03-29): CI improvement - upload TRX test results as artifacts on failure. Merged/closed.
PR #1103 (deps bump) was merged by dsyme on 2026-03-25.
Open PRs: #1129, #1127, #1108, #1106, #1105, #1089, #1072 (PR #1128 merged/closed).
All open issues have RA comments. No new human activity on issues since 2026-03-23.
Deferred: Suave2→3 (breaking API change).
IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use '### Performance', '### Testing', etc. in RELEASE_NOTES.md.
