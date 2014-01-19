F# Formatting: Documentation Tools
==================================

The F# Formatting libraries (`FSharp.CodeFormat.dll` and `FSharp.Markdown.dll`) include an F# implementation 
of Markdown parser and F# code formatter that can used to tokenize F# code and obtain information about tokens 
including tool-tips (as in Visual Studio and MonoDevelop) with type information. The library also comes with 
a sample that implements literate programming for F#.

## Build Status
[![Build Status](https://travis-ci.org/tpetricek/FSharp.Formatting.png)](https://travis-ci.org/tpetricek/FSharp.Formatting)

## Documentation 

The documentation for this library is automatically generated (using the literate programming tools based on
the library) from `*.fsx` and `*.md` files in [the docs folder][2]. If you find a typo, please submit a pull request! 

 - [F# Formatting: Documentation tools][3] provides more information about the library, how to contribute, etc. It also
   includes links to tutorials showing how to use the Markdown parser and F# code formatter.
   
 - [F# Formatting: Literate programming][4] documents the most interesting part of the package - script that
   can be used to generate documentation for F# projects from commented F# script files and Markdown documents.

### Usees
The library is used by a number of F# projects. Most prominently, the [F# snippets web site](http://www.fssnip.net)
uses it to format snippets shared by the F# community. The following sample scripts use the library to generate 
documentation and might be a useful inspiration:

 * [The `build.fsx` script](https://github.com/tpetricek/FSharp.Formatting/blob/master/build.fsx) used to generate the documentation for this project
 * [The `build.fsx` script](https://github.com/tpetricek/FSharp.Data/blob/master/tools/build.fsx) used to generate the documentation for `FSharp.Data`

## Library license

The library is available under Apache 2.0. For more information see the [License file][1] in the GitHub repository.

 [1]: https://github.com/tpetricek/FSharp.Formatting/blob/master/LICENSE.md
 [2]: https://github.com/tpetricek/FSharp.Formatting/tree/master/docs
 [3]: http://tpetricek.github.com/FSharp.Formatting/
 [4]: http://tpetricek.github.com/FSharp.Formatting/demo.html
