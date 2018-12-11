namespace FSharp.Formatting.Options.MetadataFormat

open CommandLine
open CommandLine.Text
open FSharp.MetadataFormat

open FSharp.Formatting.Common
open FSharp.Formatting.Options
open FSharp.Formatting.Options.Common
open FSharp.Formatting.IExecutable
open FSharp.Formatting.Razor


/// Exposes metadata formatting functionality. This corresponds
/// to invoking the following library function from MetadataFormat:
///
///    MetadataFormat.Generate
///      ( [dllFile], outDir, layoutRoots, ?parameters = parameters, ?namespaceTemplate = namespaceTemplate,
///        ?moduleTemplate = moduleTemplate, ?typeTemplate = typeTemplate, ?xmlFile = xmlFile)
type GenerateOptions() =

    //[<ParserState>]
    //member val LastParserState = null with get, set

    // does not work as desired in F#:
    // the HelpOption attribute is not built,
    // but receive a System.MemberAccessException
    //[<HelpOption>]
    /// autogenerated help text
    member x.GetUsageOfOption() =
        let help = new HelpText()
        help.AddDashesToOption <- true
        //help.AddOptions(x)
        "\nfsformatting metadataFormat --generate [options]" +
        "\n------------------------------------------------" +
        help.ToString()


    [<Option("help", Required = false,
        HelpText = "Display this message. All options are case-insensitive.")>]
    member val help = false with get, set

    [<Option("waitForKey", Required = false,
        HelpText = "Wait for key before exit.")>]
    member val waitForKey = false with get, set

    // all default string settings are done by FShap.Formatting,
    // non-string default options are supplied for type information

    [<Option("dllFiles", Required = true,
        HelpText = "DLL input file list.")>]
    member val dllFiles = [|""|] with get, set

    [<Option("outDir", Required = true,
        HelpText = "Ouput Directory.")>]
    member val outputDirectory = "" with get, set

    [<Option("layoutRoots", Required = true,
        HelpText = "Search directory list for the Razor Engine.")>]
    member val layoutRoots = [|""|] with get, set

    [<Option("parameters", Required = false,
        HelpText = "Property settings for the Razor Engine (optinal).")>]
    member val parameters = [|""|] with get, set

    [<Option("namespaceTemplate", Required = false,
        HelpText = "Namespace template file for formatting, defaults to 'namespaces.cshtml' (optional).")>]
    member val namespaceTemplate = "" with get, set

    [<Option("moduleTemplate", Required = false,
        HelpText = "Module template file for formatting, defaults to 'module.cshtml' (optional).")>]
    member val moduleTemplate = "" with get, set

    [<Option("typeTemplate", Required = false,
        HelpText = "Type template file for formatting, defaults to 'type.cshtml' (optional).")>]
    member val typeTemplate = "" with get, set

    [<Option("xmlFile", Required = false,
        HelpText = "Single XML file to use for all DLL files, otherwise using 'file.xml' for each 'file.dll' (optional).")>]
    member val xmlFile = "" with get, set

    [<Option("sourceRepo", Required = false,
        HelpText = "Source repository URL; silently ignored, if source repository folder is not provided (optional).")>]
    member val sourceRepo = "" with get, set

    [<Option("sourceFolder", Required = false,
        HelpText = "Source repository folder; silently ignored, if source repository URL is not provided (optional).")>]
    member val sourceFolder = "" with get, set

    [<Option("libDirs", Required = false,
        HelpText = "Search directory list for library references.")>]
    member val libDirs = [|""|] with get, set

    interface IExecutable with
        member x.Execute() =
            let mutable res = 0
            try
                if x.help then
                    printfn "%s" (x.GetUsageOfOption())
                else
                    RazorMetadataFormat.Generate (
                        dllFiles = (x.dllFiles |> List.ofArray),
                        outDir = x.outputDirectory,
                        layoutRoots = (x.layoutRoots |> List.ofArray),
                        ?parameters = (evalPairwiseStringArray x.parameters),
                        ?namespaceTemplate = (evalString x.namespaceTemplate),
                        ?moduleTemplate = (evalString x.moduleTemplate),
                        ?typeTemplate = (evalString x.typeTemplate),
                        ?xmlFile = (evalString x.xmlFile),
                        ?sourceRepo = (evalString x.sourceRepo),
                        ?sourceFolder = (evalString x.sourceFolder),
                        ?libDirs = (evalStringArray x.libDirs)
                        )
            with ex ->
                Log.errorf "received exception in RazorMetadataFormat.Generate:\n %A" ex
                printfn "Error on RazorMetadataFormat.Generate: \n%O" ex
                res <- -1
            waitForKey x.waitForKey
            res

        member x.GetErrorText() =
            //if x.LastParserState = null then ""
            //else
            //    let errors = (x.LastParserState :?> IParserState).Errors
            //    parsingErrorMessage(errors)
            "deprecated"

        member x.GetUsage() = x.GetUsageOfOption()
