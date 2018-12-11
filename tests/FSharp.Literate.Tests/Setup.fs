module FSharp.Literate.Tests.Setup

open System
open System.IO

// --------------------------------------------------------------------------------------
// Setup - find the compiler assembly etc.
// --------------------------------------------------------------------------------------

let (</>) a b = Path.Combine(a, b)

type TempFile() =
  let file = Path.GetTempFileName()
  member __.File = file
  member __.Content = File.ReadAllText(file)
  interface IDisposable with
    member __.Dispose() = File.Delete(file)

let getFormatAgent() = FSharp.CodeFormat.CodeFormat.CreateAgent()

let getFsiEvaluator() = FSharp.Literate.FsiEvaluator()
