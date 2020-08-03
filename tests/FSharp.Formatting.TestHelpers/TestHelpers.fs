module FSharp.Formatting.TestHelpers

open System.Diagnostics
open FSharp.Formatting
module Logging = FSharp.Formatting.Common.Log

// This controlls logging within the test suite ...
let enableLogging() =
    let logToConsole = true
    try
      let noTraceOptions = TraceOptions.None
      System.Diagnostics.Trace.AutoFlush <- true

      let setupListener listener =
        [ FSharp.Formatting.Common.Log.source
          FSharp.Formatting.Internal.Log.source ]
        |> Seq.iter (fun source ->
            source.Switch.Level <- System.Diagnostics.SourceLevels.All
            Logging.AddListener listener source)

      if logToConsole then
        Logging.ConsoleListener()
        |> Logging.SetupListener noTraceOptions System.Diagnostics.SourceLevels.Verbose
        |> setupListener

      // Test that everything works
      Logging.infof "FSharp.Formatting Logging setup!"
      FSharp.Formatting.Internal.Log.infof "FSharp.Formatting.Internal Logging setup!"
    with e ->
      printfn "FSharp.Formatting Logging setup failed: %A" e
