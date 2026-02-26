namespace FSharp.Formatting.Common

open System.Diagnostics

/// Internal logging helpers built on top of System.Diagnostics.TraceSource.
module internal Log =
    /// The shared TraceSource used throughout FSharp.Formatting.
    let source = new System.Diagnostics.TraceSource "FSharp.Formatting"

    /// Creates a TraceListener that writes to the console.
    let ConsoleListener () =
        { new TraceListener() with
            override __.WriteLine(s: string) = System.Console.WriteLine(s)
            override __.Write(s: string) = System.Console.Write(s) }

    /// Creates a TraceListener that writes to the specified file.
    let TextListener (file: string) = new TextWriterTraceListener(file)

    /// Clears existing listeners on a TraceSource and replaces them with the given array.
    let SetupSource (listeners: _ array) (source: TraceSource) =
        source.Listeners.Clear()
        source.Switch.Level <- System.Diagnostics.SourceLevels.All
        source.Listeners.AddRange listeners

    /// Configures a TraceListener with the given output options and event-type filter level.
    let SetupListener traceOptions levels (listener: TraceListener) =
        listener.Filter <- new EventTypeFilter(levels)
        listener.TraceOutputOptions <- traceOptions
        listener

    /// Adds a listener to a TraceSource.
    let AddListener listener (source: TraceSource) = source.Listeners.Add listener |> ignore

    /// Emits a trace event of the given type using a printf-style format string.
    let traceEventf t f =
        Printf.kprintf (fun s -> source.TraceEvent(t, 0, s)) f

    /// Logs an informational message.
    let infof f =
        traceEventf TraceEventType.Information f

    /// Logs an error message.
    let errorf f = traceEventf TraceEventType.Error f
    /// Logs a warning message.
    let warnf f = traceEventf TraceEventType.Warning f
    /// Logs a critical message.
    let critf f = traceEventf TraceEventType.Critical f
    /// Logs a verbose/diagnostic message.
    let verbf f = traceEventf TraceEventType.Verbose f
