/// Entry point module for the fsdocs command-line tool.
module fsdocs.Main

open CommandLine

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FSharp.Literate.Tests")>]
do ()

/// Parses command-line arguments and dispatches to the appropriate sub-command (build, watch, or init).
[<EntryPoint>]
let main argv =
    CommandLine.Parser.Default
        .ParseArguments<BuildCommand, WatchCommand, InitCommand>(argv)
        .MapResult(
            (fun (opts: BuildCommand) -> opts.Execute()),
            (fun (opts: WatchCommand) -> opts.Execute()),
            (fun (opts: InitCommand) -> opts.Execute()),
            (fun _ -> 1)
        )
