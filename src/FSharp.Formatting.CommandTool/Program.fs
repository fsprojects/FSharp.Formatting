module FSharp.Formatting.CommandTool.Main

open CommandLine

[<EntryPoint>]
let main argv =
    CommandLine.Parser.Default.ParseArguments<BuildCommand, WatchCommand>(argv)
        .MapResult(
            (fun (opts: BuildCommand) -> opts.Execute()),
            (fun (opts: WatchCommand) -> opts.Execute()),
            (fun _ -> 1))
