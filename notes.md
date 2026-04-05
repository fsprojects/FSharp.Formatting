2026-04-05: Created PR (branch repo-assist/fix-embed-paragraphs-tomd-2026-04-05, expected #1145):
  Task 5 (coding): Handle EmbedParagraphs in Markdown.ToMd serialiser
  (MarkdownUtils.fs). EmbedParagraphs was previously falling through to
  catch-all '| _' branch, emitting debug printfn to stdout and dropping content.
  Now delegates to cmd.Render() and recurses, consistent with HTML and LaTeX formatters.
  Removed dead catch-all. Added test. All tests pass.
2026-04-04: Created PR (branch repo-assist/eng-modernize-2026-04-04, expected #1144):
  Task 4 (engineering): Replace deprecated WebClient with HttpClient in createImageSaver
  (BuildCommand.fs). Bump Newtonsoft.Json 13.0.3→13.0.4 and System.Memory 4.5.5→4.6.3.
  Removed #nowarn "44" suppression. Build: 0 warnings, 0 errors. All tests pass.
2026-04-03: Created PR (branch repo-assist/improve-tomd-roundtrip-and-tests-2026-04-03, #1142):
  Task 5 (coding): Fix HardLineBreak ToMd serialisation ("  \n" not "\n"),
  fix HorizontalRule ToMd to use stored char (--- not 23 dashes),
  remove debug printfn from catch-all.
  Task 9 (testing): Added 9 new tests — HardLineBreak/HorizontalRule round-trips,
  Markdown.ToFsx (3 tests), Markdown.ToPynb (3 tests).
  All 291 Markdown + full suite pass.
2026-04-03: PR #1129 (perf nav) merged by dsyme.
2026-04-03: PR #1072 (embed-resources convert) merged by dsyme on 2026-04-02.
2026-04-03: Issue #1132 closed by dsyme.
2026-04-02: Created PR (branch repo-assist/fix-tomd-indented-codeblock-2026-04-02, #1134):
  Fix Markdown.ToMd for indented code blocks (fence=None): serialise as fenced blocks
  to preserve the round-trip. Added test. All 282 markdown tests + full suite pass.
2026-04-01: Created PR (branch repo-assist/fix-tooltip-interactive-2026-04-01, #1130):
  Fix tooltip interactivity - allow mouse to enter tooltip popover to select/copy text.
  Root cause: mouseout handler on trigger didn't check if relatedTarget was inside tooltip.
  Also added mouseout handler on tooltip itself + CSS cursor:text / user-select:text.
  Commented on #949, closes #949.
  Closed March monthly issue #1060. Created April monthly issue #1131.
2026-03-31: Fixed CI failures on two Repo Assist PRs:
- PR #1129 (perf nav): Fixed RELEASE_NOTES.md '### Performance' → '### Changed' (invalid Keep a Changelog subsection with IKC0002 error in Ionide.KeepAChangelog.Tasks 0.3.3).
- PR #1127 (ToMd tests): Fixed ToMd LatexBlock serialisation — single-line equation blocks now emit '$$body$$' instead of '\begin{equation}...'. All 295 markdown tests pass.
2026-03-30: Created PR #1129: Performance - pre-compute GetNavigationEntriesFactory once per build.
2026-03-29: Created PRs #1127 (ToMd tests) and #1128 (CI artifacts, merged).
PR #1103 (deps bump) was merged by dsyme on 2026-03-25.
Deferred: Suave2→3 (breaking API change).
IMPORTANT: Ionide.KeepAChangelog.Tasks 0.3.3 only allows standard Keep a Changelog subsection names (Added, Changed, Deprecated, Fixed, Removed, Security). Do NOT use '### Performance', '### Testing', etc. in RELEASE_NOTES.md.
