namespace FSharp.Formatting.Common

open System.Diagnostics

module internal Log =
    let source = new System.Diagnostics.TraceSource "FSharp.Formatting"

    let ConsoleListener () =
        { new TraceListener() with
            override __.WriteLine(s: string) = System.Console.WriteLine(s)
            override __.Write(s: string) = System.Console.Write(s) }

    let TextListener (file: string) = new TextWriterTraceListener(file)

    let SetupSource (listeners: _ array) (source: TraceSource) =
        source.Listeners.Clear()
        source.Switch.Level <- System.Diagnostics.SourceLevels.All
        source.Listeners.AddRange listeners

    let SetupListener traceOptions levels (listener: TraceListener) =
        listener.Filter <- new EventTypeFilter(levels)
        listener.TraceOutputOptions <- traceOptions
        listener

    let AddListener listener (source: TraceSource) = source.Listeners.Add listener |> ignore

    let traceEventf t f =
        Printf.kprintf (fun s -> source.TraceEvent(t, 0, s)) f

    let infof f =
        traceEventf TraceEventType.Information f

    let errorf f = traceEventf TraceEventType.Error f
    let warnf f = traceEventf TraceEventType.Warning f
    let critf f = traceEventf TraceEventType.Critical f
    let verbf f = traceEventf TraceEventType.Verbose f
