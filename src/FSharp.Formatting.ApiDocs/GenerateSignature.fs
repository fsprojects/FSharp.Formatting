module internal FSharp.Formatting.ApiDocs.GenerateSignature

open System
open System.Collections.Generic
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Compiler.Symbols
open FSharp.Formatting.Templating
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open System.Xml.Linq
open System.Text.RegularExpressions

/// <summary>
/// Type used to represent a text node.
///
/// This is mostly used to render API signature while also being able to compute the length of the text
/// in term of characters to align the signature.
/// </summary>
[<RequireQualifiedAccess>]
type TextNode =
    | Text of string
    | Anchor of url: string * label: string
    | AnchorWithId of url: string * id: string * label: string
    | Space
    | Dot
    | Comma
    | Arrow
    | GreaterThan
    | Colon
    | LessThan
    | LeftParent
    | RightParent
    | Equal
    | Tick
    | Node of TextNode list
    | Keyword of string
    | NewLine
    | Spaces of int
    | Div of TextNode list
    | DivWithClass of string * TextNode list
    | Property of string
    | Paragraph of TextNode list

    static member ToHtmlElement(node: TextNode) : HtmlElement = node.HtmlElement

    member this.HtmlElement: HtmlElement =
        match this with
        | Text s -> !!s
        | Colon -> Html.colon
        | Anchor(url, text) -> a [ Href url ] [ !!text ]
        | AnchorWithId(url, id, text) -> a [ Href url; Id id ] [ !!text ]
        | Keyword text -> Html.keyword text
        | Property text -> Html.property text
        | Div nodes -> div [] (nodes |> List.map (fun node -> node.HtmlElement))
        | DivWithClass(cls, nodes) -> div [ Class cls ] (nodes |> List.map (fun node -> node.HtmlElement))
        | Paragraph nodes -> p [] (nodes |> List.map (fun node -> node.HtmlElement))
        | Spaces n ->
            [ for _ in 0..n do
                  Space ]
            |> Node
            |> TextNode.ToHtmlElement
        | NewLine -> !! "\n" // Should it be <br> instead?
        | Arrow -> Html.arrow
        | Dot -> Html.dot
        | Comma -> Html.comma
        | Space -> Html.space
        | GreaterThan -> Html.greaterThan
        | LessThan -> Html.lessThan
        | Equal -> Html.keyword "="
        | Tick -> !! "&#x27;"
        | LeftParent -> Html.leftParent
        | RightParent -> Html.rightParent
        | Node node ->
            // TODO: Can we have something similar to fragments in React?
            let elements = span [] (node |> List.map (fun node -> node.HtmlElement))

            !! elements.ToMinifiedHtml()

    member this.Length =
        match this with
        | NewLine -> 0
        // 1 character
        | Comma
        | Colon
        | Dot
        | Space
        | GreaterThan
        | LessThan
        | LeftParent
        | RightParent
        | Equal
        | Tick -> 1
        // 2 characters
        | Anchor(_, text)
        | AnchorWithId(_, _, text)
        | Keyword text
        | Property text -> text.Length
        | Arrow -> 2
        // X characters
        | Text s -> s.Length
        | Spaces count -> count
        // Sum of children
        | Node nodes
        | Div nodes
        | DivWithClass(_, nodes)
        | Paragraph nodes -> nodes |> List.map (fun node -> node.Length) |> List.sum

[<RequireQualifiedAccess>]
module Signature =

    /// <summary>
    /// Generate a list of generic parameters
    /// <example>
    /// 'T, 'T2, 'MyType
    /// </example>
    /// </summary>
    /// <param name="parameters"></param>
    /// <returns></returns>
    let renderGenericParameters (parameters: IList<FSharpGenericParameter>) : TextNode =
        [ for index in 0 .. parameters.Count - 1 do
              let param = parameters.[index]

              if index <> 0 then
                  TextNode.Comma
                  TextNode.Space

              TextNode.Tick
              TextNode.Text param.DisplayName ]
        |> TextNode.Node

    let rec renderParameterType (isTopLevel: bool) (typ: FSharpType) : TextNode =
        // This correspond to a generic paramter like: 'T
        if typ.IsGenericParameter then
            TextNode.Node [ TextNode.Tick; TextNode.Text typ.GenericParameter.DisplayName ]
        // Not a generic type we can display it as it is
        // Example:
        //      - string
        //      - int
        //      - MyObject
        else if typ.GenericArguments.Count = 0 then
            TextNode.Text typ.TypeDefinition.DisplayName

        // This is a generic type we need more logic
        else if
            // This is a function, we need to generate something like:
            //     - 'T -> string
            //     - 'T -> 'T option
            typ.IsFunctionType
        then
            let separator = TextNode.Node [ TextNode.Space; TextNode.Arrow; TextNode.Space ]

            let result =
                [ for index in 0 .. typ.GenericArguments.Count - 1 do
                      let arg = typ.GenericArguments.[index]

                      // Add the separator if this is not the first argument
                      if index <> 0 then
                          separator

                      // This correspond to a generic paramter like: 'T
                      if arg.IsGenericParameter then
                          TextNode.Tick
                          TextNode.Text arg.GenericParameter.DisplayName

                      // This is a type definition like: 'T option or Choice<'T1, 'T2>
                      else if arg.HasTypeDefinition then
                          // For some generic types definition we don't add the generic arguments
                          if
                              arg.TypeDefinition.DisplayName = "exn"
                              || arg.TypeDefinition.DisplayName = "unit"
                          then

                              TextNode.Text arg.TypeDefinition.DisplayName

                          else
                              // This is the name of the type definition
                              // In Choice<'T1, 'T2> this correspond to Choice
                              TextNode.Text arg.TypeDefinition.DisplayName
                              TextNode.LessThan
                              // Render the generic parameters list in the form of 'T1, 'T2
                              renderGenericParameters arg.TypeDefinition.GenericParameters

                              TextNode.GreaterThan

                      else if arg.IsFunctionType then

                          let res =
                              [ for index in 0 .. arg.GenericArguments.Count - 1 do
                                    let arg = arg.GenericArguments.[index]

                                    if index <> 0 then
                                        TextNode.Space
                                        TextNode.Arrow
                                        TextNode.Space

                                    renderParameterType false arg ]

                          // Try to detect curried case
                          // Like in:
                          // let create (f: ('T -> unit) -> (exn -> unit) -> unit): JS.Promise<'T> = jsNative
                          // FCS gives back an equivalent of :
                          // let create (f: ('T -> unit) -> ((exn -> unit) -> unit)): JS.Promise<'T> = jsNative
                          // So we try to detect it to avoid the extract Parents
                          match res with
                          | (TextNode.Node(TextNode.LeftParent :: _) :: _) -> TextNode.Node res

                          | _ ->
                              TextNode.Node
                                  [ TextNode.LeftParent

                                    yield! res

                                    TextNode.RightParent ]

                      else
                          TextNode.Text "Unkown syntax please open an issue" ]

            // If this is a top level function we don't neeed to add the parenthesis
            TextNode.Node
                [ if not isTopLevel then
                      TextNode.LeftParent

                  TextNode.Node result

                  if not isTopLevel then
                      TextNode.RightParent ]

        else
            let separator = TextNode.Node [ TextNode.Space; TextNode.Comma ]

            let result =
                [ for index in 0 .. typ.GenericArguments.Count - 1 do
                      let arg = typ.GenericArguments.[index]

                      // Add the separator if this is not the first argument
                      if index <> 0 then
                          separator

                      if arg.IsGenericParameter then
                          TextNode.Tick
                          TextNode.Text arg.GenericParameter.DisplayName
                      else
                          // TODO: Generate an URL with the version of the package

                          let url =
                              // FIXME: This is a temporary fix to avoid the error
                              try
                                  arg.TypeDefinition.FullName
                                  |> String.toLower
                                  |> String.replace "." "-"
                                  |> String.append ".html"
                              with _ ->
                                  ""

                          let subType = renderParameterType false arg

                          TextNode.Anchor(url, arg.TypeDefinition.DisplayName)
                          TextNode.LessThan

                          subType

                          TextNode.GreaterThan ]

            TextNode.Node result

    type ParamTypesInformation =
        { Infos: (string * TextNode) list
          MaxNameLength: int
          MaxReturnTypeLength: int }

        static member Init(entityName: string) =
            { Infos = []
              MaxNameLength = entityName.Length
              MaxReturnTypeLength = 0 }

    /// <summary>
    /// Extracts parameter types information from a list of parameter types.
    ///
    /// The goals is to extract the information about the max length of the name and the return type
    /// to be able to format the information in a nice way.
    ///
    /// It will allows us to align the colon, arrows and other symbols.
    /// </summary>
    /// <param name="state">The current state of parameter types information</param>
    /// <param name="paramTypes">The list of parameter types to extract information from</param>
    /// <returns>The list of parameters and the max length of the name and return type</returns>
    let rec extractParamTypesInformation
        (state: ParamTypesInformation)
        (paramTypes: list<Choice<FSharpParameter, FSharpField> * string * ApiDocHtml>)
        =

        match paramTypes with
        | paramType :: tail ->
            match paramType with
            | Choice1Of2 fsharpParameter, name, _apiDoc ->
                let returnType = renderParameterType true fsharpParameter.Type

                let newState =
                    { state with
                        Infos = state.Infos @ [ name, returnType ]
                        MaxNameLength = System.Math.Max(state.MaxNameLength, name.Length)
                        MaxReturnTypeLength = System.Math.Max(state.MaxReturnTypeLength, returnType.Length) }

                extractParamTypesInformation newState tail

            // TODO: I didn't encounter this case yet, so I a not sure how to handle it
            | Choice2Of2 _fsharpField, _name, _apiDoc ->
                let newState =
                    { state with
                        Infos =
                            state.Infos
                            @ [ "TODO: extractParamTypesInformation -> fsharpField", TextNode.Div [] ] }

                failwith "Not implemented"

                extractParamTypesInformation newState tail

        | [] -> state
