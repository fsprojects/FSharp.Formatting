module fsdocs.Main

open CommandLine

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FSharp.Literate.Tests")>]
do ()

/// Entry point: dispatches to build, watch, or init subcommands based on parsed arguments
[<EntryPoint>]
let main argv =
    CommandLine.Parser.Default
        .ParseArguments<ConvertCommand, BuildCommand, WatchCommand, InitCommand>(argv)
        .MapResult(
            (fun (opts: ConvertCommand) -> opts.Execute()),
            (fun (opts: BuildCommand) -> opts.Execute()),
            (fun (opts: WatchCommand) -> opts.Execute()),
            (fun (opts: InitCommand) -> opts.Execute()),
            (fun _ -> 1)
        )
