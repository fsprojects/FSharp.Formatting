(**
---
title: Mermaid Diagrams
category: Examples
categoryindex: 2
index: 4
---

# Example: Mermaid Diagrams

[Mermaid](https://mermaid.js.org/) is a JavaScript-based diagramming and charting tool that renders Markdown-inspired text definitions into diagrams.

The recommended way to use Mermaid with fsdocs is to write diagrams as plain fenced code blocks, and add a small script that turns those blocks into diagrams when the page loads. The Markdown stays portable: GitHub renders ```` ```mermaid ```` fences natively, and your fsdocs site shows the real diagrams. This very page uses the pattern below, so the diagrams you see are fenced code blocks promoted by a `_body.html` script.

## Setup

Create or edit a `_body.html` file in your `docs` folder. fsdocs injects it at the end of every page, after the content. The script imports mermaid and promotes fenced mermaid blocks to the `<div class="mermaid">` elements mermaid looks for:

````html
<script type="module">
  import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';

  // A ```mermaid fenced block renders natively on GitHub, but reaches fsdocs
  // as a syntax-highlighted code block. Promote those blocks to
  // <div class="mermaid"> elements so one plain fence works in both places.
  for (const code of document.querySelectorAll('code[lang="mermaid"]')) {
    // fsdocs wraps the snippet in <table class="pre"><tr><td><pre><code>...;
    // replace the outermost wrapper so no table scaffolding is left around
    // the diagram.
    const snippet = code.closest('table.pre') ?? code.closest('pre');
    if (!snippet) continue;
    const diagram = document.createElement('div');
    diagram.className = 'mermaid';
    // textContent, not innerHTML: the source arrives HTML-escaped
    // (arrows come through with escaped angle brackets) and mermaid needs
    // the raw arrows back.
    diagram.textContent = code.textContent;
    snippet.replaceWith(diagram);
  }

  mermaid.initialize({ startOnLoad: true });
</script>
````

## Usage

Write your diagram in a fenced code block with the `mermaid` language tag:

````text
```mermaid
graph LR
    A[Input docs] --> B[fsdocs build]
    B --> C[HTML output]
    B --> D[API reference]
```
````

On this site, the block above is rendered as:

```mermaid
graph LR
    A[Input docs] --> B[fsdocs build]
    B --> C[HTML output]
    B --> D[API reference]
```

## More Examples

Sequence diagram:

```mermaid
sequenceDiagram
    participant User
    participant fsdocs
    participant Browser
    User->>fsdocs: dotnet fsdocs watch
    fsdocs-->>Browser: Serve docs
    User->>fsdocs: Edit .md or .fsx
    fsdocs-->>Browser: Reload page
```

Class diagram:

```mermaid
classDiagram
    class ApiDocComment {
        +Summary: string
        +Remarks: string option
        +Parameters: ApiDocSection list
    }
    class ApiDocMember {
        +Name: string
        +Comment: ApiDocComment
    }
    ApiDocMember --> ApiDocComment
```

## Tips

* To customise the Mermaid theme, pass options to `mermaid.initialize()`, for example `theme: "base"` together with `themeVariables`.
* To centre the diagrams, add a rule for the promoted element to your `docs/content/fsdocs-theme.css`:

```css
.mermaid {
  margin: 1rem auto;
  & svg {
    display: block;
    margin: 0 auto;
  }
}
```

* You can also write `<div class="mermaid">` blocks directly in your Markdown. The promotion script only touches fenced code blocks, so both forms can coexist.
* See the [Mermaid documentation](https://mermaid.js.org/intro/) for the full list of supported diagram types.

*)