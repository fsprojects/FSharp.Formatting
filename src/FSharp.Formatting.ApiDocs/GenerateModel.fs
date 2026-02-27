namespace rec FSharp.Formatting.ApiDocs

open System
open System.Reflection
open System.Collections.Generic
open System.Text
open System.IO
open System.Web
open System.Xml
open System.Xml.Linq

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Formatting.Common
open FSharp.Formatting.Internal
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Templating
open FSharp.Patterns
open FSharp.Compiler.Syntax

/// Represents an input assembly for API doc generation
type ApiDocInput =
    {
        /// The path to the assembly
        Path: string

        /// Override the default XML file (normally assumed to live alongside)
        XmlFile: string option

        /// The compile-time source folder
        SourceFolder: string option

        /// The URL the the source repo where the source code lives
        SourceRepo: string option

        /// The substitutionss active for this input. If specified these
        /// are used instead of the overall substitutions.  This allows different parameters (e.g.
        /// different authors) for each assembly in a collection.
        Substitutions: Substitutions option

        /// Whether the input uses markdown comments
        MarkdownComments: bool

        /// Whether doc processing should warn on missing comments
        Warn: bool

        /// Whether to generate only public things
        PublicOnly: bool
    }

    static member FromFile
        (assemblyPath: string, ?mdcomments, ?substitutions, ?sourceRepo, ?sourceFolder, ?publicOnly, ?warn)
        =
        { Path = assemblyPath
          XmlFile = None
          SourceFolder = sourceFolder
          SourceRepo = sourceRepo
          Warn = defaultArg warn false
          Substitutions = substitutions
          PublicOnly = defaultArg publicOnly true
          MarkdownComments = defaultArg mdcomments false }



/// Represents a set of assemblies integrated with their associated documentation
type ApiDocModel internal (substitutions, collection, entityInfos, root, qualify, fileExtensions, urlMap) =
    /// The substitutions.  Different substitutions can also be used for each specific input
    member _.Substitutions: Substitutions = substitutions

    /// The full list of all entities
    member _.Collection: ApiDocCollection = collection

    /// The full list of all entities
    member _.EntityInfos: ApiDocEntityInfo list = entityInfos |> List.filter (fun info -> not info.Entity.Exclude)

    /// The root URL for the entire generation, normally '/'
    member _.Root: string = root

    /// Indicates if each collection is being qualified by its collection name, e.g. 'reference/FSharp.Core'
    member _.Qualify: bool = qualify

    /// Specifies file extensions to use in files and URLs
    member _.FileExtensions: ApiDocFileExtensions = fileExtensions

    /// Specifies file extensions to use in files and URLs
    member internal _.Resolver: CrossReferenceResolver = urlMap

    /// URL of the 'index.html' for the reference documentation for the model
    member x.IndexFileUrl(root, collectionName, qualify, extension) =
        sprintf "%sreference/%sindex%s" root (if qualify then collectionName + "/" else "") extension

    /// URL of the 'index.html' for the reference documentation for the model
    member x.IndexOutputFile(collectionName, qualify, extension) =
        sprintf "reference/%sindex%s" (if qualify then collectionName + "/" else "") extension

    static member internal Generate
        (
            projects: ApiDocInput list,
            collectionName,
            libDirs,
            otherFlags,
            qualify,
            urlRangeHighlight,
            root,
            substitutions,
            onError,
            extensions
        ) =

        // Default template file names

        let otherFlags = defaultArg otherFlags [] |> List.map (fun (o: string) -> o.Trim())

        let libDirs = defaultArg libDirs [] |> List.map Path.GetFullPath

        let dllFiles = projects |> List.map (fun p -> Path.GetFullPath p.Path)

        let urlRangeHighlight =
            defaultArg urlRangeHighlight (fun url start stop -> String.Format("{0}#L{1}-{2}", url, start, stop))

        // Compiler arguments used when formatting code snippets inside Markdown comments
        let codeFormatCompilerArgs =
            [ for dir in libDirs do
                  yield sprintf "-I:\"%s\"" dir
              for file in dllFiles do
                  yield sprintf "-r:\"%s\"" file ]
            |> String.concat " "

        printfn "  loading %d assemblies..." dllFiles.Length

        let resolvedList =
            FSharpAssembly.LoadFiles(dllFiles, libDirs, otherFlags = otherFlags)
            |> List.zip projects

        // generate the names for the html files beforehand so we can resolve <see cref=""/> links.
        let urlMap = CrossReferenceResolver(root, collectionName, qualify, extensions)

        // Read and process assemblies and the corresponding XML files
        let assemblies =

            for (_, asmOpt) in resolvedList do
                match asmOpt with
                | (_, Some asm) ->
                    printfn "  registering entities for assembly %s..." asm.SimpleName

                    asm.Contents.Entities |> Seq.iter (urlMap.RegisterEntity)
                | _ -> ()

            resolvedList
            |> List.choose (fun (project, (dllFile, asmOpt)) ->
                let sourceFolderRepo =
                    match project.SourceFolder, project.SourceRepo with
                    | Some folder, Some repo -> Some(folder, repo)
                    | Some _folder, _ ->
                        Log.warnf "Repository url should be specified along with source folder."
                        None
                    | _, Some _repo ->
                        Log.warnf "Repository url should be specified along with source folder."
                        None
                    | _ -> None

                match asmOpt with
                | None ->
                    printfn "**** Skipping assembly '%s' because was not found in resolved assembly list" dllFile
                    onError "exiting"
                    None
                | Some asm ->
                    printfn "  reading XML doc for %s..." dllFile

                    let xmlFile = defaultArg project.XmlFile (Path.ChangeExtension(dllFile, ".xml"))

                    let xmlFileNoExt = Path.GetFileNameWithoutExtension(xmlFile)

                    let xmlFileOpt =
                        //Directory.EnumerateFiles(Path.GetDirectoryName(xmlFile), xmlFileNoExt + ".*")
                        Directory.EnumerateFiles(Path.GetDirectoryName xmlFile)
                        |> Seq.filter (fun file ->
                            let fileNoExt = Path.GetFileNameWithoutExtension file
                            let ext = Path.GetExtension file

                            xmlFileNoExt.Equals(fileNoExt, StringComparison.OrdinalIgnoreCase)
                            && ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                        |> Seq.tryHead
                    //|> Seq.map (fun f -> f, f.Remove(0, xmlFile.Length - 4))
                    //|> Seq.tryPick (fun (f, ext) ->
                    //    if ext.Equals(".xml", StringComparison.CurrentCultureIgnoreCase)
                    //      then Some(f) else None)

                    let publicOnly = project.PublicOnly
                    let mdcomments = project.MarkdownComments

                    let substitutions = defaultArg project.Substitutions substitutions

                    match xmlFileOpt with
                    | None -> raise (FileNotFoundException(sprintf "Associated XML file '%s' was not found." xmlFile))
                    | Some xmlFile ->
                        printfn "  reading assembly data for %s..." dllFile

                        SymbolReader.readAssembly (
                            asm,
                            publicOnly,
                            xmlFile,
                            substitutions,
                            sourceFolderRepo,
                            urlRangeHighlight,
                            mdcomments,
                            urlMap,
                            codeFormatCompilerArgs,
                            project.Warn
                        )
                        |> Some)

        printfn "  collecting namespaces..."
        // Union namespaces from multiple libraries
        let namespaces = Dictionary<_, (_ * _ * Substitutions)>()

        for asm, nss in assemblies do
            for ns in nss do
                printfn "  found namespace %s in assembly %s..." ns.Name asm.Name

                match namespaces.TryGetValue(ns.Name) with
                | true, (entities, summary, substitutions) ->
                    namespaces.[ns.Name] <-
                        (entities @ ns.Entities, combineNamespaceDocs [ ns.NamespaceDocs; summary ], substitutions)
                | false, _ -> namespaces.Add(ns.Name, (ns.Entities, ns.NamespaceDocs, ns.Substitutions))

        let namespaces =
            [ for (KeyValue(name, (entities, summary, substitutions))) in namespaces do
                  printfn "  found %d entities in namespace %s..." entities.Length name

                  if entities.Length > 0 then
                      ApiDocNamespace(name, entities, substitutions, summary) ]

        printfn "  found %d namespaces..." namespaces.Length

        let collection =
            ApiDocCollection(collectionName, List.map fst assemblies, namespaces |> List.sortBy (fun ns -> ns.Name))

        let rec nestedModules ns parent (modul: ApiDocEntity) =
            [ yield ApiDocEntityInfo(modul, collection, ns, parent)
              for n in modul.NestedEntities do
                  if not n.IsTypeDefinition then
                      yield! nestedModules ns (Some modul) n ]

        let moduleInfos =
            [ for ns in collection.Namespaces do
                  for n in ns.Entities do
                      if not n.IsTypeDefinition then
                          yield! nestedModules ns None n ]

        let createType ns parent typ =
            ApiDocEntityInfo(typ, collection, ns, parent)

        let rec nestedTypes ns (modul: ApiDocEntity) =
            [ let entities = modul.NestedEntities

              for n in entities do
                  if n.IsTypeDefinition then
                      yield createType ns (Some modul) n

              for n in entities do
                  if not n.IsTypeDefinition then
                      yield! nestedTypes ns n ]

        let typesInfos =
            [ for ns in collection.Namespaces do
                  let entities = ns.Entities

                  for n in entities do
                      if not n.IsTypeDefinition then
                          yield! nestedTypes ns n

                  for n in entities do
                      if n.IsTypeDefinition then
                          yield createType ns None n ]

        ApiDocModel(
            substitutions = substitutions,
            collection = collection,
            entityInfos = moduleInfos @ typesInfos,
            root = root,
            qualify = qualify,
            fileExtensions = extensions,
            urlMap = urlMap
        )

/// Represents an entry suitable for constructing a Lunr index
type ApiDocsSearchIndexEntry =
    {
        uri: string
        title: string
        content: string
        headings: string list
        /// apiDocs or content
        ``type``: string
    }

[<Obsolete("Renamed to ApiDocMember", true)>]
type Member = class end

[<Obsolete("Renamed to ApiDocMemberKind", true)>]
type MemberKind = class end

[<Obsolete("Renamed to ApiDocAttribute", true)>]
type Attribute = class end

[<Obsolete("Renamed to ApiDocComment", true)>]
type DocComment = class end

[<Obsolete("Renamed to ApiDocEntity", true)>]
type Module = class end

[<Obsolete("Renamed to ApiDocEntityInfo", true)>]
type ModuleInfo = class end

[<Obsolete("Renamed to ApiDocEntity", true)>]
type Type = class end

[<Obsolete("Renamed to ApiDocEntity", true)>]
type ApiDocType = class end

[<Obsolete("Renamed to ApiDocTypeInfo", true)>]
type TypeInfo = class end
