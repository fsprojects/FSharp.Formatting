#if INTERACTIVE
#r "../../bin/FSharp.CodeFormat.dll"
#r "../../packages/NUnit.2.6.2/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.CodeFormat.Tests
#endif

open System
open System.IO
open System.Reflection
open FsUnit
open NUnit.Framework
open FSharp.CodeFormat

// --------------------------------------------------------------------------------------
// Initialization - find F# compiler dll, setup formatting agent
// --------------------------------------------------------------------------------------

// Lookup compiler DLL
let locations = 
  [ "%ProgramFiles%\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\FSharp.Compiler.dll"
    "%ProgramFiles(x86)%\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\FSharp.Compiler.dll" ]
let compiler = 
  locations |> Seq.pick (fun location ->
    try 
      let location = Environment.ExpandEnvironmentVariables(location)
      if not (File.Exists(location)) then None else
        Some(Assembly.LoadFile(Environment.ExpandEnvironmentVariables(location)))
    with _ -> None)

let agent = CodeFormat.CreateAgent(compiler)

// --------------------------------------------------------------------------------------

[<Test>]


let tests = Path.Combine(__SOURCE_DIRECTORY__, "tests")
let doWork () =
  for file in Directory.GetFiles(tests, "a.fs") do
    printfn " - processing %s" file
    let source = File.ReadAllText(file)
    let snips, errors = agent.ParseSource(file, source.Trim())

    let res = CodeFormat.FormatHtml(snips, "fstips")
    use wr = new StreamWriter(System.Console.OpenStandardOutput()) // file + ".output")
    for snip in res.Snippets do
      wr.WriteLine(snip.Title)
      wr.WriteLine(snip.Content + "\n")
    wr.WriteLine("\n\n<!-- GENERATED TOOL TIPS -->")
    wr.Write(res.ToolTip)

doWork ()
let sw = Stopwatch()
sw.Start()
doWork ()
sw.Stop()
printfn "Processing took: %dms" sw.ElapsedMilliseconds
// Assembly.Load("FSharp.Compiler, Version=2.0.0.0, Culture=neutral, PublicKeyToken=a19089b1c74d0809")
// Assembly.LoadFile(@"C:\Program Files (x86)\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll")
