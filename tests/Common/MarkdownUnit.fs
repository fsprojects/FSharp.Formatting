module FSharp.Formatting.Markdown.Unit

open FSharp.Formatting.Markdown
open NUnit.Framework

let shouldMatchPar f pars =
    let rec loop par =
        if f par then
            true
        else
            match par with
            | MarkdownPatterns.ParagraphNested (_, pars) -> List.exists (List.exists loop) pars
            | _ -> false

    Assert.IsTrue(Seq.exists loop pars, "Should match the specified paragraph")

let shouldMatchSpan f pars =
    let rec loopSpan sp =
        if f sp then
            true
        else
            match sp with
            | MarkdownPatterns.SpanLeaf _ -> false
            | MarkdownPatterns.SpanNode (_, sps) -> List.exists loopSpan sps

    let rec loopPar par =
        match par with
        | MarkdownPatterns.ParagraphSpans (_, sps) -> List.exists loopSpan sps
        | MarkdownPatterns.ParagraphLeaf _ -> false
        | MarkdownPatterns.ParagraphNested (_, pars) -> List.exists (List.exists loopPar) pars

    Assert.IsTrue(Seq.exists loopPar pars, "Should match the specified span")
