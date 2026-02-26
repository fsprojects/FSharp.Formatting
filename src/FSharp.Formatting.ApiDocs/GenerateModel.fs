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
module internal Utils =


    let (|AllAndLast|_|) (list: 'T list) =
        if list.IsEmpty then
            None
        else
            let revd = List.rev list in Some(List.rev revd.Tail, revd.Head)

    let isAttrib<'T> (attrib: FSharpAttribute) =
        attrib.AttributeType.CompiledName = typeof<'T>.Name

    let hasAttrib<'T> (attribs: IList<FSharpAttribute>) =
        attribs |> Seq.exists (fun a -> isAttrib<'T> (a))

    let tryFindAttrib<'T> (attribs: IList<FSharpAttribute>) =
        attribs |> Seq.tryFind (fun a -> isAttrib<'T> (a))

    let (|MeasureProd|_|) (typ: FSharpType) =
        if
            typ.HasTypeDefinition
            && typ.TypeDefinition.LogicalName = "*"
            && typ.GenericArguments.Count = 2
        then
            Some(typ.GenericArguments.[0], typ.GenericArguments.[1])
        else
            None

    let (|MeasureInv|_|) (typ: FSharpType) =
        if
            typ.HasTypeDefinition
            && typ.TypeDefinition.LogicalName = "/"
            && typ.GenericArguments.Count = 1
        then
            Some typ.GenericArguments.[0]
        else
            None

    [<return: Struct>]
    let (|MeasureOne|_|) (typ: FSharpType) =
        if
            typ.HasTypeDefinition
            && typ.TypeDefinition.LogicalName = "1"
            && typ.GenericArguments.Count = 0
        then
            ValueSome()
        else
            ValueNone

    let tryGetLocation (symbol: FSharpSymbol) =
        match symbol.ImplementationLocation with
        | Some loc -> Some loc
        | None -> symbol.DeclarationLocation

    let isUnitType (ty: FSharpType) =
        ty.HasTypeDefinition
        && ty.TypeDefinition.XmlDocSig = "T:Microsoft.FSharp.Core.unit"

    module List =

        let tailOrEmpty xs =
            match xs with
            | [] -> []
            | _ :: t -> t

        let sepWith sep xs =
            match xs with
            | [] -> []
            | _ ->
                [ for x in xs do
                      yield sep
                      yield x ]
                |> List.tail

    module Html =
        let sepWith s l = l |> List.sepWith (!!s) |> span []

    type System.Xml.Linq.XElement with

        member x.TryAttr(attr: string) =
            let a = x.Attribute(XName.Get attr)

            if isNull a then None
            else if String.IsNullOrEmpty a.Value then None
            else Some a.Value

/// Represents some HTML formatted by model generation
type ApiDocHtml(html: string, id: string option) =

    /// Get the HTML text of the HTML section
    member _.HtmlText = html
    /// Get the Id of the element when rendered to html, if any
    member _.Id = id

/// Represents a documentation comment attached to source code
type ApiDocComment(xmldoc, summary, remarks, parameters, returns, examples, notes, exceptions, rawData) =

    /// The XElement for the XML doc if available
    member _.Xml: XElement option = xmldoc

    /// The summary for the comment
    member _.Summary: ApiDocHtml = summary

    /// The remarks html for comment
    member _.Remarks: ApiDocHtml option = remarks

    /// The param sections of the comment
    member _.Parameters: (string * ApiDocHtml) list = parameters

    /// The examples sections of the comment
    member _.Examples: ApiDocHtml list = examples

    /// The notes sections of the comment
    member _.Notes: ApiDocHtml list = notes

    /// The return sections of the comment
    member _.Returns: ApiDocHtml option = returns

    /// The notes sections of the comment
    member _.Exceptions: (string * string option * ApiDocHtml) list = exceptions

    /// The raw data of the comment
    member _.RawData: KeyValuePair<string, string> list = rawData

    static member internal Empty = ApiDocComment(None, ApiDocHtml("", None), None, [], None, [], [], [], [])

/// Represents a custom attribute attached to source code
type ApiDocAttribute(name, fullName, constructorArguments, namedConstructorArguments) =
    /// The name of the attribute
    member _.Name: string = name

    /// The qualified name of the attribute
    member _.FullName: string = fullName

    /// The arguments to the constructor for the attribute
    member _.ConstructorArguments: obj list = constructorArguments

    /// The named arguments for the attribute
    member _.NamedConstructorArguments: (string * obj) list = namedConstructorArguments

    /// Gets a value indicating whether this attribute is System.ObsoleteAttribute
    member x.IsObsoleteAttribute = x.FullName = "System.ObsoleteAttribute"

    /// Gets a value indicating whether this attribute is RequireQualifiedAccessAttribute
    member x.IsRequireQualifiedAccessAttribute = x.FullName = typeof<RequireQualifiedAccessAttribute>.FullName

    /// Returns the obsolete message, when this attribute is the System.ObsoleteAttribute. When its not or no message was specified, an empty string is returned
    member x.ObsoleteMessage =
        let tryFindObsoleteMessage =
            x.ConstructorArguments
            |> List.tryPick (fun x ->
                match x with
                | :? string as s -> Some s
                | _ -> None)
            |> Option.defaultValue ""

        if x.IsObsoleteAttribute then tryFindObsoleteMessage else ""

    /// Gets a value indicating whether this attribute the CustomOperationAttribute
    member x.IsCustomOperationAttribute = x.FullName = "Microsoft.FSharp.Core.CustomOperationAttribute"

    /// Returns the custom operation name, when this attribute is the CustomOperationAttribute. When its not an empty string is returned
    member x.CustomOperationName =
        let tryFindCustomOperation =
            x.ConstructorArguments
            |> List.tryPick (fun x ->
                match x with
                | :? string as s -> Some s
                | _ -> None)
            |> Option.defaultValue ""

        if x.IsCustomOperationAttribute then
            tryFindCustomOperation
        else
            ""

    /// Formats the attribute with the given name
    member private x.Format(attributeName: string, removeAttributeSuffix: bool) =
        let dropSuffix (s: string) (t: string) = s.[0 .. s.Length - t.Length - 1]

        let attributeName =
            if
                removeAttributeSuffix
                && attributeName.EndsWith("Attribute", StringComparison.Ordinal)
            then
                dropSuffix attributeName "Attribute"
            else
                attributeName

        let join sep (items: string seq) = String.Join(sep, items)
        let inline append (s: string) (sb: StringBuilder) = sb.Append(s)
        let inline appendIfTrue p s sb = if p then append s sb else sb

        let rec formatValue (v: obj) =
            match v with
            | :? string as s -> sprintf "\"%s\"" s
            | :? (obj array) as a -> a |> Seq.map formatValue |> join "; " |> sprintf "[|%s|]"
            | :? bool as b -> if b then "true" else "false"
            | _ -> string<obj> v

        let formatedConstructorArguments = x.ConstructorArguments |> Seq.map formatValue |> join ", "

        let formatedNamedConstructorArguments =
            x.NamedConstructorArguments
            |> Seq.map (fun (n, v) -> sprintf "%s = %s" n (formatValue v))
            |> join ", "

        let needsBraces = not (List.isEmpty x.ConstructorArguments && List.isEmpty x.NamedConstructorArguments)

        let needsListSeperator = not (List.isEmpty x.ConstructorArguments || List.isEmpty x.NamedConstructorArguments)

        StringBuilder()
        |> append "[<"
        |> append attributeName
        |> appendIfTrue needsBraces "("
        |> append formatedConstructorArguments
        |> appendIfTrue needsListSeperator ", "
        |> append formatedNamedConstructorArguments
        |> appendIfTrue needsBraces ")"
        |> append ">]"
        |> string<StringBuilder>

    /// Formats the attribute using the Name. Removes the "Attribute"-suffix. E.g Obsolete
    member x.Format() = x.Format(x.Name, true)

    /// Formats the attribute using the FullName. Removes the "Attribute"-suffix. E.g System.Obsolete
    member x.FormatFullName() = x.Format(x.FullName, true)

    /// Formats the attribute using the Name. Keeps the "Attribute"-suffix. E.g ObsoleteAttribute
    member x.FormatLongForm() = x.Format(x.Name, false)

    /// Formats the attribute using the FullName. Keeps the "Attribute"-suffix. E.g System.ObsoleteAttribute
    member x.FormatFullNameLongForm() = x.Format(x.FullName, false)

    /// Tries to find the System.ObsoleteAttribute and return its obsolete message
    static member internal TryGetObsoleteMessage(attributes: ApiDocAttribute seq) =
        attributes
        |> Seq.tryFind (fun a -> a.IsObsoleteAttribute)
        |> Option.map (fun a -> a.ObsoleteMessage)
        |> Option.defaultValue ""

    /// Tries to find the CustomOperationAttribute and return its obsolete message
    static member internal TryGetCustomOperationName(attributes: ApiDocAttribute seq) =
        attributes
        |> Seq.tryFind (fun a -> a.IsCustomOperationAttribute)
        |> Option.map (fun a -> a.CustomOperationName)


/// Represents the kind of member
type ApiDocMemberKind =
    // In a module
    | ValueOrFunction = 0
    | TypeExtension = 1
    | ActivePattern = 2

    // In a class
    | Constructor = 3
    | InstanceMember = 4
    | StaticMember = 5

    // In a class, F# special members
    | UnionCase = 100
    | RecordField = 101
    | StaticParameter = 102

type ApiDocMemberDetails =
    | ApiDocMemberDetails of
        usageHtml: ApiDocHtml *
        paramTypes: (Choice<FSharpParameter, FSharpField> * string * ApiDocHtml) list *
        returnType: (FSharpType * ApiDocHtml) option *
        modifiers: string list *
        typars: string list *
        extendedType: (FSharpEntity * ApiDocHtml) option *
        location: string option *
        compiledName: string option

/// Represents an method, property, constructor, function or value, record field, union case or static parameter
/// integrated with its associated documentation. Includes extension members.
type ApiDocMember
    (
        displayName: string,
        attributes: ApiDocAttribute list,
        entityUrlBaseName: string,
        kind,
        cat,
        catidx: int,
        exclude: bool,
        details: ApiDocMemberDetails,
        comment: ApiDocComment,
        symbol: FSharpSymbol,
        warn
    ) =

    let (ApiDocMemberDetails(usageHtml, paramTypes, returnType, modifiers, typars, extendedType, location, compiledName)) =
        details

    let m = defaultArg symbol.DeclarationLocation range0
    // merge the parameter docs and parameter types
    let parameters =
        let paramTypes =
            [ for (psym, _pnameText, _pty) in paramTypes ->
                  let pnm =
                      match psym with
                      | Choice1Of2 p -> p.Name
                      | Choice2Of2 f -> Some f.Name

                  (psym, pnm, _pnameText, _pty) ]

        let tnames = Set.ofList [ for (_psym, pnm, _pnameText, _pty) in paramTypes -> pnm ]

        let tdocs = Map.ofList [ for pnm, doc in comment.Parameters -> Some pnm, doc ]

        if warn then
            for (pn, _pdoc) in comment.Parameters do
                if not (tnames.Contains(Some pn)) then
                    printfn
                        "%s(%d,%d): warning: extraneous docs for unknown parameter '%s'"
                        m.FileName
                        m.StartLine
                        m.StartColumn
                        pn

            for (psym, pnm, _pn, _pty) in paramTypes do
                match pnm with
                | None ->
                    match psym with
                    | Choice1Of2 p ->
                        if isUnitType p.Type |> not then
                            printfn
                                "%s(%d,%d): warning: a parameter was missing a name"
                                m.FileName
                                m.StartLine
                                m.StartColumn
                    | Choice2Of2 _ ->
                        printfn "%s(%d,%d): warning: a field was missing a name" m.FileName m.StartLine m.StartColumn
                | Some nm ->
                    if not (tdocs.ContainsKey pnm) then
                        printfn
                            "%s(%d,%d): warning: missing docs for parameter '%s'"
                            m.FileName
                            m.StartLine
                            m.StartColumn
                            nm

        [ for (psym, pnm, pn, pty) in paramTypes ->
              {| ParameterSymbol = psym
                 ParameterNameText = pn
                 ParameterType = pty
                 ParameterDocs = tdocs.TryFind pnm |} ]

    do
        let knownExampleIds = comment.Examples |> List.choose (fun x -> x.Id) |> List.countBy id

        for (id, count) in knownExampleIds do
            if count > 1 then
                if warn then
                    printfn "%s(%d,%d): warning: duplicate id for example '%s'" m.FileName m.StartLine m.StartColumn id
                else
                    printfn "%s(%d,%d): error: duplicate id for example '%s'" m.FileName m.StartLine m.StartColumn id

        for (id, _count) in knownExampleIds do
            if id.StartsWith("example-", StringComparison.Ordinal) then
                let potentialInteger = id.["example-".Length ..]

                match System.Int32.TryParse potentialInteger with
                | true, id ->
                    if warn then
                        printfn
                            "%s(%d,%d): warning: automatic identifer generated for example '%d'. Consider adding an explicit example id attribute."
                            m.FileName
                            m.StartLine
                            m.StartColumn
                            id
                    else
                        printfn
                            "%s(%d,%d): error: automatic identifer generated for example '%d'. Consider adding an explicit example id attribute."
                            m.FileName
                            m.StartLine
                            m.StartColumn
                            id
                | _ -> ()


    /// The member's modifiers
    member x.Modifiers: string list = modifiers

    /// The member's type arguments
    member x.TypeArguments: string list = typars

    /// The usage section in a typical tooltip
    member x.UsageHtml: ApiDocHtml = usageHtml

    /// The return section in a typical tooltip
    member x.ReturnInfo =
        {| ReturnDocs = comment.Returns
           ReturnType = returnType |}

    //    /// The full signature section in a typical tooltip
    //  member x.SignatureTooltip : ApiDocHtml = signatureTooltip

    /// The member's parameters and associated documentation
    member x.Parameters = parameters

    /// The URL of the member's source location, if any
    member x.SourceLocation: string option = location

    /// The type extended by an extension member, if any
    member x.ExtendedType: (FSharpEntity * ApiDocHtml) option = extendedType

    /// The members details
    member x.Details = details

    /// The member's compiled name, if any
    member x.CompiledName: string option = compiledName

    /// Formats type arguments
    member x.FormatTypeArguments =
        // We suppress the display of ill-formatted type parameters for places
        // where these have not been explicitly declared.  This could likely be done
        // in a better way
        let res = String.concat ", " x.TypeArguments

        if x.TypeArguments.IsEmpty || res.Contains("?") then
            None
        else
            Some res

    /// Formats modifiers
    member x.FormatModifiers = String.concat " " x.Modifiers

    /// Formats the compiled name
    member x.FormatCompiledName = defaultArg x.CompiledName ""

    /// Name of the member
    member x.Name = displayName

    /// The URL base name of the best link documentation for the item (without the http://site.io/reference)
    member x.UrlBaseName = entityUrlBaseName

    /// The URL of the best link documentation for the item relative to "reference" directory (without the http://site.io/reference)
    static member GetUrl(entityUrlBaseName, displayName, root, collectionName, qualify, extension) =
        sprintf
            "%sreference/%s%s%s#%s"
            root
            (if qualify then collectionName + "/" else "")
            entityUrlBaseName
            extension
            displayName

    /// The URL of the best link documentation for the item relative to "reference" directory (without the http://site.io/reference)
    member x.Url(root, collectionName, qualify, extension) =
        ApiDocMember.GetUrl(entityUrlBaseName, displayName, root, collectionName, qualify, extension)

    /// The declared attributes of the member
    member x.Attributes = attributes

    /// The category
    member x.Category: string = cat

    /// The category index
    member x.CategoryIndex: int = catidx

    /// The exclude flag
    member x.Exclude: bool = exclude

    /// The kind of the member
    member x.Kind: ApiDocMemberKind = kind

    /// The attached comment
    member x.Comment: ApiDocComment = comment

    /// The symbol this member is related to
    member x.Symbol: FSharpSymbol = symbol

    /// Gets a value indicating whether this member is obsolete
    member x.IsObsolete = x.Attributes |> List.exists (fun a -> a.IsObsoleteAttribute)

    /// Returns the obsolete message, when this member is obsolete. When its not or no message was specified, an empty string is returned
    member x.ObsoleteMessage = ApiDocAttribute.TryGetObsoleteMessage(x.Attributes)

    member x.IsRequireQualifiedAccessAttribute =
        x.Attributes |> List.exists (fun a -> a.IsRequireQualifiedAccessAttribute)

    /// Returns the custom operation name, when this attribute is the CustomOperationAttribute.
    member x.CustomOperationName = ApiDocAttribute.TryGetCustomOperationName(x.Attributes)

/// Represents a type definition integrated with its associated documentation
type ApiDocEntity
    (
        tdef,
        name,
        cat: string,
        catidx: int,
        exclude: bool,
        urlBaseName,
        comment,
        assembly: AssemblyName,
        attributes,
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
        symbol: FSharpEntity,
        nested,
        vals,
        exts,
        pats,
        rqa,
        location: string option,
        substitutions: Substitutions
    ) =

    /// Indicates if the entity is a type definition
    member x.IsTypeDefinition: bool = tdef

    /// The name of the entity
    member x.Name: string = name

    /// The category of the type
    member x.Category = cat

    /// The category index of the type
    member x.CategoryIndex = catidx

    /// The exclude flag
    member x.Exclude = exclude

    /// The URL of the member's source location, if any
    member x.SourceLocation: string option = location

    /// The URL base name of the primary documentation for the entity  (without the http://site.io/reference)
    member x.UrlBaseName: string = urlBaseName

    /// Compute the URL of the best link for the entity relative to "reference" directory (without the http://site.io/reference)
    static member GetUrl(urlBaseName, root, collectionName, qualify, extension) =
        sprintf "%sreference/%s%s%s" root (if qualify then collectionName + "/" else "") urlBaseName extension

    /// The URL of the best link for the entity relative to "reference" directory (without the http://site.io/reference)
    member x.Url(root, collectionName, qualify, extension) =
        ApiDocEntity.GetUrl(urlBaseName, root, collectionName, qualify, extension)

    /// The name of the file generated for this entity
    member x.OutputFile(collectionName, qualify, extension) =
        sprintf "reference/%s%s%s" (if qualify then collectionName + "/" else "") urlBaseName extension

    /// The attached comment
    member x.Comment: ApiDocComment = comment

    /// The name of the type's assembly
    member x.Assembly = assembly

    /// The declared attributes of the type
    member x.Attributes: ApiDocAttribute list = attributes

    /// The cases of a union type
    member x.UnionCases: ApiDocMember list = cases

    /// The fields of a record type
    member x.RecordFields: ApiDocMember list = fields

    /// Static parameters
    member x.StaticParameters: ApiDocMember list = statParams

    /// All members of the type
    member x.AllMembers: ApiDocMember list =
        List.concat [ ctors; inst; stat; cases; fields; statParams; vals; exts; pats ]

    /// All interfaces of the type, formatted
    member x.AllInterfaces: (FSharpType * ApiDocHtml) list = allInterfaces

    /// The base type of the type, formatted
    member x.BaseType: (FSharpType * ApiDocHtml) option = baseType

    /// If this is a type abbreviation, then the abbreviated type
    member x.AbbreviatedType: (FSharpType * ApiDocHtml) option = abbreviatedType

    /// If this is a delegate, then e formatted signature
    member x.DelegateSignature: (FSharpDelegateSignature * ApiDocHtml) option = delegateSignature

    /// The constuctorsof the type
    member x.Constructors: ApiDocMember list = ctors

    /// The instance members of the type
    member x.InstanceMembers: ApiDocMember list = inst

    /// The static members of the type
    member x.StaticMembers: ApiDocMember list = stat

    /// Gets a value indicating whether this member is obsolete
    member x.IsObsolete = x.Attributes |> List.exists (fun a -> a.IsObsoleteAttribute)

    /// Returns the obsolete message, when this member is obsolete. When its not or no message was specified, an empty string is returned
    member x.ObsoleteMessage = ApiDocAttribute.TryGetObsoleteMessage(x.Attributes)

    /// The F# compiler symbol for the type definition
    member x.Symbol = symbol

    /// Does the module have the RequiresQualifiedAccess attribute
    member x.RequiresQualifiedAccess: bool = rqa

    /// All nested modules and types
    member x.NestedEntities: ApiDocEntity list = nested |> List.filter (fun e -> not e.Exclude)

    /// Values and functions of the module
    member x.ValuesAndFuncs: ApiDocMember list = vals

    /// Type extensions of the module
    member x.TypeExtensions: ApiDocMember list = exts

    /// Active patterns of the module
    member x.ActivePatterns: ApiDocMember list = pats

    /// The substitution parameters active for generating thist content
    member x.Substitutions = substitutions


/// Represents a namespace integrated with its associated documentation
type ApiDocNamespace(name: string, modifiers, substitutions: Substitutions, nsdocs: ApiDocComment option) =

    let urlBaseName = name.Replace(".", "-").ToLower()

    /// The name of the namespace
    member x.Name: string = name

    /// The hash label for the URL with the overall namespaces file
    member x.UrlHash = urlBaseName

    /// The base name for the generated file
    member x.UrlBaseName = urlBaseName

    /// The URL of the best link documentation for the item (without the http://site.io/reference)
    member x.Url(root, collectionName, qualify, extension) =
        sprintf "%sreference/%s%s%s" root (if qualify then collectionName + "/" else "") urlBaseName extension

    /// The name of the file generated for this entity
    member x.OutputFile(collectionName, qualify, extension) =
        sprintf "reference/%s%s%s" (if qualify then collectionName + "/" else "") urlBaseName extension

    /// All modules in the namespace
    member x.Entities: ApiDocEntity list = modifiers

    /// The summary text for the namespace
    member x.NamespaceDocs = nsdocs

    /// The substitution substitutions active for generating thist content
    member x.Substitutions = substitutions

/// Represents a group of assemblies integrated with its associated documentation
type ApiDocCollection(name: string, asms: AssemblyName list, nss: ApiDocNamespace list) =

    /// Name of the collection
    member x.CollectionName = name

    /// All assemblies in the collection
    member x.Assemblies = asms

    /// All namespaces in the collection
    member x.Namespaces = nss

/// High-level information about a module definition
type ApiDocEntityInfo
    (entity: ApiDocEntity, collection: ApiDocCollection, ns: ApiDocNamespace, parent: ApiDocEntity option) =
    /// The actual entity
    member x.Entity = entity

    /// The collection of assemblies the entity belongs to
    member x.Collection = collection

    /// The namespace the entity belongs to
    member x.Namespace = ns

    /// The parent module, if any.
    member x.ParentModule = parent

[<AutoOpen>]
module internal CrossReferences =
    let getXmlDocSigForType (typ: FSharpEntity) =
        if not (String.IsNullOrWhiteSpace typ.XmlDocSig) then
            typ.XmlDocSig
        else
            try
                defaultArg (Option.map (sprintf "T:%s") typ.TryFullName) ""
            with _ ->
                ""

    let getMemberXmlDocsSigPrefix (memb: FSharpMemberOrFunctionOrValue) =
        if memb.IsEvent then "E"
        elif memb.IsProperty then "P"
        else "M"

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

                    let paramList =
                        if
                            memb.CurriedParameterGroups.Count > 0
                            && memb.CurriedParameterGroups.[0].Count > 0
                        then
                            let head = memb.CurriedParameterGroups.[0]

                            let paramTypeList =
                                head
                                |> Seq.map (fun param ->
                                    if param.Type.IsGenericParameter then
                                        typeArgsMap.[param.Type.GenericParameter.Name]
                                    else
                                        param.Type.TypeDefinition.FullName)

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

type internal CrefReference =
    { IsInternal: bool
      ReferenceLink: string
      NiceName: string }

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


[<AutoOpen>]
module internal TypeFormatter =

    type TypeFormatterParams = CrossReferenceResolver

    let convHtml (html: HtmlElement) = ApiDocHtml(html.ToString(), None)

    /// We squeeze the spaces out of anything where whitespace layout must be exact - any deliberate
    /// whitespace must use &#32;
    ///
    /// This kind of sucks but stems from the fact the formatting for the internal HTML DSL is freely
    /// adding spaces which are actually significant when formatting F# type information.
    let codeHtml html =
        let html = code [] [ html ]

        ApiDocHtml(
            html.ToString().Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("<ahref", "<a href"),
            None
        )

    let formatSourceLocation
        (urlRangeHighlight: Uri -> int -> int -> string)
        (sourceFolderRepo: (string * string) option)
        (location: range option)
        =
        location
        |> Option.bind (fun location ->
            sourceFolderRepo
            |> Option.map (fun (sourceFolder, sourceRepo) ->
                let sourceFolderPath = Uri(Path.GetFullPath(sourceFolder)).ToString()

                let docPath = Uri(Path.GetFullPath(location.FileName)).ToString()

                // Even though ignoring case might be wrong, we do that because
                // one path might be file:///C:\... and the other file:///c:\...  :-(
                if
                    not
                    <| docPath.StartsWith(sourceFolderPath, StringComparison.InvariantCultureIgnoreCase)
                then
                    Log.verbf "Current source file '%s' doesn't reside in source folder '%s'" docPath sourceFolderPath
                    ""
                else
                    let relativePath = docPath.[sourceFolderPath.Length ..]
                    let uriBuilder = UriBuilder(sourceRepo)
                    uriBuilder.Path <- uriBuilder.Path + relativePath
                    urlRangeHighlight uriBuilder.Uri location.StartLine location.EndLine))

    let formatTypeArgumentAsText (typar: FSharpGenericParameter) =
        (if typar.IsSolveAtCompileTime then "^" else "'") + typar.Name

    let formatTypeArgumentsAsText (typars: FSharpGenericParameter list) =
        List.map formatTypeArgumentAsText typars

    let bracketHtml (str: HtmlElement) = span [] [ !!"("; str; !!")" ]

    let bracketNonAtomicHtml (str: HtmlElement) =
        if str.ToString().Contains("&#32;") then
            bracketHtml str
        else
            str

    let bracketHtmlIf cond str = if cond then bracketHtml str else str

    let formatTyconRefAsHtml (ctx: TypeFormatterParams) (tcref: FSharpEntity) =
        let core = !!tcref.DisplayName.Replace(" ", "&#32;")

        match ctx.TryResolveEntity tcref with
        | None -> core
        | Some url -> a [ Href url.ReferenceLink ] [ core ]

    let rec formatTypeApplicationAsHtml ctx (tcref: FSharpEntity) typeName prec prefix args : HtmlElement =
        if prefix then
            match args with
            | [] -> typeName
            | [ arg ] -> span [] [ typeName; !!"&lt;"; (formatTypeWithPrecAsHtml ctx 4 arg); !!"&gt;" ]
            | args ->
                bracketHtmlIf
                    (prec <= 1)
                    (span [] [ typeName; !!"&lt;"; formatTypesWithPrecAsHtml ctx 2 ",&#32;" args; !!"&gt;" ])
        else
            match args with
            | [] -> typeName
            | [ arg ] ->
                if tcref.DisplayName.StartsWith '[' then
                    span [] [ formatTypeWithPrecAsHtml ctx 2 arg; !!tcref.DisplayName ]
                else
                    span [] [ formatTypeWithPrecAsHtml ctx 2 arg; !!"&#32;"; typeName ]
            | args ->
                bracketHtmlIf
                    (prec <= 1)
                    (span [] [ bracketNonAtomicHtml (formatTypesWithPrecAsHtml ctx 2 ",&#32;" args); typeName ])

    and formatTypesWithPrecAsHtml ctx prec sep typs =
        typs |> List.map (formatTypeWithPrecAsHtml ctx prec) |> Html.sepWith sep

    and formatTypeWithPrecAsHtml ctx prec (typ: FSharpType) =
        // Measure types are stored as named types with 'fake' constructors for products, "1" and inverses
        // of measures in a normalized form (see Andrew Kennedy technical reports). Here we detect this
        // embedding and use an approximate set of rules for layout out normalized measures in a nice way.
        match typ with
        | MeasureProd(ty, MeasureOne)
        | MeasureProd(MeasureOne, ty) -> formatTypeWithPrecAsHtml ctx prec ty
        | MeasureProd(ty1, MeasureInv ty2)
        | MeasureProd(ty1, MeasureProd(MeasureInv ty2, MeasureOne)) ->
            span [] [ formatTypeWithPrecAsHtml ctx 2 ty1; !!"/"; formatTypeWithPrecAsHtml ctx 2 ty2 ]
        | MeasureProd(ty1, MeasureProd(ty2, MeasureOne))
        | MeasureProd(ty1, ty2) ->
            span [] [ formatTypeWithPrecAsHtml ctx 2 ty1; !!"*"; formatTypeWithPrecAsHtml ctx 2 ty2 ]
        | MeasureInv ty -> span [] [ !!"/"; formatTypeWithPrecAsHtml ctx 1 ty ]
        | MeasureOne -> !!"1"
        | _ when typ.HasTypeDefinition ->
            let tcref = typ.TypeDefinition
            let tyargs = typ.GenericArguments |> Seq.toList
            // layout postfix array types
            formatTypeApplicationAsHtml ctx tcref (formatTyconRefAsHtml ctx tcref) prec tcref.UsesPrefixDisplay tyargs
        | _ when typ.IsTupleType ->
            let tyargs = typ.GenericArguments |> Seq.toList
            bracketHtmlIf (prec <= 2) (formatTypesWithPrecAsHtml ctx 2 "&#32;*&#32;" tyargs)
        | _ when typ.IsFunctionType ->
            let rec loop soFar (typ: FSharpType) =
                if typ.IsFunctionType then
                    let domainTyp, retType = typ.GenericArguments.[0], typ.GenericArguments.[1]

                    loop (soFar @ [ formatTypeWithPrecAsHtml ctx 4 domainTyp; !!"&#32;->&#32;" ]) retType
                else
                    span [] (soFar @ [ formatTypeWithPrecAsHtml ctx 5 typ ])

            bracketHtmlIf (prec <= 4) (loop [] typ)
        | _ when typ.IsGenericParameter -> !!(formatTypeArgumentAsText typ.GenericParameter)
        | _ -> !!"(type)"

    let formatTypeAsHtml ctx (typ: FSharpType) = formatTypeWithPrecAsHtml ctx 5 typ

    let formatArgNameAndTypePair i (argName, argType) =
        let argName =
            match argName with
            | None -> if isUnitType argType then "()" else "arg" + string<int> i
            | Some nm -> nm

        argName, argType

    let formatArgNameAndType i (arg: FSharpParameter) =
        let argName, argType = formatArgNameAndTypePair i (arg.Name, arg.Type)

        let isOptionalArg = arg.IsOptionalArg || hasAttrib<OptionalArgumentAttribute> arg.Attributes

        let argName = if isOptionalArg then "?" + argName else argName

        let argType =
            // Strip off the 'option' type for optional arguments
            if isOptionalArg && argType.HasTypeDefinition && argType.GenericArguments.Count = 1 then
                argType.GenericArguments.[0]
            else
                argType

        argName, argType

    // Format each argument, including its name and type
    let formatArgUsageAsHtml i (arg: FSharpParameter) =
        let argName, _argType = formatArgNameAndType i arg
        !!argName

    let formatArgNameAndTypePairUsageAsHtml ctx (argName0, argType) =
        span
            []
            [ !!(match argName0 with
                 | None -> ""
                 | Some argName -> argName + ":&#32;")
              formatTypeWithPrecAsHtml ctx 2 argType ]

    let formatCurriedArgsUsageAsHtml preferNoParens isItemIndexer curriedArgs =
        let counter =
            let mutable n = 0

            fun () ->
                n <- n + 1
                n

        curriedArgs
        |> List.map (fun args ->
            let argTuple = args |> List.map (formatArgNameAndType (counter ()) >> fst)

            match argTuple with
            | [] -> !!"()"
            | [ argName ] when argName = "()" -> !!"()"
            | [ argName ] when preferNoParens -> !!argName
            | args ->
                let argText = args |> List.map (!!) |> Html.sepWith ",&#32;"

                if isItemIndexer then argText else bracketHtml argText)
        |> Html.sepWith "&#32;"

    let formatDelegateSignatureAsHtml ctx nm (typ: FSharpDelegateSignature) =
        let args =
            typ.DelegateArguments
            |> List.ofSeq
            |> List.map (formatArgNameAndTypePairUsageAsHtml ctx)
            |> Html.sepWith "&#32;*&#32;"

        span [] ([ !!nm; !!"("; args; !!"&#32;->&#32;"; formatTypeAsHtml ctx typ.DelegateReturnType; !!")" ])

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
                ctx.Substitutions
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
                ctx.Substitutions
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

/// Represents an input assembly for API doc generation
type ApiDocInput =
    {
        /// The path to the assembly
        Path: string

        /// Override the default XML file (normally assumed to live alongside)
        XmlFile: string option

        /// The compile-time source folder
        SourceFolder: string option

        /// The URL the the source repo where the source code lives
        SourceRepo: string option

        /// The substitutionss active for this input. If specified these
        /// are used instead of the overall substitutions.  This allows different parameters (e.g.
        /// different authors) for each assembly in a collection.
        Substitutions: Substitutions option

        /// Whether the input uses markdown comments
        MarkdownComments: bool

        /// Whether doc processing should warn on missing comments
        Warn: bool

        /// Whether to generate only public things
        PublicOnly: bool
    }

    static member FromFile
        (assemblyPath: string, ?mdcomments, ?substitutions, ?sourceRepo, ?sourceFolder, ?publicOnly, ?warn)
        =
        { Path = assemblyPath
          XmlFile = None
          SourceFolder = sourceFolder
          SourceRepo = sourceRepo
          Warn = defaultArg warn false
          Substitutions = substitutions
          PublicOnly = defaultArg publicOnly true
          MarkdownComments = defaultArg mdcomments false }


type ApiDocFileExtensions = { InFile: string; InUrl: string }

/// Represents a set of assemblies integrated with their associated documentation
type ApiDocModel internal (substitutions, collection, entityInfos, root, qualify, fileExtensions, urlMap) =
    /// The substitutions.  Different substitutions can also be used for each specific input
    member _.Substitutions: Substitutions = substitutions

    /// The full list of all entities
    member _.Collection: ApiDocCollection = collection

    /// The full list of all entities
    member _.EntityInfos: ApiDocEntityInfo list = entityInfos |> List.filter (fun info -> not info.Entity.Exclude)

    /// The root URL for the entire generation, normally '/'
    member _.Root: string = root

    /// Indicates if each collection is being qualified by its collection name, e.g. 'reference/FSharp.Core'
    member _.Qualify: bool = qualify

    /// Specifies file extensions to use in files and URLs
    member _.FileExtensions: ApiDocFileExtensions = fileExtensions

    /// Specifies file extensions to use in files and URLs
    member internal _.Resolver: CrossReferenceResolver = urlMap

    /// URL of the 'index.html' for the reference documentation for the model
    member x.IndexFileUrl(root, collectionName, qualify, extension) =
        sprintf "%sreference/%sindex%s" root (if qualify then collectionName + "/" else "") extension

    /// URL of the 'index.html' for the reference documentation for the model
    member x.IndexOutputFile(collectionName, qualify, extension) =
        sprintf "reference/%sindex%s" (if qualify then collectionName + "/" else "") extension

    static member internal Generate
        (
            projects: ApiDocInput list,
            collectionName,
            libDirs,
            otherFlags,
            qualify,
            urlRangeHighlight,
            root,
            substitutions,
            onError,
            extensions
        ) =

        // Default template file names

        let otherFlags = defaultArg otherFlags [] |> List.map (fun (o: string) -> o.Trim())

        let libDirs = defaultArg libDirs [] |> List.map Path.GetFullPath

        let dllFiles = projects |> List.map (fun p -> Path.GetFullPath p.Path)

        let urlRangeHighlight =
            defaultArg urlRangeHighlight (fun url start stop -> String.Format("{0}#L{1}-{2}", url, start, stop))

        // Compiler arguments used when formatting code snippets inside Markdown comments
        let codeFormatCompilerArgs =
            [ for dir in libDirs do
                  yield sprintf "-I:\"%s\"" dir
              for file in dllFiles do
                  yield sprintf "-r:\"%s\"" file ]
            |> String.concat " "

        printfn "  loading %d assemblies..." dllFiles.Length

        let resolvedList =
            FSharpAssembly.LoadFiles(dllFiles, libDirs, otherFlags = otherFlags)
            |> List.zip projects

        // generate the names for the html files beforehand so we can resolve <see cref=""/> links.
        let urlMap = CrossReferenceResolver(root, collectionName, qualify, extensions)

        // Read and process assemblies and the corresponding XML files
        let assemblies =

            for (_, asmOpt) in resolvedList do
                match asmOpt with
                | (_, Some asm) ->
                    printfn "  registering entities for assembly %s..." asm.SimpleName

                    asm.Contents.Entities |> Seq.iter (urlMap.RegisterEntity)
                | _ -> ()

            resolvedList
            |> List.choose (fun (project, (dllFile, asmOpt)) ->
                let sourceFolderRepo =
                    match project.SourceFolder, project.SourceRepo with
                    | Some folder, Some repo -> Some(folder, repo)
                    | Some _folder, _ ->
                        Log.warnf "Repository url should be specified along with source folder."
                        None
                    | _, Some _repo ->
                        Log.warnf "Repository url should be specified along with source folder."
                        None
                    | _ -> None

                match asmOpt with
                | None ->
                    printfn "**** Skipping assembly '%s' because was not found in resolved assembly list" dllFile
                    onError "exiting"
                    None
                | Some asm ->
                    printfn "  reading XML doc for %s..." dllFile

                    let xmlFile = defaultArg project.XmlFile (Path.ChangeExtension(dllFile, ".xml"))

                    let xmlFileNoExt = Path.GetFileNameWithoutExtension(xmlFile)

                    let xmlFileOpt =
                        //Directory.EnumerateFiles(Path.GetDirectoryName(xmlFile), xmlFileNoExt + ".*")
                        Directory.EnumerateFiles(Path.GetDirectoryName xmlFile)
                        |> Seq.filter (fun file ->
                            let fileNoExt = Path.GetFileNameWithoutExtension file
                            let ext = Path.GetExtension file

                            xmlFileNoExt.Equals(fileNoExt, StringComparison.OrdinalIgnoreCase)
                            && ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                        |> Seq.tryHead
                    //|> Seq.map (fun f -> f, f.Remove(0, xmlFile.Length - 4))
                    //|> Seq.tryPick (fun (f, ext) ->
                    //    if ext.Equals(".xml", StringComparison.CurrentCultureIgnoreCase)
                    //      then Some(f) else None)

                    let publicOnly = project.PublicOnly
                    let mdcomments = project.MarkdownComments

                    let substitutions = defaultArg project.Substitutions substitutions

                    match xmlFileOpt with
                    | None -> raise (FileNotFoundException(sprintf "Associated XML file '%s' was not found." xmlFile))
                    | Some xmlFile ->
                        printfn "  reading assembly data for %s..." dllFile

                        SymbolReader.readAssembly (
                            asm,
                            publicOnly,
                            xmlFile,
                            substitutions,
                            sourceFolderRepo,
                            urlRangeHighlight,
                            mdcomments,
                            urlMap,
                            codeFormatCompilerArgs,
                            project.Warn
                        )
                        |> Some)

        printfn "  collecting namespaces..."
        // Union namespaces from multiple libraries
        let namespaces = Dictionary<_, (_ * _ * Substitutions)>()

        for asm, nss in assemblies do
            for ns in nss do
                printfn "  found namespace %s in assembly %s..." ns.Name asm.Name

                match namespaces.TryGetValue(ns.Name) with
                | true, (entities, summary, substitutions) ->
                    namespaces.[ns.Name] <-
                        (entities @ ns.Entities, combineNamespaceDocs [ ns.NamespaceDocs; summary ], substitutions)
                | false, _ -> namespaces.Add(ns.Name, (ns.Entities, ns.NamespaceDocs, ns.Substitutions))

        let namespaces =
            [ for (KeyValue(name, (entities, summary, substitutions))) in namespaces do
                  printfn "  found %d entities in namespace %s..." entities.Length name

                  if entities.Length > 0 then
                      ApiDocNamespace(name, entities, substitutions, summary) ]

        printfn "  found %d namespaces..." namespaces.Length

        let collection =
            ApiDocCollection(collectionName, List.map fst assemblies, namespaces |> List.sortBy (fun ns -> ns.Name))

        let rec nestedModules ns parent (modul: ApiDocEntity) =
            [ yield ApiDocEntityInfo(modul, collection, ns, parent)
              for n in modul.NestedEntities do
                  if not n.IsTypeDefinition then
                      yield! nestedModules ns (Some modul) n ]

        let moduleInfos =
            [ for ns in collection.Namespaces do
                  for n in ns.Entities do
                      if not n.IsTypeDefinition then
                          yield! nestedModules ns None n ]

        let createType ns parent typ =
            ApiDocEntityInfo(typ, collection, ns, parent)

        let rec nestedTypes ns (modul: ApiDocEntity) =
            [ let entities = modul.NestedEntities

              for n in entities do
                  if n.IsTypeDefinition then
                      yield createType ns (Some modul) n

              for n in entities do
                  if not n.IsTypeDefinition then
                      yield! nestedTypes ns n ]

        let typesInfos =
            [ for ns in collection.Namespaces do
                  let entities = ns.Entities

                  for n in entities do
                      if not n.IsTypeDefinition then
                          yield! nestedTypes ns n

                  for n in entities do
                      if n.IsTypeDefinition then
                          yield createType ns None n ]

        ApiDocModel(
            substitutions = substitutions,
            collection = collection,
            entityInfos = moduleInfos @ typesInfos,
            root = root,
            qualify = qualify,
            fileExtensions = extensions,
            urlMap = urlMap
        )

/// Represents an entry suitable for constructing a Lunr index
type ApiDocsSearchIndexEntry =
    {
        uri: string
        title: string
        content: string
        headings: string list
        /// apiDocs or content
        ``type``: string
    }

[<Obsolete("Renamed to ApiDocMember", true)>]
type Member = class end

[<Obsolete("Renamed to ApiDocMemberKind", true)>]
type MemberKind = class end

[<Obsolete("Renamed to ApiDocAttribute", true)>]
type Attribute = class end

[<Obsolete("Renamed to ApiDocComment", true)>]
type DocComment = class end

[<Obsolete("Renamed to ApiDocEntity", true)>]
type Module = class end

[<Obsolete("Renamed to ApiDocEntityInfo", true)>]
type ModuleInfo = class end

[<Obsolete("Renamed to ApiDocEntity", true)>]
type Type = class end

[<Obsolete("Renamed to ApiDocEntity", true)>]
type ApiDocType = class end

[<Obsolete("Renamed to ApiDocTypeInfo", true)>]
type TypeInfo = class end
