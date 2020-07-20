namespace FSharp.Formatting.CommandTool

open CommandLine

open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.Literate.Evaluation
open FSharp.Formatting.CommandTool.Common

/// Process directory containing a mix of Markdown documents and F# Script files
[<Verb("convert", HelpText = "convert a directory of literate scripts or markdown to another format")>]
type ConvertCommand() =

    // default settings will be mapped to 'None'
    [<Option("input", Required = false, Default="docs", HelpText = "Input directory of *.fsx and *.md files.")>]
    member val input = "" with get, set

    [<Option("template", Required = false, HelpText = "Template file for formatting (optional).")>]
    member val template = "" with get, set

    [<Option("output", Required = false, Default="output", HelpText = "Ouput Directory, defaults to 'output' (optional).")>]
    member val output = "" with get, set

    [<Option("prefix", Required = false, HelpText = "Prefix for formatting, defaults to 'fs' (optional).")>]
    member val prefix = "" with get, set

    [<Option("fscoptions", Required = false, HelpText = "Compiler Options (optional).")>]
    member val fscoptions = Seq.empty<string> with get, set

    [<Option("nolinenumbers", Required = false, HelpText = "Don't add line numbers, default is to add line numbers (optional).")>]
    member val nolinenumbers = false with get, set

    [<Option("references", Required = false, HelpText = "Turn all indirect links into references, defaults to 'false' (optional).")>]
    member val references = false with get, set

    [<Option("eval", Required = false, HelpText = "Use the default FsiEvaluator, defaults to 'false'")>]
    member val eval = false with set, get

    [<Option("norecursive", Required = false, HelpText = "Disable recursive processing of sub-directories")>]
    member val norecursive = false with set, get

    [<Option("parameters", Required = false, HelpText = "A whitespace separated list of string pairs as text replacement patterns for the format template file (optional).")>]
    member val parameters = Seq.empty<string> with get, set

    member x.Execute() =
        let mutable res = 0
        use watcher = new System.IO.FileSystemWatcher(x.input)
        try
                let run () =
                    Literate.ConvertDirectory(
                        x.input,
                        ?generateAnchors = Some true,
                        ?htmlTemplate = (evalString x.template),
                        ?outputDirectory = Some x.output,
                        ?formatAgent = None,
                        ?prefix = (evalString x.prefix),
                        ?fscoptions = (evalString (concat x.fscoptions)),
                        ?lineNumbers = Some (not x.nolinenumbers),
                        ?recursive = Some (not x.norecursive),
                        ?references = Some x.references,
                        ?fsiEvaluator = (if x.eval then Some ( FsiEvaluator() :> _) else None),
                        ?parameters = evalPairwiseStrings x.parameters)

                run()

        with
            | _ as ex ->
                Log.errorf "received exception in Literate.ConvertDirectory:\n %A" ex
                printfn "Error on Literate.ConvertDirectory: \n%O" ex
                res <- -1
        res
