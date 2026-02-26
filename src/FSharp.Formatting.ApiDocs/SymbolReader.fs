namespace rec FSharp.Formatting.ApiDocs

open System
open System.Reflection
open System.Collections.Generic
open System.Text
open System.IO
open System.Web
open System.Xml
open System.Xml.Linq

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Formatting.Common
open FSharp.Formatting.Internal
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Templating
open FSharp.Patterns
open FSharp.Compiler.Syntax

[<AutoOpen>]
module internal SymbolReader =
    type ReadingContext =
        { PublicOnly: bool
          Assembly: AssemblyName
          XmlMemberMap: IDictionary<string, XElement>
          UrlMap: CrossReferenceResolver
          WarnOnMissingDocs: bool
          MarkdownComments: bool
          UrlRangeHighlight: Uri -> int -> int -> string
          SourceFolderRepository: (string * string) option
          AssemblyPath: string
          CompilerOptions: string
          Substitutions: Substitutions }

        member x.XmlMemberLookup(key) =
            match x.XmlMemberMap.TryGetValue(key) with
            | true, v -> Some v
            | _ -> None

        static member internal Create
            (
                publicOnly,
                assembly,
                map,
                sourceFolderRepo,
                urlRangeHighlight,
                mdcomments,
                urlMap,
                assemblyPath,
                fscOptions,
                substitutions,
                warn
            ) =

            { PublicOnly = publicOnly
              Assembly = assembly
              XmlMemberMap = map
              MarkdownComments = mdcomments
              WarnOnMissingDocs = warn
              UrlMap = urlMap
              UrlRangeHighlight = urlRangeHighlight
              SourceFolderRepository = sourceFolderRepo
              AssemblyPath = assemblyPath
              CompilerOptions = fscOptions
              Substitutions = substitutions }

    let inline private getCompiledName (s: ^a :> FSharpSymbol) =
        let compiledName = (^a: (member CompiledName: string) (s))

        match compiledName = s.DisplayName with
        | true -> None
        | _ -> Some compiledName

    let readAttribute (attribute: FSharpAttribute) =
        let name = attribute.AttributeType.DisplayName
        let fullName = attribute.AttributeType.FullName

        let constructorArguments = attribute.ConstructorArguments |> Seq.map snd |> Seq.toList

        let namedArguments =
            attribute.NamedArguments
            |> Seq.map (fun (_, name, _, value) -> (name, value))
            |> Seq.toList

        ApiDocAttribute(name, fullName, constructorArguments, namedArguments)

    let readAttributes (attributes: FSharpAttribute seq) =
        attributes |> Seq.map readAttribute |> Seq.toList

    let readMemberOrVal (ctx: ReadingContext) (v: FSharpMemberOrFunctionOrValue) =
        let requireQualifiedAccess =
            v.ApparentEnclosingEntity
            |> Option.exists (fun aee ->
                hasAttrib<RequireQualifiedAccessAttribute> aee.Attributes
                // Hack for FSHarp.Core - `Option` module doesn't have RQA but really should have
                || (aee.Namespace = Some "Microsoft.FSharp.Core" && aee.DisplayName = "Option")
                || (aee.Namespace = Some "Microsoft.FSharp.Core" && aee.DisplayName = "ValueOption"))

        let customOpName =
            match tryFindAttrib<CustomOperationAttribute> v.Attributes with
            | None -> None
            | Some v ->
                v.ConstructorArguments
                |> Seq.map snd
                |> Seq.tryPick (fun x ->
                    match x with
                    | :? string as s -> Some s
                    | _ -> None)

        // This module doesn't have RequireQualifiedAccessAttribute and anyway we want the name to show
        // usage of its members as Array.Parallel.map
        let specialCase1 =
            v.ApparentEnclosingEntity
            |> Option.exists (fun aee -> aee.TryFullName = Some "Microsoft.FSharp.Collections.ArrayModule.Parallel")

        let argInfos, retInfo = FSharpType.Prettify(v.CurriedParameterGroups, v.ReturnParameter)

        let argInfos = argInfos |> Seq.map Seq.toList |> Seq.toList

        // custom ops take curried synax
        let argInfos =
            if customOpName.IsSome then
                match List.map List.singleton (List.concat argInfos) with
                | _source :: rest -> rest
                | [] -> []
            else
                argInfos

        let isItemIndexer = (v.IsInstanceMember && v.DisplayName = "Item")

        let preferNoParens =
            customOpName.IsSome
            || isItemIndexer
            || not v.IsMember
            || PrettyNaming.IsLogicalOpName v.CompiledName
            || Char.IsLower(v.DisplayName.[0])

        let fullArgUsage =
            match argInfos with
            | [ [] ] when (v.IsProperty && v.HasGetterMethod) -> !!""
            | _ -> formatCurriedArgsUsageAsHtml preferNoParens isItemIndexer argInfos

        let usageHtml =

            match v.IsMember, v.IsInstanceMember, v.LogicalName, v.DisplayName, customOpName with
            // Constructors
            | _, _, ".ctor", _, _ ->
                span
                    []
                    [ match v.ApparentEnclosingEntity with
                      | None -> ()
                      | Some aee -> !!aee.DisplayName
                      fullArgUsage ]

            // Indexers
            | _, true, _, "Item", _ -> span [] [ !!"this["; fullArgUsage; !!"]" ]

            // Custom operators
            | _, _, _, _, Some name ->
                span
                    []
                    [ !!name
                      if preferNoParens then
                          !!"&#32;"
                          fullArgUsage ]

            // op_XYZ operators
            | _, false, _, name, _ when PrettyNaming.IsLogicalOpName v.CompiledName ->
                match argInfos with
                // binary operators (taking a tuple)
                | [ [ x; y ] ]
                // binary operators (curried, like in FSharp.Core.Operators)
                | [ [ x ]; [ y ] ] ->
                    let left = formatArgUsageAsHtml 0 x

                    let nm = PrettyNaming.ConvertValLogicalNameToDisplayNameCore v.CompiledName

                    let right = formatArgUsageAsHtml 1 y

                    span [] [ left; !!"&#32;"; encode nm; !!"&#32;"; right ]

                // unary operators
                | [ [ x ] ] ->
                    let nm = PrettyNaming.ConvertValLogicalNameToDisplayNameCore v.CompiledName

                    let right = formatArgUsageAsHtml 0 x

                    span [] [ encode nm; right ]
                | _ ->
                    span
                        []
                        [ !!name
                          if preferNoParens then
                              !!"&#32;"
                              fullArgUsage ]

            // Ordinary instance members
            | _, true, _, name, _ ->
                span
                    []
                    [ !!"this."
                      !!name
                      if preferNoParens then
                          !!"&#32;"
                          fullArgUsage ]

            // A hack for Array.Parallel.map in FSharp.Core. TODO: generalise this
            | _, false, _, name, _ when specialCase1 ->
                span
                    []
                    [ !!("Array.Parallel." + name)
                      if preferNoParens then
                          !!"&#32;"
                          fullArgUsage ]

            // Ordinary functions or values
            | false, _, _, name, _ when not requireQualifiedAccess ->
                span
                    []
                    [ !!name
                      if preferNoParens then
                          !!"&#32;"
                          fullArgUsage ]

            // Ordinary static members or things (?) that require fully qualified access
            | _, false, _, name, _ ->
                span
                    []
                    [ match v.ApparentEnclosingEntity with
                      | None -> !!name
                      | Some aee -> !!(aee.DisplayName + "." + name)
                      if preferNoParens then
                          !!"&#32;"
                      fullArgUsage ]

        let usageHtml = codeHtml usageHtml

        let modifiers =
            [ // TODO: v.Accessibility does not contain anything
              if v.InlineAnnotation = FSharpInlineAnnotation.AlwaysInline then
                  yield "inline"
              if v.IsDispatchSlot then
                  yield "abstract" ]

        let retType = retInfo.Type

        let argInfos, retType =
            match argInfos, v.HasGetterMethod, v.HasSetterMethod with
            | [ AllAndLast(args, last) ], _, true -> [ args ], Some last.Type
            | [ [] ], _, true -> [], Some retType
            | _, _, true -> argInfos, None
            | [ [] ], true, _ -> [], Some retType
            | _, _, _ -> argInfos, Some retType

        let paramTypes =
            argInfos
            |> List.concat
            |> List.mapi (fun i p ->
                let nm, ty = formatArgNameAndType i p

                let tyhtml = formatTypeAsHtml ctx.UrlMap ty |> codeHtml

                Choice1Of2 p, nm, tyhtml)

        // Extension members can have apparent parents which are not F# types.
        // Hence getting the generic argument count if this is a little trickier
        let numGenericParamsOfApparentParent =
            let pty = v.ApparentEnclosingEntity
            pty |> Option.map _.GenericParameters.Count |> Option.defaultValue 0

        // Ensure that there is enough number of elements to skip
        let tps =
            v.GenericParameters
            |> Seq.toList
            |> List.skip (min v.GenericParameters.Count numGenericParamsOfApparentParent)

        let typars = formatTypeArgumentsAsText tps

        //let cxs  = indexedConstraints v.GenericParameters
        let retTypeHtml = retType |> Option.map (formatTypeAsHtml ctx.UrlMap >> codeHtml)

        let returnType =
            match retType with
            | None -> None
            | Some retType ->
                if isUnitType retType then
                    None
                else
                    match retTypeHtml with
                    | None -> None
                    | Some html -> Some(retType, html)


        //let signatureTooltip =
        //  match argInfos with
        //  | [] -> retTypeText
        //  | [[x]] when (v.IsPropertyGetterMethod || v.HasGetterMethod) && x.Name.IsNone && isUnitType x.Type -> retTypeText
        //  | _  -> (formatArgsUsageAsText true v argInfos) + " -> " + retTypeText

        let extendedType =
            if v.IsExtensionMember then
                try
                    match v.ApparentEnclosingEntity with
                    | Some aee -> Some(aee, formatTyconRefAsHtml ctx.UrlMap aee |> codeHtml)
                    | None -> None
                with _ ->
                    None
            else
                None

        // If there is a signature file, we should go for implementation file
        let loc = tryGetLocation v

        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc

        ApiDocMemberDetails(
            usageHtml,
            paramTypes,
            returnType,
            modifiers,
            typars,
            extendedType,
            location,
            getCompiledName v
        )

    let readUnionCase (ctx: ReadingContext) (_typ: FSharpEntity) (case: FSharpUnionCase) =

        let formatFieldUsage (field: FSharpField) =
            if field.Name.StartsWith("Item", StringComparison.Ordinal) then
                formatTypeAsHtml ctx.UrlMap field.FieldType
            else
                !!field.Name

        let fields = case.Fields |> List.ofSeq

        let nm =
            if case.Name = "op_ColonColon" then "::"
            elif case.Name = "op_Nil" then "[]"
            else case.Name

        let usageHtml =
            let fieldsHtmls = fields |> List.map formatFieldUsage

            if case.Name = "op_ColonColon" then
                span [] [ fieldsHtmls.[0]; !!"&#32;"; !!nm; fieldsHtmls.[1] ] |> codeHtml
            else
                match fieldsHtmls with
                | [] -> span [] [ !!nm ]
                | [ fieldHtml ] -> span [] [ !!nm; !!"&#32;"; fieldHtml ]
                | _ ->
                    let fieldHtml = fieldsHtmls |> Html.sepWith ",&#32;"

                    span [] [ !!nm; !!"("; fieldHtml; !!")" ]
                |> codeHtml

        let paramTypes =
            fields
            |> List.map (fun fld ->
                let nm = fld.Name

                let html = formatTypeAsHtml ctx.UrlMap fld.FieldType |> codeHtml

                Choice2Of2 fld, nm, html)

        let returnType = None
        //if isUnitType retType then None else Some retTypeText

        let modifiers = List.empty
        let typeParams = List.empty

        //let signatureTooltip =
        //   match fields with
        //   | [] -> retTypeText
        //   | _ -> (fields |> List.map (fun field -> formatTypeAsText field.FieldType) |> String.concat " * ") + " -> " + retTypeText
        let loc = tryGetLocation case

        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc

        ApiDocMemberDetails(
            usageHtml,
            paramTypes,
            returnType,
            modifiers,
            typeParams,
            None,
            location,
            getCompiledName case
        )

    let readFSharpField (ctx: ReadingContext) (field: FSharpField) =
        let usageHtml = !!field.Name |> codeHtml

        let modifiers =
            [ if field.IsMutable then
                  yield "mutable"
              if field.IsStatic then
                  yield "static" ]

        let typeParams = List.empty
        //let signatureTooltip = formatTypeAsText field.FieldType
        let paramTypes = []

        let retType = field.FieldType

        let retTypeHtml = retType |> (formatTypeAsHtml ctx.UrlMap >> codeHtml)

        let returnType =
            if isUnitType retType then
                None
            else
                Some(retType, retTypeHtml)

        let loc = tryGetLocation field

        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc

        ApiDocMemberDetails(
            usageHtml,
            paramTypes,
            returnType,
            modifiers,
            typeParams,
            None,
            location,
            if field.Name <> field.DisplayName then
                Some field.Name
            else
                None
        )

    let getFSharpStaticParamXmlSig (typeProvider: FSharpEntity) parameterName =
        "SP:"
        + typeProvider.AccessPath
        + "."
        + typeProvider.LogicalName
        + "."
        + parameterName

    let readFSharpStaticParam (ctx: ReadingContext) (staticParam: FSharpStaticParameter) =
        let usageHtml =
            span
                []
                [ !!staticParam.Name
                  !!":&#32;"
                  formatTypeAsHtml ctx.UrlMap staticParam.Kind
                  !!(if staticParam.IsOptional then
                         sprintf " (optional, default = %A)" staticParam.DefaultValue
                     else
                         "") ]
            |> codeHtml

        let modifiers = List.empty
        let typeParams = List.empty
        let paramTypes = []
        let returnType = None
        //let signatureTooltip = formatTypeAsText staticParam.Kind + (if staticParam.IsOptional then sprintf " (optional, default = %A)" staticParam.DefaultValue else "")

        let loc = tryGetLocation staticParam

        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc

        ApiDocMemberDetails(
            usageHtml,
            paramTypes,
            returnType,
            modifiers,
            typeParams,
            None,
            location,
            if staticParam.Name <> staticParam.DisplayName then
                Some staticParam.Name
            else
                None
        )

    let removeSpaces (comment: string) =
        use reader = new StringReader(comment)

        let lines =
            [ let mutable line = ""

              while (line <- reader.ReadLine()
                     not (isNull line)) do
                  yield line ]

        String.removeSpaces lines

    let readMarkdownCommentAsHtml el (doc: LiterateDocument) =
        let groups = System.Collections.Generic.List<(_ * _)>()

        let mutable current = "<default>"
        groups.Add(current, [])

        let raw =
            match doc.Source with
            | LiterateSource.Markdown(string) -> [ KeyValuePair(current, string) ]
            | LiterateSource.Script _ -> []

        for par in doc.Paragraphs do
            match par with
            | Heading(2, [ Literal(text, _) ], _) ->
                current <- text.Trim()
                groups.Add(current, [ par ])
            | par -> groups.[groups.Count - 1] <- (current, par :: snd (groups.[groups.Count - 1]))

        // TODO: properly crack exceptions and parameters section of markdown docs, which have structure
        let groups = groups |> Seq.toList

        let summary, rest = groups |> List.partition (fun (section, _) -> section = "<default>")

        let notes, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Notes")

        let examples, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Examples")

        let returns, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Returns")

        let remarks, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Remarks")
        //let exceptions, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Exceptions")
        //let parameters, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Parameters")

        // tailOrEmpty drops the section headings, though not for summary which is implicit
        let summary = summary |> List.collect (snd >> List.rev)

        let returns = returns |> List.collect (snd >> List.rev) |> List.tailOrEmpty

        let examples = examples |> List.map (snd >> List.rev) |> List.tailOrEmpty

        let notes = notes |> List.map (snd >> List.rev) |> List.tailOrEmpty
        //let exceptions = exceptions |> List.collect (snd >> List.rev) |> List.tailOrEmpty
        //let parameters = parameters |> List.collect (snd >> List.rev) |> List.tailOrEmpty

        // All unclassified things go in 'remarks'
        let remarks =
            (remarks |> List.collect (snd >> List.rev) |> List.tailOrEmpty)
            @ (rest |> List.collect (snd >> List.rev))

        let summary = ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = summary)), None)

        let remarks =
            if remarks.IsEmpty then
                None
            else
                Some(ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = remarks)), None))
        //let exceptions = [ for e in exceptions -> ApiDocHtml(Literate.ToHtml(doc.With(paragraphs=[e]))) ]
        let notes = [ for e in notes -> ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = e)), None) ]

        let examples = [ for e in examples -> ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = e)), None) ]

        let returns =
            if returns.IsEmpty then
                None
            else
                Some(ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = returns)), None))

        ApiDocComment(
            xmldoc = Some el,
            summary = summary,
            remarks = remarks,
            parameters = [],
            returns = returns,
            examples = examples,
            notes = notes,
            exceptions = [],
            rawData = raw
        )

    let findCommand cmd =
        match cmd with
        | StringPosition.StartsWithWrapped ("[", "]") (ParseCommand(k, v), _rest) -> Some(k, v)
        | _ -> None

    /// Wraps the summary content in a <pre> tag if it is multiline and has different column indentations.
    let readXmlElementAsSingleSummary (e: XElement) =
        let text = e.Value

        let nonEmptyLines =
            e.Value.Split([| '\n' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter (String.IsNullOrWhiteSpace >> not)

        if nonEmptyLines.Length = 1 then
            nonEmptyLines.[0]
        else
            let allLinesHaveSameColumn =
                nonEmptyLines
                |> Array.map (fun line -> line |> Seq.takeWhile (fun c -> c = ' ') |> Seq.length)
                |> Array.distinct
                |> Array.length
                |> (=) 1

            let trimmed = text.TrimStart([| '\n'; '\r' |]).TrimEnd()

            if allLinesHaveSameColumn then
                trimmed
            else
                $"<pre>%s{trimmed}</pre>"

    let rec readXmlElementAsHtml
        anyTagsOK
        (urlMap: CrossReferenceResolver)
        (cmds: IDictionary<_, _>)
        (html: StringBuilder)
        (e: XElement)
        =
        for x in e.Nodes() do
            if x.NodeType = XmlNodeType.Text then
                let text = (x :?> XText).Value

                match findCommand (text, MarkdownRange.zero) with
                | Some(k, v) -> cmds.Add(k, v)
                | None -> html.Append(HttpUtility.HtmlEncode text) |> ignore
            elif x.NodeType = XmlNodeType.Element then
                let elem = x :?> XElement

                match elem.Name.LocalName with
                | "list" ->
                    html.Append("<ul>") |> ignore
                    readXmlElementAsHtml anyTagsOK urlMap cmds html elem
                    html.Append("</ul>") |> ignore
                | "item" ->
                    html.Append("<li>") |> ignore
                    readXmlElementAsHtml anyTagsOK urlMap cmds html elem
                    html.Append("</li>") |> ignore
                | "para" ->
                    html.Append("<p class='fsdocs-para'>") |> ignore
                    readXmlElementAsHtml anyTagsOK urlMap cmds html elem
                    html.Append("</p>") |> ignore
                | "paramref" ->
                    let name = elem.Attribute(XName.Get "name")
                    let nameAsHtml = HttpUtility.HtmlEncode name.Value

                    if not (isNull name) then
                        html.AppendFormat("<span class=\"fsdocs-param-name\">{0}</span>", nameAsHtml)
                        |> ignore
                | "see"
                | "seealso" ->
                    let cref = elem.Attribute(XName.Get "cref")

                    if not (isNull cref) then
                        if System.String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
                            printfn "ignoring invalid cref specified in: %A" e

                        // Older FSharp.Core cref listings don't start with "T:", see https://github.com/dotnet/fsharp/issues/9805
                        let cname = cref.Value

                        let cname = if cname.Contains(":") then cname else "T:" + cname

                        match urlMap.ResolveCref cname with
                        | Some reference ->
                            html.AppendFormat("<a href=\"{0}\">{1}</a>", reference.ReferenceLink, reference.NiceName)
                            |> ignore
                        | _ ->
                            urlMap.ResolveCref cname |> ignore
                            let crefAsHtml = HttpUtility.HtmlEncode cref.Value
                            html.Append(crefAsHtml) |> ignore
                | "c" ->
                    html.Append("<code>") |> ignore

                    let code = elem.Value.TrimEnd('\r', '\n', ' ')
                    let codeAsHtml = HttpUtility.HtmlEncode code
                    html.Append(codeAsHtml) |> ignore

                    html.Append("</code>") |> ignore
                | "code" ->
                    let code =
                        let code = Literate.ParseMarkdownString("```\n" + elem.Value.TrimEnd('\r', '\n', ' ') + "\n```")
                        Literate.ToHtml(code, lineNumbers = false)

                    html.Append(code) |> ignore
                // 'a' is not part of the XML doc standard but is widely used
                | "a" -> html.Append(elem.ToString()) |> ignore
                // This allows any HTML to be transferred through
                | _ ->
                    if anyTagsOK then
                        let elemAsXml = elem.ToString()
                        html.Append(elemAsXml) |> ignore

    let (|SummaryWithoutChildren|_|) (e: XElement) =
        if e.Name.LocalName = "summary" && not e.HasElements then
            Some e
        else
            None

    let readXmlCommentAsHtmlAux
        summaryExpected
        (urlMap: CrossReferenceResolver)
        (doc: XElement)
        (cmds: IDictionary<_, _>)
        =
        let rawData = new Dictionary<string, string>()
        // not part of the XML doc standard
        let nsels =
            let ds = doc.Elements(XName.Get "namespacedoc")

            if Seq.length ds > 0 then Some(Seq.toList ds) else None

        let summary =
            match Seq.tryExactlyOne (doc.Elements()) with
            | Some(SummaryWithoutChildren e) -> ApiDocHtml(readXmlElementAsSingleSummary e, None)
            | Some _
            | None ->
                if summaryExpected then
                    let summaries = doc.Elements(XName.Get "summary") |> Seq.toList

                    let html = new StringBuilder()

                    for (id, e) in List.indexed summaries do
                        let n = if id = 0 then "summary" else "summary-" + string<int> id

                        rawData.[n] <- e.Value
                        readXmlElementAsHtml true urlMap cmds html e

                    ApiDocHtml(html.ToString(), None)
                else
                    let html = new StringBuilder()
                    readXmlElementAsHtml false urlMap cmds html doc
                    ApiDocHtml(html.ToString(), None)

        let paramNodes = doc.Elements(XName.Get "param") |> Seq.toList

        let parameters =
            [ for e in paramNodes do
                  let paramName = e.Attribute(XName.Get "name").Value
                  let phtml = new StringBuilder()
                  readXmlElementAsHtml true urlMap cmds phtml e
                  let paramHtml = ApiDocHtml(phtml.ToString(), None)
                  paramName, paramHtml ]

        for e in doc.Elements(XName.Get "exclude") do
            cmds.["exclude"] <- e.Value

        for e in doc.Elements(XName.Get "omit") do
            cmds.["omit"] <- e.Value

        for e in doc.Elements(XName.Get "category") do
            match e.Attribute(XName.Get "index") with
            | null -> ()
            | a -> cmds.["categoryindex"] <- a.Value

            cmds.["category"] <- e.Value

        let remarks =
            let remarkNodes = doc.Elements(XName.Get "remarks") |> Seq.toList

            if not (List.isEmpty remarkNodes) then
                let html = new StringBuilder()

                for (id, e) in List.indexed remarkNodes do
                    let n = if id = 0 then "remarks" else "remarks-" + string<int> id

                    rawData.[n] <- e.Value
                    readXmlElementAsHtml true urlMap cmds html e

                ApiDocHtml(html.ToString(), None) |> Some
            else
                None

        let returns =
            let html = new StringBuilder()

            let returnNodes = doc.Elements(XName.Get "returns") |> Seq.toList

            if returnNodes.Length > 0 then
                for (id, e) in List.indexed returnNodes do
                    let n = if id = 0 then "returns" else "returns-" + string<int> id

                    rawData.[n] <- e.Value
                    readXmlElementAsHtml true urlMap cmds html e

                Some(ApiDocHtml(html.ToString(), None))
            else
                None

        let exceptions =
            let exceptionNodes = doc.Elements(XName.Get "exception") |> Seq.toList

            [ for e in exceptionNodes do
                  let cref = e.Attribute(XName.Get "cref")

                  if not (isNull cref) then
                      if String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
                          printfn "Warning: Invalid cref specified in: %A" doc

                      else
                          // FSharp.Core cref listings don't start with "T:", see https://github.com/dotnet/fsharp/issues/9805
                          let cname = cref.Value

                          let cname =
                              if cname.StartsWith("T:", StringComparison.Ordinal) then
                                  cname
                              else
                                  "T:" + cname // FSharp.Core exception listings don't start with "T:"

                          match urlMap.ResolveCref cname with
                          | Some reference ->
                              let html = new StringBuilder()
                              let referenceLinkId = "exception-" + reference.NiceName
                              rawData.[referenceLinkId] <- reference.ReferenceLink
                              readXmlElementAsHtml true urlMap cmds html e
                              reference.NiceName, Some reference.ReferenceLink, ApiDocHtml(html.ToString(), None)
                          | _ ->
                              let html = new StringBuilder()
                              readXmlElementAsHtml true urlMap cmds html e
                              cname, None, ApiDocHtml(html.ToString(), None) ]

        let examples =
            let exampleNodes = doc.Elements(XName.Get "example") |> Seq.toList

            [ for (id, e) in List.indexed exampleNodes do
                  let html = new StringBuilder()

                  let exampleId =
                      match e.TryAttr "id" with
                      | None -> if id = 0 then "example" else "example-" + string<int> id
                      | Some attrId -> attrId

                  rawData.[exampleId] <- e.Value
                  readXmlElementAsHtml true urlMap cmds html e
                  ApiDocHtml(html.ToString(), Some exampleId) ]

        let notes =
            let noteNodes = doc.Elements(XName.Get "note") |> Seq.toList
            // 'note' is not part of the XML doc standard but is supported by Sandcastle and other tools
            [ for (id, e) in List.indexed noteNodes do
                  let html = new StringBuilder()

                  let n = if id = 0 then "note" else "note-" + string<int> id

                  rawData.[n] <- e.Value
                  readXmlElementAsHtml true urlMap cmds html e
                  ApiDocHtml(html.ToString(), None) ]

        // put the non-xmldoc sections into rawData
        doc.Descendants()
        |> Seq.filter (fun n ->
            let ln = n.Name.LocalName

            ln <> "summary"
            && ln <> "param"
            && ln <> "exceptions"
            && ln <> "example"
            && ln <> "note"
            && ln <> "returns"
            && ln <> "remarks")
        |> Seq.groupBy (fun n -> n.Name.LocalName)
        |> Seq.iter (fun (n, lst) ->
            let lst = Seq.toList lst

            match lst with
            | [ x ] -> rawData.[n] <- x.Value
            | lst -> lst |> List.iteri (fun id el -> rawData.[n + "-" + string<int> id] <- el.Value))

        let rawData = rawData |> Seq.toList

        let comment =
            ApiDocComment(
                xmldoc = Some doc,
                summary = summary,
                remarks = remarks,
                parameters = parameters,
                returns = returns,
                examples = examples,
                notes = notes,
                exceptions = exceptions,
                rawData = rawData
            )

        comment, nsels

    let combineHtml (h1: ApiDocHtml) (h2: ApiDocHtml) =
        ApiDocHtml(String.concat "\n" [ h1.HtmlText; h2.HtmlText ], None)

    let combineHtmlOptions (h1: ApiDocHtml option) (h2: ApiDocHtml option) =
        match h1, h2 with
        | x, None -> x
        | None, x -> x
        | Some x, Some y -> Some(combineHtml x y)

    let combineComments (c1: ApiDocComment) (c2: ApiDocComment) =
        ApiDocComment(
            xmldoc =
                (match c1.Xml with
                 | None -> c2.Xml
                 | v -> v),
            summary = combineHtml c1.Summary c2.Summary,
            remarks = combineHtmlOptions c1.Remarks c2.Remarks,
            parameters = c1.Parameters @ c2.Parameters,
            examples = c1.Examples @ c2.Examples,
            returns = combineHtmlOptions c1.Returns c2.Returns,
            notes = c1.Notes @ c2.Notes,
            exceptions = c1.Exceptions @ c2.Exceptions,
            rawData = c1.RawData @ c2.RawData
        )

    let combineNamespaceDocs nspDocs =
        nspDocs
        |> List.choose id
        |> function
            | [] -> None
            | xs -> Some(List.reduce combineComments xs)

    let rec readXmlCommentAsHtml (urlMap: CrossReferenceResolver) (doc: XElement) (cmds: IDictionary<_, _>) =
        let doc, nsels = readXmlCommentAsHtmlAux true urlMap doc cmds

        let nsdocs = readNamespaceDocs urlMap nsels
        doc, nsdocs

    and readNamespaceDocs (urlMap: CrossReferenceResolver) (nsels: XElement list option) =
        let nscmds = Dictionary() :> IDictionary<_, _>

        nsels
        |> Option.map (
            List.map (fun n -> fst (readXmlCommentAsHtml urlMap n nscmds))
            >> List.reduce combineComments
        )

    /// Returns all indirect links in a specified span node
    let rec collectSpanIndirectLinks span =
        seq {
            match span with
            | IndirectLink(_, _, key, _) -> yield key
            | MarkdownPatterns.SpanLeaf _ -> ()
            | MarkdownPatterns.SpanNode(_, spans) ->
                for s in spans do
                    yield! collectSpanIndirectLinks s
        }

    /// Returns all indirect links in the specified paragraph node
    let rec collectParagraphIndirectLinks par =
        seq {
            match par with
            | MarkdownPatterns.ParagraphLeaf _ -> ()
            | MarkdownPatterns.ParagraphNested(_, pars) ->
                for ps in pars do
                    for p in ps do
                        yield! collectParagraphIndirectLinks p
            | MarkdownPatterns.ParagraphSpans(_, spans) ->
                for s in spans do
                    yield! collectSpanIndirectLinks s
        }

    /// Returns whether the link is not included in the document defined links
    let linkDefined (doc: LiterateDocument) (link: string) =
        [ link; link.Replace("\r\n", ""); link.Replace("\r\n", " "); link.Replace("\n", ""); link.Replace("\n", " ") ]
        |> List.exists (fun key -> doc.DefinedLinks.ContainsKey(key))

    /// Returns a tuple of the undefined link and its Cref if it exists
    let getTypeLink (ctx: ReadingContext) undefinedLink =
        // Append 'T:' to try to get the link from urlmap
        match ctx.UrlMap.ResolveCref("T:" + undefinedLink) with
        | Some cRef -> if cRef.IsInternal then Some(undefinedLink, cRef) else None
        | None -> None

    /// Adds a cross-type link to the document defined links
    let addLinkToType (doc: LiterateDocument) link =
        match link with
        | Some(k, v) -> do doc.DefinedLinks.Add(k, (v.ReferenceLink, Some v.NiceName))
        | None -> ()

    /// Wraps the span inside an IndirectLink if it is an inline code that can be converted to a link
    let wrapInlineCodeLinksInSpans (ctx: ReadingContext) span =
        match span with
        | InlineCode(code, r) ->
            match getTypeLink ctx code with
            | Some _ -> IndirectLink([ span ], code, code, r)
            | None -> span
        | _ -> span

    /// Wraps inside an IndirectLink all inline code spans in the paragraph that can be converted to a link
    let rec wrapInlineCodeLinksInParagraphs (ctx: ReadingContext) (para: MarkdownParagraph) =
        match para with
        | MarkdownPatterns.ParagraphLeaf _ -> para
        | MarkdownPatterns.ParagraphNested(info, pars) ->
            MarkdownPatterns.ParagraphNested(
                info,
                pars
                |> List.map (fun innerPars -> List.map (wrapInlineCodeLinksInParagraphs ctx) innerPars)
            )
        | MarkdownPatterns.ParagraphSpans(info, spans) ->
            MarkdownPatterns.ParagraphSpans(info, List.map (wrapInlineCodeLinksInSpans ctx) spans)

    /// Adds the missing links to types to the document defined links
    let addMissingLinkToTypes ctx (doc: LiterateDocument) =
        let replacedParagraphs = doc.Paragraphs |> List.map (wrapInlineCodeLinksInParagraphs ctx)

        do
            replacedParagraphs
            |> Seq.collect collectParagraphIndirectLinks
            |> Seq.choose (fun line ->
                if linkDefined doc line then
                    None
                else
                    getTypeLink ctx line |> Some)
            |> Seq.iter (addLinkToType doc)

        doc.With(paragraphs = replacedParagraphs)

    let readMarkdownCommentAndCommands (ctx: ReadingContext) text el (cmds: IDictionary<_, _>) =
        let lines = removeSpaces text |> List.map (fun s -> (s, MarkdownRange.zero))

        let text =
            lines
            |> List.choose (fun line ->
                match findCommand line with
                | Some(k, v) ->
                    cmds.[k] <- v
                    None
                | _ -> fst line |> Some)
            |> String.concat "\n"

        let doc =
            Literate.ParseMarkdownString(
                text,
                path = Path.Combine(ctx.AssemblyPath, "docs.fsx"),
                fscOptions = ctx.CompilerOptions
            )

        let doc = doc |> addMissingLinkToTypes ctx
        let html = readMarkdownCommentAsHtml el doc
        // TODO: namespace summaries for markdown comments
        let nsdocs = None
        cmds, html, nsdocs

    let readXmlCommentAndCommands (ctx: ReadingContext) text el (cmds: IDictionary<_, _>) =
        let lines = removeSpaces text |> List.map (fun s -> (s, MarkdownRange.zero))

        let html, nsdocs = readXmlCommentAsHtml ctx.UrlMap el cmds

        lines
        |> Seq.choose findCommand
        |> Seq.iter (fun (k, v) ->
            printfn
                "The use of `[%s]` and other commands in XML comments is deprecated, please use XML extensions, see https://github.com/fsharp/fslang-design/blob/master/tooling/FST-1031-xmldoc-extensions.md"
                k

            cmds.[k] <- v)

        cmds, html, nsdocs

    let readCommentAndCommands (ctx: ReadingContext) xmlSig (m: range option) =
        let cmds = Dictionary<string, string>() :> IDictionary<_, _>

        match ctx.XmlMemberLookup(xmlSig) with
        | None ->
            if not (System.String.IsNullOrEmpty xmlSig) then
                if ctx.WarnOnMissingDocs then
                    let m = defaultArg m range0

                    if ctx.UrlMap.IsLocal xmlSig then
                        printfn
                            "%s(%d,%d): warning FD0001: no documentation for '%s'"
                            m.FileName
                            m.StartLine
                            m.StartColumn
                            xmlSig

            cmds, ApiDocComment.Empty, None
        | Some el ->
            let sum = el.Element(XName.Get "summary")

            // sum can be null with null/empty el.Value when an non-"<summary>" XML element appears
            // as the only '///' documentation command:
            //
            // 1.
            //  // Not triple-slash ccomment
            //  /// <exclude/>
            //
            // 2.
            //  /// <exclude/>
            if isNull sum then
                let doc, nsels = readXmlCommentAsHtmlAux false ctx.UrlMap el cmds

                let nsdocs = readNamespaceDocs ctx.UrlMap nsels
                cmds, doc, nsdocs
            else if ctx.MarkdownComments then
                readMarkdownCommentAndCommands ctx sum.Value el cmds
            else
                if sum.Value.Contains("<exclude") then
                    cmds.["exclude"] <- ""

                    printfn
                        "Warning: detected \"<exclude/>\" in text of \"<summary>\" for \"%s\". Please see https://fsprojects.github.io/FSharp.Formatting/apidocs.html#Classic-XML-Doc-Comments"
                        xmlSig

                readXmlCommentAndCommands ctx sum.Value el cmds

    /// Reads XML documentation comments and calls the specified function
    /// to parse the rest of the entity, unless [omit] command is set.
    /// The function is called with category name, commands & comment.
    let readCommentsInto (sym: FSharpSymbol) ctx xmlDocSig f =
        let cmds, comment, nsdocs = readCommentAndCommands ctx xmlDocSig sym.DeclarationLocation

        match cmds with
        | Command "category" cat
        | Let "" (cat, _) ->
            let catindex =
                match cmds with
                | Command "categoryindex" idx
                | Let "1000" (idx, _) ->
                    (try
                        int idx
                     with _ ->
                         Int32.MaxValue)

            let exclude =
                match cmds with
                | Command "omit" v
                | Command "exclude" v
                | Let "false" (v, _) -> (v <> "false")

            let isCompilerHidden =
                let attribs =
                    match sym with
                    | :? FSharpMemberOrFunctionOrValue as mfv -> mfv.Attributes :> FSharpAttribute seq
                    | :? FSharpEntity as ent -> ent.Attributes :> FSharpAttribute seq
                    | _ -> Seq.empty

                attribs
                |> Seq.exists (fun a ->
                    a.AttributeType.FullName = "Microsoft.FSharp.Core.CompilerMessageAttribute"
                    && a.NamedArguments
                       |> Seq.exists (fun (_, name, _, value) -> name = "IsHidden" && (value :?> bool) = true))

            let exclude = exclude || isCompilerHidden

            try
                Some(f cat catindex exclude cmds comment, nsdocs)
            with e ->
                let name =
                    try
                        sym.FullName
                    with _ ->
                        try
                            sym.DisplayName
                        with _ ->
                            let part =
                                try
                                    let ass = sym.Assembly

                                    match ass.FileName with
                                    | Some file -> file
                                    | None -> ass.QualifiedName
                                with _ ->
                                    "unknown"

                            sprintf "unknown, part of %s" part

                printfn "Could not read comments from entity '%s': %O" name e
                None

    let checkAccess ctx (access: FSharpAccessibility) = not ctx.PublicOnly || access.IsPublic

    let collectNamespaceDocs results =
        results
        |> List.unzip
        |> function
            | (results, nspDocs) -> (results, combineNamespaceDocs nspDocs)

    let readChildren ctx (entities: FSharpEntity seq) reader cond =
        entities
        |> Seq.filter (fun v -> checkAccess ctx v.Accessibility && cond v)
        |> Seq.sortBy (fun (c: FSharpEntity) -> c.DisplayName)
        |> Seq.choose (reader ctx)
        |> List.ofSeq
        |> collectNamespaceDocs

    let tryReadMember (ctx: ReadingContext) entityUrl kind (memb: FSharpMemberOrFunctionOrValue) =
        readCommentsInto memb ctx (getXmlDocSigForMember memb) (fun cat catidx exclude _ comment ->
            let details = readMemberOrVal ctx memb

            ApiDocMember(
                memb.DisplayName,
                readAttributes memb.Attributes,
                entityUrl,
                kind,
                cat,
                catidx,
                exclude,
                details,
                comment,
                memb,
                ctx.WarnOnMissingDocs
            ))

    let readAllMembers ctx entityUrl kind (members: FSharpMemberOrFunctionOrValue seq) =
        members
        |> Seq.choose (fun v ->
            if
                checkAccess ctx v.Accessibility
                && not v.IsCompilerGenerated
                && not v.IsPropertyGetterMethod
                && not v.IsPropertySetterMethod
                && not v.IsEventAddMethod
                && not v.IsEventRemoveMethod
            then
                tryReadMember ctx entityUrl kind v
            else
                None)
        |> List.ofSeq
        |> collectNamespaceDocs

    let readMembers ctx entityUrl kind (entity: FSharpEntity) cond =
        entity.MembersFunctionsAndValues
        |> Seq.choose (fun v ->
            if checkAccess ctx v.Accessibility && not v.IsCompilerGenerated && cond v then
                tryReadMember ctx entityUrl kind v
            else
                None)
        |> List.ofSeq
        |> collectNamespaceDocs

    let readTypeNameAsText (typ: FSharpEntity) =
        typ.GenericParameters
        |> List.ofSeq
        |> List.map (fun p -> sprintf "'%s" p.Name)
        |> function
            | [] -> typ.DisplayName
            | gnames ->
                let gtext = String.concat ", " gnames

                if typ.UsesPrefixDisplay then
                    sprintf "%s<%s>" typ.DisplayName gtext
                else
                    sprintf "%s %s" gtext typ.DisplayName

    let readUnionCases ctx entityUrl (typ: FSharpEntity) =
        typ.UnionCases
        |> List.ofSeq
        |> List.choose (fun case ->
            if checkAccess ctx case.Accessibility |> not then
                None
            else
                readCommentsInto case ctx case.XmlDocSig (fun cat catidx exclude _ comment ->
                    let details = readUnionCase ctx typ case

                    ApiDocMember(
                        case.Name,
                        readAttributes case.Attributes,
                        entityUrl,
                        ApiDocMemberKind.UnionCase,
                        cat,
                        catidx,
                        exclude,
                        details,
                        comment,
                        case,
                        ctx.WarnOnMissingDocs
                    )))
        |> collectNamespaceDocs

    let readRecordFields ctx entityUrl (typ: FSharpEntity) =
        typ.FSharpFields
        |> List.ofSeq
        |> List.choose (fun field ->
            if field.IsCompilerGenerated then
                None
            else
                readCommentsInto field ctx field.XmlDocSig (fun cat catidx exclude _ comment ->
                    let details = readFSharpField ctx field

                    ApiDocMember(
                        field.Name,
                        readAttributes (Seq.append field.FieldAttributes field.PropertyAttributes),
                        entityUrl,
                        ApiDocMemberKind.RecordField,
                        cat,
                        catidx,
                        exclude,
                        details,
                        comment,
                        field,
                        ctx.WarnOnMissingDocs
                    )))
        |> collectNamespaceDocs

    let readStaticParams ctx entityUrl (typ: FSharpEntity) =
        typ.StaticParameters
        |> List.ofSeq
        |> List.choose (fun staticParam ->
            readCommentsInto
                staticParam
                ctx
                (getFSharpStaticParamXmlSig typ staticParam.Name)
                (fun cat catidx exclude _ comment ->
                    let details = readFSharpStaticParam ctx staticParam

                    ApiDocMember(
                        staticParam.Name,
                        [],
                        entityUrl,
                        ApiDocMemberKind.StaticParameter,
                        cat,
                        catidx,
                        exclude,
                        details,
                        comment,
                        staticParam,
                        ctx.WarnOnMissingDocs
                    )))
        |> collectNamespaceDocs

    let xmlDocText (xmlDoc: FSharpXmlDoc) =
        match xmlDoc with
        | FSharpXmlDoc.FromXmlText(xmlDoc) -> String.concat "" xmlDoc.UnprocessedLines
        | _ -> ""

    // Create a xml documentation snippet and add it to the XmlMemberMap
    let registerXmlDoc (ctx: ReadingContext) xmlDocSig (xmlDoc: string) =
        let xmlDoc =
            if xmlDoc.Contains "<summary>" then
                xmlDoc
            else
                "<summary>" + xmlDoc + "</summary>"

        let xmlDoc = "<member name=\"" + xmlDocSig + "\">" + xmlDoc + "</member>"

        let xmlDoc = XElement.Parse xmlDoc
        ctx.XmlMemberMap.Add(xmlDocSig, xmlDoc)
        xmlDoc

    // Provided types don't have their docs dumped into the xml file,
    // so we need to add them to the XmlMemberMap separately
    let registerProvidedTypeXmlDocs (ctx: ReadingContext) (typ: FSharpEntity) =
        let xmlDoc = registerXmlDoc ctx typ.XmlDocSig (xmlDocText typ.XmlDoc)

        xmlDoc.Elements(XName.Get "param")
        |> Seq.choose (fun p ->
            let nameAttr = p.Attribute(XName.Get "name")

            if isNull nameAttr then
                None
            else
                let xmlDocSig = getFSharpStaticParamXmlSig typ nameAttr.Value

                registerXmlDoc ctx xmlDocSig (Security.SecurityElement.Escape p.Value)
                |> ignore
                |> Some)
        |> ignore

    let rec readType (ctx: ReadingContext) (typ: FSharpEntity) =
        if typ.IsProvided && typ.XmlDoc <> FSharpXmlDoc.None then
            registerProvidedTypeXmlDocs ctx typ

        let xmlDocSig = getXmlDocSigForType typ

        readCommentsInto typ ctx xmlDocSig (fun cat catidx exclude _cmds comment ->
            let entityUrl = ctx.UrlMap.ResolveUrlBaseNameForEntity typ

            let rec getMembers (typ: FSharpEntity) =
                [ yield! typ.MembersFunctionsAndValues
                  match typ.BaseType with
                  | Some baseType ->
                      let loc = typ.DeclarationLocation

                      let cmds, _comment, _ =
                          readCommentAndCommands ctx (getXmlDocSigForType baseType.TypeDefinition) (Some loc)

                      match cmds with
                      | Command "exclude" _
                      | Command "omit" _ -> yield! getMembers baseType.TypeDefinition
                      | _ -> ()
                  | None -> () ]

            // Collect members inherited from non-excluded base types that are in the same docs set
            let rec getInheritedMemberGroups (typ: FSharpEntity) =
                [ match typ.BaseType with
                  | Some baseType ->
                      let bdef = baseType.TypeDefinition
                      let loc = typ.DeclarationLocation

                      let cmds, _comment, _ = readCommentAndCommands ctx (getXmlDocSigForType bdef) (Some loc)

                      match cmds with
                      | Command "exclude" _
                      | Command "omit" _ ->
                          // Base is excluded/omitted – its members are already folded in; recurse further
                          yield! getInheritedMemberGroups bdef
                      | _ ->
                          match ctx.UrlMap.TryResolveUrlBaseNameForEntity bdef with
                          | Some baseEntityUrl ->
                              let baseMembers =
                                  bdef.MembersFunctionsAndValues
                                  |> Seq.filter (fun v ->
                                      checkAccess ctx v.Accessibility
                                      && not v.IsCompilerGenerated
                                      && not v.IsOverrideOrExplicitInterfaceImplementation
                                      && not v.IsEventAddMethod
                                      && not v.IsEventRemoveMethod
                                      && not v.IsPropertyGetterMethod
                                      && not v.IsPropertySetterMethod
                                      && v.CompiledName <> ".ctor")
                                  |> Seq.choose (fun v ->
                                      let kind =
                                          if v.IsInstanceMember then
                                              ApiDocMemberKind.InstanceMember
                                          else
                                              ApiDocMemberKind.StaticMember

                                      match tryReadMember ctx baseEntityUrl kind v with
                                      | Some(m, _) when not m.Exclude -> Some m
                                      | _ -> None)
                                  |> List.ofSeq

                              let baseTypeHtml = baseType |> formatTypeAsHtml ctx.UrlMap |> codeHtml

                              if not (List.isEmpty baseMembers) then
                                  yield (baseTypeHtml, baseMembers)

                              yield! getInheritedMemberGroups bdef
                          | None ->
                              // Base type not in this docs set – skip but keep walking the chain
                              yield! getInheritedMemberGroups bdef
                  | None -> () ]

            let ivals, svals =
                getMembers typ
                |> Seq.filter (fun v ->
                    checkAccess ctx v.Accessibility
                    && not v.IsCompilerGenerated
                    && not v.IsOverrideOrExplicitInterfaceImplementation
                    && not v.IsEventAddMethod
                    && not v.IsEventRemoveMethod
                    && not v.IsPropertyGetterMethod
                    && not v.IsPropertySetterMethod)
                |> List.ofSeq
                |> List.partition (fun v -> v.IsInstanceMember)

            let cvals, svals = svals |> List.partition (fun v -> v.CompiledName = ".ctor")

            let baseType =
                typ.BaseType
                |> Option.map (fun bty -> bty, bty |> formatTypeAsHtml ctx.UrlMap |> codeHtml)

            let allInterfaces = [ for i in typ.AllInterfaces -> (i, formatTypeAsHtml ctx.UrlMap i |> codeHtml) ]

            let abbreviatedType =
                if typ.IsFSharpAbbreviation then
                    Some(typ.AbbreviatedType, formatTypeAsHtml ctx.UrlMap typ.AbbreviatedType |> codeHtml)
                else
                    None

            let delegateSignature =
                if typ.IsDelegate then
                    Some(
                        typ.FSharpDelegateSignature,
                        formatDelegateSignatureAsHtml ctx.UrlMap typ.DisplayName typ.FSharpDelegateSignature
                        |> codeHtml
                    )
                else
                    None

            let name = readTypeNameAsText typ
            let cases, nsdocs1 = readUnionCases ctx entityUrl typ
            let fields, nsdocs2 = readRecordFields ctx entityUrl typ
            let statParams, nsdocs3 = readStaticParams ctx entityUrl typ

            let attrs = readAttributes typ.Attributes

            let ctors, nsdocs4 = readAllMembers ctx entityUrl ApiDocMemberKind.Constructor cvals

            let inst, nsdocs5 = readAllMembers ctx entityUrl ApiDocMemberKind.InstanceMember ivals

            let stat, nsdocs6 = readAllMembers ctx entityUrl ApiDocMemberKind.StaticMember svals

            let rqa = hasAttrib<RequireQualifiedAccessAttribute> typ.Attributes

            let inheritedMembers = getInheritedMemberGroups typ

            let nsdocs = combineNamespaceDocs [ nsdocs1; nsdocs2; nsdocs3; nsdocs4; nsdocs5; nsdocs6 ]

            if nsdocs.IsSome then
                printfn "ignoring namespace summary on nested position"

            let loc = tryGetLocation typ

            let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc

            ApiDocEntity(
                true,
                name,
                cat,
                catidx,
                exclude,
                entityUrl,
                comment,
                ctx.Assembly,
                attrs,
                cases,
                fields,
                statParams,
                ctors,
                inst,
                stat,
                allInterfaces,
                baseType,
                abbreviatedType,
                delegateSignature,
                typ,
                [],
                [],
                [],
                [],
                rqa,
                location,
                ctx.Substitutions,
                inheritedMembers
            ))

    and readModule (ctx: ReadingContext) (modul: FSharpEntity) =
        readCommentsInto modul ctx modul.XmlDocSig (fun cat catidx exclude _cmd comment ->

            // Properties & value bindings in the module
            let entityUrl = ctx.UrlMap.ResolveUrlBaseNameForEntity modul

            let vals, nsdocs1 =
                readMembers ctx entityUrl ApiDocMemberKind.ValueOrFunction modul (fun v ->
                    not v.IsMember && not v.IsActivePattern)

            let exts, nsdocs2 =
                readMembers ctx entityUrl ApiDocMemberKind.TypeExtension modul (fun v -> v.IsExtensionMember)

            // `with get and set` syntax is sugar for a mutable field, a get binding and a set binding
            // This result in duplicated Method Extensions, we use DeclarationLocation to keep only one
            // See https://github.com/fsprojects/FSharp.Formatting/issues/941
            let exts = exts |> List.distinctBy (fun m -> m.Symbol.DeclarationLocation)

            let pats, nsdocs3 =
                readMembers ctx entityUrl ApiDocMemberKind.ActivePattern modul (fun v -> v.IsActivePattern)

            let attrs = readAttributes modul.Attributes
            // Nested modules and types
            let entities, nsdocs4 = readEntities ctx modul.NestedEntities

            let rqa =
                hasAttrib<RequireQualifiedAccessAttribute> modul.Attributes
                // Hack for FSHarp.Core - `Option` module doesn't have RQA but really should have
                || (modul.Namespace = Some "Microsoft.FSharp.Core" && modul.DisplayName = "Option")
                || (modul.Namespace = Some "Microsoft.FSharp.Core"
                    && modul.DisplayName = "ValueOption")

            let nsdocs = combineNamespaceDocs [ nsdocs1; nsdocs2; nsdocs3; nsdocs4 ]

            if nsdocs.IsSome then
                printfn "ignoring namespace summary on nested position"

            let loc = tryGetLocation modul

            let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc

            ApiDocEntity(
                false,
                modul.DisplayName,
                cat,
                catidx,
                exclude,
                entityUrl,
                comment,
                ctx.Assembly,
                attrs,
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                None,
                None,
                None,
                modul,
                entities,
                vals,
                exts,
                pats,
                rqa,
                location,
                ctx.Substitutions,
                []
            ))

    and readEntities ctx (entities: _ seq) =
        let modifiers, nsdocs1 = readChildren ctx entities readModule (fun x -> x.IsFSharpModule)

        let typs, nsdocs2 = readChildren ctx entities readType (fun x -> not x.IsFSharpModule)

        (modifiers @ typs), combineNamespaceDocs [ nsdocs1; nsdocs2 ]

    // ----------------------------------------------------------------------------------------------
    // Reading namespace and assembly details
    // ----------------------------------------------------------------------------------------------

    let stripMicrosoft (str: string) =
        if str.StartsWith("Microsoft.", StringComparison.Ordinal) then
            str.["Microsoft.".Length ..]
        elif str.StartsWith("microsoft-", StringComparison.Ordinal) then
            str.["microsoft-".Length ..]
        else
            str

    let readNamespace ctx (ns, entities: FSharpEntity seq) =
        let entities, nsdocs = readEntities ctx entities
        ApiDocNamespace(stripMicrosoft ns, entities, ctx.Substitutions, nsdocs)

    let readAssembly
        (
            assembly: FSharpAssembly,
            publicOnly,
            xmlFile: string,
            substitutions,
            sourceFolderRepo,
            urlRangeHighlight,
            mdcomments,
            urlMap,
            codeFormatCompilerArgs,
            warn
        ) =
        let assemblyName = AssemblyName(assembly.QualifiedName)

        // Read in the supplied XML file, map its name attributes to document text
        let doc = XDocument.Load(xmlFile)

        // don't use 'dict' to allow the dictionary to be mutated later on
        let xmlMemberMap = Dictionary()

        for key, value in
            [ for e in doc.Descendants(XName.Get "member") do
                  let attr = e.Attribute(XName.Get "name")

                  if (not (isNull attr)) && not (String.IsNullOrEmpty(attr.Value)) then
                      yield attr.Value, e ] do
            // NOTE: We completely ignore duplicate keys and I don't see
            // an easy way to detect where "value" is coming from, because the entries
            // are completely identical.
            // We just take the last here because it is the easiest to implement.
            // Additionally we log a warning just in case this is an issue in the future.
            // See https://github.com/fsprojects/FSharp.Formatting/issues/229
            // and https://github.com/fsprojects/FSharp.Formatting/issues/287
            if xmlMemberMap.ContainsKey key then
                Log.warnf "Duplicate documentation for '%s', one will be ignored!" key

            xmlMemberMap.[key] <- value

        // Code formatting agent & options used when processing inline code snippets in comments
        let asmPath = Path.GetDirectoryName(defaultArg assembly.FileName xmlFile)

        let ctx =
            ReadingContext.Create(
                publicOnly,
                assemblyName,
                xmlMemberMap,
                sourceFolderRepo,
                urlRangeHighlight,
                mdcomments,
                urlMap,
                asmPath,
                codeFormatCompilerArgs,
                substitutions,
                warn
            )

        //
        let namespaces =
            assembly.Contents.Entities
            |> Seq.filter (fun modul -> checkAccess ctx modul.Accessibility)
            |> Seq.groupBy (fun modul -> modul.AccessPath)
            |> Seq.sortBy fst
            |> Seq.map (readNamespace ctx)
            |> List.ofSeq

        assemblyName, namespaces
