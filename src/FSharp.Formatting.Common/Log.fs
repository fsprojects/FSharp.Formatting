namespace FSharp.Formatting.Common

open System.Diagnostics

module Log =
  let source = new System.Diagnostics.TraceSource("FSharp.Formatting")

  let AddListener listener =
    source.Listeners.Add(listener) |> ignore
    listener

  let LogConsole levels =
    let consoleListener = new ConsoleTraceListener()
    consoleListener.Filter <- new EventTypeFilter(levels)
    AddListener consoleListener

  let LogSvclog levels (file:string)  =
    let svclogListener = new XmlWriterTraceListener(file)
    let traceOptions = TraceOptions.Callstack ||| TraceOptions.DateTime ||| TraceOptions.LogicalOperationStack ||| 
                       TraceOptions.ProcessId ||| TraceOptions.ThreadId ||| TraceOptions.Timestamp
    svclogListener.TraceOutputOptions <- traceOptions
    svclogListener.Filter <- new EventTypeFilter(levels)
    AddListener svclogListener

  let LogText levels (file:string) =
    let textListener = new TextWriterTraceListener(file)
    textListener.TraceOutputOptions <- TraceOptions.DateTime
    textListener.Filter <- new EventTypeFilter(levels)
    AddListener textListener
    
  let traceEventf t f =
    Printf.kprintf (fun s -> source.TraceEvent(t, 0, s)) f
  let infof f = traceEventf TraceEventType.Information f
  let errorf f = traceEventf TraceEventType.Error f
  let warnf f = traceEventf TraceEventType.Warning f
  let critf f = traceEventf TraceEventType.Critical f
  let verbf f = traceEventf TraceEventType.Verbose f