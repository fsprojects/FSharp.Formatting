open System.IO
open System.Reflection
open FSharp.Literate

[<EntryPoint>]
let main argv = 
  let asmCompiler = Assembly.LoadFile(@"C:\Program Files (x86)\Microsoft F#\v4.0\FSharp.Compiler.dll")
  let loc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
  let source = Path.Combine(loc, "test.md")
  let template = Path.Combine(loc, "template-file.html")
  Literate.ProcessMarkdown(source, template, fsharpCompiler=asmCompiler)
  0
