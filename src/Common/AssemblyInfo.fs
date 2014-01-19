namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Formatting")>]
[<assembly: AssemblyProductAttribute("FSharp.Formatting")>]
[<assembly: AssemblyDescriptionAttribute("A package for building great F# documentation, samples and blogs")>]
[<assembly: AssemblyVersionAttribute("2.3.2")>]
[<assembly: AssemblyFileVersionAttribute("2.3.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.3.2"
