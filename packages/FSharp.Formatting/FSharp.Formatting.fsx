#nowarn "211"
// Standard NuGet or Paket location
#I "."
#I "lib/net40"
#r "./lib/net40/System.Web.Razor.dll"
// Force load
if (typeof<System.Web.Razor.ParserResults>.Assembly.GetName().Version.Major <= 2) then
  failwith "Wrong System.Web.Razor Version loaded!"


// Standard NuGet locations
#I "../FSharp.Compiler.Service.0.0.87/lib/net45"
#I "../FSharpVSPowerTools.Core.1.8.0/lib/net45"

// Standard Paket locations
#I "../FSharp.Compiler.Service/lib/net45"
#I "../FSharpVSPowerTools.Core/lib/net45"


// Reference VS PowerTools, Razor and F# Formatting components
#r "RazorEngine.dll"
#r "FSharpVSPowerTools.Core.dll"
#r "FSharp.Formatting.Common.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"

// Setup Logging for FSharp.Formatting and Yaaf.FSharp.Scripting
let svclogFile = "FSharp.Formatting.svclog"
let logFile = "FSharp.Formatting.log"
[ svclogFile; logFile ] |> Seq.iter (fun f -> if System.IO.File.Exists f then System.IO.File.Delete f)

module Logging = FSharp.Formatting.Common.Log
type TraceOptions = System.Diagnostics.TraceOptions
System.Diagnostics.Trace.AutoFlush <- true
let allTraceOptions = TraceOptions.Callstack ||| TraceOptions.DateTime ||| TraceOptions.LogicalOperationStack |||
                      TraceOptions.ProcessId ||| TraceOptions.ThreadId ||| TraceOptions.Timestamp
let noTraceOptions = TraceOptions.None
let listeners =
  [|Logging.SvclogListener svclogFile
    |> Logging.SetupListener allTraceOptions System.Diagnostics.SourceLevels.All
    Logging.ConsoleListener()
    |> Logging.SetupListener noTraceOptions System.Diagnostics.SourceLevels.Information |]
let sources =
  [ FSharp.Formatting.Common.Log.source
    Yaaf.FSharp.Scripting.Log.source ]

sources |> Seq.iter (Logging.SetupSource listeners)

// Test that everything works
Logging.infof "FSharp.Formatting Logging setup!"
Yaaf.FSharp.Scripting.Log.infof "Yaaf.FSharp.Scripting Logging setup!"