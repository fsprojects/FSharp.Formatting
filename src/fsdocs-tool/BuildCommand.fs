namespace fsdocs

open CommandLine

open System
open System.Diagnostics
open System.IO
open System.Globalization
open System.Net
open System.Reflection
open System.Text

open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open fsdocs.Common
open FSharp.Formatting.Templating

open Suave
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Operators
open Suave.Filters
open FSharp.Formatting.Markdown

/// Convert markdown, script and other content into a static site
type internal DocContent
    (
        rootOutputFolderAsGiven,
        previous: Map<_, _>,
        lineNumbers,
        fsiEvaluator,
        substitutions,
        saveImages,
        watch,
        root,
        crefResolver,
        onError
    ) =

    let createImageSaver (rootOutputFolderAsGiven) =
        // Download images so that they can be embedded
        let wc = new WebClient()
        let mutable counter = 0

        fun (url: string) ->
            if url.StartsWith("http") || url.StartsWith("https") then
                counter <- counter + 1
                let ext = Path.GetExtension(url)

                let url2 = sprintf "savedimages/saved%d%s" counter ext

                let fn = sprintf "%s/%s" rootOutputFolderAsGiven url2

                ensureDirectory (sprintf "%s/savedimages" rootOutputFolderAsGiven)
                printfn "downloading %s --> %s" url fn
                wc.DownloadFile(url, fn)
                url2
            else
                url

    let getOutputFileNames (inputFileFullPath: string) (outputKind: OutputKind) outputFolderRelativeToRoot =
        let inputFileName = Path.GetFileName(inputFileFullPath)
        let isFsx = inputFileFullPath.EndsWith(".fsx", true, CultureInfo.InvariantCulture)
        let isMd = inputFileFullPath.EndsWith(".md", true, CultureInfo.InvariantCulture)
        let ext = outputKind.Extension

        let outputFileRelativeToRoot =
            if isFsx || isMd then
                let basename = Path.GetFileNameWithoutExtension(inputFileFullPath)

                Path.Combine(outputFolderRelativeToRoot, sprintf "%s.%s" basename ext)
            else
                Path.Combine(outputFolderRelativeToRoot, inputFileName)

        let outputFileFullPath = Path.GetFullPath(Path.Combine(rootOutputFolderAsGiven, outputFileRelativeToRoot))
        outputFileRelativeToRoot, outputFileFullPath

    // Check if a sub-folder is actually the output directory
    let subFolderIsOutput subInputFolderFullPath =
        let subFolderFullPath = Path.GetFullPath(subInputFolderFullPath)
        let rootOutputFolderFullPath = Path.GetFullPath(rootOutputFolderAsGiven)
        (subFolderFullPath = rootOutputFolderFullPath)

    let allCultures =
        System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.AllCultures)
        |> Array.map (fun x -> x.TwoLetterISOLanguageName)
        |> Array.filter (fun x -> x.Length = 2)
        |> Array.distinct

    let makeMarkdownLinkResolver
        (inputFolderAsGiven, outputFolderRelativeToRoot, fullPathFileMap: Map<(string * OutputKind), string>, outputKind)
        (markdownReference: string)
        =
        let markdownReferenceAsFullInputPathOpt =
            try
                Path.GetFullPath(markdownReference, inputFolderAsGiven) |> Some
            with
            | _ -> None

        match markdownReferenceAsFullInputPathOpt with
        | None -> None
        | Some markdownReferenceFullInputPath ->
            match fullPathFileMap.TryFind(markdownReferenceFullInputPath, outputKind) with
            | None -> None
            | Some markdownReferenceFullOutputPath ->
                try
                    let outputFolderFullPath =
                        Path.GetFullPath(Path.Combine(rootOutputFolderAsGiven, outputFolderRelativeToRoot))

                    let uri =
                        Uri(outputFolderFullPath + "/")
                            .MakeRelativeUri(Uri(markdownReferenceFullOutputPath))
                            .ToString()

                    Some uri
                with
                | _ ->
                    printfn
                        $"Couldn't map markdown reference {markdownReference} that seemed to correspond to an input file"

                    None

    /// Prepare the map of input file to output file. This map is used to make substitutions through markdown
    /// source such A.md --> A.html or A.fsx --> A.html.  The substitutions depend on the output kind.
    let prepFile (inputFileFullPath: string) (outputKind: OutputKind) outputFolderRelativeToRoot =
        [ let inputFileName = Path.GetFileName(inputFileFullPath)

          if
              not (inputFileName.StartsWith("."))
              && not (inputFileName.StartsWith "_template")
          then
              let inputFileFullPath = Path.GetFullPath(inputFileFullPath)

              let _relativeOutputFile, outputFileFullPath =
                  getOutputFileNames inputFileFullPath outputKind outputFolderRelativeToRoot

              yield ((inputFileFullPath, outputKind), outputFileFullPath) ]

    /// Likewise prepare the map of input files to output files
    let rec prepFolder (inputFolderAsGiven: string) outputFolderRelativeToRoot =
        [ let inputs = Directory.GetFiles(inputFolderAsGiven, "*")

          for input in inputs do
              yield! prepFile input OutputKind.Html outputFolderRelativeToRoot
              yield! prepFile input OutputKind.Latex outputFolderRelativeToRoot
              yield! prepFile input OutputKind.Pynb outputFolderRelativeToRoot
              yield! prepFile input OutputKind.Fsx outputFolderRelativeToRoot
              yield! prepFile input OutputKind.Markdown outputFolderRelativeToRoot

          for subInputFolderFullPath in Directory.EnumerateDirectories(inputFolderAsGiven) do
              let subInputFolderName = Path.GetFileName(subInputFolderFullPath)
              let subFolderIsSkipped = subInputFolderName.StartsWith "."
              let subFolderIsOutput = subFolderIsOutput subInputFolderFullPath

              if not subFolderIsOutput && not subFolderIsSkipped then
                  yield!
                      prepFolder
                          (Path.Combine(inputFolderAsGiven, subInputFolderName))
                          (Path.Combine(outputFolderRelativeToRoot, subInputFolderName)) ]

    let processFile
        rootInputFolder
        (isOtherLang: bool)
        (inputFileFullPath: string)
        outputKind
        template
        outputFolderRelativeToRoot
        imageSaver
        mdlinkResolver
        =
        [ let name = Path.GetFileName(inputFileFullPath)

          if name.StartsWith(".") then
              printfn "skipping file %s" inputFileFullPath
          elif not (name.StartsWith "_template") then
              let isFsx = inputFileFullPath.EndsWith(".fsx", true, CultureInfo.InvariantCulture)

              let isMd = inputFileFullPath.EndsWith(".md", true, CultureInfo.InvariantCulture)

              // A _template.tex or _template.pynb is needed to generate those files
              match outputKind, template with
              | OutputKind.Pynb, None -> ()
              | OutputKind.Latex, None -> ()
              | OutputKind.Fsx, None -> ()
              | OutputKind.Markdown, None -> ()
              | _ ->

                  let imageSaverOpt =
                      match outputKind with
                      | OutputKind.Pynb when saveImages <> Some false -> Some imageSaver
                      | OutputKind.Latex when saveImages <> Some false -> Some imageSaver
                      | OutputKind.Fsx when saveImages = Some true -> Some imageSaver
                      | OutputKind.Html when saveImages = Some true -> Some imageSaver
                      | OutputKind.Markdown when saveImages = Some true -> Some imageSaver
                      | _ -> None

                  let outputFileRelativeToRoot, outputFileFullPath =
                      getOutputFileNames inputFileFullPath outputKind outputFolderRelativeToRoot

                  // Update only when needed - template or file or tool has changed

                  let changed =
                      let fileChangeTime =
                          try
                              File.GetLastWriteTime(inputFileFullPath)
                          with
                          | _ -> DateTime.MaxValue

                      let templateChangeTime =
                          match template with
                          | Some t when isFsx || isMd ->
                              try
                                  File.GetLastWriteTime(t)
                              with
                              | _ -> DateTime.MaxValue
                          | _ -> DateTime.MinValue

                      let toolChangeTime =
                          try
                              File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location)
                          with
                          | _ -> DateTime.MaxValue

                      let changeTime = fileChangeTime |> max templateChangeTime |> max toolChangeTime

                      let generateTime =
                          try
                              File.GetLastWriteTime(outputFileFullPath)
                          with
                          | _ -> System.DateTime.MinValue

                      changeTime > generateTime

                  // If it's changed or we don't know anything about it
                  // we have to compute the model to get the global substitutions right
                  let mainRun = (outputKind = OutputKind.Html)
                  let haveModel = previous.TryFind inputFileFullPath

                  if changed || (watch && mainRun && haveModel.IsNone) then
                      if isFsx then
                          printfn "  generating model for %s --> %s" inputFileFullPath outputFileRelativeToRoot

                          let model =
                              Literate.ParseAndTransformScriptFile(
                                  inputFileFullPath,
                                  output = outputFileRelativeToRoot,
                                  outputKind = outputKind,
                                  prefix = None,
                                  fscOptions = None,
                                  lineNumbers = lineNumbers,
                                  references = Some false,
                                  fsiEvaluator = fsiEvaluator,
                                  substitutions = substitutions,
                                  generateAnchors = Some true,
                                  imageSaver = imageSaverOpt,
                                  rootInputFolder = rootInputFolder,
                                  crefResolver = crefResolver,
                                  mdlinkResolver = mdlinkResolver,
                                  onError = Some onError
                              )

                          yield
                              ((if mainRun then
                                    Some(inputFileFullPath, isOtherLang, model)
                                else
                                    None),
                               (fun p ->
                                   printfn "  writing %s --> %s" inputFileFullPath outputFileRelativeToRoot
                                   ensureDirectory (Path.GetDirectoryName(outputFileFullPath))

                                   SimpleTemplating.UseFileAsSimpleTemplate(
                                       p @ model.Substitutions,
                                       template,
                                       outputFileFullPath
                                   )))

                      elif isMd then
                          printfn "  preparing %s --> %s" inputFileFullPath outputFileRelativeToRoot

                          let model =
                              Literate.ParseAndTransformMarkdownFile(
                                  inputFileFullPath,
                                  output = outputFileRelativeToRoot,
                                  outputKind = outputKind,
                                  prefix = None,
                                  fscOptions = None,
                                  lineNumbers = lineNumbers,
                                  references = Some false,
                                  substitutions = substitutions,
                                  generateAnchors = Some true,
                                  imageSaver = imageSaverOpt,
                                  rootInputFolder = rootInputFolder,
                                  crefResolver = crefResolver,
                                  mdlinkResolver = mdlinkResolver,
                                  parseOptions = MarkdownParseOptions.AllowYamlFrontMatter,
                                  onError = Some onError
                              )

                          yield
                              ((if mainRun then
                                    Some(inputFileFullPath, isOtherLang, model)
                                else
                                    None),
                               (fun p ->
                                   printfn "  writing %s --> %s" inputFileFullPath outputFileRelativeToRoot
                                   ensureDirectory (Path.GetDirectoryName(outputFileFullPath))

                                   SimpleTemplating.UseFileAsSimpleTemplate(
                                       p @ model.Substitutions,
                                       template,
                                       outputFileFullPath
                                   )))

                      else if mainRun then
                          yield
                              (None,
                               (fun _p ->
                                   printfn "  copying %s --> %s" inputFileFullPath outputFileRelativeToRoot
                                   ensureDirectory (Path.GetDirectoryName(outputFileFullPath))
                                   // check the file still exists for the incremental case
                                   if (File.Exists inputFileFullPath) then
                                       // ignore errors in watch mode
                                       try
                                           File.Copy(inputFileFullPath, outputFileFullPath, true)
                                           File.SetLastWriteTime(outputFileFullPath, DateTime.Now)
                                       with
                                       | _ when watch -> ()))
                  else if mainRun && watch then
                      //printfn "skipping unchanged file %s" inputFileFullPath
                      yield (Some(inputFileFullPath, isOtherLang, haveModel.Value), (fun _ -> ())) ]

    let rec processFolder
        (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate, mdTemplate, isOtherLang, rootInputFolder, fullPathFileMap)
        (inputFolderAsGiven: string)
        outputFolderRelativeToRoot
        =
        [
          // Look for the presence of the _template.* files to activate the
          // generation of the content.
          let indirName = Path.GetFileName(inputFolderAsGiven).ToLower()

          // Two-letter directory names (e.g. 'ja') with 'docs' count as multi-language and are suppressed from table-of-content
          // generation and site search index
          let isOtherLang = isOtherLang || (indirName.Length = 2 && allCultures |> Array.contains indirName)

          let possibleNewHtmlTemplate = Path.Combine(inputFolderAsGiven, "_template.html")

          let htmlTemplate =
              if File.Exists(possibleNewHtmlTemplate) then
                  Some possibleNewHtmlTemplate
              else
                  htmlTemplate

          let possibleNewPynbTemplate = Path.Combine(inputFolderAsGiven, "_template.ipynb")

          let pynbTemplate =
              if File.Exists(possibleNewPynbTemplate) then
                  Some possibleNewPynbTemplate
              else
                  pynbTemplate

          let possibleNewFsxTemplate = Path.Combine(inputFolderAsGiven, "_template.fsx")

          let fsxTemplate =
              if File.Exists(possibleNewFsxTemplate) then
                  Some possibleNewFsxTemplate
              else
                  fsxTemplate

          let possibleNewMdTemplate = Path.Combine(inputFolderAsGiven, "_template.md")

          let mdTemplate =
              if File.Exists(possibleNewMdTemplate) then
                  Some possibleNewMdTemplate
              else
                  mdTemplate

          let possibleNewLatexTemplate = Path.Combine(inputFolderAsGiven, "_template.tex")

          let texTemplate =
              if File.Exists(possibleNewLatexTemplate) then
                  Some possibleNewLatexTemplate
              else
                  texTemplate

          ensureDirectory (Path.Combine(rootOutputFolderAsGiven, outputFolderRelativeToRoot))

          let inputs = Directory.GetFiles(inputFolderAsGiven, "*")

          let imageSaver = createImageSaver (Path.Combine(rootOutputFolderAsGiven, outputFolderRelativeToRoot))

          // Look for the four different kinds of content
          for input in inputs do
              yield!
                  processFile
                      rootInputFolder
                      isOtherLang
                      input
                      OutputKind.Html
                      htmlTemplate
                      outputFolderRelativeToRoot
                      imageSaver
                      (makeMarkdownLinkResolver (
                          inputFolderAsGiven,
                          outputFolderRelativeToRoot,
                          fullPathFileMap,
                          OutputKind.Html
                      ))

              yield!
                  processFile
                      rootInputFolder
                      isOtherLang
                      input
                      OutputKind.Latex
                      texTemplate
                      outputFolderRelativeToRoot
                      imageSaver
                      (makeMarkdownLinkResolver (
                          inputFolderAsGiven,
                          outputFolderRelativeToRoot,
                          fullPathFileMap,
                          OutputKind.Latex
                      ))

              yield!
                  processFile
                      rootInputFolder
                      isOtherLang
                      input
                      OutputKind.Pynb
                      pynbTemplate
                      outputFolderRelativeToRoot
                      imageSaver
                      (makeMarkdownLinkResolver (
                          inputFolderAsGiven,
                          outputFolderRelativeToRoot,
                          fullPathFileMap,
                          OutputKind.Pynb
                      ))

              yield!
                  processFile
                      rootInputFolder
                      isOtherLang
                      input
                      OutputKind.Fsx
                      fsxTemplate
                      outputFolderRelativeToRoot
                      imageSaver
                      (makeMarkdownLinkResolver (
                          inputFolderAsGiven,
                          outputFolderRelativeToRoot,
                          fullPathFileMap,
                          OutputKind.Fsx
                      ))

              yield!
                  processFile
                      rootInputFolder
                      isOtherLang
                      input
                      OutputKind.Markdown
                      mdTemplate
                      outputFolderRelativeToRoot
                      imageSaver
                      (makeMarkdownLinkResolver (
                          inputFolderAsGiven,
                          outputFolderRelativeToRoot,
                          fullPathFileMap,
                          OutputKind.Markdown
                      ))

          for subInputFolderFullPath in Directory.EnumerateDirectories(inputFolderAsGiven) do
              let subInputFolderName = Path.GetFileName(subInputFolderFullPath)
              let subFolderIsSkipped = subInputFolderName.StartsWith "."
              let subFolderIsOutput = subFolderIsOutput subInputFolderFullPath

              if subFolderIsOutput || subFolderIsSkipped then

                  printfn "  skipping directory %s" subInputFolderFullPath
              else
                  yield!
                      processFolder
                          (htmlTemplate,
                           texTemplate,
                           pynbTemplate,
                           fsxTemplate,
                           mdTemplate,
                           isOtherLang,
                           rootInputFolder,
                           fullPathFileMap)
                          (Path.Combine(inputFolderAsGiven, subInputFolderName))
                          (Path.Combine(outputFolderRelativeToRoot, subInputFolderName)) ]

    member _.Convert(rootInputFolderAsGiven, htmlTemplate, extraInputs) =

        let inputDirectories = extraInputs @ [ (rootInputFolderAsGiven, ".") ]

        // Maps full input paths to full output paths
        let fullPathFileMap =
            [ for (rootInputFolderAsGiven, outputFolderRelativeToRoot) in inputDirectories do
                  yield! prepFolder rootInputFolderAsGiven outputFolderRelativeToRoot ]
            |> Map.ofList

        [ for (rootInputFolderAsGiven, outputFolderRelativeToRoot) in inputDirectories do
              yield!
                  processFolder
                      (htmlTemplate, None, None, None, None, false, Some rootInputFolderAsGiven, fullPathFileMap)
                      rootInputFolderAsGiven
                      outputFolderRelativeToRoot ]

    member _.GetSearchIndexEntries(docModels: (string * bool * LiterateDocModel) list) =
        [| for (_inputFile, isOtherLang, model) in docModels do
               if not isOtherLang then
                   match model.IndexText with
                   | Some text ->
                       { title = model.Title
                         content = text
                         uri = model.Uri(root) }
                   | _ -> () |]

    member _.GetNavigationEntries(docModels: (string * bool * LiterateDocModel) list) =
        let modelsForList =
            [ for thing in docModels do
                  match thing with
                  | (inputFileFullPath, isOtherLang, model) when
                      not isOtherLang
                      && model.OutputKind = OutputKind.Html
                      && not (Path.GetFileNameWithoutExtension(inputFileFullPath) = "index")
                      ->
                      model
                  | _ -> () ]

        let modelsByCategory =
            modelsForList
            |> List.groupBy (fun model -> model.Category)
            |> List.sortBy (fun (_, ms) ->
                match ms.[0].CategoryIndex with
                | Some s ->
                    (try
                        int32 s
                     with
                     | _ -> Int32.MaxValue)
                | None -> Int32.MaxValue)

        [
          // No categories specified
          if modelsByCategory.Length = 1 && (fst modelsByCategory.[0]) = None then
              li [ Class "nav-header" ] [
                  !! "Documentation"
              ]

              for model in snd modelsByCategory.[0] do
                  let link = model.Uri(root)

                  li [ Class "nav-item" ] [
                      a [ Class "nav-link"; (Href link) ] [
                          encode model.Title
                      ]
                  ]
          else
              // At least one category has been specified. Sort each category by index and emit
              // Use 'Other' as a header for uncategorised things
              for (cat, modelsInCategory) in modelsByCategory do
                  let modelsInCategory =
                      modelsInCategory
                      |> List.sortBy (fun model ->
                          match model.Index with
                          | Some s ->
                              (try
                                  int32 s
                               with
                               | _ -> Int32.MaxValue)
                          | None -> Int32.MaxValue)

                  match cat with
                  | Some c -> li [ Class "nav-header" ] [ !!c ]
                  | None -> li [ Class "nav-header" ] [ !! "Other" ]

                  for model in modelsInCategory do
                      let link = model.Uri(root)

                      li [ Class "nav-item" ] [
                          a [ Class "nav-link"; (Href link) ] [
                              encode model.Title
                          ]
                      ] ]
        |> List.map (fun html -> html.ToString())
        |> String.concat "             \n"

/// Processes and runs Suave server to host them on localhost
module Serve =
    //not sure what this was needed for
    //let refreshEvent = new Event<_>()

    /// generate the script to inject into html to enable hot reload during development
    let generateWatchScript (port: int) =
        let tag =
            """
<script type="text/javascript">
    var wsUri = "ws://localhost:{{PORT}}/websocket";
    function init()
    {
        websocket = new WebSocket(wsUri);
        websocket.onclose = function(evt) { onClose(evt) };
    }
    function onClose(evt)
    {
        console.log('closing');
        websocket.close();
        document.location.reload();
    }
    window.addEventListener("load", init, false);
</script>
"""

        tag.Replace("{{PORT}}", string port)



    let signalHotReload = new System.Threading.ManualResetEvent(false)

    let socketHandler (webSocket: WebSocket) _ =
        socket {
            signalHotReload.WaitOne() |> ignore
            signalHotReload.Reset() |> ignore
            let emptyResponse = [||] |> ByteSegment
            printfn "Triggering hot reload on the client"
            do! webSocket.send Close emptyResponse true
        }

    let startWebServer rootOutputFolderAsGiven localPort =
        let defaultBinding = defaultConfig.bindings.[0]

        let withPort = { defaultBinding.socketBinding with port = uint16 localPort }

        let serverConfig =
            { defaultConfig with
                bindings = [ { defaultBinding with socketBinding = withPort } ]
                homeFolder = Some rootOutputFolderAsGiven }

        let app =
            choose [ path "/" >=> Redirection.redirect "/index.html"
                     path "/websocket" >=> handShake socketHandler
                     Writers.setHeader "Cache-Control" "no-cache, no-store, must-revalidate"
                     >=> Writers.setHeader "Pragma" "no-cache"
                     >=> Writers.setHeader "Expires" "0"
                     >=> Files.browseHome ]

        startWebServerAsync serverConfig app |> snd |> Async.Start

type CoreBuildOptions(watch) =

    [<Option("input", Required = false, Default = "docs", HelpText = "Input directory of documentation content.")>]
    member val input = "" with get, set

    [<Option("projects",
             Required = false,
             HelpText = "Project files to build API docs for outputs, defaults to all packable projects.")>]
    member val projects = Seq.empty<string> with get, set

    [<Option("output",
             Required = false,
             HelpText = "Output Folder (default 'output' for 'build' and 'tmp/watch' for 'watch'.")>]
    member val output = "" with get, set

    [<Option("noapidocs", Default = false, Required = false, HelpText = "Disable generation of API docs.")>]
    member val noapidocs = false with get, set

    [<Option("ignoreprojects", Default = false, Required = false, HelpText = "Disable project cracking.")>]
    member val ignoreprojects = false with get, set

    [<Option("strict", Default = false, Required = false, HelpText = "Fail if there is a problem generating docs.")>]
    member val strict = false with get, set

    [<Option("eval", Default = false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = false with get, set

    [<Option("qualify",
             Default = false,
             Required = false,
             HelpText = "In API doc generation qualify the output by the collection name, e.g. 'reference/FSharp.Core/...' instead of 'reference/...' .")>]
    member val qualify = false with get, set

    [<Option("saveimages",
             Default = "none",
             Required = false,
             HelpText = "Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs.")>]
    member val saveImages = "none" with get, set

    [<Option("sourcefolder",
             Required = false,
             HelpText = "Source folder at time of component build (defaults to value of `<FsDocsSourceFolder>` from project file, else current directory)")>]
    member val sourceFolder = "" with get, set

    [<Option("sourcerepo",
             Required = false,
             HelpText = "Source repository for github links (defaults to value of `<FsDocsSourceRepository>` from project file, else `<RepositoryUrl>/tree/<RepositoryBranch>` for Git repositories)")>]
    member val sourceRepo = "" with get, set

    [<Option("linenumbers", Default = false, Required = false, HelpText = "Add line numbers.")>]
    member val linenumbers = false with get, set

    [<Option("nonpublic",
             Default = false,
             Required = false,
             HelpText = "The tool will also generate documentation for non-public members")>]
    member val nonpublic = false with get, set

    [<Option("mdcomments",
             Default = false,
             Required = false,
             HelpText = "Assume /// comments in F# code are markdown style (defaults to value of `<UsesMarkdownComments>` from project file)")>]
    member val mdcomments = false with get, set

    [<Option("parameters",
             Required = false,
             HelpText = "Additional substitution substitutions for templates, e.g. --parameters key1 value1 key2 value2")>]
    member val parameters = Seq.empty<string> with get, set

    [<Option("nodefaultcontent",
             Required = false,
             HelpText = "Do not copy default content styles, javascript or use default templates.")>]
    member val nodefaultcontent = false with get, set

    [<Option("properties",
             Required = false,
             HelpText = "Provide properties to dotnet msbuild, e.g. --properties Configuration=Release Version=3.4")>]
    member val extraMsbuildProperties = Seq.empty<string> with get, set

    [<Option("fscoptions",
             Required = false,
             HelpText = "Extra flags for F# compiler analysis, e.g. dependency resolution.")>]
    member val fscoptions = Seq.empty<string> with get, set

    [<Option("clean", Required = false, Default = false, HelpText = "Clean the output directory.")>]
    member val clean = false with get, set

    member this.Execute() =

        let onError msg =
            if this.strict then
                printfn "%s" msg
                exit 1

        let protect phase f =
            try
                f ()
                true
            with
            | ex ->
                printfn "Error : \n%O" ex

                onError (sprintf "%s failed, and --strict is on : \n%O" phase ex)
                false

        /// The substitutions as given by the user
        let userParameters =
            let parameters = Array.ofSeq this.parameters

            if parameters.Length % 2 = 1 then
                printfn "The --parameters option's arguments' count has to be an even number"
                exit 1

            evalPairwiseStringsNoOption parameters
            |> List.map (fun (a, b) -> (ParamKey a, b))

        let userParametersDict = readOnlyDict userParameters

        // Adjust the user substitutions for 'watch' mode root
        let userRoot, userParameters =
            if watch then
                let userRoot = sprintf "http://localhost:%d/" this.port_option

                if userParametersDict.ContainsKey(ParamKeys.root) then
                    printfn "ignoring user-specified root since in watch mode, root = %s" userRoot

                let userParameters =
                    [ ParamKeys.root, userRoot ]
                    @ (userParameters |> List.filter (fun (a, _) -> a <> ParamKeys.root))

                Some userRoot, userParameters
            else
                let r =
                    match userParametersDict.TryGetValue(ParamKeys.root) with
                    | true, v -> Some v
                    | _ -> None

                r, userParameters

        let userCollectionName =
            match (dict userParameters)
                      .TryGetValue(ParamKeys.``fsdocs-collection-name``)
                with
            | true, v -> Some v
            | _ -> None

        // See https://github.com/ionide/proj-info/issues/123
        let prevDotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")

        let (root, collectionName, crackedProjects, paths, docsSubstitutions), _key =
            let projects = Seq.toList this.projects
            let cacheFile = ".fsdocs/cache"

            let getTime p =
                try
                    File.GetLastWriteTimeUtc(p)
                with
                | _ -> DateTime.Now

            let key1 =
                (userRoot,
                 this.parameters,
                 projects,
                 getTime (typeof<CoreBuildOptions>.Assembly.Location),
                 (projects |> List.map getTime |> List.toArray))

            Utils.cacheBinary
                cacheFile
                (fun (_, key2) -> key1 = key2)
                (fun () ->
                    let props =
                        this.extraMsbuildProperties
                        |> Seq.toList
                        |> List.map (fun s ->
                            let arr = s.Split("=")

                            if arr.Length > 1 then
                                arr.[0], String.concat "=" arr.[1..]
                            else
                                failwith "properties must be of the form 'PropName=PropValue'")

                    Crack.crackProjects (
                        onError,
                        props,
                        userRoot,
                        userCollectionName,
                        userParameters,
                        projects,
                        this.ignoreprojects
                    ),
                    key1)

        // See https://github.com/ionide/proj-info/issues/123
        System.Environment.SetEnvironmentVariable("DOTNET_HOST_PATH", prevDotnetHostPath)

        if crackedProjects.Length > 0 then
            printfn ""
            printfn "Inputs for API Docs:"

            for (dllFile, _, _, _, _, _, _, _, _, _) in crackedProjects do
                printfn "    %s" dllFile

        //printfn "Comand lines for API Docs:"
        //for (_, runArguments, _, _, _, _, _, _, _, _) in crackedProjects do
        //    printfn "    %O" runArguments

        for (dllFile, _, _, _, _, _, _, _, _, _) in crackedProjects do
            if not (File.Exists dllFile) then
                let msg =
                    sprintf
                        "*** %s does not exist, has it been built? You may need to provide --properties Configuration=Release."
                        dllFile

                if this.strict then
                    failwith msg
                else
                    printfn "%s" msg

        if crackedProjects.Length > 0 then
            printfn ""
            printfn "Substitutions/parameters:"
            // Print the substitutions
            for (ParamKey pk, p) in docsSubstitutions do
                printfn "  %s --> %s" pk p

            // The substitutions may differ for some projects due to different settings in the project files, if so show that
            let pd = dict docsSubstitutions

            for (dllFile, _, _, _, _, _, _, _, _, projectParameters) in crackedProjects do
                for (((ParamKey pkv2) as pk2), p2) in projectParameters do
                    if pd.ContainsKey pk2 && pd.[pk2] <> p2 then
                        printfn "  (%s) %s --> %s" (Path.GetFileNameWithoutExtension(dllFile)) pkv2 p2

        let apiDocInputs =
            [ for (dllFile,
                   _,
                   repoUrlOption,
                   repoBranchOption,
                   repoTypeOption,
                   projectMarkdownComments,
                   projectWarn,
                   projectSourceFolder,
                   projectSourceRepo,
                   projectParameters) in crackedProjects ->
                  let sourceRepo =
                      match projectSourceRepo with
                      | Some s -> Some s
                      | None ->
                          match evalString this.sourceRepo with
                          | Some v -> Some v
                          | None ->
                              //printfn "repoBranchOption = %A" repoBranchOption
                              match repoUrlOption, repoBranchOption, repoTypeOption with
                              | Some url, Some branch, Some "git" when not (String.IsNullOrWhiteSpace branch) ->
                                  url + "/" + "tree/" + branch |> Some
                              | Some url, _, Some "git" -> url + "/" + "tree/" + "master" |> Some
                              | Some url, _, None -> Some url
                              | _ -> None

                  let sourceFolder =
                      match projectSourceFolder with
                      | Some s -> s
                      | None ->
                          match evalString this.sourceFolder with
                          | None -> Environment.CurrentDirectory
                          | Some v -> v

                  //printfn "sourceFolder = '%s'" sourceFolder
                  //printfn "sourceRepo = '%A'" sourceRepo
                  { Path = dllFile
                    XmlFile = None
                    SourceRepo = sourceRepo
                    SourceFolder = Some sourceFolder
                    Substitutions = Some projectParameters
                    MarkdownComments = this.mdcomments || projectMarkdownComments
                    Warn = projectWarn
                    PublicOnly = not this.nonpublic } ]

        // Compute the merge of all referenced DLLs across all projects
        // so they can be resolved during API doc generation.
        //
        // TODO: This is inaccurate: the different projects might not be referencing the same DLLs.
        // We should do doc generation for each output of each proejct separately
        let apiDocOtherFlags =
            [ for (_dllFile, otherFlags, _, _, _, _, _, _, _, _) in crackedProjects do
                  for otherFlag in otherFlags do
                      if otherFlag.StartsWith("-r:") then
                          if File.Exists(otherFlag.[3..]) then
                              yield otherFlag
                          else
                              printfn "NOTE: the reference '%s' was not seen on disk, ignoring" otherFlag ]
            // TODO: This 'distinctBy' is merging references that may be inconsistent across the project set
            |> List.distinctBy (fun ref -> Path.GetFileName(ref.[3..]))

        let rootOutputFolderAsGiven =
            if this.output = "" then
                if watch then "tmp/watch" else "output"
            else
                this.output

        // This is in-package
        //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net5.0\any
        //   to .nuget\packages\fsdocs-tool\7.1.7\templates
        let dir = Path.GetDirectoryName(typeof<CoreBuildOptions>.Assembly.Location)

        let defaultTemplateAttempt1 =
            Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "templates", "_template.html"))
        // This is in-repo only
        let defaultTemplateAttempt2 =
            Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "_template.html"))

        let defaultTemplate =
            if this.nodefaultcontent then
                None
            else if (try
                         File.Exists(defaultTemplateAttempt1)
                     with
                     | _ -> false) then
                Some defaultTemplateAttempt1
            elif (try
                      File.Exists(defaultTemplateAttempt2)
                  with
                  | _ -> false) then
                Some defaultTemplateAttempt2
            else
                None

        let extraInputs =
            [ if not this.nodefaultcontent then
                  // The "extras" content goes in "."
                  //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net5.0\any
                  //   to .nuget\packages\fsdocs-tool\7.1.7\extras
                  let attempt1 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "extras"))

                  if (try
                          Directory.Exists(attempt1)
                      with
                      | _ -> false) then
                      printfn "using extra content from %s" attempt1
                      (attempt1, ".")
                  else
                      // This is for in-repo use only, assuming we are executing directly from
                      //   src\fsdocs-tool\bin\Debug\net5.0\fsdocs.exe
                      //   src\fsdocs-tool\bin\Release\net5.0\fsdocs.exe
                      let attempt2 =
                          Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "content"))

                      if (try
                              Directory.Exists(attempt2)
                          with
                          | _ -> false) then
                          printfn "using extra content from %s" attempt2
                          (attempt2, "content")
                      else
                          printfn "no extra content found at %s or %s" attempt1 attempt2 ]

        // The incremental state (as well as the files written to disk)
        let mutable latestApiDocModel = None
        let mutable latestApiDocGlobalParameters = []
        let mutable latestApiDocCodeReferenceResolver = (fun _ -> None)
        let mutable latestApiDocPhase2 = (fun _ -> ())
        let mutable latestApiDocSearchIndexEntries = [||]
        let mutable latestDocContentPhase2 = (fun _ -> ())
        let mutable latestDocContentResults = Map.empty
        let mutable latestDocContentSearchIndexEntries = [||]
        let mutable latestDocContentGlobalParameters = []

        // Actions to read out the incremental state
        let getLatestGlobalParameters () =
            latestApiDocGlobalParameters @ latestDocContentGlobalParameters

        let regenerateSearchIndex () =
            let index = Array.append latestApiDocSearchIndexEntries latestDocContentSearchIndexEntries

            let indxTxt = System.Text.Json.JsonSerializer.Serialize index

            File.WriteAllText(Path.Combine(rootOutputFolderAsGiven, "index.json"), indxTxt)

        /// get the hot reload script if running in watch mode
        let getLatestWatchScript () =
            if watch then
                // if running in watch mode, inject hot reload script
                [ ParamKeys.``fsdocs-watch-script``, Serve.generateWatchScript this.port_option ]
            else
                // otherwise, inject empty replacement string
                [ ParamKeys.``fsdocs-watch-script``, "" ]

        // Incrementally generate API docs (regenerates all api docs, in two phases)
        let runGeneratePhase1 () =
            protect "API doc generation (phase 1)" (fun () ->
                if crackedProjects.Length > 0 then

                    if not this.noapidocs then

                        let (outputKind, initialTemplate2) =
                            let templates =
                                [ OutputKind.Html, Path.Combine(this.input, "reference", "_template.html")
                                  OutputKind.Html, Path.Combine(this.input, "_template.html")
                                  OutputKind.Markdown, Path.Combine(this.input, "reference", "_template.md")
                                  OutputKind.Markdown, Path.Combine(this.input, "_template.md") ]

                            match templates |> Seq.tryFind (fun (_, path) -> path |> File.Exists) with
                            | Some (kind, path) -> kind, Some path
                            | None ->
                                let templateFiles = templates |> Seq.map snd |> String.concat "', '"

                                match defaultTemplate with
                                | Some d ->
                                    printfn
                                        "note, no template files: '%s' found, using default template %s"
                                        templateFiles
                                        d

                                    OutputKind.Html, Some d
                                | None ->
                                    printfn
                                        "note, no template file '%s' found, and no default template at '%s'"
                                        templateFiles
                                        defaultTemplateAttempt1

                                    OutputKind.Html, None

                        printfn ""
                        printfn "API docs:"
                        printfn "  generating model for %d assemblies in API docs..." apiDocInputs.Length

                        let model, globals, index, phase2 =
                            match outputKind with
                            | OutputKind.Html ->
                                ApiDocs.GenerateHtmlPhased(
                                    inputs = apiDocInputs,
                                    output = rootOutputFolderAsGiven,
                                    collectionName = collectionName,
                                    substitutions = docsSubstitutions,
                                    qualify = this.qualify,
                                    ?template = initialTemplate2,
                                    otherFlags = apiDocOtherFlags @ Seq.toList this.fscoptions,
                                    root = root,
                                    libDirs = paths,
                                    onError = onError
                                )
                            | OutputKind.Markdown ->
                                ApiDocs.GenerateMarkdownPhased(
                                    inputs = apiDocInputs,
                                    output = rootOutputFolderAsGiven,
                                    collectionName = collectionName,
                                    substitutions = docsSubstitutions,
                                    qualify = this.qualify,
                                    ?template = initialTemplate2,
                                    otherFlags = apiDocOtherFlags @ Seq.toList this.fscoptions,
                                    root = root,
                                    libDirs = paths,
                                    onError = onError
                                )
                            | _ -> failwithf "API Docs format '%A' is not supported" outputKind

                        // Used to resolve code references in content with respect to the API Docs model
                        let resolveInlineCodeReference (s: string) =
                            if s.StartsWith("cref:") then
                                let s = s.[5..]

                                match model.Resolver.ResolveCref s with
                                | None -> None
                                | Some cref -> Some(cref.NiceName, cref.ReferenceLink)
                            else
                                None

                        latestApiDocModel <- Some model
                        latestApiDocCodeReferenceResolver <- resolveInlineCodeReference
                        latestApiDocSearchIndexEntries <- index
                        latestApiDocGlobalParameters <- globals
                        latestApiDocPhase2 <- phase2)

        let runGeneratePhase2 () =
            protect "API doc generation (phase 2)" (fun () ->
                printfn ""
                printfn "Write API Docs:"

                let globals = getLatestWatchScript () @ getLatestGlobalParameters ()

                latestApiDocPhase2 globals
                regenerateSearchIndex ())

        // Incrementally convert content
        let runDocContentPhase1 () =
            protect "Content generation (phase 1)" (fun () ->
                //printfn "projectInfos = %A" projectInfos

                printfn ""
                printfn "Content:"

                let saveImages =
                    (match this.saveImages with
                     | "some" -> None
                     | "none" -> Some false
                     | "all" -> Some true
                     | _ -> None)

                let fsiEvaluator =
                    (if this.eval then
                         Some(FsiEvaluator(onError = onError) :> IFsiEvaluator)
                     else
                         None)

                let docContent =
                    DocContent(
                        rootOutputFolderAsGiven,
                        latestDocContentResults,
                        Some this.linenumbers,
                        fsiEvaluator,
                        docsSubstitutions,
                        saveImages,
                        watch,
                        root,
                        latestApiDocCodeReferenceResolver,
                        onError
                    )

                let docModels = docContent.Convert(this.input, defaultTemplate, extraInputs)

                let actualDocModels = docModels |> List.map fst |> List.choose id

                let extrasForSearchIndex = docContent.GetSearchIndexEntries(actualDocModels)

                let navEntries = docContent.GetNavigationEntries(actualDocModels)

                let results =
                    Map.ofList [ for (thing, _action) in docModels do
                                     match thing with
                                     | Some (file, _isOtherLang, model) -> (file, model)
                                     | None -> () ]

                latestDocContentResults <- results
                latestDocContentSearchIndexEntries <- extrasForSearchIndex
                latestDocContentGlobalParameters <- [ ParamKeys.``fsdocs-list-of-documents``, navEntries ]

                latestDocContentPhase2 <-
                    (fun globals ->

                        printfn ""
                        printfn "Write Content:"

                        for (_thing, action) in docModels do
                            action globals

                        ))

        let runDocContentPhase2 () =
            protect "Content generation (phase 2)" (fun () ->
                let globals = getLatestWatchScript () @ getLatestGlobalParameters ()

                latestDocContentPhase2 globals)

        //-----------------------------------------
        // Clean

        let rootInputFolderAsGiven = this.input
        let rootInputFolderFullPath = Path.GetFullPath rootInputFolderAsGiven
        let rootOutputFolderFullPath = Path.GetFullPath rootOutputFolderAsGiven

        if this.clean then
            let rec clean dir =
                for file in Directory.EnumerateFiles(dir) do
                    File.Delete file |> ignore

                for subdir in Directory.EnumerateDirectories dir do
                    if not (Path.GetFileName(subdir).StartsWith ".") then
                        clean subdir

            let isOutputPathOK =
                rootOutputFolderAsGiven <> "/"
                && rootOutputFolderAsGiven <> "."
                && rootOutputFolderFullPath <> rootInputFolderFullPath
                && not (String.IsNullOrEmpty rootOutputFolderAsGiven)

            if isOutputPathOK then
                try
                    clean rootOutputFolderFullPath
                with
                | e -> printfn "warning: error during cleaning, continuing: %s" e.Message
            else
                printfn "warning: skipping cleaning due to strange output path: \"%s\"" rootOutputFolderAsGiven

        if watch then
            printfn "Building docs first time..."

        //-----------------------------------------
        // Build

        let ok =
            let ok1 = runGeneratePhase1 ()
            // Note, the above generates these outputs:
            //   latestApiDocModel
            //   latestApiDocGlobalParameters
            //   latestApiDocCodeReferenceResolver
            //   latestApiDocPhase2
            //   latestApiDocSearchIndexEntries

            let ok2 = runDocContentPhase1 ()
            // Note, the above references these inputs:
            //   latestApiDocCodeReferenceResolver
            //
            // Note, the above generates these outputs:
            //   latestDocContentResults
            //   latestDocContentSearchIndexEntries
            //   latestDocContentGlobalParameters
            //   latestDocContentPhase2

            let ok2 = ok2 && runGeneratePhase2 ()

            // Run this second to override anything produced by API generate, e.g.
            // bespoke file for namespaces etc.
            let ok1 = ok1 && runDocContentPhase2 ()
            regenerateSearchIndex ()
            ok1 && ok2

        //-----------------------------------------
        // Watch

        if watch then

            let docsWatchers =
                if Directory.Exists(this.input) then
                    [ new FileSystemWatcher(this.input) ]
                else
                    []

            let templateWatchers =
                if Directory.Exists(this.input) then
                    [ new FileSystemWatcher(this.input) ]
                else
                    []

            let projectOutputWatchers =
                [ for input in apiDocInputs do
                      let dir = Path.GetDirectoryName(input.Path)

                      if Directory.Exists(dir) then
                          new FileSystemWatcher(dir), input.Path ]

            use _holder =
                { new IDisposable with
                    member __.Dispose() =
                        for p in docsWatchers do
                            p.Dispose()

                        for p in templateWatchers do
                            p.Dispose()

                        for (p, _) in projectOutputWatchers do
                            p.Dispose() }

            // Only one update at a time
            let monitor = obj ()
            // One of each kind of request at a time
            let mutable docsQueued = true
            let mutable generateQueued = true

            let docsDependenciesChanged = Event<_>()

            docsDependenciesChanged.Publish.Add (fun () ->
                if not docsQueued then
                    docsQueued <- true
                    printfn "Detected change in '%s', scheduling rebuild of docs..." this.input

                    async {
                        do! Async.Sleep(300)

                        lock monitor (fun () ->
                            docsQueued <- false

                            if runDocContentPhase1 () then
                                if runDocContentPhase2 () then
                                    regenerateSearchIndex ())

                        Serve.signalHotReload.Set() |> ignore
                    }
                    |> Async.Start)

            let apiDocsDependenciesChanged = Event<_>()

            apiDocsDependenciesChanged.Publish.Add (fun () ->
                if not generateQueued then
                    generateQueued <- true
                    printfn "Detected change in built outputs, scheduling rebuild of API docs..."

                    async {
                        do! Async.Sleep(300)

                        lock monitor (fun () ->
                            generateQueued <- false

                            if runGeneratePhase1 () then
                                if runGeneratePhase2 () then
                                    regenerateSearchIndex ())

                        Serve.signalHotReload.Set() |> ignore
                    }
                    |> Async.Start)

            // Listen to changes in any input under docs
            for docsWatcher in docsWatchers do
                docsWatcher.IncludeSubdirectories <- true
                docsWatcher.NotifyFilter <- NotifyFilters.LastWrite
                docsWatcher.Changed.Add(fun _ -> docsDependenciesChanged.Trigger())

            // When _template.* change rebuild everything
            for templateWatcher in templateWatchers do
                templateWatcher.IncludeSubdirectories <- true
                templateWatcher.Filter <- "_template.html"
                templateWatcher.NotifyFilter <- NotifyFilters.LastWrite

                templateWatcher.Changed.Add (fun _ ->
                    docsDependenciesChanged.Trigger()
                    apiDocsDependenciesChanged.Trigger())

            // Listen to changes in output DLLs
            for (projectOutputWatcher, projectOutput) in projectOutputWatchers do
                projectOutputWatcher.Filter <- Path.GetFileName(projectOutput)
                projectOutputWatcher.Path <- Path.GetDirectoryName(projectOutput)
                projectOutputWatcher.NotifyFilter <- NotifyFilters.LastWrite
                projectOutputWatcher.Changed.Add(fun _ -> apiDocsDependenciesChanged.Trigger())

            // Start raising events
            for docsWatcher in docsWatchers do
                docsWatcher.EnableRaisingEvents <- true

            for templateWatcher in templateWatchers do
                templateWatcher.EnableRaisingEvents <- true

            for (pathWatcher, _path) in projectOutputWatchers do
                pathWatcher.EnableRaisingEvents <- true

            generateQueued <- false
            docsQueued <- false

            if not this.noserver_option then
                printfn
                    "starting server on http://localhost:%d for content in %s"
                    this.port_option
                    rootOutputFolderFullPath

                Serve.startWebServer rootOutputFolderFullPath this.port_option

            if not this.nolaunch_option then
                let url = sprintf "http://localhost:%d/%s" this.port_option this.open_option

                printfn "launching browser window to open %s" url

                try
                    Process.Start(new ProcessStartInfo(url, UseShellExecute = true)) |> ignore
                with
                | ex -> printfn "Warning, unable to launch browser(%s), try manually browsing to %s" ex.Message url

            waitForKey watch

        if ok then 0 else 1

    abstract noserver_option: bool
    default x.noserver_option = false

    abstract nolaunch_option: bool
    default x.nolaunch_option = false

    abstract open_option: string
    default x.open_option = ""

    abstract port_option: int
    default x.port_option = 0

[<Verb("build", HelpText = "build the documentation for a solution based on content and defaults")>]
type BuildCommand() =
    inherit CoreBuildOptions(false)

[<Verb("watch", HelpText = "build the documentation for a solution based on content and defaults, watch it and serve it")>]
type WatchCommand() =
    inherit CoreBuildOptions(true)

    override x.noserver_option = x.noserver

    [<Option("noserver", Required = false, Default = false, HelpText = "Do not serve content when watching.")>]
    member val noserver = false with get, set

    override x.nolaunch_option = x.nolaunch

    [<Option("nolaunch", Required = false, Default = false, HelpText = "Do not launch a browser window.")>]
    member val nolaunch = false with get, set

    override x.open_option = x.openv

    [<Option("open", Required = false, Default = "", HelpText = "URL extension to launch http://localhost:<port>/%s.")>]
    member val openv = "" with get, set

    override x.port_option = x.port

    [<Option("port", Required = false, Default = 8901, HelpText = "Port to serve content for http://localhost serving.")>]
    member val port = 8901 with get, set
