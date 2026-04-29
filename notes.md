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
# Key Facts

- Directory.Packages.props is PROTECTED: both push_to_pull_request_branch and create_pull_request
  are blocked. Returns "success" but creates issue instead (fallback-to-issue). Issues #1184/#1188/#1190
  all contain click-to-create-PR links for the deps bundle.
- ⚠️ Deps bundle issue #1190 has Test.Sdk 18.5.0 which is UNLISTED on NuGet. Use PR #1193 (18.5.1) instead!
- PR #1161 (CSS theming, closes #1156): has nojaf approval, CI persistently NOT triggering (0 check_runs).
  head SHA 96c1b97f2c687ce561301b397df0e8fb3f5d3c01 (css: fix typo commit).
- Deps bundle branch: repo-assist/deps-bundle-2026-04-23-6d457b475a0265de (619/619 tests pass,
  changes only Directory.Packages.props). Issue #1190 has click-to-create-PR link.
  ⚠️ Bundle has Test.Sdk 18.5.0 (UNLISTED) — should NOT be used for Test.Sdk. Use #1193 instead.
- PR #1176 (list-block-refactor): CLOSED by dsyme 2026-04-21. Do not re-propose.
- PR #1173 (Seq allocations): CLOSED by dsyme 2026-04-20. Do not re-propose.
- Issue #1178 (#1178 dotnet-interactive): nhirschey said "leave it for now". Do not update Dockerfile.
- IKC0002: Ionide.KeepAChangelog only allows standard subsection names (Added/Changed/Deprecated/
  Fixed/Removed/Security); each version block can only have ONE of each subsection type.
- PR #1174 (FCS VersionOverride + Seq.cast): MERGED 2026-04-20.
- All open user issues (#585,#685,#705,#828,#898,#924-929,#949,#1064,#1156,#1178) have
  Repo Assist comments. No new human activity as of 2026-04-29.
- Dependabot PRs #1179-#1183 superseded by deps bundle issue #1190.
  New Dependabot PRs: #1191 (dotnet-repl 0.3.259), #1193 (Test.Sdk 18.5.1 — USE THIS NOT BUNDLE).
