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
