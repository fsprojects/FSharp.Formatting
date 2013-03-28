// --------------------------------------------------------------------------------------
// TODO: Some actual unit tests would be nice
// --------------------------------------------------------------------------------------

open System.IO
open System.Diagnostics
open System.Reflection
open FSharp.CodeFormat

// Load custom-built F# compiler with joinads
let asmCompiler = Assembly.LoadFile(@"C:\Program Files (x86)\Microsoft F#\v4.0\FSharp.Compiler.dll")
//let asmCompiler = Assembly.LoadFile(__SOURCE_DIRECTORY__ + @"\\bin\\Debug\\FSharp.Compiler.dll")

let agent = CodeFormat.CreateAgent(asmCompiler)

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
