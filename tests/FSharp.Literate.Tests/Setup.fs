module FSharp.Literate.Tests.Setup

open System
open System.IO
open System.Reflection

// --------------------------------------------------------------------------------------
// Setup - find the compiler assembly etc.
// --------------------------------------------------------------------------------------

let (@@) a b = Path.Combine(a, b)

type TempFile() =
  let file = Path.GetTempFileName()
  member x.File = file
  member x.Content = File.ReadAllText(file)
  interface IDisposable with
    member x.Dispose() = File.Delete(file)

let formatAgent = FSharp.CodeFormat.CodeFormat.CreateAgent()

let fsiEvaluator = FSharp.Literate.Evaluation.FsiEvaluator()
