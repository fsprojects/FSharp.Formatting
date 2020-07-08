F# Formatting: Documentation tools
==================================

The FSharp.Formatting package include 
an F# implementation of a Markdown parser and an F# code formatter that can be used to tokenize F# 
code and obtain information about tokens including tool-tips 
with type information. The package also comes with a sample that implements literate programming for F#
(and was used to generate this documentation).

Usage:

 - The F# Formatting package is used by the [MiniScaffold template](https://github.com/TheAngryByrd/MiniScaffold/) 
   that is used by a large number of open source .NET projects. 

 - The F# Formatting package is [available on NuGet](https://nuget.org/packages/FSharp.Formatting),
   so if you want to use some of its components (for blogging, Markdown parsing, or F# code formatting),
   then the best option is to get the package.

 - The NuGet package comes with a load script `FSharp.Formatting.fsx` that references all the required DLLs
   and paths. If you are calling F# Formatting from a script file, then it is recommended to use `#load "FSharp.Formatting.fsx"`
   as a future-proof way of referencing the library.

 - F# Formatting can be used as a .NET tool:

       dotnet tool install FSharp.Formatting.CommandTool
       fsformatting convert --input docs/content --template docs/tools/template.html
       fsformatting generate --dlls bin/MyLibrary.dll --template docs/tools/reference/template.html

Documentation
-------------

The documentation for this library is generated automatically using the literate programming 
tools that are built on top of it and are described in the [literate programming page](literate.html).
If you spot a typo, please submit a pull request! The source Markdown and F# script files are
available in the [docs folder on GitHub](https://github.com/fsprojects/FSharp.Formatting/tree/master/docs).
I hope it is also a good sample showing how to write documentation for F# projects.

 - [Literate programming](literate.html) - if you want to use the library to generate documentation
   for your projects or if you want to use it to write nicely formatted F# blog posts, then
   start here! This page describes the literate programming sample. 
   
 - [Output embedding](evaluation.html) provides more details on literate programming and
   explains how to embed results of a literate script file in the generated output. This way,
   you can easily format the results of running your code!

 - [Markdown parser](markdown.html) - this page provides more details about the F# Markdown
   processor that is available in this library. It includes some basic examples of
   document processing.

 - [F# code formatting](codeformat.html) - this page provides more details about the F# code
   formatter; it discusses how to call it to obtain information about F# source files.

 - [API documentation tool](metadata.html) - provides a brief documentation for a tool
   that generates nice HTML documentation from "XML comments" in your .NET libraries.
   The tool handles comments written in Markdown too. 

 - [Contribute](https://github.com/fsprojects/FSharp.Formatting/blob/master/CONTRIBUTING.md) - how do I contribute?

More information
----------------

The project is hosted on [GitHub](https://github.com/fsprojects/FSharp.Formatting) where you can 
[report issues](https://github.com/fsprojects/FSharp.Formatting/issues), fork the project and submit pull requests.
Thanks to [Gustavo Guerra](https://github.com/ovatsus) for a great build script and 
[Steffen Forkmann](https://github.com/forki) for the great build tool [FAKE](https://github.com/fsharp/FAKE).
The library is available under Apache 2.0. For more information see the 
[License file](https://github.com/fsprojects/FSharp.Formatting/blob/master/LICENSE.md) in the GitHub repository.
