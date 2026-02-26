/// Resolves XML documentation cross-references (cref attributes) to hyperlinks pointing either to
/// internal API doc pages or to external documentation (fsharp.github.io, learn.microsoft.com).
/// Also manages URL base-name registration for all FSharp entities so internal links remain stable.
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
/// Helpers for computing XML doc signature strings (the T:/M:/P:... identifiers) for FSharp symbols.
module internal CrossReferences =
    /// Returns the XML doc signature for a type entity (e.g. "T:FSharp.Formatting.ApiDocs.ApiDocModel").
    let getXmlDocSigForType (typ: FSharpEntity) =
        if not (String.IsNullOrWhiteSpace typ.XmlDocSig) then
            typ.XmlDocSig
        else
            try
                defaultArg (Option.map (sprintf "T:%s") typ.TryFullName) ""
            with _ ->
                ""

    /// Returns the single-letter XML doc sig prefix for a member: "E" for events, "P" for properties, "M" for other members.
    let getMemberXmlDocsSigPrefix (memb: FSharpMemberOrFunctionOrValue) =
        if memb.IsEvent then "E"
        elif memb.IsProperty then "P"
        else "M"

    /// Returns the XML doc signature for a member (e.g. "M:MyNs.MyType.MyMethod(System.String)").
    /// Falls back to constructing one from the member's compiled name and type parameters when the compiler hasn't filled it in.
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

/// The result of resolving a cross-reference: a hyperlink URL plus a display name, and whether the target is internal.
type internal CrefReference =
    { IsInternal: bool
      ReferenceLink: string
      NiceName: string }

/// Resolves cref XML doc cross-references to URLs for the documentation site.
/// Entities must be registered via <c>RegisterEntity</c> before resolution so that internal links can be built.
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
                    // A reference to something external, currently assumed to be in .NET
                    let simple = getMemberName 2 false memberName
                    Some(externalDocsLink true simple typeName memberName)
            | None ->
                Log.errorf "Assumed '%s' was a member but we cannot extract a type!" memberXmlSig
                None

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
        // No idea
        | _ ->
            Log.warnf "Unresolved reference '%s'!" cref
            None

    member _.RegisterEntity entity = registerEntity entity

    member _.ResolveUrlBaseNameForEntity entity =
        getUrlBaseNameForRegisteredEntity entity

    member _.TryResolveEntity entity =
        tryResolveCrossReferenceForEntity entity

    member x.IsLocal cref =
        match x.ResolveCref(cref) with
        | None -> false
        | Some r -> r.IsInternal
