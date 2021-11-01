module internal FSharp.Formatting.ApiDocs.GenerateSearchIndex

open FSharp.Formatting.ApiDocs

type AssemblyEntities =
    { Entities: ApiDocEntity list
      GeneratorOutput: ApiDocModel }


let rec collectEntities (m: ApiDocEntity) =
    [ yield m; yield! m.NestedEntities |> List.collect collectEntities ]

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
          content = cnt }

    let refs =
        [| for nsp in model.Collection.Namespaces do
               // the entry is found when searching for types and modules
               let ctn =
                   [ for e in nsp.Entities do
                         e.Name ]
                   |> String.concat " \n"

               { uri = nsp.Url(model.Root, model.Collection.CollectionName, model.Qualify, model.FileExtensions.InUrl)
                 title = nsp.Name
                 content = ctn }

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
                 content = cnt }

               for memb in e.AllMembers do
                   doMember e.Name memb


           |]

    refs
