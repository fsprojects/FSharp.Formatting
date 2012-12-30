// --------------------------------------------------------------------------------------
// Builds the documentation from the files in the 'docs' directory
// --------------------------------------------------------------------------------------

#I "../bin"
#load "../literate/literate.fsx"
open System.IO
open FSharp.Literate

let generateDocs() =
  let source = __SOURCE_DIRECTORY__
  let template = Path.Combine(source, "template.html")
  let sources = Path.Combine(source, "../docs")
  let output = Path.Combine(source, "../docs/output")

  // Additional strings to be replaced in the HTML template
  let projInfo =
    [ "page-description", """Provides an F# implementation of Markdown parser and F# 
        code formatter that can used to tokenize F# code and obtain information about 
        tokens including tool tips with type information. The package comes with a sample 
        that implements literate programming for F#.""".Replace("\n","").Replace("      ", "")
      "page-author", "Tomas Petricek"
      "github-link", "https://github.com/tpetricek/FSharp.Formatting"
      "project-name", "F# Formatting" ]

  // Compiler options (reference the two dll files and System.Web.dll)
  let options = 
    "--reference:\"" + source + "/../bin/FSharp.CompilerBinding.dll\" " +
    "--reference:\"" + source + "/../bin/FSharp.CodeFormat.dll\" " +
    "--reference:\"" + source + "/../bin/FSharp.Markdown.dll\" " +
    "--reference:System.Web.dll"

  // We want to use "typical" paths in 'docs/demo.fsx' and so we 
  // first copy some files to fake the typical situation with NuGet
  let temp = Path.Combine(source, "../packages/FSharp.Formatting.1.0.4/literate/")
  Directory.CreateDirectory(temp) |> ignore
  File.Copy(Path.Combine(source, "../literate/literate.fsx"), Path.Combine(temp, "literate.fsx"))

  // Now we can process the samples directory (with some additional references)
  // and then we clean up the files & directories we had to create earlier
  Literate.ProcessDirectory
    ( sources, template, output, replacements = projInfo, 
      compilerOptions = options )

  // Process the literate.fsx script separately
  let changeTime = File.GetLastWriteTime(Path.Combine(source, "../literate/literate.fsx"))
  let generateTime = File.GetLastWriteTime(Path.Combine(output, "literate.html"))
  if changeTime > generateTime then
    printfn "Generating 'literate.html'"
    Literate.ProcessScriptFile
      ( Path.Combine(source, "../literate/literate.fsx"), template,
        Path.Combine(output, "literate.html"), 
        compilerOptions = options, replacements = projInfo )

  File.Delete(Path.Combine(temp, "literate.fsx"))
  Directory.Delete(Path.Combine(source, "../packages"), true)

// Generate documentation
generateDocs()