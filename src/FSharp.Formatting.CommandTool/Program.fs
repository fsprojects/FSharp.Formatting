open FSharp.Formatting.Exec


let printAssemblies msg =
  printfn "%s. Loaded Assemblies:" msg
  System.AppDomain.CurrentDomain.GetAssemblies()
    |> Seq.choose (fun a -> try Some (a.GetName().FullName, a.Location) with _ -> None)
    |> Seq.iter (fun (n, l) -> printfn "\t- %s: %s" n l)


[<EntryPoint>]
let main argv =
    try
        Env(argv).Run()
    with e ->
        printAssemblies "(DIAGNOSTICS) Documentation failed"
        printfn "fsformatting.exe failed: %O" e
        reraise()