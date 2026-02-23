# Agent Guidelines

## Release Notes

Update `RELEASE_NOTES.md` when making user-visible changes:
- Add entries under the `## [Unreleased]` section
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

# Run analyzers (G-Research.FSharp.Analyzers, Ionide.Analyzers)
dotnet msbuild /t:AnalyzeSolution
```

Individual steps:

```bash
dotnet restore FSharp.Formatting.sln
dotnet build FSharp.Formatting.sln --configuration Release
dotnet test FSharp.Formatting.sln --configuration Release --no-build
```
