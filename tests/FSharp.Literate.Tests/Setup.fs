module FSharp.Literate.Tests.Setup

open System
open System.IO
open System.Reflection

// --------------------------------------------------------------------------------------
// Setup - find the compiler assembly etc.
// --------------------------------------------------------------------------------------

let (@@) a b = Path.Combine(a, b)

let compilerAsembly =
  let files = 
    [ @"%ProgramFiles%\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll"
      @"%ProgramFiles(x86)%\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll"
      @"%ProgramFiles(x86)%\Microsoft F#\v4.0\FSharp.Compiler.dll" ]
  files |> Seq.pick (fun file ->
    let file = Environment.ExpandEnvironmentVariables(file)
    if File.Exists(file) then Some(Assembly.LoadFile(file))
    else None)

type TempFile() =
  let file = Path.GetTempFileName()
  member x.File = file
  member x.Content = File.ReadAllText(file)
  interface IDisposable with
    member x.Dispose() = File.Delete(file)

let formatAgent = FSharp.CodeFormat.CodeFormat.CreateAgent(compilerAsembly)
