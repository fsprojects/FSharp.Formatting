open FSharp.Formatting.Options.Literate
open FSharp.Formatting.Options.MetadataFormat
open CommandLine


let printAssemblies msg =
  printfn "%s. Loaded Assemblies:" msg
  System.AppDomain.CurrentDomain.GetAssemblies()
    |> Seq.choose (fun a -> try Some (a.GetName().FullName, a.Location) with _ -> None)
    |> Seq.iter (fun (n, l) -> printfn "\t- %s: %s" n l)


[<EntryPoint>]
let main argv =
    try
      CommandLine.Parser.Default.ParseArguments(argv, typeof<ProcessDirectoryOptions>, typeof<GenerateOptions>)
        .MapResult(
            (fun (opts: ProcessDirectoryOptions ) -> opts.Execute()),
            (fun (opts: GenerateOptions) -> opts.Execute()),
            (fun errs -> 1));
    with e ->
        printAssemblies "(DIAGNOSTICS) Documentation failed"
        printfn "fsformatting.exe failed: %O" e
        reraise()