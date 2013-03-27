open System.IO
open System.Reflection
open FSharp.Literate

[<EntryPoint>]
let main argv = 

  let files = 
    [ @"C:\Program Files (x86)\Microsoft SDKs\F#\3.0\Framework\v4.0\FSharp.Compiler.dll"
      @"C:\Program Files (x86)\Microsoft F#\v4.0\FSharp.Compiler.dll" ]

  let asmCompiler = 
    files |> Seq.pick (fun file ->
      if File.Exists(file) then Some(Assembly.LoadFile(file))
      else None)

  let loc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
  let template = Path.Combine(loc, "template-file.html")
  
  let source = Path.Combine(loc, "test.md")
  Literate.ProcessMarkdown(source, template, fsharpCompiler=asmCompiler)

  let source = Path.Combine(loc, "test.fsx")
  Literate.ProcessScriptFile(source, template, fsharpCompiler=asmCompiler)
  0
