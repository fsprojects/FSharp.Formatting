# Key Facts

- Monthly 2026-05 issue is #1198
- PR #1130 (tooltip fix, closes #949): still open
- PR (new, run 25601743658): remove Dockerfile + mybinder badges — closes #1178
- PRs merged 2026-05-09 by dsyme: #1203 (AnchorLink fix), #1202 (blockquote roundtrip),
  #1194 (tests), #1175 (22.0.1 release). PR #1106 closed (superseded).
- All open user issues (#585,#685,#705,#828,#898,#924-929,#949,#1064,#1178) have
  Repo Assist comments. No new human activity as of 2026-05-09 except #1178 (dsyme
  resolved: PR submitted to remove Dockerfile + mybinder).
- IKC0002: Ionide.KeepAChangelog only allows standard subsection names (Added/Changed/
  Deprecated/Fixed/Removed/Security); each version block can only have ONE of each subsection type.
- PR #1176 (list-block-refactor): CLOSED. Do not re-propose.
- PR #1173 (Seq allocations): CLOSED. Do not re-propose.
- 2026-08-25 run (32792528607): Task 2+6+5+11.
  - Task 6: all 4 open Repo Assist PRs (#1264, #1242, #1241, #1130) green CI — no action needed.
  - Task 2: reviewed oldest open issues (#1256, #1240, #1243, #685, #1221) — all already have
    appropriate Repo Assist comments/PRs; no new human activity warranting fresh comment.
  - Task 5: opened new PR (branch repo-assist/docs-literate-doctest) adding a "doc testing" section
    to docs/literate.fsx addressing #1221 (documents that --eval + include-output/include-value
    already provide lightweight doc-testing). Verified via fantomas --check, dotnet build, fsdocs build.
  - Task 11: updated Monthly Activity issue #1243 (August 2026) with new PR entry.

- **Suave 3.4.5 breaking change (found 2026-08-29)**: Dependabot PR #1269 bumps Suave 2.6.2→3.4.5, a major breaking version. Suave rewrote internals from `Async`-based to `Task`-based APIs. This breaks `src/fsdocs-tool/BuildCommand.fs` (uses removed `HttpRuntime.logger`, mixes `Async<'a>` with new `SocketOp`/`Task` types around lines 30/864/872/885/1315). Not a trivial fix — requires migrating the `fsdocs watch` live-reload server code to the new Suave API. Do not attempt to merge #1269 as-is.

## 2026-09-03 — Run 33699124048 (tasks: 2, 4, 3)
- Task 4 (Engineering Investments): Created PR (branch repo-assist/deps-bump-projinfo-nunit-20260903) bumping Ionide.ProjInfo 0.74.2→0.75.0 and NUnit3TestAdapter 6.2.0→6.3.0 directly in Directory.Packages.props (no workflow files touched, avoiding the push-protection issue that blocked prior deps bundles like #1240). Verified: dotnet restore/build clean, 12/12 fsdocs-tool.Tests, 368/368 FSharp.Markdown.Tests pass. Supersedes Dependabot PRs #1273 and #1268.
- Confirmed Suave 3.4.6 migration (previously flagged as a breaking-change risk for old Dependabot PR #1269) is ALREADY MERGED as PR #1272 — BuildCommand.fs was migrated to Task-based Suave 3 API. This concern is now resolved; do not re-flag.
- Task 3 (Issue Fix): reviewed #585, #1064 (large architectural asks, unchanged from prior runs) and #685 (covered by open PR #1242) — no new fixable issue found.
- Task 2 (Issue Comment): reviewed all 15 open issues — all have adequate prior Repo Assist engagement, no new human activity found.
- Task 11: Closed August Monthly Activity issue #1243, created new September issue "[repo-assist] Monthly Activity 2026-09".
- Deps bundle issue #1240 (Aug 2026, FSharp.Data/G-Research.FSharp.Analyzers/fsharp-analyzers/gh-aw-actions) still blocked by workflow-file push protection — unchanged, needs manual maintainer action.
- Dependabot PR #1270 (gh-aw-actions/setup 0.86.2→0.87.4): still open, no CI status checked this run.

## 2026-09-03 — Run 33723677613 (command mode: /repo-assist on PR #1264)
- Triggered by nojaf comment "/repo-assist resolve conflict and rebase" on PR #1264.
- Rebased branch repo-assist/fix-issue-1256-seealso-b4bd46cd83322a47 onto latest origin/main.
- Resolved 1 conflict in RELEASE_NOTES.md (merged the seealso "Added" entry with the Mermaid "Changed" entry, both now under Unreleased).
- Verified: dotnet build (Release) succeeded 0 errors; FSharp.ApiDocs.Tests 90/90 passed (4 skipped, pre-existing).
- Pushed rebased branch via push_to_pull_request_branch — success.
