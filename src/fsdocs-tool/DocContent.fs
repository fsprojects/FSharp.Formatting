namespace fsdocs

open System
open System.Diagnostics
open System.IO
open System.Globalization
open System.Net
open System.Reflection

open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Literate
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Literate.Evaluation
open fsdocs.Common
open FSharp.Formatting.Templating
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
                                       new FsiEvaluator(onError = onError, options = [| "--multiemit-" |])
                                       :> IFsiEvaluator
                                   )
                               else
                                   None)

                          let model =
                              try
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
                              finally
                                  fsiEvaluator |> Option.iter (fun e -> e.Dispose())

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
/// Processes and runs Suave server to host them on localhost
/// Processes and runs Suave server to host them on localhost
