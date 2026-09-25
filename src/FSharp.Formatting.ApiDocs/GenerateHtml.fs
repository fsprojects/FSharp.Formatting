module internal FSharp.Formatting.ApiDocs.GenerateHtml

open System
open System.Collections.Generic
open System.IO
open System.Web
open FSharp.Formatting.Common
open FSharp.Compiler.Symbols
open FSharp.Formatting.Templating
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html

let embed (x: ApiDocHtml) = !!x.HtmlText

let fsdocsSummary (x: ApiDocHtml) =
    // the <pre> tag is not allowed inside a <p> tag.
    if x.HtmlText.StartsWith("<pre>", StringComparison.Ordinal) then
        embed x
    else
        div [ Class "fsdocs-summary-contents" ] [ p [ Class "fsdocs-summary" ] [ embed x ] ]

/// How much two names have in common, character for character.
let sharedPrefixLength (a: string) (b: string) =
    let limit = min a.Length b.Length
    let mutable i = 0

    while i < limit && a.[i] = b.[i] do
        i <- i + 1

    i

let commonNamespacePrefix (names: string list) =
    match names with
    | []
    | [ _ ] -> ""
    | first :: rest ->
        // What the first name shares with whichever of the others agrees with it least.
        let shared =
            rest
            |> List.fold (fun shared name -> min shared (sharedPrefixLength first name)) first.Length

        // Back up to a dot: half of a segment is not a prefix worth hiding.
        let lastDot =
            if shared = 0 then
                -1
            else
                first.LastIndexOf('.', shared - 1)

        if lastDot < 0 then "" else first.Substring(0, lastDot + 1)

let menuNamespaceOf (documented: Set<string>) (name: string) =
    let rec outermost (name: string) (found: string option) =
        match name.LastIndexOf '.' with
        | -1 -> found
        | i ->
            let parent = name.Substring(0, i)
            // Walking outwards, so the last documented ancestor seen is the outermost one.
            let found = if documented.Contains parent then Some parent else found
            outermost parent found

    outermost name None |> Option.defaultValue name

type internal SectionCollector() =
    let sections = ResizeArray<int * string * string>()
    let entries = ResizeArray<int * string * string>()

    member _.Heading(level: int, id: string, title: string) =
        sections.Add(level, id, title)

        let heading =
            match level with
            | 2 -> h2
            | 3 -> h3
            | _ -> h4

        heading [ Id id; Custom("data-fsdocs-heading", string<int> sections.Count) ] [ !!title ]

    member _.Entry(href: string, text: string) =
        if sections.Count > 0 then
            entries.Add(sections.Count, href, text)

    member _.Menu =
        if sections.Count = 0 then
            PageContentList.EmptyContent
        else
            // The entries of a section go in a nested list. The scroll-driven highlight styles the
            // items of the outer list, which stay exactly the sections.
            let items =
                [
                    for index in 1 .. sections.Count do
                        let level, id, title = sections.[index - 1]

                        // The menu is narrow and cuts its entries off, so each carries its own text
                        // as a title for the reader to hover.
                        let ownEntries =
                            [
                                for (owner, href, text) in entries do
                                    if owner = index then
                                        li [] [ a [ Href href; HtmlProperties.Title text ] [ !!text ] ]
                            ]

                        li [ Class $"level-%i{level}"; Custom("data-fsdocs-heading", string<int> index) ] [
                            a [ Href($"#%s{id}"); HtmlProperties.Title title ] [ !!title ]

                            if not ownEntries.IsEmpty then
                                ul [ Class "fsdocs-menu-entries" ] ownEntries
                        ]
                ]

            string<HtmlElement>(ul [] items) + PageContentList.scrollSpyStyle sections.Count

type HtmlRender(model: ApiDocModel, ?menuTemplateFolder: string) =
    let root = model.Root
    let collectionName = model.Collection.CollectionName
    let qualify = model.Qualify

    // Grouping every namespace and entity by category is the same answer for the whole lifetime of
    // the renderer: the model it reads is a constructor argument and never changes. Rendering asks
    // for it once per page, so without this it ran once per page of the whole API reference.
    let categorised = lazy (Categorise.model model)

    let documentedNamespaces = lazy (categorised.Value |> List.map (fun (_, ns) -> ns.Name) |> Set.ofList)

    // The namespaces the menu lists. One nested in another documented namespace folds into it, so
    // a library such as Fantomas shows six entries instead of twenty. The API reference index is
    // still the list of every namespace.
    let menuNamespaces =
        lazy
            (categorised.Value
             |> List.filter (fun (_, ns) -> menuNamespaceOf documentedNamespaces.Value ns.Name = ns.Name))

    /// Whether the menu entry for a namespace is the one the reader is inside: the namespace itself,
    /// or one nested in it that the menu folded away.
    let isActiveMenuNamespace (nsOpt: ApiDocNamespace option) (ns: ApiDocNamespace) =
        match nsOpt with
        | None -> false
        | Some current -> menuNamespaceOf documentedNamespaces.Value current.Name = ns.Name

    /// The namespaces nested in this one, at any depth. The menu shows the outermost namespace only,
    /// so the page of that namespace is where the reader finds the ones it stands for.
    let nestedNamespaces (name: string) =
        categorised.Value
        |> List.filter (fun (_, ns: ApiDocNamespace) -> ns.Name.StartsWith(name + ".", StringComparison.Ordinal))

    let mutable uniqueNumber = 0

    let UniqueID () =
        uniqueNumber <- uniqueNumber + 1
        uniqueNumber

    let codeWithToolTip content tip =
        div [] [
            let id = UniqueID().ToString()

            code [ Custom("data-fsdocs-tip", id); Custom("data-fsdocs-tip-unique", id) ] content

            div [ Custom("popover", ""); Class "fsdocs-tip"; Id id ] tip
        ]

    let sourceLink url =
        [
            match url with
            | None -> ()
            | Some href ->
                a [ Href href; Class "fsdocs-source-link"; HtmlProperties.Title "Source on GitHub" ] [
                    iconifyIcon [ Icon "ri:github-fill"; Height "24"; Width "24" ]
                ]
        ]

    let removeParen (memberName: string) =
        let firstParen = memberName.IndexOf('(')

        if firstParen > 0 then
            memberName.Substring(0, firstParen)
        else
            memberName

    // Copy XML sig for use in `cref` XML
    let copyXmlSigIcon xmlDocSig =
        div [
            Class "fsdocs-source-link"
            HtmlProperties.Title "Copy signature (XML)"
            OnClick(sprintf "Clipboard_CopyTo('<see cref=\\\'%s\\\'/>')" xmlDocSig)
        ] [ iconifyIcon [ HtmlProperties.Icon "bi:filetype-xml"; Height "24"; Width "24" ] ]

    let copyXmlSigIconForSymbol (symbol: FSharpSymbol) =
        [
            match symbol with
            | :? FSharpMemberOrFunctionOrValue as v -> copyXmlSigIcon (removeParen v.XmlDocSig)
            | :? FSharpEntity as v -> copyXmlSigIcon (removeParen v.XmlDocSig)
            | _ -> ()
        ]

    // Copy XML sig for use in `cref` markdown
    let copyXmlSigIconMarkdown (xmlDocSig: string) =
        if xmlDocSig.StartsWith('`') || xmlDocSig.EndsWith('`') then
            div [] []
        else
            let delim =
                if xmlDocSig.Contains("``") then "```"
                elif xmlDocSig.Contains("`") then "``"
                else "`"

            div [
                Class "fsdocs-source-link"
                HtmlProperties.Title "Copy signature (Markdown)"
                OnClick(sprintf "Clipboard_CopyTo('%scref:%s%s')" delim xmlDocSig delim)
            ] [ iconifyIcon [ HtmlProperties.Icon "bi:filetype-md"; Height "24"; Width "24" ] ]

    let copyXmlSigIconForSymbolMarkdown (symbol: FSharpSymbol) =
        [
            match symbol with
            | :? FSharpMemberOrFunctionOrValue as v -> copyXmlSigIconMarkdown (removeParen v.XmlDocSig)
            | :? FSharpEntity as v -> copyXmlSigIconMarkdown (removeParen v.XmlDocSig)
            | _ -> ()
        ]

    let renderMembers (sections: SectionCollector) sectionId header tableHeader (members: ApiDocMember list) =
        [
            if members.Length > 0 then
                sections.Heading(3, sectionId, header)

                table [ Class "table outer-list fsdocs-member-list" ] [
                    thead [] [
                        tr [] [
                            td [ Class "fsdocs-member-list-header" ] [ !!tableHeader ]
                            td [ Class "fsdocs-member-list-header" ] [ !!"Description"; fsdocsDetailsToggle [] ]
                        ]
                    ]
                    tbody [] [
                        for m in members do
                            tr [] [
                                td [ Class "fsdocs-member-usage" ] [

                                    codeWithToolTip [
                                        // This adds #MemberName anchor. These may be ambiguous due to overloading
                                        p [] [ a [ Id m.Name ] [ a [ Href("#" + m.Name) ] [ embed m.UsageHtml ] ] ]
                                    ] [
                                        div [ Class "member-tooltip" ] [
                                            !!"Full Usage: "
                                            embed m.UsageHtml
                                            br []
                                            br []
                                            if not m.Parameters.IsEmpty then
                                                !!"Parameters: "

                                                ul [] [
                                                    for p in m.Parameters do
                                                        span [] [
                                                            b [] [ !!p.ParameterNameText ]
                                                            !!":"
                                                            embed p.ParameterType
                                                            match p.ParameterDocs with
                                                            | None -> ()
                                                            | Some d ->
                                                                !!" - "
                                                                embed d
                                                        ]

                                                        br []
                                                ]

                                                br []
                                            match m.ReturnInfo.ReturnType with
                                            | None -> ()
                                            | Some(_, rty) ->
                                                span [] [
                                                    !!(if m.Kind <> ApiDocMemberKind.RecordField then
                                                           "Returns: "
                                                       else
                                                           "Field type: ")
                                                    embed rty
                                                ]

                                                match m.ReturnInfo.ReturnDocs with
                                                | None -> ()
                                                | Some d -> embed d

                                                br []
                                            //!! "Signature: "
                                            //encode(m.SignatureTooltip)
                                            if not m.Modifiers.IsEmpty then
                                                !!"Modifiers: "
                                                encode (m.FormatModifiers)
                                                br []

                                            // We suppress the display of ill-formatted type parameters for places
                                            // where these have not been explicitly declared
                                            match m.FormatTypeArguments with
                                            | None -> ()
                                            | Some v ->
                                                !!"Type parameters: "

                                                if m.TypeConstraintDisplayMode = TypeConstraintDisplayMode.Short then
                                                    match m.FormatShortTypeConstraints with
                                                    | None -> encode v
                                                    | Some c -> encode ($"%s{v} (requires %s{c})")
                                                else
                                                    encode v

                                                br []

                                            if m.TypeConstraintDisplayMode = TypeConstraintDisplayMode.Full then
                                                match m.FormatTypeConstraints with
                                                | None -> ()
                                                | Some v ->
                                                    !!"Constraints: "
                                                    encode (v)
                                        ]
                                    ]
                                ]

                                let smry =
                                    div [ Class "fsdocs-summary" ] [
                                        fsdocsSummary m.Comment.Summary
                                        div [ Class "icon-button-row" ] [
                                            yield! sourceLink m.SourceLocation
                                            yield! copyXmlSigIconForSymbol m.Symbol
                                            yield! copyXmlSigIconForSymbolMarkdown m.Symbol
                                        ]
                                    ]

                                let dtls =
                                    [
                                        match m.Comment.Remarks with
                                        | Some r -> p [ Class "fsdocs-remarks" ] [ embed r ]
                                        | None -> ()

                                        match m.ExtendedType with
                                        | Some(_, extendedTypeHtml) ->
                                            p [] [ !!"Extended Type: "; embed extendedTypeHtml ]
                                        | _ -> ()

                                        if not m.Parameters.IsEmpty then
                                            dl [ Class "fsdocs-params" ] [
                                                for parameter in m.Parameters do
                                                    dt [ Class "fsdocs-param" ] [
                                                        span [ Class "fsdocs-param-name" ] [
                                                            !!parameter.ParameterNameText
                                                        ]
                                                        !!":"
                                                        embed parameter.ParameterType
                                                    ]

                                                    dd [ Class "fsdocs-param-docs" ] [
                                                        match parameter.ParameterDocs with
                                                        | None -> ()
                                                        | Some d -> p [] [ embed d ]
                                                    ]
                                            ]

                                        match m.ReturnInfo.ReturnType with
                                        | None -> ()
                                        | Some(_, returnTypeHtml) ->
                                            dl [ Class "fsdocs-returns" ] [
                                                dt [] [
                                                    span [ Class "fsdocs-return-name" ] [
                                                        !!(if m.Kind <> ApiDocMemberKind.RecordField then
                                                               "Returns: "
                                                           else
                                                               "Field type: ")
                                                    ]
                                                    embed returnTypeHtml
                                                ]
                                                dd [ Class "fsdocs-return-docs" ] [
                                                    match m.ReturnInfo.ReturnDocs with
                                                    | None -> ()
                                                    | Some r -> p [] [ embed r ]
                                                ]
                                            ]

                                        if not m.Comment.Exceptions.IsEmpty then
                                            //p [] [ !! "Exceptions:" ]
                                            table [ Class "fsdocs-exception-list" ] [
                                                for (nm, link, html) in m.Comment.Exceptions do
                                                    tr [] [
                                                        td
                                                            []
                                                            (match link with
                                                             | None -> []
                                                             | Some href -> [ a [ Href href ] [ !!nm ] ])
                                                        td [] [ embed html ]
                                                    ]
                                            ]

                                        for e in m.Comment.Notes do
                                            h5 [ Class "fsdocs-note-header" ] [ !!"Note" ]

                                            p [ Class "fsdocs-note" ] [ embed e ]

                                        if not m.Comment.SeeAlso.IsEmpty then
                                            h5 [ Class "fsdocs-seealso-header" ] [ !!"See also" ]

                                            ul [ Class "fsdocs-seealso-list" ] [
                                                for (nm, link, html) in m.Comment.SeeAlso do
                                                    li [] [
                                                        match link with
                                                        | Some href -> a [ Href href ] [ !!nm ]
                                                        | None -> embed html
                                                    ]
                                            ]

                                        for e in m.Comment.Examples do
                                            h5 [ Class "fsdocs-example-header" ] [ !!"Example" ]

                                            p [
                                                yield Class "fsdocs-example"
                                                match e.Id with
                                                | None -> ()
                                                | Some id -> yield Id id
                                            ] [ embed e ]
                                    //if m.IsObsolete then
                                    //    obsoleteMessage m.ObsoleteMessage

                                    //if not (String.IsNullOrEmpty(m.Details.FormatCompiledName)) then
                                    //    p [] [!!"CompiledName: "; code [] [!!m.Details.FormatCompiledName]]
                                    ]

                                td [ Class "fsdocs-member-xmldoc" ] [
                                    if List.isEmpty dtls then
                                        smry
                                    elif String.IsNullOrWhiteSpace(m.Comment.Summary.HtmlText) then
                                        div [ Class "fsdocs-member-xmldoc-column" ] [
                                            div [ Class "icon-button-row" ] (sourceLink m.SourceLocation)
                                            yield! dtls
                                        ]
                                    else
                                        details [] ((summary [] [ smry ]) :: dtls)
                                ]
                            ]
                    ]
                ]
        ]

    let renderEntities (sections: SectionCollector) (entities: ApiDocEntity list) =
        [
            if entities.Length > 0 then
                let hasTypes = entities |> List.exists (fun e -> e.IsTypeDefinition)

                let hasModules = entities |> List.exists (fun e -> not e.IsTypeDefinition)

                table [ Class "table outer-list fsdocs-entity-list" ] [
                    thead [] [
                        tr [] [
                            td [] [
                                !!(if hasTypes && hasModules then "Type/Module"
                                   elif hasTypes then "Type"
                                   else "Modules")
                            ]
                            td [] [ !!"Description" ]
                        ]
                    ]
                    tbody [] [
                        let nameCounts = entities |> List.countBy (fun e -> e.Name) |> dict

                        for e in entities do
                            let nm = e.Name

                            let multi = nameCounts.[nm] > 1

                            let nmWithSiffix =
                                if multi then
                                    (if e.IsTypeDefinition then
                                         nm + " (Type)"
                                     else
                                         nm + " (Module)")
                                else
                                    nm

                            sections.Entry(
                                e.Url(root, collectionName, qualify, model.FileExtensions.InUrl),
                                nmWithSiffix
                            )

                            tr [] [
                                td [ Class "fsdocs-entity-name" ] [

                                    // This adds #EntityName anchor. These may currently be ambiguous
                                    p [] [
                                        a [ Name nm ] [
                                            // data-fsdocs-nav makes the j / k hotkeys stop here, so they walk the
                                            // types and modules of the namespace after the headings above them.
                                            a [
                                                Href(e.Url(root, collectionName, qualify, model.FileExtensions.InUrl))
                                                Custom("data-fsdocs-nav", "")
                                            ] [ !!nmWithSiffix ]
                                        ]
                                    ]
                                ]
                                td [ Class "fsdocs-entity-xmldoc" ] [
                                    div [] [
                                        fsdocsSummary e.Comment.Summary
                                        div [ Class "icon-button-row" ] [
                                            yield! sourceLink e.SourceLocation
                                            yield! copyXmlSigIconForSymbol e.Symbol
                                            yield! copyXmlSigIconForSymbolMarkdown e.Symbol
                                        ]
                                    ]
                                ]
                            ]
                    ]
                ]
        ]

    let entityContent (sections: SectionCollector) (info: ApiDocEntityInfo) =
        // Get all the members & comment for the type
        let entity = info.Entity

        let members = entity.AllMembers |> List.filter (fun e -> not e.IsObsolete)

        let byCategory = members |> Categorise.getMembersByCategory

        let usageName =
            match info.ParentModule with
            | Some m when m.RequiresQualifiedAccess -> m.Name + "." + entity.Name
            | _ -> entity.Name

        [
            sections.Heading(
                2,
                entity.UrlBaseName,
                usageName + (if entity.IsTypeDefinition then " Type" else " Module")
            )

            dl [ Class "fsdocs-metadata" ] [
                dt [] [
                    !!"Namespace: "
                    a [ Href(info.Namespace.Url(root, collectionName, qualify, model.FileExtensions.InUrl)) ] [
                        !!info.Namespace.Name
                    ]
                ]
                dt [] [ !!("Assembly: " + entity.Assembly.Name + ".dll") ]

                match info.ParentModule with
                | None -> ()
                | Some parentModule ->
                    dt [] [
                        !!"Parent Module: "
                        a [ Href(parentModule.Url(root, collectionName, qualify, model.FileExtensions.InUrl)) ] [
                            !!parentModule.Name
                        ]
                    ]

                match entity.AbbreviatedType with
                | Some(_, abbreviatedTypHtml) -> dt [] [ !!"Abbreviation For: "; embed abbreviatedTypHtml ]

                | None -> ()

                match entity.BaseType with
                | Some(_, baseTypeHtml) -> dt [] [ !!"Base Type: "; embed baseTypeHtml ]
                | None -> ()

                match entity.AllInterfaces with
                | [] -> ()
                | l ->
                    dt [] [
                        !!("All Interfaces: ")
                        for (i, (_, ityHtml)) in Seq.indexed l do
                            if i <> 0 then
                                !!", "

                            embed ityHtml
                    ]

                if entity.Symbol.IsValueType then
                    dt [] [ !!("Kind: Struct") ]

                match entity.DelegateSignature with
                | Some(_, delegateSigHtml) -> dt [] [ !!("Delegate Signature: "); embed delegateSigHtml ]
                | None -> ()

                if entity.Symbol.IsProvided then
                    dt [] [ !!("This is a provided type definition") ]

                if entity.Symbol.IsAttributeType then
                    dt [] [ !!("This is an attribute type definition") ]

                if entity.Symbol.IsEnum then
                    dt [] [ !!("This is an enum type definition") ]

            //if info.Entity.IsObsolete then
            //    obsoleteMessage entity.ObsoleteMessage
            ]
            // Show the summary (and sectioned docs without any members)
            div [ Class "fsdocs-xmldoc" ] [
                div [] [
                    //yield! copyXmlSigIconForSymbol entity.Symbol
                    //yield! sourceLink entity.SourceLocation
                    fsdocsSummary entity.Comment.Summary
                ]
                // Show the remarks etc.
                match entity.Comment.Remarks with
                | Some r -> p [ Class "fsdocs-remarks" ] [ embed r ]
                | None -> ()
                for note in entity.Comment.Notes do
                    h5 [ Class "fsdocs-note-header" ] [ !!"Note" ]

                    p [ Class "fsdocs-note" ] [ embed note ]

                if not entity.Comment.SeeAlso.IsEmpty then
                    h5 [ Class "fsdocs-seealso-header" ] [ !!"See also" ]

                    ul [ Class "fsdocs-seealso-list" ] [
                        for (nm, link, html) in entity.Comment.SeeAlso do
                            li [] [
                                match link with
                                | Some href -> a [ Href href ] [ !!nm ]
                                | None -> embed html
                            ]
                    ]

                for example in entity.Comment.Examples do
                    h5 [ Class "fsdocs-example-header" ] [ !!"Example" ]

                    p [ Class "fsdocs-example" ] [ embed example ]

            ]

            //<!-- Render nested types and modules, if there are any -->

            let nestedEntities = entity.NestedEntities |> List.filter (fun e -> not e.IsObsolete)

            if (nestedEntities.Length > 0) then
                div [] [
                    sections.Heading(
                        3,
                        "nested-entities",
                        if nestedEntities |> List.forall (fun e -> not e.IsTypeDefinition) then
                            "Nested modules"
                        elif nestedEntities |> List.forall (fun e -> e.IsTypeDefinition) then
                            "Types"
                        else
                            "Types and nested modules"
                    )

                    yield! renderEntities sections nestedEntities
                ]

            for (index, ms, name) in byCategory do
                // Iterate over all the categories and print members. If there are more than one
                // categories, print the category heading (as <h2>) and add XML comment from the type
                // that is related to this specific category.
                if (byCategory.Length > 1) then
                    sections.Heading(2, sprintf "section%d" index, name)

                // A page can repeat a kind of member once per category, so the anchor of a section
                // carries its category index.
                let sectionId (name: string) =
                    if byCategory.Length > 1 then
                        sprintf "section%d-%s" index name
                    else
                        name

                let functionsOrValues = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ValueOrFunction)
                let extensions = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.TypeExtension)
                let activePatterns = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.ActivePattern)
                let unionCases = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.UnionCase)
                let recordFields = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.RecordField)
                let staticParameters = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticParameter)
                let constructors = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.Constructor)
                let instanceMembers = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.InstanceMember)
                let staticMembers = ms |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticMember)

                div
                    []
                    (renderMembers
                        sections
                        (sectionId "functions-and-values")
                        "Functions and values"
                        "Function or value"
                        functionsOrValues)

                div
                    []
                    (renderMembers sections (sectionId "type-extensions") "Type extensions" "Type extension" extensions)

                div
                    []
                    (renderMembers
                        sections
                        (sectionId "active-patterns")
                        "Active patterns"
                        "Active pattern"
                        activePatterns)

                div [] (renderMembers sections (sectionId "union-cases") "Union cases" "Union case" unionCases)
                div [] (renderMembers sections (sectionId "record-fields") "Record fields" "Record Field" recordFields)

                div
                    []
                    (renderMembers
                        sections
                        (sectionId "static-parameters")
                        "Static parameters"
                        "Static parameters"
                        staticParameters)

                div [] (renderMembers sections (sectionId "constructors") "Constructors" "Constructor" constructors)

                div
                    []
                    (renderMembers
                        sections
                        (sectionId "instance-members")
                        "Instance members"
                        "Instance member"
                        instanceMembers)

                div
                    []
                    (renderMembers sections (sectionId "static-members") "Static members" "Static member" staticMembers)

            let inheritedMemberGroups =
                entity.InheritedMembers
                |> List.choose (fun (baseTypeHtml, members) ->
                    let instMembers =
                        members
                        |> List.filter (fun m -> m.Kind = ApiDocMemberKind.InstanceMember && not m.IsObsolete)

                    let statMembers =
                        members
                        |> List.filter (fun m -> m.Kind = ApiDocMemberKind.StaticMember && not m.IsObsolete)

                    if not (List.isEmpty instMembers) || not (List.isEmpty statMembers) then
                        Some(baseTypeHtml, instMembers, statMembers)
                    else
                        None
                )

            if not (List.isEmpty inheritedMemberGroups) then
                sections.Heading(3, "inherited-members", "Inherited members")

                for (i, (baseTypeHtml, instMembers, statMembers)) in List.indexed inheritedMemberGroups do
                    // The base type is HTML (it carries links), so the heading is written by hand and
                    // the menu gets the plain name.
                    h4 [ Id(sprintf "inherited-%d" i) ] [ !!"Inherited from "; embed baseTypeHtml ]

                    div
                        []
                        (renderMembers
                            sections
                            (sprintf "inherited-%d-instance-members" i)
                            "Instance members"
                            "Instance member"
                            instMembers)

                    div
                        []
                        (renderMembers
                            sections
                            (sprintf "inherited-%d-static-members" i)
                            "Static members"
                            "Static member"
                            statMembers)
        ]

    /// One row of a table of namespaces: the link and the summary.
    let namespaceRow (ns: ApiDocNamespace) =
        tr [] [
            td [] [
                // data-fsdocs-nav makes the j / k hotkeys stop here, so they walk the namespaces
                // after the headings above them.
                a [
                    Href(ns.Url(root, collectionName, qualify, model.FileExtensions.InUrl))
                    HtmlProperties.Title ns.Name
                    Custom("data-fsdocs-nav", "")
                ] [ !!ns.Name ]
            ]
            td [] [
                match ns.NamespaceDocs with
                | Some nsdocs -> embed nsdocs.Summary
                | None -> ()
            ]
        ]

    let namespaceContent (sections: SectionCollector) (nsIndex, ns: ApiDocNamespace) =
        let allByCategory = Categorise.entities (nsIndex, ns, false)
        let nested = nestedNamespaces ns.Name

        [
            if allByCategory.Length > 0 || not nested.IsEmpty then
                sections.Heading(2, ns.UrlHash, ns.Name + " Namespace")

                div [ Class "fsdocs-xmldoc" ] [
                    match ns.NamespaceDocs with
                    | Some nsdocs ->
                        p [] [ embed nsdocs.Summary ]

                        match nsdocs.Remarks with
                        | Some r -> p [] [ embed r ]
                        | None -> ()

                    | None -> ()
                ]

                // The menu lists this namespace and not the ones inside it, so they are listed here
                // instead: without this the page is a dead end and the index is the only way on.
                if not nested.IsEmpty then
                    sections.Heading(3, "namespaces", "Namespaces")

                    table [ Class "table outer-list fsdocs-entity-list" ] [
                        thead [] [ tr [] [ td [] [ !!"Namespace" ]; td [] [ !!"Description" ] ] ]
                        tbody [] [
                            for _allByCategory, nestedNs in nested do
                                sections.Entry(
                                    nestedNs.Url(root, collectionName, qualify, model.FileExtensions.InUrl),
                                    nestedNs.Name
                                )

                                namespaceRow nestedNs
                        ]
                    ]

                for category in allByCategory do
                    if (allByCategory.Length > 1) then
                        sections.Heading(3, "category-" + category.CategoryIndex, category.CategoryName)

                    yield! renderEntities sections category.CategoryEntites
        ]

    let tableOfNamespacesAux () =
        [ for _allByCategory, ns in categorised.Value -> namespaceRow ns ]

    let listOfNamespacesNavAux (root: string) otherDocs (nsOpt: ApiDocNamespace option) =
        [
            let categorise = menuNamespaces.Value

            let someExist = categorise.Length > 0

            if someExist then
                // The header is the way back to the index of all namespaces, which no page
                // links to otherwise. It is the current page on an API page showing no namespace;
                // on a content page the reader is somewhere else entirely.
                li [
                    Class(
                        "nav-header"
                        + match nsOpt with
                          | None when not otherDocs -> " active"
                          | _ -> ""
                    )
                ] [
                    a [ Href(model.IndexFileUrl(root, collectionName, qualify, model.FileExtensions.InUrl)) ] [
                        !!"API Reference"
                    ]
                ]

            let prefix = commonNamespacePrefix [ for _, ns in categorise -> ns.Name ]

            for _allByCategory, ns in categorise do

                // Generate the entry for the namespace
                li [
                    Class(
                        "nav-item"
                        +
                        // add the 'active' class if this is the namespace of the thing being shown
                        if isActiveMenuNamespace nsOpt ns then " active" else ""
                    )
                ] [
                    span [] [
                        a [
                            Class(
                                "nav-link"
                                +
                                // add the 'active' class if this is the namespace of the thing being shown
                                if isActiveMenuNamespace nsOpt ns then " active" else ""
                            )
                            Href(ns.Url(root, collectionName, qualify, model.FileExtensions.InUrl))
                            // The entry drops the prefix it shares with its neighbours, so the
                            // full name is a hover away.
                            if prefix <> "" then
                                HtmlProperties.Title ns.Name
                        ] [ !!(ns.Name.Substring(prefix.Length)) ]

                    ]
                ]
        ]

    let listOfNamespacesNavWithRoot (root: string) otherDocs (nsOpt: ApiDocNamespace option) =
        let noTemplatingFallback () =
            listOfNamespacesNavAux root otherDocs nsOpt
            |> List.map (fun html -> html.ToString())
            |> String.concat "             \n"

        // What the page itself is rendered with, but with the root of this page, so a menu template
        // can use {{root}} and the other site-wide substitutions to reach the rest of the site.
        let menuSubstitutions = [ yield! model.Substitutions; yield ParamKeys.root, root ]

        match menuTemplateFolder with
        | None -> noTemplatingFallback ()
        | Some menuTemplateFolder ->
            let isTemplatingAvailable = Menu.isTemplatingAvailable menuTemplateFolder

            if not isTemplatingAvailable then
                noTemplatingFallback ()
            else
                let categorise = menuNamespaces.Value

                if categorise.Length = 0 then
                    ""
                else
                    let prefix = commonNamespacePrefix [ for _, ns in categorise -> ns.Name ]

                    let menuItems =
                        [
                            for _, ns in categorise do
                                {
                                    Menu.MenuItem.Link =
                                        ns.Url(root, collectionName, qualify, model.FileExtensions.InUrl)
                                    Menu.MenuItem.Content = ns.Name.Substring(prefix.Length)
                                    Menu.MenuItem.Title = (if String.IsNullOrEmpty prefix then None else Some ns.Name)
                                    Menu.MenuItem.IsActive = isActiveMenuNamespace nsOpt ns
                                }
                        ]

                    // A template can fold a section away, and the reader arriving on an API page is
                    // inside this one. Mark the category active so the section it renders is the one
                    // that opens. The other docs render the same list while the reader is elsewhere.
                    Menu.createMenu menuTemplateFolder menuSubstitutions (not otherDocs) "API Reference" menuItems

    let listOfNamespacesNav otherDocs (nsOpt: ApiDocNamespace option) =
        listOfNamespacesNavWithRoot root otherDocs nsOpt

    member _.GlobalSubstitutionsFor(root: string) : Substitutions =
        let toc = listOfNamespacesNavWithRoot root true None

        [ yield (ParamKeys.``fsdocs-list-of-namespaces``, toc); yield ParamKeys.``fsdocs-body-class``, "api-docs" ]

    member x.GlobalSubstitutions: Substitutions = x.GlobalSubstitutionsFor root

    member _.Pages(collectionName: string) : (string * (string option -> Substitutions -> string)) list =

        let getSubstitutons parameters toc (content: HtmlElement) pageContentList pageTitle globalParameters =
            [|
                yield! parameters
                yield (ParamKeys.``fsdocs-content``, content.ToString())
                yield (ParamKeys.``fsdocs-source``, String.Empty)
                yield (ParamKeys.``fsdocs-tooltips``, String.Empty)
                yield (ParamKeys.``fsdocs-page-title``, pageTitle)
                yield (ParamKeys.``fsdocs-page-content-list``, pageContentList)
                yield (ParamKeys.``fsdocs-meta-tags``, String.Empty)
                yield! globalParameters
                // Last one wins (the substitutions become a dictionary), so the namespace menu of the
                // page goes after the global substitutions: those carry the same list with nothing
                // marked active, which would otherwise take the place of the one marking this page.
                yield (ParamKeys.``fsdocs-list-of-namespaces``, toc)
            |]

        let page outFile parameters toc (content: SectionCollector -> HtmlElement) pageTitle =
            let render (templateOpt: string option) (globalParameters: Substitutions) =
                // The content has to be rendered before the menu: it is what fills the collector.
                let sections = SectionCollector()
                let element = content sections

                let substitutions = getSubstitutons parameters toc element sections.Menu pageTitle globalParameters

                SimpleTemplating.RenderWithFileTemplate(substitutions, templateOpt)

            outFile, render

        let collection = model.Collection

        [
            // The index page is one table of namespaces, so it registers no sections and keeps the
            // wide two column layout: a menu mirroring that table one for one would say nothing new.
            (let content (_: SectionCollector) =
                div [] [
                    h1 [] [ !!"API Reference" ]
                    h2 [] [ !!"Available Namespaces:" ]
                    table [ Class "table outer-list fsdocs-member-list" ] [
                        thead [] [
                            tr [] [
                                td [ Class "fsdocs-member-list-header" ] [ !!"Namespace" ]
                                td [ Class "fsdocs-member-list-header" ] [ !!"Description" ]
                            ]
                        ]
                        tbody [] (tableOfNamespacesAux ())
                    ]
                ]

             let pageTitle = sprintf "%s (API Reference)" collectionName

             let toc = listOfNamespacesNav false None

             let outFile = model.IndexOutputFile(collectionName, model.Qualify, model.FileExtensions.InFile)

             page outFile model.Substitutions toc content pageTitle)

            //printfn "Namespaces = %A" [ for ns in collection.Namespaces -> ns.Name ]

            for (nsIndex, ns) in Seq.indexed collection.Namespaces do
                let content sections =
                    div [] (namespaceContent sections (nsIndex, ns))

                let pageTitle = ns.Name
                let toc = listOfNamespacesNav false (Some ns)

                let outFile = ns.OutputFile(collectionName, model.Qualify, model.FileExtensions.InFile)

                page outFile model.Substitutions toc content pageTitle

            for info in model.EntityInfos do
                let content sections = div [] (entityContent sections info)

                let pageTitle = sprintf "%s (%s)" info.Entity.Name collectionName

                let toc = listOfNamespacesNav false (Some info.Namespace)

                let outFile = info.Entity.OutputFile(collectionName, model.Qualify, model.FileExtensions.InFile)

                page outFile info.Entity.Substitutions toc content pageTitle
        ]

    member x.Generate(outDir: string, templateOpt, collectionName, globalParameters) =
        for (relativeFile, render) in x.Pages(collectionName) do
            let outFile = Path.Combine(outDir, relativeFile)
            let outputText = render templateOpt globalParameters
            logger.Debugf "  Generating %s" outFile
            SimpleTemplating.WriteOutputFile(outFile, outputText)
