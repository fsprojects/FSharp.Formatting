module FSharp.Markdown.Tests.TestFiles
// --------------------------------------------------------------------------------------
// TODO: This is all obsolete
// --------------------------------------------------------------------------------------

open FSharp.Markdown
open System.IO
open System.Diagnostics

let (++) a b = Path.Combine(a, b)
let testdir = __SOURCE_DIRECTORY__ ++ Path.Combine("..","..","tests")

// --------------------------------------------------------------------------------------
// Run performance benchmarks
// --------------------------------------------------------------------------------------

let benchmark file count =
  let text = File.ReadAllText(testdir ++ "benchmark" ++ file)

  let sw = new Stopwatch()
  sw.Start()
  for n in 1 .. count do 
    Markdown.TransformHtml(text) |> ignore 
  sw.Stop()

  printfn "input string length: %d" text.Length
  printfn "%d iteration(s) in %d ms" count sw.ElapsedMilliseconds
  if count = 1 then printfn ""
  else printfn " (%f ms per iteration)\n" (float sw.ElapsedMilliseconds / float count)

let benchmarks () =
  benchmark "markdown-example-short-1.text" 4000
  benchmark "markdown-example-medium-1.text" 1000
  benchmark "markdown-example-long-2.text" 100
  benchmark "markdown-readme.text" 1
  benchmark "markdown-readme.8.text" 1
  benchmark "markdown-readme.32.text" 1

// --------------------------------------------------------------------------------------
// Run tests and verify
// --------------------------------------------------------------------------------------

let transformAndCompare transFunc (source:string) (target:string) (verify:string) = 
  try
    if File.Exists(verify) then
      let text = File.ReadAllText(source)
      ( use wr = new StreamWriter(target)
        transFunc(text, wr, "\r\n") )
    
      let contents = File.ReadAllLines(verify)
      File.WriteAllLines(verify, contents)

      let targetHtml = File.ReadAllText(target)
      let verifyHtml = File.ReadAllText(verify)
      if targetHtml = verifyHtml then File.Delete(target)
      else printfn " - %s" (target.Substring(testdir.Length))
  with e ->
    printfn " - %s (failed)\n %A" (target.Substring(testdir.Length)) e

let rec runTests dir = 
  for file in Directory.GetFiles(dir, "*.text") do
    transformAndCompare Markdown.TransformHtml file (file.Replace(".text", ".2.html")) (file.Replace(".text", ".html"))
  for file in Directory.GetFiles(dir, "*.text") do
    transformAndCompare Markdown.TransformLatex file (file.Replace(".text", ".2.tex")) (file.Replace(".text", ".tex"))
  for dir in Directory.GetDirectories(dir) do
    runTests dir

// --------------------------------------------------------------------------------------

let runAll () =
  runTests (testdir ++ "testfiles")
  benchmarks ()
