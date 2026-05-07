# Key Facts

- Directory.Packages.props is PROTECTED: both push_to_pull_request_branch and create_pull_request
  are blocked. Returns "success" but creates issue instead (fallback-to-issue). Issues #1184/#1188/#1190
  all contain click-to-create-PR links for the deps bundle.
- ⚠️ Deps bundle issue #1190 has Test.Sdk 18.5.0 which is UNLISTED on NuGet. Use PR #1193 (18.5.1) instead!
- PR #1161 (CSS theming, closes #1156): has nojaf approval, CI persistently NOT triggering.
  2026-05-05: rebased onto main, resolved RELEASE_NOTES conflict, pushed CI trigger commit.
  2026-05-07: merged main again to re-trigger CI. If CI still doesn't trigger, maintainer must re-run manually.
- PR #1201 (blockquote roundtrip fix): focused PR. Fixes QuotedBlock.ToMd round-trip bug in MarkdownUtils.fs.
  Supersedes the blockquote part of PR #1106 (which also has tooltip fix).
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
  Repo Assist comments. No new human activity as of 2026-05-07.
- Dependabot PRs #1179-#1183 superseded by deps bundle issue #1190.
  Dependabot PRs: #1191 (dotnet-repl 0.3.259), #1193 (Test.Sdk 18.5.1 — USE THIS NOT BUNDLE),
  #1196 (NuGet/login 1.2.0), #1199 (NUnit 4.6.0), #1200 (Suave 3.3.0)
