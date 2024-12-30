module internal FSharp.Formatting.ApiDocs.Generate.Module

open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.ApiDocs.GenerateSignature
open FSharp.Formatting.ApiDocs

let sectionTitle (title: string) =
    strong [] [
        !!title
    ]

let private renderSection (linkGenerator: ApiDocEntity -> string) (title: string) (entities: ApiDocEntity list) =
    [
        if not entities.IsEmpty then
            sectionTitle title

            p [] [
                table [] [
                    thead [] [
                        tr [] [
                            th [
                                Width "25%"
                            ] [
                                !! "Type"
                            ]
                            th [
                                Width "75%"
                            ] [
                                !! "Description"
                            ]
                        ]
                    ]
                    tbody [] [
                        for entity in entities do
                            tr [] [
                                td [] [
                                    a [
                                        Href(linkGenerator entity)
                                    ] [
                                        !!entity.Name
                                    ]
                                ]
                                td [] [
                                    !!(Common.formatXmlComment entity.Comment.Xml)
                                ]
                            ]
                    ]
                ]
            ]
    ]

let private renderDeclaredTypes (entities: ApiDocEntity list) (linkGenerator: ApiDocEntity -> string) =
    entities
    |> List.filter (fun entity -> entity.IsTypeDefinition)
    |> renderSection linkGenerator "Declared types"

let private renderDeclaredModules (entities: ApiDocEntity list) (linkGenerator: ApiDocEntity -> string) =
    entities
    |> List.filter (fun entity -> entity.Symbol.IsFSharpModule)
    |> renderSection linkGenerator "Declared modules"

let private renderValueOrFunctions (entities: ApiDocMember list) (linkGenerator: ApiDocEntity -> string) =
    if entities.IsEmpty then
        []
    else

        [
            sectionTitle "Functions and values"

            for entity in entities do
                let (ApiDocMemberDetails(usageHtml,
                                         paramTypes,
                                         returnType,
                                         modifiers,
                                         typars,
                                         baseType,
                                         location,
                                         compiledName)) =
                    entity.Details

                let returnHtml =
                    // TODO: Parse the return type information from
                    // let x = entity.Symbol :?> FSharpMemberOrFunctionOrValue
                    // x.FullType <-- Here we have access to all the type including the argument for the function that we should ignore... (making the processing complex)
                    // For now, we are just using returnType.HtmlText to have something ready as parsing from
                    // FSharpMemberOrFunctionOrValue seems to be quite complex
                    match returnType with
                    | Some(_, returnType) ->
                        // Remove the starting <code> and ending </code>
                        returnType.HtmlText.[6 .. returnType.HtmlText.Length - 8]
                        // Adapt the text to have basic syntax highlighting
                        |> fun text -> text.Replace("&lt;", Html.lessThan.ToMinifiedHtml())
                        |> fun text -> text.Replace("&gt;", Html.greaterThan.ToMinifiedHtml())
                        |> fun text -> text.Replace(",", Html.comma.ToMinifiedHtml())

                    | None -> "unit"

                let initial = Signature.ParamTypesInformation.Init entity.Name

                let paramTypesInfo = Signature.extractParamTypesInformation initial paramTypes

                div [
                    Class "fsdocs-block"
                ] [

                    details [] [
                        summary [] [
                            div [ Class "usage"] [
                                !!usageHtml.HtmlText
                            ]

                            div [
                                Class "actions-buttons"
                            ] [
                                a [
                                    Href "dw"
                                    Class "fsdocs-source-link"
                                    HtmlProperties.Title "Source on GitHub"
                                ] [
                                    iconifyIcon [
                                        Icon "ri:github-fill"
                                        Height "24"
                                        Width "24"
                                    ]
                                ]
                            // yield! copyXmlSigIconForSymbol entity.Symbol
                            // yield! copyXmlSigIconForSymbolMarkdown entity.Symbol
                            ]
                        ]
                        article [] [

                            match entity.Comment.Xml with
                            | Some xmlComment ->
                                let comment = xmlComment.ToString()
                                !!(CommentFormatter.formatSummaryOnly comment)

                                if not paramTypesInfo.Infos.IsEmpty then
                                    p [] [
                                        strong [] [
                                            !! "Parameters"
                                        ]
                                    ]

                                    for (name, returnType) in paramTypesInfo.Infos do
                                        let paramDoc =
                                            CommentFormatter.tryFormatParam name comment
                                            |> Option.map (fun paramDoc -> !!paramDoc)
                                            |> Option.defaultValue Html.nothing

                                        div [
                                            Class "fsdocs-doc-parameter"
                                        ] [
                                            [
                                                TextNode.DivWithClass(
                                                    "fsdocs-api-code",
                                                    [
                                                        TextNode.Property name
                                                        TextNode.Space
                                                        TextNode.Colon
                                                        TextNode.Space
                                                        returnType
                                                    ]
                                                )
                                            ]
                                            |> TextNode.Node
                                            |> TextNode.ToHtmlElement

                                            paramDoc
                                        ]

                                match CommentFormatter.tryFormatReturnsOnly comment with
                                | Some returnDoc ->
                                    p [] [
                                        strong [] [
                                            !! "Returns"
                                        ]
                                    ]

                                    !!returnDoc

                                | None -> ()

                            // TODO: Should we render a minimal documentation here with the information we have?
                            // For example, we can render the list of parameters and the return type
                            // This is to make the documentation more consistent
                            // However, these minimal information will be rondontant with the information displayed in the signature
                            | None -> ()
                        ]
                    ]

                ]

        //   hr []

        ]

let renderModule (entityInfo: ApiDocEntityInfo) (linkGenerator: ApiDocEntity -> string) : HtmlElement list =
    [
        yield! renderDeclaredTypes entityInfo.Entity.NestedEntities linkGenerator
        yield! renderDeclaredModules entityInfo.Entity.NestedEntities linkGenerator
        yield! renderValueOrFunctions entityInfo.Entity.ValuesAndFuncs linkGenerator
    ]
