2026-03-18: Fixed 2 bugs in Markdown.ToMd (MarkdownUtils.fs):
  1. Emphasis→bold (line 118): *italic* was serialized as **bold** 
  2. Ordered list 0-indexed (line 180): "0 first" instead of "1. first"
  PR created on branch repo-assist/fix-tomd-emphasis-ordered-2026-03-18
Open PRs: #1100(CI dedup),#1089(watch --root),#1072(embed-resources)
Issue #1101=protected-files deps bump (STJ 8→10, GR analyzers 0.21→0.22)
All open issues have Repo Assist comments. No new human activity.
Major version bumps deferred: NUnit 3→4, FsUnit 5→7, Suave 2→3
Future: split BuildCommand.fs (#1022), overloaded methods (#585), multi-TFM (#1064)
