/// Internal module for building the JSON search index from an <see cref="ApiDocModel"/>
module internal FSharp.Formatting.ApiDocs.GenerateSearchIndex

open FSharp.Formatting.ApiDocs

/// Bundles all entity and model data needed while building search entries
type AssemblyEntities =
    { Entities: ApiDocEntity list
      GeneratorOutput: ApiDocModel }


/// Recursively collects an entity and all its nested entities
let rec collectEntities (m: ApiDocEntity) =
    [ yield m; yield! m.NestedEntities |> List.collect collectEntities ]

/// The search index type tag used to identify API-docs entries
[<Literal>]
let ApiDocs = "apiDocs"

/// Builds all search index entries for the given <see cref="ApiDocModel"/>
let searchIndexEntriesForModel (model: ApiDocModel) =
    let allEntities =
        [ for n in model.Collection.Namespaces do
              for m in n.Entities do
                  yield! collectEntities m ]

    let entities =
        { Entities = allEntities
          GeneratorOutput = model }

    /// Builds a single search entry for a member under the given enclosing entity name
    let doMember enclName (memb: ApiDocMember) =
        let cnt =
            [ yield enclName + "." + memb.Name
              yield memb.Name
              yield memb.Comment.Summary.HtmlText
              match memb.Comment.Remarks with
              | None -> ()
              | Some s -> yield s.HtmlText ]
            |> String.concat " \n"

        { uri = memb.Url(model.Root, model.Collection.CollectionName, model.Qualify, model.FileExtensions.InUrl)
          title = enclName + "." + memb.Name
          content = cnt
          ``type`` = ApiDocs
          headings = List.empty }

    let refs =
        [| for nsp in model.Collection.Namespaces do
               // Namespace entry: content lists all child type/module names for search
               let ctn =
                   [ for e in nsp.Entities do
                         e.Name ]
                   |> String.concat " \n"

               { uri = nsp.Url(model.Root, model.Collection.CollectionName, model.Qualify, model.FileExtensions.InUrl)
                 title = nsp.Name
                 content = ctn
                 ``type`` = ApiDocs
                 headings = List.empty }

           // One entry per entity (type/module), plus one per member
           for e in entities.Entities do
               let cnt =
                   [ e.Name
                     e.Comment.Summary.HtmlText
                     match e.Comment.Remarks with
                     | None -> ()
                     | Some s -> s.HtmlText
                     for ne in e.NestedEntities do
                         e.Name + "." + ne.Name
                         ne.Name

                     for memb in e.AllMembers do
                         e.Name + "." + memb.Name
                         memb.Name

                     ]
                   |> String.concat " \n"


               let url = e.Url(model.Root, model.Collection.CollectionName, model.Qualify, model.FileExtensions.InUrl)

               { uri = url
                 title = e.Name
                 content = cnt
                 ``type`` = ApiDocs
                 headings = List.empty }

               for memb in e.AllMembers do
                   doMember e.Name memb

           |]

    refs
