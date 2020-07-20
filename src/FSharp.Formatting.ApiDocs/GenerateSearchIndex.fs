module internal FSharp.Formatting.ApiDocs.GenerateSearchIndex

open FSharp.Formatting.ApiDocs

let stripMicrosoft (str: string) =
    if str.StartsWith("Microsoft.") then
        str.["Microsoft.".Length ..]
    elif str.StartsWith("microsoft-") then
        str.["microsoft-".Length ..]
    else
        str

type AssemblyEntities = {
  Modules: ApiDocModule list
  Types: ApiDocTypeDefinition list
  GeneratorOutput: ApiDocsModel
}


let rec collectModules (m: ApiDocModule) =
    [
        yield m
        yield! m.NestedModules |> List.collect collectModules
    ]

let generateSearchIndex (model: ApiDocsModel) =
    let allModules =
        [ for n in model.AssemblyGroup.Namespaces do
            for m in n.Modules do
               yield! collectModules m
        ]
    let allTypes =
        [
            for n in model.AssemblyGroup.Namespaces do
                for t in n.Types do
                    t 
            for n in allModules do
                for t in n.NestedTypes do
                    t
        ]

    let entities = {
        Modules = allModules
        Types = allTypes
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
                [ for modul in entities.Modules do
                    modul.Name

                  for typ in entities.Types do
                    typ.Name
                ] |> String.concat " \n"

            { uri = (sprintf "%s/index.html" model.CollectionRootUrl )
              title = "API Reference"
              content = ctn }

            // generate a search index entry for each module in the assembly
            for modul in entities.Modules do
                let cnt =
                    [ modul.Name
                      modul.Comment.FullText
                      for nestedModul in modul.NestedModules do  
                        modul.Name + "." + nestedModul.Name
                        nestedModul.Name

                      for nestedType in modul.NestedTypes do 
                        modul.Name + "." + nestedType.Name
                        nestedType.Name

                      for memb in modul.ValuesAndFuncs do
                        modul.Name + "." + memb.Name
                        memb.Name

                      for ext in modul.TypeExtensions do 
                        modul.Name + "." + ext.Name 
                        ext.Name
                    ] |> String.concat " \n"


                let url = sprintf "%s/%s.html" model.CollectionRootUrl modul.UrlBaseName
                { uri = url
                  title = modul.Name
                  content = cnt }

                // generate a search index entry for each value, function or member in each module
                for memb in modul.ValuesAndFuncs do
                    doMember modul.Name memb

            // generate a search index entry for each type definition in the assembly
            for typ in entities.Types do
                let cnt =
                    [ typ.Name
                      typ.Comment.FullText

                      for memb in typ.AllMembers do
                        typ.Name + "." + memb.Name
                        memb.Name

                        ] |> String.concat " \n"

                let url = sprintf "%s/%s.html" model.CollectionRootUrl typ.UrlBaseName
                { uri = url
                  title = typ.Name
                  content = cnt }

                for memb in typ.AllMembers do
                    doMember typ.Name memb
        |]

    refs

