﻿namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FSharp.Formatting")>]
[<assembly: AssemblyProductAttribute("FSharp.Formatting")>]
[<assembly: AssemblyDescriptionAttribute("A package for building great F# documentation, samples and blogs")>]
[<assembly: AssemblyVersionAttribute("2.2.12")>]
[<assembly: AssemblyFileVersionAttribute("2.2.12")>]
[<assembly: AssemblyCopyrightAttribute("Apache 2.0 License")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2.2.12"
