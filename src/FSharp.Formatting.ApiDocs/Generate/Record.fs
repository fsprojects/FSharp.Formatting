module internal FSharp.Formatting.ApiDocs.Generate.Record

open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.ApiDocs.GenerateSignature
open FSharp.Formatting.ApiDocs
open FSharp.Compiler.Symbols

module Seq =

    let collecti (f: int -> 'T -> 'U list) (s: seq<'T>) : 'U seq =
        s |> Seq.mapi f |> Seq.concat

let renderRecordType (entityInfo: ApiDocEntityInfo) =
    let entity = entityInfo.Entity

    div [
        Class "fsdocs-api-code"
    ] [
        div [] [
            Html.keyword "type"
            Html.space
            !!entity.Name
            Html.space
            Html.equal
        ]
        div [] [
            Html.spaces 4
            Html.leftBrace
        ]

        for field in entity.RecordFields do
            match field.ReturnInfo.ReturnType with
            | Some(_, returnType) ->
                let escapedReturnType =
                    // Remove the starting <code> and ending </code>
                    returnType.HtmlText.[6 .. returnType.HtmlText.Length - 8]

                div [
                    Class "record-field"
                ] [
                    Html.spaces 8
                    a [
                        Class "record-field-name"
                        Href("#" + field.Name)
                    ] [
                        !!field.Name
                    ]
                    Html.space
                    Html.colon
                    Html.space
                    span [
                        Class "record-field-type"
                    ] [
                        !!escapedReturnType
                    ]
                ]

            | None -> ()

        div [] [
            Html.spaces 4
            Html.rightBrace
        ]

        for m in entity.InstanceMembers do
            match m.Symbol with
            | :? FSharpMemberOrFunctionOrValue as symbol ->
                div [] [

                    Html.spaces 4
                    Html.keyword "member"
                    Html.space
                    Html.keyword "this"
                    Html.dot
                    !!symbol.DisplayName

                    printfn "Parameters:             %A" m.Parameters
                    printfn "CurriedParameterGroups: %A" symbol.CurriedParameterGroups

                    if symbol.CurriedParameterGroups.Count = 0 then
                        !!"unit"
                    else
                        for parameterGroup in symbol.CurriedParameterGroups do
                            // Can this case happen?
                            if parameterGroup.Count = 0 then
                                ()
                            else if parameterGroup.Count = 1 then
                                let parameter = parameterGroup.[0]
                                Html.space
                                !!parameter.DisplayName
                                !!"dwdwd"
                                Html.space
                                Html.arrow
                                Html.space
                            else // Tupled arguments
                                yield! parameterGroup
                                |> Seq.collecti (fun index parameter ->
                                    [
                                        Html.space
                                        if index <> 0 then
                                            Html.star
                                            Html.space
                                        !!parameter.DisplayName
                                        // Format the type
                                        // parameter.Type
                                    ]
                                )

                    match m.ReturnInfo.ReturnType with
                    | Some(_, returnType) ->
                        // Only add ' : ' if there is a return type
                        // This is to work around https://github.com/fsprojects/FSharp.Formatting/issues/734
                        // I think this still generate incorrect code, because reading the implementation of returnType
                        // it seems to return `None` if the return type is `unit`
                        // For now, let's consider that returning `unit` from a member property is not a common case
                        Html.space
                        Html.colon
                        Html.space
                        !!returnType.HtmlText
                    | None -> ()

                    match symbol.HasGetterMethod, symbol.HasSetterMethod with
                    | true, true ->
                        Html.space
                        Html.keyword "with"
                        Html.space
                        Html.keyword "get"
                        Html.comma
                        Html.space
                        Html.keyword "set"
                    | true, false ->
                        Html.space
                        Html.keyword "with"
                        Html.space
                        Html.keyword "get"
                    | false, true ->
                        Html.space
                        Html.keyword "with"
                        Html.space
                        Html.keyword "set"
                    | false, false -> ()
                ]

            | unkownSymbol ->
                div [] [
                    !! $"'%s{unkownSymbol.ToString()}' symbol not supported, please report an issue at "
                    a [
                        Href "https://github.com/fsprojects/FSharp.Formatting"
                    ] [
                        !! "FSharp.Formatting."
                    ]
                ]
    ]
    |> Html.minify


let subSectionTitle (title: string) =
    div [ Class "sub-section-title" ] [
        !!title
    ]

let renderRecordVSCodeLike (entityInfo: ApiDocEntityInfo) =
    let entity = entityInfo.Entity

    div [] [
        strong [] [
            !!entity.Name
        ]

        if not entity.RecordFields.IsEmpty then
            subSectionTitle "Fields"

        if not entity.InstanceMembers.IsEmpty then
            subSectionTitle "Members"

        if not entity.StaticMembers.IsEmpty then
            subSectionTitle "Static Members"

        // TODO: what are entity.StaticParameters for a record ?
        // Are they generics?

    ]