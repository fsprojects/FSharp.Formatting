[<RequireQualifiedAccess>]
module internal FSharp.Formatting.ApiDocs.Categorise

open System

// Honour the CategoryIndex to put the categories in the right order
let private getSortedCategories xs exclude category categoryIndex =
    xs
    |> List.filter (exclude >> not)
    |> List.groupBy (category)
    |> List.map (fun (cat, xs) -> (cat, xs, xs |> List.minBy (categoryIndex)))
    |> List.sortBy (fun (cat, _xs, x) -> categoryIndex x, cat)
    |> List.map (fun (cat, xs, _x) -> cat, xs)

// Group all members by their category
let getMembersByCategory (members: ApiDocMember list) =
    getSortedCategories members (fun m -> m.Exclude) (fun m -> m.Category) (fun m -> m.CategoryIndex)
    |> List.mapi (fun i (key, elems) ->
        let elems = elems |> List.sortBy (fun m -> m.Name)

        let name = if String.IsNullOrEmpty(key) then "Other module members" else key

        (i, elems, name))

let entities (nsIndex: int, ns: ApiDocNamespace, suppress) =
    let entities = ns.Entities

    let categories =
        getSortedCategories
            entities
            (fun (m: ApiDocEntity) -> m.Exclude)
            (fun (m: ApiDocEntity) -> m.Category)
            (fun (m: ApiDocEntity) -> m.CategoryIndex)

    let allByCategory =
        [ for (catIndex, (categoryName, (categoryEntities: ApiDocEntity list))) in Seq.indexed categories do
              let categoryName =
                  (if String.IsNullOrEmpty(categoryName) then "Other namespace members" else categoryName)

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
                      |> List.filter (fun e ->
                          not (e.Symbol.Namespace = Some "Microsoft.FSharp.Data.UnitSystems.SI.UnitSymbols"))
                      |> List.filter (fun e ->
                          not (e.Symbol.Namespace = Some "Microsoft.FSharp.Data.UnitSystems.SI.UnitNames"))
                      // Don't show 'AnonymousObject' in list-of-namespaces navigation
                      |> List.filter (fun e ->
                          not (
                              e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.RuntimeHelpers"
                              && e.Symbol.DisplayName = "AnonymousObject"
                          ))
                      // Don't show 'FSharp.Linq.QueryRunExtensions' in list-of-namespaces navigation
                      |> List.filter (fun e ->
                          not (
                              e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.QueryRunExtensions"
                              && e.Symbol.DisplayName = "LowPriority"
                          ))
                      |> List.filter (fun e ->
                          not (
                              e.Symbol.Namespace = Some "Microsoft.FSharp.Linq.QueryRunExtensions"
                              && e.Symbol.DisplayName = "HighPriority"
                          ))
                  else
                      categoryEntities

              // We currently suppress all obsolete entries all the time
              let categoryEntities = categoryEntities |> List.filter (fun e -> not e.IsObsolete)

              let categoryEntities =
                  categoryEntities
                  |> List.sortBy (fun e ->
                      (e.Symbol.DisplayName.ToLowerInvariant(),
                       e.Symbol.GenericParameters.Count,
                       e.Name,
                       (if e.IsTypeDefinition then e.UrlBaseName else "ZZZ")))

              if categoryEntities.Length > 0 then
                  yield {| CategoryName = categoryName; CategoryIndex = index; CategoryEntites = categoryEntities |} ]

    allByCategory

let model (apiDocModel: ApiDocModel) =
    [ for (nsIndex, ns) in Seq.indexed apiDocModel.Collection.Namespaces do
          let allByCategory = entities (nsIndex, ns, true)

          if allByCategory.Length > 0 then allByCategory, ns ]
