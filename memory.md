2026-04-28 (run 25049012888): Task 6+11:
  - Updated PR #1161: pushed CI trigger commit (085ea978). CI should now run.
  - create_pull_request for deps bundle SILENTLY FAILED (protected files, no new issue created).
    Issue #1190 click-to-create-PR link is the only mechanism.
  - New Dependabot PR #1191: dotnet-repl 0.3.250→0.3.259 (not in existing deps bundle).

2026-04-27 (run 24991187441): Task 4+2:
  - Created issue #1190 (NOT PR): deps bundle FSharp.Core/FCS 202→203, System.Text.Json 10.0.6→10.0.7,
    Test.Sdk 18.4→18.5, FSharp.Data 8.1.10→8.1.11. Branch tested (619/619 pass).
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

## 2026-09-11 — Run 34546068188 (tasks: 1, 8, 2)
- Task 1 (Labelling): labelled 3 new unlabelled issues — #1316 (`enhancement`,`documentation`), #1313 (`bug`,`documentation`), #1314 (`bug`,`documentation`). All 3 filed by collaborator nojaf with detailed technical root-cause write-ups; #1313/#1314 already have nojaf's own fix PRs open (#1315, #1317).
- Task 8 (Performance): Found and fixed an O(n²) algorithmic issue in `pipeTableFindSplits` (`src/FSharp.Formatting.Markdown/MarkdownTableParser.fs`) — the inner scan recomputed `List.length` over the shrinking line/remainder on every recursive call to compute each cell's chunk size, making table-row splitting quadratic in row length for rows with many delimiters. Rewrote to track the number of consumed characters incrementally during the single scan, making it linear. No behavior change. Added a new test (`Transform tables with escaped pipe characters correctly`) covering the escape-handling branch. Verified: `dotnet build FSharp.Formatting.sln -c Release` 0 errors; `dotnet test tests/FSharp.Markdown.Tests` 369/369 pass (368 existing + 1 new); `dotnet fantomas --check` clean. Created draft PR on branch `repo-assist/perf-pipe-table-split-20260911`.
- Task 2 (Comment): Reviewed all 16 open issues oldest-first. New issues #1313/#1314/#1316 already have thorough self-contained root-cause analysis from the reporter (a collaborator) — no comment adds value. Older issues (#585, #705, #828, #898, #927, #928, #929, #1064) unchanged since prior runs, no new human activity — consistent with many past runs' conclusions, no comment made.
- Verified repo-assist PR backlog status: #1130, #1241, #1242, #1264, #1267, #1275 all MERGED since the 2026-09-05 memory snapshot. Only #1299 (ToLatex tests) and #1291 (deps bundle) remain open, both `mergeable_state: dirty` — need a rebase in a future Task 6 run.
- Task 11: Updated Monthly Activity issue #1277 with new Suggested Actions (rebase #1299/#1291, review new perf PR, review nojaf's #1315/#1317, review Dependabot #1318, consider #1316 fix, close stale infra issues #1240/#1296/#1297/#1298) and a new run history entry.
