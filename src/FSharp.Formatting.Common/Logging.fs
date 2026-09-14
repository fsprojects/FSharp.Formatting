namespace FSharp.Formatting.Common

open System
open System.Text.RegularExpressions
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions

/// Receives a log message: level, category, message and the exception (null when there is none).
type LogSink = LogLevel -> string -> string -> exn -> unit

/// An <see cref="T:Microsoft.Extensions.Logging.ILogger"/> that forwards formatted messages to a sink.
type internal SinkLogger(category: string, minimumLevel: LogLevel, sink: LogSink) =
    interface ILogger with
        member _.BeginScope(_state) =
            { new IDisposable with
                member _.Dispose() = ()
            }

        member _.IsEnabled(level) =
            level <> LogLevel.None && level >= minimumLevel

        member this.Log(level, _eventId, state, ex, formatter) =
            if (this :> ILogger).IsEnabled level then
                sink level category (formatter.Invoke(state, ex)) ex

/// A minimal <see cref="T:Microsoft.Extensions.Logging.ILoggerFactory"/> for hosts that only need a sink:
/// the console, a file or a buffer. Providers are not supported.
type SinkLoggerFactory(minimumLevel: LogLevel, sink: LogSink) =
    interface ILoggerFactory with
        member _.CreateLogger(category) =
            SinkLogger(category, minimumLevel, sink) :> ILogger

        member _.AddProvider(_provider) = ()
        member _.Dispose() = ()

/// Configures where FSharp.Formatting logs. The libraries log through
/// <see cref="T:Microsoft.Extensions.Logging.ILogger"/> and are silent by default: set
/// <see cref="P:FSharp.Formatting.Common.Logging.LoggerFactory"/> to your own factory, or call
/// <see cref="M:FSharp.Formatting.Common.Logging.UseConsole"/>.
[<AbstractClass; Sealed>]
type Logging private () =
    static let mutable factory: ILoggerFactory = NullLoggerFactory.Instance
    static let mutable version = 0

    /// Messages already in the canonical 'file(line,col): warning: text' form get no extra prefix.
    static let canonical = Regex(@"^\S.*\(\d+,\d+\)(-\(\d+,\d+\))?: (warning|error)\b", RegexOptions.Compiled)

    /// The factory used for all FSharp.Formatting loggers. Defaults to <c>NullLoggerFactory</c>.
    static member LoggerFactory
        with get () = factory
        and set (value: ILoggerFactory) =
            factory <- value
            version <- version + 1

    static member internal Version = version

    /// A sink writing information and below to standard output and warnings and errors to
    /// standard error, with a 'warning: ' or 'error: ' prefix and an optional timestamp.
    static member ConsoleSink(?timestamps: bool) : LogSink =
        let timestamps = defaultArg timestamps false
        let gate = obj ()

        fun level _category message ex ->
            let prefix =
                match level with
                | LogLevel.Warning when not (canonical.IsMatch message) -> "warning: "
                | LogLevel.Error
                | LogLevel.Critical when not (canonical.IsMatch message) -> "error: "
                | _ -> ""

            let stamp =
                if timestamps then
                    DateTime.Now.ToString("HH:mm:ss.fff ")
                else
                    ""

            let text =
                if isNull ex then
                    stamp + prefix + message
                else
                    stamp + prefix + message + Environment.NewLine + string<exn> ex

            lock gate (fun () ->
                let writer =
                    if level >= LogLevel.Warning then
                        Console.Error
                    else
                        Console.Out

                writer.WriteLine text)

    /// Log to the console: information and below on standard output, warnings and errors on
    /// standard error.
    static member UseConsole(minimumLevel: LogLevel, ?timestamps: bool) =
        Logging.LoggerFactory <- new SinkLoggerFactory(minimumLevel, Logging.ConsoleSink(?timestamps = timestamps))

    /// Log to the given sink.
    static member UseSink(minimumLevel: LogLevel, sink: LogSink) =
        Logging.LoggerFactory <- new SinkLoggerFactory(minimumLevel, sink)

/// A logger for one category, resolved against the current <see cref="P:FSharp.Formatting.Common.Logging.LoggerFactory"/>
/// so that a factory set after startup is honoured. Message formatting only happens when the level is enabled.
type internal CategoryLogger(category: string) =
    let mutable cached = (-1, NullLogger.Instance :> ILogger)
    let format = Func<string, exn, string>(fun message _ -> message)

    member _.Logger: ILogger =
        let version, logger = cached

        if version = Logging.Version then
            logger
        else
            let logger = Logging.LoggerFactory.CreateLogger category
            cached <- (Logging.Version, logger)
            logger

    member x.IsEnabled(level: LogLevel) = x.Logger.IsEnabled level

    member x.Log(level: LogLevel, message: string) =
        x.Logger.Log(level, EventId(0), message, null, format)

    member x.Log(level: LogLevel, message: string, ex: exn) =
        x.Logger.Log(level, EventId(0), message, ex, format)

    member x.Logf(level: LogLevel, fmt: Printf.StringFormat<'T, unit>) : 'T =
        if x.IsEnabled level then
            Printf.kprintf (fun s -> x.Log(level, s)) fmt
        else
            Printf.kprintf ignore fmt

    member x.Tracef(fmt: Printf.StringFormat<'T, unit>) : 'T = x.Logf(LogLevel.Trace, fmt)
    member x.Debugf(fmt: Printf.StringFormat<'T, unit>) : 'T = x.Logf(LogLevel.Debug, fmt)
    member x.Infof(fmt: Printf.StringFormat<'T, unit>) : 'T = x.Logf(LogLevel.Information, fmt)
    member x.Warnf(fmt: Printf.StringFormat<'T, unit>) : 'T = x.Logf(LogLevel.Warning, fmt)
    member x.Errorf(fmt: Printf.StringFormat<'T, unit>) : 'T = x.Logf(LogLevel.Error, fmt)

/// The logger of the FSharp.Formatting.Common assembly.
[<AutoOpen>]
module internal CommonLogger =
    let logger = CategoryLogger "FSharp.Formatting.Common"
