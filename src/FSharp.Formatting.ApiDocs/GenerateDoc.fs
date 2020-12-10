module internal FSharp.Formatting.ApiDocs.GenerateDoc

open System

let categoriseEntities (nsIndex: int, ns: ApiDocNamespace, suppress) getSortedCategories =
  let entities = ns.Entities

  let categories =
      getSortedCategories entities (fun (m:ApiDocEntity) -> m.Exclude) (fun (m:ApiDocEntity) -> m.Category) (fun (m:ApiDocEntity) -> m.CategoryIndex)

  let allByCategory =
      [ for (catIndex, (categoryName, (categoryEntities: ApiDocEntity list))) in Seq.indexed categories do
          let categoryName = (if String.IsNullOrEmpty(categoryName) then "Other namespace members" else categoryName)
          let index = String.Format("{0}_{1}", nsIndex, catIndex)
          let categoryEntities =
            // When calculating list-of-namespaces suppress some entries
            // Some bespoke hacks to make FSharp.Core docs look ok.
            //
            // TODO: use <exclude /> to do these, or work out if there's a better way
            if suppress then
              categoryEntities

              // Remove FSharp.Data.UnitSystems.SI from the list-of-namespaces
              // display - it's just so rarely used, has long names and dominates the docs.
              //
              // See https://github.com/fsharp/fsharp-core-docs/issues/57, we may rethink this
              |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols"))
              |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Data.UnitSystems.SI.UnitNames"))
              // Don't show 'AnonymousObject' in list-of-namespaces navigation
              |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.RuntimeHelpers" && e.Symbol.DisplayName = "AnonymousObject"))
              // Don't show 'FSharp.Linq.QueryRunExtensions' in list-of-namespaces navigation
              |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.QueryRunExtensions" && e.Symbol.DisplayName = "LowPriority"))
              |> List.filter (fun e -> not (e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.QueryRunExtensions" && e.Symbol.DisplayName = "HighPriority"))
            else
              categoryEntities
              
          // We currently suppress all obsolete entries all the time
          let categoryEntities =
              categoryEntities
              |> List.filter (fun e -> not e.IsObsolete)

          let categoryEntities =
              categoryEntities
              |> List.sortBy (fun e ->
                  (e.Symbol.DisplayName.ToLowerInvariant(), e.Symbol.GenericParameters.Count,
                      e.Name, (if e.IsTypeDefinition then e.UrlBaseName else "ZZZ")))

          if categoryEntities.Length > 0 then
              yield {| CategoryName = categoryName
                       CategoryIndex = index
                       CategoryEntites = categoryEntities |} ]

  allByCategory

let categorise model getSortedCategories =
  [ for (nsIndex, ns) in Seq.indexed model.Collection.Namespaces do
    let allByCategory = categoriseEntities (nsIndex, ns, true) getSortedCategories
    if allByCategory.Length > 0 then
      allByCategory, ns ]