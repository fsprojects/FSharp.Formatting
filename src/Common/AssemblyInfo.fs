namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Formatting")>]
[<assembly: AssemblyProductAttribute("FSharp.Formatting")>]
[<assembly: AssemblyDescriptionAttribute("A package of libraries for building great F# documentation, samples and blogs")>]
[<assembly: AssemblyVersionAttribute("2.12.0")>]
[<assembly: AssemblyFileVersionAttribute("2.12.0")>]
[<assembly: AssemblyInformationalVersionAttribute("2.12.0")>]
[<assembly: AssemblyCopyrightAttribute("Apache 2.0 License")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.12.0"
