[![Binder](https://mybinder.org/badge_logo.svg)](https://mybinder.org/v2/gh/fsprojects/FSharp.Formatting/gh-pages?filepath=index.ipynb)

F# Formatting: Documentation Tools for F# Code
==================================

The FSharp.Formatting package includes libraries and tools for processing F# script files, markdown and components
for documentation generation. Functionality from the F# Formatting package is used by the [MiniScaffold template](https://github.com/TheAngryByrd/MiniScaffold/) 
that is used by a large number of open source .NET projects, as well as Waypoint and many other repositories.

F# Formatting can be used as a .NET tool called [`fsdocs`](commandline.html):

    dotnet tool install FSharp.Formatting.CommandTool
    dotnet fsdocs build 

The F# Formatting package is also [available on NuGet](https://nuget.org/packages/FSharp.Formatting) as a set of libraries. The NuGet package comes with a load script `FSharp.Formatting.fsx` that references all the required DLLs
and paths. 

 - [Content](content.html) - explains the content expected in the `docs` directory for the `fsdocs` tool.
   
 - [Command line tool](commandline.html) - explains how to use the `fsdocs` tool.
   
 - [Literate programming](literate.html) - explains how to generate documentation
   for your projects or to write nicely formatted F# blog posts. 
   
 - [Literate output embedding](evaluation.html) - provides more details on literate programming and
   explains how to embed results of a literate script file in the generated output. This way,
   you can easily format the results of running your code!

 - [Markdown parser](markdown.html) - explains the F# Markdown
   processor that is available in this library with some basic examples of
   document processing.

 - [F# code formatting](codeformat.html) - more details about the F# code
   formatter and how to use it to obtain information about F# source files.

 - [API documentation tool](apidocs.html) - how to generate HTML documentation
   from "XML comments" in your .NET libraries. The tool handles comments written in
   Markdown too and generates a search index.

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
