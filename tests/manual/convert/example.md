# Example Markdown Document

This is a **sample** document for testing `fsdocs convert`.

## F# Code

A fenced code block:

```fsharp
let greet name =
    printfn "Hello, %s!" name

greet "World"
```

## A Table

| Column A | Column B | Column C |
|----------|----------|----------|
| One      | Two      | Three    |
| Alpha    | Beta     | Gamma    |

## Links and Images

A [relative link](example.fsx) and an [external link](https://fsharp.org).

## Images

A local PNG image (will be inlined as a base64 data URI when `--embed-resources` is active):

![Sample PNG](images/sample.png)

A local SVG image (inlined as `image/svg+xml`):

![Sample SVG](images/sample.svg)

## Maths-like content

Some `inline code` and a list:

- Item one
- Item two
  - Nested item
- Item three
