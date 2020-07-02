module FSharp.Formatting.Common.PynbModel

open System
open System.IO
open System.Web

let escapeAndQuote(txt: string) = 
    HttpUtility.JavaScriptStringEncode(txt,true)

let addLineEnd (s: string) = if s.EndsWith("\n") then s else s + "\n"

type OutputData =
    | OutputHtml of string list
    override this.ToString() =
        match this with
        | OutputHtml lines ->
            """
                "text/html": [
            """ + String.concat "\n" (List.map escapeAndQuote lines)
            + "]"
type Output =
    {
        data: OutputData
        execution_count : int
        metadata : string
        output_type: string
    }
    override this.ToString() =
        sprintf """
          {
           "data": {%s},
           "execution_count": %d,
           "metadata": {%s},
           "output_type": "%s"
          }"""
              (this.data.ToString())
              this.execution_count
              this.metadata
              this.output_type

type Cell =
    {
        cell_type : string
        execution_count : int option
        metadata : string
        outputs : Output[]
        source : string[]
    }
    static member Default = 
        {
            cell_type = "code"
            execution_count = None
            metadata = ""
            outputs = [||]
            source = [||]
        }

    override this.ToString() =
        sprintf """
          {
           "cell_type": %s,
           "metadata": {%s},
           %s
           "source": [%s]
          }""" (escapeAndQuote this.cell_type)
              
              this.metadata
              (if this.cell_type <> "code" then "" else
                  (sprintf """ "execution_count": %s, "outputs": [%s], """
                        (match this.execution_count with | None -> "null" | Some(x) -> string x) 
                        (this.outputs |> Array.map string |> String.concat ",\n")))
              (this.source |> Array.map addLineEnd |> Array.map escapeAndQuote |> String.concat ",\n")

type Kernelspec = 
    {
        display_name : string
        language : string
        name : string
    }
    static member Default = {display_name = ".NET (F#)"; language = "F#"; name = ".net-fsharp"}
    override this.ToString() = 
        sprintf """{"display_name": %s, "language": %s, "name": %s}""" 
            (escapeAndQuote this.display_name) 
            (escapeAndQuote this.language) 
            (escapeAndQuote this.name)

type LanguageInfo = 
    {
        file_extension : string
        mimetype : string
        name : string
        pygments_lexer : string
        version : string
    }
    static member Default = 
        {file_extension = ".fs"; mimetype = "text/x-fsharp"; name = "C#"; pygments_lexer = "fsharp"; version = "4.5"}
    override this.ToString() = 
        sprintf """{
        "file_extension": %s,
        "mimetype": %s,
        "name": %s,
        "pygments_lexer": %s,
        "version": %s
        }""" 
            (escapeAndQuote this.file_extension)
            (escapeAndQuote this.mimetype)
            (escapeAndQuote this.name)
            (escapeAndQuote this.pygments_lexer)
            (escapeAndQuote this.version)

type Metadata = 
    {
        kernelspec : Kernelspec
        language_info : LanguageInfo
    }
    static member Default = 
        {kernelspec = Kernelspec.Default; language_info = LanguageInfo.Default}
    override this.ToString() =
        sprintf """{
            "kernelspec": %O,
            "langauge_info": %O
        }""" this.kernelspec this.language_info

type Notebook =
    {
        nbformat : int
        nbformat_minor : int
        metadata : Metadata
        cells : Cell[]
    }
    static member Default = 
        {nbformat = 4; nbformat_minor = 1; metadata = Metadata.Default; cells = [||]}
    override this.ToString() = 
        sprintf """
        {
            "cells": [%s],
            "metadata": %O,
            "nbformat": %i,
            "nbformat_minor": %i
        }
        """ 
            (this.cells |> Array.map string |> String.concat "\n,") 
            this.metadata this.nbformat this.nbformat_minor

let internal splitLines (s: string) = s.Split([|'\n';'\r'|])

let codeCell(lines: string[]) executed codeOutputOption = 
    let lines = lines |> Array.collect splitLines |> Array.map addLineEnd
    let cell = {Cell.Default with execution_count = (if executed then Some 1 else None); cell_type = "code"; source = lines}
    let cell =
        match codeOutputOption with
        | None -> cell
        | Some outputs -> {cell with outputs = outputs}
    cell

let rawCell (s: string) = 
    {Cell.Default with cell_type = "raw"; source = splitLines s}

let markdownCell (lines: string[]) = 
    let lines = lines |> Array.collect splitLines |> Array.map addLineEnd
    {Cell.Default with cell_type = "markdown"; source = lines}
