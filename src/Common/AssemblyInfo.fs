namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Formatting")>]
[<assembly: AssemblyProductAttribute("FSharp.Formatting")>]
[<assembly: AssemblyDescriptionAttribute("A package of libraries for building great F# documentation, samples and blogs")>]
[<assembly: AssemblyVersionAttribute("3.0.0")>]
[<assembly: AssemblyFileVersionAttribute("3.0.0")>]
[<assembly: AssemblyInformationalVersionAttribute("3.0.0-beta01")>]
[<assembly: AssemblyCopyrightAttribute("Apache 2.0 License")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "3.0.0"
