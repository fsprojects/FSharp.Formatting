(*** condition: prepare ***)
#I "../src/FSharp.Formatting/bin/Release/netstandard2.1"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Formatting.Markdown.dll"
(*** condition: fsx ***)
#if FSX
#r "nuget: FSharp.Formatting,{{package-version}}"
#endif // FSX
(*** condition: ipynb ***)
#if IPYNB
#r "nuget: FSharp.Formatting,{{package-version}}"
#endif // IPYNB


(**
[![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=markdown.ipynb)

Markdown parser
==============================

This page demonstrates how to use `FSharp.Formatting.Markdown.dll` to parse a Markdown
document, process the obtained document representation and
how to turn the code into a nicely formatted HTML.

First, we need to load the assembly and open necessary namespaces:
*)

open FSharp.Formatting.Markdown
open FSharp.Formatting.Common


(**
Parsing documents
-----------------

The F# Markdown parser recognizes the standard [Markdown syntax](http://daringfireball.net/projects/markdown/)
and it is not the aim of this tutorial to fully document it. 
The following snippet creates a simple string containing a document
with several elements and then parses it using the `Markdown.Parse` method:
*)

let document = """
# F# Hello world
Hello world in [F#](http://fsharp.net) looks like this:

    printfn "Hello world!"

For more see [fsharp.org][fsorg].

  [fsorg]: http://fsharp.org "The F# organization." """

let parsed = Markdown.Parse(document)

(**
The sample document consists of a first-level heading (written using 
one of the two alternative styles) followed by a paragraph with a 
_direct_ link, code snippet and one more paragraph that includes an 
_indirect_ link. The URLs of indirect links are defined by a separate
block as demonstrated on the last line (and they can then be easily used repeatedly
from multiple places in the document).

Working with parsed documents
-----------------------------

The F# Markdown processor does not turn the document directly into HTML.
Instead, it builds a nice F# data structure that we can use to analyze, 
transform and process the document. First of all the `DefinedLinks` property
returns all indirect link definitions:
*)

parsed.DefinedLinks
// [fsi:val it : IDictionary<string,(string * string option)> =]
// [fsi:  dict [("fsorg", ("http://fsharp.org", Some "The F# organization."))]]

(**
The document content can be accessed using the `Paragraphs` property that returns
a sequence of paragraphs or other first-level elements (headings, quotes, code snippets, etc.).
The following snippet prints the heading of the document:
*)

// Iterate over all the paragraph elements
for par in parsed.Paragraphs do
  match par with
  | Heading(size=1; body=[Literal(text=text)]) -> 
      // Recognize heading that has a simple content
      // containing just a literal (no other formatting)
      printfn "%s" text
  | _ -> ()

(**
You can find more detailed information about the document structure and how to process it
in the book [F# Deep Dives](http://manning.com/petricek2/).

Processing the document recursively
-----------------------------------

The library provides active patterns that can be used to easily process the Markdown 
document recursively. The example in this section shows how to extract all links from the
document. To do that, we need to write two recursive functions. One that will process
all paragraph-style elements and one that will process all inline formattings (inside
paragraphs, headings etc.).

To avoid pattern matching on every single kind of span and every single kind of 
paragraph, we can use active patterns from the `MarkdownPatterns` module. These can be use
to recognize any paragraph or span that can contain child elements:

*)

/// Returns all links in a specified span node
let rec collectSpanLinks span = seq {
  match span with
  | DirectLink(link=url) -> yield url
  | IndirectLink(key=key) -> yield fst (parsed.DefinedLinks.[key])
  | MarkdownPatterns.SpanLeaf _ -> ()
  | MarkdownPatterns.SpanNode(_, spans) ->
      for s in spans do yield! collectSpanLinks s }
      
/// Returns all links in the specified paragraph node
let rec collectParLinks par = seq {
  match par with
  | MarkdownPatterns.ParagraphLeaf _ -> ()
  | MarkdownPatterns.ParagraphNested(_, pars) -> 
      for ps in pars do 
        for p in ps do yield! collectParLinks p 
  | MarkdownPatterns.ParagraphSpans(_, spans) ->
      for s in spans do yield! collectSpanLinks s }

/// Collect links in the entire document
Seq.collect collectParLinks parsed.Paragraphs
// [fsi:val it : seq<string> =]
// [fsi:  seq ["http://fsharp.net"; "http://fsharp.org"]]

(**
The `collectSpanLinks` function works on individual span elements that contain inline
formatting (emphasis, strong) and also links. The `DirectLink` node represents an inline
link like the one pointing to <http://fsharp.net> while `IndirectLink` represents a
link that uses one of the link definitions. The function simply returns the URL associated
with the link.

Some span nodes (like emphasis) can contain other formatting, so we need to recursively
process children. This is done by matching against `MarkdownPatterns.SpanNodes` which is an active
pattern that recognizes any node with children. The library also provides a _function_
named `MarkdownPatterns.SpanNode` that can be used to reconstruct the same node (when you want
to transform document). This is similar to how the `ExprShape` module for working with 
F# quotations works.

The function `collectParLinks` processes paragraphs - a paragraph cannot directly be a 
link so we just need to process all spans. This time, there are three options. 
`ParagraphLeaf` represents a case where the paragraph does not contain any spans
(a code block or, for example, a `<hr>` line); the `ParagraphNested` case is used for paragraphs
that contain other paragraphs (quotation) and `ParagraphSpans` is used for all other
paragraphs that contain normal text - here we call `collectSpanLinks` on all nested spans.

Generating HTML output
----------------------

Finally, the `Markdown` type also includes a method `ToHtml` that can be used
to generate an HTML document from the Markdown input. The following example shows how to call it:
*)
let html = Markdown.ToHtml(parsed)

(**
In addition, you can also use `Markdown.TransformHtml` to directly turn an input document
in the Markdown format into an HTML document (without the intermediate step).
*)
