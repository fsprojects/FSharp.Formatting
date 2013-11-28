// Copyright (c) Microsoft Corporation 2005-2009.
// This sample code is provided "as is" without warranty of any kind. 
// We disclaim all warranties, either express or implied, including the 
// warranties of merchantability and fitness for a particular purpose. 
//


namespace Microsoft.FSharp.Metadata

open System.IO
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Metadata.Reader.Internal
open Microsoft.FSharp.Metadata.Reader.Internal.AbstractIL.IL
open Microsoft.FSharp.Metadata.Reader.Internal.Tast
open Microsoft.FSharp.Metadata.Reader.Internal.Env
open Microsoft.FSharp.Metadata.Reader.Internal.Prelude
open Microsoft.FSharp.Metadata.Reader.Internal.Pickle

#nowarn "62"  // Using ^ for string concatenation 
#nowarn "44" // deprecated use of LoadWithPartialName
module Impl = 
    let isNull x = match x with null -> true  | _ -> false
    
    let readToEnd (s : Stream) = 
        let n = int s.Length 
        let res = Array.zeroCreate n 
        let mutable i = 0 
        while (i < n) do 
            i <- i + s.Read(res,i,n - i) 
        res 
        
    let makeReadOnlyCollection (arr:seq<'a>) = System.Collections.ObjectModel.ReadOnlyCollection<_>(Seq.toArray arr)
    
    let makeXmlDoc (XmlDoc x) = makeReadOnlyCollection(x)
    
    let isPublic a = (taccessPublic = a)


    let tryRescopeEntity viewedCcu (entity:Entity) : EntityRef option = 
        match entity.PublicPath with 
        | Some pubpath -> Some (ERef_nonlocal (nleref_of_pubpath viewedCcu pubpath))
        | None -> None

    let rescopeEntity viewedCcu (entity:Entity) = 
        match tryRescopeEntity viewedCcu entity with 
        | None -> mk_local_tcref entity
        | Some eref -> eref

open Impl


/// A limited implementation of type equality sufficient to resolve method signatures
/// Nothing in this module may dereference inaccessible entities, e.g. entities in .NET assemblies
module ApproxTypeEquiv = 

    //--------------------------------------------------------------------------
    // renamings
    //--------------------------------------------------------------------------

    type TyparInst = (Typar * typ) list

    let empty_tpinst = ([] : TyparInst)

    let rec inst_tpref tpinst ty tp  =
        match tpinst with 
        | [] -> ty
        | (tp',ty')::t -> 
            if typar_ref_eq tp tp' then ty' 
            else inst_tpref t ty tp

    let mk_typar_inst (typars: Typars) tyargs =  
        (List.zip typars tyargs : TyparInst)

    // This must not dereference inaccessible entities, e.g. entities in .NET assemblies
    let rec inst_type (tpinst : TyparInst) (ty:typ) =
        let ty = strip_tpeqns ty
        match ty with
        | TType_var tp as ty       -> inst_tpref tpinst ty tp
        | TType_app (tcr,tinst) -> TType_app (tcr,inst_types tpinst tinst)
        | TType_tuple l  -> TType_tuple (inst_types tpinst l)
        | TType_fun (d,r) -> TType_fun (inst_type tpinst d, inst_type tpinst r)
        | TType_forall (tps,ty) -> TType_forall (tps, inst_type tpinst ty)
        | TType_measure unt -> TType_measure (inst_measure tpinst unt)
        | _ -> ty

    and inst_measure tpinst unt =
        match unt with
        | MeasureOne -> unt
        | MeasureCon tcr -> unt
        | MeasureProd(u1,u2) -> MeasureProd(inst_measure tpinst u1, inst_measure tpinst u2)
        | MeasureInv u -> MeasureInv(inst_measure tpinst u)
        | MeasureVar tp -> 
              if ListAssoc.containsKey typar_ref_eq tp tpinst then 
                  match ListAssoc.find typar_ref_eq tp tpinst with 
                  | TType_measure unt -> unt
                  | _ -> failwith "inst_measure: incorrect kinds"
              else unt

    and inst_types tpinst types = List.map (inst_type tpinst) types

    let mk_tycon_inst (tycon: Tycon) tyargs = 
        List.zip tycon.TyparsNoRange tyargs

    let reduce_tycon_abbrev ty tycon tyargs = 
        inst_type (mk_tycon_inst tycon tyargs) ty

    let reduce_tycon_measureable (tycon:Tycon) tyargs = 
        let repr = tycon.TypeReprInfo
        match repr with 
        | Some (TMeasureableRepr ty) -> 
            if List.isEmpty tyargs then ty else inst_type (mk_tycon_inst tycon tyargs) ty
        | _ -> invalidArg "tc" "this type definition is not a refinement"

    [<NoEquality; NoComparison>]
    type TypeEquivEnv = 
        { ae_typars: Tastops.TyparMap<typ> }

    let tyeq_env_empty = { ae_typars=Tastops.tpmap_empty() }

    let bind_tyeq_env_types tps1 tys2 aenv =
        {aenv with ae_typars=List.foldBack2 Tastops.tpmap_add tps1 tys2 aenv.ae_typars}

    let bind_tyeq_env_typars tps1 tps2 aenv =
        bind_tyeq_env_types tps1 (List.map mk_typar_ty tps2) aenv

    let rec strip_tpeqns_and_tcabbrevs g ty = 
        let ty = strip_tpeqnsA ty 
        match ty with 
        | TType_app (tcref,tinst) when tcref.CanDeref -> 
            let tycon = tcref.Deref
            match tycon.TypeAbbrev with 
            | Some ty -> strip_tpeqns_and_tcabbrevs g (reduce_tycon_abbrev ty tycon tinst)
            | None -> ty
        | ty -> ty

    let rec strip_tpeqns_and_tcabbrevs_and_erase g ty =
        let ty = strip_tpeqns_and_tcabbrevs g ty
        match ty with
        | TType_app (tcref,args) when tcref.CanDeref -> 
            let tycon = tcref.Deref
            if tycon.IsMeasureableReprTycon  then
                strip_tpeqns_and_tcabbrevs_and_erase g (reduce_tycon_measureable tycon args)
            elif Tastops.tcref_eq g tcref g.nativeptr_tcr then 
                strip_tpeqns_and_tcabbrevs_and_erase g (Tastops.mk_nativeint_typ g)
            else
                ty
        | TType_fun(a,b) -> TType_app(g.fastFunc_tcr,[ a; b]) 
        | TType_tuple(l) -> Tastops.compiled_tuple_ty g l
        | ty -> ty

    let rec type_aequiv_aux  g aenv ty1 ty2 = 
        let ty1 = strip_tpeqns_and_tcabbrevs_and_erase g ty1 
        let ty2 = strip_tpeqns_and_tcabbrevs_and_erase g ty2
        match ty1, ty2 with
        | TType_forall(tps1,rty1), TType_forall(tps2,rty2) -> 
            tps1.Length = tps2.Length && type_aequiv_aux g (bind_tyeq_env_typars tps1 tps2 aenv) rty1 rty2
        | TType_var tp1, TType_var tp2 when typar_ref_eq tp1 tp2 -> 
            true
        | TType_var tp1, _ when Tastops.tpmap_mem tp1 aenv.ae_typars -> 
            type_equiv_aux g (Tastops.tpmap_find tp1 aenv.ae_typars) ty2
        | TType_app (tc1,b1)  ,TType_app (tc2,b2) -> 
            Tastops.tcref_eq g tc1 tc2 &&
            types_aequiv_aux g aenv b1 b2
        | TType_tuple l1,TType_tuple l2 -> 
            types_aequiv_aux g aenv l1 l2
        | TType_fun (dtys1,rty1),TType_fun (dtys2,rty2) -> 
            type_aequiv_aux g aenv dtys1 dtys2 && type_aequiv_aux g aenv rty1 rty2
        | TType_measure m1, TType_measure m2 -> true 
        | _ -> false

    and types_aequiv_aux g aenv l1 l2 = List.lengthsEqAndForall2 (type_aequiv_aux g aenv) l1 l2
    and type_equiv_aux g ty1 ty2 =  type_aequiv_aux g tyeq_env_empty ty1 ty2



type [<Sealed>] Env(typars:Typar list) = 
   let typars = Array.ofList typars
   member x.Typars = typars

type [<Sealed>] SourceLocation(range:range) = 
    member x.Document    = range.rangeFile
    member x.StartLine   = range.rangeBegin.posLine
    member x.StartColumn = range.rangeBegin.posCol
    member x.EndLine     = range.rangeEnd.posLine
    member x.EndColumn   = range.rangeEnd.posCol


type [<Sealed>] AssemblyLoader() = 
    static let table = Dictionary<string,FSharpAssembly>(100)
     
    static let fslib = AssemblyLoader.Add("FSharp.Core", typedefof<list<_>>.Assembly)
    
    static let _ = System.Reflection.Assembly.LoadWithPartialName "System"
    static let _ = System.Type.GetType("System.Uri, System")
     
    static let globals = 
        let p = [| "Microsoft"; "FSharp"; "Core" |]
        let nlr = NonLocalEntityRef(fslib.RawCcuThunk,p)
        { nativeptr_tcr = mk_nonlocal_tcref nlr "nativeptr`1" 
          nativeint_tcr= mk_nonlocal_tcref nlr "nativeint" 
          byref_tcr= mk_nonlocal_tcref nlr "byref`1" 
          il_arr1_tcr= mk_nonlocal_tcref nlr "[]`1" 
          il_arr2_tcr= mk_nonlocal_tcref nlr "[,]`1" 
          il_arr3_tcr= mk_nonlocal_tcref nlr "[,,]`1" 
          il_arr4_tcr= mk_nonlocal_tcref nlr "[,,,]`1" 
          tuple1_tcr= mk_nonlocal_tcref nlr "Tuple`1" 
          tuple2_tcr= mk_nonlocal_tcref nlr "Tuple`2" 
          tuple3_tcr= mk_nonlocal_tcref nlr "Tuple`3" 
          tuple4_tcr= mk_nonlocal_tcref nlr "Tuple`4" 
          tuple5_tcr= mk_nonlocal_tcref nlr "Tuple`5" 
          tuple6_tcr= mk_nonlocal_tcref nlr "Tuple`6" 
          tuple7_tcr= mk_nonlocal_tcref nlr "Tuple`7" 
          tuple8_tcr= mk_nonlocal_tcref nlr "Tuple`8" 
          fastFunc_tcr= mk_nonlocal_tcref nlr "FSharpFunc`2" 
          fslibCcu= fslib.RawCcuThunk
          unit_tcr= mk_nonlocal_tcref nlr "unit"  }


    static do  System.AppDomain.CurrentDomain.add_AssemblyResolve(new System.ResolveEventHandler(fun _ args -> 
        let shortName = AssemblyName(args.Name).Name
        lock table (fun () ->
            if table.ContainsKey shortName then 
                table.[shortName].ReflectionAssembly
            else
                null))
            )

    static member FSharpLibrary with get()  = fslib
    static member TcGlobals with get() = globals

    
    static member TryLoad(name:string) : FSharpAssembly option = 
        let existing = 
            lock table (fun () ->
                let found, result = table.TryGetValue(name)
                if found then Some result else None
            )
        match existing with
        |   Some _ -> existing
        |   None ->
                let assembly = Assembly.LoadWithPartialName name
                AssemblyLoader.TryAdd(name, assembly)
    
    static member Get(assembly : Assembly) : FSharpAssembly =
        let name = assembly.GetName().Name
        let existing = 
            lock table (fun () ->
                let found, result = table.TryGetValue(name)
                if found then Some result else None
            )
        match existing with
        |   Some fsAss -> fsAss
        |   None -> AssemblyLoader.Add(name, assembly)

    static member private Add (name, assembly:Assembly) : FSharpAssembly = 
        match AssemblyLoader.TryAdd (name, assembly) with 
        | None -> invalidArg "name" (sprintf "could not produce an FSharpAssembly object for the assembly '%s' because this is not an F# assembly" name)
        | Some res -> res

    static member private TryAdd (name, assembly:Assembly) : FSharpAssembly option = 
        let sref : ILScopeRef = ILScopeRef.Assembly(ILAssemblyRef.FromAssembly(assembly)) 
        let bytes = 
            match assembly.GetManifestResourceStream(FSharpSignatureDataResourceName ^ "." ^ name) with 
            | null -> 
                if name = "FSharp.Core" then 
                    match Internal.Utilities.FSharpEnvironment.BinFolderOfDefaultFSharpCoreReferenceAssembly with 
                    | Some path -> 
                        let fileName = Path.Combine(path,"FSharp.Core.sigdata")
                        if File.Exists(fileName) then
                            Some (File.ReadAllBytes fileName)
                        else
                            None
                    | None -> None
                else
                    None
            | resourceStream ->
                Some( resourceStream |> readToEnd)
        match bytes with 
        | None -> None
        | Some bytes -> 
            let data = unpickle_obj_with_dangling_ccus assembly.FullName sref unpickleModuleInfo bytes
            let info = data.RawData
            let ccuData = 
                { ccu_scoref=sref;
                  ccu_stamp = newStamp();
                  ccu_filename = None; 
                  ccu_qname= Some sref.QualifiedName;
                  ccu_code_dir = info.compileTimeWorkingDir; 
                  ccu_fsharp=true;
                  ccu_contents = info.mspec; 
                  ccu_usesQuotations = info.usesQuotations;
                  ccu_memberSignatureEquality= (fun ty1 ty2 -> ApproxTypeEquiv.type_equiv_aux globals ty1 ty2);
                  ccu_forwarders = PPLazy.Create (fun () -> Map.empty) }
                    
            let ccu = CcuThunk.Create(name, ccuData)
            let assem = FSharpAssembly(ccu, Some assembly)
            lock table (fun () -> table.[name] <- assem)
            let info = data.OptionalFixup(fun nm -> match AssemblyLoader.TryLoad(nm) with Some x -> Some(x.RawCcuThunk) | _ -> None )
            Some assem
        

and [<Sealed>] FSharpAssembly(ccu: CcuThunk, assemblyOpt: Assembly option) = 

    member x.RawCcuThunk = ccu

    static member FSharpLibrary with get() = AssemblyLoader.FSharpLibrary
    
    static member FromAssembly(assembly:Assembly) : FSharpAssembly = 
        AssemblyLoader.Get(assembly)
    
    static member FromFile fileName =
        let assembly = Assembly.LoadFrom fileName
        if isNull assembly then invalidOp ("error loading assembly " + fileName)
        FSharpAssembly.FromAssembly(assembly)
    
    member x.QualifiedName = ccu.QualifiedName.Value
      
    member x.CodeLocation = ccu.SourceCodeDirectory
      
    member x.ReflectionAssembly = 
        match assemblyOpt with 
        | None -> 
            let res = Assembly.Load x.QualifiedName
            if isNull res then invalidOp ("error loading assembly " + x.QualifiedName)
            res
        | Some x -> 
            x

    member x.GetEntity(name:string) = 
        let path = name.Split [| '.' |]
        if path.Length = 0 then invalidArg "name" "bad entity name"
        let path1 = path.[0..path.Length-2] 
        let nm = path.[path.Length-1]
        let tcref  = mk_nonlocal_tcref (NonLocalEntityRef(ccu,path1)) nm
        FSharpEntity(RealEntityRef tcref)
      
    member x.Entities = 
        let rec loop(entity:Entity) = 
            [| if entity.IsNamespace then 
                  for entity in entity.ModuleOrNamespaceType.AllEntities do 
                      yield! loop entity
               elif isPublic entity.Accessibility  then
                   yield FSharpEntity(RealEntityRef (rescopeEntity ccu entity)) 
               else
                   () |]
        [| for entity in ccu.TopModulesAndNamespaces do 
              yield! loop entity |] |> makeReadOnlyCollection
                 

and SimulatedEntityRef = 
   | RealEntityRef of EntityRef
   | FakeEntityRef of string

and [<Sealed>] FSharpEntity(entity:SimulatedEntityRef) = 

    static let computeIsExternal entity = 
        match entity with 
        | RealEntityRef (ERef_nonlocal (NonLocalEntityRef(ccu,_))) -> ccu.IsUnresolvedReference
        | _ -> false

    static let computeIsUnresolved entity = 
        match entity with 
        | RealEntityRef (ERef_nonlocal (NonLocalEntityRef(ccu,_)) as eref) -> ccu.IsUnresolvedReference && eref.TryDeref.IsNone 
        | _ -> false

    let isExternal() = computeIsExternal entity

    let isUnresolved() = computeIsUnresolved entity

    let poorAssembly() = 
        match entity with 
        | RealEntityRef entity -> System.Reflection.Assembly.LoadWithPartialName  entity.nlr.AssemblyName 
        | FakeEntityRef _ -> FSharpAssembly.FSharpLibrary.ReflectionAssembly
            
    let poorQualifiedName() = 
        match entity with 
        | RealEntityRef entity -> 
            if entity.nlr.AssemblyName = "mscorlib" then 
                entity.nlr.DisplayName  + ", mscorlib"
            else 
                let ass = poorAssembly()
                entity.nlr.DisplayName  + ", " + ass.FullName
        | FakeEntityRef nm -> nm + ", " + (poorAssembly().FullName)
            
    let poorNamespace() = 
        match entity with 
        | RealEntityRef entity -> 
            entity.nlr.EnclosingMangledPath |> String.concat "."
        | FakeEntityRef  _ -> ""

    let isFSharp() = 
        not (isExternal()) && not (isUnresolved())

    let checkIsFSharp() = 
        if isExternal() then invalidOp (sprintf "The entity '%s' is external to F#. This operation may only be performed on an entity from an F# assembly" (poorQualifiedName()))
        if isUnresolved() then invalidOp (sprintf "The entity '%s' does not exist or is in an unresolved assembly." (poorQualifiedName()))

    static member FromType (ty: System.Type) = 
        let assembly = ty.Assembly
        let fassembly = FSharpAssembly.FromAssembly assembly
        let gty = (if ty.IsGenericType then ty.GetGenericTypeDefinition() else ty)
        let path = (if ty.IsGenericType then ty.GetGenericTypeDefinition() else ty).FullName.Split [| '.' |]
        let path1 = if path.Length = 0 then [| |] else path.[0..path.Length-2] 
        let nm = if path.Length = 0 then gty.Name else path.[path.Length-1]
        let tcref  = mk_nonlocal_tcref (NonLocalEntityRef(fassembly.RawCcuThunk,path1)) nm
        FSharpEntity(RealEntityRef tcref)

    member private this.SimulatedEntity = entity

    member x.ReflectionType  = 
        let fail() = invalidOp (sprintf "the type %s is an abbreviation and does not have a System.Type" x.LogicalName)
        match entity with 
        | RealEntityRef entity -> 
            // Don't call this: checkIsFSharp()   -- this one is valid on non-F# types
            if isFSharp() && entity.IsTypeAbbrev then fail()
            match System.Type.GetType x.QualifiedName  with 
            | null -> invalidOp (sprintf "couldn't load type '%s'" x.QualifiedName)
            | ty -> ty
        | FakeEntityRef _ -> fail()
        
    member x.ReflectionAssembly = 
        match entity with 
        | RealEntityRef entity -> 
            if isExternal() || isUnresolved() || (isFSharp() && (entity.IsTypeAbbrev || entity.IsMeasureableReprTycon || entity.IsAsmReprTycon)) then 
                poorAssembly()     
            else            
                match entity.CompiledRepresentation with 
                | TyrepNamed(tref,_,_) -> System.Type.GetType(x.QualifiedName).Assembly
                | TyrepOpen _ -> typeof<int>.Assembly (* mscorlib *)
        | FakeEntityRef _ -> FSharpAssembly.FSharpLibrary.ReflectionAssembly
        

    member x.LogicalName = 
        match entity with 
        | RealEntityRef entity -> 
            if isFSharp() then entity.LogicalName else x.ReflectionType.Name
        | FakeEntityRef  nm -> nm
        

    member x.CompiledName = 
        match entity with 
        | RealEntityRef entity -> 
            if isFSharp() then entity.CompiledName else x.ReflectionType.Name
        | FakeEntityRef nm -> nm

    member x.DisplayName = 
        match entity with 
        | RealEntityRef entity -> 
            if isFSharp() then 
                if entity.IsModuleOrNamespace then entity.DemangledModuleOrNamespaceName
                else entity.DisplayName 
            else 
                PrettyNaming.demangleGenericTypeName x.ReflectionType.Name
        | FakeEntityRef nm -> nm

    member x.Namespace = 
        if isFSharp() then poorNamespace() else x.ReflectionType.Namespace

    member x.DeclaringEntity = 
        let fail() = invalidOp (sprintf "the type or module '%s' does not have a declaring entity" x.LogicalName)
        match entity with 
        | RealEntityRef entity -> 
            match entity.PublicPath with 
            | None -> fail()
            | Some p -> 
                let res = RealEntityRef(ERef_nonlocal(enclosing_nleref_of_pubpath entity.nlr.Ccu p))
                if computeIsUnresolved res then  fail()
                FSharpEntity res
        | FakeEntityRef _ -> fail()
    
    
    member x.QualifiedName = 
        let fail() = invalidOp (sprintf "the type '%s' does not have a qualified name" x.LogicalName)
        match entity with 
        | RealEntityRef entity -> 
            if isExternal() || isUnresolved() then 
                poorQualifiedName()     
            else            
                if entity.IsTypeAbbrev then fail()
                match entity.CompiledRepresentation with 
                | TyrepNamed(tref,_,_) -> tref.QualifiedName
                | TyrepOpen _ -> fail()
        | _ -> fail()
        

    member x.DeclarationLocation = 
        let fail() = invalidOp (sprintf "the type '%s' does not have a declaration location" x.LogicalName)
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            SourceLocation(entity.Range)
        | FakeEntityRef _  -> fail()

    member this.GenericParameters= 
        checkIsFSharp(); 
        match entity with 
        | RealEntityRef entity -> 
            let env = Env(entity.TyparsNoRange)
            entity.TyparsNoRange |> List.map (fun tp -> FSharpGenericParameter(env,tp)) |> List.toArray |> makeReadOnlyCollection
        | FakeEntityRef _  -> [ ] |> makeReadOnlyCollection

    member x.IsMeasure = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && (entity.TypeOrMeasureKind = KindMeasure)
        | FakeEntityRef _ -> false
    member x.IsModule = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && entity.IsModule
        | FakeEntityRef _ -> false
    member x.HasFSharpModuleSuffix = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && entity.IsModule && (entity.ModuleOrNamespaceType.ModuleOrNamespaceKind = ModuleOrNamespaceKind.FSharpModuleWithSuffix)
        | FakeEntityRef _ -> false
    member x.IsValueType  = 
        match entity with 
        | RealEntityRef entity -> if isFSharp() then entity.IsStructOrEnumTycon else x.ReflectionType.IsValueType
        | FakeEntityRef _ -> false

#if TODO
    member x.IsClass = (not entity.IsNamespace && not entity.IsModule && entity.TypeOrMeasureKind = TyparKind.KindType)
    member x.IsInterface = (not entity.IsNamespace && not entity.IsModule && entity.TypeOrMeasureKind = TyparKind.KindType)
    member x.IsDelegate : bool
    member x.IsAbstract : bool;                       
    member x.IsEnum : bool
#endif
    
    member x.IsExceptionDeclaration = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && entity.IsExceptionDecl
        | FakeEntityRef  _-> false

    member x.IsExternal = 
        isExternal()

    member x.IsAbbreviation = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && entity.IsTypeAbbrev 
        | FakeEntityRef _ -> false

    member x.IsRecord = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && entity.IsRecordTycon
        | FakeEntityRef _ -> false

    member x.IsUnion = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && entity.IsUnionTycon
        | FakeEntityRef _ -> false

    member x.HasAssemblyCodeRepresentation = 
        match entity with 
        | RealEntityRef entity -> isFSharp() && (entity.IsAsmReprTycon || entity.IsMeasureableReprTycon)
        | FakeEntityRef _ -> false

    static member op_Equality (left:FSharpEntity,right:FSharpEntity) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpEntity,right:FSharpEntity) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))


#if TODO
    member x.GetAssemblyCodeRepresentation : unit -> string 
    // member TyconDelegateSlotSig : SlotSig option
      

#endif

    member x.Accessibility = FSharpAccessibility() 
    member x.RepresentationAccessibility = FSharpAccessibility()

      /// Interface implementations - boolean indicates compiler-generated 
    member this.Implements = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            let env = Env(entity.TyparsNoRange)
            entity.InterfaceTypesOfFSharpTycon |> List.map (fun ty -> FSharpType(env,ty)) |> makeReadOnlyCollection
        | FakeEntityRef _ -> 
            makeReadOnlyCollection []

      /// Super type, if any 
    member this.BaseType = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            let env = Env(entity.TyparsNoRange)
            match entity.TypeContents.tcaug_super with 
            | None -> invalidOp "this entity has no base type"
            | Some ty -> FSharpType(env,ty)
        | FakeEntityRef _-> invalidOp "this entity has no base type"
        
      /// Indicates the type prefers the "tycon<a,b>" syntax for display etc. 
    member x.UsesPrefixDisplay = 
        match entity with 
        | RealEntityRef entity -> not (isFSharp()) || entity.Deref.IsPrefixDisplay
        | FakeEntityRef _ -> true


      /// Properties, methods etc. with implementations
    member this.MembersOrValues = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            ((entity.MembersOfFSharpTyconSorted
              |> List.filter (fun v -> not v.IsOverrideOrExplicitImpl && 
                                       not v.Deref.IsClassConstructor &&
                                       isPublic v.Accessibility)
              |> List.map (fun v -> FSharpMemberOrVal(this, v.Deref)))
            @
             (entity.ModuleOrNamespaceType.AllValsAndMembers
              |> Seq.toList
              |> List.filter (fun v -> v.IsExtensionMember || not v.IsMember) 
              |> List.filter (fun v -> isPublic v.Accessibility) 
              |> List.map (fun v -> FSharpMemberOrVal(this,v))))
               
              |> makeReadOnlyCollection
        | FakeEntityRef _ -> 
            makeReadOnlyCollection []

    member x.XmlDocSig = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            entity.XmlDocSig 
        | FakeEntityRef _ -> 
            invalidOp "This entity does not have an XmlDocSig"

    member x.NestedEntities = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            entity.ModuleOrNamespaceType.AllEntities 
            |> QueueList.toList
            |> List.filter (fun x -> isPublic x.Accessibility) 
            |> List.map (fun x -> FSharpEntity(RealEntityRef (rescopeEntity entity.nlr.Ccu x)))
            |> makeReadOnlyCollection
        | FakeEntityRef _ -> 
            makeReadOnlyCollection []

    member this.UnionCases = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            let env = Env(entity.TyparsNoRange)
            entity.UnionCasesAsList 
            |> List.filter (fun x -> isPublic x.Accessibility) 
            |> List.map (fun x -> FSharpUnionCase(env,x)) 
            |> makeReadOnlyCollection
        | FakeEntityRef _ -> 
            makeReadOnlyCollection []

    member this.RecordFields =
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            let env = Env(entity.TyparsNoRange)
            entity.AllFieldsAsList 
            |> List.filter (fun x -> isPublic x.Accessibility) 
            |> List.map (fun x -> FSharpRecordField(env,x)) 
            |> makeReadOnlyCollection
        | FakeEntityRef _ -> 
            makeReadOnlyCollection []

    member this.AbbreviatedType   = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            let env = Env(entity.TyparsNoRange)
            match entity.TypeAbbrev with 
            | None -> invalidOp "not a type abbreviation"
            | Some ty -> FSharpType(env,ty)
        | FakeEntityRef _ -> 
            invalidOp "not a type abbreviation"

    member x.Attributes = 
        match entity with 
        | RealEntityRef entity -> 
            checkIsFSharp(); 
            entity.Attribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection
        | FakeEntityRef _ -> 
            makeReadOnlyCollection []

    override this.Equals(other : obj) =
        if this :> obj === other then true
        else
            match other with
            |   :? FSharpEntity as otherEntity ->
                    match entity, otherEntity.SimulatedEntity with
                    |   FakeEntityRef s1, FakeEntityRef s2 -> s1 = s2
                    |   RealEntityRef eref1, RealEntityRef eref2 -> Tast.prim_entity_ref_eq eref1 eref2
                    |   _ -> false
            |   _ -> false

    override this.GetHashCode() =
        match entity with 
        |   FakeEntityRef s -> (hash s) <<< 1
        |   RealEntityRef er -> ((Tast.prim_entity_hash er) <<< 1) + 1


and [<Sealed>] FSharpUnionCase(env:Env,v: UnionCase) =

    member x.Name = v.DisplayName
    member x.DeclarationLocation = SourceLocation(v.Range)
    member this.Fields = v.RecdFields |> List.map (fun r -> FSharpRecordField(env,r)) |> List.toArray |> makeReadOnlyCollection
    member x.ReturnType = FSharpType(env,v.ucase_rty)
    member x.CompiledName = v.ucase_il_name
    member x.XmlDocSig = v.XmlDocSig
    member x.Attributes = v.Attribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection
    member x.Accessibility =  FSharpAccessibility();

    member private this.V = v
    override this.Equals(o : obj) =
        if this :> obj === o then true
        else
            match o with
            |   :? FSharpUnionCase as uc -> v === uc.V
            |   _ -> false
    
    override this.GetHashCode() = (hash (box v))

    static member op_Equality (left:FSharpUnionCase,right:FSharpUnionCase) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpUnionCase,right:FSharpUnionCase) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))


and 
    [<RequireQualifiedAccess>]
    RecordFieldContainer = Entity of FSharpEntity | UnionCase of FSharpUnionCase

and [<Sealed>] FSharpRecordField(env:Env,v: RecdField) =
    member x.IsMutable = v.IsMutable
    member x.XmlDocSig = v.XmlDocSig
    member x.Type = 
        FSharpType(env,v.FormalType)
    member x.IsStatic = v.IsStatic
    member x.Name = v.Name
    member x.IsCompilerGenerated = v.IsCompilerGenerated
    member x.DeclarationLocation = SourceLocation(v.Range)
    member x.FieldAttributes = v.FieldAttribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection
    member x.PropertyAttributes = v.PropertyAttribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection
    //member x.LiteralValue = v.Is
    member x.Accessibility =  FSharpAccessibility() 
    member private this.V = v
    override this.Equals(o : obj) =
        if this :> obj === o then true
        else
            match o with
            |   :? FSharpRecordField as uc -> v === uc.V
            |   _ -> false

    override this.GetHashCode() = (hash (box v))
    static member op_Equality (left:FSharpRecordField,right:FSharpRecordField) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpRecordField,right:FSharpRecordField) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))


and [<Sealed>] FSharpAccessibility() = 
    //member x.IsPublic : bool
    //member x.IsPrivate : bool
    //member x.IsInternal : bool
    class
    end

and [<RequireQualifiedAccess>]
    GenericParameterContainer = Entity of FSharpEntity | MemberOrVal of FSharpMemberOrVal

and [<Sealed>] FSharpGenericParameter(env:Env,v:Typar) = 

    member x.Name = v.DisplayName
    member x.DeclarationLocation = SourceLocation(v.Range)
       
    member x.IsMeasure = (v.Kind = TyparKind.KindMeasure)

    member x.XmlDoc = v.Data.typar_xmldoc |> makeXmlDoc

    member x.IsSolveAtCompileTime = (v.StaticReq = TyparStaticReq.HeadTypeStaticReq)
       
    member x.Attributes = v.Attribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection

    member x.Constraints = v.Constraints |> List.map (fun a -> FSharpGenericParameterConstraint(env,a)) |> makeReadOnlyCollection
    
    member private this.V = v

    override this.Equals(o : obj) =
        if this :> obj === o then true
        else
            match o with
            |   :? FSharpGenericParameter as p -> typar_ref_eq v p.V
            |   _ -> false
    override this.GetHashCode() = (typar_ref_hash v)

    static member op_Equality (left:FSharpGenericParameter,right:FSharpGenericParameter) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpGenericParameter,right:FSharpGenericParameter) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))


and [<Sealed>] FSharpGenericParameterConstraint(env : Env, cx : TyparConstraint) = 

    member x.IsMemberConstraint = 
        match cx with 
        | TTyparMayResolveMemberConstraint _ -> true 
        | _ -> false

    /// Indicates a constraint that a type is a subtype of the given type 
    member x.IsCoercesToConstraint = 
        match cx with 
        | TTyparCoercesToType _ -> true 
        | _ -> false

    member x.CoercesToTarget = 
        match cx with 
        | TTyparCoercesToType(ty,_) -> FSharpType(env,ty) 
        | _ -> invalidOp "not a coerces-to constraint"

    /// Indicates a default value for an inference type variable should it be netiher generalized nor solved 
    member x.IsDefaultsToConstraint = 
        match cx with 
        | TTyparDefaultsToType _ -> true 
        | _ -> false

    member x.DefaultsToPriority = 
        match cx with 
        | TTyparDefaultsToType(pri,_,_) -> pri 
        | _ -> invalidOp "incorrect constraint kind"

    member x.DefaultsToTarget = 
        match cx with 
        | TTyparDefaultsToType(_,ty,_) -> FSharpType(env,ty) 
        | _ -> invalidOp "incorrect constraint kind"

    /// Indicates a constraint that a type has a 'null' value 
    member x.IsSupportsNullConstraint  = match cx with TTyparSupportsNull _ -> true | _ -> false

    member x.MemberSources = 
        match cx with 
        | TTyparMayResolveMemberConstraint(TTrait(tys,_,_,_,_,_),_) -> tys   |> List.map (fun ty -> FSharpType(env,ty)) |> makeReadOnlyCollection
        | _ -> invalidOp "incorrect constraint kind"

    member x.MemberName = 
        match cx with 
        | TTyparMayResolveMemberConstraint(TTrait(_,nm,_,_,_,_),_) -> nm  
        | _ -> invalidOp "incorrect constraint kind"

    member x.MemberIsStatc = 
        match cx with 
        | TTyparMayResolveMemberConstraint(TTrait(_,_,flags,_,_,_),_) -> not flags.MemberIsInstance  
        | _ -> invalidOp "incorrect constraint kind"

    member x.MemberArgumentTypes = 
        match cx with 
        | TTyparMayResolveMemberConstraint(TTrait(_,_,_,tys,_,_),_) -> tys   |> List.map (fun ty -> FSharpType(env,ty)) |> makeReadOnlyCollection
        | _ -> invalidOp "incorrect constraint kind"

    member this.MemberReturnType = 
        match cx with 
        | TTyparMayResolveMemberConstraint(TTrait(tys,_,_,_,rty,_),_) -> 
            match rty with 
            | None -> FSharpType(env,TType_app(AssemblyLoader.TcGlobals.unit_tcr,[])) 
            | Some ty -> FSharpType(env,ty) 
        | _ -> invalidOp "incorrect constraint kind"

    /// Indicates a constraint that a type is a non-Nullable value type 
    member x.IsNonNullableValueTypeConstraint = 
        match cx with 
        | TTyparIsNotNullableValueType _ -> true 
        | _ -> false
    
    /// Indicates a constraint that a type is a reference type 
    member x.IsReferenceTypeConstraint  = 
        match cx with 
        | TTyparIsReferenceType _ -> true 
        | _ -> false

    /// Indicates a constraint that a type is a simple choice between one of the given ground types. Used by printf format strings.
    member x.IsSimpleChoiceConstraint = 
        match cx with 
        | TTyparSimpleChoice _ -> true 
        | _ -> false

    member x.SimpleChoices = 
        match cx with 
        | TTyparSimpleChoice (tys,_) -> 
            tys   |> List.map (fun ty -> FSharpType(env,ty)) |> makeReadOnlyCollection
        | _ -> invalidOp "incorrect constraint kind"

    /// Indicates a constraint that a type has a parameterless constructor 
    member x.IsRequiresDefaultConstructorConstraint  = match cx with TTyparRequiresDefaultConstructor _ -> true | _ -> false

    /// Indicates a constraint that a type is an enum with the given underlying 
    member x.IsEnumConstraint = 
        match cx with 
        | TTyparIsEnum _ -> true 
        | _ -> false

    member x.EnumConstraintTarget = 
        match cx with 
        | TTyparIsEnum(ty,_) -> 
            FSharpType(env,ty)
        | _ -> invalidOp "incorrect constraint kind"
    
    member x.IsComparisonConstraint = 
        match cx with 
        | TTyparSupportsComparison _ -> true 
        | _ -> false

    member x.IsEqualityConstraint = 
        match cx with 
        | TTyparSupportsEquality _ -> true 
        | _ -> false

    member x.IsUnmanagedConstraint = 
        match cx with 
        | TyparConstraint.TTyparIsUnmanaged _ -> true 
        | _ -> false

    /// Indicates a constraint that a type is a delegate from the given tuple of args to the given return type 
    member x.IsDelegateConstraint = 
        match cx with 
        | TTyparIsDelegate _ -> true 
        | _ -> false

    member x.DelegateTupledArgumentType = 
        match cx with 
        | TTyparIsDelegate (tupledArgTyp,_,_) -> 
            FSharpType(env,tupledArgTyp)
        | _ -> invalidOp "incorrect constraint kind"
 
    member x.DelegateReturnType = 
        match cx with 
        | TTyparIsDelegate (_,rty,_) -> 
            FSharpType(env,rty)
        | _ -> invalidOp "incorrect constraint kind"

and FSharpInlineAnnotation = 
   | PsuedoValue = 3
   | AlwaysInline = 2
   | OptionalInline = 1
   | NeverInline = 0

and [<Sealed>] FSharpMemberOrVal(e:FSharpEntity,v:Val) = 

    let g = AssemblyLoader.TcGlobals
    let is_unit_typ ty = 
        let ty = ApproxTypeEquiv.strip_tpeqns_and_tcabbrevs g ty 
        match ty with 
        | TType_app (tcr,_) -> prim_entity_ref_eq g.unit_tcr tcr 
        | _ -> false

    let is_fun_typ ty = 
        let ty = ApproxTypeEquiv.strip_tpeqns_and_tcabbrevs g ty 
        match ty with TType_fun _ -> true | _ -> false

    let dest_fun_typ ty = 
        let ty = ApproxTypeEquiv.strip_tpeqns_and_tcabbrevs g ty 
        match ty with TType_fun (d,r) -> (d,r) | _ -> failwith "dest_fun_typ"

    let dest_tuple_typ ty = 
        let ty = ApproxTypeEquiv.strip_tpeqns_and_tcabbrevs g ty 
        if is_unit_typ ty then [] else match ty with TType_tuple tys -> tys | _ -> [ty]

    let rec strip_fun_typ_upto n ty = 
        assert (n >= 0);
        if n > 0 && is_fun_typ ty then 
            let (d,r) = dest_fun_typ ty
            let more,rty = strip_fun_typ_upto (n-1) r in d::more, rty
        else [],ty

    (* A 'tau' type is one with its type paramaeters stripped off *)
    let GetTopTauTypeInFSharpForm (curriedArgInfos: TopArgInfo list list) tau m =

        let argtys,rty = strip_fun_typ_upto curriedArgInfos.Length tau

        if curriedArgInfos.Length <> argtys.Length then 
            error(Error((0,"Invalid member signature encountered because of an earlier error"),m))

        let argtysl = 
            (curriedArgInfos,argtys) ||> List.map2 (fun argInfos argty -> 
                match argInfos with 
                | [] -> [] //else [ (mk_unit_typ g, TopValInfo.unnamedTopArg1) ]
                | [argInfo] -> [ (argty, argInfo) ]
                | _ -> List.zip (dest_tuple_typ argty) argInfos) 
        argtysl,rty

    member x.DeclarationLocation = SourceLocation(v.Range)

    member x.LogicalEnclosingEntity = 
        match v.ApparentParent with 
        | ParentNone -> invalidOp "the value or member doesn't have a logical parent" 
        | Parent p -> FSharpEntity(RealEntityRef p)

    member this.GenericParameters = 
        let env = Env(v.Typars)
        v.Typars |> List.map (fun tp -> FSharpGenericParameter(env,tp)) |> List.toArray |> makeReadOnlyCollection

    member this.Type = 
        FSharpType(Env(v.Typars),v.TauType)

    member x.EnclosingEntity = e

    member x.IsCompilerGenerated = v.IsCompilerGenerated

    member x.InlineAnnotation = 
        match v.InlineInfo with 
        | ValInlineInfo.PseudoValue -> FSharpInlineAnnotation.PsuedoValue
        | ValInlineInfo.AlwaysInline -> FSharpInlineAnnotation.AlwaysInline
        | ValInlineInfo.OptionalInline -> FSharpInlineAnnotation.OptionalInline
        | ValInlineInfo.NeverInline -> FSharpInlineAnnotation.NeverInline

    member x.IsMutable = v.IsMutable

    member x.IsModuleValueOrMember = v.IsMember || v.IsModuleBinding
    member x.IsMember = v.IsMember 
    
    member x.IsDispatchSlot = v.IsDispatchSlot

    member x.IsGetterMethod = match v.MemberInfo with None -> false | Some memInfo -> memInfo.MemberFlags.MemberKind = MemberKindPropertyGet
    member x.IsSetterMethod = match v.MemberInfo with None -> false | Some memInfo -> memInfo.MemberFlags.MemberKind = MemberKindPropertySet

    member x.IsInstanceMember = v.IsInstanceMember

    member x.IsExtensionMember = v.IsExtensionMember

    member x.IsImplicitConstructor = v.IsIncrClassConstructor
    
    member x.IsTypeFunction = v.IsTypeFunction

    member x.IsActivePattern =  
        v.CoreDisplayName |> PrettyNaming.activePatternInfoOfValName |> isSome

    member x.CompiledName = v.CompiledName

    member x.LogicalName = v.LogicalName

    member x.DisplayName = v.DisplayName

    member x.XmlDocSig = v.XmlDocSig


    member this.CurriedParameterGroups = 
        let env = Env(v.Typars)
        match v.TopValInfo with 
        | None -> failwith "not a module let binding or member"
        | Some (TopValInfo(typars,curriedArgInfos,retInfo)) -> 
            let tau = v.TauType
            let argtysl,_ = GetTopTauTypeInFSharpForm curriedArgInfos tau range0
            let argtysl = if v.IsInstanceMember then argtysl.Tail else argtysl
            
            [ for argtys in argtysl do 
                 yield 
                   [ for argty, argInfo in argtys do 
                        yield FSharpParameter(env,argty,argInfo) ] 
                   |> makeReadOnlyCollection ]
             |> makeReadOnlyCollection

    member x.ReflectionMemberInfo = 
        let ty = e.ReflectionType
        let possibles = ty.GetMember(v.CompiledName)  
        if possibles.Length = 0 then invalidOp (sprintf "member %s not found" v.CompiledName)
        else possibles.[0]
        // invalidOp "member ambiguous"
        
    member this.ReturnParameter  = 

        let env = Env(v.Typars)
        match v.TopValInfo with 
        | None -> failwith "not a module let binding or member"; 
        | Some (TopValInfo(typars,argInfos,retInfo)) -> 
        
            let tau = v.TauType
            let _,rty = GetTopTauTypeInFSharpForm argInfos tau range0
            
            FSharpParameter(env,rty,retInfo) 


    member x.Attributes = v.Attribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection
     
(*
    /// Is this "base" in "base.M(...)"
    member x.IsBaseValue : bool

    /// Is this the "x" in "type C() as x = ..."
    member x.IsConstructorThisValue : bool

    /// Is this the "x" in "member x.M = ..."
    member x.IsMemberThisValue : bool

    /// Is this a [<Literal>] value, and if so what value?
    member x.LiteralValue : obj // may be null


      /// Get the module, type or namespace where this value appears. For 
      /// an extension member this is the type being extended 
    member x.ApparentParent: FSharpEntity

     /// Get the module, type or namespace where this value is compiled
    member x.ActualParent: FSharpEntity;

*)

      /// How visible is this? 
    member x.Accessibility = FSharpAccessibility()

    member private this.V = v
    override this.Equals(o : obj) =
        if this :> obj === o then true
        else
            match o with
            |   :? FSharpMemberOrVal as other -> v === this.V
            |   _ -> false
    override this.GetHashCode() = (hash (box v))
    static member op_Equality (left:FSharpMemberOrVal,right:FSharpMemberOrVal) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpMemberOrVal,right:FSharpMemberOrVal) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))


and [<Sealed>] FSharpType(env:Env, typ:typ) =

    member x.IsNamed = (match typ with TType_app _ | TType_measure (MeasureCon _ | MeasureProd _ | MeasureInv _ | MeasureOne _) -> true | _ -> false)
    member x.IsTuple = (match typ with TType_tuple _ -> true | _ -> false)

    member x.NamedEntity = 
        match typ with 
        | TType_app (tcref,_) -> FSharpEntity(RealEntityRef tcref) 
        | TType_measure (MeasureCon tcref) ->  FSharpEntity(RealEntityRef tcref) 
        | TType_measure (MeasureProd (t1,t2)) ->  FSharpEntity(FakeEntityRef "*") 
        | TType_measure MeasureOne ->  FSharpEntity(FakeEntityRef "1") 
        | TType_measure (MeasureInv t1) ->  FSharpEntity(FakeEntityRef "/") 
        | _ -> invalidOp "not a named type"

    member x.GenericArguments = 
        match typ with 
        | TType_app (_,tyargs) 
        | TType_tuple (tyargs) -> (tyargs |> List.map (fun ty -> FSharpType(env,ty)) |> makeReadOnlyCollection) 
        | TType_fun(d,r) -> [| FSharpType(env,d); FSharpType(env,r) |] |> makeReadOnlyCollection
        | TType_measure (MeasureCon tcref) ->  [| |] |> makeReadOnlyCollection
        | TType_measure (MeasureProd (t1,t2)) ->  [| FSharpType(env,TType_measure t1); FSharpType(env,TType_measure t2) |] |> makeReadOnlyCollection
        | TType_measure MeasureOne ->  [| |] |> makeReadOnlyCollection
        | TType_measure (MeasureInv t1) ->  [| FSharpType(env,TType_measure t1); |] |> makeReadOnlyCollection
        | _ -> invalidOp "not a named type"


    member x.IsFunction = (match typ with TType_fun _ -> true | _ -> false)

    member x.IsGenericParameter= 
        match typ with 
        | TType_var _ -> true 
        | TType_measure (MeasureVar _) -> true 
        | _ -> false

    member x.GenericParameter = 
        match typ with 
        | TType_var tp 
        | TType_measure (MeasureVar tp) -> 
            FSharpGenericParameter (env, env.Typars |> Array.find (fun tp2 -> typar_ref_eq tp tp2)) 
        | _ -> invalidOp "not a generic parameter type"

    member x.GenericParameterIndex = 
        match typ with 
        | TType_var tp 
        | TType_measure (MeasureVar tp) -> 
            env.Typars |> Array.findIndex (fun tp2 -> typar_ref_eq tp tp2)
        | _ -> invalidOp "not a generic parameter type"

    member private x.Typ = typ

    override this.Equals(other : obj) =
        if this :> obj === other then true
        else
            match other with
            |   :? FSharpType as t -> tau_typ_eq typ t.Typ
            |   _ -> false
    override this.GetHashCode() = tau_typ_hash typ
    static member op_Equality (left:FSharpType,right:FSharpType) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpType,right:FSharpType) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))


and [<Sealed>] FSharpAttribute(attrib) = 

#if FSHARP_1_9_7
    let (Attrib(tcref,kind,unnamedArgs,propVals,m)) = attrib
#else
    let (Attrib(tcref,kind,unnamedArgs,propVals,_,m)) = attrib
#endif

    member x.ReflectionType : System.Type = 
        match kind with 
        | ILAttrib(mspec) -> 
            System.Type.GetType(mspec.EnclosingTypeRef.QualifiedName)
        | FSAttrib(vref) -> 
            System.Type.GetType(tcref.CompiledRepresentationForTyrepNamed.QualifiedName)

    member x.Value = 
        let ty = x.ReflectionType
        let fail() = failwith "This custom attribute has an argument that can not yet be converted using this API"
        let evalArg e = 
            match e with
            | TExpr_const(c,m,_) -> 
                match c with 
                | TConst_bool b -> box b
                | TConst_sbyte  i  -> box i
                | TConst_int16  i  -> box  i
                | TConst_int32 i   -> box i
                | TConst_int64 i   -> box i  
                | TConst_byte i    -> box i
                | TConst_uint16 i  -> box i
                | TConst_uint32 i  -> box i
                | TConst_uint64 i  -> box i
                | TConst_float i   -> box i
                | TConst_float32 i -> box i
                | TConst_char i    -> box i
                | TConst_zero -> null
                | TConst_string s ->  box s
                | _ -> fail()
            | _ -> fail()
        let args = unnamedArgs |> List.map (fun (AttribExpr(_,e)) -> evalArg e) |> List.toArray
        let res = System.Activator.CreateInstance(ty,args)
        propVals |> List.iter (fun (AttribNamedArg(nm,_,isField,AttribExpr(_, e))) -> 
            ty.InvokeMember(nm,BindingFlags.Public ||| BindingFlags.NonPublic ||| (if isField then BindingFlags.SetField else BindingFlags.SetProperty),
                            null,res,[| evalArg e |]) |> ignore)
        res

    member private this.Attrib = attrib
    override this.Equals(o : obj) =
        if box this === o then true
        else 
            match o with
            |   :? FSharpAttribute as other -> attrib === other.Attrib
            |   _ -> false

    override this.GetHashCode() = hash (box attrib)
    static member op_Equality (left:FSharpAttribute,right:FSharpAttribute) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpAttribute,right:FSharpAttribute) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))

    
and [<Sealed>] FSharpParameter(env:Env,typ:typ,topArgInfo:TopArgInfo) = 
#if FSHARP_1_9_7
    let (TopArgInfo(attribs,idOpt)) = topArgInfo
#else
    let attribs = topArgInfo.Attribs
    let idOpt = topArgInfo.Name
#endif
    member x.Name = match idOpt with None -> null | Some v -> v.idText
    member x.Type = FSharpType(env,typ)
    member x.DeclarationLocation = SourceLocation(match idOpt with None -> range0 | Some v -> v.idRange)
    member x.Attributes = attribs |> List.map (fun a -> FSharpAttribute(a)) |> makeReadOnlyCollection
    
    member private x.TopArgInfo = topArgInfo

    override this.Equals(o : obj) =
        if box this === o then true
        else
            match o with
            |   :? FSharpParameter as p -> topArgInfo === p.TopArgInfo
            |   _ -> false
    override this.GetHashCode() = hash (box topArgInfo)

    static member op_Equality (left:FSharpParameter,right:FSharpParameter) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> left.Equals(right)
    static member op_Inequality (left:FSharpParameter,right:FSharpParameter) =
        match box left, box right with
        |   null, null -> true
        |   null, _ | _, null -> false
        |   _ -> not (left.Equals(right))
