/// Internal module that generates Markdown documentation output from an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocModel"/>.
/// Produces one Markdown file per namespace and per entity, plus an index file.
module internal FSharp.Formatting.ApiDocs.GenerateMarkdown

open System
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Formatting.Markdown
open FSharp.Formatting.Markdown.Dsl
open FSharp.Formatting.Templating

/// HTML-encodes a string and additionally escapes pipe characters for safe use in Markdown tables.
let encode (x: string) =
    HttpUtility.HtmlEncode(x).Replace("|", "&#124;")

/// URL-encodes a string (for use in anchor hrefs).
let urlEncode (x: string) = HttpUtility.UrlEncode x
/// Returns the trimmed HTML text of an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value.
let htmlString (x: ApiDocHtml) = (x.HtmlText.Trim())

/// Returns the trimmed HTML text of an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value,
/// with newlines replaced by <c>&lt;br /&gt;</c> and pipe characters escaped for Markdown tables.
let htmlStringSafe (x: ApiDocHtml) =
    (x.HtmlText.Trim()).Replace("\n", "<br />").Replace("|", "&#124;")

/// Wraps an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value as a Markdown DSL node.
let embed (x: ApiDocHtml) = !!(htmlString x)
/// Wraps an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> value as a Markdown DSL node,
/// escaping characters that would break Markdown table cells.
let embedSafe (x: ApiDocHtml) = !!(htmlStringSafe x)
/// A Markdown DSL node representing an HTML line break.
let br = !!"<br />"

/// Renders Markdown API documentation for all namespaces and entities in an
/// <see cref="T:FSharp.Formatting.ApiDocs.ApiDocModel"/>. Writes one file per namespace
/// and per entity to <paramref name="outDir"/> using the supplied template.
type MarkdownRender(model: ApiDocModel, ?menuTemplateFolder: string) =
    let root = model.Root
    let collectionName = model.Collection.CollectionName
    let qualify = model.Qualify

    /// Renders a source-code link icon for a member or entity, returning an empty list when no URL is available.
    let sourceLink url =
        [ match url with
          | None -> ()
          | Some href -> link [ img "Link to source code" (sprintf "%scontent/img/github.png" root) ] (href) ]

    /// Renders a Markdown section for a group of API members with the given section header.
    let renderMembers header (members: ApiDocMember list) =
        [ if members.Length > 0 then
              ``###`` [ !!header ]

              for m in members do
                  // HTML anchor so existing per-member links (#Name) continue to work
                  p [ !!(sprintf "<a name=\"%s\"></a>" (urlEncode m.Name)) ]

                  // Member heading containing the full usage signature
                  ``####`` [ embed m.UsageHtml ]

                  let summary = m.Comment.Summary

                  if not (summary.HtmlText |> String.IsNullOrWhiteSpace) then
                      p [ embed m.Comment.Summary ]

                  match m.Comment.Remarks with
                  | None -> ()
                  | Some r -> p [ embed r ]

                  if not m.Parameters.IsEmpty then
                      p [ strong [ !!"Parameters:" ] ]

                      for parameter in m.Parameters do
                          p [ strong [ !!parameter.ParameterNameText ]; !!": "; embed parameter.ParameterType ]

                          match parameter.ParameterDocs with
                          | None -> ()
                          | Some d -> p [ embed d ]

                  match m.ExtendedType with
                  | None -> ()
                  | Some(_, extendedTypeHtml) -> p [ !!"Extended Type: "; embed extendedTypeHtml ]

                  match m.ReturnInfo.ReturnType with
                  | None -> ()
                  | Some(_, returnTypeHtml) ->
                      p
                          [ !!(if m.Kind <> ApiDocMemberKind.RecordField then
                                   "Returns: "
                               else
                                   "Field type: ")
                            embed returnTypeHtml ]

                      match m.ReturnInfo.ReturnDocs with
                      | None -> ()
                      | Some r -> p [ embed r ]

                  match m.FormatTypeArguments with
                  | None -> ()
                  | Some v ->
                      if m.TypeConstraintDisplayMode = TypeConstraintDisplayMode.Short then
                          match m.FormatShortTypeConstraints with
                          | None -> p [ !!("Type parameters: " + v) ]
                          | Some c -> p [ !!(sprintf "Type parameters: %s (requires %s)" v c) ]
                      else
                          p [ !!("Type parameters: " + v) ]

                  if m.TypeConstraintDisplayMode = TypeConstraintDisplayMode.Full then
                      match m.FormatTypeConstraints with
                      | None -> ()
                      | Some c -> p [ !!"Constraints: "; !!c ]

                  if not m.Comment.Exceptions.IsEmpty then
                      for (nm, url, html) in m.Comment.Exceptions do
                          p
                              [ match url with
                                | None -> ()
                                | Some href -> link [ !!nm ] href
                                embed html ]

                  for e in m.Comment.Notes do
                      ``#####`` [ !!"Note" ]
                      p [ embed e ]

                  for e in m.Comment.Examples do
                      ``#####`` [ !!"Example" ]
                      p [ embed e ]

                  let sl = sourceLink m.SourceLocation

                  if not sl.IsEmpty then
                      p sl ]

    /// Renders a Markdown table listing the given entities (types and modules) with links and summaries.
    let renderEntities (entities: ApiDocEntity list) =
        [ if entities.Length > 0 then
              let hasTypes = entities |> List.exists (fun e -> e.IsTypeDefinition)

              let hasModules = entities |> List.exists (fun e -> not e.IsTypeDefinition)

              table
                  [ [ p
                          [ !!(if hasTypes && hasModules then "Type/Module"
                               elif hasTypes then "Type"
                               else "Modules") ]
                      p [ !!"Description" ]
                      p [ !!"Source" ] ] ]
                  [ AlignLeft; AlignLeft; AlignCenter ]
                  [ let nameCounts = entities |> List.countBy (fun e -> e.Name) |> dict

                    for e in entities do
                        [ [ p
                                [ let nm = e.Name

                                  let multi = nameCounts.[nm] > 1

                                  let nmWithSiffix =
                                      if multi then
                                          (if e.IsTypeDefinition then
                                               nm + " (Type)"
                                           else
                                               nm + " (Module)")
                                      else
                                          nm

                                  link
                                      [ !!nmWithSiffix ]
                                      (e.Url(root, collectionName, qualify, model.FileExtensions.InUrl)) ] ]
                          [ p [ embedSafe e.Comment.Summary ] ]
                          [ p [ yield! (sourceLink e.SourceLocation) ] ] ] ] ]

    /// Generates the full Markdown content for a single entity (type or module) page,
    /// including its summary, members grouped by category, and nested entities.
    let entityContent (info: ApiDocEntityInfo) =
        // Get all the members & comment for the type
        let entity = info.Entity

        let members = entity.AllMembers |> List.filter (fun e -> not e.IsObsolete)

        let byCategory = Categorise.getMembersByCategory members

        let usageName =
            match info.ParentModule with
            | Some m when m.RequiresQualifiedAccess -> m.Name + "." + entity.Name
            | _ -> entity.Name

        [ ``##`` [ !!(usageName + (if entity.IsTypeDefinition then " Type" else " Module")) ]
          p
              [ !!"Namespace: "
                link
                    [ !!info.Namespace.Name ]
                    (info.Namespace.Url(root, collectionName, qualify, model.FileExtensions.InUrl)) ]
          p [ !!("Assembly: " + entity.Assembly.Name + ".dll") ]

          match info.ParentModule with
          | None -> ()
          | Some parentModule ->
              p
                  [ !!"Parent Module: "
                    link
                        [ !!parentModule.Name ]
                        (parentModule.Url(root, collectionName, qualify, model.FileExtensions.InUrl)) ]

          match entity.AbbreviatedType with
          | Some(_, abbreviatedTyp) -> p [ !!"Abbreviation For: "; embed abbreviatedTyp ]
          | None -> ()

          match entity.BaseType with
          | Some(_, baseType) -> p [ !!"Base Type: "; embed baseType ]
          | None -> ()

          match entity.AllInterfaces with
          | [] -> ()
          | l ->
              p
                  [ !!"All Interfaces: "
                    for (i, (_, interfaceTyHtml)) in Seq.indexed l do
                        if i <> 0 then
                            !!", "

                        embed interfaceTyHtml ]

          if entity.Symbol.IsValueType then
              p [ !!("Kind: Struct") ]

          match entity.DelegateSignature with
          | Some(_, delegateSigHtml) -> p [ !!("Delegate Signature: "); embed delegateSigHtml ]
          | None -> ()

          if entity.Symbol.IsProvided then
              p [ !!("This is a provided type definition") ]

          if entity.Symbol.IsAttributeType then
              p [ !!("This is an attribute type definition") ]

          if entity.Symbol.IsEnum then
              p [ !!("This is an enum type definition") ]

          //if info.Entity.IsObsolete then
          //    obsoleteMessage entity.ObsoleteMessage

          // Show the summary (and sectioned docs without any members)
          p [ embed entity.Comment.Summary ]

          // Show the remarks etc.
          match entity.Comment.Remarks with
          | Some r -> p [ embed r ]
          | None -> ()

          for note in entity.Comment.Notes do
              ``#####`` [ !!"Note" ]
              p [ embed note ]

          for example in entity.Comment.Examples do
              ``#####`` [ !!"Example" ]
              p [ embed example ]

          if (byCategory.Length > 1) then
              // If there is more than 1 category in the type, generate TOC
              ``###`` [ !!"Table of contents" ]

              ul
                  [ for (index, _, name) in byCategory do
                        [ p [ link [ !!(sprintf "#section%d" index) ] (name) ] ] ]

          //<!-- Render nested types and modules, if there are any -->

          let nestedEntities = entity.NestedEntities |> List.filter (fun e -> not e.IsObsolete)

          if (nestedEntities.Length > 0) then

              ``###``
                  [ !!(if nestedEntities |> List.forall (fun e -> not e.IsTypeDefinition) then
                           "Nested modules"
                       elif nestedEntities |> List.forall (fun e -> e.IsTypeDefinition) then
                           "Types"
                       else
                           "Types and nested modules") ]

              yield! renderEntities nestedEntities

          for (_, ms, name) in byCategory do
              // Iterate over all the categories and print members. If there are more than one
              // categories, print the category heading (as <h2>) and add XML comment from the type
              // that is related to this specific category.
              if (byCategory.Length > 1) then
                  ``##`` [ !!name ]

              let functionsOrValues = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ValueOrFunction)
              let extensions = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.TypeExtension)
              let activePatterns = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ActivePattern)
              let unionCases = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.UnionCase)
              let recordFields = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.RecordField)
              let staticParameters = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticParameter)
              let constructors = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.Constructor)
              let instanceMembers = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.InstanceMember)
              let staticMembers = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticMember)

              yield! renderMembers "Functions and values" functionsOrValues
              yield! renderMembers "Type extensions" extensions
              yield! renderMembers "Active patterns" activePatterns
              yield! renderMembers "Union cases" unionCases
              yield! renderMembers "Record fields" recordFields
              yield! renderMembers "Static parameters" staticParameters
              yield! renderMembers "Constructors" constructors
              yield! renderMembers "Instance members" instanceMembers
              yield! renderMembers "Static members" staticMembers

          let inheritedMemberGroups =
              entity.InheritedMembers
              |> List.choose (fun (baseTypeHtml, members) ->
                  let instMembers =
                      members
                      |> List.filter (fun m -> m.Kind = ApiDocMemberKind.InstanceMember && not m.IsObsolete)

                  let statMembers =
                      members
                      |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticMember && not m.IsObsolete)

                  if not (List.isEmpty instMembers) || not (List.isEmpty statMembers) then
                      Some(baseTypeHtml, instMembers, statMembers)
                  else
                      None)

          if not (List.isEmpty inheritedMemberGroups) then
              ``###`` [ !!"Inherited members" ]

              for (baseTypeHtml, instMembers, statMembers) in inheritedMemberGroups do
                  ``####`` [ !!"Inherited from "; embed baseTypeHtml ]
                  yield! renderMembers "Instance members" instMembers
                  yield! renderMembers "Static members" statMembers ]

    /// Generates the full Markdown content for a namespace page, including a table of contents
    /// and one section per category of entities.
    let namespaceContent (nsIndex, ns: ApiDocNamespace) =
        let allByCategory = Categorise.entities (nsIndex, ns, false)

        [ if allByCategory.Length > 0 then
              ``##`` [ !!(ns.Name + " Namespace") ]

              match ns.NamespaceDocs with
              | Some nsdocs ->
                  p [ embed nsdocs.Summary ]

                  match nsdocs.Remarks with
                  | Some r -> p [ embed r ]
                  | None -> ()
              | None -> ()

              if (allByCategory.Length > 1) then
                  ``###`` [ !!"Contents" ]

                  ul
                      [ for category in allByCategory do
                            [ p [ link [ !!category.CategoryName ] ("#category-" + category.CategoryIndex) ] ] ]

              for category in allByCategory do
                  if (allByCategory.Length > 1) then
                      ``###`` [ link [ !!category.CategoryName ] ("#category-" + category.CategoryIndex) ]

                  yield! renderEntities category.CategoryEntites ]

    /// Builds the list-of-namespaces Markdown fragment (for sidebar navigation or index page).
    /// When <paramref name="nav"/> is <c>true</c> the active namespace is expanded to show its entities.
    let listOfNamespacesAux otherDocs nav (nsOpt: ApiDocNamespace option) =
        [
          // For FSharp.Core we make all entries available to other docs else there's not a lot else to show.
          //
          // For non-FSharp.Core we only show one link "API Reference" in the nav menu
          if otherDocs && nav && model.Collection.CollectionName <> "FSharp.Core" then
              p
                  [ !!"API Reference"
                    link
                        [ !!"All Namespaces" ]
                        (model.IndexFileUrl(root, collectionName, qualify, model.FileExtensions.InUrl)) ]
          else

              let categorise = Categorise.model model

              let someExist = categorise.Length > 0

              if someExist && nav then
                  p [ !!"Namespaces" ]

              for allByCategory, ns in categorise do

                  // Generate the entry for the namespace
                  p
                      [ link [ !!ns.Name ] (ns.Url(root, collectionName, qualify, model.FileExtensions.InUrl))

                        // If not in the navigation list then generate the summary text as well
                        if not nav then
                            !!" - "

                            match ns.NamespaceDocs with
                            | Some nsdocs -> embed nsdocs.Summary
                            | None -> () ]

                  // In the navigation bar generate the expanded list of entities
                  // for the active namespace
                  if nav then
                      match nsOpt with
                      | Some ns2 when ns.Name = ns2.Name ->
                          ul
                              [ for category in allByCategory do
                                    for e in category.CategoryEntites do
                                        [ p
                                              [ link
                                                    [ !!e.Name ]
                                                    (e.Url(root, collectionName, qualify, model.FileExtensions.InUrl)) ] ] ]
                      | _ -> () ]

    /// Returns the list-of-namespaces string, using a menu template when available.
    let listOfNamespaces otherDocs nav (nsOpt: ApiDocNamespace option) =
        let noTemplatingFallback () =
            listOfNamespacesAux otherDocs nav nsOpt
            |> List.map (fun html -> html.ToString())
            |> String.concat "             \n"

        match menuTemplateFolder with
        | None -> noTemplatingFallback ()
        | Some menuTemplateFolder ->
            let isTemplatingAvailable = Menu.isTemplatingAvailable menuTemplateFolder

            if not isTemplatingAvailable then
                noTemplatingFallback ()
            else if otherDocs && nav && model.Collection.CollectionName <> "FSharp.Core" then
                let menuItems =
                    let title = "All Namespaces"
                    let link = model.IndexFileUrl(root, collectionName, qualify, model.FileExtensions.InUrl)

                    [ { Menu.MenuItem.Link = link
                        Menu.MenuItem.Content = title
                        Menu.MenuItem.IsActive = false } ]

                Menu.createMenu menuTemplateFolder false "API Reference" menuItems

            else
                let categorise = Categorise.model model

                if categorise.Length = 0 then
                    ""
                else
                    let menuItems =
                        categorise
                        |> List.map (fun (_, ns) ->
                            let link = ns.Url(root, collectionName, qualify, model.FileExtensions.InUrl)
                            let name = ns.Name

                            { Menu.MenuItem.Link = link
                              Menu.MenuItem.Content = name
                              Menu.MenuItem.IsActive = false })

                    Menu.createMenu menuTemplateFolder false "Namespaces" menuItems

    /// Get the substitutions relevant to all
    member _.GlobalSubstitutions: Substitutions =
        let toc = listOfNamespaces true true None
        [ yield (ParamKeys.``fsdocs-list-of-namespaces``, toc) ]

    /// Writes all API documentation Markdown files (index, one per namespace, one per entity)
    /// to <paramref name="outDir"/>, applying <paramref name="templateOpt"/> to each page.
    member _.Generate(outDir: string, templateOpt, collectionName, globalParameters) =

        let getSubstitutons parameters toc (content: MarkdownDocument) pageTitle =
            [| yield! parameters
               yield (ParamKeys.``fsdocs-list-of-namespaces``, toc)
               yield (ParamKeys.``fsdocs-content``, Markdown.ToMd(content))
               yield (ParamKeys.``fsdocs-source``, "")
               yield (ParamKeys.``fsdocs-tooltips``, "")
               yield (ParamKeys.``fsdocs-page-title``, pageTitle)
               yield! globalParameters |]

        let collection = model.Collection

        (let content =
            MarkdownDocument(
                [ ``#`` [ !!"API Reference" ]
                  ``##`` [ !!"Available Namespaces" ]
                  ul [ (listOfNamespacesAux false false None) ] ],
                Map.empty
            )

         let pageTitle = sprintf "%s (API Reference)" collectionName

         let toc = listOfNamespaces false true None

         let substitutions = getSubstitutons model.Substitutions toc content pageTitle

         let outFile =
             Path.Combine(outDir, model.IndexOutputFile(collectionName, model.Qualify, model.FileExtensions.InFile))

         printfn "  Generating %s" outFile
         SimpleTemplating.UseFileAsSimpleTemplate(substitutions, templateOpt, outFile))

        ()

        for nsIndex, ns in Seq.indexed collection.Namespaces do

            let content = MarkdownDocument(namespaceContent (nsIndex, ns), Map.empty)

            let pageTitle = ns.Name
            let toc = listOfNamespaces false true (Some ns)

            let substitutions = getSubstitutons model.Substitutions toc content pageTitle

            let outFile =
                Path.Combine(outDir, ns.OutputFile(collectionName, model.Qualify, model.FileExtensions.InFile))

            printfn "  Generating %s" outFile
            SimpleTemplating.UseFileAsSimpleTemplate(substitutions, templateOpt, outFile)

        for info in model.EntityInfos do
            let content = MarkdownDocument(entityContent info, Map.empty)

            let pageTitle = sprintf "%s (%s)" info.Entity.Name collectionName

            let toc = listOfNamespaces false true (Some info.Namespace)

            let substitutions = getSubstitutons info.Entity.Substitutions toc content pageTitle

            let outFile =
                Path.Combine(outDir, info.Entity.OutputFile(collectionName, model.Qualify, model.FileExtensions.InFile))

            printfn "  Generating %s" outFile
            SimpleTemplating.UseFileAsSimpleTemplate(substitutions, templateOpt, outFile)
