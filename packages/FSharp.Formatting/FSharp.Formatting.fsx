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


// Try various folders that people might like
#I "bin"
#I "../bin"
#I "../../bin"
#I "lib"

// Reference VS PowerTools, Razor and F# Formatting components
#r "RazorEngine.dll"
#r "FSharpVSPowerTools.Core.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
