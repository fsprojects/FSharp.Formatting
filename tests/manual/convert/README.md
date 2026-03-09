# Manual Validation: `fsdocs convert`

This directory contains example files for manually validating the `fsdocs convert` command,
including the `--template fsdocs` and `--no-embed-resources` options added in PR #1072.

## Prerequisites

Build the tool from the repo root:

```bash
dotnet build FSharp.Formatting.sln
```

Set a variable to avoid repetition (Linux/macOS):

```bash
FSDOCS=/path/to/FSharp.Formatting/src/fsdocs-tool/bin/Debug/net10.0/fsdocs.dll
# e.g.:
FSDOCS=$(pwd)/src/fsdocs-tool/bin/Debug/net10.0/fsdocs.dll
```

On Windows (PowerShell):

```powershell
$FSDOCS = ".\src\fsdocs-tool\bin\Debug\net10.0\fsdocs.dll"
```

All commands below use `dotnet $FSDOCS` (Linux/macOS).  
On Windows substitute `dotnet $FSDOCS` → `dotnet $FSDOCS`.  
Run commands from the **repo root**, or replace `tests/manual/convert/` with the full path.

---

## Scenario 1 — Raw HTML (no template)

```bash
dotnet $FSDOCS convert tests/manual/convert/example.md
```

**Expected output file:** `example.html` (in the current directory)

**What to check:**
- The file contains rendered HTML for the headings, code block, table.
- There is **no** `<html>` wrapper / `<head>` section — just the body fragment.
- No `{{...}}` placeholder text appears anywhere.

---

## Scenario 2 — Minimal custom template (recommended starting point)

```bash
dotnet $FSDOCS convert tests/manual/convert/example.md \
    --template tests/manual/convert/_template-minimal.html \
    -o /tmp/example-minimal.html
```

**What to check:**
- The file is a **complete, self-contained HTML document** (has `<html>`, `<head>`, `<body>`).
- CSS from `content/fsdocs-default.css` and `content/fsdocs-theme.css` is **inlined** as `<style>` blocks (no external `href` remaining).
- `<title>` is `example` (input filename without extension).
- No `{{...}}` placeholder text appears anywhere.
- Opening the file in a browser renders the content with styling applied.

---

## Scenario 3 — Built-in `fsdocs` template (full site template)

```bash
dotnet $FSDOCS convert tests/manual/convert/example.md \
    --template fsdocs \
    -o /tmp/example-fsdocs.html
```

**What to check:**
- The file is a **complete, self-contained HTML document**.
- CSS and JS assets are **inlined** (no external references to `content/` directory).
- The `<title>` is `example | example` (title | collection-name, both default to the filename).
- No `{{...}}` placeholder text appears anywhere.
- The page has the standard fsdocs chrome (header/nav/sidebar/footer).
- Navigation links (License, Release Notes, Source Repository) exist but point to `#` (empty defaults).
- The logo area shows the page title text; the logo `<img>` src is empty (no image shown).
- Opening the file in a browser renders the content with fsdocs styling applied.

> **Note:** The full fsdocs template is designed for a documentation site with a populated project.
> Many navigation elements will be empty or link to `#` when used with `fsdocs convert` alone.
> For polished output, use `_template-minimal.html` or supply `--parameters` to fill in the blanks.

---

## Scenario 4 — Built-in template with custom parameters

```bash
dotnet $FSDOCS convert tests/manual/convert/example.md \
    --template fsdocs \
    --parameters fsdocs-page-title "My Page" fsdocs-collection-name "My Project" fsdocs-repository-link "https://github.com/example/repo" \
    -o /tmp/example-custom-params.html
```

**What to check:**
- `<title>` contains `My Page | My Project`.
- The repository link in the nav points to `https://github.com/example/repo`.
- No `{{...}}` placeholder text appears anywhere.

---

## Scenario 5 — F# script input

```bash
dotnet $FSDOCS convert tests/manual/convert/example.fsx \
    --template tests/manual/convert/_template-minimal.html \
    -o /tmp/example-fsx.html
```

**What to check:**
- F# code blocks are syntax-highlighted.
- Literate comments (`(** ... *)`) are rendered as formatted paragraphs.
- No `{{...}}` placeholder text appears.

---

## Scenario 6 — Disable resource embedding

```bash
dotnet $FSDOCS convert tests/manual/convert/example.md \
    --template tests/manual/convert/_template-minimal.html \
    --no-embed-resources \
    -o /tmp/example-no-embed.html
```

**What to check:**
- The output still references CSS via `<link href="content/fsdocs-default.css" ...>` (not inlined).
- Opening in a browser from a directory without a `content/` folder will show **unstyled** content (expected).
- No `{{...}}` placeholder text appears.

---

## Scenario 7 — Non-HTML output format

```bash
dotnet $FSDOCS convert tests/manual/convert/example.md \
    --outputformat latex \
    -o /tmp/example.tex
```

**What to check:**
- A `.tex` file is produced.
- No resource embedding occurs (embedding is HTML-only).

---

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Raw `{{fsdocs-...}}` text visible in browser | Forgetting `--template` | Add `--template fsdocs` or `--template _template-minimal.html` |
| Styled correctly only when served, not when opened as a file | `--no-embed-resources` was set, or embedding failed to find assets | Remove `--no-embed-resources`, or check that `content/` is in a search path |
| `<title>` shows filename, not a nice title | Default — no `fsdocs-page-title` parameter | Supply `--parameters fsdocs-page-title "Nice Title"` |
| Navigation links go to `#` | No project metadata available in standalone convert mode | Supply `--parameters fsdocs-repository-link "..."` etc., or use a simpler template |

---

## Answering questions from PR #1072 review

### Q1: No default template without `--template`?

By design. Without `--template`, `fsdocs convert` produces a **raw HTML fragment** (just the
document body, no `<html>` wrapper). This matches the behaviour before PR #1072 and is intentional
— convert is opt-in for templates.

To get the default fsdocs template automatically, pass `--template fsdocs`.

Note: running `dotnet fsdocs.dll convert file.fsx` vs `fsdocs convert file.fsx` behaves identically;
the `.dll` suffix doesn't change anything.

### Q2: Do we expect substitutions to be applied?

**Yes, and they now are** (fixed in PR #1072 follow-up). Before the fix, using `--template fsdocs`
would leave `{{fsdocs-page-title}}`, `{{fsdocs-collection-name}}` and many other placeholders as
literal text in the output — because `fsdocs convert` did not populate the project-level template
parameters that `fsdocs build` normally provides.

After the fix, `fsdocs convert` supplies sensible defaults for all standard template parameters:

| Parameter | Default value |
|-----------|--------------|
| `{{fsdocs-page-title}}` | Input filename (without extension) |
| `{{fsdocs-collection-name}}` | Input filename (without extension) |
| `{{fsdocs-source-basename}}` | Input filename (without extension) |
| `{{fsdocs-source-filename}}` | Input filename (with extension) |
| `{{fsdocs-body-class}}` | `content` |
| `{{fsdocs-license-link}}` | `#` |
| `{{fsdocs-repository-link}}` | `#` |
| `{{fsdocs-release-notes-link}}` | `#` |
| `{{fsdocs-logo-link}}` | `#` |
| `{{fsdocs-logo-alt}}` | Input filename (without extension) |
| All other `{{fsdocs-*}}` | `""` (empty string) |

User-supplied `--parameters` values always override these defaults.
