F# Formatting
=============

This project contains an F# implementation of Markdown parser and a 
library that formats F# code. The formatting includes colorization, but 
also generation of tooltips with types and other information similar to 
those displayed in Visual Studio when reading F# code. An example can be
found on [F# snippets web site](http://www.fssnip.net).

**TODO:** More information will be added soon.

Using the library
-----------------

The two parts (Markdown parser and F# code formatter) are separate libraries,
but they can be easily combined together. The following script shows how to 
parse Markdown document and then iterate over all CodeBlock elements (source
code samples), add formatting to them and then put them back into a document.

 * [example script used at www.tryjoinads.org](https://github.com/tpetricek/TryJoinads/blob/master/tools/build.fsx)

### Using the F# formatting

To format F# code using the `FSharp.Formatting.dll` library, you need to reference
the library, open necessary namespaces and then create an instance of formatting
agent:

    #r "FSharp.CodeFormat.dll"
	open FSharp.CodeFormat
	
    let fsharpCompiler = "..\\FSharp.Compiler.dll"
	let asm = System.Reflection.Assembly.LoadFile(fsharpCompiler)
	let formatAgent = CodeFormat.CreateAgent(asm)
 
Then you can use the agent repeatedly (it loads the F# compiler, so it is wise to reuse
the same instance) to format a snippet as follows:

    // 'fsharpSource' contains the actual source code,
	// the first argument specifies a file name (does not need
	// to physically exist) and the last is compiler arguments
    let snippets, errors = formatAgent.ParseSource("source.fsx", fsharpSource, "")
	
	// This formats the snippets into HTML 
	let formatted = CodeFormat.FormatHtml(snippets, "ft", false, false)
 
Known issues
------------

 - Nested emhasis using the same characters is not implemented.
   For example `***bold italic**just italic*`

 - Parsing of lists is a bit messy and needs to be written
   differently.
