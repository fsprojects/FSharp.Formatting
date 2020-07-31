module internal FSharp.Formatting.ApiDocs.GenerateSearchIndex

open FSharp.Formatting.ApiDocs

type AssemblyEntities = {
  Entities: ApiDocEntity list
  GeneratorOutput: ApiDocsModel
}


let rec collectEntities (m: ApiDocEntity) =
    [
        yield m
        yield! m.NestedEntities |> List.collect collectEntities
    ]

let generateSearchIndex (model: ApiDocsModel) =
    let allEntities =
        [ for n in model.AssemblyGroup.Namespaces do
            for m in n.Entities do
               yield! collectEntities m
        ]

    let entities = {
        Entities = allEntities
        GeneratorOutput = model
    }

    let doMember enclName (memb: ApiDocMember) =
        let cnt =
            [ enclName + "." + memb.Name
              memb.Name
              memb.Comment.FullText
            ] |> String.concat " \n"

        { uri = sprintf "%s/%s" model.CollectionRootUrl memb.UrlFileNameAndHash
          title = enclName + "." + memb.Name
          content = cnt }

    let refs =
        [|      
            // the entry is found when searching for types and modules
            let ctn =
                [ for e in entities.Entities do
                    e.Name
                ] |> String.concat " \n"

            { uri = (sprintf "%s/index.html" model.CollectionRootUrl )
              title = "API Reference"
              content = ctn }

            // generate a search index entry for each module in the assembly
            for e in entities.Entities do
                let cnt =
                    [ e.Name
                      e.Comment.FullText
                      for ne in e.NestedEntities do  
                        e.Name + "." + ne.Name
                        ne.Name

                      for memb in e.AllMembers do
                        e.Name + "." + memb.Name
                        memb.Name

                    ] |> String.concat " \n"


                let url = sprintf "%s/%s.html" model.CollectionRootUrl e.UrlBaseName
                { uri = url
                  title = e.Name
                  content = cnt }

                for memb in e.AllMembers do
                    doMember e.Name memb
        |]

    refs

