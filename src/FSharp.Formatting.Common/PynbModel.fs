/// Internal model for generating Jupyter notebook (.ipynb) files from literate scripts and F# Interactive sessions.
/// Types in this module mirror the JSON structure of the Jupyter nbformat 4 spec.
module internal FSharp.Formatting.PynbModel

open System.Web

/// Escapes a string for use as a JSON string value (with surrounding quotes).
let escapeAndQuote (txt: string) =
    HttpUtility.JavaScriptStringEncode(txt, true)

/// Ensures a string ends with a newline character, as required by the nbformat source line convention.
let addLineEnd (s: string) = if s.EndsWith '\n' then s else s + "\n"

/// Represents the output data payload of a notebook cell execution result, keyed by MIME type.
type OutputData =
    | OutputData of kind: string * lines: string array

    override this.ToString() =
        let (OutputData(kind, lines)) = this

        sprintf
            """
            "%s": [%s]
        """
            kind
            (String.concat ",\n" (Array.map escapeAndQuote lines))

/// Represents a single output produced by executing a notebook code cell.
type Output =
    { data: OutputData
      execution_count: int option
      metadata: string
      output_type: string }

    override this.ToString() =
        sprintf
            """
          {
           "data": {%s},
           "execution_count": %s,
           "metadata": {%s},
           "output_type": "%s"
          }"""
            (this.data.ToString())
            (match this.execution_count with
             | None -> "null"
             | Some(x) -> string<int> x)
            this.metadata
            this.output_type

/// Represents a single cell in a Jupyter notebook (code, markdown, or raw).
type Cell =
    { cell_type: string
      execution_count: int option
      metadata: string
      outputs: Output array
      source: string array }

    static member Default =
        { cell_type = "code"
          execution_count = None
          metadata =
            """
    "dotnet_interactive": {
     "language": "fsharp"
    },
    "polyglot_notebook": {
     "kernelName": "fsharp"
    }
   """
          outputs = [||]
          source = [||] }

    override this.ToString() =
        sprintf
            """
  {
   "cell_type": %s,
   "metadata": {%s},
   %s
   "source": [
    %s
   ]
  }"""
            (escapeAndQuote this.cell_type)

            this.metadata
            (if this.cell_type <> "code" then
                 ""
             else
                 (sprintf
                     """"execution_count": %s, "outputs": [%s],"""
                     (match this.execution_count with
                      | None -> "null"
                      | Some(x) -> string<int> x)
                     (this.outputs |> Array.map string<Output> |> String.concat ",\n")))
            (this.source
             |> Array.map (addLineEnd >> escapeAndQuote)
             |> String.concat ",\n    ")

/// Metadata describing the kernel (language runtime) used to execute the notebook.
type Kernelspec =
    { display_name: string
      language: string
      name: string }

    static member Default =
        { display_name = ".NET (F#)"
          language = "F#"
          name = ".net-fsharp" }

    override this.ToString() =
        sprintf
            """{
   "display_name": %s,
   "language": %s,
   "name": %s
  }"""
            (escapeAndQuote this.display_name)
            (escapeAndQuote this.language)
            (escapeAndQuote this.name)

/// Metadata describing the programming language used in the notebook (for syntax highlighting and MIME types).
type LanguageInfo =
    { file_extension: string
      mimetype: string
      name: string
      pygments_lexer: string }

    static member Default =
        { file_extension = ".fs"
          mimetype = "text/x-fsharp"
          name = "polyglot-notebook"
          pygments_lexer = "fsharp" }

    override this.ToString() =
        sprintf
            """{
   "file_extension": %s,
   "mimetype": %s,
   "name": %s,
   "pygments_lexer": %s
  },"""
            (escapeAndQuote this.file_extension)
            (escapeAndQuote this.mimetype)
            (escapeAndQuote this.name)
            (escapeAndQuote this.pygments_lexer)

/// Polyglot Notebooks kernel info, identifying the default kernel for the notebook.
type DefaultKernelInfo =
    { defaultKernelName: string
      languageName: string
      name: string }

    static member Default =
        { defaultKernelName = "fsharp"
          languageName = "fsharp"
          name = "fsharp" }

    override this.ToString() =
        sprintf
            """{
   "kernelInfo": {
    "defaultKernelName": %s,
    "items": [
     {
      "aliases": [],
      "languageName": %s,
      "name": %s
     }
    ]
   }
  }"""
            (escapeAndQuote this.defaultKernelName)
            (escapeAndQuote this.languageName)
            (escapeAndQuote this.name)

/// Top-level notebook metadata combining kernel spec, language info, and Polyglot Notebooks extension data.
type Metadata =
    { kernelspec: Kernelspec
      language_info: LanguageInfo
      defaultKernelInfo: DefaultKernelInfo }

    static member Default =
        { kernelspec = Kernelspec.Default
          language_info = LanguageInfo.Default
          defaultKernelInfo = DefaultKernelInfo.Default }

    override this.ToString() =
        sprintf
            """{
  "kernelspec": %O,
  "language_info": %O
  "polyglot_notebook": %O
 }"""
            this.kernelspec
            this.language_info
            this.defaultKernelInfo

/// The root Jupyter notebook document, containing all cells and metadata.
type Notebook =
    { nbformat: int
      nbformat_minor: int
      metadata: Metadata
      cells: Cell array }

    static member Default =
        { nbformat = 4
          nbformat_minor = 2
          metadata = Metadata.Default
          cells = [||] }

    override this.ToString() =
        sprintf
            """
{
 "cells": [%s
 ],
 "metadata": %O,
 "nbformat": %i,
 "nbformat_minor": %i
}"""
            (this.cells |> Array.map string<Cell> |> String.concat "\n,")
            this.metadata
            this.nbformat
            this.nbformat_minor

let internal splitLines (s: string) =
    s.Replace("\r\n", "\n").Split([| '\n' |])

/// Creates a code cell with the given source lines, execution count, and outputs.
let codeCell (lines: string array) executionCount outputs =
    let lines = lines |> Array.collect splitLines |> Array.map addLineEnd

    let cell =
        { Cell.Default with
            execution_count = executionCount
            cell_type = "code"
            source = lines
            outputs = outputs }

    cell

/// Creates a raw (non-executed) cell with the given content string.
let rawCell (s: string) =
    { Cell.Default with
        cell_type = "raw"
        source = splitLines s }

/// Creates a markdown cell with the given source lines.
let markdownCell (lines: string array) =
    let lines = lines |> Array.collect splitLines |> Array.map addLineEnd

    { Cell.Default with
        cell_type = "markdown"
        metadata = ""
        source = lines }
