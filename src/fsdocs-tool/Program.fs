module fsdocs.Main

open CommandLine

[<assembly: System.Runtime.CompilerServices.InternalsVisibleTo("FSharp.Literate.Tests")>]
do ()

[<EntryPoint>]
let main argv =
    CommandLine.Parser.Default
        .ParseArguments<BuildCommand, WatchCommand>(argv)
        .MapResult(
            (fun (opts: BuildCommand) -> opts.Execute()),
            (fun (opts: WatchCommand) -> opts.Execute()),
            (fun _ -> 1)
        )
