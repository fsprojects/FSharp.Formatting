2026-03-30: Created PR #1129: Performance - pre-compute GetNavigationEntriesFactory once per build.
  GetNavigationEntries was called O(n) times (once per output page), doing filter/group/sort + File.Exists checks each time.
  Refactored to factory pattern: O(n log n) once + O(n) per page. All 531 tests pass.
2026-03-29: Created two PRs:
- PR #1127 (test-tomd-coverage-2026-03-29): 15 new ToMd round-trip tests. All 281 markdown tests pass.
- PR #1128 (eng-ci-test-artifacts-2026-03-29): CI improvement - upload TRX test results as artifacts on failure.
PR #1103 (deps bump) was merged by dsyme on 2026-03-25.
Open PRs: #1129, #1128, #1127, #1108 (SVG fix), #1106 (blockquote/tooltip), #1105 (nested-nav+frontmatter), #1089 (watch --root), #1072 (embed-resources).
All open issues have RA comments. No new human activity on issues since 2026-03-23.
Deferred: Suave2→3 (breaking API change).
