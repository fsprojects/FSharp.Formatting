#if INTERACTIVE
#r "../../bin/FSharp.MetadataFormat.dll"
#endif

open System.IO
open FSharp.MetadataFormat

let (++) a b = Path.Combine(a, b)
let root = @"C:/dev/FSharp.DataFrame/tools"
let project  = root ++ "../"
let output   = root ++ "../docs"

let buildReference () = 
  // Build the API reference documentation
  if not (Directory.Exists(output ++ "reference")) then  
    Directory.CreateDirectory(output ++ "reference") |> ignore
  MetadataFormat.Generate
    ( project ++ "bin" ++ "FSharp.DataFrame.dll", 
      output ++ "reference", project ++ "tools" ++ "reference" )

do buildReference()