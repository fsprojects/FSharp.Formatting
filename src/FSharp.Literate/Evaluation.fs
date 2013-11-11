namespace FSharp.Literate

open System
open System.IO
open FSharp.Markdown

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
    let fsi = FsiEvaluationSession([|"C:\\test.exe"; "--noninteractive"|], inStream, outStream, errStream)

    member x.Evaluate(text:string) =
      try
        if text.StartsWith("=") then 
          // Evaluate an expression and print the result
          sbOut.Clear() |> ignore

          let sbConsole = new Text.StringBuilder()
          let prev = Console.Out
          Console.SetOut(new StringWriter(sbConsole))
          let value =
            match fsi.EvalExpression(text.Substring(1)) with
            | Some value -> Some(value.ReflectionValue, value.ReflectionType)
            | None -> None
          let output = 
            if String.IsNullOrEmpty(sbOut.ToString()) then None
            else Some(sbOut.ToString())
          let output = Some(sbConsole.ToString())
          Console.SetOut(prev)
          { Output = output; Result = value }
        else
          // Evaluate top-level interaction which is not an expression
          // (such as 'let foo = 10')
          fsi.EvalInteraction(text)
          { Output = None; Result = None }
      with e ->
        match e.InnerException with
        | null -> printfn "Error evaluating expression (%s)" e.Message
        | WrappedError(err, _) -> printfn "Error evaluating expression (%s)" err.Message
        | _ -> printfn "Error evaluating expression (%s)" e.Message
        { Output = None; Result = None }

  // ----------------------------------------------------------------------------------------------
  // ?
  // ----------------------------------------------------------------------------------------------

  let eval ctx (doc:LiterateDocument) = 
    let fsi = FsiEvaluator()
    let res = fsi.Evaluate("=1 + 2")
    printfn "%A" res
    doc
