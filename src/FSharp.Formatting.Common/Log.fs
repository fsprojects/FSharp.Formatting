namespace FSharp.Formatting.Common

open System.Diagnostics

/// Internal logging helpers backed by a <see cref="System.Diagnostics.TraceSource"/>
module internal Log =
    /// The shared TraceSource used by all FSharp.Formatting logging
    let source = new System.Diagnostics.TraceSource "FSharp.Formatting"

    /// Creates a TraceListener that writes to the console
    let ConsoleListener () =
        { new TraceListener() with
            override __.WriteLine(s: string) = System.Console.WriteLine(s)
            override __.Write(s: string) = System.Console.Write(s) }

    /// Creates a TraceListener that writes to a file
    let TextListener (file: string) = new TextWriterTraceListener(file)

    /// Clears existing listeners, sets the level to All, and attaches the given listeners to the source
    let SetupSource (listeners: _ array) (source: TraceSource) =
        source.Listeners.Clear()
        source.Switch.Level <- System.Diagnostics.SourceLevels.All
        source.Listeners.AddRange listeners

    /// Configures a listener with the given trace output options and event level filter
    let SetupListener traceOptions levels (listener: TraceListener) =
        listener.Filter <- new EventTypeFilter(levels)
        listener.TraceOutputOptions <- traceOptions
        listener

    /// Adds a listener to the given TraceSource
    let AddListener listener (source: TraceSource) = source.Listeners.Add listener |> ignore

    /// Emits a trace event of the given type using printf-style formatting
    let traceEventf t f =
        Printf.kprintf (fun s -> source.TraceEvent(t, 0, s)) f

    /// Logs an informational message
    let infof f =
        traceEventf TraceEventType.Information f

    /// Logs an error message
    let errorf f = traceEventf TraceEventType.Error f
    /// Logs a warning message
    let warnf f = traceEventf TraceEventType.Warning f
    /// Logs a critical message
    let critf f = traceEventf TraceEventType.Critical f
    /// Logs a verbose message
    let verbf f = traceEventf TraceEventType.Verbose f
