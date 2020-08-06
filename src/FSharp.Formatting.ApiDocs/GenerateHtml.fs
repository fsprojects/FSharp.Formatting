module internal FSharp.Formatting.ApiDocs.GenerateHtml

open System
open System.Collections.Generic
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Formatting.Templating
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

/// Embed some HTML generateed in GenerateModel
let embed (x: ApiDocHtml) = !! x.HtmlText

type HtmlRender(model: ApiDocModel) =
  let root = model.Root
  let collectionName = model.Collection.CollectionName
  let qualify = model.Qualify
  //let obsoleteMessage msg =
  //  div [Class "alert alert-warning"] [
  //      strong [] [!!"NOTE:"]
  //      p [] [!! ("This API is obsolete" + HttpUtility.HtmlEncode(msg))]
  //  ]

  let mutable uniqueNumber = 0
  let UniqueID() =
    uniqueNumber <- uniqueNumber + 1
    uniqueNumber

  let codeWithToolTip content tip =
    div [] [
      let id = UniqueID().ToString()
      code [ OnMouseOut  (sprintf "hideTip(event, '%s', %s)" id id)
             OnMouseOver (sprintf "showTip(event, '%s', %s)" id id)] content
      div [Class "fsdocs-tip"; Id id ] tip
    ]

  let renderMembers header tableHeader (members: ApiDocMember list) =
   [ if members.Length > 0 then
       h3 [] [!! header]
       table [Class "table outer-list fsdocs-member-list"] [
         thead [] [
           tr [] [
             td [Class "fsdocs-member-list-header"] [ !!tableHeader ]
             td [Class "fsdocs-member-list-header"] [ !! "Description" ]
           ]
         ]
         tbody [] [
           for m in members do
             tr [] [
               td [Class "fsdocs-member-name"; Id m.Name] [
                  
                  codeWithToolTip [
                      // This adds #MemberName anchor. These may currently be ambiguous
                      embed m.UsageTooltip
                    ]
                    [
                      div [Class "member-tooltip"] [
                        !! "Full Usage: "
                        embed m.UsageTooltip
                        br []
                        br []
                        if not m.ParameterTooltips.IsEmpty then
                            !! "Parameter Types: "
                            ul [] [
                              for (pname, ptyp) in m.ParameterTooltips do
                                span [] [b [] [!! pname]; !! ":"; embed ptyp ]
                                br []
                            ]
                            br []
                        match m.ReturnTooltip with
                        | None -> ()
                        | Some t ->
                            span [] [!! "Return Type: "; embed t]
                            br []
                        //!! "Signature: "
                        //encode(m.SignatureTooltip)
                        if not m.Modifiers.IsEmpty then
                          !! "Modifiers: "
                          encode(m.FormatModifiers)
                          br []

                          // We suppress the display of ill-formatted type parameters for places
                          // where these have not been explicitly declared
                          match m.FormatTypeArguments with
                          | None -> ()
                          | Some v -> 
                              !!"Type parameters: "
                              encode(v)
                      ]
                    ]
               ]
            
               td [Class "fsdocs-xmldoc"] [
                  if not (String.IsNullOrWhiteSpace(m.Comment.DescriptionHtml.HtmlText)) then
                      embed m.Comment.DescriptionHtml
                      br []
                  match m.ExtendedType with
                  | Some s ->
                      !! "Extended Type: "
                      embed s 
                      br []
                  | _ -> ()
                  if not m.ParameterTooltips.IsEmpty then
                      !! "Parameter Types: "
                      ul [] [
                          for (pname, ptyp) in m.ParameterTooltips do
                          li [] [
                              span [Class "fsdocs-param-name"] [!! pname]
                              !! ":"
                              embed ptyp 
                          ]
                      ]
                  match m.ReturnTooltip with
                  | None -> ()
                  | Some t ->
                      !! "Return Type: "
                      embed t
                      br []

                  //if m.IsObsolete then
                  //    obsoleteMessage m.ObsoleteMessage

                  if not (String.IsNullOrEmpty(m.FormatSourceLocation)) then
                    a [Href (m.FormatSourceLocation); Class"fsdocs-source-link" ] [
                      img [Src (sprintf "%scontent/img/github.png" root); Class "normal"]
                      img [Src (sprintf "%scontent/img/github-blue.png" root); Class "hover"]
                    ]
                  //if not (String.IsNullOrEmpty(m.Details.FormatCompiledName)) then
                  //    p [] [!!"CompiledName: "; code [] [!!m.Details.FormatCompiledName]]
               ]
            ]
          ]
        ]
    ]

  let renderEntities (entities: ApiDocEntity list) =
   [ if entities.Length > 0 then
      let hasTypes = entities |> List.exists (fun e -> e.IsTypeDefinition)
      let hasModules = entities |> List.exists (fun e -> not e.IsTypeDefinition)
      table [Class "table outer-list fsdocs-entity-list" ] [
        thead [] [
          tr [] [
            td [] [!! (if hasTypes && hasModules then "Type/Module" elif hasTypes then "Type" else "Modules")]
            td [] [!!"Description"]
          ]
        ]
        tbody [] [
          for e in entities do 
            tr [] [
               td [Class "fsdocs-entity-name"] [
                 let nm = e.Name 
                 let multi = (entities |> List.filter (fun e -> e.Name = nm) |> List.length) > 1
                 let nmWithSiffix = if multi then (if e.IsTypeDefinition then nm + " (Type)" else nm + " (Module)") else nm

                 // This adds #EntityName anchor. These may currently be ambiguous
                 a [Name nm] [a [Href (e.Url(root, collectionName, qualify))] [!!nmWithSiffix]]
               ]
               td [Class "fsdocs-xmldoc" ] [
                   //let isObsolete = e.IsObsolete 
                   //let obs = e.ObsoleteMessage 
                   //if isObsolete then
                   //  obsoleteMessage obs
                   embed e.Comment.SummaryHtml
               ]
            ]
        ]
      ]
   ]

  // Honour the CategoryIndex to put the categories in the right order
  let getSortedCategories xs exclude category categoryIndex =
    xs
    |> List.filter (fun x -> not (exclude x))
    |> List.groupBy (fun x -> category x)
    |> List.map (fun (cat, xs) -> (cat, xs, xs |> List.minBy (fun x -> categoryIndex x)))
    |> List.sortBy (fun (cat, _xs, x) -> categoryIndex x, cat)
    |> List.map (fun (cat, xs, _x) -> cat, xs)

  let entityContent (info: ApiDocEntityInfo) =
    // Get all the members & comment for the type
    let entity = info.Entity
    let members = entity.AllMembers |> List.filter (fun e -> not e.IsObsolete)
    let comment = entity.Comment

    // Group all members by their category 
    let byCategory =
      getSortedCategories members  (fun m -> m.Exclude) (fun m -> m.Category) (fun m -> m.CategoryIndex)
      |> List.mapi (fun i (key, elems) ->
          let elems = elems |> List.sortBy (fun m -> m.Name)
          let name = if String.IsNullOrEmpty(key) then  "Other module members" else key
          (i, key, elems, name))
  
    let usageName =
        match info.ParentModule with
        | Some m when m.RequiresQualifiedAccess -> m.Name + "." + entity.Name
        | _ -> entity.Name

    [ h1 [] [!! (usageName + (if entity.IsTypeDefinition then " Type" else " Module")) ]
      p [] [!! ("Namespace: " + info.Namespace.Name)]
      p [] [!! ("Assembly: " + entity.Assembly.Name + ".dll")]

      match info.ParentModule with
      | None -> ()
      | Some parentModule ->
        span [] [!! ("Parent Module: "); a [Href (parentModule.Url(root, collectionName, qualify))] [!! parentModule.Name ]]
  

      match entity.AbbreviatedType with
      | Some abbreviatedTyp ->
         p [] [!! "Abbreviation For: "; embed abbreviatedTyp]
          
      | None ->  ()

      match entity.BaseType with
      | Some baseType ->
         p [] [!! "Base Type: "; embed baseType]
      | None -> ()

      match entity.AllInterfaces with
      | [] -> ()
      | l ->
         p [] [!! ("All Interfaces: ")]
         ul [] [ for i in l -> li [] [embed i] ]
         
      if entity.Symbol.IsValueType then
         p [] [!! ("Kind: Struct")]

      match entity.DelegateSignature with
      | Some d ->
          p [] [!! ("Delegate Signature: "); embed d]
      | None -> ()

      if entity.Symbol.IsProvided then
         p [] [!! ("This is a provided type definition")]

      if entity.Symbol.IsAttributeType then
         p [] [!! ("This is an attribute type definition")]

      if entity.Symbol.IsEnum then
         p [] [!! ("This is an enum type definition")]

      //if info.Entity.IsObsolete then
      //    obsoleteMessage entity.ObsoleteMessage
  
      div [Class "fsdocs-xmldoc" ] [
        // XML comment for the type has multiple sections that can be labelled
        // with categories (to give comment for an individual category). Here,
        // we print only those that belong to the <default>
        for sec in comment.Sections do
          if not (byCategory |> List.exists (fun (_, g, _, _) -> g = sec.Key)) then
            if (sec.Key <> "<default>") then 
              h3 [] [encode(sec.Key)]
          embed sec.Value
        ]
      if (byCategory.Length > 1) then
        // If there is more than 1 category in the type, generate TOC 
        h3 [] [!!"Table of contents"]
        ul [] [
          for (index, _, _, name) in byCategory do
            li [] [ a [Href ("#section" + index.ToString())] [!! name ] ]
        ]
 
      //<!-- Render nested types and modules, if there are any -->
  
      let nestedEntities =
         entity.NestedEntities
         |> List.filter (fun e -> not e.IsObsolete)
  
      if (nestedEntities.Length > 0) then
        div [] [
          h3 [] [!!  (if nestedEntities |> List.forall (fun e -> not e.IsTypeDefinition)  then "Nested modules"
                      elif nestedEntities |> List.forall (fun e -> e.IsTypeDefinition) then "Types"
                      else "Types and nested modules")]
          yield! renderEntities nestedEntities
        ]
 
      for (index, key, ms, name) in byCategory do
        // Iterate over all the categories and print members. If there are more than one
        // categories, print the category heading (as <h2>) and add XML comment from the type
        // that is related to this specific category.
        if (byCategory.Length > 1) then
           h2 [Id ("#section" + index.ToString())] [!! name]
           //<a name="@(" section" + g.Index.ToString())">&#160;</a></h2>
        let info = comment.Sections |> Seq.tryFind(fun kvp -> kvp.Key = key)
        match info with
        | None -> ()
        | Some key ->
           div [Class "fsdocs-xmldoc"] [ embed key.Value ]
        div [] (renderMembers "Functions and values" "Function or value" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ValueOrFunction)))
        div [] (renderMembers "Type extensions" "Type extension" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.TypeExtension)))
        div [] (renderMembers "Active patterns" "Active pattern" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ActivePattern)))
        div [] (renderMembers "Union cases" "Union case" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.UnionCase)))
        div [] (renderMembers "Record fields" "Record Field" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.RecordField)))
        div [] (renderMembers "Static parameters" "Static parameters" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticParameter)))
        div [] (renderMembers "Constructors" "Constructor" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.Constructor)))
        div [] (renderMembers "Instance members" "Instance member" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.InstanceMember)))
        div [] (renderMembers "Static members" "Static member" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticMember)))
    ]
 
    
  let categoriseEntities (nsIndex: int, ns: ApiDocNamespace) =
    let entities = ns.Entities
  
    let categories =
        getSortedCategories entities (fun m -> m.Exclude) (fun m -> m.Category) (fun m -> m.CategoryIndex)

    let allByCategory =
        [ for (catIndex, (categoryName, categoryEntities)) in Seq.indexed categories do
            let caegoryName = (if String.IsNullOrEmpty(categoryName) then "Other namespace members" else categoryName)
            let index = String.Format("{0}_{1}", nsIndex, catIndex)
            let categoryEntities =
                categoryEntities

                // Some bespoke hacks to make FSharp.Core docs look ok.
                // TODO: use <exclude /> to do these
                //
                // Remove FSharp.Data.UnitSystems.SI from display, it's just so rarely used, has long names and dominates the docs.
                // Find another way to document these
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols"))
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Data.UnitSystems.SI.UnitNames"))
                // Don't show 'AnonymousObject'
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.RuntimeHelpers" && e.Symbol.DisplayName = "AnonymousObject"))
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.QueryRunExtensions" && e.Symbol.DisplayName = "LowPriority"))
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.QueryRunExtensions" && e.Symbol.DisplayName = "HighPriority"))
                |> List.filter (fun e -> not e.IsObsolete)
                
                |> List.sortBy (fun e ->
                    (e.Symbol.DisplayName.ToLowerInvariant(), e.Symbol.GenericParameters.Count,
                        e.Name, (if e.IsTypeDefinition then e.UrlBaseName else "ZZZ")))

            if categoryEntities.Length > 0 then
                yield {| CategoryName = caegoryName; CategoryIndex = index; CategoryEntites = categoryEntities |} ]

    allByCategory

  let namespaceContent (nsIndex, ns: ApiDocNamespace) =
    let allByCategory = categoriseEntities (nsIndex, ns)
    [ if allByCategory.Length > 0 then
        h2 [Id ns.UrlHash] [!! (ns.Name + " Namespace") ]

        match ns.NamespaceSummary with
        | Some (nssummary, nsremarks) -> p [] [!! nssummary; !!nsremarks ]
        | None -> () 

        p [] [!! "Categories:" ]

        if (allByCategory.Length > 1) then
            ul [] [
               for category in allByCategory do
                   li [] [a [Href ("#category-" + category.CategoryIndex)] [!!category.CategoryName]]
            ]
 
        for category in allByCategory do
          if (allByCategory.Length > 1) then
             h3 [] [a [Class "anchor"; Name ("category-" + category.CategoryIndex); Href ("#category-" + category.CategoryIndex)] [!! category.CategoryName]]
          yield! renderEntities category.CategoryEntites
    ]  

  let listOfNamespacesAux otherDocs nav (nsOpt: ApiDocNamespace option) =
    [
        // For FSharp.Core we make all entries available to other docs else there's not a lot else to show.
        //
        // For non-FSharp.Core we only show one link "API Reference" in the nav menu 
      if otherDocs && nav && model.Collection.CollectionName <> "FSharp.Core" then
          li [Class "nav-header"] [!! "API Reference"]
          li [ Class "nav-item"  ] [a [Class "nav-link"; Href (model.IndexFileUrl(root, collectionName, qualify))] [!! "All Namespaces" ] ] 
      else

      let categorise =
        [ for (nsIndex, ns) in Seq.indexed model.Collection.Namespaces do
             let allByCategory = categoriseEntities (nsIndex, ns)
             if allByCategory.Length > 0 then
                 allByCategory, ns ]

      let someExist = categorise.Length > 0 

      if someExist && nav then
        li [Class "nav-header"] [!! "Namespaces"]

      for allByCategory, ns in categorise do

          // Generate the entry for the namespace
          li [ if nav then
                    Class ("nav-item" + 
                          // add the 'active' class if this is the namespace of the thing being shown
                          match nsOpt with
                          | Some ns2 when ns.Name = ns2.Name -> " active"
                          | _ -> "") ]

                [span [] [
                    a [ if nav then
                          Class ("nav-link" +
                             // add the 'active' class if this is the namespace of the thing being shown
                             match nsOpt with
                             | Some ns2 when ns.Name = ns2.Name -> " active"
                             | _ -> "")
                        Href (ns.Url(root, collectionName, qualify))] [!!ns.Name]

                     // If not in the navigation list then generate the summary text as well
                    if not nav then
                       !! " - "
                       match ns.NamespaceSummary with
                       | Some (nssummary, _nsremarks) -> !! nssummary
                       | None -> () ] ]

          // In the navigation bar generate the expanded list of entities
          // for the active namespace
          if nav then
              match nsOpt with
              | Some ns2 when ns.Name = ns2.Name ->
                  ul [ Custom ("list-style-type", "none") (* Class "navbar-nav " *) ] [
                      for category in allByCategory do
                          for e in category.CategoryEntites do
                              li [ Class "nav-item"  ] [a [Class "nav-link"; Href (e.Url(root, collectionName, qualify))] [!! e.Name] ]
                  ]
              | _ -> ()
     ]

  let listOfNamespaces otherDocs nav (nsOpt: ApiDocNamespace option) =
     listOfNamespacesAux otherDocs nav nsOpt
     |> List.map (fun html -> html.ToString()) |> String.concat "             \n"

  /// Get the substitutions relevant to all
  member _.GlobalParameters : Parameters =
    let toc = listOfNamespaces true true None
    [ yield (ParamKeys.``fsdocs-list-of-namespaces``, toc )  ]

  member _.Generate(outDir: string, templateOpt, collectionName, globalParameters) =

    let getParameters parameters toc (content: HtmlElement) pageTitle =
        [| yield! parameters
           yield (ParamKeys.``fsdocs-list-of-namespaces``, toc )
           yield (ParamKeys.``fsdocs-content``, content.ToString() )
           yield (ParamKeys.``fsdocs-source``, "" )
           yield (ParamKeys.``fsdocs-tooltips``, "" )
           yield (ParamKeys.``fsdocs-page-title``, pageTitle )
           yield! globalParameters
           |]

    let collection = model.Collection
    begin
        let content =
           div [] [h1 [] [!! "API Reference"];
                   h2 [] [!! "Available Namespaces:"];
                   ul [] (listOfNamespacesAux false false None) ]
        let pageTitle = "API Reference" + collectionName
        let toc = listOfNamespaces false true None 
        let parameters = getParameters model.Parameters toc content pageTitle
        let outFile = Path.Combine(outDir, model.IndexOutputFile(collectionName, model.Qualify) )
        printfn "Generating %s" outFile
        SimpleTemplating.UseFileAsSimpleTemplate (parameters, templateOpt, outFile)
    end

    //printfn "Namespaces = %A" [ for ns in collection.Namespaces -> ns.Name ]

    for (nsIndex, ns) in Seq.indexed collection.Namespaces do
        let content = div [] (namespaceContent (nsIndex, ns))
        let pageTitle = ns.Name
        let toc = listOfNamespaces false true (Some ns)
        let parameters = getParameters model.Parameters toc content pageTitle
        let outFile = Path.Combine(outDir, ns.OutputFile(collectionName, model.Qualify) )
        printfn "Generating %s" outFile
        SimpleTemplating.UseFileAsSimpleTemplate (parameters, templateOpt, outFile)

    for info in model.EntityInfos do
        Log.infof "Generating type/module: %s" info.Entity.UrlBaseName
        let content = div [] (entityContent info)
        let pageTitle = info.Entity.Name + collectionName
        let toc = listOfNamespaces false true (Some info.Namespace)
        let parameters = getParameters info.Entity.Parameters toc content pageTitle
        let outFile = Path.Combine(outDir, info.Entity.OutputFile(collectionName, model.Qualify))
        printfn "Generating %s" outFile
        SimpleTemplating.UseFileAsSimpleTemplate (parameters, templateOpt, outFile)
        Log.infof "Finished %s" info.Entity.UrlBaseName

