/// Internal module that builds the Lunr/Fuse search-index entries for an API documentation model.
module internal FSharp.Formatting.ApiDocs.GenerateSearchIndex

open FSharp.Formatting.ApiDocs

/// Groups a flat list of top-level entities together with the overall API doc model
/// so they can be iterated in a single pass when building search-index entries.
type AssemblyEntities =
    { Entities: ApiDocEntity list
      GeneratorOutput: ApiDocModel }


/// Recursively collects an entity and all its nested entities into a flat list.
let rec collectEntities (m: ApiDocEntity) =
    [ yield m; yield! m.NestedEntities |> List.collect collectEntities ]

/// Constant string used as the <c>type</c> field in search-index records for API doc entries.
[<Literal>]
let ApiDocs = "apiDocs"

/// Produces an array of search-index records for every namespace, entity, and member in
/// the given <see cref="ApiDocModel"/>. Each record contains a URI, title, and concatenated
/// content string suitable for full-text indexing.
let searchIndexEntriesForModel (model: ApiDocModel) =
    let allEntities =
        [ for n in model.Collection.Namespaces do
              for m in n.Entities do
                  yield! collectEntities m ]

    let entities =
        { Entities = allEntities
          GeneratorOutput = model }

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
               // the entry is found when searching for types and modules
               let ctn =
                   [ for e in nsp.Entities do
                         e.Name ]
                   |> String.concat " \n"

               { uri = nsp.Url(model.Root, model.Collection.CollectionName, model.Qualify, model.FileExtensions.InUrl)
                 title = nsp.Name
                 content = ctn
                 ``type`` = ApiDocs
                 headings = List.empty }

           // generate a search index entry for each entity in the assembly
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
