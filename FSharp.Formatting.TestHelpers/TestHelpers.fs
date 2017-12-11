module FSharp.Formatting.TestHelpers

open System.Diagnostics
open FSharp.Formatting
module Logging = FSharp.Formatting.Common.Log

// This controlls logging within the test suite ...
let enableLogging() =
    let logToConsole = true
    let logToFile = false
    try
      let allTraceOptions =
        TraceOptions.Callstack ||| TraceOptions.DateTime ||| TraceOptions.LogicalOperationStack |||
        TraceOptions.ProcessId ||| TraceOptions.ThreadId ||| TraceOptions.Timestamp
      let noTraceOptions = TraceOptions.None
      let svclogFile = "FSharp.Formatting.svclog"
      System.Diagnostics.Trace.AutoFlush <- true

      let setupListener listener =
        [ FSharp.Formatting.Common.Log.source
          Yaaf.FSharp.Scripting.Log.source ]
        |> Seq.iter (fun source ->
            source.Switch.Level <- System.Diagnostics.SourceLevels.All
            Logging.AddListener listener source)

      if logToConsole then
        Logging.ConsoleListener()
        |> Logging.SetupListener noTraceOptions System.Diagnostics.SourceLevels.Verbose
        |> setupListener

    //   if logToFile then
    //     if System.IO.File.Exists svclogFile then System.IO.File.Delete svclogFile
    //     Logging.SvclogListener svclogFile
    //     |> Logging.SetupListener allTraceOptions System.Diagnostics.SourceLevels.All
    //     |> setupListener

      // Test that everything works
      Logging.infof "FSharp.Formatting Logging setup!"
      Yaaf.FSharp.Scripting.Log.infof "Yaaf.FSharp.Scripting Logging setup!"
    with e ->
      printfn "FSharp.Formatting Logging setup failed: %A" e
