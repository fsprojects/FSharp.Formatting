namespace rec FSharp.Formatting.ApiDocs

open System
open System.Reflection
open System.Collections.Generic
open System.Text
open System.IO
open System.Xml
open System.Xml.Linq

open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Range
open FSharp.Formatting.Common
open FSharp.Formatting.Internal
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Templating
open FSharp.Patterns

[<AutoOpen>]
module internal Utils =

    let (|AllAndLast|_|) (list:'T list)=
        if list.IsEmpty then None
        else let revd = List.rev list in Some(List.rev revd.Tail, revd.Head)

    let isAttrib<'T> (attrib: FSharpAttribute) =
        attrib.AttributeType.CompiledName = typeof<'T>.Name

    let hasAttrib<'T> (attribs: IList<FSharpAttribute>) =
        attribs |> Seq.exists (fun a -> isAttrib<'T>(a))

    let (|MeasureProd|_|) (typ : FSharpType) =
        if typ.HasTypeDefinition && typ.TypeDefinition.LogicalName = "*" && typ.GenericArguments.Count = 2 then Some (typ.GenericArguments.[0], typ.GenericArguments.[1])
        else None

    let (|MeasureInv|_|) (typ : FSharpType) =
        if typ.HasTypeDefinition && typ.TypeDefinition.LogicalName = "/" && typ.GenericArguments.Count = 1 then Some typ.GenericArguments.[0]
        else None

    let (|MeasureOne|_|) (typ : FSharpType) =
        if typ.HasTypeDefinition && typ.TypeDefinition.LogicalName = "1" && typ.GenericArguments.Count = 0 then  Some ()
        else None

    let tryGetLocation (symbol: FSharpSymbol) =
        match symbol.ImplementationLocation with
        | Some loc -> Some loc
        | None -> symbol.DeclarationLocation

    let isUnitType (ty: FSharpType) =
        ty.HasTypeDefinition && ty.TypeDefinition.XmlDocSig = "T:Microsoft.FSharp.Core.unit" 

    module List =
       let sepWith sep xs =
           match xs with
           | [] -> []
           | _ ->
               [ for x in xs do
                    yield sep
                    yield x]
               |> List.tail

    module Html =
        let sepWith s l = l |> List.sepWith (!! s) |> span []

/// Represents some HTML formatted by model generation
type ApiDocHtml(html: string) =

    /// Get the HTML text of the HTML section
    member x.HtmlText = html

/// Represents a documentation comment attached to source code
type ApiDocComment(summary, full, sects, rawData) =
    /// The summary for the comment
  member x.SummaryHtml : ApiDocHtml = summary

    /// The full html for the full description
  member x.DescriptionHtml : ApiDocHtml = full

    /// The parsed sections of the comment
  member x.Sections : KeyValuePair<string, ApiDocHtml> list = sects

    /// The raw data of the comment
  member x.RawData: KeyValuePair<string, string> list = rawData

  static member internal Empty = ApiDocComment(ApiDocHtml(""), ApiDocHtml(""), [], [])

/// Represents a custom attribute attached to source code
type ApiDocAttribute(name, fullName, constructorArguments, namedConstructorArguments) =
    /// The name of the attribute
  member x.Name : string = name

    /// The qualified name of the attribute
  member x.FullName : string = fullName

    /// The arguments to the constructor for the attribute
  member x.ConstructorArguments : obj list = constructorArguments

    /// The named arguments for the attribute
  member x.NamedConstructorArguments : (string*obj) list = namedConstructorArguments

  /// Gets a value indicating whether this attribute is System.ObsoleteAttribute
  member x.IsObsoleteAttribute =
    x.FullName = "System.ObsoleteAttribute"

  /// Gets a value indicating whether this attribute is RequireQualifiedAccessAttribute
  member x.IsRequireQualifiedAccessAttribute =
    x.FullName = typeof<RequireQualifiedAccessAttribute>.FullName

  /// Returns the obsolete message, when this attribute is the System.ObsoleteAttribute. When its not or no message was specified, an empty string is returned
  member x.ObsoleteMessage =
    let tryFindObsoleteMessage =
            x.ConstructorArguments
            |> Seq.tryFind (fun x -> x :? string)
            |> Option.map string
            |> Option.defaultValue ""
    if x.IsObsoleteAttribute then tryFindObsoleteMessage else ""

  /// Gets a value indicating whether this attribute the CustomOperationAttribute
  member x.IsCustomOperationAttribute =
    x.FullName = "Microsoft.FSharp.Core.CustomOperationAttribute"

  /// Returns the custom operation name, when this attribute is the CustomOperationAttribute. When its not an empty string is returned
  member x.CustomOperationName =
    let tryFindCustomOperation =
      x.ConstructorArguments
      |> Seq.tryFind (fun x -> x :? string)
      |> Option.map string
      |> Option.defaultValue ""
    if x.IsCustomOperationAttribute then tryFindCustomOperation else ""

  /// Formats the attribute with the given name
  member private x.Format(attributeName:string, removeAttributeSuffix:bool) =
        let dropSuffix (s:string) (t:string) = s.[0..s.Length - t.Length - 1]
        let attributeName = if removeAttributeSuffix && attributeName.EndsWith "Attribute" then dropSuffix attributeName "Attribute" else attributeName
        let join sep (items : string seq) = String.Join(sep, items)
        let inline append (s:string) (sb:StringBuilder) = sb.Append(s)
        let inline appendIfTrue p s sb =
            if p then append s sb
                else sb

        let rec formatValue (v:obj) =
            match v with
            | :? string as s -> sprintf "\"%s\"" s
            | :? array<obj> as a -> a |> Seq.map formatValue |> join "; " |> sprintf "[|%s|]"
            | :? bool as b -> if b then "true" else "false"
            | _ -> string v
        let formatedConstructorArguments =
            x.ConstructorArguments
            |> Seq.map formatValue
            |> join ", "

        let formatedNamedConstructorArguments =
            x.NamedConstructorArguments
            |> Seq.map (fun (n,v) -> sprintf "%s = %s" n (formatValue v))
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
        |> string

  /// Formats the attribute using the Name. Removes the "Attribute"-suffix. E.g Obsolete
  member x.Format() = x.Format(x.Name, true)

  /// Formats the attribute using the FullName. Removes the "Attribute"-suffix. E.g System.Obsolete
  member x.FormatFullName() = x.Format(x.FullName, true)

  /// Formats the attribute using the Name. Keeps the "Attribute"-suffix. E.g ObsoleteAttribute
  member x.FormatLongForm() = x.Format(x.Name, false)

  /// Formats the attribute using the FullName. Keeps the "Attribute"-suffix. E.g System.ObsoleteAttribute
  member x.FormatFullNameLongForm() = x.Format(x.FullName, false)

  /// Tries to find the System.ObsoleteAttribute and return its obsolete message
  static member internal TryGetObsoleteMessage(attributes:seq<ApiDocAttribute>)=
    attributes
    |> Seq.tryFind (fun a -> a.IsObsoleteAttribute)
    |> Option.map (fun a -> a.ObsoleteMessage)
    |> Option.defaultValue ""

  /// Tries to find the CustomOperationAttribute and return its obsolete message
  static member internal TryGetCustomOperationName(attributes:seq<ApiDocAttribute>)=
    attributes
    |> Seq.tryFind (fun a -> a.IsCustomOperationAttribute)
    |> Option.map (fun a -> a.CustomOperationName)
    |> Option.defaultValue ""


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

/// Represents an method, property, constructor, function or value, record field, union case or static parameter
/// integrated with its associated documentation. Includes extension members.
type ApiDocMember(displayName: string, attributes: ApiDocAttribute list, entityUrlBaseName: string,
         kind, cat, details, comment, symbol) =

  // The URL for a member is currently the #DisplayName on the enclosing entity
  let (usageHtml, paramHtmls, returnHtml, mods, typars, baseType, location, compiledName) = details

    /// The member's modifiers
  member x.Modifiers : string list = mods

    /// The member's type arguments
  member x.TypeArguments : string list = typars

    /// The usage section in a typical tooltip
  member x.UsageTooltip : ApiDocHtml = usageHtml

    /// The return section in a typical tooltip
  member x.ReturnTooltip : ApiDocHtml option = returnHtml

//    /// The full signature section in a typical tooltip
//  member x.SignatureTooltip : ApiDocHtml = signatureTooltip

    /// The member's parameter sections in a typical tooltip
  member x.ParameterTooltips: (string * ApiDocHtml) list = paramHtmls

    /// The member's source location, if any
  member x.SourceLocation : string option = location

    /// The type extended by an extension member, if any
  member x.ExtendedType : ApiDocHtml option =  baseType

  /// The member's compiled name, if any
  [<Obsolete("Remove '.Details'. 'ApiDocsMemberDetails' has been merged with 'ApiDocsMember'", true)>]
  member x.Details : int = 0

    /// The member's compiled name, if any
  member x.CompiledName : string option = compiledName

  /// Formats type arguments
  member x.FormatTypeArguments =
      // We suppress the display of ill-formatted type parameters for places
      // where these have not been explicitly declared.  This could likely be done
      // in a better way
      let res = String.concat ", " x.TypeArguments
      if x.TypeArguments.IsEmpty || res.Contains("?") then None else Some res

  /// Formats modifiers
  member x.FormatModifiers = String.concat " " x.Modifiers

  /// Formats source location
  member x.FormatSourceLocation = defaultArg x.SourceLocation ""

  /// Formats the compiled name
  member x.FormatCompiledName = defaultArg x.CompiledName ""

    /// Name of the member
  member x.Name = displayName

    /// The URL base name of the best link documentation for the item (without the http://site.io/reference)
  member x.UrlBaseName = entityUrlBaseName

    /// The URL of the best link documentation for the item relative to "reference" directory (without the http://site.io/reference)
  member x.Url(root, collectionName, qualify) =
      sprintf "%sreference/%s%s.html#%s" root (if qualify then collectionName else "") entityUrlBaseName displayName

    /// The declared attributes of the member
  member x.Attributes = attributes

    /// The category
  member x.Category : string = cat

    /// The kind of the member
  member x.Kind : ApiDocMemberKind = kind

    /// The attached comment
  member x.Comment : ApiDocComment = comment

    /// The symbol this member is related to
  member x.Symbol : FSharpSymbol = symbol

  /// Gets a value indicating whether this member is obsolete
  member x.IsObsolete =
    x.Attributes
    |> Seq.exists (fun a -> a.IsObsoleteAttribute)

  /// Returns the obsolete message, when this member is obsolete. When its not or no message was specified, an empty string is returned
  member x.ObsoleteMessage =
    ApiDocAttribute.TryGetObsoleteMessage(x.Attributes)

  member x.IsRequireQualifiedAccessAttribute =
    x.Attributes
    |> Seq.exists (fun a -> a.IsRequireQualifiedAccessAttribute)

  /// Gets a value indicating whether this member has CustomOperationAttribute
  member x.IsCustomOperation =
    x.Attributes
    |> Seq.exists (fun a -> a.IsCustomOperationAttribute)

  /// Returns the custom operation name, when this attribute is the CustomOperationAttribute. When its not an empty string is returned
  member x.CustomOperationName =
    ApiDocAttribute.TryGetCustomOperationName(x.Attributes)

/// Represents a type definition integrated with its associated documentation
type ApiDocEntity
       (tdef, name, cat, urlBaseName, comment, assembly: AssemblyName, attributes,
        cases, fields, statParams, ctors, inst, stat, allInterfaces, baseType, abbreviatedType,
        delegateSignature, symbol: FSharpEntity,
        nested, vals, exts, pats, rqa, parameters: Parameters) =

    /// Indicates if the entity is a type definition
    member x.IsTypeDefinition : bool = tdef

    /// The name of the entity
    member x.Name : string = name

    /// The category of the type
    member x.Category : string = cat

    /// The URL base name of the primary documentation for the entity  (without the http://site.io/reference)
    member x.UrlBaseName : string = urlBaseName

    /// The URL of the best link for the entity relative to "reference" directory (without the http://site.io/reference)
    member x.Url(root, collectionName, qualify) =
        sprintf "%sreference/%s%s.html" root (if qualify then collectionName else "") urlBaseName

    /// The name of the file generated for this entity
    member x.OutputFile(collectionName, qualify) =
        sprintf "reference/%s%s.html" (if qualify then collectionName else "") urlBaseName

    /// The attached comment
    member x.Comment : ApiDocComment = comment

    /// The name of the type's assembly
    member x.Assembly = assembly

    /// The declared attributes of the type
    member x.Attributes : ApiDocAttribute list = attributes

    /// The cases of a union type
    member x.UnionCases : ApiDocMember list = cases

    /// The fields of a record type
    member x.RecordFields : ApiDocMember list = fields

    /// Static parameters
    member x.StaticParameters : ApiDocMember list = statParams

    /// All members of the type
    member x.AllMembers : ApiDocMember list = List.concat [ ctors; inst; stat; cases; fields; statParams; vals; exts; pats  ]

    /// All interfaces of the type, formatted
    member x.AllInterfaces : ApiDocHtml list = allInterfaces

    /// The base type of the type, formatted
    member x.BaseType : ApiDocHtml option = baseType

    /// If this is a type abbreviation, then the abbreviated type
    member x.AbbreviatedType : ApiDocHtml option = abbreviatedType

    /// If this is a delegate, then e formatted signature
    member x.DelegateSignature : ApiDocHtml option = delegateSignature

    /// The constuctorsof the type
    member x.Constructors : ApiDocMember list = ctors

    /// The instance members of the type
    member x.InstanceMembers : ApiDocMember list = inst

    /// The static members of the type
    member x.StaticMembers : ApiDocMember list = stat

    /// Gets a value indicating whether this member is obsolete
    member x.IsObsolete =
        x.Attributes
        |> Seq.exists (fun a -> a.IsObsoleteAttribute)

    /// Returns the obsolete message, when this member is obsolete. When its not or no message was specified, an empty string is returned
    member x.ObsoleteMessage =
        ApiDocAttribute.TryGetObsoleteMessage(x.Attributes)

    /// The F# compiler symbol for the type definition
    member x.Symbol = symbol

    /// Does the module have the RequiresQualifiedAccess attribute
    member x.RequiresQualifiedAccess : bool = rqa

    /// All nested modules and types
    member x.NestedEntities : ApiDocEntity list = nested

    /// Values and functions of the module
    member x.ValuesAndFuncs : ApiDocMember list = vals

    /// Type extensions of the module
    member x.TypeExtensions : ApiDocMember list = exts

    /// Active patterns of the module
    member x.ActivePatterns : ApiDocMember list = pats

    /// The substitution parameters active for generating thist content
    member x.Parameters  = parameters


/// Represents a namespace integrated with its associated documentation
type ApiDocNamespace(name: string, mods, parameters: Parameters) =

    let urlBaseName = name.Replace(".", "-").ToLower()

    /// The name of the namespace
    member x.Name : string = name

    /// The hash label for the URL with the overall namespaces file
    member x.UrlHash = name.Replace(".", "-").ToLower()

    /// The base name for the generated file
    member x.UrlBaseName = urlBaseName

    /// The URL of the best link documentation for the item (without the http://site.io/reference)
    member x.Url(root, collectionName, qualify) =
        sprintf "%sreference/%s%s.html" root (if qualify then collectionName else "") urlBaseName

    /// The name of the file generated for this entity
    member x.OutputFile(collectionName, qualify) =
        sprintf "reference/%s%s.html" (if qualify then collectionName else "") urlBaseName

    /// All modules in the namespace
    member x.Entities : ApiDocEntity list = mods

    /// The substitution parameters active for generating thist content
    member x.Parameters  = parameters

/// Represents a group of assemblies integrated with its associated documentation
type ApiDocCollection(name: string, asms: AssemblyName list, nss: ApiDocNamespace list) =

    /// Name of the collection
    member x.CollectionName = name

    /// All assemblies in the collection
    member x.Assemblies = asms

    /// All namespaces in the collection
    member x.Namespaces = nss

/// High-level information about a module definition
type ApiDocEntityInfo(entity: ApiDocEntity, collection: ApiDocCollection, ns: ApiDocNamespace, parent: ApiDocEntity option) =
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
    let getXmlDocSigForType (typ:FSharpEntity) =
        match typ.XmlDocSig with
        | "" ->
            try
                defaultArg
                    (Option.map (sprintf "T:%s") typ.TryFullName)
                    ""
            with _ -> ""
        | n -> n

    let getMemberXmlDocsSigPrefix (memb:FSharpMemberOrFunctionOrValue) =
        if memb.IsEvent then "E"
        elif memb.IsProperty then "P"
        else "M"

    let getXmlDocSigForMember (memb:FSharpMemberOrFunctionOrValue) =
        match memb.XmlDocSig with
        | "" ->
            let memberName =
              try
                let name = memb.CompiledName.Replace(".ctor", "#ctor")
                let typeGenericParameters =
                    memb.DeclaringEntity.Value.GenericParameters |> Seq.mapi (fun num par -> par.Name, sprintf "`%d" num)

                let methodGenericParameters =
                    memb.GenericParameters |> Seq.mapi (fun num par -> par.Name, sprintf "``%d" num)

                let typeArgsMap =
                    Seq.append methodGenericParameters typeGenericParameters
                    |> Seq.groupBy fst
                    |> Seq.map (fun (_name, grp) -> grp |> Seq.head)
                    |> dict

                let typeArgs =
                    if memb.GenericParameters.Count > 0
                    then sprintf "``%d" memb.GenericParameters.Count
                    else ""

                let paramList =
                    if memb.CurriedParameterGroups.Count > 0 && memb.CurriedParameterGroups.[0].Count > 0 then
                        let head = memb.CurriedParameterGroups.[0]
                        let paramTypeList =
                            head
                            |> Seq.map (fun param ->
                                if param.Type.IsGenericParameter then
                                    typeArgsMap.[param.Type.GenericParameter.Name]
                                else
                                    param.Type.TypeDefinition.FullName)
                        "(" + System.String.Join(", ", paramTypeList) + ")"
                    else ""
                sprintf "%s%s%s" name typeArgs paramList
              with exn ->
                printfn "Error while building member-name for %s because: %s" memb.FullName exn.Message
                Log.verbf "Full Exception details of previous message: %O" exn
                memb.CompiledName
            match (memb.DeclaringEntity.Value.TryFullName) with
            | None    -> ""
            | Some(n)  -> sprintf "%s:%s.%s" (getMemberXmlDocsSigPrefix memb)  n memberName
        | n -> n

type internal CrefReference =
    { IsInternal : bool; ReferenceLink : string; NiceName : string; HasModuleSuffix: bool }

type internal CrossReferenceResolver (root) =
    let toReplace =
        ([("Microsoft.", ""); (".", "-"); ("`", "-"); ("<", "_"); (">", "_"); (" ", "_"); ("#", "_")] @
            (Path.GetInvalidPathChars()
            |> Seq.append (Path.GetInvalidFileNameChars())
            |> Seq.map (fun inv -> (inv.ToString(), "_")) |> Seq.toList))
        |> Seq.distinctBy fst
        |> Seq.toList
    let usedNames = Dictionary<_, _>()
    let registeredEntitiesToUrlBaseName = Dictionary<FSharpEntity, string>()
    let xmlDocNameToEntity = Dictionary<string, FSharpEntity>()
    let niceNameEntityLookup = Dictionary<_, _>()

    let nameGen (name:string) =
        let nice = (toReplace
                    |> Seq.fold (fun (s:string) (inv, repl) -> s.Replace(inv, repl)) name)
                    .ToLower()
        let found =
            seq { yield nice
                  for i in Seq.initInfinite id do yield sprintf "%s-%d" nice i }
            |> Seq.find (usedNames.ContainsKey >> not)
        usedNames.Add(found, true)
        found

    let rec registerEntity (entity: FSharpEntity) =
        let newName = nameGen (sprintf "%s.%s" entity.AccessPath entity.CompiledName)
        registeredEntitiesToUrlBaseName.[entity] <- newName
        let xmlsig = getXmlDocSigForType entity

        if (not (System.String.IsNullOrEmpty xmlsig)) then
            assert (xmlsig.StartsWith("T:"))
            xmlDocNameToEntity.[xmlsig.Substring(2)] <- entity
            if (not(niceNameEntityLookup.ContainsKey(entity.LogicalName))) then
                niceNameEntityLookup.[entity.LogicalName] <- System.Collections.Generic.List<_>()
            niceNameEntityLookup.[entity.LogicalName].Add(entity)

        for nested in entity.NestedEntities do
            registerEntity nested

    let getUrlBaseNameForRegisteredEntity (entity:FSharpEntity) =
        match registeredEntitiesToUrlBaseName.TryGetValue (entity) with
        | true, v -> v
        | _ -> failwithf "The entity %s was not registered before!" (sprintf "%s.%s" entity.AccessPath entity.CompiledName)

    let removeParen (memberName:string) =
        let firstParen = memberName.IndexOf("(")
        if firstParen > 0 then memberName.Substring(0, firstParen) else memberName

    let tryGetTypeFromMemberName (memberName : string) =
        let sub = removeParen memberName
        let lastPeriod = sub.LastIndexOf(".")
        if lastPeriod > 0 then
            Some (memberName.Substring(0, lastPeriod))
        else None

    let getMemberName keepParts hasModuleSuffix (memberNameNoParen:string) =
        let splits = memberNameNoParen.Split('.') |> Array.toList 
        let noNamespaceParts =
            if splits.Length > keepParts then
                splits.[splits.Length - keepParts ..]
            else splits
        let noNamespaceParts =
            if hasModuleSuffix then
                match noNamespaceParts with
                | h::t when h.EndsWith("Module") -> h.[0..h.Length-7] :: t
                | s  -> s
            else
                noNamespaceParts
        let res = String.concat "." noNamespaceParts
        let noGenerics =
            match res.Split('`') with
            | [| |] -> ""
            | [| s |] -> s
            | arr -> String.Join("`", arr.[0..arr.Length-2])
        noGenerics

    let dotnetDocsLink (memberName: string) =
        let noParen = removeParen memberName
        let docs = noParen.Replace("``", "").Replace("`", "-").ToLower()
        sprintf "https://docs.microsoft.com/dotnet/api/%s" docs

    let tryResolveCrossReferenceForEntity entity =
        match registeredEntitiesToUrlBaseName.TryGetValue (entity) with
        | true, _v -> 
            let urlBaseName = getUrlBaseNameForRegisteredEntity entity
            Some
              { IsInternal = true
                ReferenceLink = sprintf "%sreference/%s.html" root urlBaseName  
                NiceName = entity.LogicalName
                HasModuleSuffix=entity.HasFSharpModuleSuffix }
        | _ -> 
            match entity.TryFullName with
            | None -> None
            | Some nm ->
                let simple = entity.DisplayName
                Some
                  { IsInternal = false
                    ReferenceLink = dotnetDocsLink nm
                    NiceName = simple
                    HasModuleSuffix = false}

    let resolveCrossReferenceForTypeByName typeName =
        match xmlDocNameToEntity.TryGetValue(typeName) with
        | true, entity ->
            { IsInternal = true
              ReferenceLink = sprintf "%s.html" (getUrlBaseNameForRegisteredEntity entity)
              NiceName = entity.LogicalName
              HasModuleSuffix=entity.HasFSharpModuleSuffix }
        | _ ->
            match niceNameEntityLookup.TryGetValue(typeName) with
            | true, entities ->
                match Seq.toList entities with
                | entity :: _rest -> 
                    { IsInternal = true
                      ReferenceLink = sprintf "%s.html" (getUrlBaseNameForRegisteredEntity entity)
                      NiceName = entity.LogicalName
                      HasModuleSuffix=entity.HasFSharpModuleSuffix }
                | _ -> failwith "unreachable"
            | _ ->
                // A reference to something external, currently assumed to be in .NET
                let simple = getMemberName 1 false typeName
                { IsInternal = false
                  ReferenceLink = dotnetDocsLink typeName
                  NiceName = simple
                  HasModuleSuffix = false}

    member _.ResolveCref (cref:string) =
        if (cref.Length < 2) then invalidArg "cref" (sprintf "the given cref: '%s' is invalid!" cref)
        let memberName = cref.Substring(2)
        let noParen = removeParen memberName
        match cref with
        // Type
        | _ when cref.StartsWith("T:") ->
            let reference = resolveCrossReferenceForTypeByName memberName 
            // A reference to something in this component
            let simple = getMemberName 1 reference.HasModuleSuffix noParen
            Some { reference with NiceName = simple }

        // Compiler was unable to resolve!
        | _ when cref.StartsWith("!:")  ->
            Log.warnf "Compiler was unable to resolve %s" cref
            None

        // ApiDocMember
        | _ when cref.[1] = ':' ->
            match tryGetTypeFromMemberName memberName with
            | Some typeName ->
                let reference = resolveCrossReferenceForTypeByName typeName 
                // A reference to something in this component
                let simple = getMemberName 2 reference.HasModuleSuffix noParen
                Some { reference with NiceName = simple }
            | None ->
                Log.warnf "Assumed '%s' was a member but we cannot extract a type!" cref
                None
        // No idea
        | _ ->
            Log.warnf "Unresolved reference '%s'!" cref
            None

    member x.RegisterEntity entity = registerEntity entity
    member x.TryResolveUrlBaseNameForEntity entity = getUrlBaseNameForRegisteredEntity entity
    member x.TryResolveEntity entity = tryResolveCrossReferenceForEntity entity


[<AutoOpen>]
module internal TypeFormatter =

    type TypeFormatterParams = CrossReferenceResolver

    let convHtml (html: HtmlElement) = ApiDocHtml(html.ToString())

    /// We squeeze the spaces out of anything where whitespace layout must be exact - any deliberate
    /// whitespace must use &#32;
    ///
    /// This kind of sucks but stems from the fact the formatting for the internal HTML DSL is freely
    /// adding spaces which are actually significant when formatting F# type information.
    let codeHtml html =
        let html = code [] [html]
        ApiDocHtml(html.ToString().Replace(" ", "").Replace("\n","").Replace("\r","").Replace("<ahref", "<a href"))

    let formatSourceLocation (urlRangeHighlight : Uri -> int -> int -> string) (sourceFolderRepo : (string * string) option) (location : range option) =
        location |> Option.bind (fun location ->
            sourceFolderRepo |> Option.map (fun (sourceFolder, sourceRepo) ->
                let sourceFolderPath = Uri(Path.GetFullPath(sourceFolder)).ToString()
                let docPath = Uri(Path.GetFullPath(location.FileName)).ToString()

                // Even though ignoring case might be wrong, we do that because
                // one path might be file:///C:\... and the other file:///c:\...  :-(
                if not <| docPath.StartsWith(sourceFolderPath, StringComparison.InvariantCultureIgnoreCase) then
                    Log.verbf "Current source file '%s' doesn't reside in source folder '%s'" docPath sourceFolderPath
                    ""
                else
                    let relativePath = docPath.[sourceFolderPath.Length..]
                    let uriBuilder = UriBuilder(sourceRepo)
                    uriBuilder.Path <- uriBuilder.Path + relativePath
                    urlRangeHighlight uriBuilder.Uri location.StartLine location.EndLine ) )

    let formatTypeArgumentAsText (typar: FSharpGenericParameter) =
        (if typar.IsSolveAtCompileTime then "^" else "'") + typar.Name

    let formatTypeArgumentsAsText (typars: FSharpGenericParameter list) =
        List.map formatTypeArgumentAsText typars 

    let bracketHtml (str:HtmlElement) =
        span [] [!! "("; str; !! ")"] 

    let bracketNonAtomicHtml (str:HtmlElement) =
        if str.ToString().Contains("&#32;") then bracketHtml str else str

    let bracketHtmlIf cond str =
        if cond then bracketHtml str else str

    let formatTyconRefAsHtml (ctx:TypeFormatterParams) (tcref:FSharpEntity) =
        let core = !! tcref.DisplayName.Replace(" ", "&#32;")
        match ctx.TryResolveEntity tcref with
        | None -> core
        | Some url -> a [Href url.ReferenceLink] [core]

    let rec formatTypeApplicationAsHtml ctx (tcref: FSharpEntity) typeName prec prefix args : HtmlElement =
        if prefix then
            match args with
            | [] -> typeName
            | [arg] -> span [] [typeName; !!"&lt;"; (formatTypeWithPrecAsHtml ctx 4 arg); !!  "&gt;" ]
            | args -> bracketHtmlIf (prec <= 1) (span [] [typeName; !! "&lt;"; formatTypesWithPrecAsHtml ctx 2 ",&#32;" args; !! "&gt;"])
        else
            match args with
            | [] -> typeName
            | [arg] ->
                if tcref.DisplayName.StartsWith "[" then 
                   span [] [formatTypeWithPrecAsHtml ctx 2 arg; !! tcref.DisplayName ]
                else
                   span [] [formatTypeWithPrecAsHtml ctx 2 arg; !! "&#32;"; typeName]
            | args ->
                 bracketHtmlIf (prec <= 1) (span [] [bracketNonAtomicHtml (formatTypesWithPrecAsHtml ctx 2 ",&#32;" args); typeName])

    and formatTypesWithPrecAsHtml ctx prec sep typs =
        typs |> List.map (formatTypeWithPrecAsHtml ctx prec) |> Html.sepWith sep

    and formatTypeWithPrecAsHtml ctx prec (typ:FSharpType) =
        // Measure types are stored as named types with 'fake' constructors for products, "1" and inverses
        // of measures in a normalized form (see Andrew Kennedy technical reports). Here we detect this
        // embedding and use an approximate set of rules for layout out normalized measures in a nice way.
        match typ with
        | MeasureProd (ty,MeasureOne)
        | MeasureProd (MeasureOne, ty) -> formatTypeWithPrecAsHtml ctx prec ty
        | MeasureProd (ty1, MeasureInv ty2)
        | MeasureProd (ty1, MeasureProd (MeasureInv ty2, MeasureOne)) ->
            span [] [formatTypeWithPrecAsHtml ctx 2 ty1; !! "/"; formatTypeWithPrecAsHtml ctx 2 ty2 ]
        | MeasureProd (ty1,MeasureProd(ty2,MeasureOne))
        | MeasureProd (ty1,ty2) ->
            span [] [formatTypeWithPrecAsHtml ctx 2 ty1; !! "*"; formatTypeWithPrecAsHtml ctx 2 ty2 ]
        | MeasureInv ty ->
            span [] [!! "/"; formatTypeWithPrecAsHtml ctx 1 ty ]
        | MeasureOne  ->
            !! "1"
        | _ when typ.HasTypeDefinition ->
            let tcref = typ.TypeDefinition
            let tyargs = typ.GenericArguments |> Seq.toList
            // layout postfix array types
            formatTypeApplicationAsHtml ctx tcref (formatTyconRefAsHtml ctx tcref) prec tcref.UsesPrefixDisplay tyargs
        | _ when typ.IsTupleType ->
            let tyargs = typ.GenericArguments |> Seq.toList
            bracketHtmlIf (prec <= 2) (formatTypesWithPrecAsHtml ctx 2 "&#32;*&#32;" tyargs)
        | _ when typ.IsFunctionType ->
            let rec loop soFar (typ:FSharpType) =
              if typ.IsFunctionType then
                let domainTyp, retType = typ.GenericArguments.[0], typ.GenericArguments.[1]
                loop (soFar @ [ formatTypeWithPrecAsHtml ctx 4 domainTyp; !! "&#32;->&#32;"]) retType
              else
                span [] (soFar @ [ formatTypeWithPrecAsHtml ctx 5 typ])
            bracketHtmlIf (prec <= 4) (loop [] typ)
        | _ when typ.IsGenericParameter ->
            !! (formatTypeArgumentAsText typ.GenericParameter)
        | _ -> !! "(type)"

    let formatTypeAsHtml ctx (typ: FSharpType) =
        formatTypeWithPrecAsHtml ctx 5 typ

    let formatArgNameAndTypePair i (argName, argType) =
        let argName =
          match argName with
          | None ->
              if isUnitType argType then "()"
              else "arg" + string i
          | Some nm -> nm
        argName, argType

    let formatArgNameAndType i (arg:FSharpParameter) =
        let argName, argType  =  formatArgNameAndTypePair i (arg.Name, arg.Type)
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
    let formatArgUsageAsHtml _ctx i (arg:FSharpParameter) =
        let argName, _argType = formatArgNameAndType i arg
        !! argName

    let formatArgNameAndTypePairUsageAsHtml ctx (argName0, argType) =
        span [] [ !! (match argName0 with None -> "" | Some argName -> argName + ":&#32;");
                  formatTypeWithPrecAsHtml ctx 2 argType ]

    let formatCurriedArgsUsageAsHtml preferNoParens isItemIndexer curriedArgs =
        let counter = let n = ref 0 in fun () -> incr n; !n
        curriedArgs
        |> List.map (List.map (fun x -> formatArgNameAndType (counter()) x |> fst))
        |> List.map (fun argTuple ->
            match argTuple with
            | [] -> !! "()"
            | [argName] when argName = "()" -> !! "()"
            | [argName] when preferNoParens -> !!argName
            | args ->
                let argText = args |> List.map (!!) |> Html.sepWith ",&#32;"
                if isItemIndexer then
                    argText
                else
                    bracketHtml argText)
        |> Html.sepWith "&#32;"

    let formatDelegateSignatureAsHtml ctx nm (typ: FSharpDelegateSignature) =
        let args =
            typ.DelegateArguments
            |> List.ofSeq
            |> List.map (formatArgNameAndTypePairUsageAsHtml ctx)
            |> Html.sepWith "&#32;*&#32;"
        span [] ([!! nm; !!"("; args; !! "&#32;->&#32;"; formatTypeAsHtml ctx typ.DelegateReturnType; !!")"  ])

[<AutoOpen>]
module internal SymbolReader =
    type ReadingContext =
        { PublicOnly : bool
          Assembly : AssemblyName
          XmlMemberMap : IDictionary<string, XElement>
          UrlMap : CrossReferenceResolver
          MarkdownComments : bool
          UrlRangeHighlight : Uri -> int -> int -> string
          SourceFolderRepository : (string * string) option
          AssemblyPath : string
          CompilerOptions : string
          FormatAgent : CodeFormatAgent
          Parameters : Parameters }

        member x.XmlMemberLookup(key) =
            match x.XmlMemberMap.TryGetValue(key) with
            | true, v -> Some v
            | _ -> None

        static member internal Create
            (publicOnly, assembly, map, sourceFolderRepo, urlRangeHighlight, mdcomments, urlMap,
             assemblyPath, fscoptions, formatAgent, parameters ) =

          { PublicOnly=publicOnly
            Assembly = assembly
            XmlMemberMap = map
            MarkdownComments = mdcomments
            UrlMap = urlMap
            UrlRangeHighlight = urlRangeHighlight
            SourceFolderRepository = sourceFolderRepo
            AssemblyPath = assemblyPath
            CompilerOptions = fscoptions
            FormatAgent = formatAgent
            Parameters = parameters}

    let inline private getCompiledName (s : ^a when ^a :> FSharpSymbol) =
        let compiledName = (^a : (member CompiledName : string) (s))
        match compiledName = s.DisplayName with
        | true -> None
        | _    -> Some compiledName

    let readAttribute (attribute: FSharpAttribute) =
        let name = attribute.AttributeType.DisplayName
        let fullName = attribute.AttributeType.FullName
        let constructorArguments = attribute.ConstructorArguments |> Seq.map snd |> Seq.toList
        let namedArguments = attribute.NamedArguments |> Seq.map (fun (_,name,_,value) -> (name, value))  |> Seq.toList
        ApiDocAttribute(name, fullName, constructorArguments, namedArguments)

    let readAttributes (attributes:seq<FSharpAttribute>) =
        attributes
        |> Seq.map readAttribute
        |> Seq.toList

    let readMemberOrVal (ctx:ReadingContext) (v:FSharpMemberOrFunctionOrValue) =
        let requireQualifiedAccess =
            hasAttrib<RequireQualifiedAccessAttribute> v.ApparentEnclosingEntity.Attributes

        // This module doesn't have RequireQualifiedAccessAttribute and anyway we want the name to show
        // usage of its members as Array.Parallel.map
        let specialCase1 =
            v.ApparentEnclosingEntity.TryFullName = Some "Microsoft.FSharp.Collections.ArrayModule.Parallel"

        let argInfos, retInfo = FSharpType.Prettify(v.CurriedParameterGroups, v.ReturnParameter)
        let argInfos = argInfos |> Seq.map Seq.toList |> Seq.toList

        let isItemIndexer = (v.IsInstanceMember && v.DisplayName = "Item")
        let preferNoParens = isItemIndexer || not v.IsMember || Char.IsLower(v.DisplayName.[0])
        let fullArgUsage =
          match argInfos with
          | [[]] when (v.IsProperty && v.HasGetterMethod) -> !! ""
          | _  -> formatCurriedArgsUsageAsHtml preferNoParens isItemIndexer argInfos

        let usageHtml =

          match v.IsMember, v.IsInstanceMember, v.LogicalName, v.DisplayName with
          // Constructors 
          | _, _, ".ctor", _ -> span []  [!!v.ApparentEnclosingEntity.DisplayName; fullArgUsage ]
          // Indexers
          | _, true, _, "Item" -> span [] [!! "this.["; fullArgUsage; !! "]"]
          // Ordinary instance members
          | _, true, _, name -> span [] [!! "this."; !! name; fullArgUsage ]
          // A hack for Array.Parallel.map in FSharp.Core
          | _, false, _, name when specialCase1 -> span [] [!! ("Array.Parallel." + name); fullArgUsage]
          // Ordinary functions or values
          | false, _, _, name when not requireQualifiedAccess -> span [] [!!name; fullArgUsage]
          // Ordinary static members or things (?) that require fully qualified access
          | _, false, _, name ->
              span [] [!!(v.ApparentEnclosingEntity.DisplayName + "." + name)
                       if preferNoParens then !! "&#32;"
                       fullArgUsage]

        let usageHtml = codeHtml usageHtml

        let modifiers =
          [ // TODO: v.Accessibility does not contain anything
            if v.InlineAnnotation = FSharpInlineAnnotation.AlwaysInline then yield "inline"
            if v.IsDispatchSlot then yield "abstract" ]

        let retType = retInfo.Type
        let argInfos, retType =
            match argInfos, v.HasGetterMethod, v.HasSetterMethod with
            | [ AllAndLast(args, last) ], _, true -> [ args ], Some last.Type
            | _, _, true -> argInfos, None
            | [[]], true, _ -> [], Some retType
            | _, _, _ -> argInfos, Some retType

        let paramHtmls =
            argInfos
            |> List.concat
            |> List.mapi (fun i p ->
                let nm, ty = formatArgNameAndType i p
                let html = formatTypeAsHtml ctx.UrlMap ty |> codeHtml
                nm, html )
           
        // Extension members can have apparent parents which are not F# types.
        // Hence getting the generic argument count if this is a little trickier
        let numGenericParamsOfApparentParent =
            let pty = v.ApparentEnclosingEntity
            pty.GenericParameters.Count

        // Ensure that there is enough number of elements to skip
        let tps = v.GenericParameters |> Seq.toList |> List.skip (min v.GenericParameters.Count numGenericParamsOfApparentParent)
        let typars = formatTypeArgumentsAsText tps

        //let cxs  = indexedConstraints v.GenericParameters
        let retTypeHtml = retType |> Option.map (formatTypeAsHtml ctx.UrlMap >> codeHtml)

        let returnHtml =
           match retType with
           | None -> None
           | Some retType -> if isUnitType retType then None else retTypeHtml

        //let signatureTooltip =
        //  match argInfos with
        //  | [] -> retTypeText
        //  | [[x]] when (v.IsPropertyGetterMethod || v.HasGetterMethod) && x.Name.IsNone && isUnitType x.Type -> retTypeText
        //  | _  -> (formatArgsUsageAsText true v argInfos) + " -> " + retTypeText

        let baseType =
            if v.IsExtensionMember then
                try Some (formatTyconRefAsHtml ctx.UrlMap v.ApparentEnclosingEntity |> codeHtml) with _ -> None
            else
                None

        // If there is a signature file, we should go for implementation file
        let loc = tryGetLocation v
        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
        (usageHtml, paramHtmls, returnHtml, modifiers, typars, baseType, location, getCompiledName v)

    let readUnionCase (ctx:ReadingContext) (_typ: FSharpEntity) (case:FSharpUnionCase) =

        let formatFieldUsage (field:FSharpField) =
            if field.Name.StartsWith("Item") then
                formatTypeAsHtml ctx.UrlMap field.FieldType
            else
                !! field.Name

        let fields = case.UnionCaseFields |> List.ofSeq

        let nm = if case.Name = "op_ColonColon" then "::" elif case.Name = "op_Nil" then "[]" else case.Name
        let usageHtml =
            let fieldsHtmls = fields |> List.map formatFieldUsage 
            if case.Name = "op_ColonColon" then
                span [] [fieldsHtmls.[0]; !!"&#32;"; !! nm; fieldsHtmls.[1] ] 
                |> codeHtml
            else
                let fieldHtml = fieldsHtmls |> Html.sepWith ",&#32;"
                span [] [!! nm; !! "("; fieldHtml;  !! ")" ]
                |> codeHtml

        let paramHtmls =
            fields |> List.map (fun p ->
                let nm = p.Name
                let html = formatTypeAsHtml ctx.UrlMap p.FieldType  |> codeHtml
                nm, html)

        let returnHtml = None
           //if isUnitType retType then None else Some retTypeText 

        let modifiers = List.empty
        let typeParams = List.empty

        //let signatureTooltip =
        //   match fields with
        //   | [] -> retTypeText
        //   | _ -> (fields |> List.map (fun field -> formatTypeAsText field.FieldType) |> String.concat " * ") + " -> " + retTypeText
        let loc = tryGetLocation case
        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
        (usageHtml, paramHtmls, returnHtml, modifiers, typeParams, None, location, getCompiledName case)

    let readFSharpField (ctx:ReadingContext) (field:FSharpField) =
        let usageHtml = !!field.Name |> codeHtml
        let modifiers =
          [ if field.IsMutable then yield "mutable"
            if field.IsStatic then yield "static" ]
        let typeParams = List.empty
        //let signatureTooltip = formatTypeAsText field.FieldType
        let paramHtmls = []

        let returnHtml = None
           //if isUnitType retType then None else Some retTypeText 
        let loc = tryGetLocation field
        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
        (usageHtml, paramHtmls, returnHtml, modifiers, typeParams, None, location, if field.Name <> field.DisplayName then Some field.Name else None)

    let getFSharpStaticParamXmlSig (typeProvider:FSharpEntity) parameterName =
      "SP:" + typeProvider.AccessPath + "." + typeProvider.LogicalName + "." + parameterName

    let readFSharpStaticParam (ctx:ReadingContext) (staticParam:FSharpStaticParameter) =
        let usageHtml =
            span [] [ !!staticParam.Name; !! ":&#32;"; formatTypeAsHtml ctx.UrlMap staticParam.Kind;
                      !!(if staticParam.IsOptional then sprintf " (optional, default = %A)" staticParam.DefaultValue else "") ]
            |> codeHtml
        let modifiers = List.empty
        let typeParams = List.empty
        let paramHtmls = []
        let returnHtml = None
        //let signatureTooltip = formatTypeAsText staticParam.Kind + (if staticParam.IsOptional then sprintf " (optional, default = %A)" staticParam.DefaultValue else "")
        
        let loc = tryGetLocation staticParam
        let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
        (usageHtml, paramHtmls, returnHtml, modifiers, typeParams, None, location, if staticParam.Name <> staticParam.DisplayName then Some staticParam.Name else None)

    let removeSpaces (comment:string) =
        use reader = new StringReader(comment)
        let lines =
          [ let line = ref ""
            while (line := reader.ReadLine(); line.Value <> null) do
              yield line.Value ]
        String.removeSpaces lines

    let readMarkdownCommentAsHtml (doc:LiterateDocument) =
        let groups = System.Collections.Generic.Dictionary<_, _>()
        let mutable current = "<default>"
        groups.Add(current, [])
        let raw =
            match doc.Source with
            | LiterateSource.Markdown (string) -> [KeyValuePair(current, string)]
            | LiterateSource.Script _ -> []

        for par in doc.Paragraphs do
          match par with
          | Heading(2, [Literal(text, _)], _) ->
              current <- text.Trim()
              groups.Add(current, [par])
          | par ->
              groups.[current] <- par::groups.[current]
        let summary = ApiDocHtml(Literate.ToHtml(doc.With(List.rev groups.["<default>"])))
        let full = ApiDocHtml(Literate.ToHtml(doc))

        let sections =
          [ for (KeyValue(k, v)) in groups ->
              let body = if k = "<default>" then List.rev v else List.tail (List.rev v)
              let html = ApiDocHtml(Literate.ToHtml(doc.With(body)))
              KeyValuePair(k, html) ]

        ApiDocComment(summary, full, sections, raw)

    let findCommand = (function
        | StringPosition.StartsWithWrapped ("[", "]") (ParseCommand(k, v), _rest) ->
            Some (k, v)
        | _ -> None)

    let readXmlCommentAsHtmlAux all (urlMap: CrossReferenceResolver) (doc: XElement) (cmds: IDictionary<_, _>)=
      let rawData = new Dictionary<string, string>()
      let html = new StringBuilder()
      let rec readElement (e : XElement) =
          for x in e.Nodes() do
              if x.NodeType = XmlNodeType.Text then
                  let text = (x :?> XText).Value
                  match findCommand (text, MarkdownRange.zero) with
                  | Some (k,v) -> cmds.Add(k,v)
                  | None -> html.Append(text) |> ignore
              elif x.NodeType = XmlNodeType.Element then
                  let elem = x :?> XElement
                  match elem.Name.LocalName with
                  | "list" ->
                      html.Append("<ul>") |> ignore
                      readElement elem
                      html.Append("</ul>") |> ignore
                  | "item" ->
                      html.Append("<li>") |> ignore
                      readElement elem
                      html.Append("</li>") |> ignore
                  | "para" ->
                      html.Append("<p class='para'>") |> ignore
                      readElement elem
                      html.Append("</p>") |> ignore
                  | "see"
                  | "seealso" ->
                      let cref = elem.Attribute(XName.Get "cref")
                      if cref <> null then
                         if System.String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
                           failwithf "Invalid cref specified in: %A" doc
 
                         // FSharp.Core cref listings don't start with "T:", see https://github.com/dotnet/fsharp/issues/9805
                         let cname = cref.Value
                         let cname = if cname.Contains(":") then cname else "T:"+cname 
                            
                         match urlMap.ResolveCref cname with
                         | Some reference ->
                            html.AppendFormat("<a href=\"{0}\">{1}</a>", reference.ReferenceLink, reference.NiceName) |> ignore
                         | _ ->
                            urlMap.ResolveCref cname |> ignore
                            html.AppendFormat("{0}", cref.Value) |> ignore
                  | "c" ->
                     html.Append("<code>") |> ignore
                     html.Append(elem.Value) |> ignore
                     html.Append("</code>") |> ignore
                  | "code" ->
                     html.Append("<pre>") |> ignore
                     html.Append(elem.Value) |> ignore
                     html.Append("</pre>") |> ignore
                  | _ ->
                    ()
      readElement doc
   //full.Append("</br>") |> ignore

      let summaries = doc.Descendants(XName.Get "summary")
      summaries |> Seq.iteri (fun id e ->
         let n = if id = 0 then "summary" else "summary-" + string id
         rawData.[n] <- e.Value
         html.Append("<p class='summary'>") |> ignore
         readElement e
         html.Append("</p>") |> ignore
      )

      if all then
          let remarkNodes = doc.Descendants(XName.Get "remarks")
          if Seq.length remarkNodes > 0 then
             //html.Append("<h4>Remarks</h4>") |> ignore
             remarkNodes |> Seq.iteri (fun id e ->
               let n = if id = 0 then "remarks" else "remarks-" + string id
               rawData.[n] <- e.Value
               html.Append("<p class='remarks'>") |> ignore
               readElement e
               html.Append("</p>") |> ignore
             )

          let paramNodes = doc.Descendants(XName.Get "param")
          if Seq.length paramNodes > 0 then
             //full.Append("<h4>Parameters</h4>") |> ignore
             html.Append("<dl>") |> ignore
             for e in paramNodes do
               let name = e.Attribute(XName.Get "name").Value
               rawData.["param-" + name] <- e.Value
               html.AppendFormat("<dt><span class='parameter'>{0}</span></dt><dd><p>", name) |> ignore
               readElement e
               html.AppendFormat("</p></dd>") |> ignore
             html.Append("</dl>") |> ignore

          let returns = doc.Descendants(XName.Get "returns")
          returns |> Seq.iteri (fun id e ->
             let n = if id = 0 then "returns" else "returns-" + string id

             html.Append("<p class='returns'>") |> ignore
             rawData.[n] <- e.Value
             html.AppendFormat("<p>Returns: ") |> ignore
             readElement e
             html.Append("</p>") |> ignore
           )

          let exceptions = doc.Descendants(XName.Get "exception")
          if Seq.length exceptions > 0 then
             //html.Append("<p>Exceptions:</p>") |> ignore
             html.Append("<table class='inner-list exception-list'>") |> ignore
             for e in exceptions do
                 let cref = e.Attribute(XName.Get "cref")
                 if cref <> null then
                     if String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
                         failwithf "Invalid cref specified in: %A" doc

                     // FSharp.Core cref listings don't start with "T:", see https://github.com/dotnet/fsharp/issues/9805
                     let cname = cref.Value
                     let cname = if cname.StartsWith("T:") then cname else "T:"+cname // FSharp.Core exception listings don't start with "T:"

                     match urlMap.ResolveCref cname with
                     | Some reference ->
                         rawData.["exception-" + reference.NiceName] <- reference.ReferenceLink
                         html.AppendFormat("<tr><td><a href=\"{0}\">{1}</a></td><td>", reference.ReferenceLink, reference.NiceName) |> ignore
                         readElement e
                         html.AppendFormat("</td></tr>") |> ignore
                     | _ ->
                         html.AppendFormat("<tr><td>UNRESOLVED({0})</td><td></td></tr>", cref.Value) |> ignore
             html.Append("</table>") |> ignore


      doc.Descendants ()
       |> Seq.filter (fun n ->
            let ln = n.Name.LocalName
            ln <> "summary" &&
            ln <> "param" &&
            ln <> "exceptions" &&
            ln <> "returns" &&
            ln <> "remarks" )
       |> Seq.groupBy (fun n -> n.Name.LocalName)
       |> Seq.iter (fun (n, lst) ->
            let lst = Seq.toList lst
            match lst with
            | [x] -> rawData.[n] <- x.Value
            | lst ->
                lst |> Seq.iteri (fun id el ->
                    rawData.[n + "-" + string id] <- el.Value)
            ()
       )

      let str = ApiDocHtml(html.ToString())
      str, rawData

    let readXmlCommentAsHtml (urlMap : CrossReferenceResolver) (doc : XElement) (cmds: IDictionary<_, _>) =
        let summary, _raw = readXmlCommentAsHtmlAux false urlMap doc cmds
        let full, rawData = readXmlCommentAsHtmlAux true urlMap doc cmds
        let raw = rawData |> Seq.toList
        ApiDocComment(summary, full, [KeyValuePair("<default>", full)], raw)

    /// Returns all indirect links in a specified span node
    let rec collectSpanIndirectLinks span =
        seq {
            match span with
            | IndirectLink (_, _, key, _) -> yield key
            | MarkdownPatterns.SpanLeaf _ -> ()
            | MarkdownPatterns.SpanNode(_, spans) ->
              for s in spans do yield! collectSpanIndirectLinks s
        }

    /// Returns all indirect links in the specified paragraph node
    let rec collectParagraphIndirectLinks par = seq {
        match par with
        | MarkdownPatterns.ParagraphLeaf _ -> ()
        | MarkdownPatterns.ParagraphNested(_, pars) ->
          for ps in pars do
            for p in ps do yield! collectParagraphIndirectLinks p
        | MarkdownPatterns.ParagraphSpans(_, spans) ->
          for s in spans do yield! collectSpanIndirectLinks s }

    /// Returns whether the link is not included in the document defined links
    let linkNotDefined (doc: LiterateDocument) (link:string) =
        [ link; link.Replace("\r\n", ""); link.Replace("\r\n", " ");
          link.Replace("\n", ""); link.Replace("\n", " ") ]
        |> Seq.map (fun key -> not(doc.DefinedLinks.ContainsKey(key)) )
        |> Seq.reduce (fun a c -> a && c)

    /// Returns a tuple of the undefined link and its Cref if it exists
    let getTypeLink (ctx:ReadingContext) undefinedLink =
        // Append 'T:' to try to get the link from urlmap
        match ctx.UrlMap.ResolveCref ("T:" + undefinedLink) with
        | Some cRef -> if cRef.IsInternal then Some (undefinedLink, cRef) else None
        | None -> None

    /// Adds a cross-type link to the document defined links
    let addLinkToType (doc: LiterateDocument) link =
        match link with
        | Some(k, v) -> do doc.DefinedLinks.Add(k, (v.ReferenceLink, Some v.NiceName))
        | None -> ()

    /// Wraps the span inside an `IndirectLink` if it is an inline code that can be converted to a link
    let wrapInlineCodeLinksInSpans (ctx:ReadingContext) span =
        match span with
        | InlineCode(code, r) ->
            match getTypeLink ctx code with
            | Some _ -> IndirectLink([span], code, code, r)
            | None -> span
        | _ -> span

    /// Wraps inside an `IndirectLink` all inline code spans in the paragraph that can be converted to a link
    let rec wrapInlineCodeLinksInParagraphs (ctx:ReadingContext) (para:MarkdownParagraph) =
        match para with
        | MarkdownPatterns.ParagraphLeaf _ -> para
        | MarkdownPatterns.ParagraphNested(info, pars) ->
            MarkdownPatterns.ParagraphNested(info, pars |> List.map (fun innerPars -> List.map (wrapInlineCodeLinksInParagraphs ctx) innerPars))
        | MarkdownPatterns.ParagraphSpans(info, spans) -> MarkdownPatterns.ParagraphSpans(info, List.map (wrapInlineCodeLinksInSpans ctx) spans)

    /// Adds the missing links to types to the document defined links
    let addMissingLinkToTypes ctx (doc: LiterateDocument) =
        let replacedParagraphs = doc.Paragraphs |> List.map (wrapInlineCodeLinksInParagraphs ctx)

        do replacedParagraphs
        |> Seq.collect collectParagraphIndirectLinks
        |> Seq.filter (linkNotDefined doc)
        |> Seq.map (getTypeLink ctx)
        |> Seq.iter (addLinkToType doc)

        doc.With(paragraphs=replacedParagraphs)

    let readCommentAndCommands (ctx:ReadingContext) xmlSig =
        match ctx.XmlMemberLookup(xmlSig) with
        | None ->
            if not (System.String.IsNullOrEmpty xmlSig) then
                Log.verbf "Could not find documentation for '%s'! (You can ignore this message when you have not written documentation for this member)" xmlSig
            dict[], ApiDocComment.Empty
        | Some el ->
            let sum = el.Element(XName.Get "summary")
            match sum with
            | null when String.IsNullOrEmpty el.Value ->
              dict[], ApiDocComment.Empty
            | null ->
              let summary, _raw = readXmlCommentAsHtmlAux false ctx.UrlMap el (dict [])
              let all, _raw = readXmlCommentAsHtmlAux true ctx.UrlMap el (dict [])
              dict[], (ApiDocComment(summary, all, [KeyValuePair("<default>", summary)], []))
            | sum ->
              let lines = removeSpaces sum.Value |> List.map (fun s -> (s, MarkdownRange.zero))
              let cmds = Dictionary<string, string>()

              if ctx.MarkdownComments then
                let text =
                  lines
                  |> List.filter (findCommand >> (function
                      | Some (k, v) ->
                          cmds.Add(k, v)
                          false
                      | _ -> true))
                  |> List.map fst
                  |> String.concat "\n"

                let doc =
                    Literate.ParseMarkdownString
                      ( text, path=Path.Combine(ctx.AssemblyPath, "docs.fsx"),
                        formatAgent=ctx.FormatAgent, fscoptions=ctx.CompilerOptions )

                let doc = doc |> addMissingLinkToTypes ctx
                let html = readMarkdownCommentAsHtml doc
                cmds :> IDictionary<_, _>, html

              else
                let html = readXmlCommentAsHtml ctx.UrlMap el cmds
                lines
                  |> Seq.choose findCommand
                  |> Seq.iter (fun (k, v) -> cmds.Add(k,v))
                cmds :> IDictionary<_, _>, html


    /// Reads XML documentation comments and calls the specified function
    /// to parse the rest of the entity, unless [omit] command is set.
    /// The function is called with category name, commands & comment.
    let readCommentsInto (sym:FSharpSymbol) ctx xmlDocSig f =
        let cmds, comment = readCommentAndCommands ctx xmlDocSig
        match cmds with
        | Command "omit" _ -> None
        | Command "category" cat
        | Let "" (cat, _) ->
          try
            Some(f cat cmds comment)
          with e ->
            let name =
              try sym.FullName
              with _ ->
                try sym.DisplayName
                with _ ->
                  let part =
                    try
                      let ass = sym.Assembly
                      match ass.FileName with
                      | Some file -> file
                      | None -> ass.QualifiedName
                    with _ -> "unknown"
                  sprintf "unknown, part of %s" part
            printfn "Could not read comments from entity '%s': %O" name e
            None

    let checkAccess ctx (access: FSharpAccessibility) =
       not ctx.PublicOnly || access.IsPublic

    let readChildren ctx (entities:seq<FSharpEntity>) reader cond =
        entities
        |> Seq.filter (fun v -> checkAccess ctx v.Accessibility)
        |> Seq.filter cond
        |> Seq.sortBy (fun (c:FSharpEntity) -> c.DisplayName)
        |> Seq.choose (reader ctx)
        |> List.ofSeq

    let tryReadMember (ctx:ReadingContext) entityUrl kind (memb:FSharpMemberOrFunctionOrValue) =
      readCommentsInto memb ctx (getXmlDocSigForMember memb) (fun cat _ comment ->
        let details = readMemberOrVal ctx memb
        ApiDocMember(memb.DisplayName, readAttributes memb.Attributes, entityUrl, kind, cat, details, comment, memb))

    let readAllMembers ctx entityUrl kind (members:seq<FSharpMemberOrFunctionOrValue>) =
      members
      |> Seq.filter (fun v -> checkAccess ctx v.Accessibility)
      |> Seq.filter (fun v ->
          not v.IsCompilerGenerated &&
          not v.IsPropertyGetterMethod && not v.IsPropertySetterMethod &&
          not v.IsEventAddMethod && not v.IsEventRemoveMethod)
      |> Seq.choose (tryReadMember ctx entityUrl kind) |> List.ofSeq

    let readMembers ctx entityUrl kind (entity:FSharpEntity) cond =
      entity.MembersFunctionsAndValues
      |> Seq.filter (fun v -> checkAccess ctx v.Accessibility)
      |> Seq.filter (fun v -> not v.IsCompilerGenerated)
      |> Seq.filter cond |> Seq.choose (tryReadMember ctx entityUrl kind) |> List.ofSeq
 
    let readTypeNameAsText (typ:FSharpEntity) =
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

    let readUnionCases ctx entityUrl (typ:FSharpEntity) =
      typ.UnionCases
      |> List.ofSeq
      |> List.filter (fun v -> checkAccess ctx v.Accessibility)
      |> List.choose (fun case ->
        readCommentsInto case ctx case.XmlDocSig (fun cat _ comment ->
          let details = readUnionCase ctx typ case
          ApiDocMember(case.Name, readAttributes case.Attributes, entityUrl, ApiDocMemberKind.UnionCase, cat, details, comment, case)))

    let readRecordFields ctx entityUrl (typ:FSharpEntity) =
      typ.FSharpFields
      |> List.ofSeq
      |> List.filter (fun field -> not field.IsCompilerGenerated)
      |> List.choose (fun field ->
        readCommentsInto field ctx field.XmlDocSig (fun cat _ comment ->
          let details = readFSharpField ctx field
          ApiDocMember(field.Name, readAttributes (Seq.append field.FieldAttributes field.PropertyAttributes), entityUrl, ApiDocMemberKind.RecordField, cat, details, comment, field)))
            
    let readStaticParams ctx entityUrl (typ:FSharpEntity) =
      typ.StaticParameters
      |> List.ofSeq
      |> List.choose (fun staticParam ->
        readCommentsInto staticParam ctx (getFSharpStaticParamXmlSig typ staticParam.Name) (fun cat _ comment ->
          let details = readFSharpStaticParam ctx staticParam
          ApiDocMember(staticParam.Name, [], entityUrl, ApiDocMemberKind.StaticParameter, cat, details, comment, staticParam)))

    // Create a xml documentation snippet and add it to the XmlMemberMap
    let registerXmlDoc (ctx:ReadingContext) xmlDocSig (xmlDoc:string) =
      let xmlDoc = if xmlDoc.Contains "<summary>" then xmlDoc else "<summary>" + xmlDoc + "</summary>"
      let xmlDoc = "<member name=\"" + xmlDocSig + "\">" + xmlDoc + "</member>"
      let xmlDoc = XElement.Parse xmlDoc
      ctx.XmlMemberMap.Add(xmlDocSig, xmlDoc)
      xmlDoc

    // Type providers don't have their docs dumped into the xml file,
    // so we need to add them to the XmlMemberMap separately
    let registerTypeProviderXmlDocs (ctx:ReadingContext) (typ:FSharpEntity) =
      let xmlDoc = registerXmlDoc ctx typ.XmlDocSig (String.concat "" typ.XmlDoc)
      xmlDoc.Descendants(XName.Get "param")
      |> Seq.choose (fun p ->
          let nameAttr = p.Attribute(XName.Get "name")
          if nameAttr = null then None
          else Some (nameAttr.Value, p.Value))
      |> Seq.iter (fun (name, xmlDoc) ->
        let xmlDocSig = getFSharpStaticParamXmlSig typ name
        registerXmlDoc ctx xmlDocSig (Security.SecurityElement.Escape xmlDoc) |> ignore)

    let rec readEntities ctx (entities:seq<_>) =
      readChildren ctx entities readModule (fun x -> x.IsFSharpModule) @
      readChildren ctx entities readType (fun x -> not x.IsFSharpModule)

    and readType (ctx:ReadingContext) (typ:FSharpEntity) =
      if typ.IsProvided && typ.XmlDoc.Count > 0 then
          registerTypeProviderXmlDocs ctx typ

      let xmlDocSig = getXmlDocSigForType typ

      readCommentsInto typ ctx xmlDocSig (fun cat _cmds comment ->
        let entityUrl = ctx.UrlMap.TryResolveUrlBaseNameForEntity typ

        let rec getMembers (typ:FSharpEntity) = [
          yield! typ.MembersFunctionsAndValues
          match typ.BaseType with
          | Some baseType ->
              //TODO: would be better to reuse instead of reparsing the base type xml docs
              let cmds, _comment = readCommentAndCommands ctx (getXmlDocSigForType baseType.TypeDefinition)
              match cmds with
              | Command "omit" _ -> yield! getMembers baseType.TypeDefinition
              | _ -> ()
          | None -> ()
        ]

        let ivals, svals =
            getMembers typ
            |> List.ofSeq
            |> List.filter (fun v -> checkAccess ctx v.Accessibility && not v.IsCompilerGenerated && not v.IsOverrideOrExplicitInterfaceImplementation)
            |> List.filter (fun v ->
                  not v.IsCompilerGenerated &&
                  not v.IsEventAddMethod && not v.IsEventRemoveMethod &&
                  not v.IsPropertyGetterMethod && not v.IsPropertySetterMethod)
            |> List.partition (fun v -> v.IsInstanceMember)

        let cvals, svals = svals |> List.partition (fun v -> v.CompiledName = ".ctor")

        let baseType = typ.BaseType |> Option.map (formatTypeAsHtml ctx.UrlMap >> codeHtml)
        let allInterfaces = [ for i in typ.AllInterfaces -> formatTypeAsHtml ctx.UrlMap i  |> codeHtml ]
        let abbreviatedType = if typ.IsFSharpAbbreviation then Some (formatTypeAsHtml ctx.UrlMap typ.AbbreviatedType |> codeHtml) else None 
        let delegateSignature = if typ.IsDelegate then Some (formatDelegateSignatureAsHtml ctx.UrlMap typ.DisplayName typ.FSharpDelegateSignature  |> codeHtml) else None 

        let name = readTypeNameAsText typ
        let cases = readUnionCases ctx entityUrl typ
        let fields = readRecordFields ctx entityUrl typ
        let statParams = readStaticParams ctx entityUrl typ

        let attrs = readAttributes typ.Attributes

        let ctors = readAllMembers ctx entityUrl ApiDocMemberKind.Constructor cvals
        let inst = readAllMembers ctx entityUrl ApiDocMemberKind.InstanceMember ivals
        let stat = readAllMembers ctx entityUrl ApiDocMemberKind.StaticMember svals

        let rqa = hasAttrib<RequireQualifiedAccessAttribute> typ.Attributes
        ApiDocEntity (true, name, cat, entityUrl, comment, ctx.Assembly, attrs, cases, fields, statParams, ctors,
           inst, stat, allInterfaces, baseType, abbreviatedType, delegateSignature, typ, [], [], [], [], rqa, ctx.Parameters))

    and readModule (ctx:ReadingContext) (modul:FSharpEntity) =
      readCommentsInto modul ctx modul.XmlDocSig (fun cat _cmd comment ->

        // Properties & value bindings in the module
        let entityUrl = ctx.UrlMap.TryResolveUrlBaseNameForEntity modul
        let vals = readMembers ctx entityUrl ApiDocMemberKind.ValueOrFunction modul (fun v -> not v.IsMember && not v.IsActivePattern)
        let exts = readMembers ctx entityUrl ApiDocMemberKind.TypeExtension modul (fun v -> v.IsExtensionMember)
        let pats = readMembers ctx entityUrl ApiDocMemberKind.ActivePattern modul (fun v -> v.IsActivePattern)
        let attrs = readAttributes modul.Attributes
        // Nested modules and types
        let entities = readEntities ctx modul.NestedEntities
        let rqa = hasAttrib<RequireQualifiedAccessAttribute> modul.Attributes
 
        ApiDocEntity
          ( false, modul.DisplayName, cat, entityUrl, comment, ctx.Assembly, attrs,
            [], [], [], [], [], [], [], None, None, None, modul, entities, vals,
            exts, pats, rqa, ctx.Parameters ))

    // ----------------------------------------------------------------------------------------------
    // Reading namespace and assembly details
    // ----------------------------------------------------------------------------------------------

    let stripMicrosoft (str: string) =
        if str.StartsWith("Microsoft.") then
            str.["Microsoft.".Length ..]
        elif str.StartsWith("microsoft-") then
            str.["microsoft-".Length ..]
        else
            str
  
    let readNamespace ctx (ns, entities:seq<FSharpEntity>) =
      let entities = readEntities ctx entities
      ApiDocNamespace(stripMicrosoft ns, entities, ctx.Parameters)

    let readAssembly (assembly:FSharpAssembly, publicOnly, xmlFile:string, parameters, sourceFolderRepo, urlRangeHighlight, mdcomments, urlMap, codeFormatCompilerArgs) =
      let assemblyName = AssemblyName(assembly.QualifiedName)

      // Read in the supplied XML file, map its name attributes to document text
      let doc = XDocument.Load(xmlFile)

      // don't use 'dict' to allow the dictionary to be mutated later on
      let xmlMemberMap = Dictionary()
      for key, value in
        [ for e in doc.Descendants(XName.Get "member") do
            let attr = e.Attribute(XName.Get "name")
            if attr <> null && not (String.IsNullOrEmpty(attr.Value)) then
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
      let formatAgent = CodeFormatAgent()
      let ctx =
        ReadingContext.Create
          (publicOnly, assemblyName, xmlMemberMap, sourceFolderRepo, urlRangeHighlight,
           mdcomments, urlMap, asmPath, codeFormatCompilerArgs, formatAgent, parameters)
             
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

      /// The substitution parameters active for this input. If specified these
      /// are used instead of the overall parameters.  This allows different parameters (e.g.
      /// different authors) for each assembly in a collection.
      Parameters: Parameters option

      /// Whether the input uses markdown comments
      MarkdownComments: bool

      /// Whether to generate only public things
      PublicOnly: bool 
    }
    static member FromFile(assemblyPath: string, ?mdcomments, ?parameters, ?sourceRepo, ?sourceFolder, ?publicOnly) =
       { Path=assemblyPath;
         XmlFile = None;
         SourceFolder = sourceFolder;
         SourceRepo=sourceRepo;
         Parameters = parameters;
         PublicOnly=defaultArg publicOnly true;
         MarkdownComments = defaultArg mdcomments false }

/// Represents a set of assemblies integrated with their associated documentation
type ApiDocModel =
  {
    /// The parameters.  Different parameters can also be used for each specific input
    Parameters: Parameters

    /// The full list of all entities
    Collection: ApiDocCollection

    /// The full list of all entities
    EntityInfos: ApiDocEntityInfo list

    /// The root URL for the entire generation, normally '/'
    Root: string

    /// Indicates if each collection is being qualified by its collection name, e.g. 'reference/FSharp.Core'
    Qualify: bool

  }

  /// URL of the 'index.html' for the reference documentation for the model
  member x.IndexUrl(root, collectionName, qualify) =
        sprintf "%sreference/%sindex.html" root (if qualify then collectionName else "") 

  /// URL of the 'index.html' for the reference documentation for the model
  member x.IndexOutputFile(collectionName, qualify) =
        sprintf "reference/%sindex.html" (if qualify then collectionName else "") 

  static member internal Generate(projects: ApiDocInput list, collectionName, libDirs, otherFlags,
         qualify, urlRangeHighlight, root, parameters) =

    let (@@) a b = Path.Combine(a, b)

    // Default template file names

    let otherFlags = defaultArg otherFlags []
    let libDirs = defaultArg libDirs [] |> List.map Path.GetFullPath
    let dllFiles = projects |> List.map (fun p -> Path.GetFullPath p.Path)
    let urlRangeHighlight =defaultArg urlRangeHighlight (fun url start stop -> String.Format("{0}#L{1}-{2}", url, start, stop))

    // When resolving assemblies, look in folders where all DLLs live
    AppDomain.CurrentDomain.add_AssemblyResolve(System.ResolveEventHandler(fun o e ->
      Log.verbf "Resolving assembly: %s" e.Name
      let asmName = System.Reflection.AssemblyName(e.Name)
      let asmOpt =
        dllFiles |> Seq.tryPick (fun dll ->
          let root = Path.GetDirectoryName(dll)
          let file = root @@ (asmName.Name + ".dll")
          if File.Exists(file) then
            try
                let bytes = File.ReadAllBytes(file)
                Some(System.Reflection.Assembly.Load(bytes))
            with e ->
              printfn "Couldn't load Assembly\n%s\n%s" e.Message e.StackTrace
              None
          else None )
      defaultArg asmOpt null
    ))

    // Compiler arguments used when formatting code snippets inside Markdown comments
    let codeFormatCompilerArgs =
      [ for dir in libDirs do yield sprintf "-I:\"%s\"" dir
        for file in dllFiles do yield sprintf "-r:\"%s\"" file ]
      |> String.concat " "

    // Read and process assemblies and the corresponding XML files
    let assemblies =
      let resolvedList =
        //FSharpAssembly.LoadFiles(projects, libDirs, otherFlags = otherFlags)
        FSharpAssembly.LoadFiles(dllFiles, libDirs, otherFlags = otherFlags, manualResolve=true)
        |> Seq.toList
        |> List.zip projects

      // generate the names for the html files beforehand so we can resolve <see cref=""/> links.
      let urlMap = CrossReferenceResolver(root)

      for (_, asmOpt) in resolvedList do
        match asmOpt with
        | (_, Some asm) ->
          asm.Contents.Entities |> Seq.iter (urlMap.RegisterEntity)
        | _ -> ()

      resolvedList |> List.choose (fun (project, (dllFile, asmOpt)) ->
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
          Log.warnf "**** Skipping assembly '%s' because was not found in resolved assembly list" dllFile
          None
        | Some asm ->
          let xmlFile = defaultArg project.XmlFile (Path.ChangeExtension(dllFile, ".xml"))
          let xmlFileNoExt = Path.GetFileNameWithoutExtension(xmlFile)
          let xmlFileOpt =
            //Directory.EnumerateFiles(Path.GetDirectoryName(xmlFile), xmlFileNoExt + ".*")
            Directory.EnumerateFiles(Path.GetDirectoryName xmlFile)
            |> Seq.filter (fun file ->
                let fileNoExt = Path.GetFileNameWithoutExtension file
                let ext = Path.GetExtension file
                xmlFileNoExt.Equals(fileNoExt,StringComparison.OrdinalIgnoreCase)
                && ext.Equals(".xml",StringComparison.OrdinalIgnoreCase)
            )
            |> Seq.tryHead
            //|> Seq.map (fun f -> f, f.Remove(0, xmlFile.Length - 4))
            //|> Seq.tryPick (fun (f, ext) ->
            //    if ext.Equals(".xml", StringComparison.CurrentCultureIgnoreCase)
            //      then Some(f) else None)

          let publicOnly = project.PublicOnly
          let mdcomments = project.MarkdownComments
          let parameters = defaultArg project.Parameters parameters 
          match xmlFileOpt with
          | None -> raise (FileNotFoundException(sprintf "Associated XML file '%s' was not found." xmlFile))
          | Some xmlFile ->
            SymbolReader.readAssembly (asm, publicOnly, xmlFile, parameters, sourceFolderRepo, urlRangeHighlight, mdcomments, urlMap, codeFormatCompilerArgs )
            |> Some)

    // Union namespaces from multiple libraries
    let namespaces = Dictionary<_, _>()
    for _, nss in assemblies do
      for ns in nss do
        match namespaces.TryGetValue(ns.Name) with
        | true, (entities, parameters) -> namespaces.[ns.Name] <- (entities @ ns.Entities, parameters)
        | false, _ -> namespaces.Add(ns.Name, (ns.Entities, ns.Parameters))

    let namespaces =
      [ for (KeyValue(name, (entities, parameters))) in namespaces do
          if entities.Length > 0 then
              ApiDocNamespace(name, entities, parameters) ]

    let collection = ApiDocCollection(collectionName, List.map fst assemblies, namespaces |> List.sortBy (fun ns -> ns.Name))

    let rec nestedModules ns parent (modul:ApiDocEntity) =
      [
        yield ApiDocEntityInfo(modul, collection, ns, parent)
        for n in modul.NestedEntities do
            if not n.IsTypeDefinition then
               yield! nestedModules ns (Some modul) n
      ]

    let moduleInfos =
      [ for ns in collection.Namespaces do
          for n in ns.Entities do
            if not n.IsTypeDefinition then
               yield! nestedModules ns None n ]

    let createType ns parent typ =
        ApiDocEntityInfo(typ, collection, ns, parent)

    let rec nestedTypes ns (modul:ApiDocEntity) =
      [ for n in modul.NestedEntities do
         if n.IsTypeDefinition then
            yield createType ns (Some modul) n
        for n in modul.NestedEntities do
         if not n.IsTypeDefinition then
          yield! nestedTypes ns n ]

    let typesInfos =
      [ for ns in collection.Namespaces do
          for n in ns.Entities do
              if not n.IsTypeDefinition then
                  yield! nestedTypes ns n
          for n in ns.Entities do
              if n.IsTypeDefinition then
                 yield createType ns None n  ]

    {
      Parameters = parameters
      Collection = collection
      EntityInfos = moduleInfos @ typesInfos
      Root = root
      Qualify = qualify
    }

/// Represents an entry suitable for constructing a Lunr index
type ApiDocsSearchIndexEntry = {
    uri: string
    title: string
    content: string
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

