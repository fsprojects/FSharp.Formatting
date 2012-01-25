// --------------------------------------------------------------------------------------
// TODO: Some actual unit tests would be nice
// --------------------------------------------------------------------------------------

#r @"..\FSharp.CodeFormat\bin\Debug\FSharp.CodeFormat.dll"
open FSharp.CodeFormat
open System.Reflection

// Load custom-built F# compiler with joinads
let asm = Assembly.LoadFile(@"C:\tomas\Binary\FSharp.Extensions\Debug\cli\4.0\bin\FSharp.Compiler.dll")

let agent = CodeFormat.CreateAgent(asm)

let source = @"
  let foo = 10
  foo
"

let snips = 
  agent.AsyncParseSource("C:\\test.fsx", source.Trim())
  |> Async.RunSynchronously

let res = CodeFormat.FormatHtml(snips, "fstips")
res.SnippetsHtml.[0].Html

// Assembly.Load("FSharp.Compiler, Version=2.0.0.0, Culture=neutral, PublicKeyToken=a19089b1c74d0809")
// Assembly.LoadFile(@"C:\Program Files (x86)\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll")
