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

// Setup Logging for FSharp.Formatting
let svclogFile = "FSharp.Formatting.svclog"
let logFile = "FSharp.Formatting.log"
[ svclogFile; logFile ] |> Seq.iter (fun f -> if System.IO.File.Exists f then System.IO.File.Delete f)

System.Diagnostics.Trace.AutoFlush <- true
FSharp.Formatting.Common.Log.source.Listeners.Clear()
FSharp.Formatting.Common.Log.source.Switch.Level <- System.Diagnostics.SourceLevels.All
FSharp.Formatting.Common.Log.LogConsole System.Diagnostics.SourceLevels.Information
  |> ignore
FSharp.Formatting.Common.Log.LogSvclog System.Diagnostics.SourceLevels.All svclogFile
  |> ignore
FSharp.Formatting.Common.Log.LogText System.Diagnostics.SourceLevels.Warning logFile
  |> ignore
FSharp.Formatting.Common.Log.infof "FSharp.Formatting Logging setup!"