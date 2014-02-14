namespace FSharp.Literate

open System
open System.IO
open FSharp.Markdown
open FSharp.CodeFormat

open Microsoft.FSharp.Compiler.ErrorLogger
open Microsoft.FSharp.Compiler.Interactive.Shell

module Evaluation = 

  type FsiEvaluationResult = 
    { Output : string option
      Result : (obj * Type) option }

  type FsiEvaluator() =
    // Initialize F# Interactive evaluation session
    let inStream = new StringReader("")
    let sbOut = new Text.StringBuilder()
    let sbErr = new Text.StringBuilder()

    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
    let fsiSession = FsiEvaluationSession(fsiConfig, [|"C:\\test.exe"; "--noninteractive"|], inStream, outStream, errStream)

    member x.Evaluate(text:string, ?asExpression) =
      try
          let asExpression = defaultArg asExpression false
          sbOut.Clear() |> ignore
          let sbConsole = new Text.StringBuilder()
          let prev = Console.Out
          Console.SetOut(new StringWriter(sbConsole))
          let value =
            if asExpression then
                match fsiSession.EvalExpression(text) with
                | Some value -> Some(value.ReflectionValue, value.ReflectionType)
                | None -> None
            else
              fsiSession.EvalInteraction(text)
              None
          let output = Some(sbConsole.ToString())
          Console.SetOut(prev)
          { Output = output; Result = value  }
      with e ->
        match e.InnerException with
        | null -> printfn "Error evaluating expression (%s)" e.Message
        | WrappedError(err, _) -> printfn "Error evaluating expression (%s)" err.Message
        | _ -> printfn "Error evaluating expression (%s)" e.Message
        { Output = None; Result = None }

  // ----------------------------------------------------------------------------------------------
  // ?
  // ----------------------------------------------------------------------------------------------


  /// Unparse a Line list to a string - for evaluation by fsi.
  let private unparse (lines: Line list) =
    let joinLine (Line spans) =
      spans
      |> Seq.map (fun span -> match span with Token (_,s,_) -> s | Omitted (s1,s2) -> s2 | _ -> "")
      |> String.concat ""
    lines
    |> Seq.map joinLine
    |> String.concat "\n"

  /// Evaluate all the snippets in a literate document, returning the results.
  /// The result is a map of string * bool to FsiEvaluationResult. The bool indicates
  /// whether the result is a top level variable (i.e. include-value) or a reference to 
  /// some output (i.e. define-output and include-output). This just to put each of those
  /// names in a separate scope.
  let rec private evalBlocks (fsi:FsiEvaluator) acc (paras:MarkdownParagraphs) = 
    match paras with
    | EmbedParagraphs(para)::paras ->
      match para :?> LiterateParagraph with
      | OutputReferencedCode (name,snip) ->
          let text = unparse snip
          let result = fsi.Evaluate text
          evalBlocks fsi (((name,false),result)::acc) paras
      | HiddenCode(_,snip)
      | FormattedCode(snip) ->
        //need to eval because subsequent code might refer it, but we don't need result
        let text = unparse snip
        let result = fsi.Evaluate text
        evalBlocks fsi acc paras
      | ValueReference (ref,None) -> 
        let result = fsi.Evaluate(ref,asExpression=true)
        evalBlocks fsi (((ref,true),result)::acc) paras
      | _ -> evalBlocks fsi acc paras
    | para::paras -> evalBlocks fsi acc paras
    | [] -> acc

  let eval fsi (doc:LiterateDocument) = 
    let evaluations = evalBlocks fsi [] doc.Paragraphs |> Map.ofList
    evaluations
