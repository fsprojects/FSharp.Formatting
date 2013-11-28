namespace FSharp.MetadataFormat

open System
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Metadata
open FSharp.Patterns

open System.IO
open System.Xml.Linq

type Comment =
  { Blurb : string
    FullText : string
    Sections : list<KeyValuePair<string, string>> }
  static member Empty =
    { Blurb = ""; FullText = ""; Sections = [] }
  static member Create(blurb, full, sects) = 
    { Blurb = blurb; FullText = full; Sections = sects }

type MemberOrValue = 
  { Usage : int -> string 
    Modifiers : string list 
    TypeArguments : string list 
    Signature : string }
  member x.FormatUsage(maxLength) = x.Usage(maxLength)
  member x.FormatTypeArguments = String.concat ", " x.TypeArguments
  member x.FormatModifiers = String.concat " " x.Modifiers
  static member Create(usage, mods, typars, sign) =
    { Usage = usage; Modifiers = mods; TypeArguments = typars;
      Signature = sign }

type MemberKind = 
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

type Member =
  { Name : string
    Category : string
    Kind : MemberKind
    Details : MemberOrValue
    Comment : Comment }
  static member Create(name, kind, cat, details, comment) = 
    { Member.Name = name; Kind = kind;
      Category = cat; Details = details; Comment = comment }

type Type = 
  { Name : string 
    UrlName : string
    Comment : Comment 

    UnionCases : Member list
    RecordFields : Member list

    AllMembers : Member list
    Constructors : Member list
    InstanceMembers : Member list
    StaticMembers : Member list }
  static member Create(name, url, comment, cases, fields, ctors, inst, stat) = 
    { Type.Name = name; UrlName = url; Comment = comment;
      UnionCases = cases; RecordFields = fields
      AllMembers = List.concat [ ctors; inst; stat; cases; fields ] ;
      Constructors = ctors; InstanceMembers = inst; StaticMembers = stat }

type Module = 
  { Name : string 
    UrlName : string
    Comment : Comment
    
    AllMembers : Member list

    NestedModules : Module list
    NestedTypes : Type list

    ValuesAndFuncs : Member list
    TypeExtensions : Member list
    ActivePatterns : Member list }
  static member Create(name, url, comment, modules, types, vals, exts, pats) = 
    { Module.Name = name; UrlName = url; Comment = comment;
      AllMembers = List.concat [ vals; exts; pats ] 
      NestedModules = modules; NestedTypes = types
      ValuesAndFuncs = vals; TypeExtensions = exts; ActivePatterns = pats }

type Namespace = 
  { Name : string
    Modules : Module list
    Types : Type list }
  static member Create(name, mods, typs) = 
    { Namespace.Name = name; Modules = mods; Types = typs }

type AssemblyGroup = 
  { Name : string
    Assemblies : AssemblyName list
    Namespaces : Namespace list }
  static member Create(name, asms, nss) =
    { AssemblyGroup.Name = name; Assemblies = asms; Namespaces = nss }

type ModuleInfo = 
  { Module : Module
    Assembly : AssemblyGroup }
  static member Create(modul, asm) = 
    { ModuleInfo.Module = modul; Assembly = asm }

type TypeInfo = 
  { Type : Type
    Assembly : AssemblyGroup }
  static member Create(typ, asm) = 
    { TypeInfo.Type = typ; Assembly = asm }

module ValueReader = 
  open System.Collections.ObjectModel

  let (|AllAndLast|_|) (list:'T list)= 
    if list.IsEmpty then None
    else let revd = List.rev list in Some(List.rev revd.Tail, revd.Head)

  let uncapitalize (s:string) =
    s.Substring(0, 1).ToLowerInvariant() + s.Substring(1)

  let isAttrib<'T> (attrib: FSharpAttribute)  =
    attrib.ReflectionType = typeof<'T> 

  let tryFindAttrib<'T> (attribs: ReadOnlyCollection<FSharpAttribute>)  =
    attribs |> Seq.tryPick (fun a -> if isAttrib<'T>(a) then Some (a.Value :?> 'T) else None)

  let hasAttrib<'T> (attribs: ReadOnlyCollection<FSharpAttribute>) = 
    tryFindAttrib<'T>(attribs).IsSome

  let (|MeasureProd|_|) (typ : FSharpType) = 
      if typ.IsNamed && typ.NamedEntity.LogicalName = "*" && typ.GenericArguments.Count = 2 then Some (typ.GenericArguments.[0], typ.GenericArguments.[1])
      else None

  let (|MeasureInv|_|) (typ : FSharpType) = 
      if typ.IsNamed && typ.NamedEntity.LogicalName = "/" && typ.GenericArguments.Count = 1 then Some typ.GenericArguments.[0]
      else None

  let (|MeasureOne|_|) (typ : FSharpType) = 
      if typ.IsNamed && typ.NamedEntity.LogicalName = "1" && typ.GenericArguments.Count = 0 then  Some ()
      else None

  let formatTypeArgument (typar:FSharpGenericParameter) =
    (if typar.IsSolveAtCompileTime then "^" else "'") + typar.Name

  let formatTypeArguments (typars:seq<FSharpGenericParameter>) =
    Seq.map formatTypeArgument typars |> List.ofSeq

  let bracket (str:string) = 
    if str.Contains(" ") then "(" + str + ")" else str

  let bracketIf cond str = 
    if cond then "(" + str + ")" else str

  let formatTyconRef (tcref:FSharpEntity) = 
    // TODO: layoutTyconRef generates hyperlinks 
    tcref.DisplayName

  let rec formatTypeApplication typeName prec prefix args =
    if prefix then 
      match args with
      | [] -> typeName
      | [arg] -> typeName + "<" + (formatTypeWithPrec 4 arg) + ">"
      | args -> bracketIf (prec <= 1) (typeName + "<" + (formatTypesWithPrec 2 "," args) + ">")
    else
      match args with
      | [] -> typeName
      | [arg] -> (formatTypeWithPrec 2 arg) + " " + typeName 
      | args -> bracketIf (prec <= 1) ((bracket (formatTypesWithPrec 2 "," args)) + typeName)

  and formatTypesWithPrec prec sep typs = 
    String.concat sep (typs |> Seq.map (formatTypeWithPrec prec))

  and formatTypeWithPrec prec (typ:FSharpType) =
    // Measure types are stored as named types with 'fake' constructors for products, "1" and inverses
    // of measures in a normalized form (see Andrew Kennedy technical reports). Here we detect this 
    // embedding and use an approximate set of rules for layout out normalized measures in a nice way. 
    match typ with 
    | MeasureProd (ty,MeasureOne) 
    | MeasureProd (MeasureOne, ty) -> formatTypeWithPrec prec ty
    | MeasureProd (ty1, MeasureInv ty2) 
    | MeasureProd (ty1, MeasureProd (MeasureInv ty2, MeasureOne)) -> 
        (formatTypeWithPrec 2 ty1) + "/" + (formatTypeWithPrec 2 ty2)
    | MeasureProd (ty1,MeasureProd(ty2,MeasureOne)) 
    | MeasureProd (ty1,ty2) -> 
        (formatTypeWithPrec 2 ty1) + "*" + (formatTypeWithPrec 2 ty2)
    | MeasureInv ty -> "/" + (formatTypeWithPrec 1 ty)
    | MeasureOne  -> "1" 
    | _ when typ.IsNamed -> 
        let tcref = typ.NamedEntity 
        let tyargs = typ.GenericArguments |> Seq.toList
        // layout postfix array types
        formatTypeApplication (formatTyconRef tcref) prec tcref.UsesPrefixDisplay tyargs 
    | _ when typ.IsTuple ->
        let tyargs = typ.GenericArguments |> Seq.toList
        bracketIf (prec <= 2) (formatTypesWithPrec 2 " * " tyargs)
    | _ when typ.IsFunction ->
        let rec loop soFar (typ:FSharpType) = 
          if typ.IsFunction then 
            let domainTyp, retType = typ.GenericArguments.[0], typ.GenericArguments.[1]
            loop (soFar + (formatTypeWithPrec 4 typ.GenericArguments.[0]) + " -> ") retType
          else 
            soFar + formatTypeWithPrec 5 typ
        bracketIf (prec <= 4) (loop "" typ)
    | _ when typ.IsGenericParameter ->
        formatTypeArgument typ.GenericParameter
    | _ -> "(type)" 

  let formatType typ = 
    formatTypeWithPrec 5 typ

  // Format each argument, including its name and type 
  let formatArgUsage generateTypes i (arg:FSharpParameter) = 
    // Detect an optional argument 
    let isOptionalArg = hasAttrib<OptionalArgumentAttribute> arg.Attributes
    let nm = match arg.Name with null -> "arg" + string i | nm -> nm
    let argName = if isOptionalArg then "?" + nm else nm
    if generateTypes then 
      (if String.IsNullOrWhiteSpace(arg.Name) then "" else argName + ":") + 
      formatTypeWithPrec 2 arg.Type
    else argName

  let formatArgsUsage generateTypes (v:FSharpMemberOrVal) args =
    let isItemIndexer = (v.IsInstanceMember && v.DisplayName = "Item")
    let counter = let n = ref 0 in fun () -> incr n; !n
    let unit, argSep, tupSep = 
      if generateTypes then "unit", " -> ", " * "
      else "()", " ", ", "
    args
    |> List.map (List.map (fun x -> formatArgUsage generateTypes (counter()) x))
    |> List.map (function 
        | [] -> unit 
        | [arg] when not v.IsMember || isItemIndexer -> arg 
        | args when isItemIndexer -> String.concat tupSep args
        | args -> bracket (String.concat tupSep args))
    |> String.concat argSep
  
  let readMemberOrVal (v:FSharpMemberOrVal) = 
    let buildUsage (args:string option) = 
      let tyname = v.LogicalEnclosingEntity.DisplayName
      let parArgs = args |> Option.map (fun s -> 
        if String.IsNullOrWhiteSpace(s) then "" 
        elif s.StartsWith("(") then s
        else sprintf "(%s)" s)
      match v.IsMember, v.IsInstanceMember, v.LogicalName, v.DisplayName with
      // Constructors and indexers
      | _, _, ".ctor", _ -> "new" + (defaultArg parArgs "(...)")
      | _, true, _, "Item" -> "[" + (defaultArg args "...") + "]"
      // Ordinary instance members
      | _, true, _, name -> name + (defaultArg parArgs "(...)")
      // Ordinary functions or values
      | false, _, _, name when 
          not (hasAttrib<RequireQualifiedAccessAttribute> v.LogicalEnclosingEntity.Attributes) -> 
            name + " " + (defaultArg args "(...)")
      // Ordinary static members or things (?) that require fully qualified access
      | _, _, _, name -> name + (defaultArg parArgs "(...)")

    let modifiers =
      [ // TODO: v.Accessibility does not contain anything
        if v.InlineAnnotation = FSharpInlineAnnotation.AlwaysInline then yield "inline"
        if v.IsDispatchSlot then yield "abstract" ]

    let argInfos = v.CurriedParameterGroups |> Seq.map Seq.toList |> Seq.toList 
    let retType = v.ReturnParameter.Type
    let argInfos, retType = 
        match argInfos, v.IsGetterMethod, v.IsSetterMethod with
        | [ AllAndLast(args, last) ], _, true -> [ args ], Some last.Type
        | _, _, true -> argInfos, None
        | [[]], true, _ -> [], Some retType
        | _, _, _ -> argInfos, Some retType

    // Extension members can have apparent parents which are not F# types.
    // Hence getting the generic argument count if this is a little trickier
    let numGenericParamsOfApparentParent = 
        let pty = v.LogicalEnclosingEntity 
        if pty.IsExternal then 
            let ty = v.LogicalEnclosingEntity.ReflectionType 
            if ty.IsGenericType then ty.GetGenericArguments().Length 
            else 0 
        else 
            pty.GenericParameters.Count
    let tps = v.GenericParameters |> Seq.skip numGenericParamsOfApparentParent
    let typars = formatTypeArguments tps 

    //let cxs  = indexedConstraints v.GenericParameters 
    let retType = defaultArg (retType |> Option.map formatType) "unit"
    let signature =
      match argInfos with
      | [] -> retType
      | _  -> (formatArgsUsage true v argInfos) + " -> " + retType

    let usage = formatArgsUsage false v argInfos
    let buildShortUsage length = 
      let long = buildUsage (Some usage)
      if long.Length <= length then long
      else buildUsage None
    MemberOrValue.Create(buildShortUsage, modifiers, typars, signature)

    (*

    let docL = 
        let afterDocs = 
            [ let argCount = ref 0 
              for xs in argInfos do 
                for x in xs do 
                    incr argCount
                    yield layoutArgUsage true !argCount x

              if not v.IsGetterMethod && not v.IsSetterMethod && retType.IsSome then 
                  yield wordL "returns" ++ retTypeL
              match layoutConstraints denv () cxs with 
              | None ->  ()
              | Some cxsL -> yield cxsL ]
        match afterDocs with
        | [] -> emptyL
        | _ -> (List.reduce (@@) [ yield wordL ""; yield! afterDocs ])

    let noteL = 
        let noteDocs = 
            [ if cxs |> List.exists (snd >> List.exists (fun cx -> cx.IsMemberConstraint)) then
                  yield (wordL "Note: this operator is overloaded")  ]
        match noteDocs with
        | [] -> emptyL
        | _ -> (List.reduce (@@) [ yield wordL ""; yield! noteDocs ])
                
    let usageL = if v.IsSetterMethod then usageL --- wordL "<- v" else usageL
        
    //layoutAttribs denv v.Attributes 
    usageL  , docL, noteL
    *)

  let readUnionCase (case:FSharpUnionCase) =
    let usage (maxLength:int) = case.Name
    let modifiers = List.empty
    let typeparams = List.empty
    let signature = case.Fields |> List.ofSeq |> List.map (fun field -> formatType field.Type) |> String.concat " * "
    MemberOrValue.Create(usage, modifiers, typeparams, signature)

  let readRecordField (field:FSharpRecordField) =
    let usage (maxLength:int) = field.Name
    let modifiers =
      [ if field.IsMutable then yield "mutable"
        if field.IsStatic then yield "static" ]
    let typeparams = List.empty
    let signature = formatType field.Type
    MemberOrValue.Create(usage, modifiers, typeparams, signature)

module Reader =
  open FSharp.Markdown
  open System.IO
  open ValueReader

  type ReadingContext = 
    { XmlMemberMap : IDictionary<string, XElement>
      MarkdownComments : bool
      UniqueUrlName : string -> string }
    member x.XmlMemberLookup(key) =
      match x.XmlMemberMap.TryGetValue(key) with
      | true, v -> Some v
      | _ -> None 
    static member Create(map) = 
      let usedNames = Dictionary<_, _>()
      let nameGen (name:string) =
        let nice = name.Replace(".", "-").Replace("`", "-").ToLower()
        let found =
          seq { yield nice
                for i in Seq.initInfinite id do yield sprintf "%s-%d" nice i }
          |> Seq.find (usedNames.ContainsKey >> not)
        usedNames.Add(found, true)
        found
      { XmlMemberMap = map; MarkdownComments = true; UniqueUrlName = nameGen }

  // ----------------------------------------------------------------------------------------------
  // Helper functions
  // ----------------------------------------------------------------------------------------------

  let removeSpaces (comment:string) =
    use reader = new StringReader(comment)
    let lines = 
      [ let line = ref ""
        while (line := reader.ReadLine(); line.Value <> null) do
          yield line.Value ]
    let spaces =
      lines 
      |> Seq.filter (String.IsNullOrWhiteSpace >> not)
      |> Seq.map (fun line -> line |> Seq.takeWhile Char.IsWhiteSpace |> Seq.length)
      |> Seq.min
    lines 
    |> Seq.map (fun line -> 
        if String.IsNullOrWhiteSpace(line) then ""
        else line.Substring(spaces))

  let readMarkdownComment (doc:MarkdownDocument) = 
    let groups = System.Collections.Generic.Dictionary<_, _>()
    let mutable current = "<default>"
    groups.Add(current, [])
    for par in doc.Paragraphs do
      match par with 
      | Heading(2, [Literal text]) -> 
          current <- text.Trim()
          groups.Add(current, [par])
      | par -> 
          groups.[current] <- par::groups.[current]
    let blurb = Markdown.WriteHtml(MarkdownDocument(List.rev groups.["<default>"], doc.DefinedLinks))
    let full = Markdown.WriteHtml(doc)

    let sections = 
      [ for (KeyValue(k, v)) in groups ->
          let body = if k = "<default>" then List.rev v else List.tail (List.rev v)
          let html = Markdown.WriteHtml(MarkdownDocument(body, doc.DefinedLinks))
          KeyValuePair(k, html) ]
    Comment.Create(blurb, full, sections)
          
  let readCommentAndCommands (ctx:ReadingContext) xmlSig = 
    match ctx.XmlMemberLookup(xmlSig) with 
    | None -> dict[], Comment.Empty
    | Some el ->
        let sum = el.Element(XName.Get "summary")
        if sum = null then dict[], Comment.Empty 
        else 
          if ctx.MarkdownComments then 
            let lines = removeSpaces sum.Value
            let cmds = new System.Collections.Generic.Dictionary<_, _>()
            let text =
              lines |> Seq.filter (function
                | String.StartsWithWrapped ("[", "]") (ParseCommand(k, v), rest) -> 
                    cmds.Add(k, v)
                    false
                | _ -> true) |> String.concat "\n"
            let doc = Markdown.Parse(text)
            cmds :> IDictionary<_, _>, readMarkdownComment doc
          else failwith "XML comments not supported yet"

  let readComment ctx xmlSig = readCommentAndCommands ctx xmlSig |> snd

  // ----------------------------------------------------------------------------------------------
  // Reading entities
  // ----------------------------------------------------------------------------------------------

  /// Reads XML documentation comments and calls the specified function
  /// to parse the rest of the entity, unless [omit] command is set.
  /// The function is called with category name, commands & comment.
  let readCommentsInto ctx xmlDoc f =
    let cmds, comment = readCommentAndCommands ctx xmlDoc
    match cmds with
    | Command "omit" _ -> None
    | Command "category" cat 
    | Let "" (cat, _) -> Some(f cat cmds comment)

  let readChildren ctx entities reader cond = 
    entities 
    |> Seq.filter cond 
    |> Seq.sortBy (fun (c:FSharpEntity) -> c.DisplayName)
    |> Seq.choose (reader ctx) 
    |> List.ofSeq

  let tryReadMember (ctx:ReadingContext) kind (memb:FSharpMemberOrVal) =
    readCommentsInto ctx memb.XmlDocSig (fun cat _ comment ->
      Member.Create(memb.DisplayName, kind, cat, readMemberOrVal memb, comment))

  let readAllMembers ctx kind (members:seq<FSharpMemberOrVal>) = 
    members 
    |> Seq.filter (fun v -> not v.IsCompilerGenerated)
    |> Seq.choose (tryReadMember ctx kind) |> List.ofSeq

  let readMembers ctx kind (entity:FSharpEntity) cond = 
    entity.MembersOrValues 
    |> Seq.filter (fun v -> not v.IsCompilerGenerated)
    |> Seq.filter cond |> Seq.choose (tryReadMember ctx kind) |> List.ofSeq

  let readTypeName (typ:FSharpEntity) =
    typ.GenericParameters
    |> List.ofSeq
    |> List.map (fun p -> sprintf "'%s" p.Name)
    |> function
    | [] -> typ.DisplayName
    | gnames -> sprintf "%s<%s>" typ.DisplayName (String.concat ", " gnames)

  let readUnionCases ctx (typ:FSharpEntity) =
    typ.UnionCases
    |> List.ofSeq
    |> List.choose (fun case ->
      readCommentsInto ctx case.XmlDocSig (fun cat _ comment ->
        Member.Create(case.Name, MemberKind.UnionCase, cat, readUnionCase case, comment)))

  let readRecordFields ctx (typ:FSharpEntity) =
    typ.RecordFields
    |> List.ofSeq
    |> List.choose (fun field ->
      readCommentsInto ctx field.XmlDocSig (fun cat _ comment ->
        Member.Create(field.Name, MemberKind.RecordField, cat, readRecordField field, comment)))

  // ----------------------------------------------------------------------------------------------
  // Reading modules types (mutually recursive, because of nesting)
  // ----------------------------------------------------------------------------------------------

  let rec readModulesAndTypes ctx (entities:seq<_>) = 
    let modules = readChildren ctx entities readModule (fun x -> x.IsModule) 
    let types = readChildren ctx entities readType (fun x -> not x.IsModule) 
    modules, types

  and readType (ctx:ReadingContext) (typ:FSharpEntity) =
    readCommentsInto ctx typ.XmlDocSig (fun cat cmds comment ->
      let urlName = ctx.UniqueUrlName (sprintf "%s.%s" typ.Namespace typ.CompiledName)

      let ivals, svals = typ.MembersOrValues |> List.ofSeq |> List.partition (fun v -> v.IsInstanceMember)
      let cvals, svals = svals |> List.partition (fun v -> v.CompiledName = ".ctor")
    
      (*
      // Base types?
      let iimpls = 
        if ( not typ.IsAbbreviation && not typ.HasAssemblyCodeRepresentation && 
             typ.ReflectionType.IsInterface) then [] else typ.Implements |> List.ofSeq
      // TODO: layout base type in some way
      if not iimpls.IsEmpty then 
        newTable1 hFile "Interfaces" 40 "Type"  (fun () -> 
          iimpls |> List.iter (fun i -> 
              newEntry1 hFile ("<pre>"+outputL widthVal (layoutType denv i)+"</pre>"))) 
      *)

      let name = readTypeName typ
      let cases = readUnionCases ctx typ
      let fields = readRecordFields ctx typ
        
      let ctors = readAllMembers ctx MemberKind.Constructor cvals 
      let inst = readAllMembers ctx MemberKind.InstanceMember ivals 
      let stat = readAllMembers ctx MemberKind.StaticMember svals 
      Type.Create
        ( name, urlName, comment, cases, fields, ctors, inst, stat ))

  and readModule (ctx:ReadingContext) (modul:FSharpEntity) =
    readCommentsInto ctx modul.XmlDocSig (fun cat cmd comment ->
    
      // Properties & value bindings in the module
      let urlName = ctx.UniqueUrlName (sprintf "%s.%s" modul.Namespace modul.CompiledName)
      let vals = readMembers ctx MemberKind.ValueOrFunction modul (fun v -> not v.IsMember && not v.IsActivePattern)
      let exts = readMembers ctx MemberKind.TypeExtension modul (fun v -> v.IsExtensionMember)
      let pats = readMembers ctx MemberKind.ActivePattern modul (fun v -> v.IsActivePattern)

      // Nested modules and types
      let modules, types = readModulesAndTypes ctx modul.NestedEntities

      Module.Create
        ( modul.DisplayName, urlName, comment,
          modules, types,
          vals, exts, pats ))

  // ----------------------------------------------------------------------------------------------
  // Reading namespace and assembly details
  // ----------------------------------------------------------------------------------------------

  let readNamespace ctx (ns, entities:seq<FSharpEntity>) =
    let modules, types = readModulesAndTypes ctx entities 
    Namespace.Create(ns, modules, types)

  let readAssembly (assembly:FSharpAssembly) (xmlFile:string) =
    let assemblyName = assembly.ReflectionAssembly.GetName()
    
    // Read in the supplied XML file, map its name attributes to document text 
    let doc = XDocument.Load(xmlFile)
    let xmlMemberMap =
      [ for e in doc.Descendants(XName.Get "member") do
          let attr = e.Attribute(XName.Get "name") 
          if attr <> null && not (String.IsNullOrEmpty(attr.Value)) then 
            yield attr.Value, e ] |> dict
    let ctx = ReadingContext.Create(xmlMemberMap)

    // 
    let namespaces = 
      assembly.Entities 
      |> Seq.groupBy (fun m -> m.Namespace) |> Seq.sortBy fst
      |> Seq.map (readNamespace ctx) |> List.ofSeq
    assemblyName, namespaces

// ------------------------------------------------------------------------------------------------
// Main - generating HTML
// ------------------------------------------------------------------------------------------------

open System.IO
open FSharp.MetadataFormat

/// For use in the tempaltes (lives in namespace FSharp.MetadataFormat)
type Html private() =
  static let mutable uniqueNumber = 0
  static member UniqueID() = 
    uniqueNumber <- uniqueNumber + 1
    uniqueNumber
  static member Encode(str) = 
    System.Web.HttpUtility.HtmlEncode(str)

/// Exposes metadata formatting functionality
type MetadataFormat = 
  static member Generate(dllFile, outDir, layoutRoots, ?parameters, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate, ?xmlFile) =
    MetadataFormat.Generate
      ( [dllFile], outDir, layoutRoots, ?parameters = parameters, ?namespaceTemplate = namespaceTemplate, 
        ?moduleTemplate = moduleTemplate, ?typeTemplate = typeTemplate, ?xmlFile = xmlFile)

  static member Generate(dllFiles, outDir, layoutRoots, ?parameters, ?namespaceTemplate, ?moduleTemplate, ?typeTemplate, ?xmlFile) =
    let (@@) a b = Path.Combine(a, b)
    let parameters = defaultArg parameters []
    let props = [ "Properties", dict parameters ]

    // Default template file names
    let namespaceTemplate = defaultArg namespaceTemplate "namespaces.cshtml"
    let moduleTemplate = defaultArg moduleTemplate "module.cshtml"
    let typeTemplate = defaultArg typeTemplate "type.cshtml"

    // When resolving assemblies, look in folders where all DLLs live
    AppDomain.CurrentDomain.add_AssemblyResolve(System.ResolveEventHandler(fun o e ->
      Log.logf "Resolving assembly: %s" e.Name
      let asmName = System.Reflection.AssemblyName(e.Name)
      let asmOpt = 
        dllFiles |> Seq.tryPick (fun dll ->
          let root = Path.GetDirectoryName(dll)
          let file = root @@ (asmName.Name + ".dll")
          if File.Exists(file) then 
            Some(System.Reflection.Assembly.LoadFile(file))
          else None )
      defaultArg asmOpt null
    ))

    // Read and process assmeblies and the corresponding XML files
    let assemblies = 
      [ for dllFile in dllFiles do
          let xmlFile = defaultArg xmlFile (Path.ChangeExtension(dllFile, ".xml"))
          if not (File.Exists xmlFile) then 
            raise <| FileNotFoundException(sprintf "Associated XML file '%s' was not found." xmlFile)

          Log.logf "Reading assembly: %s" dllFile
          let asm = FSharpAssembly.FromFile(dllFile)
          Log.logf "Parsing assembly"
          yield Reader.readAssembly asm xmlFile ]

    // Get the name - either from parameters, or name of the assembly (if there is just one)
    let name = 
      let projName = parameters |> List.tryFind (fun (k, v) -> k = "project-name") |> Option.map snd
      match assemblies, projName with 
      | _, Some name -> name
      | [ asm, _ ], _ -> asm.Name
      | _ -> failwith "Unknown project name. Provide 'properties' parameter with 'project-name' key."
    // Union namespaces from multiple libraries
    let namespaces = Dictionary<_, _>()
    for _, nss in assemblies do 
      for ns in nss do 
        match namespaces.TryGetValue(ns.Name) with
        | true, (mods, typs) -> namespaces.[ns.Name] <- (mods @ ns.Modules, typs @ ns.Types)
        | false, _ -> namespaces.Add(ns.Name, (ns.Modules, ns.Types))

    let namespaces = [ for (KeyValue(name, (mods, typs))) in namespaces -> Namespace.Create(name, mods, typs) ]
    let asm = AssemblyGroup.Create(name, List.map fst assemblies, namespaces)
        
    // Generate all the HTML stuff
    Log.logf "Starting razor engine"
    let razor = RazorRender(layoutRoots, ["FSharp.MetadataFormat"])
    razor.Model <- box asm

    Log.logf "Generating: index.html"
    let out = razor.ProcessFile(RazorRender.Resolve(layoutRoots, namespaceTemplate), props)
    File.WriteAllText(outDir @@ "index.html", out)

    // Generate documentation for all modules
    Log.logf "Generating modules..."
    let rec nestedModules (modul:Module) = seq {
      yield modul
      for n in modul.NestedModules do yield! nestedModules n }
    let modules = 
      [ for ns in asm.Namespaces do 
          for n in ns.Modules do yield! nestedModules n ]
    
    let moduleTemplateFile = RazorRender.Resolve(layoutRoots, moduleTemplate)
    Parallel.pfor modules (fun () -> RazorRender(layoutRoots,["FSharp.MetadataFormat"])) (fun modul _ razor -> 
      Log.logf "Generating module: %s" modul.UrlName
      razor.Model <- box (ModuleInfo.Create(modul, asm))
      let out = razor.ProcessFile(moduleTemplateFile, props)
      File.WriteAllText(outDir @@ (modul.UrlName + ".html"), out)
      Log.logf "Finished module: %s" modul.UrlName
      razor)

    Log.logf "Generating types..."
    let rec nestedTypes (modul:Module) = seq {
      yield! modul.NestedTypes
      for n in modul.NestedModules do yield! nestedTypes n }
    let types = 
      [ for ns in asm.Namespaces do 
          for n in ns.Modules do yield! nestedTypes n
          yield! ns.Types ]

    // Generate documentation for all types
    let typeTemplateFile = RazorRender.Resolve(layoutRoots, typeTemplate)
    Parallel.pfor types (fun () -> RazorRender(layoutRoots, ["FSharp.MetadataFormat"])) (fun typ _ razor -> 
      Log.logf "Generating type: %s" typ.UrlName
      razor.Model <- box (TypeInfo.Create(typ, asm))
      let out = razor.ProcessFile(typeTemplateFile, props)
      File.WriteAllText(outDir @@ (typ.UrlName + ".html"), out)
      Log.logf "Finished type: %s" typ.UrlName
      razor)
