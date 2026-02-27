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
        constraints: string list *
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

    let (ApiDocMemberDetails(usageHtml,
                             paramTypes,
                             returnType,
                             modifiers,
                             typars,
                             constraints,
                             extendedType,
                             location,
                             compiledName)) =
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

    /// The member's type constraints
    member x.Constraints: string list = constraints

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

    /// Formats type constraints as a 'when' clause
    member x.FormatTypeConstraints =
        if x.Constraints.IsEmpty then
            None
        else
            Some(String.concat " and " x.Constraints)

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
        substitutions: Substitutions,
        inheritedMembers: (ApiDocHtml * ApiDocMember list) list
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

    /// Members inherited from base types (grouped by base type, shown in docs as "Inherited from X")
    member x.InheritedMembers: (ApiDocHtml * ApiDocMember list) list = inheritedMembers

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


type ApiDocFileExtensions = { InFile: string; InUrl: string }
