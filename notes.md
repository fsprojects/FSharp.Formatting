2026-04-01: Created PR (branch repo-assist/fix-tooltip-interactive-2026-04-01):
  Fix tooltip interactivity - allow mouse to enter tooltip popover to select/copy text.
  Root cause: mouseout handler on trigger didn't check if relatedTarget was inside tooltip.
  Also added mouseout handler on tooltip itself + CSS cursor:text / user-select:text.
  Commented on #949, closes #949.
  Closed March monthly issue #1060. Created April monthly issue.
2026-03-31: Fixed CI failures on two Repo Assist PRs:
- PR #1129 (perf nav): Fixed RELEASE_NOTES.md '### Performance' → '### Changed' (invalid Keep a Changelog subsection with IKC0002 error in Ionide.KeepAChangelog.Tasks 0.3.3).
- PR #1127 (ToMd tests): Fixed ToMd LatexBlock serialisation — single-line equation blocks now emit '$$body$$' instead of '\begin{equation}...'. All 295 markdown tests pass.
2026-03-30: Created PR #1129: Performance - pre-compute GetNavigationEntriesFactory once per build.
2026-03-29: Created PRs #1127 (ToMd tests) and #1128 (CI artifacts, merged).
PR #1103 (deps bump) was merged by dsyme on 2026-03-25.
Deferred: Suave2→3 (breaking API change).
IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use '### Performance', '### Testing', etc. in RELEASE_NOTES.md.
