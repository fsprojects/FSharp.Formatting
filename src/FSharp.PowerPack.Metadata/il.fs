// (c) Microsoft Corporation. Apache 2.0 License

module internal Microsoft.FSharp.Metadata.Reader.Internal.AbstractIL.IL
open System.Collections.Generic
open System.Collections
#nowarn "62"  // Using ^ for string concatenation 
#nowarn "343" // The type 'ILAssemblyRef' implements 'System.IComparable' explicitly but provides no corresponding override for 'Object.Equals'.
#nowarn "346" // The struct, record or union type 'IlxExtensionType' has an explicit implementation of 'Object.Equals'. ...

type Lazy<'T> = Microsoft.FSharp.Metadata.Reader.Internal.Prelude.PPLazy<'T>

let notlazy v = Lazy.CreateFromValue v

/// A little ugly, but the idea is that if a data structure does not 
/// contain lazy values then we don't add laziness.  So if the thing to map  
/// is already evaluated then immediately apply the function.  
let lazyMap f (x:Lazy<_>) =  
      if x.IsValueCreated then notlazy (f (x.Force())) else Lazy.Create (fun () -> f (x.Force()))

// -------------------------------------------------------------------- 
// Ordered lists with a lookup table
// --------------------------------------------------------------------

/// This is used to store event, property and field maps.
///
/// Review: this is not such a great data structure.
type LazyOrderedMultiMap<'Key,'Data when 'Key : equality>(keyf : 'Data -> 'Key, lazyItems : Lazy<'Data list>) = 

    let quickMap= 
        lazyItems |> lazyMap (fun entries -> 
            let t = new Dictionary<_,_>(entries.Length, HashIdentity.Structural)
            do entries |> List.iter (fun y -> let key = keyf y in t.[key] <- y :: (if t.ContainsKey(key) then t.[key] else [])) 
            t)

    member self.Entries() = lazyItems.Force()

    member self.Add(y) = new LazyOrderedMultiMap<'Key,'Data>(keyf, lazyItems |> lazyMap (fun x -> y :: x))
    
    member self.Filter(f) = new LazyOrderedMultiMap<'Key,'Data>(keyf, lazyItems |> lazyMap (List.filter f))

    member self.Item with get(x) = let t = quickMap.Force() in if t.ContainsKey x then t.[x] else []


//---------------------------------------------------------------------
// SHA1 hash-signing algorithm.  Used to get the public key token from
// the public key.
//---------------------------------------------------------------------


let b0 n =  (n &&& 0xFF)
let b1 n =  ((n >>> 8) &&& 0xFF)
let b2 n =  ((n >>> 16) &&& 0xFF)
let b3 n =  ((n >>> 24) &&& 0xFF)


module SHA1 = 
    let inline (>>>&)  (x:int) (y:int)  = int32 (uint32 x >>> y)
    let f(t,b,c,d) = 
        if t < 20 then (b &&& c) ||| ((~~~b) &&& d) else
        if t < 40 then b ^^^ c ^^^ d else
        if t < 60 then (b &&& c) ||| (b &&& d) ||| (c &&& d) else
        b ^^^ c ^^^ d

    let k0to19 = 0x5A827999
    let k20to39 = 0x6ED9EBA1
    let k40to59 = 0x8F1BBCDC
    let k60to79 = 0xCA62C1D6

    let k(t) = 
        if t < 20 then k0to19 
        elif t < 40 then k20to39 
        elif t < 60 then k40to59 
        else k60to79 


    type chan = SHABytes of byte[] 
    type sha_instream = 
        { stream: chan;
          mutable pos: int;
          mutable eof:  bool; }

    let rot_left32 x n =  (x <<< n) ||| (x >>>& (32-n))

    let sha_eof sha = sha.eof

    (* padding and length (in bits!) recorded at end *)
    let sha_after_eof sha  = 
        let n = sha.pos
        let len = 
          (match sha.stream with
          | SHABytes s -> s.Length)
        if n = len then 0x80
        else 
          let padded_len = (((len + 9 + 63) / 64) * 64) - 8
          if n < padded_len - 8  then 0x0  
          elif (n &&& 63) = 56 then int32 ((int64 len * int64 8) >>> 56) &&& 0xff
          elif (n &&& 63) = 57 then int32 ((int64 len * int64 8) >>> 48) &&& 0xff
          elif (n &&& 63) = 58 then int32 ((int64 len * int64 8) >>> 40) &&& 0xff
          elif (n &&& 63) = 59 then int32 ((int64 len * int64 8) >>> 32) &&& 0xff
          elif (n &&& 63) = 60 then int32 ((int64 len * int64 8) >>> 24) &&& 0xff
          elif (n &&& 63) = 61 then int32 ((int64 len * int64 8) >>> 16) &&& 0xff
          elif (n &&& 63) = 62 then int32 ((int64 len * int64 8) >>> 8) &&& 0xff
          elif (n &&& 63) = 63 then (sha.eof <- true; int32 (int64 len * int64 8) &&& 0xff)
          else 0x0

    let sha_read8 sha = 
        let b = 
            match sha.stream with 
            | SHABytes s -> if sha.pos >= s.Length then sha_after_eof sha else int32 s.[sha.pos]
        sha.pos <- sha.pos + 1; 
        b
        
    let sha_read32 sha  = 
        let b0 = sha_read8 sha
        let b1 = sha_read8 sha
        let b2 = sha_read8 sha
        let b3 = sha_read8 sha
        let res = (b0 <<< 24) ||| (b1 <<< 16) ||| (b2 <<< 8) ||| b3
        res


    let sha1_hash sha = 
        let h0 = ref 0x67452301
        let h1 = ref 0xEFCDAB89
        let h2 = ref 0x98BADCFE
        let h3 = ref 0x10325476
        let h4 = ref 0xC3D2E1F0
        let a = ref 0
        let b = ref 0
        let c = ref 0
        let d = ref 0
        let e = ref 0
        let w = Array.create 80 0x00
        while (not (sha_eof sha)) do
            for i = 0 to 15 do
                w.[i] <- sha_read32 sha
            for t = 16 to 79 do
                w.[t] <- rot_left32 (w.[t-3] ^^^ w.[t-8] ^^^ w.[t-14] ^^^ w.[t-16]) 1;
            a := !h0; 
            b := !h1; 
            c := !h2; 
            d := !h3; 
            e := !h4;
            for t = 0 to 79 do
                let temp =  (rot_left32 !a 5) + f(t,!b,!c,!d) + !e + w.[t] + k(t)
                e := !d; 
                d := !c; 
                c :=  rot_left32 !b 30; 
                b := !a; 
                a := temp;
            h0 := !h0 + !a; 
            h1 := !h1 + !b; 
            h2 := !h2 + !c;  
            h3 := !h3 + !d; 
            h4 := !h4 + !e
        (!h0,!h1,!h2,!h3,!h4)

    let sha1_hash_bytes s = 
        let (_h0,_h1,_h2,h3,h4) = sha1_hash { stream = SHABytes s; pos = 0; eof = false }   // the result of the SHA algorithm is stored in registers 3 and 4
        Array.map byte [|  b0 h4; b1 h4; b2 h4; b3 h4; b0 h3; b1 h3; b2 h3; b3 h3; |]


let sha1_hash_bytes s = SHA1.sha1_hash_bytes s

// --------------------------------------------------------------------
// 
// -------------------------------------------------------------------- 

type ILVersionInfo = uint16 * uint16 * uint16 * uint16

type Locale = string

[<StructuralEquality; StructuralComparison>]
type PublicKey =
    | PublicKey of byte[]
    | PublicKeyToken of byte[]
    member x.IsKey=match x with PublicKey _ -> true | _ -> false
    member x.IsKeyToken=match x with PublicKeyToken _ -> true | _ -> false
    member x.Key=match x with PublicKey b -> b | _ -> invalidOp "not a key"
    member x.KeyToken=match x with PublicKeyToken b -> b | _ -> invalidOp"not a key token"

    member x.ToToken() = 
        match x with 
        | PublicKey bytes -> SHA1.sha1_hash_bytes bytes
        | PublicKeyToken token -> token
    static member KeyAsToken(k) = PublicKeyToken(PublicKey(k).ToToken())

[<StructuralEquality; StructuralComparison>]
type AssemblyRefData =
    { assemRefName: string;
      assemRefHash: byte[] option;
      assemRefPublicKeyInfo: PublicKey option;
      assemRefRetargetable: bool;
      assemRefVersion: ILVersionInfo option;
      assemRefLocale: Locale option; } 

/// Global state: table of all assembly references keyed by AssemblyRefData

[<Sealed>]
type ILAssemblyRef(data)  =  
    member x.Name=data.assemRefName
    member x.Hash=data.assemRefHash
    member x.PublicKey=data.assemRefPublicKeyInfo
    member x.Retargetable=data.assemRefRetargetable  
    member x.Version=data.assemRefVersion
    member x.Locale=data.assemRefLocale
    member x.Data = data
    interface System.IComparable with
        override x.CompareTo(yobj) = compare x.Data (yobj :?> ILAssemblyRef).Data 
    override x.GetHashCode() = hash data
    override x.Equals(yobj) = ((yobj :?> ILAssemblyRef).Data = data)
    static member Create(name,hash,publicKey,retargetable,version,locale) =
        ILAssemblyRef
            { assemRefName=name;
              assemRefHash=hash;
              assemRefPublicKeyInfo=publicKey;
              assemRefRetargetable=retargetable;
              assemRefVersion=version;
              assemRefLocale=locale; } 

    static member FromAssembly(assembly:System.Reflection.Assembly) =
        let aname = assembly.GetName()
        let locale = None
        let publicKey = 
           match aname.GetPublicKey()  with 
           | null | [| |] -> 
               match aname.GetPublicKeyToken()  with 
               | null | [| |] -> None
               | bytes -> Some (PublicKeyToken bytes)
           | bytes -> 
               Some (PublicKey bytes)
        
        let version = 
           match aname.Version with 
           | null -> None
           | v -> Some (uint16 v.Major,uint16 v.Minor,uint16 v.Build,uint16 v.Revision)

        ILAssemblyRef.Create(aname.Name,None,publicKey,false,version,locale)

    member aref.QualifiedName = 
        let b = new System.Text.StringBuilder(100)
        let add (s:string) = (b.Append(s) |> ignore)
        let addC (s:char) = (b.Append(s) |> ignore)
        add(aref.Name);
        match aref.Version with 
        | None -> ()
        | Some (a,b,c,d) -> 
            add ", Version=";
            add (string (int a))
            add ".";
            add (string (int b))
            add ".";
            add (string (int c))
            add ".";
            add (string (int d))
            add ", Culture="
            match aref.Locale with 
            | None -> add "neutral"
            | Some b -> add b
            add ", PublicKeyToken="
            match aref.PublicKey with 
            | None -> add "null"
            | Some pki -> 
                  let pkt = pki.ToToken()
                  let convDigit(digit) = 
                      let digitc = 
                          if digit < 10 
                          then  System.Convert.ToInt32 '0' + digit 
                          else System.Convert.ToInt32 'a' + (digit - 10) 
                      System.Convert.ToChar(digitc)
                  for i = 0 to pkt.Length-1 do
                      let v = pkt.[i]
                      addC (convDigit(System.Convert.ToInt32(v)/16))
                      addC (convDigit(System.Convert.ToInt32(v)%16))
        b.ToString()


[<StructuralEquality; StructuralComparison>]
type ILModuleRef = 
    { name: string;
      hasMetadata: bool; 
      hash: byte[] option; }
    static member Create(name,hasMetadata,hash) = 
        { name=name;
          hasMetadata= hasMetadata;
          hash=hash }
    
    member x.Name=x.name
    member x.HasMetadata=x.hasMetadata
    member x.Hash=x.hash 

[<StructuralEquality; StructuralComparison>]
type ILScopeRef = 
    | ScopeRef_local
    | ScopeRef_module of ILModuleRef 
    | ScopeRef_assembly of ILAssemblyRef
    static member Local = ScopeRef_local
    static member Module(mref) = ScopeRef_module(mref)
    static member Assembly(aref) = ScopeRef_assembly(aref)
    member x.IsLocalRef   = match x with ScopeRef_local      -> true | _ -> false
    member x.IsModuleRef  = match x with ScopeRef_module _   -> true | _ -> false
    member x.IsAssemblyRef= match x with ScopeRef_assembly _ -> true | _ -> false
    member x.ModuleRef    = match x with ScopeRef_module x   -> x | _ -> failwith "not a module reference"
    member x.AssemblyRef  = match x with ScopeRef_assembly x -> x | _ -> failwith "not an assembly reference"

    member scoref.QualifiedName = 
        match scoref with 
        | ScopeRef_local -> ""
        | ScopeRef_module mref -> "module "^mref.Name
        | ScopeRef_assembly aref when aref.Name = "mscorlib" -> ""
        | ScopeRef_assembly aref -> aref.QualifiedName

    member scoref.QualifiedNameWithNoShortMscorlib = 
        match scoref with 
        | ScopeRef_local -> ""
        | ScopeRef_module mref -> "module "^mref.Name
        | ScopeRef_assembly aref -> aref.QualifiedName

type ILArrayBound = int32 option 
type ILArrayBounds = ILArrayBound * ILArrayBound

[<StructuralEquality; StructuralComparison>]
type ILArrayShape = 
    | ILArrayShape of ILArrayBounds list (* lobound/size pairs *)
    member x.Rank = (let (ILArrayShape l) = x in l.Length)


/// Calling conventions.  These are used in method pointer types.
[<StructuralEquality; StructuralComparison>]
type ILArgumentConvention = 
    | CC_default
    | CC_cdecl 
    | CC_stdcall 
    | CC_thiscall 
    | CC_fastcall 
    | CC_vararg
      
[<StructuralEquality; StructuralComparison>]
type ILThisConvention =
    | CC_instance
    | CC_instance_explicit
    | CC_static

let mutable instance_callconv : obj = new obj()
let mutable static_callconv : obj = new obj()

[<StructuralEquality; StructuralComparison>]
type ILCallingConv =
    | Callconv of ILThisConvention * ILArgumentConvention
    member x.ThisConv           = let (Callconv(a,_b)) = x in a
    member x.BasicConv          = let (Callconv(_a,b)) = x in b
    member x.IsInstance         = match x.ThisConv with CC_instance -> true | _ -> false
    member x.IsInstanceExplicit = match x.ThisConv with CC_instance_explicit -> true | _ -> false
    member x.IsStatic           = match x.ThisConv with CC_static -> true | _ -> false

    static member Instance : ILCallingConv = unbox(instance_callconv) 
    static member Static : ILCallingConv = unbox(static_callconv) 

do instance_callconv <- box (Callconv(CC_instance,CC_default))
do static_callconv <- box (Callconv(CC_static,CC_default))

let callconv_eq (a:ILCallingConv) b = (a = b)

type ILBoxity = 
  | AsObject 
  | AsValue

// IL type references have a pre-computed hash code to enable quick lookup tables during binary generation.
[<CustomEquality; CustomComparison>]
type ILTypeRef = 
    { trefScope: ILScopeRef;
      trefEnclosing: string list;
      trefName: string; 
      hashCode : int }
      
    static member Create(scope,enclosing,name) = 
        let hashCode = hash scope * 17 ^^^ (hash enclosing * 101 <<< 1) ^^^ (hash name * 47 <<< 2)
        { trefScope=scope;
          trefEnclosing= enclosing;
          trefName=name;
          hashCode=hashCode }
          
    member x.Scope= x.trefScope
    member x.Enclosing= x.trefEnclosing
    member x.Name=x.trefName
    member x.ApproxId= x.hashCode
    override x.GetHashCode() = x.hashCode
    override x.Equals(yobj) = 
         let y = (yobj :?> ILTypeRef) 
         (x.ApproxId = y.ApproxId) && 
         (x.Scope = y.Scope) && 
         (x.Name = y.Name) && 
         (x.Enclosing = y.Enclosing)
    interface System.IComparable with
        override x.CompareTo(yobj) = 
            let y = (yobj :?> ILTypeRef) 
            let c = compare x.ApproxId y.ApproxId
            if c <> 0 then c else
            let c = compare x.Scope y.Scope
            if c <> 0 then c else
            let c = compare x.Name y.Name 
            if c <> 0 then c else
            compare x.Enclosing y.Enclosing
        
    member tref.FullName = String.concat "." (tref.Enclosing @ [tref.Name])
        
    member tref.BasicQualifiedName = 
        String.concat "+" (tref.Enclosing @ [ tref.Name ])

    member tref.AddQualifiedNameExtensionWithNoShortMscorlib(basic) = 
        let sco = tref.Scope.QualifiedNameWithNoShortMscorlib
        if sco = "" then basic else String.concat ", " [basic;sco]

    member tref.QualifiedNameWithNoShortMscorlib = 
        tref.AddQualifiedNameExtensionWithNoShortMscorlib(tref.BasicQualifiedName)

    member tref.QualifiedName = 
        let basic = tref.BasicQualifiedName
        let sco = tref.Scope.QualifiedName
        if sco = "" then basic else String.concat ", " [basic;sco]


    override x.ToString() = x.FullName

type 
    [<StructuralEquality; StructuralComparison>]
    ILTypeSpec = 
    { tspecTypeRef: ILTypeRef;    
      /// The type instantiation if the type is generic
      tspecInst: ILGenericArgs }    
    member x.TypeRef=x.tspecTypeRef
    member x.Scope=x.TypeRef.Scope
    member x.Enclosing=x.TypeRef.Enclosing
    member x.Name=x.TypeRef.Name
    member x.GenericArgs=x.tspecInst
    static member Create(tref,inst) = { tspecTypeRef =tref; tspecInst=inst }
    override x.ToString() = x.TypeRef.ToString() + (match x.GenericArgs with [] -> "" | _ -> "<...>")
    member x.BasicQualifiedName = 
        let tc = x.TypeRef.BasicQualifiedName
        match x.GenericArgs with 
        | [] -> tc
        | args -> tc + "[" + String.concat "," (args |> List.map (fun arg -> "[" + arg.QualifiedNameWithNoShortMscorlib + "]")) + "]"

    member x.AddQualifiedNameExtensionWithNoShortMscorlib(basic) = 
        x.TypeRef.AddQualifiedNameExtensionWithNoShortMscorlib(basic)

and [<StructuralEquality; StructuralComparison>]
    ILType =
    | Type_void                   
    | Type_array    of ILArrayShape * ILType 
    | Type_value    of ILTypeSpec      
    | Type_boxed    of ILTypeSpec      
    | Type_ptr      of ILType             
    | Type_byref    of ILType           
    | Type_fptr     of ILCallingSignature 
    | Type_tyvar    of uint16              
    | Type_modified of bool * ILTypeRef * ILType

    member x.BasicQualifiedName = 
        match x with 
        | Type_tyvar n -> "!" + string n
        | Type_modified(_,_ty1,ty2) -> ty2.BasicQualifiedName
        | Type_array (ILArrayShape(s),ty) -> ty.BasicQualifiedName + "[" + System.String(',',s.Length-1) + "]"
        | Type_value tr | Type_boxed tr -> tr.BasicQualifiedName
        | Type_void -> "void"
        | Type_ptr _ty -> failwith "unexpected pointer type"
        | Type_byref _ty -> failwith "unexpected byref type"
        | Type_fptr _mref -> failwith "unexpected function pointer type"

    member x.AddQualifiedNameExtensionWithNoShortMscorlib(basic) = 
        match x with 
        | Type_tyvar _n -> basic
        | Type_modified(_,_ty1,ty2) -> ty2.AddQualifiedNameExtensionWithNoShortMscorlib(basic)
        | Type_array (ILArrayShape(_s),ty) -> ty.AddQualifiedNameExtensionWithNoShortMscorlib(basic)
        | Type_value tr | Type_boxed tr -> tr.AddQualifiedNameExtensionWithNoShortMscorlib(basic)
        | Type_void -> failwith "void"
        | Type_ptr _ty -> failwith "unexpected pointer type"
        | Type_byref _ty -> failwith "unexpected byref type"
        | Type_fptr _mref -> failwith "unexpected function pointer type"
        
    member x.QualifiedNameWithNoShortMscorlib = 
        x.AddQualifiedNameExtensionWithNoShortMscorlib(x.BasicQualifiedName)

and 
    [<CustomEquality; CustomComparison>]
    IlxExtensionType = 
    | Ext_typ of obj
    member x.Value = (let (Ext_typ(v)) = x in v)
    override x.Equals(yobj) = match yobj with :? IlxExtensionType as y -> Unchecked.equals x.Value y.Value | _ -> false
    interface System.IComparable with
        override x.CompareTo(yobj) = match yobj with :? IlxExtensionType as y -> Unchecked.compare x.Value y.Value | _ -> invalidOp "bad comparison"

and [<StructuralEquality; StructuralComparison>]
    ILCallingSignature = 
    { callsigCallconv: ILCallingConv;
      callsigArgs: ILType list;
      callsigReturn: ILType }
    member x.CallingConv = x.callsigCallconv
    member x.ArgTypes = x.callsigArgs
    member x.ReturnType = x.callsigReturn


and ILGenericArgs = 
    ILType list


type ILMethodRef =
    { mrefParent: ILTypeRef;
      mrefCallconv: ILCallingConv;
      mrefGenericArity: int; 
      mrefName: string;
      mrefArgs: ILType list;
      mrefReturn: ILType }
    member x.EnclosingTypeRef = x.mrefParent
    member x.CallingConv = x.mrefCallconv
    member x.Name = x.mrefName
    member x.GenericArity = x.mrefGenericArity
    member x.ArgCount = x.mrefArgs.Length
    member x.ArgTypes = x.mrefArgs
    member x.ReturnType = x.mrefReturn

    static member Create(a,b,c,d,e,f) = 
        { mrefParent= a;mrefCallconv=b;mrefName=c;mrefGenericArity=d; mrefArgs=e;mrefReturn=f }
    override x.ToString() = x.EnclosingTypeRef.ToString() + "::" + x.Name + "(...)"


[<StructuralEquality; StructuralComparison>]
type ILFieldRef = 
    { frefParent: ILTypeRef;
      frefName: string;
      frefType: ILType }
    member x.EnclosingTypeRef = x.frefParent
    member x.Name = x.frefName
    member x.Type = x.frefType
    override x.ToString() = x.EnclosingTypeRef.ToString() + "::" + x.Name

[<StructuralEquality; StructuralComparison>]
type ILMethodSpec = 
    { mspecMethodRefF: ILMethodRef;
      mspecEnclosingTypeF: ILType;          
      mspecMethodInstF: ILGenericArgs; }     
    static member Create(a,b,c) = { mspecEnclosingTypeF=a; mspecMethodRefF =b; mspecMethodInstF=c }
    member x.MethodRef = x.mspecMethodRefF
    member x.EnclosingType=x.mspecEnclosingTypeF
    member x.GenericArgs=x.mspecMethodInstF
    member x.Name=x.MethodRef.Name
    member x.CallingConv=x.MethodRef.CallingConv
    member x.GenericArity = x.MethodRef.GenericArity
    member x.FormalArgTypes = x.MethodRef.ArgTypes
    member x.FormalReturnType = x.MethodRef.ReturnType
    override x.ToString() = x.MethodRef.ToString() + "(...)"

type ILFieldSpec =
    { fspecFieldRef: ILFieldRef;
      fspecEnclosingType: ILType }         
    member x.FieldRef         = x.fspecFieldRef
    member x.EnclosingType    = x.fspecEnclosingType
    member x.FormalType       = x.FieldRef.Type
    member x.Name             = x.FieldRef.Name
    member x.EnclosingTypeRef = x.FieldRef.EnclosingTypeRef
    override x.ToString() = x.FieldRef.ToString()


// --------------------------------------------------------------------
// Debug info.                                                     
// -------------------------------------------------------------------- 

type Guid =  byte[]

type ILPlatform = 
    | X86
    | AMD64
    | IA64

type ILSourceDocument = 
    { sourceLanguage: Guid option; 
      sourceVendor: Guid option;
      sourceDocType: Guid option;
      sourceFile: string; }
    static member Create(language,vendor,docType,file) =
        { sourceLanguage=language; 
          sourceVendor=vendor;
          sourceDocType=docType;
          sourceFile=file; }
    member x.Language=x.sourceLanguage
    member x.Vendor=x.sourceVendor
    member x.DocumentType=x.sourceDocType
    member x.File=x.sourceFile

type ILSourceMarker =
    { sourceDocument: ILSourceDocument;
      sourceLine: int;
      sourceColumn: int;
      sourceEndLine: int;
      sourceEndColumn: int }
    static member Create(document, line, column, endLine, endColumn) = 
        { sourceDocument=document;
          sourceLine=line;
          sourceColumn=column;
          sourceEndLine=endLine;
          sourceEndColumn=endColumn }
    member x.Document=x.sourceDocument
    member x.Line=x.sourceLine
    member x.Column=x.sourceColumn
    member x.EndLine=x.sourceEndLine
    member x.EndColumn=x.sourceEndColumn
    override x.ToString() = sprintf "(%d,%d)-(%d,%d)" x.Line x.Column x.EndLine x.EndColumn

// --------------------------------------------------------------------
// Custom attributes                                                     
// -------------------------------------------------------------------- 

type ILAttributeElement =  
  | CustomElem_string of string  option
  | CustomElem_bool of bool
  | CustomElem_char of char
  | CustomElem_int8 of int8
  | CustomElem_int16 of int16
  | CustomElem_int32 of int32
  | CustomElem_int64 of int64
  | CustomElem_uint8 of uint8
  | CustomElem_uint16 of uint16
  | CustomElem_uint32 of uint32
  | CustomElem_uint64 of uint64
  | CustomElem_float32 of single
  | CustomElem_float64 of double
  | CustomElem_objnull 
  | CustomElem_type of ILType option
  | CustomElem_tref of ILTypeRef option
  | CustomElem_array of ILType * ILAttributeElement list

type ILAttributeNamedArg =  (string * ILType * bool * ILAttributeElement)
type ILAttribute = 
    { customMethod: ILMethodSpec;
      customData: byte[] }
    member x.Data = x.customData
    member x.Method =x.customMethod

[<NoEquality; NoComparison>]
type ILAttributes = 
   | CustomAttrs of Lazy<ILAttribute list>

type ILCodeLabel = int

// --------------------------------------------------------------------
// Instruction set.                                                     
// -------------------------------------------------------------------- 

type ILBasicType =
  | DT_R
  | DT_I1
  | DT_U1
  | DT_I2
  | DT_U2
  | DT_I4
  | DT_U4
  | DT_I8
  | DT_U8
  | DT_R4
  | DT_R8
  | DT_I
  | DT_U
  | DT_REF

type ILTokenSpec = 
  | Token_type of ILType 
  | Token_method of ILMethodSpec 
  | Token_field of ILFieldSpec

[<StructuralEquality; StructuralComparison>]
type ILConstSpec = 
  | NUM_I4 of int32
  | NUM_I8 of int64
  | NUM_R4 of single
  | NUM_R8 of double

type Tailcall = 
  | Tailcall
  | Normalcall

type Alignment =  
  | Aligned
  | Unaligned_1
  | Unaligned_2
  | Unaligned_4

type Volatility =  
  | Volatile
  | Nonvolatile

type ReadonlySpec =  
  | ReadonlyAddress
  | NormalAddress

type varargs = ILType list option

[<StructuralEquality; StructuralComparison>]
type ILComparisonInstr = 
  | BI_beq        
  | BI_bge        
  | BI_bge_un     
  | BI_bgt        
  | BI_bgt_un        
  | BI_ble        
  | BI_ble_un        
  | BI_blt        
  | BI_blt_un 
  | BI_bne_un 
  | BI_brfalse 
  | BI_brtrue 

[<StructuralEquality; StructuralComparison>]
type ILArithInstr = 
  | AI_add    
  | AI_add_ovf
  | AI_add_ovf_un
  | AI_and    
  | AI_div   
  | AI_div_un
  | AI_ceq      
  | AI_cgt      
  | AI_cgt_un   
  | AI_clt     
  | AI_clt_un  
  | AI_conv      of ILBasicType
  | AI_conv_ovf  of ILBasicType
  | AI_conv_ovf_un  of ILBasicType
  | AI_mul       
  | AI_mul_ovf   
  | AI_mul_ovf_un
  | AI_rem       
  | AI_rem_un       
  | AI_shl       
  | AI_shr       
  | AI_shr_un
  | AI_sub       
  | AI_sub_ovf   
  | AI_sub_ovf_un   
  | AI_xor       
  | AI_or        
  | AI_neg       
  | AI_not       
  | AI_ldnull    
  | AI_dup       
  | AI_pop
  | AI_ckfinite 
  | AI_nop
  | AI_ldc of ILBasicType * ILConstSpec 



[<StructuralEquality; NoComparison>]
type ILInstr = 
  | I_arith of ILArithInstr
  | I_ldarg     of uint16
  | I_ldarga    of uint16
  | I_ldind     of Alignment * Volatility * ILBasicType
  | I_ldloc     of uint16
  | I_ldloca    of uint16
  | I_starg     of uint16
  | I_stind     of  Alignment * Volatility * ILBasicType
  | I_stloc     of uint16

  | I_br    of  ILCodeLabel
  | I_jmp   of ILMethodSpec
  | I_brcmp of ILComparisonInstr * ILCodeLabel * ILCodeLabel (* second label is fall-through *)
  | I_switch    of (ILCodeLabel list * ILCodeLabel) (* last label is fallthrough *)
  | I_ret 

  | I_call     of Tailcall * ILMethodSpec * varargs
  | I_callvirt of Tailcall * ILMethodSpec * varargs
  | I_callconstraint of Tailcall * ILType * ILMethodSpec * varargs
  | I_calli    of Tailcall * ILCallingSignature * varargs
  | I_ldftn    of ILMethodSpec
  | I_newobj  of ILMethodSpec  * varargs
  
  | I_throw
  | I_endfinally
  | I_endfilter
  | I_leave     of  ILCodeLabel

  | I_ldsfld      of Volatility * ILFieldSpec
  | I_ldfld       of Alignment * Volatility * ILFieldSpec
  | I_ldsflda     of ILFieldSpec
  | I_ldflda      of ILFieldSpec 
  | I_stsfld      of Volatility  *  ILFieldSpec
  | I_stfld       of Alignment * Volatility * ILFieldSpec
  | I_ldstr       of string
  | I_isinst      of ILType
  | I_castclass   of ILType
  | I_ldtoken     of ILTokenSpec
  | I_ldvirtftn   of ILMethodSpec

  | I_cpobj       of ILType
  | I_initobj     of ILType
  | I_ldobj       of Alignment * Volatility * ILType
  | I_stobj       of Alignment * Volatility * ILType
  | I_box         of ILType
  | I_unbox       of ILType
  | I_unbox_any   of ILType
  | I_sizeof      of ILType

  | I_ldelem      of ILBasicType
  | I_stelem      of ILBasicType
  | I_ldelema     of ReadonlySpec * ILArrayShape * ILType
  | I_ldelem_any  of ILArrayShape * ILType
  | I_stelem_any  of ILArrayShape * ILType
  | I_newarr      of ILArrayShape * ILType 
  | I_ldlen

  | I_mkrefany    of ILType
  | I_refanytype  
  | I_refanyval   of ILType
  | I_rethrow

  | I_break 
  | I_seqpoint of ILSourceMarker

  | I_arglist  

  | I_localloc
  | I_cpblk of Alignment * Volatility
  | I_initblk of Alignment  * Volatility

  (* FOR EXTENSIONS, e.g. MS-ILX *)  
  | EI_ilzero of ILType
  | EI_ldlen_multi      of int32 * int32
  | I_other    of IlxExtensionInstr

and IlxExtensionInstr = Ext_instr of obj


type ILDebugMapping = 
    { localNum: int;
      localName: string; }
    member x.LocalVarIndex = x.localNum
    member x.Name = x.localName

type ILBasicBlock = 
    { bblockLabel: ILCodeLabel;
      bblockInstrs: ILInstr array }
    member x.Label = x.bblockLabel
    member x.Instructions = x.bblockInstrs

type ILCode = 
    | ILBasicBlock    of ILBasicBlock
    | GroupBlock    of ILDebugMapping list * ILCode list
    | RestrictBlock of ILCodeLabel list * ILCode
    | TryBlock      of ILCode * ILExceptionBlock

and ILExceptionBlock = 
    | FaultBlock       of ILCode 
    | FinallyBlock     of ILCode
    | FilterCatchBlock of (ILFilterBlock * ILCode) list

and ILFilterBlock = 
    | TypeFilter of ILType
    | CodeFilter of ILCode

type Local = 
    { localType: ILType;
      localPinned: bool }
    member x.Type = x.localType
    member x.IsPinned = x.localPinned
      
type ILMethodBody = 
    { ilZeroInit: bool;
      ilMaxStack: int32;
      ilNoInlining: bool;
      ilLocals: Local list;
      ilCode:  ILCode;
      ilSource: ILSourceMarker option }

type ILMemberAccess = 
    | MemAccess_assembly
    | MemAccess_compilercontrolled
    | MemAccess_famandassem
    | MemAccess_famorassem
    | MemAccess_family
    | MemAccess_private 
    | MemAccess_public 

[<StructuralEquality; StructuralComparison>]
[<RequireQualifiedAccess>]
type ILFieldInit = 
    | String of string
    | Bool of bool
    | Char of uint16
    | Int8 of int8
    | Int16 of int16
    | Int32 of int32
    | Int64 of int64
    | UInt8 of uint8
    | UInt16 of uint16
    | UInt32 of uint32
    | UInt64 of uint64
    | Single of single
    | Double of double
    | Null
  
// -------------------------------------------------------------------- 
// Native Types, for marshalling to the native C interface.
// These are taken directly from the ILASM syntax, and don't really
// correspond yet to the ECMA Spec (Partition II, 7.4).  
// -------------------------------------------------------------------- 

[<StructuralEquality; StructuralComparison>]
type ILNativeType = 
    | NativeType_empty
    | NativeType_custom of Guid * string * string * byte[] (* guid,nativeTypeName,custMarshallerName,cookieString *)
    | NativeType_fixed_sysstring of int32
    | NativeType_fixed_array of int32
    | NativeType_currency
    | NativeType_lpstr
    | NativeType_lpwstr
    | NativeType_lptstr
    | NativeType_byvalstr
    | NativeType_tbstr
    | NativeType_lpstruct
    | NativeType_struct
    | NativeType_void
    | NativeType_bool
    | NativeType_int8
    | NativeType_int16
    | NativeType_int32
    | NativeType_int64
    | NativeType_float32
    | NativeType_float64
    | NativeType_unsigned_int8
    | NativeType_unsigned_int16
    | NativeType_unsigned_int32
    | NativeType_unsigned_int64
    | NativeType_array of ILNativeType option * (int32 * int32 option) option (* optional idx of parameter giving size plus optional additive i.e. num elems *)
    | NativeType_int
    | NativeType_unsigned_int
    | NativeType_method
    | NativeType_as_any
    | (* COM interop *) NativeType_bstr
    | (* COM interop *) NativeType_iunknown
    | (* COM interop *) NativeType_idsipatch
    | (* COM interop *) NativeType_interface
    | (* COM interop *) NativeType_error               
    | (* COM interop *) NativeType_safe_array of ILNativeVariantType * string option 
    | (* COM interop *) NativeType_ansi_bstr
    | (* COM interop *) NativeType_variant_bool


  and ILNativeVariantType = 
    | VariantType_empty
    | VariantType_null
    | VariantType_variant
    | VariantType_currency
    | VariantType_decimal               
    | VariantType_date               
    | VariantType_bstr               
    | VariantType_lpstr               
    | VariantType_lpwstr               
    | VariantType_iunknown               
    | VariantType_idispatch               
    | VariantType_safearray               
    | VariantType_error               
    | VariantType_hresult               
    | VariantType_carray               
    | VariantType_userdefined               
    | VariantType_record               
    | VariantType_filetime
    | VariantType_blob               
    | VariantType_stream               
    | VariantType_storage               
    | VariantType_streamed_object               
    | VariantType_stored_object               
    | VariantType_blob_object               
    | VariantType_cf                
    | VariantType_clsid
    | VariantType_void 
    | VariantType_bool
    | VariantType_int8
    | VariantType_int16                
    | VariantType_int32                
    | VariantType_int64                
    | VariantType_float32                
    | VariantType_float64                
    | VariantType_unsigned_int8                
    | VariantType_unsigned_int16                
    | VariantType_unsigned_int32                
    | VariantType_unsigned_int64                
    | VariantType_ptr                
    | VariantType_array of ILNativeVariantType                
    | VariantType_vector of ILNativeVariantType                
    | VariantType_byref of ILNativeVariantType                
    | VariantType_int                
    | VariantType_unsigned_int                

type ILSecurityAction = 
    | SecAction_request 
    | SecAction_demand
    | SecAction_assert
    | SecAction_deny
    | SecAction_permitonly
    | SecAction_linkcheck 
    | SecAction_inheritcheck
    | SecAction_reqmin
    | SecAction_reqopt
    | SecAction_reqrefuse
    | SecAction_prejitgrant
    | SecAction_prejitdeny
    | SecAction_noncasdemand
    | SecAction_noncaslinkdemand
    | SecAction_noncasinheritance
    | SecAction_linkdemandchoice
    | SecAction_inheritancedemandchoice
    | SecAction_demandchoice

type ILPermission = 
    | PermissionSet of ILSecurityAction * byte[]

type ILPermissions =
    | SecurityDecls of Lazy<ILPermission list>

[<RequireQualifiedAccess>]
type PInvokeCharBestFit  = 
    | UseAssembly
    | Enabled
    | Disabled

[<RequireQualifiedAccess>]
type PInvokeThrowOnUnmappableChar =
    | UseAssembly
    | Enabled
    | Disabled

[<RequireQualifiedAccess>]
type PInvokeCallingConvention =
    | None
    | Cdecl
    | Stdcall
    | Thiscall
    | Fastcall
    | WinApi

[<RequireQualifiedAccess>]
type PInvokeCharEncoding =
    | None
    | Ansi
    | Unicode
    | Auto

type PInvokeMethod =
    { pinvokeWhere: ILModuleRef;
      pinvokeName: string;
      pinvokeCallconv: PInvokeCallingConvention;
      PInvokeCharEncoding: PInvokeCharEncoding;
      pinvokeNoMangle: bool;
      pinvokeLastErr: bool;
      PInvokeThrowOnUnmappableChar: PInvokeThrowOnUnmappableChar;
      PInvokeCharBestFit: PInvokeCharBestFit }
    member x.Where = x.pinvokeWhere
    member x.Name = x.pinvokeName
    member x.CallingConv = x.pinvokeCallconv
    member x.CharEncoding = x.PInvokeCharEncoding
    member x.NoMangle = x.pinvokeNoMangle
    member x.LastError = x.pinvokeLastErr
    member x.ThrowOnUnmappableChar = x.PInvokeThrowOnUnmappableChar
    member x.CharBestFit = x.PInvokeCharBestFit

type ILParameter =
    { paramName: string option;
      paramType: ILType;
      paramDefault: ILFieldInit option;  
      paramMarshal: ILNativeType option; 
      paramIn: bool;
      paramOut: bool;
      paramOptional: bool;
      paramCustomAttrs: ILAttributes }
    member x.Name = x.paramName
    member x.Type = x.paramType
    member x.Default = x.paramDefault
    member x.Marshal = x.paramMarshal
    member x.IsIn = x.paramIn
    member x.IsOut = x.paramOut
    member x.IsOptional = x.paramOptional
    member x.CustomAttrs = x.paramCustomAttrs


type ILReturnValue = 
    { returnMarshal: ILNativeType option;
      returnType: ILType; 
      returnCustomAttrs: ILAttributes }
    member x.Type =  x.returnType
    member x.Marshal = x.returnMarshal
    member x.CustomAttrs = x.returnCustomAttrs

type OverridesSpec = 
    | OverridesSpec of ILMethodRef * ILType
    member x.MethodRef = let (OverridesSpec(mr,_ty)) = x in mr
    member x.EnclosingType = let (OverridesSpec(_mr,ty)) = x in ty

type ILMethodVirtualInfo = 
    { virtFinal: bool; 
      virtNewslot: bool; 
      virtStrict: bool; (* mdCheckAccessOnOverride *)
      virtAbstract: bool;
    }
    member x.IsFinal = x.virtFinal
    member x.IsNewSlot = x.virtNewslot
    member x.IsCheckAccessOnOverride = x.virtStrict
    member x.IsAbstract = x.virtAbstract

type MethodKind =
    | MethodKind_static 
    | MethodKind_cctor 
    | MethodKind_ctor 
    | MethodKind_nonvirtual 
    | MethodKind_virtual of ILMethodVirtualInfo

type MethodBody =
    | MethodBody_il of ILMethodBody
    | MethodBody_pinvoke of PInvokeMethod       (* platform invoke to native  *)
    | MethodBody_abstract
    | MethodBody_native

type LazyMethodBody = LazyMethodBody of Lazy<MethodBody >

type MethodCodeKind =
    | MethodCodeKind_il
    | MethodCodeKind_native
    | MethodCodeKind_runtime

let mk_mbody mb = LazyMethodBody (Lazy.CreateFromValue mb)
let dest_mbody (LazyMethodBody mb) = mb.Force()
let mk_lazy_mbody mb = LazyMethodBody mb

let typs_of_params (ps:ILParameter list) = ps |> List.map (fun p -> p.Type) 

[<StructuralEquality; StructuralComparison>]
type ILGenericVariance = 
    | NonVariant            
    | CoVariant             
    | ContraVariant         

type ILGenericParameterDef =
    { gpName: string;
      gpConstraints: ILType list;
      gpVariance: ILGenericVariance; 
      gpReferenceTypeConstraint: bool;     
      gpCustomAttrs : ILAttributes;
      gpNotNullableValueTypeConstraint: bool;
      gpDefaultConstructorConstraint: bool; }

    member x.Name = x.gpName
    member x.Constraints = x.gpConstraints
    member x.Variance = x.gpVariance
    member x.HasReferenceTypeConstraint = x.gpReferenceTypeConstraint
    member x.HasNotNullableValueTypeConstraint = x.gpNotNullableValueTypeConstraint
    member x.HasDefaultConstructorConstraint = x.gpDefaultConstructorConstraint
    override x.ToString() = x.Name 

type ILGenericParameterDefs = ILGenericParameterDef list

type ILMethodDef = 
    { mdName: string;
      mdKind: MethodKind;
      mdCallconv: ILCallingConv;
      mdParams: ILParameter list;
      mdReturn: ILReturnValue;
      mdAccess: ILMemberAccess;
      mdBody: LazyMethodBody;   
      mdCodeKind: MethodCodeKind;   
      mdInternalCall: bool;
      mdManaged: bool;
      mdForwardRef: bool;
      mdSecurityDecls: ILPermissions;
      mdHasSecurity: bool;
      mdEntrypoint:bool;
      mdReqSecObj: bool;
      mdHideBySig: bool;
      mdSpecialName: bool;
      mdUnmanagedExport: bool;
      mdSynchronized: bool;
      mdPreserveSig: bool;
      mdMustRun: bool; 
      mdExport: (int32 * string option) option;
      mdVtableEntry: (int32 * int32) option;
     
      mdGenericParams: ILGenericParameterDefs;
      mdCustomAttrs: ILAttributes; }
    member x.Name = x.mdName
    member x.CallingConv = x.mdCallconv
    member x.Parameters = x.mdParams
    member x.ParameterTypes = typs_of_params x.mdParams
    member x.Return = x.mdReturn
    member x.Access = x.mdAccess
    member x.IsInternalCall = x.mdInternalCall
    member x.IsManaged = x.mdManaged
    member x.IsForwardRef = x.mdForwardRef
    member x.SecurityDecls = x.mdSecurityDecls
    member x.HasSecurity = x.mdHasSecurity
    member x.IsEntrypoint = x.mdEntrypoint
    member x.IsReqSecObj = x.mdReqSecObj
    member x.IsHideBySig = x.mdHideBySig
    member x.IsUnmanagedExport = x.mdUnmanagedExport
    member x.IsSynchronized = x.mdSynchronized
    member x.IsPreserveSig = x.mdPreserveSig
    // Whidbey feature: SafeHandle finalizer must be run 
    member x.IsMustRun = x.mdMustRun
    member x.GenericParams = x.mdGenericParams
    member x.CustomAttrs = x.mdCustomAttrs
    member md.Code = 
          match dest_mbody md.mdBody with 
          | MethodBody_il il-> Some il.ilCode
          | _ -> None
    member x.IsIL = match dest_mbody x.mdBody with | MethodBody_il _ -> true | _ -> false
    member x.Locals = match dest_mbody x.mdBody with | MethodBody_il il -> il.ilLocals | _ -> []

    member x.MethodBody = match dest_mbody x.mdBody with MethodBody_il il -> il | _ -> failwith "ilmbody_of_mdef: not IL"

    member x.IsNoInline   = x.MethodBody.ilNoInlining  
    member x.SourceMarker = x.MethodBody.ilSource
    member x.MaxStack     = x.MethodBody.ilMaxStack  
    member x.IsZeroInit   = x.MethodBody.ilZeroInit

    member x.IsClassInitializer   = match x.mdKind with | MethodKind_cctor      -> true | _ -> false
    member x.IsConstructor        = match x.mdKind with | MethodKind_ctor       -> true | _ -> false
    member x.IsStatic             = match x.mdKind with | MethodKind_static     -> true | _ -> false
    member x.IsNonVirtualInstance = match x.mdKind with | MethodKind_nonvirtual -> true | _ -> false
    member x.IsVirtual            = match x.mdKind with | MethodKind_virtual _  -> true | _ -> false

    member x.IsFinal                = match x.mdKind with | MethodKind_virtual v -> v.virtFinal    | _ -> invalidOp "ILMethodDef.IsFinal"
    member x.IsNewSlot              = match x.mdKind with | MethodKind_virtual v -> v.virtNewslot  | _ -> invalidOp "ILMethodDef.IsNewSlot"
    member x.IsCheckAccessOnOverride= match x.mdKind with | MethodKind_virtual v -> v.virtStrict   | _ -> invalidOp "ILMethodDef.IsCheckAccessOnOverride"
    member x.IsAbstract             = match x.mdKind with | MethodKind_virtual v -> v.virtAbstract | _ -> invalidOp "ILMethodDef.IsAbstract"

/// Index table by name and arity. 
type MethodDefMap = Map<string, ILMethodDef list>

[<NoEquality; NoComparison>]
type ILMethodDefs = 
    | Methods of Lazy<ILMethodDef list * MethodDefMap>
    interface IEnumerable with 
        member x.GetEnumerator() = ((x :> IEnumerable<ILMethodDef>).GetEnumerator() :> IEnumerator)
    interface IEnumerable<ILMethodDef> with 
        member x.GetEnumerator() = 
            let (Methods(lms)) = x
            let ms,_ = lms.Force()
            (ms :> IEnumerable<ILMethodDef>).GetEnumerator()
    member x.AsList = Seq.toList x

type ILEventDef =
    { eventType: ILType option; 
      eventName: string;
      eventRTSpecialName: bool;
      eventSpecialName: bool;
      eventAddOn: ILMethodRef; 
      eventRemoveOn: ILMethodRef;
      eventFire: ILMethodRef option;
      eventOther: ILMethodRef list;
      eventCustomAttrs: ILAttributes; }
    member x.Type = x.eventType
    member x.Name = x.eventName
    member x.AddMethod = x.eventAddOn
    member x.RemoveMethod = x.eventRemoveOn
    member x.FireMethod = x.eventFire
    member x.OtherMethods = x.eventOther
    member x.CustomAttrs = x.eventCustomAttrs

(* Index table by name. *)
[<NoEquality; NoComparison>]
type ILEventDefs = 
    | Events of LazyOrderedMultiMap<string, ILEventDef>

type ILPropertyDef = 
    { propName: string;
      propRTSpecialName: bool;
      propSpecialName: bool;
      propSet: ILMethodRef option;
      propGet: ILMethodRef option;
      propCallconv: ILThisConvention;
      propType: ILType;
      propInit: ILFieldInit option;
      propArgs: ILType list;
      propCustomAttrs: ILAttributes; }
    member x.Name = x.propName
    member x.SetMethod = x.propSet
    member x.GetMethod = x.propGet
    member x.CallingConv = x.propCallconv
    member x.Type = x.propType
    member x.Init = x.propInit
    member x.Args = x.propArgs
    member x.CustomAttrs = x.propCustomAttrs
    
// Index table by name.
[<NoEquality; NoComparison>]
type ILPropertyDefs = 
    | Properties of LazyOrderedMultiMap<string, ILPropertyDef>

type ILFieldDef = 
    { fdName: string;
      fdType: ILType;
      fdStatic: bool;
      fdAccess: ILMemberAccess;
      fdData:  byte[] option;
      fdInit:  ILFieldInit option;
      fdOffset:  int32 option; (* -- the explicit offset in bytes *)
      fdSpecialName: bool;
      fdMarshal: ILNativeType option; 
      fdNotSerialized: bool;
      fdLiteral: bool ;
      fdInitOnly: bool;
      fdCustomAttrs: ILAttributes; }
    member x.Name = x.fdName
    member x.Type = x.fdType
    member x.IsStatic = x.fdStatic
    member x.Access = x.fdAccess
    member x.Data = x.fdData
    member x.LiteralValue = x.fdInit
    /// The explicit offset in bytes when explicit layout is used.
    member x.Offset = x.fdOffset
    member x.Marshal = x.fdMarshal
    member x.NotSerialized = x.fdNotSerialized
    member x.IsLiteral = x.fdLiteral
    member x.IsInitOnly = x.fdInitOnly
    member x.CustomAttrs = x.fdCustomAttrs


// Index table by name.  Keep a canonical list to make sure field order is not disturbed for binary manipulation.
type ILFieldDefs = 
    | Fields of LazyOrderedMultiMap<string, ILFieldDef>

type ILMethodImplDef =
    { mimplOverrides: OverridesSpec;
      mimplOverrideBy: ILMethodSpec }

// Index table by name and arity. 
type ILMethodImplDefs = 
    | MethodImpls of Lazy<MethodImplsMap>
and MethodImplsMap = Map<string * int, ILMethodImplDef list>

type ILTypeDefLayout =
    | TypeLayout_auto
    | TypeLayout_sequential of ILTypeDefLayoutInfo
    | TypeLayout_explicit of ILTypeDefLayoutInfo 

and ILTypeDefLayoutInfo =
    { typeSize: int32 option;
      typePack: uint16 option } 
    member x.Size = x.typeSize
    member x.Pack = x.typePack

type ILTypeDefInitSemantics =
    | TypeInit_beforefield
    | TypeInit_onany

[<RequireQualifiedAccess>]
type ILDefaultPInvokeEncoding =
    | Ansi
    | Auto
    | Unicode

type ILTypeDefAccess =
    | Public 
    | Private
    | Nested of ILMemberAccess 

[<RequireQualifiedAccess>]
type ILTypeDefKind =
    | Class
    | ValueType
    | Interface
    | Enum 
    | Delegate
    | Other of IlxExtensionTypeKind

and IlxExtensionTypeKind = Ext_type_def_kind of obj


type ILTypeDef =  
    { tdKind: ILTypeDefKind;
      tdName: string;  
      tdGenericParams: ILGenericParameterDefs;   (* class is generic *)
      tdAccess: ILTypeDefAccess;  
      tdAbstract: bool;
      tdSealed: bool; 
      tdSerializable: bool; 
      tdComInterop: bool; (* Class or interface generated for COM interop *) 
      tdLayout: ILTypeDefLayout;
      tdSpecialName: bool;
      tdEncoding: ILDefaultPInvokeEncoding;
      tdNested: ILTypeDefs;
      tdImplements: ILType list;  
      tdExtends: ILType option; 
      tdMethodDefs: ILMethodDefs;
      tdSecurityDecls: ILPermissions;
      tdHasSecurity: bool;
      tdFieldDefs: ILFieldDefs;
      tdMethodImpls: ILMethodImplDefs;
      tdInitSemantics: ILTypeDefInitSemantics;
      tdEvents: ILEventDefs;
      tdProperties: ILPropertyDefs;
      tdCustomAttrs: ILAttributes; }
    member x.IsClass=     (match x.tdKind with ILTypeDefKind.Class -> true | _ -> false)
    member x.IsValueType= (match x.tdKind with ILTypeDefKind.ValueType -> true | _ -> false)
    member x.IsInterface= (match x.tdKind with ILTypeDefKind.Interface -> true | _ -> false)
    member x.IsEnum=      (match x.tdKind with ILTypeDefKind.Enum -> true | _ -> false)
    member x.IsDelegate=  (match x.tdKind with ILTypeDefKind.Delegate -> true | _ -> false)
    member x.Name = x.tdName
    member x.GenericParams = x.tdGenericParams
    member x.Access = x.tdAccess
    member x.IsAbstract = x.tdAbstract
    member x.IsSealed = x.tdSealed
    member x.IsSerializable = x.tdSerializable
    member x.IsComInterop = x.tdComInterop
    member x.Layout = x.tdLayout
    member x.IsSpecialName = x.tdSpecialName
    member x.Encoding = x.tdEncoding
    member x.NestedTypes = x.tdNested
    member x.Implements = x.tdImplements
    member x.Extends = x.tdExtends
    member x.Methods = x.tdMethodDefs
    member x.SecurityDecls = x.tdSecurityDecls
    member x.HasSecurity = x.tdHasSecurity
    member x.Fields = x.tdFieldDefs
    member x.MethodImpls = x.tdMethodImpls
    member x.InitSemantics = x.tdInitSemantics
    member x.Events = x.tdEvents
    member x.Properties = x.tdProperties
    member x.CustomAttrs = x.tdCustomAttrs

and ILTypeDefs = 
    | TypeDefTable of Lazy<(string list * string * ILAttributes * Lazy<ILTypeDef>) array> * Lazy<TypeDefsMap>
    interface IEnumerable with 
        member x.GetEnumerator() = ((x :> IEnumerable<ILTypeDef>).GetEnumerator() :> IEnumerator)
    interface IEnumerable<ILTypeDef> with 
        member x.GetEnumerator() = 
            let (TypeDefTable (larr,_tab)) = x
            let tds = seq { for (_,_,_,td) in larr.Force() -> td.Force() }
            tds.GetEnumerator()
    member x.AsList = Seq.toList x
        
/// keyed first on namespace then on type name.  The namespace is often a unique key for a given type map.
and TypeDefsMap = 
     Map<string list,Dictionary<string,Lazy<ILTypeDef>>>

and NamespaceAndTypename = string list * string

type ILNestedExportedType =
    { nestedExportedTypeName: string;
      nestedExportedTypeAccess: ILMemberAccess;
      nestedExportedTypeNested: ILNestedExportedTypes;
      nestedExportedTypeCustomAttrs: ILAttributes } 

and ILNestedExportedTypes = ILNestedExportedTypes of Lazy<NestedExportedTypesMap>
and NestedExportedTypesMap = Map<string,ILNestedExportedType>

and ILExportedType =
    { exportedTypeScope: ILScopeRef;
      exportedTypeName: string;
      exportedTypeForwarder: bool;
      exportedTypeAccess: ILTypeDefAccess;
      exportedTypeNested: ILNestedExportedTypes;
      exportedTypeCustomAttrs: ILAttributes } 
    member x.ScopeRef = x.exportedTypeScope
    member x.Name = x.exportedTypeName
    member x.IsForwarder = x.exportedTypeForwarder
    member x.Access = x.exportedTypeAccess
    member x.Nested = x.exportedTypeNested
    member x.CustomAttrs = x.exportedTypeCustomAttrs

and ILExportedTypes = ILExportedTypes of Lazy<ExportedTypesMap>
and ExportedTypesMap = Map<string,ILExportedType>

[<RequireQualifiedAccess>]
type ILResourceAccess = 
    | Public 
    | Private 

[<RequireQualifiedAccess>]
type ILResourceLocation =
    | Local of (unit -> byte[])
    | File of ILModuleRef * int32
    | Assembly of ILAssemblyRef

type ILResource =
    { resourceName: string;
      resourceWhere: ILResourceLocation;
      resourceAccess: ILResourceAccess;
      resourceCustomAttrs: ILAttributes }
    member x.Name = x.resourceName
    member x.Location = x.resourceWhere
    member x.Access = x.resourceAccess
    member x.CustomAttrs = x.resourceCustomAttrs

type ILResources = ILResources of Lazy<ILResource list>

// -------------------------------------------------------------------- 
// One module in the "current" assembly
// -------------------------------------------------------------------- 

[<RequireQualifiedAccess>]
type ILAssemblyLongevity =
    | Unspecified
    | Library
    | PlatformAppDomain
    | PlatformProcess
    | PlatformSystem


type ILAssemblyManifest = 
    { manifestName: string;
      manifestAuxModuleHashAlgorithm: int32;
      manifestSecurityDecls: ILPermissions;
      manifestPublicKey: byte[] option;
      manifestVersion: ILVersionInfo option;
      manifestLocale: Locale option;
      manifestCustomAttrs: ILAttributes;

      manifestLongevity: ILAssemblyLongevity; 
      manifestDisableJitOptimizations: bool;
      manifestJitTracking: bool;
      manifestRetargetable: bool;

      manifestExportedTypes: ILExportedTypes;
               (* -- Records the types impemented by other modules. *)
      manifestEntrypointElsewhere: ILModuleRef option; 
               (* -- Records whether the entrypoint resides in another module. *)
    } 
    member x.Name = x.manifestName
    member x.AuxModuleHashAlgorithm = x.manifestAuxModuleHashAlgorithm
    member x.SecurityDecls = x.manifestSecurityDecls
    member x.PublicKey = x.manifestPublicKey
    member x.Version = x.manifestVersion
    member x.Locale = x.manifestLocale
    member x.CustomAttrs = x.manifestCustomAttrs
    member x.AssemblyLongevity = x.manifestLongevity
    member x.DisableJitOptimizations = x.manifestDisableJitOptimizations
    member x.JitTracking = x.manifestJitTracking
    member x.Retargetable = x.manifestRetargetable
    member x.ExportedTypes = x.manifestExportedTypes
    member x.EntrypointElsewhere = x.manifestEntrypointElsewhere

type ILModuleDef = 
    { modulManifest: ILAssemblyManifest option;
      modulCustomAttrs: ILAttributes;
      modulName: string;
      modulTypeDefs: ILTypeDefs;
      (* Random bits of relatively uninteresting data *)
      modulSubSystem: int32;
      modulDLL: bool;
      modulILonly: bool;
      modulPlatform: ILPlatform option; 
      modulStackReserveSize: int32 option;
      modul32bit: bool;
      modul64bit: bool;
      modulVirtAlignment: int32;
      modulPhysAlignment: int32;
      modulImageBase: int32;
      modulMetadataVersion: string;
      modulResources: ILResources;
      modulNativeResources: list<Lazy<byte[]>>; (* e.g. win32 resources *)
    }
    member x.Manifest = x.modulManifest
    member x.CustomAttrs = x.modulCustomAttrs
    member x.Name = x.modulName
    member x.TypeDefs = x.modulTypeDefs
    member x.SubSystemFlags = x.modulSubSystem
    member x.IsDLL = x.modulDLL
    member x.IsILOnly = x.modulILonly
    member x.Platform = x.modulPlatform
    member x.Is32Bit = x.modul32bit
    member x.Is64Bit = x.modul64bit
    member x.VirtualAlignment = x.modulVirtAlignment
    member x.PhysicalAlignment = x.modulPhysAlignment
    member x.ImageBase = x.modulImageBase
    member x.MetadataVersion = x.modulMetadataVersion
    member x.Resources = x.modulResources
    member x.NativeResources = x.modulNativeResources

    member x.ManifestOfAssembly = 
        match x.modulManifest with 
        | Some m -> m
        | None -> failwith "no manifest.  It is possible you are using an auxiliary module of an assembly in a context where the main module of an assembly is expected.  Typically the main module of an assembly must be specified first within a list of the modules in an assembly."



// -------------------------------------------------------------------- 
// Utilities: type names
// -------------------------------------------------------------------- 

let split_name_at nm idx = 
    if idx < 0 then failwith "split_name_at: idx < 0";
    let last = String.length nm - 1 
    if idx > last then failwith "split_name_at: idx > last";
    (nm.Substring(0,idx)),
    (if idx < last then nm.Substring (idx+1,last - idx) else "")

let rec split_namespace_aux (nm:string) = 
    match nm.IndexOf '.' with 
    | -1 -> [nm]
    | idx -> 
        let s1,s2 = split_name_at nm idx 
        s1::split_namespace_aux s2 

/// Global State. All namespace splits
let memoize_namespace_tab = 
    Dictionary<string,string list>(10)

let split_namespace nm =
    let mutable res = Unchecked.defaultof<_>
    let ok = memoize_namespace_tab.TryGetValue(nm,&res)
    if ok then res else
    let x = split_namespace_aux nm
    (memoize_namespace_tab.[nm] <- x; x)

let split_type_name (nm:string) = 
    match nm.LastIndexOf '.' with
    | -1 -> [],nm
    | idx -> 
        let s1,s2 = split_name_at nm idx
        split_namespace s1,s2


let mk_empty_gparams = ([]: ILGenericParameterDefs)
let mk_empty_gactuals = ([]: ILGenericArgs)


type ILType with
    member x.TypeSpec =
      match x with 
      | Type_boxed tr | Type_value tr -> tr
      | _ -> failwith "tspec_of_typ"
    member x.Boxity =
      match x with 
      | Type_boxed _ -> AsObject
      | Type_value _ -> AsValue
      | _ -> failwith "boxity_of_typ"
    member x.TypeRef = 
      match x with 
      | Type_boxed tspec | Type_value tspec -> tspec.TypeRef
      | _ -> failwith "tref_of_typ"
    member x.IsNominal = 
      match x with 
      | Type_boxed _ | Type_value _ -> true
      | _ -> false
    member x.GenericArgs =
      match x with 
      | Type_boxed tspec | Type_value tspec -> tspec.GenericArgs
      | _ -> mk_empty_gactuals
    member x.IsTyvar =
      match x with 
      | Type_tyvar _ -> true | _ -> false

// --------------------------------------------------------------------
// Make ILTypeRefs etc.
// -------------------------------------------------------------------- 

let mk_tspec (tref,inst) =  ILTypeSpec.Create(tref, inst)
let mk_typ boxed tspec = 
  match boxed with AsObject -> Type_boxed tspec | _ -> Type_value tspec

// -------------------------------------------------------------------- 
// Rescoping
// -------------------------------------------------------------------- 

let qrescope_scoref scoref scoref_old = 
  match scoref,scoref_old with 
  | _,ScopeRef_local -> Some scoref
  | ScopeRef_local,_ -> None
  | _,ScopeRef_module _ -> Some scoref
  | ScopeRef_module _,_ -> None
  | _ -> None

let rescope_scoref x y = match qrescope_scoref x y with Some x -> x | None -> y
