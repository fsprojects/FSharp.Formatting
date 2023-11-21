namespace FSharp.Formatting.Literate

open System.IO
open System.Text.Json
open FSharp.Formatting.Templating
open FSharp.Formatting.PynbModel

module internal ParsePynb =

    type ParsedCell =
        | Code of
            {| lang: string
               source: string
               outputs: string[] option |}
        | Markdown of source: string

        member this.ToMarkdown() =
            match this with
            | Markdown source -> source
            | Code code ->
                let codeBlock = sprintf $"```{code.lang}\n{addLineEnd code.source}```"

                match code.outputs with
                | None -> codeBlock
                | Some outputs ->
                    let outputsString = outputs |> String.concat "\n"
                    sprintf $"{codeBlock}\n{outputsString}"

    module Output =
        let (|TextHtml|_|) (x: JsonElement) =
            match x.TryGetProperty("text/html") with
            | true, html ->
                let html = html.EnumerateArray() |> Seq.map (fun x -> x.GetString()) |> String.concat "\n"
                Some $"<p>{html}</p>"
            | _ -> None

        let (|TextPlain|_|) (x: JsonElement) =
            match x.TryGetProperty("text/plain") with
            | true, text ->
                let text = text.EnumerateArray() |> Seq.map (fun x -> x.GetString()) |> String.concat ""
                Some $"""<table class="pre"><tbody><tr><td><pre><code>{text}</code></pre></td></tr></tbody></table>"""
            | _ -> None

        let (|DisplayData|_|) (x: JsonElement) =
            match x.TryGetProperty("output_type") with
            | true, outputType ->
                if outputType.GetString() = "display_data" then
                    match x.TryGetProperty("data") with
                    | true, TextHtml html -> html
                    | true, TextPlain text -> text
                    | true, s -> failwith $"unknown output {s}"
                    | false, _ -> failwith "no data property"
                    |> Some
                else
                    None
            | _ -> failwith "no output_type property"

        let (|Stream|_|) (x: JsonElement) =
            match x.TryGetProperty("output_type") with
            | true, outputType ->
                if outputType.GetString() = "stream" then
                    let text =
                        match x.TryGetProperty("text") with
                        | true, xs -> xs.EnumerateArray() |> Seq.map (fun x -> x.GetString()) |> String.concat ""
                        | _ -> failwith "no text property"

                    Some
                        $"""<table class="pre"><tbody><tr><td><pre><code>{text}</code></pre></td></tr></tbody></table>"""
                else
                    None
            | _ -> failwith "no output_type property"

        let parse (output: JsonElement) =
            match output with
            | Stream stream -> stream
            | DisplayData displayData -> displayData
            | s -> failwith $"""unknown output {s.GetProperty("output_type").GetString()}"""

    let getSource (cell: JsonElement) =
        let source =
            match cell.TryGetProperty("source") with
            | true, xs -> xs.EnumerateArray()
            | _ -> failwith "no source"

        source |> Seq.map (fun x -> x.GetString()) |> String.concat ""

    let collectOutputs (cell: JsonElement) =
        match cell.TryGetProperty("outputs") with
        | true, outputs ->
            let xs = outputs.EnumerateArray()

            if Seq.isEmpty xs then
                None
            else
                xs |> Seq.map Output.parse |> Seq.toArray |> Some
        | _ -> None

    let getCode (cell: JsonElement) =
        let lang =
            let metadata (elem: JsonElement) =
                match elem.TryGetProperty("metadata") with
                | false, _ -> failwith "Code cell does not have metadata"
                | true, metadata -> metadata

            let languageInfo (metadata: JsonElement) =
                match metadata.TryGetProperty("polyglot_notebook") with
                | false, _ -> failwith "code cell does not have metadata.polyglot_notebook"
                | true, language_info -> language_info

            let kernelName (languageInfo: JsonElement) =
                match languageInfo.TryGetProperty("kernelName") with
                | false, _ -> failwith "code cell does not have metadata.polyglot_notebook.kernelName"
                | true, name -> name.GetString()

            cell |> metadata |> languageInfo |> kernelName

        let source = getSource cell
        let outputs = collectOutputs cell

        Code
            {| lang = lang
               source = source
               outputs = outputs |}


    let parseCell (cell: JsonElement) =
        let cell_type =
            match cell.TryGetProperty("cell_type") with
            | true, cellType -> cellType.GetString()
            | _ -> failwith "no cell type"

        match cell_type with
        | "markdown" ->
            match getSource cell, collectOutputs cell with
            | _, Some _ -> failwith $"Markdown should not have outputs"
            | source, None -> Markdown source
        | "code" -> getCode cell
        | _ -> failwith $"unknown cell type {cell_type}"

    let pynbStringToMarkdown (ipynb: string) =
        let json = JsonDocument.Parse(ipynb)

        json.RootElement.GetProperty("cells").EnumerateArray()
        |> Seq.map (parseCell >> (fun x -> x.ToMarkdown()))
        |> String.concat "\n"

    let pynbToMarkdown ipynbFile =
        ipynbFile |> File.ReadAllText |> pynbStringToMarkdown

    let parseFrontMatter ipynbFile =
        let json = JsonDocument.Parse(ipynbFile |> File.ReadAllText)

        json.RootElement.GetProperty("cells").EnumerateArray()
        |> Seq.map parseCell
        |> Seq.choose (fun cell ->
            match cell with
            | Code _ -> None
            | Markdown source ->
                let lines = source.Split([| '\n'; '\r' |], System.StringSplitOptions.RemoveEmptyEntries)
                FrontMatterFile.ParseFromLines ipynbFile lines)
        |> Seq.tryHead
