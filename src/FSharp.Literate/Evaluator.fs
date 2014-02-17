namespace FSharp.Literate

open System
open System.IO
open FSharp.Markdown
open FSharp.CodeFormat

open Microsoft.FSharp.Compiler.ErrorLogger
open Microsoft.FSharp.Compiler.Interactive.Shell

/// Represents the result of evaluating an F# snippet. This contains
/// the generated console output together with a result and its static type.
type FsiEvaluationResult = 
  { Output : string option
    Result : (obj * Type) option }

/// A wrapper for F# interactive serivice that is used to evaluate inline snippets
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

  /// Registered transformations for pretty printing values
  /// (the default formats value as a string and emits single CodeBlock)
  let mutable valueTransformations = 
    [ (fun (o:obj, t:Type) ->Some([CodeBlock(sprintf "%A" o)]) ) ]

  /// Register a function that formats (some) values that are produced by the evaluator.
  /// The specified function should return 'Some' when it knows how to format a value
  /// and it should return formatted 
  member x.RegisterTransformation(f) =
    valueTransformations <- f::valueTransformations

  /// Format a specified output (produces Markdown code block at the moment)
  member internal x.FormatOutput(s) =
    [ CodeBlock(s) ]

  /// Format a specified value and produce a markdown document as the result
  member internal x.FormatValue(obj,typ) =
    valueTransformations |> Seq.pick (fun f -> f (obj, typ))

  /// Evaluates the given text in an fsi session and returns
  /// an FsiEvaluationResult.
  ///
  /// If evaluated as an expression, Result should be set with the
  /// result of evaluating the text as an F# expression.
  /// If not, just the console output of the evaluation is captured and
  /// returned in Output.
  ///
  /// If file is set, the text will be evaluated as if it was present in the
  /// given script file - this is for correct usage of #I and #r with relative paths.
  /// Note however that __SOURCE_DIRECTORY___ does not currently pick this up.
  member internal x.Evaluate(text:string, ?asExpression, ?file) =
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
