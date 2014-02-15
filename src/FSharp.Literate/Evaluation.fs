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

  type FsiEvaluator(?options:string[]) =
    // Initialize F# Interactive evaluation session
    let inStream = new StringReader("")
    let sbOut = new Text.StringBuilder()
    let sbErr = new Text.StringBuilder()

    let outStream = new StringWriter(sbOut)
    let errStream = new StringWriter(sbErr)
    let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration()
    let argv = Array.append [|"C:\\test.exe"; "--noninteractive"|] (defaultArg options [||])
    let fsiSession = FsiEvaluationSession(fsiConfig, argv, inStream, outStream, errStream)

    /// Evaluates the given text in an fsi session and returns
    /// and FsiEvaluationResult.
    /// If evaluated as an expression, Result should be set with the
    /// result of evaluating the text as an F# expression.
    /// If not, just the console output of the evaluation is captured and
    /// returned in Output.
    /// If file is set, the text will be evaluated as if it was present in the
    /// given script file - this is for correct usage of #I and #r with relative paths.
    /// Note however that __SOURCE_DIRECTORY___ does not currently pick this up.
    member x.Evaluate(text:string, ?asExpression, ?file) =
      try
        let asExpression = defaultArg asExpression false
        file |> Option.iter (Path.GetDirectoryName >> sprintf "#cd @\"%s\""  >> fsiSession.EvalInteraction)
        sbOut.Clear() |> ignore
        let sbConsole = new Text.StringBuilder()
        let prev = Console.Out
        try //try..finally to make sure console.out is re-set to prev
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
          { Output = output; Result = value  }
        finally
          Console.SetOut(prev)
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
  let rec private evalBlocks (fsi:FsiEvaluator) file acc (paras:MarkdownParagraphs) = 
    match paras with
    | EmbedParagraphs(para)::paras ->
      match para :?> LiterateParagraph with
      | OutputReferencedCode (name,snip) ->
          let text = unparse snip
          let result = fsi.Evaluate(text, file=file)
          evalBlocks fsi file (((name,false),result)::acc) paras
      | HiddenCode(_,snip)
      | FormattedCode(snip) ->
        //need to eval because subsequent code might refer it, but we don't need result
        let text = unparse snip
        let result = fsi.Evaluate(text, file=file)
        evalBlocks fsi file acc paras
      | ValueReference (ref,None) -> 
        let result = fsi.Evaluate(ref,asExpression=true,file=file)
        evalBlocks fsi file (((ref,true),result)::acc) paras
      | _ -> evalBlocks fsi file acc paras
    | para::paras -> evalBlocks fsi file acc paras
    | [] -> acc

  let eval fsi (doc:LiterateDocument) = 
    let evaluations = evalBlocks fsi doc.SourceFile [] doc.Paragraphs |> Map.ofList
    evaluations
