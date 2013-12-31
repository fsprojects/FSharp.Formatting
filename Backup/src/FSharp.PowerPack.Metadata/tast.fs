// (c) Microsoft Corporation. Apache 2.0 License
  
module internal Microsoft.FSharp.Metadata.Reader.Internal.Tast

open System.Collections.Generic 
open Microsoft.FSharp.Metadata.Reader.Internal.AbstractIL.IL
open Microsoft.FSharp.Metadata.Reader.Internal.PrettyNaming
open Microsoft.FSharp.Metadata.Reader.Internal.Prelude

/// Unique name generator for stamps attached to lambdas and object expressions
type uniq = int64
let newUnique = let i = ref 0L in fun () -> i := !i + 1L; !i
type stamp = int64

/// Unique name generator for stamps attached to to val_specs, tycon_specs etc.
let newStamp = let i = ref 0L in fun () -> i := !i + 1L; !i

type StampMap<'T> = Map<stamp,'T>

//-------------------------------------------------------------------------
// Flags

type ValInlineInfo =
    /// Indicates the value must always be inlined 
    | PseudoValue 
    /// Indictes the value is inlined but the code for the function still exists, e.g. to satisfy interfaces on objects, but that it is also always inlined 
    | AlwaysInline 
    | OptionalInline
    | NeverInline

let mustinline = function PseudoValue | AlwaysInline -> true | OptionalInline | NeverInline -> false

type ValRecursiveScopeInfo =
    /// Set while the value is within its recursive scope. The flag indicates if the value has been eagerly generalized and accepts generic-recursive calls 
    | ValInRecScope of bool
    /// The normal value for this flag when the value is not within its recursive scope 
    | ValNotInRecScope

type ValMutability   = 
    | Immutable 
    | Mutable

type TyparDynamicReq = 
    /// Indicates the type parameter is not needed at runtime and may be eliminated
    | NoDynamicReq 
    /// Indicates the type parameter is needed at runtime and may not be eliminated
    | DynamicReq

type ValBaseOrThisInfo = 
    /// Indicates a ref-cell holding 'this' or the implicit 'this' used throughout an 
    /// implicit constructor to access and set values
    | CtorThisVal 
    /// Indicates the value called 'base' available for calling base class members
    | BaseVal 
    /// Indicates a normal value
    | NormalVal 
    /// Indicates the 'this' value specified in a memberm e.g. 'x' in 'member x.M() = 1'
    | MemberThisVal

//---------------------------------------------------------------------------
// Flags on values
//---------------------------------------------------------------------------

[<Struct>]
type ValFlags(flags:int64) = 

    new (recValInfo, baseOrThis, isCompGen, inlineInfo, isMutable, isModuleOrMemberBinding, isExtensionMember, isIncrClassSpecialMember, isTyFunc, allowTypeInst) =
        let flags = 
                     (match baseOrThis with
                                        | BaseVal ->           0b000000000000000000L
                                        | CtorThisVal ->       0b000000000000000010L
                                        | NormalVal ->         0b000000000000000100L
                                        | MemberThisVal ->     0b000000000000000110L) |||
                     (if isCompGen then                        0b000000000000001000L 
                      else                                     0b000000000000000000L) |||
                     (match inlineInfo with
                                        | PseudoValue ->       0b000000000000000000L
                                        | AlwaysInline ->      0b000000000000010000L
                                        | OptionalInline ->    0b000000000000100000L
                                        | NeverInline ->       0b000000000000110000L) |||
                     (match isMutable with
                                        | Immutable ->         0b000000000000000000L
                                        | Mutable   ->         0b000000000001000000L) |||

                     (match isModuleOrMemberBinding with
                                        | false     ->         0b000000000000000000L
                                        | true      ->         0b000000000010000000L) |||
                     (match isExtensionMember with
                                        | false     ->         0b000000000000000000L
                                        | true      ->         0b000000000100000000L) |||
                     (match isIncrClassSpecialMember with
                                        | false     ->         0b000000000000000000L
                                        | true      ->         0b000000001000000000L) |||
                     (match isTyFunc with
                                        | false     ->         0b000000000000000000L
                                        | true      ->         0b000000010000000000L) |||

                     (match recValInfo with
                                     | ValNotInRecScope     -> 0b000000000000000000L
                                     | ValInRecScope(true)  -> 0b000000100000000000L
                                     | ValInRecScope(false) -> 0b000001000000000000L) |||

                     (match allowTypeInst with
                                        | false     ->         0b000000000000000000L
                                        | true      ->         0b000100000000000000L)

        ValFlags(flags)

    member x.BaseOrThisInfo = 
                                  match (flags       &&&       0b000000000000000110L) with 
                                                             | 0b000000000000000000L -> BaseVal
                                                             | 0b000000000000000010L -> CtorThisVal
                                                             | 0b000000000000000100L -> NormalVal
                                                             | 0b000000000000000110L -> MemberThisVal
                                                             | _          -> failwith "unreachable"



    member x.IsCompilerGenerated =      (flags       &&&       0b000000000000001000L) <> 0x0L

    member x.SetIsCompilerGenerated(isCompGen) = 
            let flags =                 (flags       &&&    ~~~0b000000000000001000L) |||
                                        (match isCompGen with
                                          | false           -> 0b000000000000000000L
                                          | true            -> 0b000000000000001000L)
            ValFlags(flags)

    member x.InlineInfo = 
                                  match (flags       &&&       0b000000000000110000L) with 
                                                             | 0b000000000000000000L -> PseudoValue
                                                             | 0b000000000000010000L -> AlwaysInline
                                                             | 0b000000000000100000L -> OptionalInline
                                                             | 0b000000000000110000L -> NeverInline
                                                             | _          -> failwith "unreachable"

    member x.MutabilityInfo = 
                                  match (flags       &&&       0b000000000001000000L) with 
                                                             | 0b000000000000000000L -> Immutable
                                                             | 0b000000000001000000L -> Mutable
                                                             | _          -> failwith "unreachable"


    member x.IsMemberOrModuleBinding = 
                                  match (flags       &&&       0b000000000010000000L) with 
                                                             | 0b000000000000000000L -> false
                                                             | 0b000000000010000000L -> true
                                                             | _          -> failwith "unreachable"


    member x.SetIsMemberOrModuleBinding = ValFlags(flags |||   0b000000000010000000L)


    member x.IsExtensionMember        = (flags       &&&       0b000000000100000000L) <> 0L
    member x.IsIncrClassSpecialMember = (flags       &&&       0b000000001000000000L) <> 0L
    member x.IsTypeFunction           = (flags       &&&       0b000000010000000000L) <> 0L

    member x.RecursiveValInfo =   match (flags       &&&       0b000001100000000000L) with 
                                                             | 0b000000000000000000L -> ValNotInRecScope
                                                             | 0b000000100000000000L -> ValInRecScope(true)
                                                             | 0b000001000000000000L -> ValInRecScope(false)
                                                             | _                   -> failwith "unreachable"

    member x.SetRecursiveValInfo(recValInfo) = 
            let flags = 
                     (flags       &&&                       ~~~0b000001100000000000L) |||
                     (match recValInfo with
                                     | ValNotInRecScope     -> 0b000000000000000000L
                                     | ValInRecScope(true)  -> 0b000000100000000000L
                                     | ValInRecScope(false) -> 0b000001000000000000L) 
            ValFlags(flags)

    member x.MakesNoCriticalTailcalls         = (flags &&&     0b000010000000000000L) <> 0L

    member x.SetMakesNoCriticalTailcalls = ValFlags(flags |||  0b000010000000000000L)

    member x.PermitsExplicitTypeInstantiation = (flags &&&     0b000100000000000000L) <> 0L
    member x.HasBeenReferenced                = (flags &&&     0b001000000000000000L) <> 0L

    member x.SetHasBeenReferenced        = ValFlags(flags |||  0b001000000000000000L)

    member x.IsCompiledAsStaticPropertyWithoutField       = (flags &&&     0b010000000000000000L) <> 0L

    member x.SetIsCompiledAsStaticPropertyWithoutField   = ValFlags(flags |||  0b010000000000000000L)
    /// Get the flags as included in the F# binary metadata
    member x.PickledBits = 
        // Clear the RecursiveValInfo, only used during inference and irrelevant across assembly boundaries
        // Clear the IsCompiledAsStaticPropertyWithoutField, only used to determine whether to use a true field for a value, and to eliminate the optimization info for observable bindings
        // Clear the HasBeenReferenced, only used to report "unreferenced variable" warnings and to help collect 'it' values in FSI.EXE
                                        (flags       &&&    ~~~0b011001100000000000L) 

type TyparKind = 
    | KindType 
    | KindMeasure
    member x.AttrName =
      match x with
      | KindType -> None
      | KindMeasure -> Some "Measure"
    override x.ToString() = 
      match x with
      | KindType -> "type"
      | KindMeasure -> "measure"


type TyparRigidity = 
    /// Indicates the type parameter can't be solved
    | TyparRigid 
    /// Indicates the type parameter can't be solved, but the variable is not set to "rigid" until after inference is complete
    | TyparWillBeRigid 
    /// Indicates we give a warning if the type parameter is ever solved
    | TyparWarnIfNotRigid 
    /// Indicates the type parameter is an inference variable may be solved
    | TyparFlexible
    /// Indicates the type parameter derives from an '_' anonymous type
    /// For units-of-measure, we give a warning if this gets solved to '1'
    | TyparAnon
    member x.ErrorIfUnified = match x with TyparRigid -> true | _ -> false
    member x.WarnIfUnified = match x with TyparWillBeRigid | TyparWarnIfNotRigid -> true | _ -> false
    member x.WarnIfMissingConstraint = match x with TyparWillBeRigid -> true | _ -> false


/// Encode typar flags into a bit field  
[<Struct>]
type TyparFlags(flags:int32) =

    new (kind:TyparKind, rigidity:TyparRigidity, isFromError:bool, isCompGen:bool, staticReq:TyparStaticReq, dynamicReq:TyparDynamicReq, equalityDependsOn: bool, comparisonDependsOn: bool) = 
        TyparFlags((if isFromError then       0b000000000010 else 0) |||
                   (if isCompGen   then       0b000000000100 else 0) |||
                   (match staticReq with
                     | NoStaticReq         -> 0b000000000000
                     | HeadTypeStaticReq   -> 0b000000001000) |||
                   (match rigidity with
                     | TyparRigid          -> 0b000000000000
                     | TyparWillBeRigid    -> 0b000000100000
                     | TyparWarnIfNotRigid -> 0b000001000000
                     | TyparFlexible       -> 0b000001100000
                     | TyparAnon           -> 0b000010000000) |||
                   (match kind with
                     | KindType            -> 0b000000000000
                     | KindMeasure         -> 0b000100000000) |||
                   (if comparisonDependsOn then 
                                              0b001000000000 else 0) |||
                   (match dynamicReq with
                     | NoDynamicReq        -> 0b000000000000
                     | DynamicReq          -> 0b010000000000) |||
                   (if equalityDependsOn then 
                                              0b100000000000 else 0))

    member x.IsFromError         = (flags &&& 0b000000000010) <> 0x0
    member x.IsCompilerGenerated = (flags &&& 0b000000000100) <> 0x0
    member x.StaticReq           = 
                             match (flags &&& 0b000000001000) with 
                                            | 0b000000000000 -> NoStaticReq
                                            | 0b000000001000 -> HeadTypeStaticReq
                                            | _             -> failwith "unreachable"

    member x.Rigidity = 
                             match (flags &&& 0b000011100000) with 
                                            | 0b000000000000 -> TyparRigid
                                            | 0b000000100000 -> TyparWillBeRigid
                                            | 0b000001000000 -> TyparWarnIfNotRigid
                                            | 0b000001100000 -> TyparFlexible
                                            | 0b000010000000 -> TyparAnon
                                            | _          -> failwith "unreachable"

    member x.Kind           = 
                             match (flags &&& 0b000100000000) with 
                                            | 0b000000000000 -> KindType
                                            | 0b000100000000 -> KindMeasure
                                            | _             -> failwith "unreachable"


    member x.ComparisonConditionalOn =
                                   (flags &&& 0b001000000000) <> 0x0
    member x.DynamicReq     = 
                             match (flags &&& 0b010000000000) with 
                                            | 0b000000000000 -> NoDynamicReq
                                            | 0b010000000000 -> DynamicReq
                                            | _             -> failwith "unreachable"
    member x.EqualityConditionalOn = 
                                   (flags &&& 0b100000000000) <> 0x0


    /// Get the flags as included in the F# binary metadata. We pickle this as int64 to allow for future expansion
    member x.PickledBits =         flags       

/// Encode entity flags into a bit field. We leave lots of space to allow for future expansion.
[<Struct>]
type EntityFlags(flags:int64) =

    new (usesPrefixDisplay, isModuleOrNamespace, preEstablishedHasDefaultCtor, hasSelfReferentialCtor) = 
        EntityFlags((if isModuleOrNamespace then                        0b00000000001L else 0L) |||
                    (if usesPrefixDisplay   then                        0b00000000010L else 0L) |||
                    (if preEstablishedHasDefaultCtor then               0b00000000100L else 0L) |||
                    (if hasSelfReferentialCtor then                     0b00000001000L else 0L)) 

    member x.IsModuleOrNamespace                 = (flags       &&&     0b00000000001L) <> 0x0L
    member x.IsPrefixDisplay                     = (flags       &&&     0b00000000010L) <> 0x0L
    
    // This bit is not pickled, only used while establishing a type constructor. It is needed because the type constructor
    // is known to satisfy the default constructor constraint even before any of its members have been established.
    member x.PreEstablishedHasDefaultConstructor = (flags       &&&     0b00000000100L) <> 0x0L

    // This bit represents an F# specific condition where a type has at least one constructor that may access
    // the 'this' pointer prior to successful initialization of the partial contents of the object. In this
    // case sub-classes must protect themselves against early access to their contents.
    member x.HasSelfReferentialConstructor       = (flags       &&&     0b00000001000L) <> 0x0L

    /// Get the flags as included in the F# binary metadata
    member x.PickledBits =                         (flags       &&&  ~~~0b00000000100L)


#if DEBUG
assert (sizeof<ValFlags> = 8)
assert (sizeof<EntityFlags> = 8)
assert (sizeof<TyparFlags> = 4)
#endif


let unassignedTyparName = "?"

exception UndefinedName of int * (* error func that expects identifier name *)(string -> string) * ident * string list
exception InternalUndefinedItemRef of (string * string * string -> int * string) * string * string * string

let addTyconsByDemangledNameAndArity nm (typars:'a list) x tab = 
    let nm = demangleGenericTypeName nm 
    Map.add (NameArityPair(nm, typars.Length)) x tab

let addTyconsByAccessNames nm x tab = 
    if isMangledGenericName nm then 
        let dnm = demangleGenericTypeName nm 
        let res = NameMultiMap.add nm x tab 
        NameMultiMap.add dnm x res
    else
        NameMultiMap.add nm x tab 
       

// Type definitions, exception definitions, module definitions and
// namespace definitions are all 'entities'. These have too much in common to make it 
// worth factoring them out as separate types.
//
// Tycons, exncs and moduls are all modelled via tycon_specs, 
// they have different name-resolution logic. 
// For example, an excon ABC really correspond to a type called 
// ABCException with a union case ABC. At the moment they are 
// simply indexed in the excon table as the discriminator constructor ABC. 
type Entity = 
    { mutable Data: EntityData; }
    member x.LogicalName = x.Data.entity_logical_name
    member x.CompiledName = match x.Data.entity_compiled_name with None -> x.LogicalName | Some s -> s
    member x.DisplayName = demangleGenericTypeName x.Data.entity_logical_name
    member x.DisplayNameWithUnderscoreTypars = 
        match x.Typars(x.Range) with 
        | [] -> x.DisplayName
        | tps -> x.DisplayName + "<" + String.concat "," (Array.create tps.Length "_") + ">"
    
    member x.Range = x.Data.entity_range
    member x.Stamp = x.Data.entity_stamp
    member x.Attribs = x.Data.entity_attribs
    member x.XmlDoc = x.Data.entity_xmldoc
    member x.XmlDocSig 
        with get() = x.Data.entity_xmldocsig
        and set(v) = x.Data.entity_xmldocsig <- v
    member x.ModuleOrNamespaceType = x.Data.entity_modul_contents.Force()
    
    member x.TypeContents = x.Data.entity_tycon_tcaug
    member x.TypeOrMeasureKind = x.Data.entity_kind
    member x.Id = ident(x.LogicalName, x.Range)
    member x.TypeReprInfo = x.Data.entity_tycon_repr
    member x.ExceptionInfo = x.Data.entity_exn_info
    member x.IsExceptionDecl = match x.ExceptionInfo with TExnNone -> false | _ -> true

    static member DemangleEntityName nm k =  
        match k with 
        | FSharpModuleWithSuffix -> String.dropSuffix nm fsharpModuleSuffix
        | _ -> nm

    member x.DemangledModuleOrNamespaceName =  
          Entity.DemangleEntityName x.LogicalName x.ModuleOrNamespaceType.ModuleOrNamespaceKind
    
    member x.Typars(m) = x.Data.entity_typars.Force(m) // lazy because it may read metadata, must provide a context "range" in case error occurs reading metadata
    member x.TyparsNoRange = x.Typars(x.Range)
    member x.TypeAbbrev = x.Data.entity_tycon_abbrev
    member x.IsTypeAbbrev = x.TypeAbbrev.IsSome
    member x.TypeReprAccessibility = x.Data.entity_tycon_repr_accessibility
    member x.CompiledReprCache = x.Data.entity_il_repr_cache
    member x.PublicPath = x.Data.entity_pubpath
    member x.Accessibility = x.Data.entity_accessiblity
    /// Indicates the type prefers the "tycon<a,b>" syntax for display etc. 
    member x.IsPrefixDisplay = x.Data.entity_flags.IsPrefixDisplay
    /// Indicates the "tycon blob" is actually a module 
    member x.IsModuleOrNamespace = x.Data.entity_flags.IsModuleOrNamespace
    
    member x.IsNamespace = x.IsModuleOrNamespace && (match x.ModuleOrNamespaceType.ModuleOrNamespaceKind with Namespace -> true | _ -> false)
    member x.IsModule = x.IsModuleOrNamespace && (match x.ModuleOrNamespaceType.ModuleOrNamespaceKind with Namespace -> false | _ -> true)
    member x.CompilationPathOpt = x.Data.entity_cpath 
    member x.CompilationPath = 
        match x.CompilationPathOpt with 
        | Some cpath -> cpath 
        | None -> error(Error(FSComp.SR.tastTypeOrModuleNotConcrete(x.LogicalName),x.Range))
    
    member x.AllFieldTable = 
        match x.TypeReprInfo with 
        | Some (TRecdRepr x | TFsObjModelRepr {fsobjmodel_rfields=x}) -> x
        |  _ -> 
        match x.ExceptionInfo with 
        | TExnFresh x -> x
        | _ -> 
        { rfields_by_index = [| |]; 
          rfields_by_name = NameMap.empty }

    member x.AllFieldsArray = x.AllFieldTable.rfields_by_index
    member x.AllFieldsAsList = x.AllFieldsArray |> Array.toList

    // NOTE: This method is over-used...
    member x.AllInstanceFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsStatic)
    member x.TrueFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsCompilerGenerated)
    member x.TrueInstanceFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsStatic && not f.IsCompilerGenerated)

    member x.GetFieldByIndex(n) = x.AllFieldTable.FieldByIndex(n)
    member x.GetFieldByName(n) = x.AllFieldTable.FieldByName(n)

    member x.UnionTypeInfo = 
        match x.Data.entity_tycon_repr with 
        | Some (TFiniteUnionRepr x) -> Some x 
        |  _ -> None

    member x.UnionCasesArray = 
        match x.UnionTypeInfo with 
        | Some x -> x.funion_ucases.ucases_by_index 
        | None -> [| |] 

    member x.UnionCasesAsList = x.UnionCasesArray |> Array.toList

    member x.GetUnionCaseByName(n) =
        match x.UnionTypeInfo with 
        | Some x  -> NameMap.tryFind n x.funion_ucases.ucases_by_name
        | None -> None

    
    // OSGN support
    static member NewUnlinked() : Entity = { Data = Unchecked.defaultof<_> }
    static member New reason data : Entity  = 
        { Data = data }
    member x.Link(tg) = x.Data <- tg
    member x.IsLinked = match box x.Data with null -> false | _ -> true 

    override x.ToString() = x.LogicalName

    member x.FSharpObjectModelTypeInfo = 
         match x.Data.entity_tycon_repr with 
         | Some (TFsObjModelRepr x) -> x 
         |  _ -> failwith "not an F# object model type definition"

    member x.IsILTycon = match x.TypeReprInfo with | Some (TILObjModelRepr _) -> true |  _ -> false
    member x.ILTyconInfo = match x.TypeReprInfo with | Some (TILObjModelRepr (a,b,c)) -> (a,b,c) |  _ -> failwith "not a .NET type definition"
    member x.ILTyconRawMetadata = let _,_,td = x.ILTyconInfo in td

    member x.IsUnionTycon = match x.TypeReprInfo with | Some (TFiniteUnionRepr _) -> true |  _ -> false
    member x.UnionInfo = match x.TypeReprInfo with | Some (TFiniteUnionRepr x) -> Some x |  _ -> None

    member x.IsRecordTycon = match x.TypeReprInfo with | Some (TRecdRepr _) -> true |  _ -> false
    member x.IsFSharpObjectModelTycon = match x.TypeReprInfo with | Some (TFsObjModelRepr _) -> true |  _ -> false
    member x.IsAsmReprTycon = match x.TypeReprInfo with | Some (TAsmRepr _) -> true |  _ -> false
    member x.IsMeasureableReprTycon = match x.TypeReprInfo with | Some (TMeasureableRepr _) -> true |  _ -> false
    member x.IsHiddenReprTycon = match x.TypeAbbrev,x.TypeReprInfo with | None,None -> true |  _ -> false

    member x.IsFSharpInterfaceTycon = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconInterface -> true | _ -> false
    member x.IsFSharpDelegateTycon  = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconDelegate _ -> true | _ -> false
    member x.IsFSharpEnumTycon      = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconEnum -> true | _ -> false
    member x.IsFSharpClassTycon     = x.IsFSharpObjectModelTycon && match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with TTyconClass -> true | _ -> false
    member x.IsILEnumTycon          = x.IsILTycon && x.ILTyconRawMetadata.IsEnum
    member x.IsEnumTycon            = x.IsILEnumTycon || x.IsFSharpEnumTycon


    member x.IsFSharpStructOrEnumTycon =
        x.IsFSharpObjectModelTycon &&
        match x.FSharpObjectModelTypeInfo.fsobjmodel_kind with 
        | TTyconClass | TTyconInterface   | TTyconDelegate _ -> false
        | TTyconStruct | TTyconEnum -> true

    member x.IsILStructTycon =
        x.IsILTycon && 
        match x.ILTyconRawMetadata.tdKind with
        | ILTypeDefKind.ValueType | ILTypeDefKind.Enum -> true
        | _ -> false

    member x.IsStructOrEnumTycon = 
        x.IsILStructTycon || x.IsFSharpStructOrEnumTycon

    member x.InterfacesOfFSharpTycon =
        x.TypeContents.tcaug_interfaces

    member x.InterfaceTypesOfFSharpTycon =
        x.InterfacesOfFSharpTycon |> List.map (fun (x,_,_) -> x)

    /// Note: result is alphabetically sorted, then for each name the results are in declaration order
    member x.MembersOfFSharpTyconSorted =
        x.TypeContents.tcaug_adhoc 
        |> NameMultiMap.rangeReversingEachBucket 
        |> List.filter (fun v -> not v.IsCompilerGenerated)

    /// Note: result is a indexed table, and for each name the results are in reverse declaration order
    member x.MembersOfFSharpTyconByName =
        x.TypeContents.tcaug_adhoc 

    member x.GeneratedHashAndEqualsWithComparerValues = x.TypeContents.tcaug_hash_and_equals_withc 
    member x.GeneratedCompareToWithComparerValues = x.TypeContents.tcaug_compare_withc
    member x.GeneratedCompareToValues = x.TypeContents.tcaug_compare
    member x.GeneratedHashAndEqualsValues = x.TypeContents.tcaug_equals
    member x.AllGeneratedValues = 
        [ match x.GeneratedCompareToValues with 
          | None -> ()
          | Some (v1,v2) -> yield v1; yield v2
          match x.GeneratedCompareToWithComparerValues with
          | None -> ()
          | Some v -> yield v
          match x.GeneratedHashAndEqualsValues with
          | None -> ()
          | Some (v1,v2) -> yield v1; yield v2
          match x.GeneratedHashAndEqualsWithComparerValues with
          | None -> ()
          | Some (v1,v2,v3) -> yield v1; yield v2; yield v3 ]
    

    /// From TAST TyconRef to IL ILTypeRef
    member x.CompiledRepresentation =

        let ilTypeRefForCompilationPath (CompPath(sref,p)) item = 
            let rec top racc  p = 
                match p with 
                | [] -> ILTypeRef.Create(sref,[],text_of_path  (List.rev (item::racc)))
                | (h,istype)::t -> 
                    match istype with 
                    | FSharpModuleWithSuffix | FSharpModule -> 
                        let outerTypeName = (text_of_path (List.rev (h::racc)))
                        ILTypeRef.Create(sref, (outerTypeName :: List.map (fun (nm,_) -> nm) t),item)
                    | _ -> 
                      top (h::racc) t
            top [] p 


        cached x.CompiledReprCache (fun () -> 
            match x.ExceptionInfo with 
            | TExnAbbrevRepr ecref2 -> ecref2.CompiledRepresentation
            | TExnAsmRepr tref -> TyrepNamed(tref, AsObject, Some (mk_typ AsObject (mk_tspec (tref,[]))))
            | _ -> 
            match x.TypeReprInfo with 
            | Some (TAsmRepr typ) -> TyrepOpen typ
            | _ -> 
                let boxity = if x.IsStructOrEnumTycon then AsValue else AsObject
                let ilTypeRef = ilTypeRefForCompilationPath x.CompilationPath x.CompiledName
                let ilTypeOpt = 
                    match x.TyparsNoRange with 
                    | [] -> Some (mk_typ boxity (mk_tspec (ilTypeRef,[]))) 
                    | _ -> None 
                TyrepNamed (ilTypeRef, boxity, ilTypeOpt))


    member x.CompiledRepresentationForTyrepNamed =
        match x.CompiledRepresentation with 
        | TyrepNamed(tref, _, _) -> tref
        | TyrepOpen _ -> invalidOp (FSComp.SR.tastTypeHasAssemblyCodeRepresentation(x.DisplayNameWithUnderscoreTypars))


    member x.PreEstablishedHasDefaultConstructor = x.Data.entity_flags.PreEstablishedHasDefaultConstructor
    member x.HasSelfReferentialConstructor = x.Data.entity_flags.HasSelfReferentialConstructor

and 
    [<NoEquality; NoComparison>]
    EntityData =
    { /// The declared type parameters of the type  
      mutable entity_typars: LazyWithContext<Typars, range>;        

      mutable entity_kind : TyparKind;
      
      mutable entity_flags : EntityFlags;
      
      /// The unique stamp of the "tycon blob". Note the same tycon in signature and implementation get different stamps 
      entity_stamp: stamp;

      /// The name of the type, possibly with `n mangling 
      entity_logical_name: string;

      /// The name of the type, possibly with `n mangling 
      mutable entity_compiled_name: string option;

      /// The declaration location for the type constructor 
      entity_range: range;
      
      /// The declared accessibility of the representation, not taking signatures into account 
      entity_tycon_repr_accessibility: Accessibility;
      
      /// The declared attributes for the type 
      mutable entity_attribs: Attribs;     
                
      /// The declared representation of the type, i.e. record, union, class etc. 
      mutable entity_tycon_repr: TyconRepresentation option;   

      /// If non-None, indicates the type is an abbreviation for another type. 
      mutable entity_tycon_abbrev: typ option;             
      
      /// The methods and properties of the type 
      mutable entity_tycon_tcaug: TyconAugmentation;      
      
      /// Field used when the 'tycon' is really an exception definition 
      mutable entity_exn_info: ExceptionInfo;     
      
      /// This field is used when the 'tycon' is really a module definition. It holds statically nested type definitions and nested modules 
      mutable entity_modul_contents: Lazy<ModuleOrNamespaceType>;     

      /// The declared documentation for the type or module 
      entity_xmldoc : XmlDoc;
      
      /// The XML document signature for this entity
      mutable entity_xmldocsig : string;

      /// The stable path to the type, e.g. Microsoft.FSharp.Core.FSharpFunc`2 
      entity_pubpath : PublicPath option; 

      mutable entity_accessiblity: Accessibility; (*   how visible is this? *) 
 
      /// The stable path to the type, e.g. Microsoft.FSharp.Core.FSharpFunc`2 
      entity_cpath : CompilationPath option; 

      /// Used during codegen to hold the ILX representation indicating how to access the type 
      entity_il_repr_cache : CompiledTypeRepr cache;  

    }

and ParentRef = 
    | Parent of TyconRef
    | ParentNone
    
and 
    [<NoEquality; NoComparison>]
    TyconAugmentation = 
    { /// This is the value implementing the auto-generated comparison 
      /// semantics if any. It is not present if the type defines its own implementation 
      /// of IComparable or if the type doesn't implement IComparable implicitly. 
      mutable tcaug_compare        : (ValRef * ValRef) option;
      
      /// This is the value implementing the auto-generated comparison
      /// semantics if any. It is not present if the type defines its own implementation
      /// of IStructuralComparable or if the type doesn't implement IComparable implicitly.
      mutable tcaug_compare_withc : ValRef option;                      

      /// This is the value implementing the auto-generated equality 
      /// semantics if any. It is not present if the type defines its own implementation 
      /// of Object.Equals or if the type doesn't override Object.Equals implicitly. 
      mutable tcaug_equals        : (ValRef * ValRef) option;

      /// This is the value implementing the auto-generated comparison
      /// semantics if any. It is not present if the type defines its own implementation
      /// of IStructuralEquatable or if the type doesn't implement IComparable implicitly.
      mutable tcaug_hash_and_equals_withc : (ValRef * ValRef * ValRef) option;                                    

      /// True if the type defined an Object.GetHashCode method. In this 
      /// case we give a warning if we auto-generate a hash method since the semantics may not match up
      mutable tcaug_hasObjectGetHashCode : bool;             
      
      /// Properties, methods etc. in declaration order
      tcaug_adhoc_list     : ResizeArray<ValRef> ;
      
      /// Properties, methods etc. as lookup table
      mutable tcaug_adhoc          : NameMultiMap<ValRef>;
      
      /// Interface implementations - boolean indicates compiler-generated 
      mutable tcaug_interfaces     : (typ * bool * range) list;  
      
      /// Super type, if any 
      mutable tcaug_super          : typ option;                 
      
      /// Set to true at the end of the scope where proper augmentations are allowed 
      mutable tcaug_closed         : bool;                       

      /// Set to true if the type is determined to be abstract 
      mutable tcaug_abstract : bool;                       
    }
   
and 
    [<NoEquality; NoComparison>]
    TyconRepresentation = 
    /// Indicates the type is a class, struct, enum, delegate or interface 
    | TFsObjModelRepr    of TyconObjModelData
    /// Indicates the type is a record 
    | TRecdRepr          of TyconRecdFields
    /// Indicates the type is a discriminated union 
    | TFiniteUnionRepr   of TyconUnionData 
    /// Indicates the type is a .NET type 
    | TILObjModelRepr    of 
          // scope: 
          ILScopeRef * 
          // nesting:   
          ILTypeDef list * 
          // definition: 
          ILTypeDef 
    /// Indicates the type is implemented as IL assembly code using the given closed Abstract IL type 
    | TAsmRepr           of ILType
    /// Indicates the type is parameterized on a measure (e.g. float<_>) but erases to some other type (e.g. float)
    | TMeasureableRepr   of typ


and 
  TyconObjModelKind = 
    /// Indicates the type is a class (also used for units-of-measure)
    | TTyconClass 
    /// Indicates the type is an interface 
    | TTyconInterface 
    /// Indicates the type is a struct 
    | TTyconStruct 
    /// Indicates the type is a delegate with the given Invoke signature 
    | TTyconDelegate of SlotSig 
    /// Indicates the type is an enumeration 
    | TTyconEnum
    
and 
    [<NoEquality; NoComparison>]
    TyconObjModelData = 
    { /// Indicates whether the type declaration is a class, interface, enum, delegate or struct 
      fsobjmodel_kind: TyconObjModelKind;
      /// The declared abstract slots of the class, interface or struct 
      fsobjmodel_vslots: ValRef list; 
      /// The fields of the class, struct or enum 
      fsobjmodel_rfields: TyconRecdFields }

and 
    [<NoEquality; NoComparison>]
    TyconRecdFields = 
    { /// The fields of the record, in declaration order. 
      rfields_by_index: RecdField array;
      
      /// The fields of the record, indexed by name. 
      rfields_by_name : RecdField NameMap  }

    member x.FieldByIndex(n) = 
        if n >= 0 && n < Array.length x.rfields_by_index then x.rfields_by_index.[n] 
        else failwith "FieldByIndex"

    member x.FieldByName(n) = x.rfields_by_name.TryFind(n)

    member x.AllFieldsAsList = x.rfields_by_index |> Array.toList
    member x.TrueFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsCompilerGenerated)   
    member x.TrueInstanceFieldsAsList = x.AllFieldsAsList |> List.filter (fun f -> not f.IsStatic && not f.IsCompilerGenerated)   

and 
    [<NoEquality; NoComparison>]
    TyconUnionCases = 
    { /// The cases of the discriminated union, in declaration order. 
      ucases_by_index: UnionCase array;
      /// The cases of the discriminated union, indexed by name. 
      ucases_by_name : UnionCase NameMap 
    }
    member x.GetUnionCaseByIndex(n) = 
        if n >= 0 && n < x.ucases_by_index.Length then x.ucases_by_index.[n] 
        else invalidArg "n" "GetUnionCaseByIndex"

    member x.UnionCasesAsList = x.ucases_by_index |> Array.toList

and 
    [<NoEquality; NoComparison>]
    TyconUnionData =
    { /// The cases contained in the discriminated union. 
      funion_ucases: TyconUnionCases;
      /// The ILX data structure representing the discriminated union. 
    }
    member x.UnionCasesAsList = x.funion_ucases.ucases_by_index |> Array.toList

and 
    [<NoEquality; NoComparison>]
    [<StructuredFormatDisplay("{DisplayName}")>]
    UnionCase =
    { /// Data carried by the case. 
      ucase_rfields: TyconRecdFields;
      /// Return type constructed by the case. Normally exactly the type of the enclosing type, sometimes an abbreviation of it 
      ucase_rty: typ;
      /// Name of the case in generated IL code 
      ucase_il_name: string;
      /// Documentation for the case 
      ucase_xmldoc : XmlDoc;
      /// XML documentation signature for the case
      mutable ucase_xmldocsig : string;
      /// Name/range of the case 
      ucase_id: ident; 
      ///  Indicates the declared visibility of the union constructor, not taking signatures into account 
      ucase_access: Accessibility; 
      /// Attributes, attached to the generated static method to make instances of the case 
      mutable ucase_attribs: Attribs; }

    member uc.Attribs = uc.ucase_attribs
    member uc.Range = uc.ucase_id.idRange
    member uc.Id = uc.ucase_id
    member uc.Accessibility = uc.ucase_access
    member uc.DisplayName = uc.Id.idText
    member uc.RecdFieldsArray = uc.ucase_rfields.rfields_by_index 
    member uc.RecdFields = uc.ucase_rfields.rfields_by_index |> Array.toList
    member uc.GetFieldByName nm = uc.ucase_rfields.FieldByName nm
    member uc.IsNullary = (uc.ucase_rfields.rfields_by_index.Length = 0)
    member uc.XmlDoc = uc.ucase_xmldoc
    member uc.XmlDocSig 
        with get() = uc.ucase_xmldocsig
        and set(v) = uc.ucase_xmldocsig <- v
    member uc.ReturnType = uc.ucase_rty

and 
    /// This may represent a "field" in either a struct, class, record or union
    /// It is normally compiled to a property.
    [<NoEquality; NoComparison>]
    RecdField =
    { /// Is the field declared mutable in F#? 
      rfield_mutable: bool;
      /// Documentation for the field 
      rfield_xmldoc : XmlDoc;
      /// XML Documentation signature for the field
      mutable rfield_xmldocsig : string;
      /// The type of the field, w.r.t. the generic parameters of the enclosing type constructor 
      rfield_type: typ;
      /// Indicates a static field 
      rfield_static: bool;
      /// Indicates a volatile field 
      rfield_volatile: bool;
      /// Indicates a compiler generated field, not visible to Intellisense or name resolution 
      rfield_secret: bool;
      /// The default initialization info, for static literals 
      rfield_const: Constant option; 
      ///  Indicates the declared visibility of the field, not taking signatures into account 
      rfield_access: Accessibility; 
      /// Attributes attached to generated property 
      mutable rfield_pattribs: Attribs; 
      /// Attributes attached to generated field 
      mutable rfield_fattribs: Attribs; 
      /// Name/declaration-location of the field 
      rfield_id: ident; }
    member v.Accessibility = v.rfield_access
    member v.PropertyAttribs = v.rfield_pattribs
    member v.FieldAttribs = v.rfield_fattribs
    member v.Range = v.rfield_id.idRange
    member v.Id = v.rfield_id
    member v.Name = v.rfield_id.idText
    member v.IsCompilerGenerated = v.rfield_secret
    member v.IsMutable = v.rfield_mutable
    member v.IsStatic = v.rfield_static
    member v.IsVolatile = v.rfield_volatile
    member v.FormalType = v.rfield_type
    member v.XmlDoc = v.rfield_xmldoc
    member v.XmlDocSig
        with get() = v.rfield_xmldocsig
        and set(x) = v.rfield_xmldocsig <- x

    member v.LiteralValue = 
        match v.rfield_const  with 
        | None -> None
        | Some(TConst_zero) -> None
        | Some(k) -> Some(k)

    member v.IsZeroInit = 
        match v.rfield_const  with 
        | None -> false 
        | Some(TConst_zero) -> true 
        | _ -> false

and ExceptionInfo =
    /// Indicates that an exception is an abbreviation for the given exception 
    | TExnAbbrevRepr of TyconRef 
    /// Indicates that an exception is shorthand for the given .NET exception type 
    | TExnAsmRepr of ILTypeRef
    /// Indicates that an exception carries the given record of values 
    | TExnFresh of TyconRecdFields
    /// Indicates that an exception is abstract, i.e. is in a signature file, and we do not know the representation 
    | TExnNone

and ModuleOrNamespaceKind = 
    /// Indicates that a module is compiled to a class with the "Module" suffix added. 
    | FSharpModuleWithSuffix 
    /// Indicates that a module is compiled to a class with the same name as the original module 
    | FSharpModule 
    /// Indicates that a 'module' is really a namespace 
    | Namespace

and 
    [<Sealed>]
    ModuleOrNamespaceType(kind: ModuleOrNamespaceKind, vals: QueueList<Val>, entities: QueueList<Entity>) = 

      /// Mutation used during compilation of FSharp.Core.dll
      let mutable entities = entities 
      
      // Lookup tables keyed the way various clients expect them to be keyed.
      // We attach them here so we don't need to store lookup tables via any other technique.
      //
      // The type option ref is used because there are a few functions that treat these as first class values.
      // We should probably change to 'mutable'.
      //
      // We do not need to lock this mutable state this it is only ever accessed from the compiler thread.
      let apref_cache                        : NameMap<ActivePatternElemRef> option ref = ref None
      let modulesByDemangledName_cache       : NameMap<ModuleOrNamespace>    option ref = ref None
      let exconsByDemangledName_cache        : NameMap<Tycon>                option ref = ref None
      let tyconsByDemangledNameAndArity_cache: Map<NameArityPair, Tycon>     option ref = ref None
      let tyconsByAccessNames_cache          : NameMultiMap<Tycon>           option ref = ref None
      let tyconsByMangledName_cache          : NameMap<Tycon>                option ref = ref None
      let allEntitiesByMangledName_cache     : NameMap<Entity>               option ref = ref None
      let allValsAndMembersByPartialLinkageKey_cache : MultiMap<ValLinkagePartialKey, Val>    option ref = ref None
      let allValsByLogicalName_cache         : NameMap<Val>               option ref = ref None
  
      /// Namespace or module-compiled-as-type? 
      member mtyp.ModuleOrNamespaceKind = kind 
              
      /// Values, including members in F# types in this module-or-namespace-fragment. 
      member mtyp.AllValsAndMembers = vals

      /// Type, mapping mangled name to Tycon, e.g. 
      ////     "Dictionary`2" --> Tycon 
      ////     "ListModule" --> Tycon with module info
      ////     "FooException" --> Tycon with exception info
      member mtyp.AllEntities = entities

      /// Mutation used during compilation of FSharp.Core.dll
      member mtyp.AddModuleOrNamespaceByMutation(modul:ModuleOrNamespace) =
          entities <- QueueList.appendOne entities modul
          modulesByDemangledName_cache := None          
          allEntitiesByMangledName_cache := None          
          
      member mtyp.AddEntity(tycon:Tycon) = 
          ModuleOrNamespaceType(kind, vals, entities.AppendOne tycon)
          
      member mtyp.AddVal(vspec:Val) = 
          ModuleOrNamespaceType(kind, vals.AppendOne vspec, entities)
          
      member mtyp.ActivePatternsLookupTable               = apref_cache
  
      member mtyp.TypeDefinitions               = entities |> Seq.filter (fun x -> not x.IsExceptionDecl && not x.IsModuleOrNamespace) |> Seq.toList
      member mtyp.ExceptionDefinitions          = entities |> Seq.filter (fun x -> x.IsExceptionDecl) |> Seq.toList
      member mtyp.ModuleAndNamespaceDefinitions = entities |> Seq.filter (fun x -> x.IsModuleOrNamespace) |> Seq.toList
      member mtyp.TypeAndExceptionDefinitions   = entities |> Seq.filter (fun x -> not x.IsModuleOrNamespace) |> Seq.toList

      member mtyp.TypesByDemangledNameAndArity(m) = 
        cacheOptRef tyconsByDemangledNameAndArity_cache (fun () -> 
           List.foldBack (fun (tc:Tycon) acc -> addTyconsByDemangledNameAndArity tc.LogicalName (tc.Typars(m)) tc acc) mtyp.TypeAndExceptionDefinitions  Map.empty)

      member mtyp.TypesByAccessNames = 
          cacheOptRef tyconsByAccessNames_cache (fun () -> 
             List.foldBack (fun (tc:Tycon) acc -> addTyconsByAccessNames tc.LogicalName tc acc) mtyp.TypeAndExceptionDefinitions  Map.empty)

      member mtyp.TypesByMangledName = 
          let addTyconByMangledName (x:Tycon) tab = NameMap.add x.LogicalName x tab 
          cacheOptRef tyconsByMangledName_cache (fun () -> 
             List.foldBack addTyconByMangledName mtyp.TypeAndExceptionDefinitions  Map.empty)

      member mtyp.AllEntitiesByCompiledAndLogicalMangledNames : NameMap<Entity> = 
          let addEntityByMangledName (x:Entity) tab = 
              let name1 = x.LogicalName
              let name2 = x.CompiledName
              let tab = NameMap.add name1 x tab 
              if name1 = name2 then tab
              else NameMap.add name2 x tab 
          
          cacheOptRef allEntitiesByMangledName_cache (fun () -> 
             QueueList.foldBack addEntityByMangledName entities  Map.empty)

      member mtyp.AllEntitiesByLogicalMangledName : NameMap<Entity> = 
          let addEntityByMangledName (x:Entity) tab = NameMap.add x.LogicalName x tab 
          QueueList.foldBack addEntityByMangledName entities  Map.empty

      member mtyp.AllValsAndMembersByPartialLinkageKey = 
          let addValByMangledName (x:Val) tab = 
             if x.IsCompiledAsTopLevel then
                 MultiMap.add x.LinkagePartialKey x tab 
             else
                 tab
          cacheOptRef allValsAndMembersByPartialLinkageKey_cache (fun () -> 
             QueueList.foldBack addValByMangledName vals MultiMap.empty)

      member mtyp.TryLinkVal(ccu:CcuThunk,key:ValLinkageFullKey) = 
          mtyp.AllValsAndMembersByPartialLinkageKey
            |> MultiMap.find key.PartialKey
            |> List.tryFind (fun v -> match key.TypeForLinkage with 
                                      | None -> true
                                      | Some keyTy -> ccu.MemberSignatureEquality(keyTy,v.Type))

      member mtyp.AllValsByLogicalName = 
          let addValByName (x:Val) tab = 
             // Note: names may occur twice prior to raising errors about this in PostTypecheckSemanticChecks
             // Earlier ones take precedence sice we report errors about the later ones
             if not x.IsMember && not x.IsCompilerGenerated then 
                 NameMap.add x.LogicalName x tab 
             else
                 tab
          cacheOptRef allValsByLogicalName_cache (fun () -> 
             QueueList.foldBack addValByName vals Map.empty)

      member mtyp.AllValsAndMembersByLogicalNameUncached = 
          let addValByName (x:Val) tab = 
             if not x.IsCompilerGenerated then 
                 MultiMap.add x.LogicalName x tab 
             else
                 tab
          QueueList.foldBack addValByName vals MultiMap.empty

      member mtyp.ExceptionDefinitionsByDemangledName = 
          let addExconByDemangledName (tycon:Tycon) acc = NameMap.add tycon.LogicalName tycon acc
          cacheOptRef exconsByDemangledName_cache (fun () -> 
             List.foldBack addExconByDemangledName mtyp.ExceptionDefinitions  Map.empty)

      member mtyp.ModulesAndNamespacesByDemangledName = 
          let add_moduleByDemangledName (entity:Entity) acc = 
              if entity.IsModuleOrNamespace then 
                  NameMap.add entity.DemangledModuleOrNamespaceName entity acc
              else acc
          cacheOptRef modulesByDemangledName_cache (fun () -> 
             QueueList.foldBack add_moduleByDemangledName entities  Map.empty)

and ModuleOrNamespace = Entity 
and Tycon = Entity 

and Accessibility = 
    /// Indicates the construct can only be accessed from any code in the given type constructor, module or assembly. [] indicates global scope. 
    | TAccess of CompilationPath list
    
and 
    [<NoEquality; NoComparison>]
    TyparData = 
    { mutable typar_id: ident; 
       
      mutable typar_il_name: string option;
       
      mutable typar_flags: TyparFlags;
       
       /// The unique stamp of the typar blob. 
      typar_stamp: stamp; 
       
       /// The documentation for the type parameter. Empty for type inference variables.
      typar_xmldoc : XmlDoc;
       
       /// The declared attributes of the type parameter. Empty for type inference variables. 
      mutable typar_attribs: Attribs;                      
       
       /// An inferred equivalence for a type inference variable. 
      mutable typar_solution: typ option;
       
       /// The inferred constraints for the type inference variable 
      mutable typar_constraints: TyparConstraint list; 
    } 

and 
    [<NoEquality; NoComparison>]
    [<StructuredFormatDisplay("{Name}")>]
    Typar = 
    { mutable Data: TyparData;
      mutable AsType: typ }
    member x.Name                = x.Data.typar_id.idText
    member x.Range               = x.Data.typar_id.idRange
    member x.Id                  = x.Data.typar_id
    member x.Stamp               = x.Data.typar_stamp
    member x.Solution            = x.Data.typar_solution
    member x.Constraints         = x.Data.typar_constraints
    member x.IsCompilerGenerated = x.Data.typar_flags.IsCompilerGenerated
    member x.Rigidity            = x.Data.typar_flags.Rigidity
    member x.DynamicReq          = x.Data.typar_flags.DynamicReq
    member x.EqualityConditionalOn = x.Data.typar_flags.EqualityConditionalOn
    member x.ComparisonConditionalOn = x.Data.typar_flags.ComparisonConditionalOn
    member x.StaticReq           = x.Data.typar_flags.StaticReq
    member x.IsFromError         = x.Data.typar_flags.IsFromError
    member x.Kind                = x.Data.typar_flags.Kind
    member x.IsErased            = match x.Kind with KindType -> false | _ -> true
    member x.Attribs             = x.Data.typar_attribs
    member x.DisplayName = let nm = x.Name in if nm = "?" then "?"+string x.Stamp else nm

    // OSGN support
    static member NewUnlinked() : Typar  = 
        let res = { Data = Unchecked.defaultof<_>; AsType=Unchecked.defaultof<_> }
        res.AsType <- TType_var res
        res
    static member New(data) : Typar = 
        let res = { Data = data; AsType=Unchecked.defaultof<_> }
        res.AsType <- TType_var res
        res
    member x.Link(tg) = x.Data <- tg
    member x.IsLinked = match box x.Data with null -> false | _ -> true 

    override x.ToString() = x.Name

and
    [<NoEquality; NoComparison>]
    TyparConstraint = 
    /// Indicates a constraint that a type is a subtype of the given type 
    | TTyparCoercesToType              of typ * range

    /// Indicates a default value for an inference type variable should it be netiher generalized nor solved 
    | TTyparDefaultsToType             of int * typ * range 
    
    /// Indicates a constraint that a type has a 'null' value 
    | TTyparSupportsNull               of range 
    
    /// Indicates a constraint that a type has a member with the given signature 
    | TTyparMayResolveMemberConstraint of TraitConstraintInfo * range 
    
    /// Indicates a constraint that a type is a non-Nullable value type 
    /// These are part of .NET's model of generic constraints, and in order to 
    /// generate verifiable code we must attach them to F# generalzied type variables as well. 
    | TTyparIsNotNullableValueType     of range 
    
    /// Indicates a constraint that a type is a reference type 
    | TTyparIsReferenceType            of range 

    /// Indicates a constraint that a type is a simple choice between one of the given ground types. See format.fs 
    | TTyparSimpleChoice               of typ list * range 

    /// Indicates a constraint that a type has a parameterless constructor 
    | TTyparRequiresDefaultConstructor of range 

    /// Indicates a constraint that a type is an enum with the given underlying 
    | TTyparIsEnum                     of typ * range 
    
    /// Indicates a constraint that a type implements IComparable, with special rules for some known structural container types
    | TTyparSupportsComparison               of range 
    
    /// Indicates a constraint that a type does not have the Equality(false) attribute, or is not a structural type with this attribute, with special rules for some known structural container types
    | TTyparSupportsEquality                of range 
    
    /// Indicates a constraint that a type is a delegate from the given tuple of args to the given return type
    | TTyparIsDelegate                 of typ * typ * range 
    
    /// Indicates a constraint that a type is .NET unmanaged type
    | TTyparIsUnmanaged                 of range
    
/// The specification of a member constraint that must be solved 
and 
    [<NoEquality; NoComparison>]
    TraitConstraintInfo = 
    /// Indicates the signature of a member constraint. Contains a mutable solution cell
    /// to store the inferred solution of the constraint.
    | TTrait of typ list * string * MemberFlags * typ list * typ option * (* solution: *) TraitConstraintSln option ref 
    member x.MemberName = (let (TTrait(_,nm,_,_,_,_)) = x in nm)
    member x.ReturnType = (let (TTrait(_,_,_,_,ty,_)) = x in ty)
    member x.Solution 
        with get() = (let (TTrait(_,_,_,_,_,sln)) = x in sln.Value)
        and set(v) = (let (TTrait(_,_,_,_,_,sln)) = x in sln.Value <- v)
    
and 
    [<NoEquality; NoComparison>]
    TraitConstraintSln = 
    | FSMethSln of 
         typ * // the type and its instantiation
         ValRef  *   // the method
         tinst // the generic method instantiation 
    | ILMethSln of
         typ * 
         ILTypeRef option (* extension? *) * 
         ILMethodRef * 
         // typars * // the uninstantiated generic method args 
         tinst    // the generic method instantiation 
    | BuiltInSln

and ValLinkagePartialKey = 
   { MemberParentMangledName : string option; 
     MemberIsOverride: bool; 
     LogicalName: string; 
     TotalArgCount: int } 

and ValLinkageFullKey(partialKey: ValLinkagePartialKey,  typeForLinkage:typ option) =
   member x.PartialKey = partialKey
   member x.TypeForLinkage = typeForLinkage


and 
    [<StructuredFormatDisplay("{LogicalName}")>]
    Val = 
    { mutable Data: ValData; }
    /// The place where the value was defined. 
    member x.Range = x.Data.val_range
    /// A unique stamp within the context of this invocation of the compiler process 
    member x.Stamp = x.Data.val_stamp
    /// The type of the value. 
    /// May be a Type_forall for a generic value. 
    /// May be a type variable or type containing type variables during type inference. 

    // Mutability used in inference by adjustAllUsesOfRecValue.  
    // This replaces the recursively inferred type with a schema. 
    member x.Type                       = x.Data.val_type
    member x.Accessibility              = x.Data.val_access
    /// Range of the definition (implementation) of the value, used by Visual Studio 
    /// Updated by mutation when the implementation is matched against the signature. 
    member x.DefinitionRange            = x.Data.val_defn_range
    /// The value of a value or member marked with [<LiteralAttribute>] 
    member x.LiteralValue               = x.Data.val_const
    member x.TopValInfo : ValTopReprInfo option = x.Data.val_top_repr_info
    member x.Id                         = ident(x.LogicalName,x.Range)
    /// Is this represented as a "top level" static binding (i.e. a static field, static member,
    /// instance member), rather than an "inner" binding that may result in a closure.
    ///
    /// This is implied by IsMemberOrModuleBinding, however not vice versa, for two reasons.
    /// Some optimizations mutate this value when they decide to change the representation of a 
    /// binding to be IsCompiledAsTopLevel. Second, even immediately after type checking we expect
    /// some non-module, non-member bindings to be marked IsCompiledAsTopLevel, e.g. 'y' in 
    /// 'let x = let y = 1 in y + y' (NOTE: check this, don't take it as gospel)
    member x.IsCompiledAsTopLevel       = x.TopValInfo.IsSome 


    member x.LinkagePartialKey : ValLinkagePartialKey = 
        assert x.IsCompiledAsTopLevel
        { LogicalName = x.LogicalName; 
          MemberParentMangledName = (if x.IsMember then Some x.MemberApparentParent.LogicalName else None);
          MemberIsOverride = x.IsOverrideOrExplicitImpl;
          TotalArgCount = if x.IsMember then List.sum x.TopValInfo.Value.AritiesOfArgs else 0 }

    member x.LinkageFullKey : ValLinkageFullKey = 
        assert x.IsCompiledAsTopLevel
        ValLinkageFullKey(x.LinkagePartialKey, (if x.IsMember then Some x.Type else None))


    /// Is this a member definition or module definition?
    member x.IsMemberOrModuleBinding    = x.Data.val_flags.IsMemberOrModuleBinding
    member x.IsExtensionMember          = x.Data.val_flags.IsExtensionMember

    member x.ReflectedDefinition        = x.Data.val_defn

    /// Is this a member, if so some more data about the member.
    ///
    /// Note, the value may still be (a) an extension member or (b) and abtract slot without
    /// a true body. These cases are often causes of bugs in the compiler.
    member x.MemberInfo                 = x.Data.val_member_info

    /// Is this a member, if so some more data about the member.
    ///
    /// Note, the value may still be (a) an extension member or (b) and abtract slot without
    /// a true body. These cases are often causes of bugs in the compiler.
    member x.IsMember                   = x.MemberInfo.IsSome

    /// Is this a member, excluding extension members
    member x.IsIntrinsicMember          = x.IsMember && not x.IsExtensionMember

    /// Is this a value in a module, or an extension member, but excluding compiler generated bindings from optimizations
    member x.IsModuleBinding            = x.IsMemberOrModuleBinding && not x.IsMember 

    /// Is this something compiled into a module, i.e. a user-defined value, an extension member or a compiler-generated value
    member x.IsCompiledIntoModule       = x.IsExtensionMember || x.IsModuleBinding

    /// Is this an instance member. 
    ///
    /// Note, the value may still be (a) an extension member or (b) and abtract slot without
    /// a true body. These cases are often causes of bugs in the compiler.
    member x.IsInstanceMember = x.IsMember && x.MemberInfo.Value.MemberFlags.MemberIsInstance

    /// Is this a 'new' constructor member
    member x.IsConstructor              =
        match x.MemberInfo with 
        | Some(memberInfo) when not x.IsExtensionMember && (memberInfo.MemberFlags.MemberKind = MemberKindConstructor) -> true
        | _ -> false

    /// Is this a compiler-generated class constructor member
    member x.IsClassConstructor              =
        match x.MemberInfo with 
        | Some(memberInfo) when not x.IsExtensionMember && (memberInfo.MemberFlags.MemberKind = MemberKindClassConstructor) -> true
        | _ -> false

    /// Was this member declared 'override' or is it an implementation of an interface slot
    member x.IsOverrideOrExplicitImpl                 =
        match x.MemberInfo with 
        | Some(memberInfo) when memberInfo.MemberFlags.MemberIsOverrideOrExplicitImpl -> true
        | _ -> false
            
    /// Was the value declared 'mutable'
    member x.IsMutable                  = (match x.Data.val_flags.MutabilityInfo with Immutable -> false | Mutable -> true)

    /// Was the value inferred to be a method or function that definitely makes no critical tailcalls?
    member x.MakesNoCriticalTailcalls = x.Data.val_flags.MakesNoCriticalTailcalls
    
    /// Was the value ever referenced?
    member x.HasBeenReferenced = x.Data.val_flags.HasBeenReferenced

    /// Was the value ever referenced?
    member x.IsCompiledAsStaticPropertyWithoutField = x.Data.val_flags.IsCompiledAsStaticPropertyWithoutField

    /// Does the value allow the use of an explicit type instantiation (i.e. does it itself have explciti type arguments,
    /// or does it have a signature?)
    member x.PermitsExplicitTypeInstantiation = x.Data.val_flags.PermitsExplicitTypeInstantiation

    /// Is this a member generated from the de-sugaring of 'let' function bindings in the implicit class syntax?
    member x.IsIncrClassGeneratedMember     = x.IsCompilerGenerated && x.Data.val_flags.IsIncrClassSpecialMember

    /// Is this a constructor member generated from the de-sugaring of implicit constructor for a class type?
    member x.IsIncrClassConstructor = x.IsConstructor && x.Data.val_flags.IsIncrClassSpecialMember

    /// Get the information about the value used during type inference
    member x.RecursiveValInfo           = x.Data.val_flags.RecursiveValInfo

    /// Is this a 'base' or 'this' value?
    member x.BaseOrThisInfo             = x.Data.val_flags.BaseOrThisInfo

    //  Was this value declared to be a type function, e.g. "let f<'a> = typeof<'a>"
    member x.IsTypeFunction             = x.Data.val_flags.IsTypeFunction

    /// Get the inline declaration on the value
    member x.InlineInfo                 = x.Data.val_flags.InlineInfo

    /// Does the inline declaration for the value indicate that the value must be inlined?
    member x.MustInline                 = mustinline(x.InlineInfo)

    /// Was the value generated by the compiler?
    ///
    /// Note: this is true for the overrides generated by hash/compare augmentations
    member x.IsCompilerGenerated        = x.Data.val_flags.IsCompilerGenerated
    
    /// Get the declared attributes for the value
    member x.Attribs                    = x.Data.val_attribs

    /// Get the declared documentation for the value
    member x.XmlDoc                     = x.Data.val_xmldoc
    
    ///Get the signature for the value's XML documentation
    member x.XmlDocSig 
        with get() = x.Data.val_xmldocsig
        and set(v) = x.Data.val_xmldocsig <- v

    /// The parent type or module, if any (None for expression bindings and parameters)
    member x.ActualParent               = x.Data.val_actual_parent

    /// Get the actual parent entity for the value (a module or a type), i.e. the entity under which the
    /// value will appear in compiled code. For extension members this is the module where the extension member
    /// is declared.
    member x.TopValActualParent = 
        match x.ActualParent  with 
        | Parent tcref -> tcref
        | ParentNone -> error(InternalError("TopValActualParent: does not have a parent",x.Range))
            
    /// Get the apparent parent entity for a member
    member x.MemberApparentParent : TyconRef = 
        match x.MemberInfo with 
        | Some membInfo -> membInfo.ApparentParent
        | None -> error(InternalError("MemberApparentParent",x.Range))

    /// Get the apparent parent entity for the value, i.e. the entity under with which the
    /// value is associated. For extension members this is the nominal type the member extends.
    /// For other values it is just the actual parent.
    member x.ApparentParent = 
        match x.MemberInfo with 
        | Some membInfo -> Parent(membInfo.ApparentParent)
        | None -> x.ActualParent

    /// Get the public path to the value, if any? Should be set if and only if
    /// IsMemberOrModuleBinding is set.
    //
    // We use it here:
    //   - in opt.fs   : when compiling fslib, we bind an entry for the value in a global table (see bind_escaping_local_vspec)
    //   - in ilxgen.fs: when compiling fslib, we bind an entry for the value in a global table (see bind_escaping_local_vspec)
    //   - in opt.fs   : (full_display_text_of_vref) for error reporting of non-inlinable values
    //   - in service.fs (boutput_item_description): to display the full text of a value's binding location
    //   - in check.fs: as a boolean to detect public values for saving quotations 
    //   - in ilxgen.fs: as a boolean to detect public values for saving quotations 
    //   - in MakeExportRemapping, to build non-local references for values
    member x.PublicPath                 = 
        match x.ActualParent  with 
        | Parent eref -> 
            match eref.PublicPath with 
            | None -> None
            | Some p -> Some(ValPubPath(p,x.LinkageFullKey))
        | ParentNone -> 
            None


    member x.IsDispatchSlot = 
        match x.MemberInfo with 
        | Some(membInfo) -> membInfo.MemberFlags.MemberIsDispatchSlot 
        | _ -> false

    /// Get the type of the value including any generic type parameters
    member x.TypeScheme = 
        match x.Type with 
        | TType_forall(tps,tau) -> tps,tau
        | ty -> [],ty

    /// Get the type of the value after removing any generic type parameters
    member x.TauType = 
        match x.Type with 
        | TType_forall(_,tau) -> tau
        | ty -> ty

    /// Get the generic type parameters for the value
    member x.Typars = 
        match x.Type with 
        | TType_forall(tps,_) -> tps
        | _ -> []

    /// The name of the method. 
    ///   - If this is a property then this is 'get_Foo' or 'set_Foo'
    ///   - If this is an implementation of an abstract slot then this is the name of the method implemented by the abstract slot
    ///   - If this is an extension member then this will be the simple name
    member x.LogicalName = 
        match x.MemberInfo with 
        | None -> x.Data.val_logical_name
        | Some membInfo -> 
            match membInfo.ImplementedSlotSigs with 
            | slotsig :: _ -> slotsig.Name
            | _ -> x.Data.val_logical_name

    /// The name of the method in compiled code (with some exceptions where ilxgen.fs decides not to use a method impl)
    ///   - If this is a property then this is 'get_Foo' or 'set_Foo'
    ///   - If this is an implementation of an abstract slot then this may be a mangled name
    ///   - If this is an extension member then this will be a mangled name
    ///   - If this is an operator then this is 'op_Addition'
    member x.CompiledName =
        let givenName = 
            match x.Data.val_compiled_name with 
            | Some n -> n
            | None -> x.LogicalName 
        givenName

    ///   - If this is a property then this is 'Foo' 
    ///   - If this is an implementation of an abstract slot then this is the name of the property implemented by the abstract slot
    member x.PropertyName = 
        let logicalName =  x.LogicalName
        chopPropertyName logicalName


    /// The name of the method. 
    ///   - If this is a property then this is 'Foo' 
    ///   - If this is an implementation of an abstract slot then this is the name of the method implemented by the abstract slot
    ///   - If this is an operator then this is 'op_Addition'
    member x.CoreDisplayName = 
        match x.MemberInfo with 
        | Some membInfo -> 
            match membInfo.MemberFlags.MemberKind with 
            | MemberKindClassConstructor 
            | MemberKindConstructor 
            | MemberKindMember -> x.LogicalName
            | MemberKindPropertyGetSet 
            | MemberKindPropertySet
            | MemberKindPropertyGet -> x.PropertyName
        | None -> x.LogicalName

    ///   - If this is a property then this is 'Foo' 
    ///   - If this is an implementation of an abstract slot then this is the name of the method implemented by the abstract slot
    ///   - If this is an operator then this is '(+)'
    member x.DisplayName = 
        demangleOperatorName x.CoreDisplayName


    // OSGN support
    static member NewUnlinked() : Val  = { Data = Unchecked.defaultof<_> }
    static member New(data) : Val = { Data = data }
    member x.Link(tg) = x.Data <- tg
    member x.IsLinked = match box x.Data with null -> false | _ -> true 

    override x.ToString() = x.LogicalName
    
    
and 
    [<NoEquality; NoComparison>]
    [<StructuredFormatDisplay("{val_logical_name}")>]
    ValData =
    { val_logical_name: string;
      val_compiled_name: string option;
      val_range: range;
      mutable val_defn_range: range; 
      mutable val_type: typ;
      val_stamp: stamp; 
      /// See vflags section further below for encoding/decodings here 
      mutable val_flags: ValFlags;
      mutable val_const: Constant option;
      
      /// What is the original, unoptimized, closed-term definition, if any? 
      /// Used to implement [<ReflectedDefinition>]
      mutable val_defn: expr option; 

      /// How visible is this? 
      val_access: Accessibility; 

      /// Is the value actually an instance method/property/event that augments 
      /// a type, and if so what name does it take in the IL?
      val_member_info: ValMemberInfo option;

      /// Custom attributes attached to the value. These contain references to other values (i.e. constructors in types). Mutable to fixup  
      /// these value references after copying a colelction of values. 
      val_attribs: Attribs;

      /// Top level values have an arity inferred and/or specified
      /// signatures.  The arity records the number of arguments preferred 
      /// in each position for a curried functions. The currying is based 
      /// on the number of lambdas, and in each position the elements are 
      /// based on attempting to deconstruct the type of the argument as a 
      /// tuple-type.  The field is mutable because arities for recursive 
      /// values are only inferred after the r.h.s. is analyzed, but the 
      /// value itself is created before the r.h.s. is analyzed. 
      ///
      /// TLR also sets this for inner bindings that it wants to 
      /// represent as "top level" bindings.
     
      mutable val_top_repr_info: ValTopReprInfo option;


      // The fresh temporary should just be created with the right parent
      mutable val_actual_parent: ParentRef;

      /// XML documentation attached to a value.
      val_xmldoc : XmlDoc; 
      
      /// XML documentation signature for the value
      mutable val_xmldocsig : string;
  } 

and 
    [<NoEquality; NoComparison>]
    ValMemberInfo = 
    { /// The parent type. For an extension member this is the type being extended 
      ApparentParent: TyconRef;  

      /// Gets updated with full slotsig after interface implementation relation is checked 
      mutable ImplementedSlotSigs: SlotSig list; 

      /// Gets updated with 'true' if an abstract slot is implemented in the file being typechecked.  Internal only. 
      mutable IsImplemented: bool;                      

      MemberFlags: MemberFlags }


and 
    [<StructuredFormatDisplay("{Display}")>]
    NonLocalValOrMemberRef = 
    { /// A reference to the entity containing the value or member. THis will always be a non-local reference
      EnclosingEntity : EntityRef; 
      /// The name of the value, or the full signature of the member
      ItemKey: ValLinkageFullKey; }

    member x.Ccu = x.EnclosingEntity.nlr.Ccu
    member x.AssemblyName = x.EnclosingEntity.nlr.AssemblyName
    member x.Display = x.ToString()
    override x.ToString() = x.EnclosingEntity.nlr.ToString() + "::" + x.ItemKey.PartialKey.LogicalName
      
/// A public path records where a construct lives within the global namespace
/// of a CCU.
and PublicPath      = 
    | PubPath of string[] 
    member x.EnclosingPath = 
        let (PubPath(pp)) = x 
        assert (pp.Length >= 1)
        pp.[0..pp.Length-2]

and ValPublicPath      = 
    | ValPubPath of PublicPath * ValLinkageFullKey

/// The information ILXGEN needs about the location of an item
and CompilationPath = 
    | CompPath of ILScopeRef * (string * ModuleOrNamespaceKind) list
    member x.ILScopeRef = (let (CompPath(scoref,_)) = x in scoref)
    member x.AccessPath = (let (CompPath(_,p)) = x in p)

/// Index into the namespace/module structure of a particular CCU 
and NonLocalEntityRef    = 
    | NonLocalEntityRef of CcuThunk * string[]
        
    member nleref.TryDeref = 
        let (NonLocalEntityRef(ccu,p)) = nleref 
        if ccu.IsUnresolvedReference then None else
        let rec loop (entity:Entity)  i = 
            if i >= p.Length then Some entity
            else 
                let next = entity.ModuleOrNamespaceType.AllEntitiesByCompiledAndLogicalMangledNames.TryFind(p.[i])
                match next with 
                | Some res -> loop res (i+1)
                | None -> None

        match loop ccu.Contents 0 with
        | Some _ as r -> r
        | None ->
            // OK, the lookup failed. Check if we can redirect through a type forwarder on this assembly.
            // Look for a forwarder for each prefix-path
            let rec tryForwardPrefixPath i = 
                if i < p.Length then 
                    match ccu.TryForward(p.[0..i-1],p.[i]) with
                    | Some tcref -> 
                       // OK, found a forwarder, now continue with the lookup to find the nested type
                       loop tcref.Deref (i+1)
                    | None -> tryForwardPrefixPath (i+1)
                else
                    None
            tryForwardPrefixPath 0
        
    member nleref.Ccu =
        let (NonLocalEntityRef(ccu,_)) = nleref 
        ccu
    member nleref.Path =
        let (NonLocalEntityRef(_,p)) = nleref 
        p

    member nleref.DisplayName =
        String.concat "." nleref.Path

    member nleref.LastItemMangledName = 
        let p = nleref.Path
        p.[p.Length-1]
    member nleref.EnclosingMangledPath =  
        let p = nleref.Path
        p.[0..p.Length-2]
        
    member nleref.AssemblyName = nleref.Ccu.AssemblyName

    member nleref.Deref = 
        match nleref.TryDeref with 
        | Some res -> res
        | None -> 
              errorR (InternalUndefinedItemRef (FSComp.SR.tastUndefinedItemRefModuleNamespace, nleref.DisplayName, nleref.AssemblyName, "<some module on this path>")); 
              raise (KeyNotFoundException())
        
    member nleref.TryModuleOrNamespaceType = 
        nleref.TryDeref |> Option.map (fun v -> v.ModuleOrNamespaceType) 

    member nleref.ModuleOrNamespaceType = 
        nleref.Deref.ModuleOrNamespaceType

    override x.ToString() = x.DisplayName
        
and 
    [<StructuredFormatDisplay("{LogicalName}")>]
    [<NoEquality; NoComparison>]
    EntityRef = 
    { /// Indicates a reference to something bound in this CCU 
      mutable binding: Entity 
      /// Indicates a reference to something bound in another CCU 
      nlr: NonLocalEntityRef }
    member x.IsLocalRef = match box x.nlr with null -> true | _ -> false
    member x.IsResolved = match box x.binding with null -> false | _ -> true
    member x.PrivateTarget = x.binding
    member x.ResolvedTarget = x.binding

    member private tcr.Resolve() = 
        let res = tcr.nlr.TryDeref
        match res with 
        | Some r -> 
             tcr.binding <- r; 
        | None -> 
             ()

    // Dereference the TyconRef to a Tycon. Amortize the cost of doing this.
    // This path should not allocate in the amortized case
    member tcr.Deref = 
        match box tcr.binding with 
        | null ->
            tcr.Resolve()
            match box tcr.binding with 
            | null -> error (InternalUndefinedItemRef (FSComp.SR.tastUndefinedItemRefModuleNamespaceType, tcr.nlr.DisplayName, tcr.nlr.AssemblyName, tcr.nlr.LastItemMangledName))
            | _ -> tcr.binding
        | _ -> 
            tcr.binding

    // Dereference the TyconRef to a Tycon option.
    member tcr.TryDeref = 
        match box tcr.binding with 
        | null -> 
            tcr.Resolve()
            match box tcr.binding with 
            | null -> None
            | _ -> Some tcr.binding

        | _ -> 
            Some tcr.binding

    /// Is the destination assembly available?
    member tcr.CanDeref = tcr.TryDeref.IsSome

    override x.ToString() = 
       if x.IsLocalRef then 
           x.ResolvedTarget.DisplayName 
       else 
           x.nlr.DisplayName 


    member x.CompiledRepresentation = x.Deref.CompiledRepresentation
    member x.CompiledRepresentationForTyrepNamed = x.Deref.CompiledRepresentationForTyrepNamed
    member x.LogicalName = x.Deref.LogicalName
    member x.CompiledName = x.Deref.CompiledName
    member x.DisplayName = x.Deref.DisplayName
    member x.DisplayNameWithUnderscoreTypars = x.Deref.DisplayNameWithUnderscoreTypars
    member x.Range = x.Deref.Range
    member x.Stamp = x.Deref.Stamp
    member x.Attribs = x.Deref.Attribs
    member x.XmlDoc = x.Deref.XmlDoc
    member x.XmlDocSig = x.Deref.XmlDocSig
    member x.ModuleOrNamespaceType = x.Deref.ModuleOrNamespaceType
    
    member x.DemangledModuleOrNamespaceName = x.Deref.DemangledModuleOrNamespaceName

    member x.TypeContents = x.Deref.TypeContents
    member x.TypeOrMeasureKind = x.Deref.TypeOrMeasureKind
    member x.Id = x.Deref.Id
    member x.TypeReprInfo = x.Deref.TypeReprInfo
    member x.ExceptionInfo = x.Deref.ExceptionInfo
    member x.IsExceptionDecl = x.Deref.IsExceptionDecl
    
    member x.Typars(m) = x.Deref.Typars(m)
    member x.TyparsNoRange = x.Deref.TyparsNoRange
    member x.TypeAbbrev = x.Deref.TypeAbbrev
    member x.IsTypeAbbrev = x.Deref.IsTypeAbbrev
    member x.TypeReprAccessibility = x.Deref.TypeReprAccessibility
    member x.CompiledReprCache = x.Deref.CompiledReprCache
    member x.PublicPath : PublicPath option = x.Deref.PublicPath
    member x.Accessibility = x.Deref.Accessibility
    member x.IsPrefixDisplay = x.Deref.IsPrefixDisplay
    member x.IsModuleOrNamespace  = x.Deref.IsModuleOrNamespace
    member x.IsNamespace          = x.Deref.IsNamespace
    member x.IsModule             = x.Deref.IsModule
    member x.CompilationPathOpt   = x.Deref.CompilationPathOpt
    member x.CompilationPath      = x.Deref.CompilationPath
    member x.AllFieldTable        = x.Deref.AllFieldTable
    member x.AllFieldsArray       = x.Deref.AllFieldsArray
    member x.AllFieldsAsList = x.Deref.AllFieldsAsList
    member x.TrueFieldsAsList = x.Deref.TrueFieldsAsList
    member x.TrueInstanceFieldsAsList = x.Deref.TrueInstanceFieldsAsList
    member x.AllInstanceFieldsAsList = x.Deref.AllInstanceFieldsAsList
    member x.GetFieldByIndex(n)        = x.Deref.GetFieldByIndex(n)
    member x.GetFieldByName(n)         = x.Deref.GetFieldByName(n)
    member x.UnionTypeInfo             = x.Deref.UnionTypeInfo
    member x.UnionCasesArray           = x.Deref.UnionCasesArray
    member x.UnionCasesAsList     = x.Deref.UnionCasesAsList
    member x.GetUnionCaseByName(n)     = x.Deref.GetUnionCaseByName(n)
    member x.FSharpObjectModelTypeInfo = x.Deref.FSharpObjectModelTypeInfo
    member x.InterfacesOfFSharpTycon = x.Deref.InterfacesOfFSharpTycon
    member x.InterfaceTypesOfFSharpTycon = x.Deref.InterfaceTypesOfFSharpTycon
    member x.MembersOfFSharpTyconSorted = x.Deref.MembersOfFSharpTyconSorted
    member x.MembersOfFSharpTyconByName = x.Deref.MembersOfFSharpTyconByName
    member x.IsStructOrEnumTycon             = x.Deref.IsStructOrEnumTycon
    member x.IsAsmReprTycon            = x.Deref.IsAsmReprTycon
    member x.IsMeasureableReprTycon    = x.Deref.IsMeasureableReprTycon
    

    member x.GeneratedHashAndEqualsWithComparerValues = x.Deref.GeneratedHashAndEqualsWithComparerValues
    member x.GeneratedCompareToWithComparerValues = x.Deref.GeneratedCompareToWithComparerValues
    member x.GeneratedCompareToValues = x.Deref.GeneratedCompareToValues
    member x.GeneratedHashAndEqualsValues = x.Deref.GeneratedHashAndEqualsValues
    
    member x.IsILTycon                = x.Deref.IsILTycon
    member x.ILTyconInfo              = x.Deref.ILTyconInfo
    member x.ILTyconRawMetadata       = x.Deref.ILTyconRawMetadata
    member x.IsUnionTycon             = x.Deref.IsUnionTycon
    member x.UnionInfo                = x.Deref.UnionInfo
    member x.IsRecordTycon            = x.Deref.IsRecordTycon
    member x.IsFSharpObjectModelTycon = x.Deref.IsFSharpObjectModelTycon
    member x.IsHiddenReprTycon        = x.Deref.IsHiddenReprTycon

    member x.IsFSharpInterfaceTycon   = x.Deref.IsFSharpInterfaceTycon
    member x.IsFSharpDelegateTycon    = x.Deref.IsFSharpDelegateTycon
    member x.IsFSharpEnumTycon        = x.Deref.IsFSharpEnumTycon
    member x.IsILEnumTycon            = x.Deref.IsILEnumTycon
    member x.IsEnumTycon              = x.Deref.IsEnumTycon

    member x.IsFSharpStructOrEnumTycon      = x.Deref.IsFSharpStructOrEnumTycon

    member x.IsILStructTycon          = x.Deref.IsILStructTycon
    member x.PreEstablishedHasDefaultConstructor = x.Deref.PreEstablishedHasDefaultConstructor
    member x.HasSelfReferentialConstructor = x.Deref.HasSelfReferentialConstructor


/// note: ModuleOrNamespaceRef and TyconRef are type equivalent 
and ModuleOrNamespaceRef       = EntityRef
and TyconRef       = EntityRef

/// References are either local or nonlocal
and 
    [<NoEquality; NoComparison>]
    [<StructuredFormatDisplay("{LogicalName}")>]
    ValRef = 
    { /// Indicates a reference to something bound in this CCU 
      mutable binding: Val 
      /// Indicates a reference to something bound in another CCU 
      nlr: NonLocalValOrMemberRef }
    member x.IsLocalRef = match box x.nlr with null -> true | _ -> false
    member x.IsResolved = match box x.binding with null -> false | _ -> true
    member x.PrivateTarget = x.binding
    member x.ResolvedTarget = x.binding

    member vr.Deref = 
        match box vr.binding with 
        | null ->
            let res = 
                let nlr = vr.nlr 
                let e =  nlr.EnclosingEntity.Deref 
                let possible = e.ModuleOrNamespaceType.TryLinkVal(nlr.EnclosingEntity.nlr.Ccu, nlr.ItemKey)
                match possible with 
                | None -> error (InternalUndefinedItemRef (FSComp.SR.tastUndefinedItemRefVal, e.DisplayName, nlr.AssemblyName, sprintf "%+A" nlr.ItemKey.PartialKey))
                | Some h -> h
            vr.binding <- res; 
            res 
        | _ -> vr.binding

    member vr.TryDeref = 
        match box vr.binding with 
        | null -> 
            let resOpt = 
                vr.nlr.EnclosingEntity.TryDeref |> Option.bind (fun e -> 
                    e.ModuleOrNamespaceType.TryLinkVal(vr.nlr.EnclosingEntity.nlr.Ccu, vr.nlr.ItemKey))
            match resOpt with 
            | None -> ()
            | Some res -> 
                vr.binding <- res; 
            resOpt
        | _ -> 
            Some vr.binding

    member x.Type                       = x.Deref.Type
    member x.TypeScheme                 = x.Deref.TypeScheme
    member x.TauType                    = x.Deref.TauType
    member x.Typars                     = x.Deref.Typars
    member x.LogicalName                = x.Deref.LogicalName
    member x.DisplayName                = x.Deref.DisplayName
    member x.CoreDisplayName            = x.Deref.CoreDisplayName
    member x.Range                      = x.Deref.Range

    member x.Accessibility              = x.Deref.Accessibility
    member x.ActualParent               = x.Deref.ActualParent
    member x.ApparentParent             = x.Deref.ApparentParent
    member x.DefinitionRange            = x.Deref.DefinitionRange
    member x.LiteralValue               = x.Deref.LiteralValue
    member x.Id                         = x.Deref.Id
    member x.PropertyName               = x.Deref.PropertyName
    member x.Stamp                      = x.Deref.Stamp
    member x.IsCompiledAsTopLevel       = x.Deref.IsCompiledAsTopLevel
    member x.IsDispatchSlot             = x.Deref.IsDispatchSlot
    member x.CompiledName         = x.Deref.CompiledName

    member x.PublicPath                 = x.Deref.PublicPath
    member x.ReflectedDefinition        = x.Deref.ReflectedDefinition
    member x.IsConstructor              = x.Deref.IsConstructor
    member x.IsOverrideOrExplicitImpl   = x.Deref.IsOverrideOrExplicitImpl
    member x.MemberInfo                 = x.Deref.MemberInfo
    member x.IsMember                   = x.Deref.IsMember
    member x.IsModuleBinding            = x.Deref.IsModuleBinding
    member x.IsInstanceMember           = x.Deref.IsInstanceMember

    member x.IsMutable                  = x.Deref.IsMutable
    member x.PermitsExplicitTypeInstantiation  = x.Deref.PermitsExplicitTypeInstantiation
    member x.MakesNoCriticalTailcalls  = x.Deref.MakesNoCriticalTailcalls
    member x.IsMemberOrModuleBinding    = x.Deref.IsMemberOrModuleBinding
    member x.IsExtensionMember          = x.Deref.IsExtensionMember
    member x.IsIncrClassConstructor = x.Deref.IsIncrClassConstructor
    member x.IsIncrClassGeneratedMember = x.Deref.IsIncrClassGeneratedMember
    member x.RecursiveValInfo           = x.Deref.RecursiveValInfo
    member x.BaseOrThisInfo             = x.Deref.BaseOrThisInfo
    member x.IsTypeFunction             = x.Deref.IsTypeFunction
    member x.TopValInfo                  = x.Deref.TopValInfo
    member x.InlineInfo                 = x.Deref.InlineInfo
    member x.MustInline                 = x.Deref.MustInline
    member x.IsCompilerGenerated        = x.Deref.IsCompilerGenerated
    member x.Attribs                    = x.Deref.Attribs
    member x.XmlDoc                     = x.Deref.XmlDoc
    member x.XmlDocSig                  = x.Deref.XmlDocSig
    member x.TopValActualParent         = x.Deref.TopValActualParent
    member x.MemberApparentParent       = x.Deref.MemberApparentParent
    override x.ToString() = 
       if x.IsLocalRef then x.ResolvedTarget.DisplayName 
       else x.nlr.ToString()

and UnionCaseRef = 
    | UCRef of TyconRef * string
    member x.TyconRef = let (UCRef(tcref,_)) = x in tcref
    member x.CaseName = let (UCRef(_,nm)) = x in nm
    member x.Tycon = x.TyconRef.Deref

and RecdFieldRef = 
    | RFRef of TyconRef * string
    member x.TyconRef = let (RFRef(tcref,_)) = x in tcref
    member x.FieldName = let (RFRef(_,id)) = x in id
    member x.Tycon = x.TyconRef.Deref

and 
  /// The algebra of types
    [<NoEquality; NoComparison>]
    typ =
    /// Indicates the type is a universal type, only used for types of values, members and record fields 
    | TType_forall of Typars * typ
    /// Indicates the type is a type application 
    | TType_app of TyconRef * tinst
    /// Indicates the type is a tuple type 
    | TType_tuple of typ list
    /// Indicates the type is a function type 
    | TType_fun of  typ * typ
    /// Indicates the type is a non-F#-visible type representing a "proof" that a union value belongs to a particular union case
    /// These types are not user-visible and will never appear as an inferred type. They are the types given to
    /// the temporaries arising out of pattern matching on union values.
    | TType_ucase of  UnionCaseRef * tinst
    /// Indicates the type is a variable type, whether declared, generalized or an inference type parameter  
    | TType_var of Typar 
    | TType_measure of MeasureExpr

and tinst = typ list 

and MeasureExpr = 
    | MeasureVar of Typar
    | MeasureCon of TyconRef
    | MeasureProd of MeasureExpr*MeasureExpr
    | MeasureInv of MeasureExpr
    | MeasureOne

and 
    [<NoEquality; NoComparison>]
    CcuData = 
    { /// Holds the filename for the DLL, if any 
      ccu_filename: string option; 
      
      /// Holds the data indicating how this assembly/module is referenced from the code being compiled. 
      ccu_scoref: ILScopeRef;
      
      /// A unique stamp for this DLL 
      ccu_stamp: stamp;
      
      /// The fully qualified assembly reference string to refer to this assembly. This is persisted in quotations 
      ccu_qname: string option; 
      
      /// A hint as to where does the code for the CCU live (e.g what was the tcConfig.implicitIncludeDir at compilation time for this DLL?) 
      ccu_code_dir: string; 
      
      /// Indicates that this DLL was compiled using the F# compiler 
      ccu_fsharp: bool; 
      
      /// Indicates that this DLL uses quotation literals somewhere. This is used to implement a restriction on static linking
      mutable ccu_usesQuotations : bool;
      
      /// A handle to the full specification of the contents of the module contained in this ccu
      // NOTE: may contain transient state during typechecking 
      mutable ccu_contents: ModuleOrNamespace;
      
      /// A helper function used to link method signatures using type equality. This is effectively a forward call to the type equality 
      /// logic in tastops.fs
      mutable ccu_memberSignatureEquality : (typ -> typ -> bool) 
      
      ccu_forwarders : CcuTypeForwarderTable }

and CcuTypeForwarderTable = Lazy<Map<string[] * string, EntityRef>>

and CcuReference =  string // ILAssemblyRef

/// A relinkable handle to the contents of a compilation unit. Relinking is performed by mutation.
and CcuThunk = 
    { mutable target: CcuData;
      mutable orphanfixup : bool;
      name: CcuReference  }
      

    member ccu.Deref = 
        if isNull ccu.target || ccu.orphanfixup then 
            raise(UnresolvedReferenceNoRange ccu.name)
        ccu.target
   
    member ccu.IsUnresolvedReference = (isNull ccu.target || ccu.orphanfixup)

    /// Ensure the ccu is derefable in advance. Supply a path to attach to any resulting error message.
    member ccu.EnsureDerefable(requiringPath:string[]) = 
        // ccu.orphanfixup is true when a reference is missing in the transitive closure of static references that
        // may potentially be required for the metadata of referenced DLLs. It is set to true if the "loader"
        // used in the F# metadata-deserializer or the .NET metadata reader returns a failing value (e.g. None).
        // Note: When used from Visual Studio, the loader will not automatically chase down transitively referenced DLLs - they
        // must be in the explicit references in the project.
        if ccu.IsUnresolvedReference then 
            let path = System.String.Join(".", requiringPath)
            raise(UnresolvedPathReferenceNoRange(ccu.name,path))
            
    member ccu.UsesQuotations with get() = ccu.Deref.ccu_usesQuotations and set(v) = ccu.Deref.ccu_usesQuotations <- v
    member ccu.AssemblyName = ccu.name
    member ccu.ILScopeRef = ccu.Deref.ccu_scoref
    member ccu.Stamp = ccu.Deref.ccu_stamp
    member ccu.FileName = ccu.Deref.ccu_filename
    member ccu.QualifiedName = ccu.Deref.ccu_qname
    member ccu.SourceCodeDirectory = ccu.Deref.ccu_code_dir
    member ccu.IsFSharp = ccu.Deref.ccu_fsharp
    member ccu.Contents = ccu.Deref.ccu_contents
    member ccu.TypeForwarders : Map<string[] * string, EntityRef>  = ccu.Deref.ccu_forwarders.Force()

    static member Create(nm,x) = 
        { target = x; 
          orphanfixup = false;
          name = nm;  }

    static member CreateDelayed(nm) = 
        { target = Unchecked.defaultof<_>; 
          orphanfixup = false;
          name = nm;  }

    member x.Fixup(avail:CcuThunk) = 
        match box x.target with
        | null -> 
            assert (avail.AssemblyName = x.AssemblyName)
            x.target <- 
               (match box avail.target with
                | null -> error(Failure("internal error: ccu thunk '"+avail.name+"' not fixed up!"))
                | _ -> avail.target)
        | _ -> errorR(Failure("internal error: the ccu thunk for assembly "+x.AssemblyName+" not delayed!"));
        
    member x.FixupOrphaned() = 
        match box x.target with
        | null -> x.orphanfixup<-true
        | _ -> errorR(Failure("internal error: the ccu thunk for assembly "+x.AssemblyName+" not delayed!"));
            
    member ccu.TryForward(nlpath:string[],item:string) : EntityRef option  = 
        if ccu.IsUnresolvedReference then None else
        ccu.TypeForwarders.TryFind(nlpath,item) 
        //printfn "trying to forward %A::%s from ccu '%s', res = '%A'" p n ccu.AssemblyName res.IsSome

    member ccu.MemberSignatureEquality(ty1:typ, ty2:typ) = 
        ccu.Deref.ccu_memberSignatureEquality ty1 ty2
    

    override ccu.ToString() = ccu.AssemblyName

/// The result of attempting to resolve an assembly name to a full ccu.
/// UnresolvedCcu will contain the name of the assembly that could not be resolved.
and CcuResolutionResult =
    | ResolvedCcu of CcuThunk
    | UnresolvedCcu of string

and PickledModuleInfo =
  { mspec: ModuleOrNamespace;
    compileTimeWorkingDir: string;
    usesQuotations : bool }

//---------------------------------------------------------------------------
// Attributes
//---------------------------------------------------------------------------

and Attribs = Attrib list 

and AttribKind = 
  /// Indicates an attribute refers to a type defined in an imported .NET assembly 
  | ILAttrib of ILMethodRef 
  /// Indicates an attribute refers to a type defined in an imported F# assembly 
  | FSAttrib of ValRef

/// Attrib(kind,unnamedArgs,propVal,appliedToAGetterOrSetter,range)
and Attrib = 
  | Attrib of TyconRef * AttribKind * AttribExpr list * AttribNamedArg list * bool * range

/// We keep both source expression and evaluated expression around to help intellisense and signature printing
and AttribExpr = AttribExpr of (* source *) expr * (* evaluated *) expr 

/// AttribNamedArg(name,type,isField,value)
and AttribNamedArg = AttribNamedArg of (string*typ*bool*AttribExpr)

/// Constants in expressions
and Constant = 
  | TConst_bool       of bool
  | TConst_sbyte       of sbyte
  | TConst_byte      of byte
  | TConst_int16      of int16
  | TConst_uint16     of uint16
  | TConst_int32      of int32
  | TConst_uint32     of uint32
  | TConst_int64      of int64
  | TConst_uint64     of uint64
  | TConst_nativeint  of int64
  | TConst_unativeint of uint64
  | TConst_float32    of single
  | TConst_float      of double
  | TConst_char       of char
  | TConst_string     of string // in unicode 
  | TConst_decimal    of System.Decimal 
  | TConst_unit
  | TConst_zero // null/zero-bit-pattern 
  

/// Decision trees. Pattern matching has been compiled down to
/// a decision tree by this point.  The right-hand-sides (actions) of
/// the decision tree are labelled by integers that are unique for that
/// particular tree.
and 
    [<NoEquality; NoComparison>]
    DecisionTree = 

    /// Indicates a decision point in a decision tree. 
    | TDSwitch  of 
          (* input: *) expr * 
          (* cases: *) DecisionTreeCase list * 
          (* default: *) DecisionTree option * range

    /// Indicates the decision tree has terminated with success, calling the given target with the given parameters 
    | TDSuccess of 
          (* results: *) FlatExprs * 
          (* target: *) int  

    /// Bind the given value throught the remaining cases of the dtree. 
    | TDBind of 
          (* binding: *) Binding * 
          (* body: *) DecisionTree

and DecisionTreeCase = 
    | TCase of DecisionTreeDiscriminator * DecisionTree

and 
    [<NoEquality; NoComparison>]
    DecisionTreeDiscriminator = 
    /// Test if the input to a decision tree matches the given constructor 
    | TTest_unionconstr of (UnionCaseRef * tinst) 

    /// Test if the input to a decision tree is an array of the given length 
    | TTest_array_length of int * typ  

    /// Test if the input to a decision tree is the given constant value 
    | TTest_const of Constant

    /// Test if the input to a decision tree is null 
    | TTest_isnull 

    /// Test if the input to a decision tree is an instance of the given type 
    | TTest_isinst of (* source: *) typ * (* target: *) typ

    /// Run the active pattern and bind a successful result to the (one) variable in the remaining tree 
    | TTest_query of expr * typ list * (ValRef * tinst) option * int * ActivePatternInfo


/// A target of a decision tree. Can be thought of as a little function, though is compiled as a local block. 
and DecisionTreeTarget = 
    | TTarget of FlatVals * expr * SequencePointInfoForTarget

and Bindings = FlatList<Binding>

and Binding = 
    | TBind of Val * expr * SequencePointInfoForBinding
    member x.Var               = (let (TBind(v,_,_)) = x in v)
    member x.Expr              = (let (TBind(_,e,_)) = x in e)
    member x.SequencePointInfo = (let (TBind(_,_,sp)) = x in sp)
    
// ActivePatternElemRef: active pattern element (deconstruction case), e.g. 'JNil' or 'JCons'. 
// Integer indicates which choice in the target set is being selected by this item. 
and ActivePatternElemRef = 
    | APElemRef of ActivePatternInfo * ValRef * int 

    member x.IsTotalActivePattern = (let (APElemRef(total,_,_)) = x in total)
    member x.ActivePatternVal = (let (APElemRef(_,vref,_)) = x in vref)
    member x.CaseIndex = (let (APElemRef(_,_,n)) = x in n)

and ValTopReprInfo  = 
    | TopValInfo  of (* numTypars: *) TopTyparInfo list * (* args: *) TopArgInfo list list * (* result: *) TopArgInfo 
    member x.ArgInfos       = (let (TopValInfo(_,args,_)) = x in args)
    member x.NumCurriedArgs = (let (TopValInfo(_,args,_)) = x in args.Length)
    member x.NumTypars      = (let (TopValInfo(n,_,_)) = x in n.Length)
    member x.AritiesOfArgs  = (let (TopValInfo(_,args,_)) = x in List.map List.length args)
    member x.KindsOfTypars  = (let (TopValInfo(n,_,_)) = x in n |> List.map (fun (TopTyparInfo(_,k)) -> k))

/// The extra metadata stored about typars for top-level definitions. Any information here is propagated from signature through
/// to the compiled code.
and 
    [<RequireQualifiedAccess>]
    TopArgInfo = 
    { mutable Attribs : Attribs 
      mutable Name : ident option  }

/// The extra metadata stored about typars for top-level definitions. Any information here is propagated from signature through
/// to the compiled code.
and TopTyparInfo = TopTyparInfo of ident * TyparKind

and Typars = Typar list
 
and Exprs = expr list
and FlatExprs = FlatList<expr>
and Vals = Val list
and FlatVals = FlatList<Val>

/// The big type of expressions.  
and expr =
    /// A constant expression. 
    | TExpr_const of Constant * range * typ

    /// Reference a value. The flag is only relevant if the value is an object model member 
    /// and indicates base calls and special uses of object constructors. 
    | TExpr_val of ValRef * ValUseFlag * range

    /// Sequence expressions, used for "a;b", "let a = e in b;a" and "a then b" (the last an OO constructor). 
    | TExpr_seq of expr * expr * SequentialOpKind * SequencePointInfoForSeq * range

    /// Lambda expressions. 
    
    // Why multiple vspecs? A TExpr_lambda taking multiple arguments really accepts a tuple. 
    // But it is in a convenient form to be compile accepting multiple 
    // arguments, e.g. if compiled as a toplevel static method. 

    | TExpr_lambda of uniq * Val option * Val option * Val list * expr * range * typ * SkipFreeVarsCache

    // Type lambdas.  These are used for the r.h.s. of polymorphic 'let' bindings and 
    // for expressions that implement first-class polymorphic values. 
    | TExpr_tlambda of uniq * Typars * expr * range * typ  * SkipFreeVarsCache

    /// Applications combine type and term applications, and are normalized so 
    /// that sequential applications are combined, so "(f x y)" becomes "f [[x];[y]]". 
    /// The type attached to the function is the formal function type, used to ensure we don't build application 
    /// nodes that over-apply when instantiating at function types. 
    | TExpr_app of expr * typ * tinst * Exprs * range

    /// Bind a recursive set of values. 
    | TExpr_letrec of Bindings * expr * range * FreeVarsCache

    /// Bind a value. 
    | TExpr_let of Binding * expr * range * FreeVarsCache

    // Object expressions: A closure that implements an interface or a base type. 
    // The base object type might be a delegate type. 
    | TExpr_obj of 
         (* unique *)           uniq * 
         (* object typ *)       typ *                                         (* <-- NOTE: specifies type parameters for base type *)
         (* base val *)         Val option * 
         (* ctor call *)        expr * 
         (* overrides *)        ObjExprMethod list * 
         (* extra interfaces *) (typ * ObjExprMethod list) list *                   
                                range * 
                                SkipFreeVarsCache

    // Pattern matching. 

    /// Matches are a more complicated form of "let" with multiple possible destinations 
    /// and possibly multiple ways to get to each destination.  
    /// The first mark is that of the expression being matched, which is used 
    /// as the mark for all the decision making and binding that happens during the match. 
    | TExpr_match of SequencePointInfoForBinding * range * DecisionTree * DecisionTreeTarget array * range * typ * SkipFreeVarsCache

    /// If we statically know some infomation then in many cases we can use a more optimized expression 
    /// This is primarily used by terms in the standard library, particularly those implementing overloaded 
    /// operators. 
    | TExpr_static_optimization of StaticOptimization list * expr * expr * range

    /// An intrinsic applied to some (strictly evaluated) arguments 
    /// A few of intrinsics (TOp_try, TOp_while, TOp_for) expect arguments kept in a normal form involving lambdas 
    | TExpr_op of ExprOpSpec * tinst * Exprs * range

    // Indicates the expression is a quoted expression tree. 
    | TExpr_quote of expr * (typ list * Exprs * ExprData) option ref * range * typ  
    
    /// Typechecking residue: Indicates a free choice of typars that arises due to 
    /// minimization of polymorphism at let-rec bindings.  These are 
    /// resolved to a concrete instantiation on subsequent rewrites. 
    | TExpr_tchoose of Typars * expr * range

    /// Typechecking residue: A TExpr_link occurs for every use of a recursively bound variable. While type-checking 
    /// the recursive bindings a dummy expression is stored in the mutable reference cell. 
    /// After type checking the bindings this is replaced by a use of the variable, perhaps at an 
    /// appropriate type instantiation. These are immediately eliminated on subsequent rewrites. 
    | TExpr_link of expr ref

/// A type for a module-or-namespace-fragment and the actual definition of the module-or-namespace-fragment
and ModuleOrNamespaceExprWithSig = 
    | TMTyped of 
         /// The module_typ is a binder. However it is not used in the ModuleOrNamespaceExpr: it is only referenced from the 'outside' 
         ModuleOrNamespaceType 
         * ModuleOrNamespaceExpr
         * range

/// The contents of a module-or-namespace-fragment definition 
and ModuleOrNamespaceExpr = 
    /// Indicates the module is a module with a signature 
    | TMAbstract of ModuleOrNamespaceExprWithSig
    /// Indicates the module fragment is made of several module fragments in succession 
    | TMDefs     of ModuleOrNamespaceExpr list  
    /// Indicates the module fragment is a 'let' definition 
    | TMDefLet   of Binding * range
    /// Indicates the module fragment is an evaluation of expression for side-effects
    | TMDefDo   of expr * range
    /// Indicates the module fragment is a 'rec' definition of types, values and modules
    | TMDefRec   of Tycon list * Bindings * ModuleOrNamespaceBinding list * range

/// A named module-or-namespace-fragment definition 
and ModuleOrNamespaceBinding = 
    | TMBind of 
         /// This ModuleOrNamespace that represents the compilation of a module as a class. 
         /// The same set of tycons etc. are bound in the ModuleOrNamespace as in the ModuleOrNamespaceExpr
         ModuleOrNamespace * 
         /// This is the body of the module/namespace 
         ModuleOrNamespaceExpr


and RecordConstructionInfo = 
   /// We're in a constructor. The purpose of the record expression is to 
   /// fill in the fields of a pre-created but uninitialized object 
   | RecdExprIsObjInit
   /// Normal record construction 
   | RecdExpr
   
and 
    [<NoEquality; NoComparison>]
    ExprOpSpec =
    /// An operation representing the creation of a union value of the particular union case
    | TOp_ucase of UnionCaseRef 
    /// An operation representing the creation of an exception value using an F# exception declaration
    | TOp_exnconstr of TyconRef
    /// An operation representing the creation of a tuple value
    | TOp_tuple 
    /// An operation representing the creation of an array value
    | TOp_array
    /// Constant bytes, but a new mutable blob is generated each time the construct is executed 
    | TOp_bytes of byte[] 
    | TOp_uint16s of uint16[] 
    /// An operation representing a lambda-encoded while loop. The special while loop marker is used to mark compilations of 'foreach' expressions
    | TOp_while of SequencePointInfoForWhileLoop * SpecialWhileLoopMarker
    /// An operation representing a lambda-encoded for loop
    | TOp_for of SequencePointInfoForForLoop * ForLoopStyle (* count up or down? *)
    /// An operation representing a lambda-encoded try/catch
    | TOp_try_catch of SequencePointInfoForTry * SequencePointInfoForWith
    /// An operation representing a lambda-encoded try/finally
    | TOp_try_finally of SequencePointInfoForTry * SequencePointInfoForFinally

    /// Construct a record or object-model value. The ValRef is for self-referential class constructors, otherwise 
    /// it indicates that we're in a constructor and the purpose of the expression is to 
    /// fill in the fields of a pre-created but uninitialized object, and to assign the initialized 
    /// version of the object into the optional mutable cell pointed to be the given value. 
    | TOp_recd of RecordConstructionInfo * TyconRef
    
    /// An operation representing setting a record field
    | TOp_rfield_set of RecdFieldRef 
    /// An operation representing getting a record field
    | TOp_rfield_get of RecdFieldRef 
    /// An operation representing getting the address of a record field
    | TOp_field_get_addr of RecdFieldRef       
    /// An operation representing getting an integer tag for a union value representing the union case number
    | TOp_ucase_tag_get of TyconRef 
    /// An operation representing a coercion that proves a union value is of a particular union case. THis is not a test, its
    /// simply added proof to enable us to generate verifiable code for field access on union types
    | TOp_ucase_proof of UnionCaseRef
    /// An operation representing a field-get from a union value, where that value has been proven to be of the corresponding union case.
    | TOp_ucase_field_get of UnionCaseRef * int 
    /// An operation representing a field-get from a union value. THe value is not assumed to have been proven to be of the corresponding union case.
    | TOp_ucase_field_set of  UnionCaseRef * int
    /// An operation representing a field-get from an F# exception value.
    | TOp_exnconstr_field_get of TyconRef * int 
    /// An operation representing a field-set on an F# exception value.
    | TOp_exnconstr_field_set of TyconRef * int 
    /// An operation representing a field-get from an F# tuple value.
    | TOp_tuple_field_get of int 
    /// IL assembly code - type list are the types pushed on the stack 
    | TOp_asm of ILInstr list * typ list 
    /// generate a ldflda on an 'a ref. 
    | TOp_get_ref_lval 
    /// Conversion node, compiled via type-directed translation or to box/unbox 
    | TOp_coerce 
    /// Represents a "rethrow" operation. May not be rebound, or used outside of try-finally, expecting a unit argument 
    | TOp_reraise 
    | TOp_return

    /// Pseudo method calls. This is used for overloaded operations like op_Addition. 
    | TOp_trait_call of TraitConstraintInfo  

    /// Operation nodes represnting C-style operations on byrefs and mutable vals (l-values) 
    | TOp_lval_op of LValueOperation * ValRef 

    /// IL method calls 
    | TOp_ilcall of 
       bool * // virtual call? 
       bool * // protected? 
       bool * // is the object a value type? 
       bool * // newobj call? 
       ValUseFlag * // isSuperInit call? 
       bool * // property? used for reflection 
       bool * // DllImport? if so don't tailcall 
       ILMethodRef * 
       typ list * // instantiation of the enclosing type
       typ list * // instantiation of the method
       typ list   // the types of pushed values if any 

// If this is Some(ty) then it indicates that a .NET 2.0 constrained call is required, witht he given type as the
// static type of the object argument.
and ConstrainedCallInfo = typ option

and SpecialWhileLoopMarker = 
    | NoSpecialWhileLoopMarker
    | WhileLoopForCompiledForEachExprMarker  // marks the compiled form of a 'for ... in ... do ' expression
    
and ForLoopStyle = 
    /// Evaluate start and end once, loop up
    | FSharpForLoopUp 
    /// Evaluate start and end once, loop down
    | FSharpForLoopDown 
    /// Evaluate start once and end multiple times, loop up
    | CSharpForLoopUp

and LValueOperation = 
    /// In C syntax this is: &localv            
    | LGetAddr      
    /// In C syntax this is: *localv_ptr        
    | LByrefGet     
    /// In C syntax this is:  localv = e     , note == *(&localv) = e == LGetAddr; LByrefSet
    | LSet          
    /// In C syntax this is: *localv_ptr = e   
    | LByrefSet     

and SequentialOpKind = 
    /// a ; b 
    | NormalSeq 
    /// let res = a in b;res 
    | ThenDoSeq     

and ValUseFlag =
    /// Indicates a use of a value represents a call to a method that may require
    /// a .NET 2.0 constrained call. A constrained call is only used for calls where 
    // the object argument is a value type or generic type, and the call is to a method
    //  on System.Object, System.ValueType, System.Enum or an interface methods.
    | PossibleConstrainedCall of typ
    /// A normal use of a value
    | NormalValUse
    /// A call to a constructor, e.g. 'inherit C()'
    | CtorValUsedAsSuperInit
    /// A call to a constructor, e.g. 'new C() = new C(3)'
    | CtorValUsedAsSelfInit
    /// A call to a base method, e.g. 'base.OnPaint(args)'
    | VSlotDirectCall
  
and StaticOptimization = 
    | TTyconEqualsTycon of typ * typ
    | TTyconIsStruct of typ 
  
/// A representation of a method in an object expression. 
/// Note: Methods associated with types are represented as val declarations
/// Note: We should probably use val_specs for object expressions, as then the treatment of members 
/// in object expressions could be more unified with the treatment of members in types 
and ObjExprMethod = 
    | TObjExprMethod of SlotSig * Attribs * Typars * Val list list * expr * range
    member x.Id = let (TObjExprMethod(slotsig,_,_,_,_,m)) = x in mksyn_id m slotsig.Name

and SlotSig = 
    | TSlotSig of string * typ * Typars * Typars * SlotParam list list * typ option
    member ss.Name             = let (TSlotSig(nm,_,_,_,_,_)) = ss in nm
    member ss.ImplementedType  = let (TSlotSig(_,ty,_,_,_,_)) = ss in ty
    member ss.ClassTypars      = let (TSlotSig(_,_,ctps,_,_,_)) = ss in ctps
    member ss.MethodTypars     = let (TSlotSig(_,_,_,mtps,_,_)) = ss in mtps
    member ss.FormalParams     = let (TSlotSig(_,_,_,_,ps,_)) = ss in ps
    member ss.FormalReturnType = let (TSlotSig(_,_,_,_,_,rt)) = ss in rt

and SlotParam = 
    | TSlotParam of  string option * typ * bool (* in *) * bool (* out *) * bool (* optional *) * Attribs
    member x.Type = let (TSlotParam(_,ty,_,_,_,_)) = x in ty

/// Specifies the compiled representations of type and exception definitions.  
/// Computed and cached by later phases (never computed type checking).  Cached at 
/// type and exception definitions. Not pickled. Cache an optional ILType object for 
/// non-generic types
and CompiledTypeRepr = 
    | TyrepNamed of ILTypeRef * ILBoxity * ILType option
    | TyrepOpen of ILType  

/// Metadata on values (names of arguments etc. 
module TopValInfo = 
    let unnamedTopArg1 : TopArgInfo = { Attribs=[]; Name=None }

//-------------------------------------------------------------------------
// Managed cached type name lookup tables
//------------------------------------------------------------------------- 
 
type CcuThunk with 
    member ccu.TopModulesAndNamespaces = ccu.Contents.ModuleOrNamespaceType.ModuleAndNamespaceDefinitions
    member ccu.TopTypeAndExceptionDefinitions = ccu.Contents.ModuleOrNamespaceType.TypeAndExceptionDefinitions

//---------------------------------------------------------------------------
// Equality relations on locally defined things 
//---------------------------------------------------------------------------

let typar_ref_eq    (lv1:Typar) (lv2:Typar) = (lv1.Stamp = lv2.Stamp)

let typar_ref_hash (lv1:Typar) = hash lv1.Stamp

/// Equality on CCU references, implemented as reference equality except when unresolved
let ccu_eq (mv1: CcuThunk) (mv2: CcuThunk) = 
    (mv1 === mv2) || 
    (if mv1.IsUnresolvedReference || mv2.IsUnresolvedReference then 
        mv1.AssemblyName = mv2.AssemblyName
     else 
        mv1.Contents === mv2.Contents)

//---------------------------------------------------------------------------
// Get information from refs
//---------------------------------------------------------------------------

exception InternalUndefinedTyconItem of (string * string -> int * string) * TyconRef * string

type UnionCaseRef with 
    member x.UnionCase = 
        let (UCRef(tcref,nm)) = x
        match tcref.GetUnionCaseByName nm with 
        | Some res -> res
        | None -> error (InternalUndefinedTyconItem (FSComp.SR.tastUndefinedTyconItemUnionCase, tcref, nm))
    member x.Attribs = x.UnionCase.Attribs
    member x.Range = x.UnionCase.Range

type RecdFieldRef with 
    member x.RecdField = 
        let (RFRef(tcref,id)) = x
        match tcref.GetFieldByName id with 
        | Some res -> res
        | None -> error (InternalUndefinedTyconItem (FSComp.SR.tastUndefinedTyconItemField, tcref, id))
    member x.PropertyAttribs = x.RecdField.PropertyAttribs
    member x.Range = x.RecdField.Range

//--------------------------------------------------------------------------
// Make references to TAST items
//--------------------------------------------------------------------------

let VRef_local    x : ValRef = { binding=x; nlr=Unchecked.defaultof<_> }      
let VRef_nonlocal x : ValRef = { binding=Unchecked.defaultof<_>; nlr=x }      

let (|VRef_local|VRef_nonlocal|) (x: ValRef) = 
    match box x.nlr with 
    | null -> VRef_local x.binding
    | _ -> VRef_nonlocal x.nlr

let ERef_local x : EntityRef = { binding=x; nlr=Unchecked.defaultof<_> }      
let ERef_nonlocal x : EntityRef = { binding=Unchecked.defaultof<_>; nlr=x }      
let (|ERef_local|ERef_nonlocal|) (x: EntityRef) = 
    match box x.nlr with 
    | null -> ERef_local x.binding
    | _ -> ERef_nonlocal x.nlr

//--------------------------------------------------------------------------
// Type parameters and inference unknowns
//-------------------------------------------------------------------------

let mk_typar_ty (tp:Typar) = 
    match tp.Kind with 
    | KindType -> tp.AsType 
    | KindMeasure -> TType_measure (MeasureVar tp)

//--------------------------------------------------------------------------
// Inference variables
//-------------------------------------------------------------------------- 

let tpref_is_solved (r:Typar) = 
    match r.Solution with 
    | None -> false
    | _ -> true

    
let try_shortcut_solved_upref (r:Typar) = 
    match r.Solution with
    | Some (TType_measure unt) -> unt
    | _ -> failwith "try_shortcut_solved_upref: unsolved"
      

let rec strip_upeqnsA unt = 
    match unt with 
    | MeasureVar r when tpref_is_solved r -> strip_upeqnsA (try_shortcut_solved_upref r)
    | _ -> unt

let rec strip_tpeqnsA ty = 
    match ty with 
    | TType_var r -> 
        match r.Solution with
        | Some soln -> strip_tpeqnsA soln
        | None -> ty
    | TType_measure unt -> TType_measure (strip_upeqnsA unt)
    | _ -> ty

let strip_tpeqns ty = strip_tpeqnsA ty

//--------------------------------------------------------------------------
// Construct local references
//-------------------------------------------------------------------------- 


let mk_nleref ccu mp = NonLocalEntityRef(ccu,mp)
let mk_nested_nleref (nleref:NonLocalEntityRef) id = mk_nleref nleref.Ccu (Array.append nleref.Path [| id |])
let mk_local_tcref x = ERef_local x
let mk_nonlocal_tcref nleref id = ERef_nonlocal (mk_nested_nleref nleref id)

//--------------------------------------------------------------------------
// From Ref_private to Ref_nonlocal when exporting data.
//--------------------------------------------------------------------------

let enclosing_nleref_of_pubpath viewedCcu (PubPath(p)) = NonLocalEntityRef(viewedCcu, p.[0..p.Length-2])
let nleref_of_pubpath viewedCcu (PubPath(p)) = NonLocalEntityRef(viewedCcu,p)

//---------------------------------------------------------------------------
// Equality between TAST items.
//---------------------------------------------------------------------------

let array_path_eq (y1:string[]) (y2:string[]) =
    let len1 = y1.Length 
    let len2 = y2.Length 
    (len1 = len2) && 
    (let rec loop i = (i >= len1) || (y1.[i] = y2.[i] && loop (i+1)) 
     loop 0)

let nleref_eq (NonLocalEntityRef(x1,y1) as smr1) (NonLocalEntityRef(x2,y2) as smr2) = 
    smr1 === smr2 || (ccu_eq x1 x2 && array_path_eq y1 y2)

/// This predicate tests if non-local resolution paths are definitely known to resolve
/// to different entities. All references with different named paths always resolve to 
/// different entities. Two references with the same named paths may resolve to the same 
/// entities even if they reference through different CCUs, because one reference
/// may be forwarded to another via a .NET TypeForwarder.
let nleref_definitely_not_eq (NonLocalEntityRef(_,y1)) (NonLocalEntityRef(_,y2)) = 
    not (array_path_eq y1 y2)


/// Primitive routine to compare two EntityRef's for equality
/// This takes into account the possibility that they may have type forwarders
let prim_entity_ref_eq (x : EntityRef) (y : EntityRef) = 
    x === y ||
    match x.IsResolved,y.IsResolved with 
    | true, true -> x.ResolvedTarget === y.ResolvedTarget 
    | _ -> 
    match x.IsLocalRef,y.IsLocalRef with 
    | false, false when 
        (// Two tcrefs with identical paths are always equal
         nleref_eq x.nlr y.nlr || 
         // The tcrefs may have forwarders. If they may possibly be equal then resolve them to get their canonical references
         // and compare those using pointer equality.
         (not (nleref_definitely_not_eq x.nlr y.nlr) && x.Deref === y.Deref)) -> 
        true
    | _ -> 
        false

let prim_entity_hash (x : EntityRef) =
    match x.TryDeref with
    |   None -> 0
    |   Some target -> hash (box target)

let ucref_eq (uc1 : UnionCaseRef) (uc2 : UnionCaseRef) =
    prim_entity_ref_eq uc1.TyconRef uc2.TyconRef && uc1.CaseName = uc2.CaseName
let ucref_hash (uc : UnionCaseRef) =
    (prim_entity_hash uc.TyconRef) * 29 + (hash uc.CaseName)

let taccessPublic = TAccess []

//---------------------------------------------------------------------------
// Construct TAST nodes
//---------------------------------------------------------------------------

let SkipFreeVarsCache() = ()
let NewFreeVarsCache() = newCache ()

let MakeUnionCasesTable ucs = 
    { ucases_by_index = Array.ofList ucs; 
      ucases_by_name = NameMap.ofKeyedList (fun uc -> uc.DisplayName) ucs }
                                                                  
let MakeRecdFieldsTable ucs = 
    { rfields_by_index = Array.ofList ucs; 
      rfields_by_name = ucs  |> NameMap.ofKeyedList (fun rfld -> rfld.Name) }
                                                                  
let MakeUnionCases ucs = 
    { funion_ucases=MakeUnionCasesTable ucs; }

let MakeUnionRepr ucs = TFiniteUnionRepr (MakeUnionCases ucs)

let tau_typ_eq t1 t2 =
    let rec tinst_eq tinst1 tinst2 =
        (List.length tinst1) = (List.length tinst2) &&
        List.forall2 tau_typ_eq tinst1 tinst2

    and tau_typ_eq t1 t2 =
        match t1, t2 with 
        |   TType_app(tcref1, tinst1), TType_app(tcref2, tinst2) ->            
                    prim_entity_ref_eq tcref1 tcref2 && tinst_eq tinst1 tinst2
        |   TType_tuple tinst1, TType_tuple tinst2 -> 
                    tinst_eq tinst1 tinst2 
        |   TType_fun(tdom1,trange1), TType_fun(tdom2,trange2) -> 
                    tau_typ_eq tdom1 tdom2 && tau_typ_eq trange1 trange2
        |   TType_ucase(ucref1,tinst1), TType_ucase(ucref2,tinst2) ->
                    ucref_eq ucref1 ucref2 && tinst_eq tinst1 tinst2
        |   TType_var typar1, TType_var typar2 -> 
                    typar_ref_eq typar1 typar2 || typar_ref_eq typar1 typar2 
        |   TType_measure m1, TType_measure m2 ->
                    measure_eq m1 m2
        |   (TType_forall(_) as x), _ 
        |   _, (TType_forall(_) as x) -> failwithf "Not a tau type %A" x
        |   _ -> false

    and measure_eq m1 m2 = 
        match m1, m2 with
        |   MeasureOne, MeasureOne -> true
        |   MeasureInv m1, MeasureInv m2 -> measure_eq m1 m2
        |   MeasureProd(m11,m12), MeasureProd(m21,m22) -> measure_eq m11 m21 && measure_eq m12 m22
        |   MeasureCon tcref1, MeasureCon tcref2 -> prim_entity_ref_eq tcref1 tcref2
        |   MeasureVar typar1, MeasureVar typar2 -> typar_ref_eq typar1 typar2
        |   _ -> false

    tau_typ_eq t1 t2

let tau_typ_hash t =
    let rec tau_typ_hash t =
        match t with
        |   TType_app(tcref, tinst) ->   (prim_entity_hash tcref) + 13 * (tinst_hash tinst)
        |   TType_tuple tinst ->        1 + tinst_hash tinst 
        |   TType_fun(tdom,trange) ->   2 + (tau_typ_hash trange) + 13 * (tau_typ_hash tdom)
        |   TType_ucase(uc,tinst) ->    3 + (ucref_hash uc) + 13 * (tinst_hash tinst)
        |   TType_var typar ->          5 + (typar_ref_hash typar)
        |   TType_measure m ->          7 + (measure_hash m)
        |   TType_forall _ ->           failwith "Non-tau types unsupported"
    and tinst_hash tinst = List.fold (fun h t -> h * 13 + tau_typ_hash t) 0 tinst
    and measure_hash m =
        match m with
        |   MeasureOne ->           1
        |   MeasureInv m ->         2 + (measure_hash m)
        |   MeasureProd(m1,m2) ->   3 + (measure_hash m1) + 13 * (measure_hash m2)
        |   MeasureCon tcref ->     5 + (prim_entity_hash tcref)
        |   MeasureVar typar ->     7 + (typar_ref_hash typar)
    tau_typ_hash t

         
//--------------------------------------------------------------------------
// Resource format for pickled data
//--------------------------------------------------------------------------

let FSharpOptimizationDataResourceName = "FSharpOptimizationData"
let FSharpSignatureDataResourceName = "FSharpSignatureData"



