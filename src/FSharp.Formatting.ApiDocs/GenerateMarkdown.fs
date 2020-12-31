module internal FSharp.Formatting.ApiDocs.GenerateMarkdown

open System
open System.IO
open System.Web
open FSharp.Formatting.Markdown
open FSharp.Formatting.Markdown.Dsl
open FSharp.Formatting.Templating

let encode = HttpUtility.HtmlEncode 
let urlEncode (x: string) = HttpUtility.UrlEncode x
let htmlString (x: ApiDocHtml) = (x.HtmlText.Trim())
let htmlStringSafe (x: ApiDocHtml) =  (x.HtmlText.Trim()).Replace("\n", "<br />")
let embed (x: ApiDocHtml) = !! (htmlString x)
let embedSafe (x: ApiDocHtml) = !! (htmlStringSafe x)
let br = !! "<br />"

type MarkdownRender(model: ApiDocModel) =
  let root = model.Root
  let collectionName = model.Collection.CollectionName
  let qualify = model.Qualify

  let sourceLink url =
      [ match url with
        | None -> ()
        | Some href ->
          link [
            img "Link to source code" (sprintf "%scontent/img/github.png" root)
          ] (href) ]

  let renderMembers header tableHeader (members: ApiDocMember list) =
    [ if members.Length > 0 then
        ``###`` [!! header]
        table [
          [p [ !! tableHeader ]]
          [p [ !! "Description"]]
          [p [ !! "Source"]]
         ]
         [AlignLeft; AlignLeft; AlignCenter]
         [
            for m in members ->
            [
              [
                p [link [!! encode(m.Name)] ("#" + urlEncode(m.Name))]
              ]
              [
                let summary = m.Comment.Summary
                let emptySummary = summary.HtmlText |> String.IsNullOrWhiteSpace

                if not emptySummary then 
                  p [ 
                      embedSafe m.Comment.Summary 
                      br
                    ]
                match m.Comment.Remarks with
                 | None -> ()
                 | Some r ->  p [
                     embedSafe r
                     br
                   ]
                if not m.Parameters.IsEmpty then
                  p [ !! "Parameters: "]
                  yield! m.Parameters |> List.collect (fun parameter ->
                    [
                          p [ 
                             strong [!! parameter.ParameterNameText]
                             !! ":"
                             embedSafe parameter.ParameterType
                          ]
                          match parameter.ParameterDocs with
                           | None -> ()
                           | Some d -> p [!! (sprintf ": %s" (htmlStringSafe d))]
                         
                    ])
                  p [ ] // empty to force new line
                match m.ExtendedType with
                 | None -> ()
                 | Some s -> p [
                     !! "Extended Type: "
                     embedSafe s
                     br
                   ]
                match m.ReturnInfo.ReturnType with
                 | None -> ()
                 | Some t -> p [
                       !! "Returns:"
                       embedSafe t
                       match m.ReturnInfo.ReturnDocs with
                             | None -> ()
                             | Some r -> embedSafe r
                                         br
                    ]
              ]
              [
                p [yield! sourceLink m.SourceLocation]
              ]
            ]
         ]
    ]   


                //    if not m.Comment.Exceptions.IsEmpty then

                //        table [Class "fsdocs-exception-list"] [
                //            for (nm, link, html) in m.Comment.Exceptions do
                //              tr [] [td [] (match link with None -> [] | Some href -> [a [Href href] [!! nm] ])
                //                     td [] [embed html]]
                //        ]

                //    for e in m.Comment.Notes do 
                //        h5 [Class "fsdocs-note-header"] [!! "Note"]
                //        p [Class "fsdocs-note"] [embed e]

                //    for e in m.Comment.Examples do 
                //        h5 [Class "fsdocs-example-header"] [!! "Example"]
                //        p [Class "fsdocs-example"] [embed e]

  let renderEntities (entities: ApiDocEntity list) =
   [ if entities.Length > 0 then
      let hasTypes = entities |> List.exists (fun e -> e.IsTypeDefinition)
      let hasModules = entities |> List.exists (fun e -> not e.IsTypeDefinition)
      table [ 
        [
         p [!! (if hasTypes && hasModules then "Type/Module" elif hasTypes then "Type" else "Modules")]
         p [!!"Description"]
         p [ !! "Source"]
        ]
       ] 
       [AlignLeft; AlignLeft; AlignCenter]
       [
        for e in entities do 
          [
            [p [
              let nm = e.Name 
              let multi = (entities |> List.filter (fun e -> e.Name = nm) |> List.length) > 1
              let nmWithSiffix = if multi then (if e.IsTypeDefinition then nm + " (Type)" else nm + " (Module)") else nm
              link [!!nmWithSiffix] (e.Url(root, collectionName, qualify, model.FileExtensions.InUrl))
            ]]
            [
              p [
                embedSafe e.Comment.Summary
              ]
            ]
            [
             p [
                yield! (sourceLink e.SourceLocation)
              ]
            ]
          ]
       ]
   ]
   
  // Honour the CategoryIndex to put the categories in the right order
  let getSortedCategories xs exclude category categoryIndex =
    xs
    |> List.filter (exclude >> not)
    |> List.groupBy (category)
    |> List.map (fun (cat, xs) -> (cat, xs, xs |> List.minBy (categoryIndex)))
    |> List.sortBy (fun (cat, _xs, x) -> categoryIndex x, cat)
    |> List.map (fun (cat, xs, _x) -> cat, xs)

  let entityContent (info: ApiDocEntityInfo) =
    // Get all the members & comment for the type
    let entity = info.Entity
    let members = entity.AllMembers |> List.filter (fun e -> not e.IsObsolete)

    // Group all members by their category 
    let byCategory =
      getSortedCategories members  (fun m -> m.Exclude) (fun m -> m.Category) (fun m -> m.CategoryIndex)
      |> List.mapi (fun i (key, elems) ->
          let elems = elems |> List.sortBy (fun m -> m.Name)
          let name = if String.IsNullOrEmpty(key) then  "Other module members" else key
          (i, elems, name))
  
    let usageName =
        match info.ParentModule with
        | Some m when m.RequiresQualifiedAccess -> m.Name + "." + entity.Name
        | _ -> entity.Name

    [ 
      ``#`` [!! (usageName + (if entity.IsTypeDefinition then " Type" else " Module"))]
      p [
        !! "Namespace: "
        link [!! info.Namespace.Name] (info.Namespace.Url(root, collectionName, qualify, model.FileExtensions.InUrl))
      ]
      p [!! ("Assembly: " + entity.Assembly.Name + ".dll")]
   

      match info.ParentModule with
      | None -> ()
      | Some parentModule ->
        p [
          !! "Parent Module: "
          link [!! parentModule.Name] (parentModule.Url(root, collectionName, qualify, model.FileExtensions.InUrl))
        ]

      match entity.AbbreviatedType with
      | Some abbreviatedTyp ->
         p [
           !! "Abbreviation For: "
           embed abbreviatedTyp
         ]
      | None ->  ()

      match entity.BaseType with
      | Some baseType ->
        p [
           !! "Base Type: "
           embed baseType
         ]
      | None -> ()

      match entity.AllInterfaces with
      | [] -> ()
      | l ->
         p [!! "All Interfaces: "
            for (i, ity) in Seq.indexed l do
                  if i <> 0 then !! ", "
                  embed ity ]
         
      if entity.Symbol.IsValueType then
          p [!! ("Kind: Struct")]

      match entity.DelegateSignature with
      | Some d ->
          p [!! ("Delegate Signature: "); embed d]
      | None -> ()

      if entity.Symbol.IsProvided then
         p [!! ("This is a provided type definition")]

      if entity.Symbol.IsAttributeType then
         p [!! ("This is an attribute type definition")]

      if entity.Symbol.IsEnum then
         p [!! ("This is an enum type definition")]

      //if info.Entity.IsObsolete then
      //    obsoleteMessage entity.ObsoleteMessage
  
      // Show the summary (and sectioned docs without any members)
      p [ embed entity.Comment.Summary ]

      // Show the remarks etc.
      match entity.Comment.Remarks with
      | Some r ->
          p [embed r]
      | None -> ()

      for note in entity.Comment.Notes do 
          ``#####`` [!! "Note"]
          p [embed note]

      for example in entity.Comment.Examples do 
          ``#####`` [!! "Example"]
          p [embed example]

      if (byCategory.Length > 1) then
        // If there is more than 1 category in the type, generate TOC 
        ``###`` [!! "Table of contents"]
        ul [
          for (index, _, name) in byCategory do
            [p [link [!! (sprintf "#section%d" index)] (name)]]
        ]
 
      //<!-- Render nested types and modules, if there are any -->
  
      let nestedEntities =
         entity.NestedEntities
         |> List.filter (fun e -> not e.IsObsolete)
  
      if (nestedEntities.Length > 0) then
        
        ``###`` [!! (if nestedEntities |> List.forall (fun e -> not e.IsTypeDefinition)  then "Nested modules"
                     elif nestedEntities |> List.forall (fun e -> e.IsTypeDefinition) then "Types"
                     else "Types and nested modules")]
        yield! renderEntities nestedEntities

      for (index, ms, name) in byCategory do
        // Iterate over all the categories and print members. If there are more than one
        // categories, print the category heading (as <h2>) and add XML comment from the type
        // that is related to this specific category.
        if (byCategory.Length > 1) then ``##`` [!! name]

        yield! renderMembers "Functions and values" "Function or value" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ValueOrFunction))
        yield! renderMembers "Type extensions" "Type extension" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.TypeExtension))
        yield! renderMembers "Active patterns" "Active pattern" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ActivePattern))
        yield! renderMembers "Union cases" "Union case" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.UnionCase))
        yield! renderMembers "Record fields" "Record Field" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.RecordField))
        yield! renderMembers "Static parameters" "Static parameters" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticParameter))
        yield! renderMembers "Constructors" "Constructor" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.Constructor))
        yield! renderMembers "Instance members" "Instance member" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.InstanceMember))
        yield! renderMembers "Static members" "Static member" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticMember))
    ]

  let namespaceContent (nsIndex, ns: ApiDocNamespace) =
    let allByCategory = GenerateDoc.categoriseEntities (nsIndex, ns, false) getSortedCategories
    [ if allByCategory.Length > 0 then
        ``##`` [!! (ns.Name + " Namespace")]

        match ns.NamespaceDocs with
        | Some nsdocs ->
            p [embed nsdocs.Summary ]
            match nsdocs.Remarks with
            | Some r -> p [embed r ]
            | None -> ()
            
        | None -> () 

        if (allByCategory.Length > 1) then
            p [!! "Categories:" ]

            ul [
               for category in allByCategory do
                   [p [link [!!category.CategoryName] ("#category-" + category.CategoryIndex)]]
            ]
 
        for category in allByCategory do
          if (allByCategory.Length > 1) then
             ``###`` [link [!! category.CategoryName] ("#category-" + category.CategoryIndex)]
          yield! renderEntities category.CategoryEntites
    ]  

  let listOfNamespacesAux otherDocs nav (nsOpt: ApiDocNamespace option) =
    [
        // For FSharp.Core we make all entries available to other docs else there's not a lot else to show.
        //
        // For non-FSharp.Core we only show one link "API Reference" in the nav menu 
      if otherDocs && nav && model.Collection.CollectionName <> "FSharp.Core" then
        p [
          !! "API Reference"
          link [!! "All Namespaces"] (model.IndexFileUrl(root, collectionName, qualify, model.FileExtensions.InUrl)) 
        ]
      else

      let categorise = GenerateDoc.categorise model getSortedCategories

      let someExist = categorise.Length > 0 

      if someExist && nav then
        p [!! "Namespaces"]

      for allByCategory, ns in categorise do

          // Generate the entry for the namespace
          p [
              link [!!ns.Name] (ns.Url(root, collectionName, qualify, model.FileExtensions.InUrl))

               // If not in the navigation list then generate the summary text as well
              if not nav then
                 !! " - "
                 match ns.NamespaceDocs with
                 | Some nsdocs -> embed nsdocs.Summary
                 | None -> () ] 

          // In the navigation bar generate the expanded list of entities
          // for the active namespace
          if nav then
              match nsOpt with
              | Some ns2 when ns.Name = ns2.Name ->
                  ul [
                      for category in allByCategory do
                          for e in category.CategoryEntites do
                              [p [link [!! e.Name] (e.Url(root, collectionName, qualify, model.FileExtensions.InUrl))]  ]
                  ]
              | _ -> ()
     ]

  let listOfNamespaces otherDocs nav (nsOpt: ApiDocNamespace option) =
     listOfNamespacesAux otherDocs nav nsOpt
     |> List.map (fun html -> html.ToString()) |> String.concat "             \n"

  /// Get the substitutions relevant to all
  member _.GlobalSubstitutions : Substitutions =
    let toc = listOfNamespaces true true None
    [ yield (ParamKeys.``fsdocs-list-of-namespaces``, toc )  ]

  member _.Generate(outDir: string, templateOpt, collectionName, globalParameters) = 
    
    let getSubstitutons parameters toc (content: MarkdownDocument) pageTitle =
      [| yield! parameters
         yield (ParamKeys.``fsdocs-list-of-namespaces``, toc )
         yield (ParamKeys.``fsdocs-content``, Markdown.ToMd(content))
         yield (ParamKeys.``fsdocs-source``,"" )
         yield (ParamKeys.``fsdocs-tooltips``, "" )
         yield (ParamKeys.``fsdocs-page-title``, pageTitle )
         yield! globalParameters
         |]

    let links = ["s", ("s", Some "s")] |> Map.ofList


    let collection = model.Collection
    begin
        let content = MarkdownDocument([
              ``#`` [!! "API Reference"]
              ``##`` [!! "Available Namespaces"]
              ul [(listOfNamespacesAux false false None)]], links)
        let pageTitle = sprintf "%s (API Reference)" collectionName
        let toc = listOfNamespaces false true None 
        let substitutions = getSubstitutons model.Substitutions toc content pageTitle
        let outFile = Path.Combine(outDir, model.IndexOutputFile(collectionName, model.Qualify, model.FileExtensions.InFile) )
        printfn "  Generating %s" outFile
        SimpleTemplating.UseFileAsSimpleTemplate (substitutions, templateOpt, outFile)
    end
    ()

    for (nsIndex, ns) in Seq.indexed collection.Namespaces do

        let content = MarkdownDocument( namespaceContent (nsIndex, ns), links )
        let pageTitle = ns.Name
        let toc = listOfNamespaces false true (Some ns)
        let substitutions = getSubstitutons model.Substitutions toc content pageTitle
        let outFile = Path.Combine(outDir, ns.OutputFile(collectionName, model.Qualify, model.FileExtensions.InFile) )
        printfn "  Generating %s" outFile
        SimpleTemplating.UseFileAsSimpleTemplate (substitutions, templateOpt, outFile)

    for info in model.EntityInfos do
        let content = MarkdownDocument(entityContent info, links)
        let pageTitle = sprintf "%s (%s)" info.Entity.Name collectionName
        let toc = listOfNamespaces false true (Some info.Namespace)
        let substitutions = getSubstitutons info.Entity.Substitutions toc content pageTitle
        let outFile = Path.Combine(outDir, info.Entity.OutputFile(collectionName, model.Qualify, model.FileExtensions.InFile))
        printfn "  Generating %s" outFile
        SimpleTemplating.UseFileAsSimpleTemplate (substitutions, templateOpt, outFile)
