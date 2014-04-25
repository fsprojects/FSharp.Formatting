namespace FSharp.Literate

open System
open System.IO
open FSharp.Markdown
open FSharp.CodeFormat

open Microsoft.FSharp.Compiler.ErrorLogger
open Microsoft.FSharp.Compiler.Interactive.Shell

// ------------------------------------------------------------------------------------------------
// Helpers needed by the evaluator
// ------------------------------------------------------------------------------------------------

module EvaluatorHelpers =

  /// Represents a simple (fake) event loop for the 'fsi' object
  type SimpleEventLoop () = 
    member x.Run () = ()
    member x.Invoke<'T>(f:unit -> 'T) = f()
    member x.ScheduleRestart() = ()

  /// Implements a simple 'fsi' object to be passed to the FSI evaluator
  [<Sealed>]
  type InteractiveSettings()  = 
      let mutable evLoop = (new SimpleEventLoop())
      let mutable showIDictionary = true
      let mutable showDeclarationValues = true
      let mutable args = Environment.GetCommandLineArgs()
      let mutable fpfmt = "g10"
      let mutable fp = (System.Globalization.CultureInfo.InvariantCulture :> System.IFormatProvider)
      let mutable printWidth = 78
      let mutable printDepth = 100
      let mutable printLength = 100
      let mutable printSize = 10000
      let mutable showIEnumerable = true
      let mutable showProperties = true
      let mutable addedPrinters = []

      member self.FloatingPointFormat with get() = fpfmt and set v = fpfmt <- v
      member self.FormatProvider with get() = fp and set v = fp <- v
      member self.PrintWidth  with get() = printWidth and set v = printWidth <- v
      member self.PrintDepth  with get() = printDepth and set v = printDepth <- v
      member self.PrintLength  with get() = printLength and set v = printLength <- v
      member self.PrintSize  with get() = printSize and set v = printSize <- v
      member self.ShowDeclarationValues with get() = showDeclarationValues and set v = showDeclarationValues <- v
      member self.ShowProperties  with get() = showProperties and set v = showProperties <- v
      member self.ShowIEnumerable with get() = showIEnumerable and set v = showIEnumerable <- v
      member self.ShowIDictionary with get() = showIDictionary and set v = showIDictionary <- v
      member self.AddedPrinters with get() = addedPrinters and set v = addedPrinters <- v
      member self.CommandLineArgs with get() = args  and set v  = args <- v
      member self.AddPrinter(printer : 'T -> string) =
        addedPrinters <- Choice1Of2 (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: addedPrinters

      member self.EventLoop
          with get () = evLoop
          and set (x:SimpleEventLoop)  = ()

      member self.AddPrintTransformer(printer : 'T -> obj) =
        addedPrinters <- Choice2Of2 (typeof<'T>, (fun (x:obj) -> printer (unbox x))) :: addedPrinters

// ------------------------------------------------------------------------------------------------
// Evaluator
// ------------------------------------------------------------------------------------------------

open EvaluatorHelpers

/// Represents the result of evaluating an F# snippet. This contains
/// the generated console output together with a result and its static type.
type FsiEvaluationResult = 
  { Output : string option
    ItValue : (obj * Type) option
    Result : (obj * Type) option }

/// Record that is reported by the `EvaluationFailed` event when something
/// goes wrong during evalutaiton of an expression
type FsiEvaluationFailedInfo = 
  { Text : string
    AsExpression : bool
    File : string option
    Exception : exn }

/// A wrapper for F# interactive serivice that is used to evaluate inline snippets
type FsiEvaluator(?options:string[]) =
  // Initialize F# Interactive evaluation session
  let inStream = new StringReader("")
  let sbOut = new Text.StringBuilder()
  let sbErr = new Text.StringBuilder()
  let outStream = new StringWriter(sbOut)
  let errStream = new StringWriter(sbErr)
  let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration(new InteractiveSettings())
  let argv = Array.append [|"C:\\test.exe"; "--quiet"; "--noninteractive"|] (defaultArg options [||])
  let fsiSession = FsiEvaluationSession(fsiConfig, argv, inStream, outStream, errStream)
  let evalFailed = new Event<_>()

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
    let asExpression = defaultArg asExpression false
    try
      file |> Option.iter (fun file ->
        let dir = Path.GetDirectoryName(file)
        fsiSession.EvalInteraction(sprintf "System.IO.Directory.SetCurrentDirectory(@\"%s\")" dir)
        fsiSession.EvalInteraction(sprintf "#cd @\"%s\"" dir) )
      sbOut.Clear() |> ignore
      let sbConsole = new Text.StringBuilder()
      let prev = Console.Out
      try //try..finally to make sure console.out is re-set to prev
        Console.SetOut(new StringWriter(sbConsole))
        let value, itvalue =
          if asExpression then
              match fsiSession.EvalExpression(text) with
              | Some value -> Some(value.ReflectionValue, value.ReflectionType), None
              | None -> None, None
          else
            fsiSession.EvalInteraction(text)
            // try get the "it" value, but silently ignore any errors
            try 
              match fsiSession.EvalExpression("it") with
              | Some value -> None, Some(value.ReflectionValue, value.ReflectionType)
              | None -> None, None
            with _ -> None, None
        let output = Some(sbConsole.ToString())
        { Output = output; Result = value; ItValue = itvalue  }
      finally
        Console.SetOut(prev)
    with e ->
      evalFailed.Trigger({ File=file; AsExpression=asExpression; Text=text; Exception=e })
      { Output = None; Result = None; ItValue = None }

    /// This event is fired whenever an evaluation of an expression fails
    member x.EvaluationFailed = evalFailed.Publish