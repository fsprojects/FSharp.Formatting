﻿namespace FSharp.MetadataFormat

open System
open System.Reflection
open System.Collections.Generic
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Range
open FSharp.Formatting.Common
open Yaaf.FSharp.Scripting
open FSharp.Patterns
open FSharp.CodeFormat

open System.Text
open System.IO
open System.Xml
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
    Signature : string
    SourceLocation : string option
    CompiledName : string option }
  member x.FormatUsage(maxLength) = x.Usage(maxLength)
  member x.FormatTypeArguments = String.concat ", " x.TypeArguments
  member x.FormatModifiers = String.concat " " x.Modifiers
  member x.FormatSourceLocation = defaultArg x.SourceLocation ""
  member x.FormatCompiledName = defaultArg x.CompiledName ""
  static member Create(usage, mods, typars, sign, location, compiledName) =
    { Usage = usage; Modifiers = mods; TypeArguments = typars;
      Signature = sign; SourceLocation = location; CompiledName = compiledName }

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
  | StaticParameter = 102

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
    Category :string
    UrlName : string
    Comment : Comment
    Assembly : AssemblyName

    UnionCases : Member list
    RecordFields : Member list
    StaticParameters : Member list

    AllMembers : Member list
    Constructors : Member list
    InstanceMembers : Member list
    StaticMembers : Member list }
  static member Create(name, cat, url, comment, assembly, cases, fields, statParams, ctors, inst, stat) =
    { Type.Name = name
      Category = cat
      UrlName = url
      Comment = comment
      Assembly = assembly
      UnionCases = cases
      RecordFields = fields
      StaticParameters = statParams
      AllMembers = List.concat [ ctors; inst; stat; cases; fields; statParams ]
      Constructors = ctors
      InstanceMembers = inst
      StaticMembers = stat }

type Module =
  { Name : string
    Category : string
    UrlName : string
    Comment : Comment
    Assembly : AssemblyName

    AllMembers : Member list

    NestedModules : Module list
    NestedTypes : Type list

    ValuesAndFuncs : Member list
    TypeExtensions : Member list
    ActivePatterns : Member list }
  static member Create(name, cat, url, comment, assembly, modules, types, vals, exts, pats) =
    { Module.Name = name; UrlName = url; Comment = comment; Assembly = assembly; Category = cat
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
    Assembly : AssemblyGroup
    Namespace : Namespace
    ParentModule : Module option }
  member this.HasParentModule = this.ParentModule.IsSome
  static member Create(modul, asm, ns, parent) =
    { ModuleInfo.Module = modul; Assembly = asm; Namespace = ns; ParentModule = parent }

type TypeInfo =
  { Type : Type
    Assembly : AssemblyGroup
    Namespace : Namespace
    ParentModule : Module option }
  member this.HasParentModule = this.ParentModule.IsSome
  static member Create(typ, asm, ns, modul) =
    { TypeInfo.Type = typ; Assembly = asm; Namespace = ns; ParentModule = modul }

/// [omit]
[<System.Runtime.CompilerServices.Extension>]
module ExtensionMethods =
   [<System.Runtime.CompilerServices.Extension>]
   let Exists(opt : Module option) = opt.IsSome

module ValueReader =
  open System.Collections.ObjectModel
  type CrefReference =
    { IsInternal : bool; ReferenceLink : string; NiceName : string }
  type IUrlHolder =
    abstract RegisterEntity : FSharpEntity -> unit
    abstract GetUrl : FSharpEntity -> string
    abstract ResolveCref : string -> CrefReference option

  type ReadingContext =
    { PublicOnly : bool
      Assembly : AssemblyName
      XmlMemberMap : IDictionary<string, XElement>
      UrlMap : IUrlHolder
      MarkdownComments : bool
      UrlRangeHighlight : Uri -> int -> int -> string
      SourceFolderRepository : (string * string) option
      AssemblyPath : string
      CompilerOptions : string
      FormatAgent : CodeFormatAgent }
    member x.XmlMemberLookup(key) =
      match x.XmlMemberMap.TryGetValue(key) with
      | true, v -> Some v
      | _ -> None
    static member Create
        ( publicOnly, assembly, map, sourceFolderRepo, urlRangeHighlight, markDownComments, urlMap,
          assemblyPath, compilerOptions, formatAgent ) =
      { PublicOnly=publicOnly;
        Assembly = assembly
        XmlMemberMap = map;
        MarkdownComments = markDownComments;
        UrlMap = urlMap;
        UrlRangeHighlight = urlRangeHighlight;
        SourceFolderRepository = sourceFolderRepo
        AssemblyPath = assemblyPath
        CompilerOptions = compilerOptions
        FormatAgent = formatAgent }

  let inline private getCompiledName (s : ^a when ^a :> FSharpSymbol) =
    let compiledName = (^a : (member CompiledName : string) (s))
    match compiledName = s.DisplayName with
    | true -> None
    | _    -> Some compiledName

  let formatSourceLocation (urlRangeHighlight : Uri -> int -> int -> string) (sourceFolderRepo : (string * string) option) (location : range option) =
    location |> Option.bind (fun location ->
        sourceFolderRepo |> Option.map (fun (baseFolder, repo) ->
            let basePath = Uri(Path.GetFullPath(baseFolder)).ToString()
            let docPath = Uri(Path.GetFullPath(location.FileName)).ToString()

            // Even though ignoring case might be wrong, we do that because
            // one path might be file:///C:\... and the other file:///c:\...  :-(
            if not <| docPath.StartsWith(basePath, StringComparison.InvariantCultureIgnoreCase) then
                Log.errorf "Current source file '%s' doesn't reside in source folder '%s'" docPath basePath
                ""
            else
                let relativePath = docPath.[basePath.Length..]
                let uriBuilder = UriBuilder(repo)
                uriBuilder.Path <- uriBuilder.Path + relativePath
                urlRangeHighlight uriBuilder.Uri location.StartLine location.EndLine ) )

  let (|AllAndLast|_|) (list:'T list)=
    if list.IsEmpty then None
    else let revd = List.rev list in Some(List.rev revd.Tail, revd.Head)

  let uncapitalize (s:string) =
    s.Substring(0, 1).ToLowerInvariant() + s.Substring(1)

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
    | _ when typ.HasTypeDefinition ->
        let tcref = typ.TypeDefinition
        let tyargs = typ.GenericArguments |> Seq.toList
        // layout postfix array types
        formatTypeApplication (formatTyconRef tcref) prec tcref.UsesPrefixDisplay tyargs
    | _ when typ.IsTupleType ->
        let tyargs = typ.GenericArguments |> Seq.toList
        bracketIf (prec <= 2) (formatTypesWithPrec 2 " * " tyargs)
    | _ when typ.IsFunctionType ->
        let rec loop soFar (typ:FSharpType) =
          if typ.IsFunctionType then
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
    let nm =
      match arg.Name with
      | None ->
          if arg.Type.HasTypeDefinition && arg.Type.TypeDefinition.XmlDocSig = "T:Microsoft.FSharp.Core.unit" then "()"
          else "arg" + string i
      | Some nm -> nm
    // Detect an optional argument
    let isOptionalArg = hasAttrib<OptionalArgumentAttribute> arg.Attributes
    let argName = if isOptionalArg then "?" + nm else nm
    if generateTypes then
      (match arg.Name with None -> "" | Some argName -> argName + ":") +
      formatTypeWithPrec 2 arg.Type
    else argName

  let formatArgsUsage generateTypes (v:FSharpMemberOrFunctionOrValue) args =
    let isItemIndexer = (v.IsInstanceMember && v.DisplayName = "Item")
    let counter = let n = ref 0 in fun () -> incr n; !n
    let unit, argSep, tupSep =
      if generateTypes then "unit", " -> ", " * "
      else "()", " ", ", "
    args
    |> List.map (List.map (fun x -> formatArgUsage generateTypes (counter()) x))
    |> List.map (function
        | [] -> unit
        | [arg] when arg = unit -> unit
        | [arg] when not v.IsMember || isItemIndexer -> arg
        | args when isItemIndexer -> String.concat tupSep args
        | args -> bracket (String.concat tupSep args))
    |> String.concat argSep

  let tryGetLocation (symbol: FSharpSymbol) =
    match symbol.ImplementationLocation with
    | Some loc -> Some loc
    | None -> symbol.DeclarationLocation

  let readMemberOrVal (ctx:ReadingContext) (v:FSharpMemberOrFunctionOrValue) =
    // we calculate this early just in case this fails with an FCS error.
    let requireQualifiedAccess =
        hasAttrib<RequireQualifiedAccessAttribute> v.LogicalEnclosingEntity.Attributes

    let buildUsage (args:string option) =
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
      | false, _, _, name when not <| requireQualifiedAccess ->
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
        match argInfos, v.IsPropertyGetterMethod || v.HasGetterMethod, v.IsPropertySetterMethod || v.HasSetterMethod with
        | [ AllAndLast(args, last) ], _, true -> [ args ], Some last.Type
        | _, _, true -> argInfos, None
        | [[]], true, _ -> [], Some retType
        | _, _, _ -> argInfos, Some retType

    // Extension members can have apparent parents which are not F# types.
    // Hence getting the generic argument count if this is a little trickier
    let numGenericParamsOfApparentParent =
        let pty = v.LogicalEnclosingEntity
        //if pty.IsExternal then
        //    let ty = v.LogicalEnclosingEntity.ReflectionType
        //    if ty.IsGenericType then ty.GetGenericArguments().Length
        //    else 0
        //else
        pty.GenericParameters.Count
    // Ensure that there is enough number of elements to skip
    let tps = v.GenericParameters |> Seq.skip (min v.GenericParameters.Count numGenericParamsOfApparentParent)
    let typars = formatTypeArguments tps

    //let cxs  = indexedConstraints v.GenericParameters
    let retType = defaultArg (retType |> Option.map formatType) "unit"
    let signature =
      match argInfos with
      | [] -> retType
      | [[x]] when (v.IsPropertyGetterMethod || v.HasGetterMethod) && x.Name.IsNone && x.Type.TypeDefinition.XmlDocSig = "T:Microsoft.FSharp.Core.unit" -> retType
      | _  -> (formatArgsUsage true v argInfos) + " -> " + retType

    let usage =
      match argInfos with
      | [[x]] when (v.IsPropertyGetterMethod || v.HasGetterMethod) && x.Name.IsNone && x.Type.TypeDefinition.XmlDocSig = "T:Microsoft.FSharp.Core.unit" -> ""
      | _  -> formatArgsUsage false v argInfos

    let buildShortUsage length =
      let long = buildUsage (Some usage)
      if long.Length <= length then long
      else buildUsage None
    // If there is a signature file, we should go for implementation file
    let loc = tryGetLocation v
    let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
    MemberOrValue.Create(buildShortUsage, modifiers, typars, signature, location, getCompiledName v)

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

  let readUnionCase (ctx:ReadingContext) (case:FSharpUnionCase) =
    let formatFieldUsage (field:FSharpField) =
        if field.Name.StartsWith("Item") then
            formatType field.FieldType
        else
            field.Name
    let fields = case.UnionCaseFields |> List.ofSeq
    let buildUsage maxLength fields =
        let long = "(" + (fields |> List.map formatFieldUsage |> String.concat ",") + ")"
        match long.Length with
        | x when x <= 2 -> ""
        | x when x <= maxLength -> long
        | _ -> "(...)"
    let usage (maxLength:int) = case.Name + buildUsage (maxLength - case.Name.Length) fields
    let modifiers = List.empty
    let typeparams = List.empty
    let signature = fields |> List.map (fun field -> formatType field.FieldType) |> String.concat " * "
    let loc = tryGetLocation case
    let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
    MemberOrValue.Create(usage, modifiers, typeparams, signature, location, getCompiledName case)

  let readFSharpField (ctx:ReadingContext) (field:FSharpField) =
    let usage (maxLength:int) = field.Name
    let modifiers =
      [ if field.IsMutable then yield "mutable"
        if field.IsStatic then yield "static" ]
    let typeparams = List.empty
    let signature = formatType field.FieldType
    let loc = tryGetLocation field
    let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
    MemberOrValue.Create(usage, modifiers, typeparams, signature, location, if field.Name <> field.DisplayName then Some field.Name else None)

  let getFSharpStaticParamXmlSig (typeProvider:FSharpEntity) parameterName =
    "SP:" + typeProvider.AccessPath + "." + typeProvider.LogicalName + "." + parameterName

  let readFSharpStaticParam (ctx:ReadingContext) (staticParam:FSharpStaticParameter) =
    let usage (maxLength:int) = staticParam.Name
    let modifiers = List.empty
    let typeparams = List.empty
    let signature = formatType staticParam.Kind + (if staticParam.IsOptional then sprintf " (optional, default = %A)" staticParam.DefaultValue else "")
    let loc = tryGetLocation staticParam
    let location = formatSourceLocation ctx.UrlRangeHighlight ctx.SourceFolderRepository loc
    MemberOrValue.Create(usage, modifiers, typeparams, signature, location, if staticParam.Name <> staticParam.DisplayName then Some staticParam.Name else None)

module Reader =
  open FSharp.Markdown
  open FSharp.Literate
  open System.IO
  open ValueReader

  // ----------------------------------------------------------------------------------------------
  // Helper functions
  // ----------------------------------------------------------------------------------------------

  let removeSpaces (comment:string) =
    use reader = new StringReader(comment)
    let lines =
      [ let line = ref ""
        while (line := reader.ReadLine(); line.Value <> null) do
          yield line.Value ]
    String.removeSpaces lines

  let readMarkdownComment (doc:LiterateDocument) =
    let groups = System.Collections.Generic.Dictionary<_, _>()
    let mutable current = "<default>"
    groups.Add(current, [])
    for par in doc.Paragraphs do
      match par with
      | Heading(2, [Literal(text, _)], _) ->
          current <- text.Trim()
          groups.Add(current, [par])
      | par ->
          groups.[current] <- par::groups.[current]
    let blurb = Literate.WriteHtml(doc.With(List.rev groups.["<default>"]))
    let full = Literate.WriteHtml(doc)

    let sections =
      [ for (KeyValue(k, v)) in groups ->
          let body = if k = "<default>" then List.rev v else List.tail (List.rev v)
          let html = Literate.WriteHtml(doc.With(body))
          KeyValuePair(k, html) ]
    Comment.Create(blurb, full, sections)

  let findCommand = (function
    | StringPosition.StartsWithWrapped ("[", "]") (ParseCommand(k, v), rest) ->
        Some (k, v)
    | _ -> None)

  let readXmlComment (urlMap : IUrlHolder) (doc : XElement) (cmds: IDictionary<_, _>)=

   let full = new StringBuilder()
   let rec readElement (e : XElement) =
     Seq.iter (fun (x : XNode) ->
      if x.NodeType = XmlNodeType.Text then
       let text = (x :?> XText).Value
       match findCommand (text, MarkdownRange.zero) with
       | Some (k,v) -> cmds.Add(k,v)
       | None -> full.Append(text) |> ignore
      elif x.NodeType = XmlNodeType.Element then
        let elem = x :?> XElement
        match elem.Name.LocalName with
        | "list" ->
          full.Append("<ul>") |> ignore
          readElement elem
          full.Append("</ul>") |> ignore
        | "item" ->
          full.Append("<li>") |> ignore
          full.Append(elem.Value) |> ignore
          full.Append("</li>") |> ignore
        | "para" ->
          full.Append("<p class='para'>") |> ignore
          full.Append(elem.Value) |> ignore
          full.Append("</p>") |> ignore
        | "see"
        | "seealso" ->
           let cref = elem.Attribute(XName.Get "cref")
           if cref <> null then
            if System.String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
              failwithf "Invalid cref specified in: %A" doc
            match urlMap.ResolveCref cref.Value with
            | Some (reference) ->
              full.AppendFormat("<a href=\"{0}\">{1}</a>", reference.ReferenceLink, reference.NiceName) |> ignore
            | _ ->
              full.AppendFormat("UNRESOLVED({0})", cref.Value) |> ignore
        | _ ->
          ()
       ) (e.Nodes())
   readElement doc
   full.Append("</br>") |> ignore

   for e in doc.Descendants(XName.Get "summary") do
     full.Append("<p class='summary'>") |> ignore
     readElement e
     full.Append("</p>") |> ignore

   let parameters = doc.Descendants(XName.Get "params")

   if Seq.length parameters > 0 then
     full.Append("<h2>Parameters</h2>") |> ignore
     full.Append("<dl>") |> ignore
     for e in parameters do
       let name = e.Attribute(XName.Get "name").Value
       let description = e.Value
       full.AppendFormat("<dt><span class='parameter'>{0}</span></dt><dd><p>{1}</p></dd>", name, description) |> ignore
     full.Append("</dl>") |> ignore

   for e in doc.Descendants(XName.Get "returns") do
     full.Append("<p class='returns'>") |> ignore
     let description = e.Value
     full.AppendFormat("Returns: {0}",description) |> ignore
     full.Append("</p>") |> ignore

   let exceptions = doc.Descendants(XName.Get "exceptions")
   if Seq.length exceptions > 0 then
     full.Append("<h2>Exceptions</h2>") |> ignore
     full.Append("<table>") |> ignore
     for e in exceptions do
       let cref = e.Attribute(XName.Get "cref")
       if cref <> null then
        if System.String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
          failwithf "Invalid cref specified in: %A" doc
        match urlMap.ResolveCref cref.Value with
        | Some (reference) ->
          full.AppendFormat("<tr><td><a href=\"{0}\">{1}</a></td><td>{2}</td></tr>", reference.ReferenceLink, reference.NiceName,e.Value) |> ignore
        | _ ->
          full.AppendFormat("<tr><td>UNRESOLVED({0})</td><td></td></tr>", cref.Value) |> ignore
     full.Append("</table>") |> ignore

   let remarks = doc.Descendants(XName.Get "remarks")
   if Seq.length remarks > 0 then
     full.Append("<h2>Remarks</h2>") |> ignore
     for e in remarks do
       full.Append("<p class='remarks'>") |> ignore
       readElement e
       full.Append("</p>") |> ignore

   // TODO: process param, returns tags, note that given that FSharp.Formatting infers the signature
   // via reflection this tags are not so important in F#
   let str = full.ToString()
   Comment.Create(str, str, [KeyValuePair("<default>", str)])

  /// Returns all indirect links in a specified span node
  let rec collectSpanIndirectLinks span = seq {
    match span with
    | IndirectLink (_, _, key, _) -> yield key
    | Matching.SpanLeaf _ -> ()
    | Matching.SpanNode(_, spans) ->
      for s in spans do yield! collectSpanIndirectLinks s }

  /// Returns all indirect links in the specified paragraph node
  let rec collectParagraphIndirectLinks par = seq {
    match par with
    | Matching.ParagraphLeaf _ -> ()
    | Matching.ParagraphNested(_, pars) ->
      for ps in pars do
        for p in ps do yield! collectParagraphIndirectLinks p
    | Matching.ParagraphSpans(_, spans) ->
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
    | Matching.ParagraphLeaf _ -> para
    | Matching.ParagraphNested(info, pars) ->
        Matching.ParagraphNested(info, pars |> List.map (fun innerPars -> List.map (wrapInlineCodeLinksInParagraphs ctx) innerPars))
    | Matching.ParagraphSpans(info, spans) -> Matching.ParagraphSpans(info, List.map (wrapInlineCodeLinksInSpans ctx) spans)

  /// Adds the missing links to types to the document defined links
  let addMissingLinkToTypes ctx (doc: LiterateDocument) =
    let replacedParagraphs = doc.Paragraphs |> List.map (wrapInlineCodeLinksInParagraphs ctx)

    do replacedParagraphs
    |> Seq.collect collectParagraphIndirectLinks
    |> Seq.filter (linkNotDefined doc)
    |> Seq.map (getTypeLink ctx)
    |> Seq.iter (addLinkToType doc)

    LiterateDocument(replacedParagraphs, doc.FormattedTips, doc.DefinedLinks, doc.Source, doc.SourceFile, doc.Errors)

  let readCommentAndCommands (ctx:ReadingContext) xmlSig =
    match ctx.XmlMemberLookup(xmlSig) with
    | None ->
        if not (System.String.IsNullOrEmpty xmlSig) then
            Log.verbf "Could not find documentation for '%s'! (You can ignore this message when you have not written documentation for this member)" xmlSig
        dict[], Comment.Empty
    | Some el ->
        let sum = el.Element(XName.Get "summary")
        match sum with
        | null when String.IsNullOrEmpty el.Value ->
          dict[], Comment.Empty
        | null ->
          dict[], (Comment.Create ("", el.Value, []))
        | sum ->
          let lines = removeSpaces sum.Value |> Seq.map (fun s -> (s, MarkdownRange.zero))
          let cmds = new System.Collections.Generic.Dictionary<_, _>()

          if ctx.MarkdownComments then
            let text =
              lines |> Seq.filter (findCommand >> (function
                  | Some (k, v) ->
                      cmds.Add(k, v)
                      false
                  | _ -> true)) |> Seq.map fst |> String.concat "\n"
            let doc =
                Literate.ParseMarkdownString
                  ( text, path=Path.Combine(ctx.AssemblyPath, "docs.fsx"),
                    formatAgent=ctx.FormatAgent, compilerOptions=ctx.CompilerOptions )
                  |> (addMissingLinkToTypes ctx)
            cmds :> IDictionary<_, _>, readMarkdownComment doc
          else
            lines
              |> Seq.choose findCommand
              |> Seq.iter (fun (k, v) -> cmds.Add(k,v))
            cmds :> IDictionary<_, _>, readXmlComment ctx.UrlMap el cmds

  let readComment ctx xmlSig = readCommentAndCommands ctx xmlSig |> snd


  // -----------------------------------------------------------------------
  // Hack for getting the xmldoc signature from C# assemblies
  // FSC consistently returns emtpy strings in .XmlDocSig properties
  // and as such Fsharp.formatting is unable to look up the correct
  // comment.

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
    let memberName =
      try
        let name = memb.CompiledName.Replace(".ctor", "#ctor")
        let typeGenericParameters =
            memb.EnclosingEntity.GenericParameters |> Seq.mapi (fun num par -> par.Name, sprintf "`%d" num)
        let methodGenericParameters =
            memb.GenericParameters |> Seq.mapi (fun num par -> par.Name, sprintf "``%d" num)
        let typeArgsMap =
            Seq.append methodGenericParameters typeGenericParameters
            |> Seq.groupBy fst
            |> Seq.map (fun (name, grp) -> grp |> Seq.head)
            |> dict
        let typeargs =
            if memb.GenericParameters.Count > 0
            then sprintf "``%d" memb.GenericParameters.Count
            else ""

        let paramList =
            if memb.CurriedParameterGroups.Count > 0 && memb.CurriedParameterGroups.[0].Count > 0
            then
                let head = memb.CurriedParameterGroups.[0]
                let paramTypeList =
                    head
                    |> Seq.map (fun param ->
                        if param.Type.IsGenericParameter then
                            typeArgsMap.[param.Type.GenericParameter.Name]
                        else
                            let rec reduceAbb (t:FSharpType) =
                                if t.IsAbbreviation
                                then reduceAbb t.AbbreviatedType
                                else t
                            let paramType = reduceAbb param.Type
                            paramType.TypeDefinition.FullName)
                "(" + System.String.Join(",", paramTypeList) + ")"
            else ""
        sprintf "%s%s%s" name typeargs paramList
      with exn ->
        Log.errorf "Error while building member-name for %s because: %s" memb.FullName exn.Message
        Log.verbf "Full Exception details of previous message: %O" exn
        memb.CompiledName
    match (memb.XmlDocSig, memb.EnclosingEntity.TryFullName) with
    | "",  None    -> ""
    | "", Some(n)  -> sprintf "%s:%s.%s" (getMemberXmlDocsSigPrefix memb)  n memberName
    | n, _         -> n

  //
  // ---------------------------------------------------------------------



  let getTypeProviderXmlSig (typ:FSharpEntity) =
    "T:" + typ.AccessPath + "." + typ.LogicalName

  let createUrlHolder ()=
    let toReplace =
        ([(".", "-"); ("`", "-"); ("<", "_"); (">", "_"); (" ", "_"); ("#", "_")] @
            (Path.GetInvalidPathChars()
            |> Seq.append (Path.GetInvalidFileNameChars())
            |> Seq.map (fun inv -> (inv.ToString(), "_")) |> Seq.toList))
        |> Seq.distinctBy fst
        |> Seq.toList
    let usedNames = Dictionary<_, _>()
    let registeredEntities = Dictionary<_, _>()
    let entityLookup = Dictionary<_, _>()
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
        registeredEntities.[entity] <- newName
        let xmlsig = getXmlDocSigForType entity
        if (not (System.String.IsNullOrEmpty xmlsig)) then
            assert (xmlsig.StartsWith("T:"))
            entityLookup.[xmlsig.Substring(2)] <- entity
            if (not(niceNameEntityLookup.ContainsKey(entity.LogicalName))) then
                niceNameEntityLookup.[entity.LogicalName] <- List<_>()
            niceNameEntityLookup.[entity.LogicalName].Add(entity)
        for nested in entity.NestedEntities do registerEntity nested

    let getUrl (entity:FSharpEntity) =
        match registeredEntities.TryGetValue (entity) with
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
    let getNoNamespaceMemberName keepParts (memberNameNoParen:string) =
        let splits = memberNameNoParen.Split([|'.'|])
        let noNamespaceParts =
            if splits.Length > keepParts then
                Array.sub splits (splits.Length - keepParts) keepParts
            else splits
        System.String.Join(".", noNamespaceParts)

    let lookupTypeCref typeName =
        match entityLookup.TryGetValue(typeName) with
        | true, entity ->
            Some { IsInternal = true; ReferenceLink = sprintf "%s.html" (getUrl entity); NiceName = entity.LogicalName }
        | _ ->
            match niceNameEntityLookup.TryGetValue(typeName) with
            | true, entityList ->
                if entityList.Count = 1 then
                    Some{ IsInternal = true; ReferenceLink = sprintf "%s.html" (getUrl entityList.[0]); NiceName = entityList.[0].LogicalName }
                else
                    if entityList.Count > 1 then
                        do Log.warnf "Duplicate types found for the simple name: %s" typeName
                    None
            | _ -> None

    let resolveCref (cref:string) =
        if (cref.Length < 2) then invalidArg "cref" (sprintf "the given cref: '%s' is invalid!" cref)
        let memberName = cref.Substring(2)
        let noParen = removeParen memberName
        match cref with
        // Type
        | _ when cref.StartsWith("T:") ->
            match lookupTypeCref (memberName) with
            | Some ref -> Some ref
            | None ->
                let simple = getNoNamespaceMemberName 1 noParen
                Some { IsInternal = false;
                       ReferenceLink = sprintf "http://msdn.microsoft.com/en-us/library/%s" noParen;
                       NiceName = simple }
        // Compiler was unable to resolve!
        | _ when cref.StartsWith("!:")  ->
            Log.warnf "Compiler was unable to resolve %s" cref
            None
        // Member
        | _ when cref.[1] = ':' ->
            let simple = getNoNamespaceMemberName 2 noParen
            match tryGetTypeFromMemberName memberName with
            | Some typeName ->
                match lookupTypeCref typeName with
                | Some reference -> Some { reference with NiceName = simple }
                | None ->
                    Some { IsInternal = false;
                           ReferenceLink = sprintf "http://msdn.microsoft.com/en-us/library/%s" noParen;
                           NiceName = simple }
            | None ->
                Log.warnf "Assumed '%s' was a member but we cannot extract a type!" cref
                None
        // No idea
        | _ ->
            Log.warnf "Unresolved reference '%s'!" cref
            None
    { new IUrlHolder with
        member x.RegisterEntity entity =
            registerEntity entity
        member x.GetUrl entity = getUrl entity
        member x.ResolveCref cref = resolveCref cref
    }

  // ----------------------------------------------------------------------------------------------
  // Reading entities
  // ----------------------------------------------------------------------------------------------

  /// Reads XML documentation comments and calls the specified function
  /// to parse the rest of the entity, unless [omit] command is set.
  /// The function is called with category name, commands & comment.
  let readCommentsInto (sym:FSharpSymbol) ctx xmlDoc f =
    let cmds, comment = readCommentAndCommands ctx xmlDoc
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
                  | Some f -> f
                  | None -> ass.QualifiedName
                with _ -> "unknown"
              sprintf "unknown, part of %s" part
        Log.errorf "Could not read comments from entity '%s': %O" name e
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

  let tryReadMember (ctx:ReadingContext) kind (memb:FSharpMemberOrFunctionOrValue) =
    readCommentsInto memb ctx (getXmlDocSigForMember memb) (fun cat _ comment ->
      Member.Create(memb.DisplayName, kind, cat, readMemberOrVal ctx memb, comment))

  let readAllMembers ctx kind (members:seq<FSharpMemberOrFunctionOrValue>) =
    members
    |> Seq.filter (fun v -> checkAccess ctx v.Accessibility)
    |> Seq.filter (fun v -> not v.IsCompilerGenerated && not v.IsPropertyGetterMethod && not v.IsPropertyGetterMethod)
    |> Seq.choose (tryReadMember ctx kind) |> List.ofSeq

  let readMembers ctx kind (entity:FSharpEntity) cond =
    entity.MembersFunctionsAndValues
    |> Seq.filter (fun v -> checkAccess ctx v.Accessibility)
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
    |> List.filter (fun v -> checkAccess ctx v.Accessibility)
    |> List.choose (fun case ->
      readCommentsInto case ctx case.XmlDocSig (fun cat _ comment ->
        Member.Create(case.Name, MemberKind.UnionCase, cat, readUnionCase ctx case, comment)))

  let readRecordFields ctx (typ:FSharpEntity) =
    typ.FSharpFields
    |> List.ofSeq
    |> List.filter (fun field -> not field.IsCompilerGenerated)
    |> List.choose (fun field ->
      readCommentsInto field ctx field.XmlDocSig (fun cat _ comment ->
        Member.Create(field.Name, MemberKind.RecordField, cat, readFSharpField ctx field, comment)))

  let readStaticParams ctx (typ:FSharpEntity) =
    typ.StaticParameters
    |> List.ofSeq
    |> List.choose (fun staticParam ->
      readCommentsInto staticParam ctx (getFSharpStaticParamXmlSig typ staticParam.Name) (fun cat _ comment ->
        Member.Create(staticParam.Name, MemberKind.StaticParameter, cat, readFSharpStaticParam ctx staticParam, comment)))

  // ----------------------------------------------------------------------------------------------
  // Reading modules types (mutually recursive, because of nesting)
  // ----------------------------------------------------------------------------------------------


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
    |> Seq.choose (fun p -> let nameAttr = p.Attribute(XName.Get "name")
                            if nameAttr = null
                            then None
                            else Some (nameAttr.Value, p.Value))
    |> Seq.iter (fun (name, xmlDoc) ->
        let xmlDocSig = getFSharpStaticParamXmlSig typ name
        registerXmlDoc ctx xmlDocSig (Security.SecurityElement.Escape xmlDoc) |> ignore)

  let rec readModulesAndTypes ctx (entities:seq<_>) =
    let modules = readChildren ctx entities readModule (fun x -> x.IsFSharpModule)
    let types = readChildren ctx entities readType (fun x -> not x.IsFSharpModule)
    modules, types

  and readType (ctx:ReadingContext) (typ:FSharpEntity) =
    if typ.IsProvided && typ.XmlDoc.Count > 0 then
        registerTypeProviderXmlDocs ctx typ
    let xmlDocSig = getXmlDocSigForType typ
    readCommentsInto typ ctx xmlDocSig (fun cat cmds comment ->
      let urlName = ctx.UrlMap.GetUrl typ

      let rec getMembers (typ:FSharpEntity) = [
        yield! typ.MembersFunctionsAndValues
        match typ.BaseType with
        | Some baseType ->
            //TODO: would be better to reuse instead of reparsing the base type xml docs
            let cmds, comment = readCommentAndCommands ctx (getXmlDocSigForType baseType.TypeDefinition)
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
            if v.EnclosingEntity.IsFSharp then true else
                not v.IsEventAddMethod && not v.IsEventRemoveMethod &&
                not v.IsPropertyGetterMethod && not v.IsPropertySetterMethod)
          |> List.partition (fun v -> v.IsInstanceMember)
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
      let statParams = readStaticParams ctx typ

      let ctors = readAllMembers ctx MemberKind.Constructor cvals
      let inst = readAllMembers ctx MemberKind.InstanceMember ivals
      let stat = readAllMembers ctx MemberKind.StaticMember svals

      Type.Create
        ( name, cat, urlName, comment, ctx.Assembly, cases, fields, statParams, ctors, inst, stat ))

  and readModule (ctx:ReadingContext) (modul:FSharpEntity) =
    readCommentsInto modul ctx modul.XmlDocSig (fun cat cmd comment ->

      // Properties & value bindings in the module
      let urlName = ctx.UrlMap.GetUrl modul
      let vals = readMembers ctx MemberKind.ValueOrFunction modul (fun v -> not v.IsMember && not v.IsActivePattern)
      let exts = readMembers ctx MemberKind.TypeExtension modul (fun v -> v.IsExtensionMember)
      let pats = readMembers ctx MemberKind.ActivePattern modul (fun v -> v.IsActivePattern)

      // Nested modules and types
      let modules, types = readModulesAndTypes ctx modul.NestedEntities

      Module.Create
        ( modul.DisplayName, cat, urlName, comment, ctx.Assembly,
          modules, types,
          vals, exts, pats ))

  // ----------------------------------------------------------------------------------------------
  // Reading namespace and assembly details
  // ----------------------------------------------------------------------------------------------

  let readNamespace ctx (ns, entities:seq<FSharpEntity>) =
    let modules, types = readModulesAndTypes ctx entities
    Namespace.Create(ns, modules, types)

  let readAssembly (assembly:FSharpAssembly, publicOnly, xmlFile:string, sourceFolderRepo, urlRangeHighlight, markDownComments, urlMap, codeFormatCompilerArgs) =
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
    let formatAgent = CodeFormat.CreateAgent()
    let ctx =
      ReadingContext.Create
        ( publicOnly, assemblyName, xmlMemberMap, sourceFolderRepo, urlRangeHighlight,
          markDownComments, urlMap, asmPath, codeFormatCompilerArgs, formatAgent)

    //
    let namespaces =
      assembly.Contents.Entities
      |> Seq.filter (fun modul -> checkAccess ctx modul.Accessibility)
      |> Seq.groupBy (fun modul -> modul.AccessPath)
      |> Seq.sortBy fst
      |> Seq.map (readNamespace ctx)
      |> List.ofSeq

    assemblyName, namespaces

// ------------------------------------------------------------------------------------------------
// Main - generating HTML
// ------------------------------------------------------------------------------------------------

open System.IO

type GeneratorOutput = {
  AssemblyGroup      : AssemblyGroup
  ModuleInfos        : ModuleInfo list
  TypesInfos         : TypeInfo list
  Properties         : (string * IDictionary<string, string>) list
}

/// For use in the tempaltes (lives in namespace FSharp.MetadataFormat)
type Html private() =
  static let mutable uniqueNumber = 0
  static member UniqueID() =
    uniqueNumber <- uniqueNumber + 1
    uniqueNumber
  static member Encode(str) =
    System.Web.HttpUtility.HtmlEncode(str)

/// This type exposes the functionality for producing documentation model from `dll` files with associated `xml` files
/// generated by the F# or C# compiler. To generate documentation model, use one of the overloades of the `Generate` method.
/// The overloads have the following optional parameters:
///
///  - `parameters` - provides additional parameters to the Razor templates
///  - `xmlFile` - can be used to override the default name of the XML file (by default, we assume
///     the file has the same name as the DLL)
///  - `markDownComments` - specifies if you want to use the Markdown parser for in-code comments.
///    With `markDownComments` enabled there is no support for `<see cref="">` links, so `false` is
///    recommended for C# assemblies (if not specified, `true` is used).
///  - `sourceFolder` and `sourceRepo` - When specified, the documentation generator automatically
///    generates links to GitHub pages for each of the entity.
///  - `publicOnly` - When set to `false`, the tool will also generate documentation for non-public members
///  - `libDirs` - Use this to specify additional paths where referenced DLL files can be found
///  - `otherFlags` - Additional flags that are passed to the F# compiler (you can use this if you want to
///    specify references explicitly etc.)
///  - `urlRangeHighlight` - A function that can be used to override the default way of generating GitHub links
///
type MetadataFormat =
  /// This overload generates documentation for a single file specified by the `dllFile` parameter
  static member Generate(dllFile : string, ?parameters, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight) =
    MetadataFormat.Generate
      ( Seq.singleton dllFile, ?parameters = parameters, ?xmlFile = xmlFile, ?sourceRepo = sourceRepo, ?sourceFolder = sourceFolder,
        ?publicOnly = publicOnly, ?libDirs = libDirs, ?otherFlags = otherFlags, ?markDownComments = markDownComments, ?urlRangeHighlight = urlRangeHighlight)

  /// generates documentation for multiple files specified by the `dllFiles` parameter
  static member Generate(dllFiles : string list, ?parameters, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight) =
    MetadataFormat.Generate
      ( (dllFiles :> _ seq), ?parameters = parameters, ?xmlFile = xmlFile, ?sourceRepo = sourceRepo, ?sourceFolder = sourceFolder,
        ?publicOnly = publicOnly, ?libDirs = libDirs, ?otherFlags = otherFlags, ?markDownComments = markDownComments, ?urlRangeHighlight = urlRangeHighlight)

  /// This overload generates documentation for multiple files specified by the `dllFiles` parameter
  static member Generate(dllFiles : string seq, ?parameters, ?xmlFile, ?sourceRepo, ?sourceFolder, ?publicOnly, ?libDirs, ?otherFlags, ?markDownComments, ?urlRangeHighlight) =
    let (@@) a b = Path.Combine(a, b)
    let parameters = defaultArg parameters []
    let props = [ "Properties", dict parameters ]

    // Default template file names

    let otherFlags = defaultArg otherFlags []
    let publicOnly = defaultArg publicOnly true
    let libDirs = defaultArg libDirs [] |> List.map Path.GetFullPath
    let dllFiles = dllFiles |> List.ofSeq |>  List.map Path.GetFullPath
    let urlRangeHighlight =
      defaultArg urlRangeHighlight (fun url start stop -> String.Format("{0}#L{1}-{2}", url, start, stop))
    let sourceFolderRepo =
        match sourceFolder, sourceRepo with
        | Some folder, Some repo -> Some(folder, repo)
        | Some folder, _ ->
            Log.warnf "Repository url should be specified along with source folder."
            None
        | _, Some repo ->
            Log.warnf "Source folder should be specified along with repository url."
            None
        | _ -> None

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
              Log.errorf "Couldn't load Assembly\n%s\n%s" e.Message e.StackTrace 
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
        //FSharpAssembly.LoadFiles(dllFiles, libDirs, otherFlags = otherFlags)
        FSharpAssembly.LoadFiles(dllFiles, libDirs, otherFlags = otherFlags,manualResolve=true)
        |> Seq.toList

      // generate the names for the html files beforehand so we can resolve <see cref=""/> links.
      let urlMap = Reader.createUrlHolder()

      resolvedList |> Seq.iter (function
        | _, Some asm ->
          asm.Contents.Entities |> Seq.iter (urlMap.RegisterEntity)
        | _ -> ())

      resolvedList |> List.choose (function
        | dllFile, None ->
          Log.warnf "**** Skipping assembly '%s' because was not found in resolved assembly list" dllFile
          None
        | dllFile, Some asm ->
          let xmlFile = defaultArg xmlFile (Path.ChangeExtension(dllFile, ".xml"))
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

          match xmlFileOpt with
          | None -> raise <| FileNotFoundException(sprintf "Associated XML file '%s' was not found." xmlFile)
          | Some xmlFile ->
            Reader.readAssembly
              ( asm, publicOnly, xmlFile, sourceFolderRepo, urlRangeHighlight,
                defaultArg markDownComments true, urlMap, codeFormatCompilerArgs )
            |> Some)
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
    let asm = AssemblyGroup.Create(name, List.map fst assemblies, namespaces |> List.sortBy (fun ns -> ns.Name))

    let rec nestedModules ns parent (modul:Module) = seq {
      yield ModuleInfo.Create(modul, asm, ns, parent)
      for n in modul.NestedModules do yield! nestedModules ns (Some modul) n }
    let moduleInfos =
      [ for ns in asm.Namespaces do
          for n in ns.Modules do yield! nestedModules ns None n ]

    let createType ns modul typ =
        TypeInfo.Create(typ, asm, ns, modul)

    let rec nestedTypes ns (modul:Module) = seq {
      yield! (modul.NestedTypes |> List.map (createType ns (Some modul) ))
      for n in modul.NestedModules do yield! nestedTypes ns n }
    let typesInfos =
      [ for ns in asm.Namespaces do
          for n in ns.Modules do yield! nestedTypes ns n
          yield! (ns.Types |> List.map (createType ns None ))  ]

    {
      AssemblyGroup = asm
      ModuleInfos = moduleInfos
      TypesInfos = typesInfos
      Properties = props
    }

