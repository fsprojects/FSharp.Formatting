
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
      CommandLine.Parser.Default.ParseArguments(argv, typeof<ConvertCommand>, typeof<ApiCommand>, typeof<BuildCommand>, typeof<WatchCommand>)
        .MapResult(
            (fun (opts: ConvertCommand) -> opts.Execute()),
            (fun (opts: ApiCommand) -> opts.Execute()),
            (fun (opts: BuildCommand) -> opts.Execute()),
            (fun (opts: WatchCommand) -> opts.Execute()),
            (fun errs -> 1));
    with e ->
        let e =
            match e with
            | :? System.AggregateException as ex -> ex.InnerExceptions.[0]
            | _ -> e
        //printAssemblies "(DIAGNOSTICS) Documentation failed"
        printfn "fsdocs.exe failed: %O" e
        1
        //reraise()