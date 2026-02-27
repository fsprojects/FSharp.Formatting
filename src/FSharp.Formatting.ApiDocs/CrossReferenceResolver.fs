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

/// Internal helpers for computing XML doc signature strings (e.g. "T:MyNs.MyType")
/// that are used as stable identifiers for cross-reference lookup.
[<AutoOpen>]
module internal CrossReferences =
    /// Returns the XML doc signature for a type, falling back to a "T:FullName" construction.
    let getXmlDocSigForType (typ: FSharpEntity) =
        if not (String.IsNullOrWhiteSpace typ.XmlDocSig) then
            typ.XmlDocSig
        else
            try
                defaultArg (Option.map (sprintf "T:%s") typ.TryFullName) ""
            with _ ->
                ""

    /// Returns the XML doc sig prefix letter for a member: "E" for events, "P" for properties, "M" otherwise.
    let getMemberXmlDocsSigPrefix (memb: FSharpMemberOrFunctionOrValue) =
        if memb.IsEvent then "E"
        elif memb.IsProperty then "P"
        else "M"

    /// Returns the XML doc signature for a member, constructing it from the member's type and parameter list
    /// when <see cref="P:FSharp.Compiler.Symbols.FSharpMemberOrFunctionOrValue.XmlDocSig"/> is empty.
    let getXmlDocSigForMember (memb: FSharpMemberOrFunctionOrValue) =
        if not (String.IsNullOrWhiteSpace memb.XmlDocSig) then
            memb.XmlDocSig
        else
            let memberName =
                try
                    let name = memb.CompiledName.Replace(".ctor", "#ctor")

                    let typeGenericParameters =
                        match memb.DeclaringEntity with
                        | None -> Seq.empty
                        | Some declaringEntity ->
                            declaringEntity.GenericParameters
                            |> Seq.mapi (fun num par -> par.Name, sprintf "`%d" num)

                    let methodGenericParameters =
                        memb.GenericParameters |> Seq.mapi (fun num par -> par.Name, sprintf "``%d" num)

                    let typeArgsMap =
                        Seq.append methodGenericParameters typeGenericParameters
                        |> Seq.groupBy fst
                        |> Seq.map (fun (_name, grp) -> grp |> Seq.head)
                        |> dict

                    let typeArgs =
                        if memb.GenericParameters.Count > 0 then
                            sprintf "``%d" memb.GenericParameters.Count
                        else
                            ""

                    let rec formatTypeForXmlDocSig (typ: FSharpType) =
                        if typ.IsGenericParameter then
                            typeArgsMap.[typ.GenericParameter.Name]
                        elif typ.HasTypeDefinition && typ.TypeDefinition.IsArrayType then
                            let elementTypeName = formatTypeForXmlDocSig typ.GenericArguments.[0]
                            let rank = typ.TypeDefinition.ArrayRank

                            if rank = 1 then
                                elementTypeName + "[]"
                            else
                                let dims = String.concat "," (Array.create rank "0:")
                                elementTypeName + "[" + dims + "]"
                        elif typ.HasTypeDefinition then
                            let baseName =
                                match typ.TypeDefinition.TryFullName with
                                | Some fullName -> fullName
                                | None -> typ.TypeDefinition.CompiledName

                            if typ.GenericArguments.Count > 0 then
                                let args = typ.GenericArguments |> Seq.map formatTypeForXmlDocSig |> String.concat ","

                                baseName + "{" + args + "}"
                            else
                                baseName
                        else
                            typ.Format(FSharpDisplayContext.Empty)

                    let paramList =
                        if
                            memb.CurriedParameterGroups.Count > 0
                            && memb.CurriedParameterGroups.[0].Count > 0
                        then
                            let head = memb.CurriedParameterGroups.[0]

                            let paramTypeList = head |> Seq.map (fun param -> formatTypeForXmlDocSig param.Type)

                            "(" + System.String.Join(", ", paramTypeList) + ")"
                        else
                            ""

                    sprintf "%s%s%s" name typeArgs paramList
                with exn ->
                    printfn "Error while building fsdocs-member-name for %s because: %s" memb.FullName exn.Message
                    Log.verbf "Full Exception details of previous message: %O" exn
                    memb.CompiledName

            match
                memb.DeclaringEntity
                |> Option.bind (fun declaringEntity -> declaringEntity.TryFullName)
            with
            | None -> ""
            | Some fullName -> sprintf "%s:%s.%s" (getMemberXmlDocsSigPrefix memb) fullName memberName

/// A resolved cross-reference containing the target URL and display name.
type internal CrefReference =
    { IsInternal: bool
      ReferenceLink: string
      NiceName: string }

/// Resolves XML doc <c>cref</c> references to documentation URLs for both internal
/// (this collection) and external (.NET / FSharp.Core) symbols. Call
/// <see cref="M:FSharp.Formatting.ApiDocs.CrossReferenceResolver.RegisterEntity"/> for
/// every entity in the collection before resolving any references.
type internal CrossReferenceResolver(root, collectionName, qualify, extensions) =
    let toReplace =
        ([ ("Microsoft.", ""); (".", "-"); ("`", "-"); ("<", "_"); (">", "_"); (" ", "_"); ("#", "_") ]
         @ (Path.GetInvalidPathChars()
            |> Seq.append (Path.GetInvalidFileNameChars())
            |> Seq.map (fun inv -> (inv.ToString(), "_"))
            |> Seq.toList))
        |> Seq.distinctBy fst
        |> Seq.toList

    let usedNames = Dictionary<_, _>()
    let registeredSymbolsToUrlBaseName = Dictionary<FSharpSymbol, string>()
    let xmlDocNameToSymbol = Dictionary<string, FSharpSymbol>()
    let niceNameEntityLookup = Dictionary<_, _>()
    let extensions = extensions

    let nameGen (name: string) =
        let nice =
            (toReplace
             |> List.fold (fun (s: string) (inv, repl) -> s.Replace(inv, repl)) name)
                .ToLower()

        let found =
            seq {
                yield nice

                for i in Seq.initInfinite id do
                    yield sprintf "%s-%d" nice i
            }
            |> Seq.find (usedNames.ContainsKey >> not)

        usedNames.Add(found, true)
        found

    let registerMember (memb: FSharpMemberOrFunctionOrValue) =
        let xmlsig = getXmlDocSigForMember memb

        if (not (System.String.IsNullOrEmpty xmlsig)) then
            assert
                (xmlsig.StartsWith("M:", StringComparison.Ordinal)
                 || xmlsig.StartsWith("P:", StringComparison.Ordinal)
                 || xmlsig.StartsWith("F:", StringComparison.Ordinal)
                 || xmlsig.StartsWith("E:", StringComparison.Ordinal))

            xmlDocNameToSymbol.[xmlsig] <- memb

    let rec registerEntity (entity: FSharpEntity) =
        let newName = nameGen (sprintf "%s.%s" entity.AccessPath entity.CompiledName)

        registeredSymbolsToUrlBaseName.[entity] <- newName
        let xmlsig = getXmlDocSigForType entity

        if (not (System.String.IsNullOrEmpty xmlsig)) then
            assert (xmlsig.StartsWith("T:", StringComparison.Ordinal))
            xmlDocNameToSymbol.[xmlsig] <- entity

            if (not (niceNameEntityLookup.ContainsKey(entity.LogicalName))) then
                niceNameEntityLookup.[entity.LogicalName] <- System.Collections.Generic.List<_>()

            niceNameEntityLookup.[entity.LogicalName].Add(entity)

        for nested in entity.NestedEntities do
            registerEntity nested

        for memb in entity.TryGetMembersFunctionsAndValues() do
            registerMember memb

    let getUrlBaseNameForRegisteredEntity (entity: FSharpEntity) =
        match registeredSymbolsToUrlBaseName.TryGetValue(entity) with
        | true, v -> v
        | _ ->
            failwithf "The entity %s was not registered before!" (sprintf "%s.%s" entity.AccessPath entity.CompiledName)

    let removeParen (memberName: string) =
        let firstParen = memberName.IndexOf('(')

        if firstParen > 0 then
            memberName.Substring(0, firstParen)
        else
            memberName

    // Strip generic parameters from a type name: "Map<K,V>" -> "Map", "List`1" -> "List"
    let stripGenericSuffix (name: string) =
        let angleIdx = name.IndexOf('<')
        let backtickIdx = name.IndexOf('`')

        let cutAt =
            match angleIdx, backtickIdx with
            | -1, -1 -> name.Length
            | -1, b -> b
            | a, -1 -> a
            | a, b -> min a b

        name.Substring(0, cutAt)

    let tryGetTypeFromMemberName (memberName: string) =
        let sub = removeParen memberName
        let lastPeriod = sub.LastIndexOf('.')

        if lastPeriod > 0 then
            Some(memberName.Substring(0, lastPeriod))
        else
            None

    let tryGetShortMemberNameFromMemberName (memberName: string) =
        let sub = removeParen memberName
        let lastPeriod = sub.LastIndexOf('.')

        if lastPeriod > 0 then
            Some(memberName.Substring(lastPeriod + 1))
        else
            None

    let getMemberName keepParts hasModuleSuffix (memberNameNoParen: string) =
        let splits = memberNameNoParen.Split('.') |> Array.toList

        let noNamespaceParts =
            if splits.Length > keepParts then
                splits.[splits.Length - keepParts ..]
            else
                splits

        let noNamespaceParts =
            if hasModuleSuffix then
                match noNamespaceParts with
                | h :: t when h.EndsWith("Module", StringComparison.Ordinal) -> h.[0 .. h.Length - 7] :: t
                | s -> s
            else
                noNamespaceParts

        let res = String.concat "." noNamespaceParts

        let noGenerics =
            match res.Split([| '`' |], StringSplitOptions.RemoveEmptyEntries) with
            | [||] -> ""
            | [| s |] -> s
            | arr -> String.Join("`", arr.[0 .. arr.Length - 2])

        noGenerics

    let externalDocsLink isMember simple (typeName: string) (fullName: string) =
        if
            fullName.StartsWith("FSharp.", StringComparison.Ordinal)
            || fullName.StartsWith("Microsoft.FSharp.", StringComparison.Ordinal)
        then
            let noParen = removeParen typeName

            let docs =
                noParen
                    .Replace("Microsoft.FSharp.", "FSharp.")
                    .Replace("``", "-")
                    .Replace("`", "-")
                    .Replace(".", "-")
                    .ToLower()

            let link = sprintf "https://fsharp.github.io/fsharp-core-docs/reference/%s" docs

            let link =
                if isMember then
                    link + "#" + (getMemberName 1 false fullName)
                else
                    link

            let niceName =
                match simple with
                | "FSharpAsync" -> "Async"
                | "FSharpAsyncBuilder" -> "AsyncBuilder"
                | "FSharpEvent" -> "Event"
                | "FSharpDelegateEvent" -> "DelegateEvent"
                | "FSharpAsyncReplyChannel" -> "AsyncReplyChannel"
                | "FSharpMailboxProcessor" -> "MailboxProcessor"
                | "FSharpMap" -> "Map"
                | "FSharpChoice" -> "Choice"
                | "FSharpRef" -> "ref"
                | "FSharpList" -> "list"
                | "FSharpOption" -> "option"
                | "FSharpValueOption" -> "voption"
                | "FSharpHandler" -> "Handler"
                | "FSharpVar" -> "Var"
                | "FSharpExpr" -> "Expr"
                | "FSharpSet" -> "Set"
                | "StringModule" -> "String"
                | "OptionModule" -> "Option"
                | "SeqModule" -> "Seq"
                | "ArrayModule" -> "Array"
                | "ListModule" -> "List"
                | _ -> simple

            { IsInternal = false
              ReferenceLink = link
              NiceName = niceName }
        else
            let noParen = removeParen fullName

            let docs = noParen.Replace("``", "").Replace("`", "-").ToLower()

            let link = sprintf "https://learn.microsoft.com/dotnet/api/%s" docs

            { IsInternal = false
              ReferenceLink = link
              NiceName = simple }

    let internalCrossReference urlBaseName =
        ApiDocEntity.GetUrl(urlBaseName, root, collectionName, qualify, extensions.InUrl)

    let internalCrossReferenceForMember entityUrlBaseName (memb: FSharpMemberOrFunctionOrValue) =
        ApiDocMember.GetUrl(entityUrlBaseName, memb.DisplayName, root, collectionName, qualify, extensions.InUrl)

    let tryResolveCrossReferenceForEntity (entity: FSharpEntity) =
        match registeredSymbolsToUrlBaseName.TryGetValue(entity) with
        | true, _v ->
            let urlBaseName = getUrlBaseNameForRegisteredEntity entity

            Some
                { IsInternal = true
                  ReferenceLink = internalCrossReference urlBaseName
                  NiceName = entity.LogicalName }
        | _ ->
            match entity.TryFullName with
            | None -> None
            | Some nm -> Some(externalDocsLink false entity.DisplayName nm nm)

    let resolveCrossReferenceForTypeByXmlSig (typeXmlSig: string) =
        assert (typeXmlSig.StartsWith("T:", StringComparison.Ordinal))

        match xmlDocNameToSymbol.TryGetValue(typeXmlSig) with
        | true, (:? FSharpEntity as entity) ->
            let urlBaseName = getUrlBaseNameForRegisteredEntity entity

            { IsInternal = true
              ReferenceLink = internalCrossReference urlBaseName
              NiceName = entity.DisplayName }
        | _ ->
            let typeName = typeXmlSig.Substring(2)

            match niceNameEntityLookup.TryGetValue(typeName) with
            | true, entities ->
                match Seq.toList entities with
                | entity :: _rest ->
                    let urlBaseName = getUrlBaseNameForRegisteredEntity entity

                    { IsInternal = true
                      ReferenceLink = internalCrossReference urlBaseName
                      NiceName = entity.DisplayName }
                | _ -> failwith "unreachable"
            | _ ->
                // The cref might be a member reference of form "Type.Member" that was
                // incorrectly prefixed with "T:" by the caller. Try to resolve it as such.
                let lastDot = typeName.LastIndexOf('.')

                if lastDot > 0 then
                    let typePartRaw = typeName.Substring(0, lastDot)
                    let typePartStripped = stripGenericSuffix typePartRaw
                    let memberPart = typeName.Substring(lastDot + 1)

                    // Try the raw type name first (handles "GenericClass2`1"), then stripped (handles "Class2")
                    let tryFindEntities () =
                        match niceNameEntityLookup.TryGetValue(typePartRaw) with
                        | true, entities when entities.Count > 0 -> Some(entities, typePartStripped)
                        | _ when typePartStripped <> typePartRaw ->
                            match niceNameEntityLookup.TryGetValue(typePartStripped) with
                            | true, entities when entities.Count > 0 -> Some(entities, typePartStripped)
                            | _ -> None
                        | _ -> None

                    match tryFindEntities () with
                    | Some(entities, displayTypeName) ->
                        let entity = entities.[0]
                        let urlBaseName = getUrlBaseNameForRegisteredEntity entity

                        entity.MembersFunctionsAndValues
                        |> Seq.tryFind (fun mfv -> mfv.DisplayName = memberPart)
                        |> function
                            | Some mfv ->
                                { IsInternal = true
                                  ReferenceLink = internalCrossReferenceForMember urlBaseName mfv
                                  NiceName = displayTypeName + "." + memberPart }
                            | None ->
                                { IsInternal = true
                                  ReferenceLink = internalCrossReference urlBaseName
                                  NiceName = displayTypeName + "." + memberPart }
                    | None ->
                        // A reference to something external, currently assumed to be in .NET
                        let simple = getMemberName 1 false typeName
                        externalDocsLink false simple typeName typeName
                else
                    // A reference to something external, currently assumed to be in .NET
                    let simple = getMemberName 1 false typeName
                    externalDocsLink false simple typeName typeName

    let mfvToCref (mfv: FSharpMemberOrFunctionOrValue) =
        match mfv.DeclaringEntity with
        | None -> failwith $"%s{mfv.DisplayName} does not have a DeclaringEntity"
        | Some declaringEntity ->
            let entityUrlBaseName = getUrlBaseNameForRegisteredEntity declaringEntity

            { IsInternal = true
              ReferenceLink = internalCrossReferenceForMember entityUrlBaseName mfv
              NiceName = declaringEntity.DisplayName + "." + mfv.DisplayName }

    let tryResolveCrossReferenceForMemberByXmlSig (memberXmlSig: string) =
        assert
            (memberXmlSig.StartsWith("M:", StringComparison.Ordinal)
             || memberXmlSig.StartsWith("P:", StringComparison.Ordinal)
             || memberXmlSig.StartsWith("F:", StringComparison.Ordinal)
             || memberXmlSig.StartsWith("E:", StringComparison.Ordinal))

        match xmlDocNameToSymbol.TryGetValue(memberXmlSig) with
        | true, (:? FSharpMemberOrFunctionOrValue as memb) when memb.DeclaringEntity.IsSome -> memb |> mfvToCref |> Some
        | _ ->
            // If we can't find the exact symbol for the member, don't despair, look for the type
            let memberName = memberXmlSig.Substring(2) |> removeParen

            match tryGetTypeFromMemberName memberName with
            | Some typeName ->
                let typeXmlSig = ("T:" + typeName)

                match xmlDocNameToSymbol.TryGetValue(typeXmlSig) with
                | true, (:? FSharpEntity as entity) ->
                    let urlBaseName = getUrlBaseNameForRegisteredEntity entity

                    // See if we find the member that was intended, otherwise default to containing entity
                    tryGetShortMemberNameFromMemberName memberName
                    |> Option.bind (fun shortName ->
                        entity.MembersFunctionsAndValues
                        |> Seq.tryFind (fun mfv -> mfv.DisplayName = shortName))
                    |> function
                        | Some mb -> Some(mfvToCref mb)
                        | None ->
                            Some
                                { IsInternal = true
                                  ReferenceLink = internalCrossReference urlBaseName
                                  NiceName = getMemberName 2 entity.HasFSharpModuleSuffix memberName }

                | _ ->
                    // The full XML sig lookup failed; try niceNameEntityLookup with the simple type name
                    // Try both the raw name (handles "GenericClass2`1") and stripped (handles "Class2")
                    let typeNameRaw =
                        let lastDot = typeName.LastIndexOf('.')

                        if lastDot >= 0 then
                            typeName.Substring(lastDot + 1)
                        else
                            typeName

                    let typeNameStripped = stripGenericSuffix typeNameRaw

                    let tryFindEntityBySimpleName () =
                        match niceNameEntityLookup.TryGetValue(typeNameRaw) with
                        | true, entities when entities.Count > 0 -> Some entities
                        | _ when typeNameStripped <> typeNameRaw ->
                            match niceNameEntityLookup.TryGetValue(typeNameStripped) with
                            | true, entities when entities.Count > 0 -> Some entities
                            | _ -> None
                        | _ -> None

                    match tryFindEntityBySimpleName () with
                    | Some entities ->
                        let entity = entities.[0]
                        let urlBaseName = getUrlBaseNameForRegisteredEntity entity

                        tryGetShortMemberNameFromMemberName memberName
                        |> Option.bind (fun shortName ->
                            entity.MembersFunctionsAndValues
                            |> Seq.tryFind (fun mfv -> mfv.DisplayName = shortName))
                        |> function
                            | Some mb -> Some(mfvToCref mb)
                            | None ->
                                Some
                                    { IsInternal = true
                                      ReferenceLink = internalCrossReference urlBaseName
                                      NiceName = getMemberName 2 entity.HasFSharpModuleSuffix memberName }
                    | None ->
                        // A reference to something external, currently assumed to be in .NET
                        let simple = getMemberName 2 false memberName
                        Some(externalDocsLink true simple typeName memberName)
            | None ->
                Log.errorf "Assumed '%s' was a member but we cannot extract a type!" memberXmlSig
                None


    // Try to resolve a cref that has no XML doc prefix (e.g. "MyType", "MyType.MyMember", "Map<K,V>")
    let tryResolveUnqualifiedCref (cref: string) =
        let baseName = stripGenericSuffix cref

        if baseName.Contains('.') then
            // Try Type.Member pattern: split on last dot
            let lastDot = baseName.LastIndexOf('.')
            let typePart = stripGenericSuffix (baseName.Substring(0, lastDot))
            let memberPart = baseName.Substring(lastDot + 1)

            match niceNameEntityLookup.TryGetValue(typePart) with
            | true, entities ->
                match Seq.toList entities with
                | entity :: _ ->
                    let urlBaseName = getUrlBaseNameForRegisteredEntity entity

                    entity.MembersFunctionsAndValues
                    |> Seq.tryFind (fun mfv -> mfv.DisplayName = memberPart)
                    |> function
                        | Some mfv -> Some(mfvToCref mfv)
                        | None ->
                            // Fall back to linking to the type page
                            Some
                                { IsInternal = true
                                  ReferenceLink = internalCrossReference urlBaseName
                                  NiceName = typePart + "." + memberPart }
                | _ -> None
            | _ -> None
        else
            // Try unqualified type name
            match niceNameEntityLookup.TryGetValue(baseName) with
            | true, entities ->
                match Seq.toList entities with
                | entity :: _ ->
                    let urlBaseName = getUrlBaseNameForRegisteredEntity entity

                    Some
                        { IsInternal = true
                          ReferenceLink = internalCrossReference urlBaseName
                          NiceName = entity.DisplayName }
                | _ -> None
            | _ -> None

    member _.ResolveCref(cref: string) =
        if (cref.Length < 2) then
            invalidArg "cref" (sprintf "the given cref: '%s' is invalid!" cref)

        match cref with
        // Type
        | _ when cref.StartsWith("T:", StringComparison.Ordinal) -> Some(resolveCrossReferenceForTypeByXmlSig cref)
        // Compiler was unable to resolve!
        | _ when cref.StartsWith("!:", StringComparison.Ordinal) ->
            Log.warnf "Compiler was unable to resolve %s" cref
            None
        // ApiDocMember
        | _ when cref.[1] = ':' -> tryResolveCrossReferenceForMemberByXmlSig cref
        // Try tolerant resolution for unqualified references (no XML doc prefix)
        | _ ->
            match tryResolveUnqualifiedCref cref with
            | Some r -> Some r
            | None ->
                Log.warnf "Unresolved reference '%s'!" cref
                None

    member _.RegisterEntity entity = registerEntity entity

    member _.ResolveUrlBaseNameForEntity entity =
        getUrlBaseNameForRegisteredEntity entity

    member _.TryResolveUrlBaseNameForEntity(entity: FSharpEntity) =
        match registeredSymbolsToUrlBaseName.TryGetValue(entity) with
        | true, v -> Some v
        | _ -> None

    member _.TryResolveEntity entity =
        tryResolveCrossReferenceForEntity entity

    member x.IsLocal cref =
        match x.ResolveCref(cref) with
        | None -> false
        | Some r -> r.IsInternal
