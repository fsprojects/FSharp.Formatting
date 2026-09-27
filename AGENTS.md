# Agent Guidelines

## Release Notes

Update `RELEASE_NOTES.md` when making user-visible changes:
- Add entries under the `## [Unreleased]` section
- A `## [x.y.z] - date` heading is a version that is already published. Never add an entry to one, not even the topmost, and never edit the entries it holds
- The release commit renames `## [Unreleased]` to the version it ships, so right after a release the file has no `## [Unreleased]` section. Add one above the topmost version heading rather than appending to that version
- Use categories: `### Added`, `### Changed`, `### Fixed`, `### Removed`
- Include PR links: `[#123](https://github.com/fsprojects/FSharp.Formatting/pull/123)`

## Code Formatting

Format code using Fantomas before committing:

```bash
dotnet tool restore
dotnet fantomas build.fsx src tests docs
```

Check formatting without modifying files:

```bash
dotnet fantomas build.fsx src tests docs --check
```

Configuration is in `.editorconfig`.

## CI Checks

Run these checks locally before pushing:

```bash
# Full CI pipeline (lint, build, test, docs)
dotnet fsi build.fsx

# Just lint and test
dotnet fsi build.fsx -- -p Verify

# Run analyzers (G-Research.FSharp.Analyzers, Ionide.Analyzers) over the whole solution.
# This is what CI does, and it takes minutes.
dotnet msbuild /t:AnalyzeSolution

# The same analyzers over the files the working tree touched, which takes seconds.
# Use this while working; it cannot see a finding your change causes in a file you did
# not edit, so run the full one above before opening a pull request.
dotnet fsi build.fsx -- -p AnalyzeChanged
```

Individual steps:

```bash
dotnet restore FSharp.Formatting.sln
dotnet build FSharp.Formatting.sln --configuration Release
dotnet test FSharp.Formatting.sln --configuration Release --no-build
```

## Testing Locally Against Another Project

After building the repo with `dotnet build`, run the tool directly from the build output in your project's directory:

```bash
# macOS / Linux
/path/to/FSharp.Formatting/src/fsdocs-tool/bin/Debug/net10.0/fsdocs build

# Windows
\path\to\FSharp.Formatting\src\fsdocs-tool\bin\Debug\net10.0\fsdocs.exe build
```
