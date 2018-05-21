namespace FSharp.Literate

open System
open System.IO
open FSharp.Markdown
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Interactive.Shell
open FSharp.CodeFormat
open Yaaf.FSharp.Scripting
open Yaaf.FSharp.Scripting.Extensions
open System.Text


// ------------------------------------------------------------------------------------------------
// Evaluator
// ------------------------------------------------------------------------------------------------

/// Represents a kind of thing that can be embedded
[<RequireQualifiedAccessAttribute>]
type FsiEmbedKind =
  | Output
  | ItValue
  | Value

/// An interface that represents FSI evaluation result
/// (we make this abstract so that evaluators can store other info)
type IFsiEvaluationResult = interface end

/// Represents the result of evaluating an F# snippet. This contains
/// the generated console output together with a result and its static type.
type FsiEvaluationResult =
  { Output : string option
    ItValue : (obj * Type) option
    Result : (obj * Type) option }
  interface IFsiEvaluationResult

/// Record that is reported by the `EvaluationFailed` event when something
/// goes wrong during evalutaiton of an expression
type FsiEvaluationFailedInfo =
  { Text : string
    AsExpression : bool
    File : string option
    Exception : exn
    StdErr : string }
    override x.ToString() =
      let indent (s:string) =
        s.Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map(fun x -> "    " + x)
        |> fun x -> String.Join("\n", x)
      sprintf "Error evaluating expression \nExpression:\n%s\nError:\n%s" (indent x.Text) (indent x.StdErr)

/// Represents an evaluator for F# snippets embedded in code
type IFsiEvaluator =
  /// Called to format some part of evaluation result generated by FSI
  abstract Format : IFsiEvaluationResult * FsiEmbedKind -> MarkdownParagraphs
  /// Called to evaluate a snippet
  abstract Evaluate : string * asExpression:bool * file:string option -> IFsiEvaluationResult

/// Represents a simple (fake) event loop for the 'fsi' object
type private NoOpFsiEventLoop () =
  member x.Run () = ()
  member x.Invoke<'T>(f:unit -> 'T) = f()
  member x.ScheduleRestart() = ()

/// Implements a simple 'fsi' object to be passed to the FSI evaluator
[<Sealed>]
type private NoOpFsiObject()  =
  let mutable evLoop = new NoOpFsiEventLoop()
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
  let mutable addedPrinters : list<Choice<System.Type * (obj -> string), System.Type * (obj -> obj)>> = []

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
  member self.AddPrinter(printer : 'T -> string) = ()
  member self.EventLoop with get () = evLoop and set (x:NoOpFsiEventLoop)  = ()
  member self.AddPrintTransformer(printer : 'T -> obj) = ()

/// Provides configuration options for the `FsiEvaluator`
type FsiEvaluatorConfig() =
  /// Creates a dummy `fsi` object that does not affect the behaviour of F# Interactive
  /// (and simply ignores all operations that are done on it). You can use this to
  /// e.g. disable registered printers that would open new windows etc.
  static member CreateNoOpFsiObject() = box (new NoOpFsiObject())

/// A wrapper for F# interactive service that is used to evaluate inline snippets
type FsiEvaluator(?options:string[], ?fsiObj) =
  // Initialize F# Interactive evaluation session

  // Intialize output and input streams
  let sbOut = new StringBuilder()
  let sbErr = new StringBuilder()
  let inStream = new StringReader("")
  let outStream = new StringWriter(sbOut)
  let errStream = new StringWriter(sbErr)
  // Build command line arguments & start FSI session
  let argv = [| "C:\\fsi.exe" |]
  let allArgs = Array.append argv [|"--noninteractive"|]

  let fsiOptions = FsiEvaluationSession.GetDefaultConfiguration() // defaultArg (Option.map  FsiOptions.ofArgs options) FsiOptions.Default
  let fsiSession = FsiEvaluationSession.Create(fsiOptions, allArgs, inStream, outStream, errStream)
  let evalFailed = new Event<_>()
  let lockObj = obj()

  /// Registered transformations for pretty printing values
  /// (the default formats value as a string and emits single CodeBlock)
  let mutable valueTransformations =
    [ (fun (o:obj, t:Type) ->Some([CodeBlock (sprintf "%A" o, "", "", None)]) ) ]

  /// Register a function that formats (some) values that are produced by the evaluator.
  /// The specified function should return 'Some' when it knows how to format a value
  /// and it should return formatted
  member x.RegisterTransformation(f) =
    valueTransformations <- f::valueTransformations

  /// This event is fired whenever an evaluation of an expression fails
  member x.EvaluationFailed = evalFailed.Publish

  member x.ChangeCurrentDirectory dir =
    let oldDir = System.IO.Directory.GetCurrentDirectory()
    let cd dir =
        fsiSession.EvalInteraction (sprintf "System.IO.Directory.SetCurrentDirectory(@\"%s\")" dir)
        fsiSession.EvalInteraction (sprintf "#cd @\"%s\"" dir)
    cd dir
    let isDisposed = ref false
    { new System.IDisposable with
        member __.Dispose() =
            if not !isDisposed then
            cd oldDir
            isDisposed := true }

   member __.TryEvalExpressionWithOutput text =
      match fsiSession.EvalExpression text with
      | Some fsiValue ->0,  Some (fsiValue.ReflectionValue, fsiValue.ReflectionType)
      | None -> 1, None

  /// Same as ChangeCurrentDirectory but takes a function for the scope.
  member x.WithCurrentDirectory dir f =
     use __ = x.ChangeCurrentDirectory dir
     f ()

  interface IFsiEvaluator with
    /// Format a specified result or value
    member x.Format(result, kind) =
      if not (result :? FsiEvaluationResult) then
        invalidArg "result" "FsiEvaluator.Format: Expected 'FsiEvaluationResult' value as argument."
      match result :?> FsiEvaluationResult, kind with
      | result, FsiEmbedKind.Output ->
          let s = defaultArg result.Output "No output has been produced."
          [ CodeBlock(s.Trim(), "", "", None) ]
      | { ItValue = Some v }, FsiEmbedKind.ItValue
      | { Result = Some v }, FsiEmbedKind.Value ->
          valueTransformations |> Seq.pick (fun f -> lock lockObj (fun () -> f v))
      | _, FsiEmbedKind.ItValue -> [ CodeBlock ("No value has been returned", "", "", None) ]
      | _, FsiEmbedKind.Value -> [ CodeBlock ("No value has been returned", "", "", None) ]




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
    member x.Evaluate(text:string, asExpression, ?file) =
     try
       lock lockObj <| fun () ->
         let dir =
           match file with
           | Some f -> Path.GetDirectoryName f
           | None -> Directory.GetCurrentDirectory()
         x.WithCurrentDirectory dir (fun () ->
            let (output, value), itvalue =
                if asExpression then
                   match fsiSession.EvalExpressionNonThrowing text with
                   | Choice1Of2 (Some fsiValue), err ->
                        match fsiSession.EvalExpression "it" with
                        | Some itValue ->
                           (fsiValue.ToString(),  Some(fsiValue.ReflectionValue,fsiValue.ReflectionType)),Some(itValue.ReflectionValue,itValue.ReflectionType)
                        | None  ->
                            (fsiValue.ToString(),  Some(fsiValue.ReflectionValue,fsiValue.ReflectionType)), None
                   | Choice1Of2 None, err ->
                        let msg = String.Concat err
                        (msg ,None),None
                   | Choice2Of2 ex,  err ->
                        let msg = String.concat "\n" [ String.Concat err;  ex.Message; ex.StackTrace ]
                        (msg , None), None
                else
                   match fsiSession.EvalExpression text with
                   | Some fsiValue ->
                        // try get the "it" value, but silently ignore any errors
                       try
                          match fsiSession.EvalExpression "it" with
                          | Some itValue ->
                             (fsiValue.ToString(),  Some(fsiValue.ReflectionValue,fsiValue.ReflectionType)),Some(itValue.ReflectionValue,itValue.ReflectionType)
                          | None  ->
                             (fsiValue.ToString(),  Some(fsiValue.ReflectionValue,fsiValue.ReflectionType)), None
                       with _ -> (fsiValue.ToString(), None), None
                   | None -> ("---", None), None
            { Output = Some output; Result = value; ItValue = itvalue  } :> _
         )
     with :? FsiEvaluationException as e ->
       //evalFailed.Trigger { File=file; AsExpression=asExpression; Text=text; Exception=e; StdErr = e.Result.Error.Merged }
        { Output = None; Result = None; ItValue = None } :> _
