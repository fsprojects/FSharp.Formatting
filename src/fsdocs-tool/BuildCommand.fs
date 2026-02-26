namespace fsdocs

open System.Collections.Concurrent
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
open Suave.Logging
open FSharp.Formatting.Markdown

#nowarn "44" // Obsolete WebClient

/// Convert markdown, script and other content into a static site
type internal DocContent
    (
        rootOutputFolderAsGiven,
        previous: Map<_, _>,
        lineNumbers,
        evaluate,
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
            if
                url.StartsWith("http", StringComparison.Ordinal)
                || url.StartsWith("https", StringComparison.Ordinal)
            then
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
        let isPynb = inputFileFullPath.EndsWith(".ipynb", true, CultureInfo.InvariantCulture)
        let ext = outputKind.Extension

        let outputFileRelativeToRoot =
            if isFsx || isMd || isPynb then
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
        CultureInfo.GetCultures(CultureTypes.AllCultures)
        |> Array.choose (fun x ->
            if x.TwoLetterISOLanguageName.Length <> 2 then
                None
            else
                Some x.TwoLetterISOLanguageName)
        |> Array.distinct

    let makeMarkdownLinkResolver
        (inputFolderAsGiven, outputFolderRelativeToRoot, fullPathFileMap: Map<(string * OutputKind), string>, outputKind)
        (markdownReference: string)
        =
        let markdownReferenceAsFullInputPathOpt =
            try
                Path.GetFullPath(Path.Combine(inputFolderAsGiven, markdownReference)) |> Some
            with _ ->
                None

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
                        Uri(outputFolderFullPath + "/").MakeRelativeUri(Uri(markdownReferenceFullOutputPath)).ToString()

                    Some uri
                with _ ->
                    printfn
                        $"Couldn't map markdown reference %s{markdownReference} that seemed to correspond to an input file"

                    None

    /// Prepare the map of input file to output file. This map is used to make substitutions through markdown
    /// source such A.md --> A.html or A.fsx --> A.html.  The substitutions depend on the output kind.
    let prepFile (inputFileFullPath: string) (outputKind: OutputKind) outputFolderRelativeToRoot =
        [ let inputFileName = Path.GetFileName(inputFileFullPath)

          if
              not (inputFileName.StartsWith('.'))
              && not (inputFileName.StartsWith("_template", StringComparison.Ordinal))
              && not (
                  inputFileName.StartsWith("_menu", StringComparison.Ordinal)
                  && inputFileName.EndsWith("_template.html", StringComparison.Ordinal)
              )
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
              let subFolderIsSkipped = subInputFolderName.StartsWith '.'
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
        (filesWithFrontMatter: FrontMatterFile array)
        =
        [ let name = Path.GetFileName(inputFileFullPath)

          if name.StartsWith('.') then
              printfn "skipping file %s" inputFileFullPath
          elif
              not (name.StartsWith("_template", StringComparison.Ordinal))
              && not (
                  name.StartsWith("_menu", StringComparison.Ordinal)
                  && name.EndsWith("_template.html", StringComparison.Ordinal)
              )
          then
              let isFsx = inputFileFullPath.EndsWith(".fsx", StringComparison.OrdinalIgnoreCase)

              let isMd = inputFileFullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)

              let isPynb = inputFileFullPath.EndsWith(".ipynb", StringComparison.OrdinalIgnoreCase)

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
                          with _ ->
                              DateTime.MaxValue

                      let templateChangeTime =
                          match template with
                          | Some t when isFsx || isMd || isPynb ->
                              try
                                  let fi = FileInfo(t)
                                  let input = fi.Directory.Name
                                  let headPath = Path.Combine(input, "_head.html")
                                  let bodyPath = Path.Combine(input, "_body.html")

                                  [ yield File.GetLastWriteTime(t)
                                    if Menu.isTemplatingAvailable input then
                                        yield! Menu.getLastWriteTimes input
                                    if File.Exists headPath then
                                        yield File.GetLastWriteTime headPath
                                    if File.Exists bodyPath then
                                        yield File.GetLastWriteTime bodyPath ]
                                  |> List.max
                              with _ ->
                                  DateTime.MaxValue
                          | _ -> DateTime.MinValue

                      let toolChangeTime =
                          try
                              File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location)
                          with _ ->
                              DateTime.MaxValue

                      let changeTime = fileChangeTime |> max templateChangeTime |> max toolChangeTime

                      let generateTime =
                          try
                              File.GetLastWriteTime(outputFileFullPath)
                          with _ ->
                              System.DateTime.MinValue

                      changeTime > generateTime

                  // If it's changed or we don't know anything about it
                  // we have to compute the model to get the global substitutions right
                  let mainRun = (outputKind = OutputKind.Html)
                  let haveModel = previous.TryFind inputFileFullPath

                  if changed || (watch && mainRun && haveModel.IsNone) then
                      if isFsx then
                          printfn "  generating model for %s --> %s" inputFileFullPath outputFileRelativeToRoot

                          let fsiEvaluator =
                              (if evaluate then
                                   Some(
                                       FsiEvaluator(onError = onError, options = [| "--multiemit-" |]) :> IFsiEvaluator
                                   )
                               else
                                   None)

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
                                  onError = Some onError,
                                  filesWithFrontMatter = filesWithFrontMatter
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
                                  onError = Some onError,
                                  filesWithFrontMatter = filesWithFrontMatter
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
                      elif isPynb then
                          printfn "  preparing %s --> %s" inputFileFullPath outputFileRelativeToRoot

                          let evaluateNotebook ipynbFile =
                              let args =
                                  $"repl --run %s{ipynbFile} --default-kernel fsharp --exit-after-run --output-path %s{ipynbFile}"

                              let psi =
                                  ProcessStartInfo(
                                      fileName = "dotnet",
                                      arguments = args,
                                      UseShellExecute = false,
                                      CreateNoWindow = true
                                  )

                              try
                                  let p = Process.Start(psi)
                                  p.WaitForExit()
                              with _ ->
                                  let msg =
                                      $"Failed to evaluate notebook %s{ipynbFile} using dotnet-repl\n"
                                      + $"""try running "%s{args}" at the command line and inspect the error"""

                                  failwith msg

                          let checkDotnetReplInstall () =
                              let failmsg =
                                  "'dotnet-repl' is not installed. Please install it using 'dotnet tool install dotnet-repl'"

                              try
                                  let psi =
                                      ProcessStartInfo(
                                          fileName = "dotnet",
                                          arguments = "tool list --local",
                                          UseShellExecute = false,
                                          CreateNoWindow = true,
                                          RedirectStandardOutput = true
                                      )

                                  let p = Process.Start(psi)
                                  let ol = p.StandardOutput.ReadToEnd()
                                  p.WaitForExit()
                                  psi.Arguments <- "tool list --global"
                                  p.Start() |> ignore
                                  let og = p.StandardOutput.ReadToEnd()
                                  let output = $"%s{ol}\n%s{og}"

                                  if not (output.Contains("dotnet-repl")) then
                                      failwith failmsg

                                  p.WaitForExit()
                              with _ ->
                                  failwith failmsg

                          if evaluate then
                              checkDotnetReplInstall ()
                              printfn $"  evaluating %s{inputFileFullPath} with dotnet-repl"
                              evaluateNotebook inputFileFullPath


                          let model =
                              Literate.ParseAndTransformPynbFile(
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
                                  onError = Some onError,
                                  filesWithFrontMatter = filesWithFrontMatter
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
                                       with _ when watch ->
                                           ()))
                  //printfn "skipping unchanged file %s" inputFileFullPath
                  else if mainRun && watch then
                      match haveModel with
                      | None -> ()
                      | Some haveModel -> yield (Some(inputFileFullPath, isOtherLang, haveModel), (fun _ -> ())) ]

    let rec processFolder
        (htmlTemplate, texTemplate, pynbTemplate, fsxTemplate, mdTemplate, isOtherLang, rootInputFolder, fullPathFileMap)
        (inputFolderAsGiven: string)
        outputFolderRelativeToRoot
        (filesWithFrontMatter: FrontMatterFile array)
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
                      filesWithFrontMatter

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
                      filesWithFrontMatter

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
                      filesWithFrontMatter

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
                      filesWithFrontMatter

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
                      filesWithFrontMatter

          for subInputFolderFullPath in Directory.EnumerateDirectories(inputFolderAsGiven) do
              let subInputFolderName = Path.GetFileName(subInputFolderFullPath)
              let subFolderIsSkipped = subInputFolderName.StartsWith '.'
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
                          (Path.Combine(outputFolderRelativeToRoot, subInputFolderName))
                          filesWithFrontMatter ]

    member _.Convert(rootInputFolderAsGiven, htmlTemplate, extraInputs) =

        let inputDirectories = extraInputs @ [ (rootInputFolderAsGiven, ".") ]

        // Maps full input paths to full output paths
        let fullPathFileMap =
            [ for (rootInputFolderAsGiven, outputFolderRelativeToRoot) in inputDirectories do
                  yield! prepFolder rootInputFolderAsGiven outputFolderRelativeToRoot ]
            |> Map.ofList

        // In order to create {{next-page-url}} and {{previous-page-url}}
        // We need to scan all *.fsx and *.md files for their frontmatter.
        let filesWithFrontMatter =
            fullPathFileMap
            |> Map.keys
            |> Seq.map fst
            |> Seq.distinct
            |> Seq.choose (fun fileName ->
                let ext = Path.GetExtension fileName

                if ext = ".fsx" then
                    ParseScript.ParseFrontMatter(fileName)
                elif ext = ".md" then
                    File.ReadLines fileName |> FrontMatterFile.ParseFromLines fileName
                elif ext = ".ipynb" then
                    ParsePynb.parseFrontMatter fileName
                else
                    None)
            |> Seq.sortBy (fun { Index = idx; CategoryIndex = cIdx } -> cIdx, idx)
            |> Seq.toArray

        [ for (rootInputFolderAsGiven, outputFolderRelativeToRoot) in inputDirectories do
              yield!
                  processFolder
                      (htmlTemplate, None, None, None, None, false, Some rootInputFolderAsGiven, fullPathFileMap)
                      rootInputFolderAsGiven
                      outputFolderRelativeToRoot
                      filesWithFrontMatter ]

    member _.GetSearchIndexEntries(docModels: (string * bool * LiterateDocModel) list) =
        [| for (_inputFile, isOtherLang, model) in docModels do
               if not isOtherLang then
                   match model.IndexText with
                   | Some(IndexText(fullContent, headings)) ->
                       { title = model.Title
                         content = fullContent
                         headings = headings
                         uri = model.Uri(root)
                         ``type`` = "content" }
                   | _ -> () |]

    member _.GetNavigationEntries
        (
            input,
            docModels: (string * bool * LiterateDocModel) list,
            currentPagePath: string option,
            ignoreUncategorized: bool
        ) =
        let modelsForList =
            [ for thing in docModels do
                  match thing with
                  | (inputFileFullPath, isOtherLang, model) when
                      not isOtherLang
                      && model.OutputKind = OutputKind.Html
                      && (Path.GetFileNameWithoutExtension(inputFileFullPath) <> "index")
                      ->
                      { model with
                          IsActive =
                              match currentPagePath with
                              | None -> false
                              | Some currentPagePath -> currentPagePath = inputFileFullPath }
                  | _ -> () ]

        let excludeUncategorized =
            if ignoreUncategorized then
                List.filter (fun (model: LiterateDocModel) -> model.Category.IsSome)
            else
                id

        let modelsByCategory =
            modelsForList
            |> excludeUncategorized
            |> List.groupBy (fun (model) -> model.Category)
            |> List.sortBy (fun (_, ms) ->
                match ms.[0].CategoryIndex with
                | Some s ->
                    (try
                        int32 s
                     with _ ->
                         Int32.MaxValue)
                | None -> Int32.MaxValue)

        let orderList (list: (LiterateDocModel) list) =
            list
            |> List.sortBy (fun model -> Option.defaultValue Int32.MaxValue model.Index)

        if Menu.isTemplatingAvailable input then
            let createGroup (isCategoryActive: bool) (header: string) (items: LiterateDocModel list) : string =
                //convert items into menuitem list
                let menuItems =
                    orderList items
                    |> List.map (fun (model: LiterateDocModel) ->
                        let link = model.Uri(root)
                        let title = System.Web.HttpUtility.HtmlEncode model.Title

                        { Menu.MenuItem.Link = link
                          Menu.MenuItem.Content = title
                          Menu.MenuItem.IsActive = model.IsActive })

                Menu.createMenu input isCategoryActive header menuItems
            // No categories specified
            if modelsByCategory.Length = 1 && (fst modelsByCategory.[0]) = None then
                let _, items = modelsByCategory.[0]
                createGroup false "Documentation" items
            else
                modelsByCategory
                |> List.map (fun (header, items) ->
                    let header = Option.defaultValue "Other" header
                    let isActive = items |> List.exists (fun m -> m.IsActive)
                    createGroup isActive header items)
                |> String.concat "\n"
        else
            [
              // No categories specified
              if modelsByCategory.Length = 1 && (fst modelsByCategory.[0]) = None then
                  li [ Class "nav-header" ] [ !!"Documentation" ]

                  for model in snd modelsByCategory.[0] do
                      let link = model.Uri(root)
                      let activeClass = if model.IsActive then "active" else ""

                      li
                          [ Class $"nav-item %s{activeClass}" ]
                          [ a [ Class "nav-link"; (Href link) ] [ encode model.Title ] ]
              else
                  // At least one category has been specified. Sort each category by index and emit
                  // Use 'Other' as a header for uncategorised things
                  for (cat, modelsInCategory) in modelsByCategory do
                      let modelsInCategory = orderList modelsInCategory

                      let categoryActiveClass =
                          if modelsInCategory |> List.exists (fun m -> m.IsActive) then
                              "active"
                          else
                              ""

                      match cat with
                      | Some c -> li [ Class $"nav-header %s{categoryActiveClass}" ] [ !!c ]
                      | None -> li [ Class $"nav-header %s{categoryActiveClass}" ] [ !!"Other" ]

                      for model in modelsInCategory do
                          let link = model.Uri(root)
                          let activeClass = if model.IsActive then "active" else ""

                          li
                              [ Class $"nav-item %s{activeClass}" ]
                              [ a [ Class "nav-link"; (Href link) ] [ encode model.Title ] ] ]
            |> List.map (fun html -> html.ToString())
            |> String.concat "             \n"

/// Processes and runs Suave server to host them on localhost
module Serve =
    let refreshEvent = FSharp.Control.Event<string>()

    /// generate the script to inject into html to enable hot reload during development
    let generateWatchScript (port: int) =
        let tag =
            """
<script type="text/javascript">
    var wsUri = "ws://localhost:{{PORT}}/websocket";
    function init()
    {
        websocket = new WebSocket(wsUri);
        websocket.onmessage = function(evt) {
            const data = evt.data;
            if (data.endsWith(".css")) {
                console.log(`Trying to reload ${data}`);
                const link = document.querySelector(`link[href*='${data}']`);
                if (link) {
                    const href = new URL(link.href);
                    const ticks = new Date().getTime();
                    href.searchParams.set("v", ticks);
                    link.href = href.toString();
                }
            }
            else {
                console.log('closing');
                websocket.close();
                document.location.reload();
            }
        }
    }
    window.addEventListener("load", init, false);
</script>
"""

        tag.Replace("{{PORT}}", string<int> port)

    let connectedClients = ConcurrentDictionary<WebSocket, unit>()

    let socketHandler (webSocket: WebSocket) (context: HttpContext) =
        context.runtime.logger.info (Message.eventX "New websocket connection")
        connectedClients.TryAdd(webSocket, ()) |> ignore

        socket {
            let! msg = webSocket.read ()

            match msg with
            | Close, _, _ ->
                context.runtime.logger.info (Message.eventX "Closing connection")
                connectedClients.TryRemove webSocket |> ignore
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
            | _ -> ()
        }

    let broadCastReload (msg: string) =
        let msg = msg |> Encoding.UTF8.GetBytes |> ByteSegment

        connectedClients.Keys
        |> Seq.map (fun client ->
            async {
                let! _ = client.send Text msg true
                ()
            })
        |> Async.Parallel
        |> Async.Ignore
        |> Async.RunSynchronously

    refreshEvent.Publish
    |> Event.add (fun fileName ->
        if Path.HasExtension fileName then
            let fileName = fileName.Replace("\\", "/").TrimEnd('~')
            broadCastReload fileName)

    let startWebServer rootOutputFolderAsGiven localPort =
        let mimeTypesMap ext =
            match ext with
            | ".323" -> Writers.createMimeType "text/h323" false
            | ".3g2" -> Writers.createMimeType "video/3gpp2" false
            | ".3gp2" -> Writers.createMimeType "video/3gpp2" false
            | ".3gp" -> Writers.createMimeType "video/3gpp" false
            | ".3gpp" -> Writers.createMimeType "video/3gpp" false
            | ".aac" -> Writers.createMimeType "audio/aac" false
            | ".aaf" -> Writers.createMimeType "application/octet-stream" false
            | ".aca" -> Writers.createMimeType "application/octet-stream" false
            | ".accdb" -> Writers.createMimeType "application/msaccess" false
            | ".accde" -> Writers.createMimeType "application/msaccess" false
            | ".accdt" -> Writers.createMimeType "application/msaccess" false
            | ".acx" -> Writers.createMimeType "application/internet-property-stream" false
            | ".adt" -> Writers.createMimeType "audio/vnd.dlna.adts" false
            | ".adts" -> Writers.createMimeType "audio/vnd.dlna.adts" false
            | ".afm" -> Writers.createMimeType "application/octet-stream" false
            | ".ai" -> Writers.createMimeType "application/postscript" false
            | ".aif" -> Writers.createMimeType "audio/x-aiff" false
            | ".aifc" -> Writers.createMimeType "audio/aiff" false
            | ".aiff" -> Writers.createMimeType "audio/aiff" false
            | ".appcache" -> Writers.createMimeType "text/cache-manifest" false
            | ".application" -> Writers.createMimeType "application/x-ms-application" false
            | ".art" -> Writers.createMimeType "image/x-jg" false
            | ".asd" -> Writers.createMimeType "application/octet-stream" false
            | ".asf" -> Writers.createMimeType "video/x-ms-asf" false
            | ".asi" -> Writers.createMimeType "application/octet-stream" false
            | ".asm" -> Writers.createMimeType "text/plain" false
            | ".asr" -> Writers.createMimeType "video/x-ms-asf" false
            | ".asx" -> Writers.createMimeType "video/x-ms-asf" false
            | ".atom" -> Writers.createMimeType "application/atom+xml" false
            | ".au" -> Writers.createMimeType "audio/basic" false
            | ".avi" -> Writers.createMimeType "video/x-msvideo" false
            | ".axs" -> Writers.createMimeType "application/olescript" false
            | ".bas" -> Writers.createMimeType "text/plain" false
            | ".bcpio" -> Writers.createMimeType "application/x-bcpio" false
            | ".bin" -> Writers.createMimeType "application/octet-stream" false
            | ".bmp" -> Writers.createMimeType "image/bmp" false
            | ".c" -> Writers.createMimeType "text/plain" false
            | ".cab" -> Writers.createMimeType "application/vnd.ms-cab-compressed" false
            | ".calx" -> Writers.createMimeType "application/vnd.ms-office.calx" false
            | ".cat" -> Writers.createMimeType "application/vnd.ms-pki.seccat" false
            | ".cdf" -> Writers.createMimeType "application/x-cdf" false
            | ".chm" -> Writers.createMimeType "application/octet-stream" false
            | ".class" -> Writers.createMimeType "application/x-java-applet" false
            | ".clp" -> Writers.createMimeType "application/x-msclip" false
            | ".cmx" -> Writers.createMimeType "image/x-cmx" false
            | ".cnf" -> Writers.createMimeType "text/plain" false
            | ".cod" -> Writers.createMimeType "image/cis-cod" false
            | ".cpio" -> Writers.createMimeType "application/x-cpio" false
            | ".cpp" -> Writers.createMimeType "text/plain" false
            | ".crd" -> Writers.createMimeType "application/x-mscardfile" false
            | ".crl" -> Writers.createMimeType "application/pkix-crl" false
            | ".crt" -> Writers.createMimeType "application/x-x509-ca-cert" false
            | ".csh" -> Writers.createMimeType "application/x-csh" false
            | ".css" -> Writers.createMimeType "text/css" false
            | ".csv" -> Writers.createMimeType "text/csv" false
            | ".cur" -> Writers.createMimeType "application/octet-stream" false
            | ".dcr" -> Writers.createMimeType "application/x-director" false
            | ".deploy" -> Writers.createMimeType "application/octet-stream" false
            | ".der" -> Writers.createMimeType "application/x-x509-ca-cert" false
            | ".dib" -> Writers.createMimeType "image/bmp" false
            | ".dir" -> Writers.createMimeType "application/x-director" false
            | ".disco" -> Writers.createMimeType "text/xml" false
            | ".dlm" -> Writers.createMimeType "text/dlm" false
            | ".doc" -> Writers.createMimeType "application/msword" false
            | ".docm" -> Writers.createMimeType "application/vnd.ms-word.document.macroEnabled.12" false
            | ".docx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.wordprocessingml.document" false
            | ".dot" -> Writers.createMimeType "application/msword" false
            | ".dotm" -> Writers.createMimeType "application/vnd.ms-word.template.macroEnabled.12" false
            | ".dotx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.wordprocessingml.template" false
            | ".dsp" -> Writers.createMimeType "application/octet-stream" false
            | ".dtd" -> Writers.createMimeType "text/xml" false
            | ".dvi" -> Writers.createMimeType "application/x-dvi" false
            | ".dvr-ms" -> Writers.createMimeType "video/x-ms-dvr" false
            | ".dwf" -> Writers.createMimeType "drawing/x-dwf" false
            | ".dwp" -> Writers.createMimeType "application/octet-stream" false
            | ".dxr" -> Writers.createMimeType "application/x-director" false
            | ".eml" -> Writers.createMimeType "message/rfc822" false
            | ".emz" -> Writers.createMimeType "application/octet-stream" false
            | ".eot" -> Writers.createMimeType "application/vnd.ms-fontobject" false
            | ".eps" -> Writers.createMimeType "application/postscript" false
            | ".etx" -> Writers.createMimeType "text/x-setext" false
            | ".evy" -> Writers.createMimeType "application/envoy" false
            | ".exe" -> Writers.createMimeType "application/vnd.microsoft.portable-executable" false
            | ".fdf" -> Writers.createMimeType "application/vnd.fdf" false
            | ".fif" -> Writers.createMimeType "application/fractals" false
            | ".fla" -> Writers.createMimeType "application/octet-stream" false
            | ".flr" -> Writers.createMimeType "x-world/x-vrml" false
            | ".flv" -> Writers.createMimeType "video/x-flv" false
            | ".gif" -> Writers.createMimeType "image/gif" false
            | ".gtar" -> Writers.createMimeType "application/x-gtar" false
            | ".gz" -> Writers.createMimeType "application/x-gzip" false
            | ".h" -> Writers.createMimeType "text/plain" false
            | ".hdf" -> Writers.createMimeType "application/x-hdf" false
            | ".hdml" -> Writers.createMimeType "text/x-hdml" false
            | ".hhc" -> Writers.createMimeType "application/x-oleobject" false
            | ".hhk" -> Writers.createMimeType "application/octet-stream" false
            | ".hhp" -> Writers.createMimeType "application/octet-stream" false
            | ".hlp" -> Writers.createMimeType "application/winhlp" false
            | ".hqx" -> Writers.createMimeType "application/mac-binhex40" false
            | ".hta" -> Writers.createMimeType "application/hta" false
            | ".htc" -> Writers.createMimeType "text/x-component" false
            | ".htm" -> Writers.createMimeType "text/html" false
            | ".html" -> Writers.createMimeType "text/html" false
            | ".htt" -> Writers.createMimeType "text/webviewhtml" false
            | ".hxt" -> Writers.createMimeType "text/html" false
            | ".ical" -> Writers.createMimeType "text/calendar" false
            | ".icalendar" -> Writers.createMimeType "text/calendar" false
            | ".ico" -> Writers.createMimeType "image/x-icon" false
            | ".ics" -> Writers.createMimeType "text/calendar" false
            | ".ief" -> Writers.createMimeType "image/ief" false
            | ".ifb" -> Writers.createMimeType "text/calendar" false
            | ".iii" -> Writers.createMimeType "application/x-iphone" false
            | ".inf" -> Writers.createMimeType "application/octet-stream" false
            | ".ins" -> Writers.createMimeType "application/x-internet-signup" false
            | ".isp" -> Writers.createMimeType "application/x-internet-signup" false
            | ".IVF" -> Writers.createMimeType "video/x-ivf" false
            | ".jar" -> Writers.createMimeType "application/java-archive" false
            | ".java" -> Writers.createMimeType "application/octet-stream" false
            | ".jck" -> Writers.createMimeType "application/liquidmotion" false
            | ".jcz" -> Writers.createMimeType "application/liquidmotion" false
            | ".jfif" -> Writers.createMimeType "image/pjpeg" false
            | ".jpb" -> Writers.createMimeType "application/octet-stream" false
            | ".jpe" -> Writers.createMimeType "image/jpeg" false
            | ".jpeg" -> Writers.createMimeType "image/jpeg" false
            | ".jpg" -> Writers.createMimeType "image/jpeg" false
            | ".js" -> Writers.createMimeType "text/javascript" false
            | ".json" -> Writers.createMimeType "application/json" false
            | ".jsx" -> Writers.createMimeType "text/jscript" false
            | ".latex" -> Writers.createMimeType "application/x-latex" false
            | ".lit" -> Writers.createMimeType "application/x-ms-reader" false
            | ".lpk" -> Writers.createMimeType "application/octet-stream" false
            | ".lsf" -> Writers.createMimeType "video/x-la-asf" false
            | ".lsx" -> Writers.createMimeType "video/x-la-asf" false
            | ".lzh" -> Writers.createMimeType "application/octet-stream" false
            | ".m13" -> Writers.createMimeType "application/x-msmediaview" false
            | ".m14" -> Writers.createMimeType "application/x-msmediaview" false
            | ".m1v" -> Writers.createMimeType "video/mpeg" false
            | ".m2ts" -> Writers.createMimeType "video/vnd.dlna.mpeg-tts" false
            | ".m3u" -> Writers.createMimeType "audio/x-mpegurl" false
            | ".m4a" -> Writers.createMimeType "audio/mp4" false
            | ".m4v" -> Writers.createMimeType "video/mp4" false
            | ".man" -> Writers.createMimeType "application/x-troff-man" false
            | ".manifest" -> Writers.createMimeType "application/x-ms-manifest" false
            | ".map" -> Writers.createMimeType "text/plain" false
            | ".markdown" -> Writers.createMimeType "text/markdown" false
            | ".md" -> Writers.createMimeType "text/markdown" false
            | ".mdb" -> Writers.createMimeType "application/x-msaccess" false
            | ".mdp" -> Writers.createMimeType "application/octet-stream" false
            | ".me" -> Writers.createMimeType "application/x-troff-me" false
            | ".mht" -> Writers.createMimeType "message/rfc822" false
            | ".mhtml" -> Writers.createMimeType "message/rfc822" false
            | ".mid" -> Writers.createMimeType "audio/mid" false
            | ".midi" -> Writers.createMimeType "audio/mid" false
            | ".mix" -> Writers.createMimeType "application/octet-stream" false
            | ".mjs" -> Writers.createMimeType "text/javascript" false
            | ".mmf" -> Writers.createMimeType "application/x-smaf" false
            | ".mno" -> Writers.createMimeType "text/xml" false
            | ".mny" -> Writers.createMimeType "application/x-msmoney" false
            | ".mov" -> Writers.createMimeType "video/quicktime" false
            | ".movie" -> Writers.createMimeType "video/x-sgi-movie" false
            | ".mp2" -> Writers.createMimeType "video/mpeg" false
            | ".mp3" -> Writers.createMimeType "audio/mpeg" false
            | ".mp4" -> Writers.createMimeType "video/mp4" false
            | ".mp4v" -> Writers.createMimeType "video/mp4" false
            | ".mpa" -> Writers.createMimeType "video/mpeg" false
            | ".mpe" -> Writers.createMimeType "video/mpeg" false
            | ".mpeg" -> Writers.createMimeType "video/mpeg" false
            | ".mpg" -> Writers.createMimeType "video/mpeg" false
            | ".mpp" -> Writers.createMimeType "application/vnd.ms-project" false
            | ".mpv2" -> Writers.createMimeType "video/mpeg" false
            | ".ms" -> Writers.createMimeType "application/x-troff-ms" false
            | ".msi" -> Writers.createMimeType "application/octet-stream" false
            | ".mso" -> Writers.createMimeType "application/octet-stream" false
            | ".mvb" -> Writers.createMimeType "application/x-msmediaview" false
            | ".mvc" -> Writers.createMimeType "application/x-miva-compiled" false
            | ".nc" -> Writers.createMimeType "application/x-netcdf" false
            | ".nsc" -> Writers.createMimeType "video/x-ms-asf" false
            | ".nws" -> Writers.createMimeType "message/rfc822" false
            | ".ocx" -> Writers.createMimeType "application/octet-stream" false
            | ".oda" -> Writers.createMimeType "application/oda" false
            | ".odc" -> Writers.createMimeType "text/x-ms-odc" false
            | ".ods" -> Writers.createMimeType "application/oleobject" false
            | ".oga" -> Writers.createMimeType "audio/ogg" false
            | ".ogg" -> Writers.createMimeType "video/ogg" false
            | ".ogv" -> Writers.createMimeType "video/ogg" false
            | ".ogx" -> Writers.createMimeType "application/ogg" false
            | ".one" -> Writers.createMimeType "application/onenote" false
            | ".onea" -> Writers.createMimeType "application/onenote" false
            | ".onetoc" -> Writers.createMimeType "application/onenote" false
            | ".onetoc2" -> Writers.createMimeType "application/onenote" false
            | ".onetmp" -> Writers.createMimeType "application/onenote" false
            | ".onepkg" -> Writers.createMimeType "application/onenote" false
            | ".osdx" -> Writers.createMimeType "application/opensearchdescription+xml" false
            | ".otf" -> Writers.createMimeType "font/otf" false
            | ".p10" -> Writers.createMimeType "application/pkcs10" false
            | ".p12" -> Writers.createMimeType "application/x-pkcs12" false
            | ".p7b" -> Writers.createMimeType "application/x-pkcs7-certificates" false
            | ".p7c" -> Writers.createMimeType "application/pkcs7-mime" false
            | ".p7m" -> Writers.createMimeType "application/pkcs7-mime" false
            | ".p7r" -> Writers.createMimeType "application/x-pkcs7-certreqresp" false
            | ".p7s" -> Writers.createMimeType "application/pkcs7-signature" false
            | ".pbm" -> Writers.createMimeType "image/x-portable-bitmap" false
            | ".pcx" -> Writers.createMimeType "application/octet-stream" false
            | ".pcz" -> Writers.createMimeType "application/octet-stream" false
            | ".pdf" -> Writers.createMimeType "application/pdf" false
            | ".pfb" -> Writers.createMimeType "application/octet-stream" false
            | ".pfm" -> Writers.createMimeType "application/octet-stream" false
            | ".pfx" -> Writers.createMimeType "application/x-pkcs12" false
            | ".pgm" -> Writers.createMimeType "image/x-portable-graymap" false
            | ".pko" -> Writers.createMimeType "application/vnd.ms-pki.pko" false
            | ".pma" -> Writers.createMimeType "application/x-perfmon" false
            | ".pmc" -> Writers.createMimeType "application/x-perfmon" false
            | ".pml" -> Writers.createMimeType "application/x-perfmon" false
            | ".pmr" -> Writers.createMimeType "application/x-perfmon" false
            | ".pmw" -> Writers.createMimeType "application/x-perfmon" false
            | ".png" -> Writers.createMimeType "image/png" false
            | ".pnm" -> Writers.createMimeType "image/x-portable-anymap" false
            | ".pnz" -> Writers.createMimeType "image/png" false
            | ".pot" -> Writers.createMimeType "application/vnd.ms-powerpoint" false
            | ".potm" -> Writers.createMimeType "application/vnd.ms-powerpoint.template.macroEnabled.12" false
            | ".potx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.template" false
            | ".ppam" -> Writers.createMimeType "application/vnd.ms-powerpoint.addin.macroEnabled.12" false
            | ".ppm" -> Writers.createMimeType "image/x-portable-pixmap" false
            | ".pps" -> Writers.createMimeType "application/vnd.ms-powerpoint" false
            | ".ppsm" -> Writers.createMimeType "application/vnd.ms-powerpoint.slideshow.macroEnabled.12" false
            | ".ppsx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.slideshow" false
            | ".ppt" -> Writers.createMimeType "application/vnd.ms-powerpoint" false
            | ".pptm" -> Writers.createMimeType "application/vnd.ms-powerpoint.presentation.macroEnabled.12" false
            | ".pptx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.presentation" false
            | ".prf" -> Writers.createMimeType "application/pics-rules" false
            | ".prm" -> Writers.createMimeType "application/octet-stream" false
            | ".prx" -> Writers.createMimeType "application/octet-stream" false
            | ".ps" -> Writers.createMimeType "application/postscript" false
            | ".psd" -> Writers.createMimeType "application/octet-stream" false
            | ".psm" -> Writers.createMimeType "application/octet-stream" false
            | ".psp" -> Writers.createMimeType "application/octet-stream" false
            | ".pub" -> Writers.createMimeType "application/x-mspublisher" false
            | ".qt" -> Writers.createMimeType "video/quicktime" false
            | ".qtl" -> Writers.createMimeType "application/x-quicktimeplayer" false
            | ".qxd" -> Writers.createMimeType "application/octet-stream" false
            | ".ra" -> Writers.createMimeType "audio/x-pn-realaudio" false
            | ".ram" -> Writers.createMimeType "audio/x-pn-realaudio" false
            | ".rar" -> Writers.createMimeType "application/octet-stream" false
            | ".ras" -> Writers.createMimeType "image/x-cmu-raster" false
            | ".rf" -> Writers.createMimeType "image/vnd.rn-realflash" false
            | ".rgb" -> Writers.createMimeType "image/x-rgb" false
            | ".rm" -> Writers.createMimeType "application/vnd.rn-realmedia" false
            | ".rmi" -> Writers.createMimeType "audio/mid" false
            | ".roff" -> Writers.createMimeType "application/x-troff" false
            | ".rpm" -> Writers.createMimeType "audio/x-pn-realaudio-plugin" false
            | ".rtf" -> Writers.createMimeType "application/rtf" false
            | ".rtx" -> Writers.createMimeType "text/richtext" false
            | ".scd" -> Writers.createMimeType "application/x-msschedule" false
            | ".sct" -> Writers.createMimeType "text/scriptlet" false
            | ".sea" -> Writers.createMimeType "application/octet-stream" false
            | ".setpay" -> Writers.createMimeType "application/set-payment-initiation" false
            | ".setreg" -> Writers.createMimeType "application/set-registration-initiation" false
            | ".sgml" -> Writers.createMimeType "text/sgml" false
            | ".sh" -> Writers.createMimeType "application/x-sh" false
            | ".shar" -> Writers.createMimeType "application/x-shar" false
            | ".sit" -> Writers.createMimeType "application/x-stuffit" false
            | ".sldm" -> Writers.createMimeType "application/vnd.ms-powerpoint.slide.macroEnabled.12" false
            | ".sldx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.presentationml.slide" false
            | ".smd" -> Writers.createMimeType "audio/x-smd" false
            | ".smi" -> Writers.createMimeType "application/octet-stream" false
            | ".smx" -> Writers.createMimeType "audio/x-smd" false
            | ".smz" -> Writers.createMimeType "audio/x-smd" false
            | ".snd" -> Writers.createMimeType "audio/basic" false
            | ".snp" -> Writers.createMimeType "application/octet-stream" false
            | ".spc" -> Writers.createMimeType "application/x-pkcs7-certificates" false
            | ".spl" -> Writers.createMimeType "application/futuresplash" false
            | ".spx" -> Writers.createMimeType "audio/ogg" false
            | ".src" -> Writers.createMimeType "application/x-wais-source" false
            | ".ssm" -> Writers.createMimeType "application/streamingmedia" false
            | ".sst" -> Writers.createMimeType "application/vnd.ms-pki.certstore" false
            | ".stl" -> Writers.createMimeType "application/vnd.ms-pki.stl" false
            | ".sv4cpio" -> Writers.createMimeType "application/x-sv4cpio" false
            | ".sv4crc" -> Writers.createMimeType "application/x-sv4crc" false
            | ".svg" -> Writers.createMimeType "image/svg+xml" false
            | ".svgz" -> Writers.createMimeType "image/svg+xml" false
            | ".swf" -> Writers.createMimeType "application/x-shockwave-flash" false
            | ".t" -> Writers.createMimeType "application/x-troff" false
            | ".tar" -> Writers.createMimeType "application/x-tar" false
            | ".tcl" -> Writers.createMimeType "application/x-tcl" false
            | ".tex" -> Writers.createMimeType "application/x-tex" false
            | ".texi" -> Writers.createMimeType "application/x-texinfo" false
            | ".texinfo" -> Writers.createMimeType "application/x-texinfo" false
            | ".tgz" -> Writers.createMimeType "application/x-compressed" false
            | ".thmx" -> Writers.createMimeType "application/vnd.ms-officetheme" false
            | ".thn" -> Writers.createMimeType "application/octet-stream" false
            | ".tif" -> Writers.createMimeType "image/tiff" false
            | ".tiff" -> Writers.createMimeType "image/tiff" false
            | ".toc" -> Writers.createMimeType "application/octet-stream" false
            | ".tr" -> Writers.createMimeType "application/x-troff" false
            | ".trm" -> Writers.createMimeType "application/x-msterminal" false
            | ".ts" -> Writers.createMimeType "video/vnd.dlna.mpeg-tts" false
            | ".tsv" -> Writers.createMimeType "text/tab-separated-values" false
            | ".ttc" -> Writers.createMimeType "application/x-font-ttf" false
            | ".ttf" -> Writers.createMimeType "application/x-font-ttf" false
            | ".tts" -> Writers.createMimeType "video/vnd.dlna.mpeg-tts" false
            | ".txt" -> Writers.createMimeType "text/plain" false
            | ".u32" -> Writers.createMimeType "application/octet-stream" false
            | ".uls" -> Writers.createMimeType "text/iuls" false
            | ".ustar" -> Writers.createMimeType "application/x-ustar" false
            | ".vbs" -> Writers.createMimeType "text/vbscript" false
            | ".vcf" -> Writers.createMimeType "text/x-vcard" false
            | ".vcs" -> Writers.createMimeType "text/plain" false
            | ".vdx" -> Writers.createMimeType "application/vnd.ms-visio.viewer" false
            | ".vml" -> Writers.createMimeType "text/xml" false
            | ".vsd" -> Writers.createMimeType "application/vnd.visio" false
            | ".vss" -> Writers.createMimeType "application/vnd.visio" false
            | ".vst" -> Writers.createMimeType "application/vnd.visio" false
            | ".vsto" -> Writers.createMimeType "application/x-ms-vsto" false
            | ".vsw" -> Writers.createMimeType "application/vnd.visio" false
            | ".vsx" -> Writers.createMimeType "application/vnd.visio" false
            | ".vtx" -> Writers.createMimeType "application/vnd.visio" false
            | ".wasm" -> Writers.createMimeType "application/wasm" false
            | ".wav" -> Writers.createMimeType "audio/wav" false
            | ".wax" -> Writers.createMimeType "audio/x-ms-wax" false
            | ".wbmp" -> Writers.createMimeType "image/vnd.wap.wbmp" false
            | ".wcm" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".wdb" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".webm" -> Writers.createMimeType "video/webm" false
            | ".webmanifest" -> Writers.createMimeType "application/manifest+json" false
            | ".webp" -> Writers.createMimeType "image/webp" false
            | ".wks" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".wm" -> Writers.createMimeType "video/x-ms-wm" false
            | ".wma" -> Writers.createMimeType "audio/x-ms-wma" false
            | ".wmd" -> Writers.createMimeType "application/x-ms-wmd" false
            | ".wmf" -> Writers.createMimeType "application/x-msmetafile" false
            | ".wml" -> Writers.createMimeType "text/vnd.wap.wml" false
            | ".wmlc" -> Writers.createMimeType "application/vnd.wap.wmlc" false
            | ".wmls" -> Writers.createMimeType "text/vnd.wap.wmlscript" false
            | ".wmlsc" -> Writers.createMimeType "application/vnd.wap.wmlscriptc" false
            | ".wmp" -> Writers.createMimeType "video/x-ms-wmp" false
            | ".wmv" -> Writers.createMimeType "video/x-ms-wmv" false
            | ".wmx" -> Writers.createMimeType "video/x-ms-wmx" false
            | ".wmz" -> Writers.createMimeType "application/x-ms-wmz" false
            | ".woff" -> Writers.createMimeType "application/font-woff" false
            | ".woff2" -> Writers.createMimeType "font/woff2" false
            | ".wps" -> Writers.createMimeType "application/vnd.ms-works" false
            | ".wri" -> Writers.createMimeType "application/x-mswrite" false
            | ".wrl" -> Writers.createMimeType "x-world/x-vrml" false
            | ".wrz" -> Writers.createMimeType "x-world/x-vrml" false
            | ".wsdl" -> Writers.createMimeType "text/xml" false
            | ".wtv" -> Writers.createMimeType "video/x-ms-wtv" false
            | ".wvx" -> Writers.createMimeType "video/x-ms-wvx" false
            | ".x" -> Writers.createMimeType "application/directx" false
            | ".xaf" -> Writers.createMimeType "x-world/x-vrml" false
            | ".xaml" -> Writers.createMimeType "application/xaml+xml" false
            | ".xap" -> Writers.createMimeType "application/x-silverlight-app" false
            | ".xbap" -> Writers.createMimeType "application/x-ms-xbap" false
            | ".xbm" -> Writers.createMimeType "image/x-xbitmap" false
            | ".xdr" -> Writers.createMimeType "text/plain" false
            | ".xht" -> Writers.createMimeType "application/xhtml+xml" false
            | ".xhtml" -> Writers.createMimeType "application/xhtml+xml" false
            | ".xla" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xlam" -> Writers.createMimeType "application/vnd.ms-excel.addin.macroEnabled.12" false
            | ".xlc" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xlm" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xls" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xlsb" -> Writers.createMimeType "application/vnd.ms-excel.sheet.binary.macroEnabled.12" false
            | ".xlsm" -> Writers.createMimeType "application/vnd.ms-excel.sheet.macroEnabled.12" false
            | ".xlsx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" false
            | ".xlt" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xltm" -> Writers.createMimeType "application/vnd.ms-excel.template.macroEnabled.12" false
            | ".xltx" ->
                Writers.createMimeType "application/vnd.openxmlformats-officedocument.spreadsheetml.template" false
            | ".xlw" -> Writers.createMimeType "application/vnd.ms-excel" false
            | ".xml" -> Writers.createMimeType "text/xml" false
            | ".xof" -> Writers.createMimeType "x-world/x-vrml" false
            | ".xpm" -> Writers.createMimeType "image/x-xpixmap" false
            | ".xps" -> Writers.createMimeType "application/vnd.ms-xpsdocument" false
            | ".xsd" -> Writers.createMimeType "text/xml" false
            | ".xsf" -> Writers.createMimeType "text/xml" false
            | ".xsl" -> Writers.createMimeType "text/xml" false
            | ".xslt" -> Writers.createMimeType "text/xml" false
            | ".xsn" -> Writers.createMimeType "application/octet-stream" false
            | ".xtp" -> Writers.createMimeType "application/octet-stream" false
            | ".xwd" -> Writers.createMimeType "image/x-xwindowdump" false
            | ".z" -> Writers.createMimeType "application/x-compress" false
            | ".zip" -> Writers.createMimeType "application/x-zip-compressed" false
            | _ -> None

        let defaultBinding = defaultConfig.bindings.[0]

        let withPort =
            { defaultBinding.socketBinding with
                port = uint16 localPort }

        let serverConfig =
            { defaultConfig with
                bindings =
                    [ { defaultBinding with
                          socketBinding = withPort } ]
                homeFolder = Some rootOutputFolderAsGiven
                mimeTypesMap = mimeTypesMap }

        let app =
            choose
                [ path "/" >=> Redirection.redirect "/index.html"
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

    [<Option("ignoreuncategorized",
             Default = false,
             Required = false,
             HelpText = "Disable generation of 'Other' category for uncategorized docs.")>]
    member val ignoreuncategorized = false with get, set

    [<Option("ignoreprojects", Default = false, Required = false, HelpText = "Disable project cracking.")>]
    member val ignoreprojects = false with get, set

    [<Option("strict", Default = false, Required = false, HelpText = "Fail if there is a problem generating docs.")>]
    member val strict = false with get, set

    [<Option("eval", Default = false, Required = false, HelpText = "Evaluate F# fragments in scripts.")>]
    member val eval = false with get, set

    [<Option("qualify",
             Default = false,
             Required = false,
             HelpText =
                 "In API doc generation qualify the output by the collection name, e.g. 'reference/FSharp.Core/...' instead of 'reference/...' .")>]
    member val qualify = false with get, set

    [<Option("saveimages",
             Default = "none",
             Required = false,
             HelpText =
                 "Save images referenced in docs (some|none|all). If 'some' then image links in formatted results are saved for latex and ipynb output docs.")>]
    member val saveImages = "none" with get, set

    [<Option("sourcefolder",
             Required = false,
             HelpText =
                 "Source folder at time of component build (defaults to value of `<FsDocsSourceFolder>` from project file, else current directory)")>]
    member val sourceFolder = "" with get, set

    [<Option("sourcerepo",
             Required = false,
             HelpText =
                 "Source repository for github links (defaults to value of `<FsDocsSourceRepository>` from project file, else `<RepositoryUrl>/tree/<RepositoryBranch>` for Git repositories)")>]
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
             HelpText =
                 "Assume /// comments in F# code are markdown style (defaults to value of `<UsesMarkdownComments>` from project file)")>]
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
            with ex ->
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
            match (dict userParameters).TryGetValue(ParamKeys.``fsdocs-collection-name``) with
            | true, v -> Some v
            | _ -> None

        // See https://github.com/ionide/proj-info/issues/123
        let prevDotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")

        let (root, collectionName, crackedProjects, paths, docsSubstitutions), _key =
            let projects = Seq.toList this.projects
            let cacheFile = ".fsdocs/cache"

            let getTime (p: string) =
                try
                    File.GetLastWriteTimeUtc(p)
                with _ ->
                    DateTime.Now

            let key1 =
                (userRoot,
                 this.parameters,
                 projects,
                 getTime (typeof<CoreBuildOptions>.Assembly.Location),
                 (projects |> List.map getTime |> List.toArray))

            Utils.cacheBinary cacheFile (fun (_, key2) -> key1 = key2) (fun () ->
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

                if this.strict then failwith msg else printfn "%s" msg

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
                      if otherFlag.StartsWith("-r:", StringComparison.Ordinal) then
                          if File.Exists(otherFlag.[3..]) then
                              yield otherFlag
                          else
                              printfn "NOTE: the reference '%s' was not seen on disk, ignoring" otherFlag ]
            // TODO: This 'distinctBy' is merging references that may be inconsistent across the project set
            |> List.distinctBy (fun ref -> Path.GetFileName(ref.[3..]))

        let rootOutputFolderAsGiven =
            if String.IsNullOrWhiteSpace this.output then
                if watch then "tmp/watch" else "output"
            else
                this.output

        // This is in-package
        //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net6.0\any
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
            else if
                (try
                    File.Exists(defaultTemplateAttempt1)
                 with _ ->
                     false)
            then
                Some defaultTemplateAttempt1
            elif
                (try
                    File.Exists(defaultTemplateAttempt2)
                 with _ ->
                     false)
            then
                Some defaultTemplateAttempt2
            else
                None

        let extraInputs =
            [ if not this.nodefaultcontent then
                  // The "extras" content goes in "."
                  //   From .nuget\packages\fsdocs-tool\7.1.7\tools\net6.0\any
                  //   to .nuget\packages\fsdocs-tool\7.1.7\extras
                  let attempt1 = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "extras"))

                  if
                      (try
                          Directory.Exists(attempt1)
                       with _ ->
                           false)
                  then
                      printfn "using extra content from %s" attempt1
                      (attempt1, ".")
                  else
                      // This is for in-repo use only, assuming we are executing directly from
                      //   src\fsdocs-tool\bin\Debug\net6.0\fsdocs.exe
                      //   src\fsdocs-tool\bin\Release\net6.0\fsdocs.exe
                      let attempt2 =
                          Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "..", "docs", "content"))

                      if
                          (try
                              Directory.Exists(attempt2)
                           with _ ->
                               false)
                      then
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
                if crackedProjects.Length = 0 || this.noapidocs then
                    latestApiDocGlobalParameters <- [ ParamKeys.``fsdocs-list-of-namespaces``, "" ]
                elif crackedProjects.Length > 0 then
                    let (outputKind, initialTemplate2) =
                        let templates =
                            [ OutputKind.Html, Path.Combine(this.input, "reference", "_template.html")
                              OutputKind.Html, Path.Combine(this.input, "_template.html")
                              OutputKind.Markdown, Path.Combine(this.input, "reference", "_template.md")
                              OutputKind.Markdown, Path.Combine(this.input, "_template.md") ]

                        match templates |> List.tryFind (fun (_, path) -> path |> File.Exists) with
                        | Some(kind, path) -> kind, Some path
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
                                onError = onError,
                                menuTemplateFolder = this.input
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
                        if s.StartsWith("cref:", StringComparison.Ordinal) then
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

                let docContent =
                    DocContent(
                        rootOutputFolderAsGiven,
                        latestDocContentResults,
                        Some this.linenumbers,
                        this.eval,
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

                let navEntriesWithoutActivePage =
                    docContent.GetNavigationEntries(
                        this.input,
                        actualDocModels,
                        None,
                        ignoreUncategorized = this.ignoreuncategorized
                    )

                let headTemplateContent =
                    let headTemplatePath = Path.Combine(this.input, "_head.html")

                    if not (File.Exists headTemplatePath) then
                        ""
                    else
                        File.ReadAllText headTemplatePath
                        |> SimpleTemplating.ApplySubstitutionsInText [ ParamKeys.root, root ]

                let bodyTemplateContent =
                    let bodyTemplatePath = Path.Combine(this.input, "_body.html")

                    if not (File.Exists bodyTemplatePath) then
                        ""
                    else
                        File.ReadAllText bodyTemplatePath
                        |> SimpleTemplating.ApplySubstitutionsInText [ ParamKeys.root, root ]

                let results =
                    Map.ofList
                        [ for (thing, _action) in docModels do
                              match thing with
                              | Some(file, _isOtherLang, model) -> (file, model)
                              | None -> () ]

                latestDocContentResults <- results
                latestDocContentSearchIndexEntries <- extrasForSearchIndex

                latestDocContentGlobalParameters <-
                    [ ParamKeys.``fsdocs-list-of-documents``, navEntriesWithoutActivePage
                      ParamKeys.``fsdocs-head-extra``, headTemplateContent
                      ParamKeys.``fsdocs-body-extra``, bodyTemplateContent ]

                latestDocContentPhase2 <-
                    (fun globals ->
                        printfn ""
                        printfn "Write Content:"

                        for (optDocModel, action) in docModels do
                            let globals =
                                match optDocModel with
                                | None -> globals
                                | Some(currentPagePath, _, _) ->
                                    // Update the nav entries with the current page doc model
                                    let navEntries =
                                        docContent.GetNavigationEntries(
                                            this.input,
                                            actualDocModels,
                                            Some currentPagePath,
                                            ignoreUncategorized = this.ignoreuncategorized
                                        )

                                    globals
                                    |> List.map (fun (pk, v) ->
                                        if pk <> ParamKeys.``fsdocs-list-of-documents`` then
                                            pk, v
                                        else
                                            ParamKeys.``fsdocs-list-of-documents``, navEntries)

                            action globals))

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
                    if not (Path.GetFileName(subdir).StartsWith '.') then
                        clean subdir

            let isOutputPathOK =
                rootOutputFolderAsGiven <> "/"
                && rootOutputFolderAsGiven <> "."
                && rootOutputFolderFullPath <> rootInputFolderFullPath
                && not (String.IsNullOrEmpty rootOutputFolderAsGiven)

            if isOutputPathOK then
                try
                    clean rootOutputFolderFullPath
                with e ->
                    printfn "warning: error during cleaning, continuing: %s" e.Message
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
                [ if Directory.Exists(this.input) then
                      yield new FileSystemWatcher(this.input)
                  match defaultTemplate with
                  | Some defaultTemplate ->
                      yield new FileSystemWatcher(Path.GetDirectoryName(defaultTemplate), IncludeSubdirectories = true)
                  | None -> () ]

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
                    member _.Dispose() =
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

            let docsDependenciesChanged = FSharp.Control.Event<string>()

            docsDependenciesChanged.Publish.Add(fun fileName ->
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

                        Serve.refreshEvent.Trigger fileName
                    }
                    |> Async.Start)

            let apiDocsDependenciesChanged = FSharp.Control.Event<_>()

            apiDocsDependenciesChanged.Publish.Add(fun () ->
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

                        Serve.refreshEvent.Trigger "full"
                    }
                    |> Async.Start)

            // Listen to changes in any input under docs
            for docsWatcher in docsWatchers do
                docsWatcher.IncludeSubdirectories <- true
                docsWatcher.NotifyFilter <- NotifyFilters.LastWrite
                docsWatcher.Changed.Add(fun fileEvent -> docsDependenciesChanged.Trigger fileEvent.Name)

            // When _template.* change rebuild everything
            for templateWatcher in templateWatchers do
                templateWatcher.IncludeSubdirectories <- true
                // _menu_template.html or _menu-item_template.html could be changed as well.
                templateWatcher.Filter <- "*template.html"
                templateWatcher.NotifyFilter <- NotifyFilters.LastWrite

                templateWatcher.Changed.Add(fun fileEvent ->
                    docsDependenciesChanged.Trigger fileEvent.Name
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
                with ex ->
                    printfn "Warning, unable to launch browser(%s), try manually browsing to %s" ex.Message url

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
