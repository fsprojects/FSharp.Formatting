namespace FSharp.Formatting.DotLiquid

open System.Globalization
open System.IO
open DotLiquid
open FSharp.Formatting.Literate
open FSharp.Formatting.Common
open FSharp.Formatting.ApiDocs

[<AutoOpen>]
module Impl =
    let internal defaultOutput output input kind =
        match output, defaultArg kind OutputKind.Html with
        | Some out, _ -> out
        | _, OutputKind.Latex -> Path.ChangeExtension(input, "tex")
        | _, OutputKind.Html -> Path.ChangeExtension(input, "html")
        | _, OutputKind.Pynb -> Path.ChangeExtension(input, "ipynb")

    let internal generateLiterateFile contentTag parameters (templateOpt: string option) outputFile =
        match templateOpt with
        | Some file when file.EndsWith("cshtml", true, CultureInfo.InvariantCulture) ->
            failwith "Razor templating no longer supported by FSharp.Formatting, DotLiquid templating now used instead"
        | Some file when file.EndsWith("html", true, CultureInfo.InvariantCulture) || file.EndsWith("liquid", true, CultureInfo.InvariantCulture)->
          let props = dict [| for (k,v) in parameters -> (k, box v) |]
          let dirName = Path.GetDirectoryName(Path.GetFullPath(file))
          Template.FileSystem <- DotLiquid.FileSystems.LocalFileSystem(dirName)
          let template = Template.Parse(File.ReadAllText(file))
          let generatedText = template.Render(Hash.FromDictionary(props))
          File.WriteAllText(outputFile, generatedText)
        | _ ->
          Literate.GenerateFile(contentTag, parameters, templateOpt, outputFile)

    type Literate with
        static member ConvertDocumentWithDotLiquid
            (doc, output, ?templateFile, ?format, ?prefix, ?lineNumbers, ?includeSource, ?generateAnchors, ?replacements, ?assemblyReferences) =
              let res =
                Literate.GenerateReplacementsForDocument
                  (doc, output, ?format=format, ?prefix=prefix, ?lineNumbers=lineNumbers, ?includeSource=includeSource,
                   ?generateAnchors=generateAnchors, ?replacements=replacements)
              generateLiterateFile res.ContentTag res.Parameters templateFile output

        static member ConvertMarkdownWithDotLiquid
            (input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
              ?lineNumbers, ?references, ?replacements, ?includeSource, ?generateAnchors, ?customizeDocument ) =

              let res =
                Literate.GenerateReplacementsForMarkdown
                   (input ,?output=output, ?format=format, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
                    ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
                    ?replacements=replacements, ?customizeDocument=customizeDocument )
              generateLiterateFile res.ContentTag res.Parameters templateFile (defaultOutput output input format)

        static member ConvertScriptFileWithDotLiquid
            (input, ?templateFile, ?output, ?format, ?formatAgent, ?prefix, ?compilerOptions,
             ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource,
             ?generateAnchors, ?customizeDocument ) =
                let res =
                  Literate.GenerateReplacementsForScriptFile
                    (input ,?output=output, ?format=format, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
                     ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
                     ?replacements=replacements, ?customizeDocument=customizeDocument, ?fsiEvaluator=fsiEvaluator )
                generateLiterateFile res.ContentTag res.Parameters templateFile (defaultOutput output input format) 

        static member ConvertDirectoryWithDotLiquid
            (inputDirectory, ?templateFile, ?outputDirectory, ?format, ?formatAgent, ?prefix, ?compilerOptions,
              ?lineNumbers, ?references, ?fsiEvaluator, ?replacements, ?includeSource, ?generateAnchors,
              ?processRecursive, ?customizeDocument  ) =
                let outputDirectory = defaultArg outputDirectory inputDirectory

                let res =
                  Literate.GenerateReplacementsForDirectory
                    (inputDirectory, outputDirectory, ?format=format, ?formatAgent=formatAgent, ?prefix=prefix, ?compilerOptions=compilerOptions,
                     ?lineNumbers=lineNumbers, ?references=references, ?includeSource=includeSource, ?generateAnchors=generateAnchors,
                     ?replacements=replacements, ?customizeDocument=customizeDocument, ?processRecursive=processRecursive, ?fsiEvaluator=fsiEvaluator)

                for (path, res) in res do
                    generateLiterateFile res.ContentTag res.Parameters templateFile path

    type ApiDocs with 

        static member GenerateFromModelWithDotLiquid(model: ApiDocsModel, outDir, layoutRoots, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate,?assemblyReferences) =

        /// Generates documentation for multiple files specified by the `dllFiles` parameter
        ///  - `outDir` - specifies the output directory where documentation should be placed
        ///  - `layoutRoots` - a list of paths where DotLiquid templates can be found
        ///  - `parameters` - provides additional parameters to the DotLiquid templates
        ///  - `xmlFile` - can be used to override the default name of the XML file (by default, we assume
        ///     the file has the same name as the DLL)
        ///  - `markDownComments` - specifies if you want to use the Markdown parser for in-code comments.
        ///    With `markDownComments` enabled there is no support for `<see cref="">` links, so `false` is
        ///    recommended for C# assemblies (if not specified, `true` is used).
        ///  - `typeTemplate` - the templates to be used for normal types (and C# types)
        ///    (if not specified, `"type.html"` is used).
        ///  - `moduleTemplate` - the templates to be used for modules
        ///    (if not specified, `"module.html"` is used).
        ///  - `namespaceTemplate` - the templates to be used for namespaces
        ///    (if not specified, `"namespaces.html"` is used).
        ///  - `assemblyReferences` - The assemblies to use when compiling DotLiquid templates.
        ///    Use this parameter if templates fail to compile with `mcs` on Linux or Mac or
        ///    if you need additional references in your templates
        ///    (if not specified, we use the currently loaded assemblies).
        ///  - `sourceFolder` and `sourceRepo` - When specified, the documentation generator automatically
        ///    generates links to GitHub pages for each of the entity.
        ///  - `publicOnly` - When set to `false`, the tool will also generate documentation for non-public members
        ///  - `libDirs` - Use this to specify additional paths where referenced DLL files can be found
        ///  - `otherFlags` - Additional flags that are passed to the F# compiler (you can use this if you want to
        ///    specify references explicitly etc.)
        ///  - `urlRangeHighlight` - A function that can be used to override the default way of generating GitHub links
        static member GenerateWithDotLiquid(dllFiles : seq<string>, outDir, layoutRoots, ?parameters, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight, ?assemblyReferences) =
            let model = ApiDocs.GenerateModel(dllFiles, ?parameters=parameters, ?xmlFile=xmlFile, ?sourceRepo=sourceRepo, ?sourceFolder=sourceFolder, ?publicOnly=publicOnly, ?libDirs=libDirs, ?otherFlags=otherFlags, ?markDownComments=markDownComments, ?urlRangeHighlight=urlRangeHighlight)
            ApiDocs.GenerateFromModelWithDotLiquid(model, outDir, layoutRoots, ?namespaceTemplate=namespaceTemplate, ?moduleTemplate=moduleTemplate, ?typeTemplate=typeTemplate, ?assemblyReferences=assemblyReferences)

