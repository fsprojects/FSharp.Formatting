# Key Facts

- Directory.Packages.props is PROTECTED: both push_to_pull_request_branch and create_pull_request
  are blocked. Returns "success" but creates issue instead (fallback-to-issue). Issues #1184/#1188/#1190
  all contain click-to-create-PR links for the deps bundle.
- PR #1161 (CSS theming, closes #1156): has nojaf approval, CI finally triggered 2026-04-28.
  head SHA 085ea978 (empty CI trigger commit).
- Deps bundle branch: repo-assist/deps-bundle-2026-04-23-6d457b475a0265de (619/619 tests pass,
  changes only Directory.Packages.props). Issue #1190 has click-to-create-PR link.
- PR #1176 (list-block-refactor): CLOSED by dsyme 2026-04-21. Do not re-propose.
- PR #1173 (Seq allocations): CLOSED by dsyme 2026-04-20. Do not re-propose.
- Issue #1178 (#1178 dotnet-interactive): nhirschey said "leave it for now". Do not update Dockerfile.
- IKC0002: Ionide.KeepAChangelog only allows standard subsection names (Added/Changed/Deprecated/
  Fixed/Removed/Security); each version block can only have ONE of each subsection type.
- PR #1174 (FCS VersionOverride + Seq.cast): MERGED 2026-04-20.
- All open user issues (#585,#685,#705,#828,#898,#924-929,#949,#1064,#1156,#1178) have
  Repo Assist comments. No new human activity as of 2026-04-28.
- Dependabot PRs #1179-#1183 superseded by deps bundle issue #1190.
  New Dependabot PR #1191 (dotnet-repl 0.3.250→0.3.259) created 2026-04-28.
