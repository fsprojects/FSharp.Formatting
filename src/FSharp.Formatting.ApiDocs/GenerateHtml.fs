module internal FSharp.Formatting.ApiDocs.GenerateHtml

open System
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

let obsoleteMessage msg =
    div [Class "alert alert-warning"] [
        strong [] [!!"NOTE:"]
        p [] [!! ("This API is obsolete" + HttpUtility.HtmlEncode(msg))]
    ]

let nestedTypesAndModules (entities: Choice<FSharp.Formatting.ApiDocs.Type, Module> list) =
  [ if entities.Length > 0 then
      let hasTypes = entities |> List.exists (function Choice1Of2 _ -> true | _ -> false)
      let hasModules = entities |> List.exists (function Choice2Of2 _ -> true | _ -> false)
      table [Class "table table-bordered type-list module-list" ] [
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
                 let nm = match e with Choice1Of2 t -> t.Name | Choice2Of2 m -> m.Name
                 let urlnm = match e with Choice1Of2 t -> t.UrlName | Choice2Of2 m -> m.UrlName
                 let multi = (entities |> List.filter (function Choice1Of2 t -> t.Name = nm | Choice2Of2 m -> m.Name = nm) |> List.length) > 1
                 let nm = if multi then match e with Choice1Of2 _ -> nm + " (Type)" | Choice2Of2 _ -> nm + " (Module)" else nm
                 a [Href (urlnm + ".html")] [!!nm]
               ]
               td [Class "xmldoc" ] [
                   let isObsolete = match e with Choice1Of2 t -> t.IsObsolete | Choice2Of2 m -> m.IsObsolete
                   let obs = match e with Choice1Of2 t -> t.ObsoleteMessage | Choice2Of2 m -> m.ObsoleteMessage
                   let blurb = match e with Choice1Of2 t -> t.Comment.Blurb | Choice2Of2 m -> m.Comment.Blurb
                   if isObsolete then
                     obsoleteMessage obs
                   !! blurb
               ]
            ]
        ]
      ]
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
let renderMembers header tableHeader (members: Member list) =
   [ if members.Length > 0 then
       h3 [] [!! header]
       table [Class "table table-bordered member-list"] [
         thead [] [
           tr [] [
             td [] [ !!tableHeader ]
             td [] [ !! "Description" ]
           ]
         ]
         tbody [] [
           for m in members do
             tr [] [
               td [Class "member-name"] [
                 renderWithToolTip [
                   !! HttpUtility.HtmlEncode(m.Details.FormatUsage(40)) 
                 ] [
                    strong [] [!! "Signature:"]
                    !! HttpUtility.HtmlEncode(m.Details.Signature)
                    br []
                    if not m.Details.Modifiers.IsEmpty then
                      strong [] [!! "Modifiers:"]
                      !! HttpUtility.HtmlEncode(m.Details.FormatModifiers)
                      br []
                    if not (m.Details.TypeArguments.IsEmpty) then
                      strong [] [!!"Type parameters: "]
                      !!m.Details.FormatTypeArguments
                 ]
               ]
            
               td [Class "xmldoc"] [
                  !!m.Comment.FullText
                  if m.IsObsolete then
                      obsoleteMessage m.ObsoleteMessage
                  if not (String.IsNullOrEmpty(m.Details.FormatSourceLocation)) then
                    a [Href (m.Details.FormatSourceLocation); Class"github-link" ] [
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

let moduleContent (info: ModuleInfo) =
  // Get all the members & comment for the type
  let members = info.Module.AllMembers
  let comment = info.Module.Comment
  let entity = info.Module

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

  [ h1 [] [!! (entity.Name + " Module") ]
    p [] [!! ("Namespace: " + info.Namespace.Name)]
    p [] [!! ("Assembly: " + info.Assembly.Name)]
    br []
    match info.ParentModule with
    | None -> ()
    | Some parentModule ->
      span [] [!! ("Parent Module: "); a [Href (parentModule.UrlName + ".html")] [!! parentModule.Name ]]

    if info.Module.IsObsolete then
        obsoleteMessage entity.ObsoleteMessage

    div [Class "xmldoc" ] [
      // XML comment for the type has multiple sections that can be labelled
      // with categories (to give comment for an individual category). Here,
      // we print only those that belong to the <default>
      for sec in comment.Sections do
        if not (byCategory |> List.exists (fun (_, g, _, _) -> g = sec.Key)) then
          if (sec.Key <> "<default>") then 
            h2 [] [!!sec.Key]
        !! sec.Value 
      ]
    if (byCategory.Length > 1) then
      // If there is more than 1 category in the type, generate TOC 
      h2 [] [!!"Table of contents"]
      ul [] [
        for (index, _, _, name) in byCategory do
          li [] [ a [Href ("#section" + index.ToString())] [!! name ] ]
      ]

    //<!-- Render nested types and modules, if there are any -->

    let nestedEntities =
      [ for t in entity.NestedTypes -> Choice1Of2 t
        for m in entity.NestedModules -> Choice2Of2 m ]

    if (nestedEntities.Length > 0) then
      div [] [
        h2 [] [!!"Types and modules"]
        yield! nestedTypesAndModules nestedEntities
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
      div [] (renderMembers "Functions and values" "Function or value" (ms |> List.filter (fun m -> m.Kind = MemberKind.ValueOrFunction)))
      div [] (renderMembers "Type extensions" "Type extension" (ms |> List.filter (fun m -> m.Kind = MemberKind.TypeExtension)))
      div [] (renderMembers "Active patterns" "Active pattern" (ms |> List.filter (fun m -> m.Kind = MemberKind.ActivePattern)))
  ]

let typeContent (info: TypeInfo) =
  let members = info.Type.AllMembers
  let comment = info.Type.Comment
  let entity = info.Type
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
        let ms = ms |> List.sortBy(fun m -> if (m.Kind = MemberKind.StaticParameter) then "" else m.Name)
        let name = (if String.IsNullOrEmpty(g) then "Other type members" else g)
        (n, g, ms, name))

  [ h1 [] [!! (entity.Name + " Type")]
    p [] [!! ("Namespace: " + info.Namespace.Name)]
    p [] [!! ("Assembly: " + info.Assembly.Name)]
    br []
    match info.ParentModule with
    | None -> ()
    | Some parentModule ->
      span [] [!! ("Parent Module: "); a [Href (parentModule.UrlName + ".html")] [!! parentModule.Name ]]

    if (entity.IsObsolete) then
        obsoleteMessage entity.ObsoleteMessage

    div [Class "xmldoc" ] [
      // XML comment for the type has multiple sections that can be labelled
      // with categories (to give comment for an individual category). Here,
      // we print only those that belong to the <default>
      for sec in comment.Sections do
        if not (byCategory |> List.exists (fun (_, g, _, _) -> g = sec.Key)) then
          if (sec.Key <> "<default>") then 
            h2 [] [!!sec.Key]
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
      div [] (renderMembers "Union cases" "Union case" (ms |> List.filter (fun m -> m.Kind = MemberKind.UnionCase)))
      div [] (renderMembers "Record fields" "Record Field" (ms |> List.filter (fun m -> m.Kind = MemberKind.RecordField)))
      div [] (renderMembers "Static parameters" "Static parameters" (ms |> List.filter (fun m -> m.Kind = MemberKind.StaticParameter)))
      div [] (renderMembers "Constructors" "Constructor" (ms |> List.filter (fun m -> m.Kind = MemberKind.Constructor)))
      div [] (renderMembers "Instance members" "Instance member" (ms |> List.filter (fun m -> m.Kind = MemberKind.InstanceMember)))
      div [] (renderMembers "Static members" "Static member" (ms |> List.filter (fun m -> m.Kind = MemberKind.StaticMember)))
  ]
    
let namespacesContent (asm: AssemblyGroup) =
  [ h1 [] [!! asm.Name]
    for (nsIndex, ns) in Seq.indexed asm.Namespaces do
      let entities =
        [ for t in ns.Types -> Choice1Of2 t
          for m in ns.Modules -> Choice2Of2 m ]

      let entitiesByCategory =
        [ for e in entities ->
             match e with
             | Choice1Of2 t -> t.Category
             | Choice2Of2 m -> m.Category ]
        |> List.distinct
        |> List.sortBy (fun s -> if String.IsNullOrEmpty(s) then "ZZZ" else s)

      let allByCategory =
          [ for (catIndex, c) in Seq.indexed entitiesByCategory do
                let name = (if String.IsNullOrEmpty(c) then "Other namespace members" else c)
                let index = String.Format("{0}_{1}", nsIndex, catIndex)
                let entities =
                    entities
                    |> List.filter (fun e ->
                        let cat =
                            match e with
                            | Choice1Of2 t -> t.Category
                            | Choice2Of2 m -> m.Category
                        cat = c)
                    |> List.sortBy (fun e ->
                        match e with
                        | Choice1Of2 t -> (t.Name, t.UrlName)
                        | Choice2Of2 m -> (m.Name, "ZZZ")
                    )
                if entities.Length > 0 then
                    yield (name, index, entities) ]

      h2 [] [!! (ns.Name + " Namespace") ]
      if (allByCategory.Length > 1) then
          ul [] [
             for (name, index, entities) in allByCategory do
                 li [] [a [Href ("#section" + index)] [!!name]]
          ]
 
      for (name, index, entities) in allByCategory do
        if (allByCategory.Length > 1) then
           h3 [] [a [Class "anchor"; Name ("section" + index); Href ("#section" + index)] [!! name]]
        yield! nestedTypesAndModules entities
    ]

let Generate(model: ApiDocsModel, outDir: string, templateOpt) =
    let (@@) a b = Path.Combine(a, b)

    let props = (dict model.Properties).["Properties"]
    let projectName = if props.ContainsKey "project-name" then " - " + props.["project-name"] else ""
    let contentTag = "document"
    let pageTitleTag = "page-title"

    let getParameters (content: HtmlElement) pageTitle =
        [| for KeyValue(k,v) in props -> (k, v)
           yield (contentTag, content.ToString() )
           yield (pageTitleTag, pageTitle ) |]

    let content = div [] (namespacesContent model.AssemblyGroup)
    let outFile = outDir @@ "index.html"
    let pageTitle = "API Reference" + projectName
    let parameters = getParameters content pageTitle
    printfn "Generating %s" outFile
    HtmlFile.UseFileAsSimpleTemplate (contentTag, parameters, templateOpt, outFile)

    for modulInfo in model.ModuleInfos do
        Log.infof "Generating module: %s" modulInfo.Module.UrlName
        let content = div [] (moduleContent modulInfo)
        let outFile = outDir @@ (modulInfo.Module.UrlName + ".html")
        let pageTitle = modulInfo.Module.Name + projectName
        let parameters = getParameters content pageTitle
        printfn "Generating %s" outFile
        HtmlFile.UseFileAsSimpleTemplate (contentTag, parameters, templateOpt, outFile)
        Log.infof "Finished module: %s" modulInfo.Module.UrlName

    for info in model.TypesInfos do
        Log.infof "Generating type: %s" info.Type.UrlName
        let content = div [] (typeContent info)
        let outFile = outDir @@ (info.Type.UrlName + ".html")
        let pageTitle = info.Type.Name + projectName
        let parameters = getParameters content pageTitle

        printfn "Generating %s" outFile
        HtmlFile.UseFileAsSimpleTemplate (contentTag, parameters, templateOpt, outFile)
        Log.infof "Finished type: %s" info.Type.UrlName
