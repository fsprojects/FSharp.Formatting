[![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=index.ipynb)

F# Formatting: Documentation tools
==================================

The FSharp.Formatting package includes libraries and tools for processing F# script files, markdown and components
for documentation generation.

Functionality from the F# Formatting package is used by the [MiniScaffold template](https://github.com/TheAngryByrd/MiniScaffold/) 
that is used by a large number of open source .NET projects, as well as Waypoint and many other repositories.

F# Formatting can be used as a .NET tool called [`fsdocs`](commandline.html):

    dotnet tool install FSharp.Formatting.CommandTool
    dotnet fsdocs build --input docs 

The F# Formatting package is also [available on NuGet](https://nuget.org/packages/FSharp.Formatting) as a set of libraries. The NuGet package comes with a load script `FSharp.Formatting.fsx` that references all the required DLLs
and paths. 

The sub-components are:

 - [Command line tool](commandline.html) - documents how to use the `fsdocs` tool.
   
 - [Literate programming](literate.html) - if you want to use the library to generate documentation
   for your projects or if you want to use it to write nicely formatted F# blog posts, then
   start here. This page describes the literate programming sample. 
   
 - [Literate output embedding](evaluation.html) provides more details on literate programming and
   explains how to embed results of a literate script file in the generated output. This way,
   you can easily format the results of running your code!

 - [Markdown parser](markdown.html) - this page provides more details about the F# Markdown
   processor that is available in this library. It includes some basic examples of
   document processing.

 - [F# code formatting](codeformat.html) - this page provides more details about the F# code
   formatter; it discusses how to call it to obtain information about F# source files.

 - [API documentation tool](apidocs.html) - provides a brief documentation for a tool
   that generates nice HTML documentation from "XML comments" in your .NET libraries.
   The tool handles comments written in Markdown too. 

 - [Contribute](https://github.com/fsprojects/FSharp.Formatting/blob/master/CONTRIBUTING.md) - how do I contribute?

The documentation for this library is generated automatically using the literate programming 
tools that are built on top of it and are described in the [literate programming page](literate.html).
If you spot a typo, please submit a pull request! The source Markdown and F# script files are
available in the [docs folder on GitHub](https://github.com/fsprojects/FSharp.Formatting/tree/master/docs).

More information
----------------

The project is hosted on [GitHub](https://github.com/fsprojects/FSharp.Formatting) where you can 
[report issues](https://github.com/fsprojects/FSharp.Formatting/issues), fork the project and submit pull requests.
Thanks to [Gustavo Guerra](https://github.com/ovatsus) for a great build script and 
[Steffen Forkmann](https://github.com/forki) for the great build tool [FAKE](https://github.com/fsharp/FAKE).
The library is available under Apache 2.0. For more information see the 
[License file](https://github.com/fsprojects/FSharp.Formatting/blob/master/LICENSE.md) in the GitHub repository.
