namespace FSharp.Formatting.CommandTool

open System.IO
open CommandLine
open CommandLine.Text

open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Common
open FSharp.Formatting.CommandTool.Common

/// Exposes metadata formatting functionality. 
[<Verb("api", HelpText = "generate API reference docs from metadata")>]
type ApiDocsCommand() =
    member x.GetUsageOfOption() =
        let help = new HelpText()
        help.AddDashesToOption <- true
        "\nfsdocs api [options]" +
        "\n------------------------------------------------" +
        help.ToString()

    [<Option("help", Required = false, HelpText = "Display this message. All options are case-sensitive.")>]
    member val help = false with get, set

    [<Option("waitForKey", Required = false, HelpText = "Wait for key before exit.")>]
    member val waitForKey = false with get, set

    [<Option("dlls", Required = true, HelpText = "DLL input file list.")>]
    member val dlls = Seq.empty<string> with get, set

    [<Option("output", Required = false, Default="output/reference", HelpText = "Output Directory (optional, defaults to 'output')")>]
    member val output = "" with get, set

    [<Option("parameters", Required = false, HelpText = "Property settings for {{prop-name}} substitutions in the template (optional).")>]
    member val parameters = Seq.empty<string> with get, set

    [<Option("template", Required = false, HelpText = "template file for formatting (optional).")>]
    member val template = "" with get, set

    [<Option("xmlFile", Required = false, HelpText = "Single XML file to use for all DLL files, otherwise using 'file.xml' for each 'file.dll' (optional).")>]
    member val xmlFile = "" with get, set

    [<Option("nonPublic", Default=false, Required = false, HelpText = "The tool will also generate documentation for non-public members")>]
    member val nonPublic = false with get, set

    [<Option("xmlComments", Default=false, Required = false, HelpText = "Do not use the Markdown parser for in-code comments. Recommended for C# assemblies (optional, default true)")>]
    member val xmlComments = false with get, set

    [<Option("sourceRepo", Required = false, HelpText = "Source repository URL (optional).")>]
    member val sourceRepo = "" with get, set

    [<Option("sourceFolder", Required = false, HelpText = "Source repository folder; silently ignored, if source repository URL is not provided (optional).")>]
    member val sourceFolder = "" with get, set

    [<Option("libDirs", Required = false, HelpText = "Search directory list for library references.")>]
    member val libDirs = Seq.empty<string> with get, set

    member x.Execute() =
        let mutable res = 0
        try
            if x.help then
                printfn "%s" (x.GetUsageOfOption())
            else
                let template = evalString x.template
                let template =
                    match template with
                    | Some s -> Some s
                    | None ->
                    let t1 = Path.Combine("docs", "reference", "_template.html")
                    let t2 = Path.Combine("docs", "_template.html")
                    if File.Exists(t1) then
                        Some t1
                    elif File.Exists(t2) then
                        Some t2
                    else
                        printfn "note, expected template file '%s' or '%s' to exist, proceeding without template" t1 t2
                        None

                ApiDocs.GenerateHtml (
                    dllFiles = (x.dlls |> List.ofSeq),
                    outDir = x.output,
                    ?parameters = evalPairwiseStrings x.parameters,
                    ?template = template,
                    ?xmlFile = evalString x.xmlFile,
                    ?sourceRepo = evalString x.sourceRepo,
                    ?sourceFolder = evalString x.sourceFolder,
                    ?libDirs = evalStrings x.libDirs,
                    ?publicOnly = Some (not x.nonPublic),
                    ?markDownComments = Some (not x.xmlComments)
                    )
        with ex ->
            Log.errorf "received exception in ApiDocs.GenerateHtml:\n %A" ex
            printfn "Error on ApiDocs.GenerateHtml: \n%O" ex
            res <- -1
        waitForKey x.waitForKey
        res

