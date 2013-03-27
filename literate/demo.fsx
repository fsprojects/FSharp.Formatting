// Given a typical setup (with 'FSharp.Formatting' referenced using NuGet),
// the following will include binaries and load the literate script
#I "../bin"
#load "literate.fsx"
open FSharp.Literate

/// This functions processes a single F# Script file
let processScript filename outputKind =
  let file = __SOURCE_DIRECTORY__ + "\\test.fsx"
  let template = __SOURCE_DIRECTORY__ + filename
  Literate.ProcessScriptFile(file, template, outputKind)

/// This functions processes a single Markdown document
let processDocument filename outputKind =
  let file = __SOURCE_DIRECTORY__ + "\\demo.md"
  let template = __SOURCE_DIRECTORY__ + filename
  Literate.ProcessMarkdown(file, template, outputKind)

/// This functions processes an entire directory containing
/// multiple script files (*.fsx) and Markdown documents (*.md)
/// and it specifies additional replacements for the template file
let processDirectory() =
  let dir = __SOURCE_DIRECTORY__
  let template = __SOURCE_DIRECTORY__ + "\\templates\\template-project.html"
  let projInfo =
    [ "page-description", "F# Literate Programming"
      "page-author", "Tomas Petricek"
      "github-link", "https://github.com/tpetricek/FSharp.Formatting"
      "project-name", "F# Formatting" ]

  Literate.ProcessDirectory
    ( dir, template, Html, dir + "\\output", 
      replacements = projInfo)

#time "on";;
let a = processScript "\\templates\\template-file.html" Html;;
let b = processDocument "\\templates\\template-file.html" Html;;
let c = processScript "\\templates\\template-color.tex" Latex;;
let d = processDocument "\\templates\\template-color.tex" Latex;;