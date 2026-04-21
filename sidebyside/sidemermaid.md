---
title: Mermaid Diagrams
category: Examples
categoryindex: 2
index: 4
---

# Example: Mermaid Diagrams

[Mermaid](https://mermaid.js.org/) is a JavaScript-based diagramming and charting tool that renders Markdown-inspired text definitions into diagrams.

## Setup

Add the Mermaid JavaScript library to your site by creating or editing a `_head.html` file in your `docs` folder:

```html
<script type="module">
  import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
</script>
```

## Usage

To embed a Mermaid diagram, wrap your Mermaid syntax in a `<div>` element with the `mermaid` CSS class:

```html
<div class="mermaid">
graph LR
    A[Input docs] --> B[fsdocs build]
    B --> C[HTML output]
    B --> D[API reference]
</div>
```

This renders as:

<div class="mermaid">
graph LR
    A[Input docs] --> B[fsdocs build]
    B --> C[HTML output]
    B --> D[API reference]
</div>
## More Examples

Sequence diagram:

<div class="mermaid">
sequenceDiagram
    participant User
    participant fsdocs
    participant Browser
    User->>fsdocs: dotnet fsdocs watch
    fsdocs-->>Browser: Serve docs
    User->>fsdocs: Edit .md or .fsx
    fsdocs-->>Browser: Reload page
</div>
Class diagram:

<div class="mermaid">
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
</div>
## Tips

* You can also use `<div class="mermaid text-center">` to centre the diagram on the page.
* To customise the Mermaid theme, pass options to `mermaid.initialize()` before the `import` call.
* See the [Mermaid documentation](https://mermaid.js.org/intro/) for the full list of supported diagram types.
