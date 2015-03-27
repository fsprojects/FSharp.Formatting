#nowarn "211"
// Standard NuGet or Paket location
#I "."
#I "lib/net40"

// Standard NuGet locations
#I "../FSharp.Compiler.Service.0.0.86/lib/net45"
#I "../FSharpVSPowerTools.Core.1.7.0/lib/net45"

// Standard Paket locations
#I "../FSharp.Compiler.Service/lib/net45"
#I "../FSharpVSPowerTools.Core/lib/net45"

// Try various folders that people might like
#I "bin"
#I "../bin"
#I "../../bin"
#I "lib"

// Reference VS PowerTools, Razor and F# Formatting components
#r "FSharpVSPowerTools.Core.dll"
#r "System.Web.Razor.dll"
#r "RazorEngine.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"