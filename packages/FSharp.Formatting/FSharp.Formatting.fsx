#nowarn "211"
// Standard NuGet or Paket location
#I "."
#I "lib/net40"
#r "./lib/net40/System.Web.Razor.dll"

// Standard NuGet locations
#r "../Microsoft.AspNet.Razor.3.2.3/lib/net45/System.Web.Razor.dll"
#I "../RazorEngine.3.6.4/lib/net45"
#I "../FSharp.Compiler.Service.0.0.87/lib/net45"
#I "../FSharpVSPowerTools.Core.1.8.0/lib/net45"

// Standard Paket locations
#r "../Microsoft.AspNet.Razor/lib/net45/System.Web.Razor.dll"
#I "../RazorEngine/lib/net45"
#I "../FSharp.Compiler.Service/lib/net45"
#I "../FSharpVSPowerTools.Core/lib/net45"


// Try various folders that people might like
#I "bin"
#I "../bin"
#I "../../bin"
#I "lib"

// Reference VS PowerTools, Razor and F# Formatting components
#r "FSharpVSPowerTools.Core.dll"
#r "FSharp.Markdown.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
