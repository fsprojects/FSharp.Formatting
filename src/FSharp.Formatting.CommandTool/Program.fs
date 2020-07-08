
module FSharp.Formatting.CommandTool.Main

open CommandLine

let printAssemblies msg =
  printfn "%s. Loaded Assemblies:" msg
  System.AppDomain.CurrentDomain.GetAssemblies()
    |> Seq.choose (fun a -> try Some (a.GetName().FullName, a.Location) with _ -> None)
    |> Seq.iter (fun (n, l) -> printfn "\t- %s: %s" n l)


[<EntryPoint>]
let main argv =
    try
      CommandLine.Parser.Default.ParseArguments(argv, typeof<ConvertCommand>, typeof<ApiDocsCommand>, typeof<BuildCommand>)
        .MapResult(
            (fun (opts: ConvertCommand) -> opts.Execute()),
            (fun (opts: ApiDocsCommand) -> opts.Execute()),
            (fun (opts: BuildCommand) -> opts.Execute()),
            (fun errs -> 1));
    with e ->
        printAssemblies "(DIAGNOSTICS) Documentation failed"
        printfn "fsformatting.exe failed: %O" e
        reraise()