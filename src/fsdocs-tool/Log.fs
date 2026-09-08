namespace fsdocs

open System
open System.Collections.Concurrent
open Microsoft.Extensions.Logging
open FSharp.Formatting.Common

/// The logger of the fsdocs tool.
[<AutoOpen>]
module internal Log =
    let logger = CategoryLogger "fsdocs"

/// A line kept by the log buffer.
type LogLine =
    {
        Time: DateTime
        Level: string
        Category: string
        Message: string
    }

/// The last few hundred log lines, for the doctor.
type internal LogBuffer() =
    let lines = ConcurrentQueue<LogLine>()

    member _.Add(level: LogLevel, category: string, message: string, ex: exn) =
        lines.Enqueue
            {
                Time = DateTime.Now
                Level = string<LogLevel> level
                Category = category
                Message =
                    if isNull ex then
                        message
                    else
                        message + Environment.NewLine + string<exn> ex
            }

        while lines.Count > 300 do
            lines.TryDequeue() |> ignore

    member _.Lines = lines |> Seq.toList

/// The '--verbosity' option: which log levels reach the console.
module internal Verbosity =

    /// The log lines of this process, oldest first.
    let buffer = LogBuffer()

    let names = [ "quiet"; "minimal"; "normal"; "detailed"; "diagnostic" ]

    /// The minimum level and whether to show timestamps, for a verbosity name (or its first letters as dotnet accepts them).
    let parse (verbosity: string) : (LogLevel * bool) option =
        match verbosity.Trim().ToLowerInvariant() with
        | "q"
        | "quiet" -> Some(LogLevel.Error, false)
        | "m"
        | "minimal" -> Some(LogLevel.Warning, false)
        | "n"
        | "normal" -> Some(LogLevel.Information, false)
        | "d"
        | "detailed" -> Some(LogLevel.Debug, false)
        | "diag"
        | "diagnostic" -> Some(LogLevel.Trace, true)
        | _ -> None

    /// Route FSharp.Formatting and fsdocs logging to the console at the given verbosity, and keep the
    /// last lines for the doctor. Returns false (after an error message) when the name is unknown.
    let configure (verbosity: string) =
        match parse verbosity with
        | None ->
            eprintfn "error: unknown verbosity '%s', expected one of %s" verbosity (String.concat ", " names)
            false
        | Some(level, timestamps) ->
            let console = Logging.ConsoleSink(timestamps = timestamps)

            Logging.UseSink(
                level,
                fun level category message ex ->
                    buffer.Add(level, category, message, ex)
                    console level category message ex
            )

            true
