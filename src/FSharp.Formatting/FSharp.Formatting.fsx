#nowarn "211"

// FSharp.Formatting repo context: uncomment for intellisense
#I "bin/Release/netstandard2.0"

// Standard NuGet or Paket location
#I __SOURCE_DIRECTORY__
#I "lib/netstandard2.0"

// Standard Paket locations
#I "../FSharp.Compiler.Service/lib/netstandard2.0"

// Reference VS PowerTools, Razor and F# Formatting components
#r "RazorEngine.NetCore.dll"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"

// Ensure that FSharpVSPowerTools.Core.dll is loaded before trying to load FSharp.CodeFormat.dll
;;

#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
#r "FSharp.Formatting.Razor.dll"

// Setup Logging for FSharp.Formatting and Yaaf.FSharp.Scripting
module Logging = FSharp.Formatting.Common.Log
type TraceOptions = System.Diagnostics.TraceOptions

// By default, we log to console only. Other modes are enabled by setting
// the `FSHARP_FORMATTING_LOG` environment variable.
let logToFile, logToConsole =
  match System.Environment.GetEnvironmentVariable("FSHARP_FORMATTING_LOG") with
  | "ALL" -> true, true
  | "NONE" -> false, false
  | "FILE_ONLY" -> true, false
  | _ -> false, true

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

  // Test that everything works
  Logging.infof "FSharp.Formatting Logging setup!"
  Yaaf.FSharp.Scripting.Log.infof "Yaaf.FSharp.Scripting Logging setup!"
with e ->
  printfn "FSharp.Formatting Logging setup failed: %A" e
