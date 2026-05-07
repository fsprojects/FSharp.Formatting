2026-05-05 (run 25351299219): Task 2+6+11:
  - Task 6: Rebased PR #1161 onto main, resolved RELEASE_NOTES merge conflict
    (merged panel-* CSS entry with other Unreleased entries), pushed CI re-trigger commit.
  - Task 2: All user issues already have Repo Assist comments; no new human activity since 2026-04-29.
  - Task 11: Updated monthly activity issue #1198 with new run entry and updated suggested actions
    (added new Dependabot PRs #1199/#1200).

2026-05-03 (run 25265702844): Task 9+11:
  - Created PR #1197: 6 new tests (ToFsx output-comment, ToPynb execution-output, LaTeX blocks).
    352/352 pass.
  - Closed monthly issue #1131 (2026-04); created new #1198 (2026-05).

2026-04-29 (run 25105019910): Task 4+9+11:
  - Created PR #1194: 4 new tests (OutputBlock, AnchorLink, code-block language specifier)
    All 350 FSharp.Markdown.Tests pass.
  - Commented on #1190: Test.Sdk 18.5.0 is UNLISTED on NuGet (vstest binding bug).
    Bundle branch has 18.5.0. Maintainer should use #1193 (18.5.1) instead.
  - New Dependabot PR #1193 (Test.Sdk 18.4→18.5.1) created 2026-04-29.

2026-04-28 (run 25049012888): Task 6+11:
  - Updated PR #1161: pushed CI trigger commit (085ea978). CI should now run.
  - create_pull_request for deps bundle SILENTLY FAILED (protected files, no new issue created).
    Issue #1190 click-to-create-PR link is the only mechanism.
  - New Dependabot PR #1191: dotnet-repl 0.3.250→0.3.259 (not in existing deps bundle).

2026-04-27 (run 24991187441): Task 4+2:
  - Created issue #1190 (NOT PR): deps bundle FSharp.Core/FCS 202→203, System.Text.Json 10.0.6→10.0.7,
    Test.Sdk 18.4→18.5.0 (⚠️ UNLISTED!), FSharp.Data 8.1.10→8.1.11. Branch tested (619/619 pass).
    Supersedes #1184/#1188 and Dependabot PRs #1179-#1183.

2026-04-26 (run 24954489456): Updated PR #1161: CSS typo fix. CI still not triggering (0 check_runs).

2026-04-25 (run 24928898904): push_to_pull_request_branch FAILED → issue #1189 created. Issue #1188 created.

2026-04-24 (run 24885587464): Created PR #1187: 7 ToMd tests. CI 4/4 pass.

2026-04-23 (run 24831099209): Created issue #1184 (deps bundle) + PR #1185 (indirect links fix).

2026-04-22 (run 24774101402): Labelled #1178, commented on #1178. Updated PR #1161 CSS comment.

2026-04-21 (run 24718213220): Updated PR #1161 (renamed --surface-* to --panel-*). PR #1176 CLOSED by dsyme.

2026-04-20 (run 24662685228): Updated PR #1175. PR #1174 MERGED. PR #1173 CLOSED by dsyme.

2026-04-19 (run 24627004104): Created PR #1175: Release 22.0.1.

2026-04-18 (run 24602767430): Created PR #1174 (FCS/Seq.cast). MERGED 2026-04-20.

IMPORTANT: Ionide.KeepAChangelog: only standard subsection names; one per version block.
IMPORTANT: Directory.Packages.props is protected — create_pull_request silently fails, creates no item.

2026-05-07 (run 25469038887): Task 6+3+11:
  - Task 6: Merged main into PR #1161 branch to re-trigger CI (was stuck at 0 check_runs).
  - Task 3: Created PR #1201: fix Markdown.ToMd multi-paragraph blockquote round-trip.
    Root cause: bare blank line between inner paragraphs closed blockquote. Fix: emit '>' separator.
    2 new tests (round-trip produces single QuotedBlock), 348/348 pass.
  - Task 11: Updated #1198 monthly activity issue with 2026-05-05 and 2026-05-07 run entries.
    Added #1201, #1199, #1200 to suggested actions.
