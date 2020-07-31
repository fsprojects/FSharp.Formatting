module internal FSharp.Formatting.ApiDocs.GenerateHtml

open System
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

type HtmlRender(markDownComments) =
  let sigWidth = 300
  let obsoleteMessage msg =
    div [Class "alert alert-warning"] [
        strong [] [!!"NOTE:"]
        p [] [!! ("This API is obsolete" + HttpUtility.HtmlEncode(msg))]
    ]

  let mutable uniqueNumber = 0
  let UniqueID() =
    uniqueNumber <- uniqueNumber + 1
    uniqueNumber


  let renderWithToolTip content tip =
    div [] [
      let id = UniqueID().ToString()
      code [ OnMouseOut  (sprintf "hideTip(event, '%s', %s)" id id)
             OnMouseOver (sprintf "showTip(event, '%s', %s)" id id)] content
      div [Class "tip"; Id id ] tip
    ]

  let renderMembers header tableHeader (members: ApiDocMember list) =
   [ if members.Length > 0 then
       h3 [] [!! header]
       table [Class "table outer-list member-list"] [
         thead [] [
           tr [] [
             td [Class "member-list-header"] [ !!tableHeader ]
             td [Class "member-list-header"] [ !! "Description" ]
           ]
         ]
         tbody [] [
           for m in members do
             tr [] [
               td [Class "member-name"; Id m.Name] [
                  
                  renderWithToolTip [
                      // This adds #MemberName anchor. These may currently be ambiguous
                      !! HttpUtility.HtmlEncode(m.FormatUsage(sigWidth)) 
                    ]
                    [
                      div [Class "member-tooltip"] [
                        !! "Full Usage: "
                        br []
                        !! HttpUtility.HtmlEncode(m.UsageTooltip)
                        if not m.ParameterTooltips.IsEmpty then
                            !! "Parameter Types: "
                            ul [] [
                              for (pname, ptyp) in m.ParameterTooltips do
                                li [] [
                                  b [] [!! pname]
                                  !! ":"
                                  !! HttpUtility.HtmlEncode(ptyp)
                                ]
                            ]
                        br []
                        match m.ReturnTooltip with
                        | None -> ()
                        | Some t ->
                            !! "Return Type: "
                            !! HttpUtility.HtmlEncode(t)
                            br []
                        !! "Signature: "
                        !! HttpUtility.HtmlEncode(m.SignatureTooltip)
                        br []
                        if not m.Modifiers.IsEmpty then
                          !! "Modifiers: "
                          !! HttpUtility.HtmlEncode(m.FormatModifiers)
                          br []

                          // We suppress the display of ill-formatted type parameters for places
                          // where these have not been explicitly declared
                          match m.FormatTypeArguments with
                          | None -> ()
                          | Some v -> 
                              !!"Type parameters: "
                              !! HttpUtility.HtmlEncode(v)
                      ]
                    ]
               ]
            
               td [Class "xmldoc"] [
                  if not (String.IsNullOrWhiteSpace(m.Comment.FullText)) then
                      !! m.Comment.FullText.Trim()
                      br []
                  match m.ExtendedType with
                  | Some s ->
                      !! "Extended Type: "
                      code [Class "code-type"] [!! HttpUtility.HtmlEncode(s)]
                      br []
                  | _ -> ()
                  if not m.ParameterTooltips.IsEmpty then
                      !! "Parameter Types: "
                      ul [] [
                          for (pname, ptyp) in m.ParameterTooltips do
                          li [] [
                              code [] [!! pname]
                              !! ":"
                              code [Class "code-type"] [!! HttpUtility.HtmlEncode(ptyp) ]
                          ]
                      ]
                  match m.ReturnTooltip with
                  | None -> ()
                  | Some t ->
                      !! "Return Type: "
                      code [Class "code-type"] [!! HttpUtility.HtmlEncode(t)]
                      br []

                  if m.IsObsolete then
                      obsoleteMessage m.ObsoleteMessage

                  if not (String.IsNullOrEmpty(m.FormatSourceLocation)) then
                    a [Href (m.FormatSourceLocation); Class"github-link" ] [
                      img [Src "../content/img/github.png"; Class "normal"]
                      img [Src "../content/img/github-blue.png"; Class "hover"]
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
      table [Class "table outer-list entity-list" ] [
        thead [] [
          tr [] [
            td [] [!! (if hasTypes && hasModules then "Type/Module" elif hasTypes then "Type" else "Modules")]
            td [] [!!"Description"]
          ]
        ]
        tbody [] [
          for e in entities do 
            tr [] [
               td [Class "type-name"] [
                 let nm = e.Name 
                 let multi = (entities |> List.filter (fun e -> e.Name = nm) |> List.length) > 1
                 let nmWithSiffix = if multi then (if e.IsTypeDefinition then nm + " (Type)" else nm + " (Module)") else nm

                 // This adds #EntityName anchor. These may currently be ambiguous
                 a [Name nm] [a [Href (e.UrlBaseName + ".html")] [!!nmWithSiffix]]
               ]
               td [Class "xmldoc" ] [
                   let isObsolete = e.IsObsolete 
                   let obs = e.ObsoleteMessage 
                   let blurb = e.Comment.Summary
                   if isObsolete then
                     obsoleteMessage obs
                   !! blurb
               ]
            ]
        ]
      ]
   ]

  let moduleContent (info: ApiDocEntityInfo) =
    // Get all the members & comment for the type
    let members = info.Entity.AllMembers
    let comment = info.Entity.Comment
    let entity = info.Entity

    // Group all members by their category which is an inline annotation
    // that can be added to members using special XML comment:
    //
    //     /// [category:Something]
    //
    // ...and can be used to categorize members in large modules or types
    // (but if this is not used, then all members end up in just one category)
    let byCategory =
      members 
      |> List.groupBy(fun m -> m.Category)
      |> List.sortBy (fun (key, _) -> if String.IsNullOrEmpty(key) then "ZZZ" else key)
      |> List.mapi (fun n (key, elems) ->
          let elems= elems |> List.sortBy (fun m -> m.Name)
          let name = if String.IsNullOrEmpty(key) then  "Other module members" else key
          (n, key, elems, name))
  
    let usageName =
        match info.ParentModule with
        | Some m when m.RequiresQualifiedAccess -> m.Name + "." + entity.Name
        | _ -> entity.Name

    [ h1 [] [!! (usageName + " Module") ]
      p [] [!! ("Namespace: " + info.Namespace.Name)]
      p [] [!! ("Assembly: " + entity.Assembly.Name + ".dll")]
      br []
      match info.ParentModule with
      | None -> ()
      | Some parentModule ->
        span [] [!! ("Parent Module: "); a [Href (parentModule.UrlBaseName + ".html")] [!! parentModule.Name ]]
  
      if info.Entity.IsObsolete then
          obsoleteMessage entity.ObsoleteMessage
  
      div [Class "xmldoc" ] [
        // XML comment for the type has multiple sections that can be labelled
        // with categories (to give comment for an individual category). Here,
        // we print only those that belong to the <default>
        for sec in comment.Sections do
          if not (byCategory |> List.exists (fun (_, g, _, _) -> g = sec.Key)) then
            if (sec.Key <> "<default>") then 
              h3 [] [!! HttpUtility.HtmlEncode(sec.Key)]
          !! sec.Value 
        ]
      if (byCategory.Length > 1) then
        // If there is more than 1 category in the type, generate TOC 
        h3 [] [!!"Table of contents"]
        ul [] [
          for (index, _, _, name) in byCategory do
            li [] [ a [Href ("#section" + index.ToString())] [!! name ] ]
        ]
 
      //<!-- Render nested types and modules, if there are any -->
  
      let nestedEntities = entity.NestedEntities
  
      if (nestedEntities.Length > 0) then
        div [] [
          h3 [] [!!  (if nestedEntities |> List.forall (fun e -> not e.IsTypeDefinition)  then "Nested modules"
                      elif nestedEntities |> List.forall (fun e -> e.IsTypeDefinition) then "Types"
                      else "Types and nested modules")]
          yield! renderEntities nestedEntities
        ]
 
      for (n, key, ms, name) in byCategory do
        // Iterate over all the categories and print members. If there are more than one
        // categories, print the category heading (as <h2>) and add XML comment from the type
        // that is related to this specific category.
        if (byCategory.Length > 1) then
           h2 [] [!! name]
           //<a name="@(" section" + g.Index.ToString())">&#160;</a></h2>
        let info = comment.Sections |> Seq.tryFind(fun kvp -> kvp.Key = key)
        match info with
        | None -> ()
        | Some key ->
           div [Class "xmldoc"] [ !! key.Value ]
        div [] (renderMembers "Functions and values" "Function or value" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ValueOrFunction)))
        div [] (renderMembers "Type extensions" "Type extension" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.TypeExtension)))
        div [] (renderMembers "Active patterns" "Active pattern" (ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ActivePattern)))
    ]
 
  let typeContent (info: ApiDocEntityInfo) =
    let members = info.Entity.AllMembers
    let comment = info.Entity.Comment
    let entity = info.Entity

      // Group all members by their category which is an inline annotation
      // that can be added to members using special XML comment:
      //
      //     /// [category:Something]
      //
      // ...and can be used to categorize members in large modules or types
      // (but if this is not used, then all members end up in just one category)
    let byCategory =
      members
      |> List.groupBy(fun m -> m.Category)
      |> List.sortBy(fun (g, ms) -> if String.IsNullOrEmpty(g) then "ZZZ" else g)
      |> List.mapi (fun n (g, ms) -> 
          let ms = ms |> List.sortBy(fun m -> if (m.Kind = ApiDocMemberKind.StaticParameter) then "" else m.Name)
          let name = (if String.IsNullOrEmpty(g) then "Other type members" else g)
          (n, g, ms, name))
  
    let usageName =
        match info.ParentModule with
        | Some m when m.RequiresQualifiedAccess -> m.Name + "." + entity.Name
        | _ -> entity.Name

    [ h1 [] [!! (usageName + " Type")]
      p [] [!! ("Namespace: " + info.Namespace.Name)]
      p [] [!! ("Assembly: " + entity.Assembly.Name + ".dll")]
      br []
      match info.ParentModule with
      | None -> ()
      | Some parentModule ->
        span [] [!! ("Parent Module: "); a [Href (parentModule.UrlBaseName + ".html")] [!! parentModule.Name ]]
  
      match entity.AbbreviatedType with
      | Some abbreviatedTyp ->
         p [] [!! HttpUtility.HtmlEncode("Abbreviation For: " + abbreviatedTyp)]
          
      | None ->  ()
      match entity.BaseType with
      | Some baseType ->
         p [] [!! HttpUtility.HtmlEncode("Base Type: " + baseType)]
      | None -> ()
      match entity.AllInterfaces with
      | [] -> ()
      | l ->
         p [] [!! ("All Interfaces: ")]
         ul [] [ for i in l -> li [] [!! HttpUtility.HtmlEncode(i)] ]
               
      if entity.Symbol.IsValueType then
         p [] [!! ("Kind: Struct")]

      match entity.DelegateSignature with
      | Some d ->
          p [] [!! ("Kind: Delegate")]
          code  [] [!! HttpUtility.HtmlEncode(d)]
      | None -> ()

      if entity.Symbol.IsProvided then
         p [] [!! ("This is a provided type definition")]

      if entity.Symbol.IsAttributeType then
         p [] [!! ("This is an attribute type definition")]

      if entity.Symbol.IsEnum then
         p [] [!! ("This is an enum type definition")]

      if entity.IsObsolete then
          obsoleteMessage entity.ObsoleteMessage
  
      div [Class "xmldoc" ] [
        // XML comment for the type has multiple sections that can be labelled
        // with categories (to give comment for an individual category). Here,
        // we print only those that belong to the <default>
        for sec in comment.Sections do
          if not (byCategory |> List.exists (fun (_, g, _, _) -> g = sec.Key)) then
            if (sec.Key <> "<default>") then 
              h2 [] [!! HttpUtility.HtmlEncode(sec.Key)]
          !! sec.Value 
      ]
      if (byCategory.Length > 1) then
        // If there is more than 1 category in the type, generate TOC 
        h2 [] [!!"Table of contents"]
        ul [] [
          for (index, _, _, name) in byCategory do
            li [] [ a [Href ("#section" + index.ToString())] [!! name ] ]
        ]
 
      for (n, key, ms, name) in byCategory do
        // Iterate over all the categories and print members. If there are more than one
        // categories, print the category heading (as <h2>) and add XML comment from the type
        // that is related to this specific category.
        if (byCategory.Length > 1) then
           h2 [] [!! name]
           //<a name="@(" section" + g.Index.ToString())">&#160;</a></h2>
        let info = comment.Sections |> Seq.tryFind(fun kvp -> kvp.Key = key)
        match info with
        | None -> ()
        | Some key ->
           div [Class "xmldoc"] [ !! key.Value ]
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
        [ for e in entities -> e.Category ]
        |> List.distinct
        |> List.sortBy (fun s -> if String.IsNullOrEmpty(s) then "ZZZ" else s)

    let allByCategory =
        [ for (catIndex, c) in Seq.indexed categories do
            let name = (if String.IsNullOrEmpty(c) then "Other namespace members" else c)
            let index = String.Format("{0}_{1}", nsIndex, catIndex)
            let entities =
                entities
                |> List.filter (fun e ->
                    let cat = e.Category
                    cat = c)
                // Remove the funky array type definitions in FSharp.Core from display
                |> List.filter (fun e -> not e.Symbol.IsArrayType)
                // Remove the obsolete lazy<t> type definition in FSharp.Core from display
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Core" && e.Symbol.DisplayName = "lazy"))
                // Remove the List<t> type definition in FSharp.Core from display,
                // the type 't list is canonical
                |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Core" && e.Symbol.DisplayName = "List"))
                |> List.sortBy (fun e ->
                    (e.Symbol.DisplayName.ToLowerInvariant(), e.Symbol.GenericParameters.Count,
                        e.Name, (if e.IsTypeDefinition then e.UrlBaseName else "ZZZ")))
            if entities.Length > 0 then
                yield (name, index, entities) ]
    allByCategory

  let namespaceContent (nsIndex, ns: ApiDocNamespace) =
    let allByCategory = categoriseEntities (nsIndex, ns)
    [   h2 [Id ns.UrlHash] [!! (ns.Name + " Namespace") ]
        if (allByCategory.Length > 1) then
            ul [] [
               for (name, index, entities) in allByCategory do
                   li [] [a [Href ("#section" + index)] [!!name]]
            ]
 
        for (name, index, entities) in allByCategory do
          if (allByCategory.Length > 1) then
             h3 [] [a [Class "anchor"; Name ("section" + index); Href ("#section" + index)] [!! name]]
          yield! renderEntities entities
      ]  

  let namespacesContent (asm: ApiDocAssemblyGroup) =
    [ h1 [] [!! asm.Name]
      for (nsIndex, ns) in Seq.indexed asm.Namespaces do
          yield! namespaceContent (nsIndex, ns) ]

  let tableOfContents (asm: ApiDocAssemblyGroup) (nsOpt: ApiDocNamespace option) =
    [  //ul [ Custom ("list-style-type", "none") ] [
       for (nsIndex, ns) in Seq.indexed asm.Namespaces do
         let allByCategory = categoriseEntities (nsIndex, ns)

         li [] [a [Href ns.UrlFileNameAndHash] [!!ns.Name]]
         match nsOpt with
         | Some ns2 when ns.Name = ns2.Name ->
             ul [ Custom ("list-style-type", "none") ] [
                 for (name, index, entities) in allByCategory do
                     //if (allByCategory.Length > 1) then
                     //    dt [] [dd [] [a [Href ("#section" + index)] [!! name]]]
                     for e in entities do
                         li [] [a [Href (e.UrlBaseName + ".html")] [!! e.Name] ]
             ]
         | _ -> ()
       //]
     ]

  member _.Generate(model: ApiDocsModel, outDir: string, templateOpt) =
    let (@@) a b = Path.Combine(a, b)
    let props = (dict model.Properties).["Properties"]
    let projectName = if props.ContainsKey "project-name" then " - " + props.["project-name"] else ""
    let contentTag = "document"
    let tocTag = "table-of-contents"
    let pageTitleTag = "page-title"

    let getParameters (content: HtmlElement) (toc: HtmlElement) pageTitle =
        [| for KeyValue(k,v) in props -> (k, v)
           yield (contentTag, content.ToString() )
           yield (tocTag, toc.ToString() )
           yield ("tooltips", "" )
           yield (pageTitleTag, pageTitle ) |]

    let asm = model.AssemblyGroup
    begin
        let content = div [] (namespacesContent asm)
        let outFile = outDir @@ "index.html"
        let pageTitle = "API Reference" + projectName
        let toc = div [] (tableOfContents asm None)
        let parameters = getParameters content toc pageTitle
        printfn "Generating %s" outFile
        HtmlFile.UseFileAsSimpleTemplate (contentTag, parameters, templateOpt, outFile)
    end

    for info in model.EntityInfos do
        Log.infof "Generating type/module: %s" info.Entity.UrlBaseName
        let content =
            if info.Entity.IsTypeDefinition then
                div [] (typeContent info)
            else
                div [] (moduleContent info)
        let outFile = outDir @@ (info.Entity.UrlBaseName + ".html")
        let pageTitle = info.Entity.Name + projectName
        let toc = div [] (tableOfContents asm (Some info.Namespace))
        let parameters = getParameters content toc pageTitle
        printfn "Generating %s" outFile
        HtmlFile.UseFileAsSimpleTemplate (contentTag, parameters, templateOpt, outFile)
        Log.infof "Finished %s" info.Entity.UrlBaseName

