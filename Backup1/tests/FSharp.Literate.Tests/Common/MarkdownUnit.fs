module FSharp.Markdown.Unit

open FSharp.Markdown
open NUnit.Framework

let shouldMatchPar f pars =
  let rec loop par = 
    if f par then true else
    match par with
    | Matching.ParagraphNested(_, pars) -> List.exists (List.exists loop) pars
    | _ -> false
  Assert.IsTrue(Seq.exists loop pars, "Should match the specified paragraph")

let shouldMatchSpan f pars =
  let rec loopSpan sp = 
    if f sp then true else
    match sp with
    | Matching.SpanLeaf _ -> false
    | Matching.SpanNode(_, sps) -> List.exists loopSpan sps
  let rec loopPar par = 
    match par with 
    | Matching.ParagraphSpans(_, sps) -> List.exists loopSpan sps
    | Matching.ParagraphLeaf _ -> false
    | Matching.ParagraphNested(_, pars) -> List.exists (List.exists loopPar) pars
  Assert.IsTrue(Seq.exists loopPar pars, "Should match the specified span")