// (c) Microsoft Corporation. All rights reserved


module internal Microsoft.FSharp.Metadata.Reader.Internal.Pickle

open System.Collections.Generic 
open Microsoft.FSharp.Metadata.Reader.Internal
open Microsoft.FSharp.Metadata.Reader.Internal.AbstractIL.IL
open Microsoft.FSharp.Metadata.Reader.Internal.Tast
open Microsoft.FSharp.Metadata.Reader.Internal.Prelude

let ffailwith fileName str = 
    let msg = FSComp.SR.pickleErrorReadingWritingMetadata(fileName, str)
    System.Diagnostics.Debug.Assert(false, msg)
    failwith msg
    

// Fixup pickled data w.r.t. a set of CCU thunks indexed by name
[<NoEquality; NoComparison>]
type PickledDataWithReferences<'rawData> = 
    { /// The data that uses a collection of CcuThunks internally
      RawData: 'rawData; 
      /// The assumptions that need to be fixed up
      FixupThunks: list<CcuThunk> } 

    member x.Fixup loader =
        x.FixupThunks |> List.iter (fun reqd -> reqd.Fixup(loader reqd.AssemblyName)) ;
        x.RawData

    /// Like Fixup but loader may return None, in which case there is no fixup.
    member x.OptionalFixup loader =
        x.FixupThunks 
        |> List.iter(fun reqd->
            match loader reqd.AssemblyName with 
            | Some(loaded) -> reqd.Fixup(loaded)
            | None -> reqd.FixupOrphaned() );
        x.RawData
    

//---------------------------------------------------------------------------
// Basic pickle/unpickle state
//---------------------------------------------------------------------------

[<NoEquality; NoComparison>]
type Table<'T> = 
    { name: string;
      tbl: Dictionary<'T, int>;
      mutable rows: ResizeArray<'T>;
      mutable count: int }

let inline new_tbl n = 
  { name = n;
    tbl = new System.Collections.Generic.Dictionary<_,_>(1000, HashIdentity.Structural);
    rows= new ResizeArray<_>(1000);
    count=0; }

let get_tbl tbl = Seq.toArray tbl.rows
let tbl_size tbl = tbl.rows.Count

let add_entry tbl x =
    let n = tbl.count
    tbl.count <- tbl.count + 1;
    tbl.tbl.[x] <- n;
    tbl.rows.Add(x);
    n

let find_or_add_entry tbl x =
    let mutable res = Unchecked.defaultof<_>
    let ok = tbl.tbl.TryGetValue(x,&res)
    if ok then res else add_entry tbl x

[<NoEquality; NoComparison>]
type InputTable<'T> = 
    { itbl_name: string;
      itbl_rows: 'T array }

let new_itbl n r = { itbl_name=n; itbl_rows=r }

[<NoEquality; NoComparison>]
type ObservableNodeInMap<'Data,'Node> = 
    | ObservableNodeInMap of ('Node -> 'Data -> unit) * ('Node -> bool) * string * 'Node array 
    member x.Get(n:int) = let (ObservableNodeInMap(_,_,_,arr)) = x in arr.[n]

let new_osgn_inmap mk lnk isLinked nm n = ObservableNodeInMap (lnk,isLinked,nm, Array.init n (fun _i -> mk() ))

[<NoEquality; NoComparison>]
type ReaderState = 
  { is: ByteStream; 
    iilscope: ILScopeRef;
    iccus: InputTable<CcuThunk>; 
    itycons: ObservableNodeInMap<EntityData,Tycon>;  
    itypars: ObservableNodeInMap<TyparData,Typar>; 
    ivals: ObservableNodeInMap<ValData,Val>;
    istrings: InputTable<string>;
    ipubpaths: InputTable<PublicPath>; 
    inlerefs: InputTable<NonLocalEntityRef>; 
    isimpletyps: InputTable<typ>;
    ifile: string;
  }

let ufailwith st str = ffailwith st.ifile str

//---------------------------------------------------------------------------
// Basic pickle/unpickle operations
//---------------------------------------------------------------------------
 
let u_byte st = Bytestream.read_byte st.is

type unpickler<'T> = ReaderState -> 'T

let u_bool st = let b = u_byte st in (b = 1) 



let prim_u_int32 st = 
    let b0 =  (u_byte st)
    let b1 =  (u_byte st)
    let b2 =  (u_byte st)
    let b3 =  (u_byte st)
    b0 ||| (b1 <<< 8) ||| (b2 <<< 16) ||| (b3 <<< 24)

let u_int32 st = 
    let b0 = u_byte st
    if b0 <= 0x7F then b0 
    else if b0 <= 0xbf then 
        let b0 = b0 &&& 0x7F
        let b1 = (u_byte st)
        (b0 <<< 8) ||| b1
    else  
        assert(b0 = 0xFF);
        prim_u_int32 st

let u_bytes st = 
    let n =  (u_int32 st)
    Bytestream.read_bytes st.is n

let u_prim_string st = 
    let len =  (u_int32 st)
    Bytestream.read_utf8_bytes_as_string st.is len

let u_int st = u_int32 st
let u_int8 st = sbyte (u_int32 st)
let u_uint8 st = byte (u_byte st)
let u_int16 st = int16 (u_int32 st)
let u_uint16 st = uint16 (u_int32 st)
let u_uint32 st = uint32 (u_int32 st)
let u_int64 st = 
    let b1 = (int64 (u_int32 st)) &&& 0xFFFFFFFFL
    let b2 = int64 (u_int32 st)
    b1 ||| (b2 <<< 32)

let u_uint64 st = uint64 (u_int64 st)
let float32_of_bits (x:int32) = System.BitConverter.ToSingle(System.BitConverter.GetBytes(x),0)
let float_of_bits (x:int64) = System.BitConverter.Int64BitsToDouble(x)

let u_single st = float32_of_bits (u_int32 st)
let u_double st = float_of_bits (u_int64 st)

let u_ieee64 st = float_of_bits (u_int64 st)

let u_char st = char (int32 (u_uint16 st))
let u_space n st = 
    for i = 0 to n - 1 do 
        u_byte st |> ignore
        


let inline  u_tup2 p1 p2 (st:ReaderState) = let a = p1 st in let b = p2 st in (a,b)
let inline  u_tup3 p1 p2 p3 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in (a,b,c)
let inline u_tup4 p1 p2 p3 p4 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in (a,b,c,d)
let inline u_tup5 p1 p2 p3 p4 p5 (st:ReaderState) =
  let a = p1 st 
  let b = p2 st 
  let c = p3 st 
  let d = p4 st 
  let e = p5 st 
  (a,b,c,d,e)
let inline u_tup6 p1 p2 p3 p4 p5 p6 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in (a,b,c,d,e,f)
let inline u_tup7 p1 p2 p3 p4 p5 p6 p7 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in let x7 = p7 st in (a,b,c,d,e,f,x7)
let inline u_tup8 p1 p2 p3 p4 p5 p6 p7 p8 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in  (a,b,c,d,e,f,x7,x8)
let inline u_tup9 p1 p2 p3 p4 p5 p6 p7 p8 p9 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in let x9 = p9 st in (a,b,c,d,e,f,x7,x8,x9)
let inline u_tup10 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in (a,b,c,d,e,f,x7,x8,x9,x10)
let inline u_tup11 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in (a,b,c,d,e,f,x7,x8,x9,x10,x11)
let inline u_tup12 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in
  (a,b,c,d,e,f,x7,x8,x9,x10,x11,x12)
let inline u_tup13 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  (a,b,c,d,e,f,x7,x8,x9,x10,x11,x12,x13)
let inline u_tup14 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in
  (a,b,c,d,e,f,x7,x8,x9,x10,x11,x12,x13,x14)
let inline u_tup15 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 p15 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in let x15 = p15 st in
  (a,b,c,d,e,f,x7,x8,x9,x10,x11,x12,x13,x14,x15)

let inline u_tup16 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 p15 p16 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in let x15 = p15 st in let x16 = p16 st in
  (a,b,c,d,e,f,x7,x8,x9,x10,x11,x12,x13,x14,x15,x16)

let inline u_tup17 p1 p2 p3 p4 p5 p6 p7 p8 p9 p10 p11 p12 p13 p14 p15 p16 p17 (st:ReaderState) =
  let a = p1 st in let b = p2 st in let c = p3 st in let d = p4 st in
  let e = p5 st in let f = p6 st in let x7 = p7 st in let x8 = p8 st in
  let x9 = p9 st in let x10 = p10 st in let x11 = p11 st in let x12 = p12 st in let x13 = p13 st in
  let x14 = p14 st in let x15 = p15 st in let x16 = p16 st in let x17 = p17 st in
  (a,b,c,d,e,f,x7,x8,x9,x10,x11,x12,x13,x14,x15,x16,x17)


//---------------------------------------------------------------------------
// Pickle/unpickle operations for observably shared graph nodes
//---------------------------------------------------------------------------

// exception Nope

// ctxt is for debugging

let u_osgn_ref (ObservableNodeInMap(_lnk,_isLinked,nm,arr)) st = 
    let n = u_int st
    if n < 0 || n >= Array.length arr then ufailwith st ("u_osgn_ref: out of range, table = "+nm+", n = "+string n); 
    arr.[n]

let u_osgn_decl (ObservableNodeInMap(lnk,_isLinked,_nm,arr)) u st = 
    let idx,data = u_tup2 u_int u st
    //   dprintf "unpickling osgn %d in table %s\n" idx nm; 
    let res = arr.[idx]
    lnk res data;
    res

//---------------------------------------------------------------------------
// Pickle/unpickle operations for interned nodes 
//---------------------------------------------------------------------------

let encode_uniq tbl key = find_or_add_entry tbl key
let lookup_uniq st tbl n = 
    let arr = tbl.itbl_rows
    if n < 0 || n >= Array.length arr then ufailwith st ("lookup_uniq in table "+tbl.itbl_name+" out of range, n = "+string n+ ", sizeof(tab) = " + string (Array.length arr)); 
    arr.[n]

//---------------------------------------------------------------------------
// Pickle/unpickle arrays and lists. For lists use the same binary format as arrays so we can switch
// between internal representations relatively easily
//------------------------------------------------------------------------- 
 
let u_array f st =
    let n = u_int st
    let res = Array.zeroCreate n
    for i = 0 to n-1 do
        res.[i] <- f st
    res

let u_list f st = Array.toList (u_array f st)

let u_FlatList f st = u_list f st // new FlatList<_> (u_array f st)

let u_array_revi f st =
    let n = u_int st
    let res = Array.zeroCreate n
    for i = 0 to n-1 do
        res.[i] <- f st (n-1-i) 
    res

// Mark up default constraints with a priority in reverse order: last gets 0 etc. See comment on TTyparDefaultsToType 
let u_list_revi f st = Array.toList (u_array_revi f st)
 
 
let u_wrap (f: 'U -> 'T) (u : 'U unpickler) : 'T unpickler = (fun st -> f (u st))

let u_option f st = 
    let tag = u_byte st
    match tag with
    | 0 -> None
    | 1 -> Some (f st)
    | n -> ufailwith st ("u_option: found number " + string n)

let u_lazy u st = 

    // Read the number of bytes in the record
    let len         = prim_u_int32 st // fixupPos1
    // These are the ranges of OSGN nodes defined within the lazily read portion of the graph
    let otyconsIdx1 = prim_u_int32 st // fixupPos2
    let otyconsIdx2 = prim_u_int32 st // fixupPos3
    let otyparsIdx1 = prim_u_int32 st // fixupPos4
    let otyparsIdx2 = prim_u_int32 st // fixupPos5
    let ovalsIdx1   = prim_u_int32 st // fixupPos6
    let ovalsIdx2   = prim_u_int32 st // fixupPos7

    ignore (len, otyconsIdx1, otyconsIdx2, otyparsIdx1, otyparsIdx2, ovalsIdx1, ovalsIdx2)
    Lazy.CreateFromValue(u st)

let u_hole () = 
    let h = ref (None : 'T unpickler option)
    (fun f -> h := Some f),(fun st -> match !h with Some f -> f st | None -> ufailwith st "u_hole: unfilled hole")

//---------------------------------------------------------------------------
// Pickle/unpickle F# interface data 
//---------------------------------------------------------------------------

// Strings 
// A huge number of these occur in pickled F# data, so make them unique 
let encode_string stringTab x = encode_uniq stringTab x
let decode_string x = x
let lookup_string st stringTab x = lookup_uniq st stringTab x
let u_encoded_string = u_prim_string
let u_string st   = lookup_uniq st st.istrings (u_int st)
let u_strings = u_list u_string
let u_ints = u_list u_int

// CCU References 
// A huge number of these occur in pickled F# data, so make them unique 
let encode_ccuref ccuTab (x:CcuThunk) = encode_uniq ccuTab x.AssemblyName 
let decode_ccuref x = x
let lookup_ccuref st ccuTab x = lookup_uniq st ccuTab x
let u_encoded_ccuref st = 
    match u_byte st with 
    | 0 -> u_prim_string st
    | n -> ufailwith st ("u_encoded_ccuref: found number " + string n)
let u_ccuref st   = lookup_uniq st st.iccus (u_int st)

// References to public items in this module 
// A huge number of these occur in pickled F# data, so make them unique 
let decode_pubpath st stringTab a = PubPath(Array.map (lookup_string st stringTab) a)
let lookup_pubpath st pubpathTab x = lookup_uniq st pubpathTab x
let u_encoded_pubpath = u_array u_int
let u_pubpath st = lookup_uniq st st.ipubpaths (u_int st)

// References to other modules 
// A huge number of these occur in pickled F# data, so make them unique 
let decode_nleref st ccuTab stringTab (a,b) = mk_nleref (lookup_ccuref st ccuTab a) (Array.map (lookup_string st stringTab) b)
let lookup_nleref st nlerefTab x = lookup_uniq st nlerefTab x
let u_encoded_nleref = u_tup2 u_int (u_array u_int)
let u_nleref st = lookup_uniq st st.inlerefs (u_int st)

// Simple types are types like "int", represented as TType(Ref_nonlocal(...,"int"),[]). 
// A huge number of these occur in pickled F# data, so make them unique. 
let decode_simpletyp st _ccuTab _stringTab nlerefTab a = TType_app(ERef_nonlocal (lookup_nleref st nlerefTab a),[])
let lookup_simpletyp st simpletypTab x = lookup_uniq st simpletypTab x
let u_encoded_simpletyp st = u_int  st
let u_simpletyp st = lookup_uniq st st.isimpletyps (u_int st)

type sizes = int * int * int 
    
let unpickle_obj_with_dangling_ccus file ilscope u (phase2bytes:byte[]) =
    let st2 = 
       { is = Bytestream.of_bytes phase2bytes 0 phase2bytes.Length; 
         iilscope= ilscope;
         iccus= new_itbl "iccus (fake)" [| |]; 
         itycons= new_osgn_inmap Tycon.NewUnlinked (fun osgn tg -> osgn.Link(tg)) (fun osgn -> osgn.IsLinked) "itycons" 0; 
         itypars= new_osgn_inmap Typar.NewUnlinked (fun osgn tg -> osgn.Link(tg)) (fun osgn -> osgn.IsLinked) "itypars" 0; 
         ivals  = new_osgn_inmap Val.NewUnlinked   (fun osgn tg -> osgn.Link(tg)) (fun osgn -> osgn.IsLinked) "ivals" 0;
         istrings = new_itbl "istrings (fake)" [| |]; 
         inlerefs = new_itbl "inlerefs (fake)" [| |]; 
         ipubpaths = new_itbl "ipubpaths (fake)" [| |]; 
         isimpletyps = new_itbl "isimpletyps (fake)" [| |]; 
         ifile=file }
    let phase2data = 
        u_tup7
           (u_array u_encoded_ccuref) 
           (u_tup3 u_int u_int u_int) 
           (u_array u_encoded_string) 
           (u_array u_encoded_pubpath) 
           (u_array u_encoded_nleref) 
           (u_array u_encoded_simpletyp) 
           u_bytes st2
    let ccuNameTab,sizes,stringTab,pubpathTab,nlerefTab,simpletypTab,phase1bytes = phase2data
    let ccuTab       = new_itbl "iccus"       (Array.map (CcuThunk.CreateDelayed) ccuNameTab)
    let stringTab    = new_itbl "istrings"    (Array.map decode_string stringTab)
    let pubpathTab   = new_itbl "ipubpaths"   (Array.map (decode_pubpath st2 stringTab) pubpathTab)
    let nlerefTab    = new_itbl "inlerefs"    (Array.map (decode_nleref st2 ccuTab stringTab) nlerefTab)
    let simpletypTab = new_itbl "isimpletyps" (Array.map (decode_simpletyp st2 ccuTab stringTab nlerefTab) simpletypTab)
    let ((ntycons,ntypars,nvals) : sizes) = sizes
    let data = 
        let st1 = 
           { is = Bytestream.of_bytes phase1bytes 0 phase1bytes.Length; 
             iccus=  ccuTab; 
             iilscope= ilscope;
             itycons= new_osgn_inmap Tycon.NewUnlinked (fun osgn tg -> osgn.Link(tg)) (fun osgn -> osgn.IsLinked)  "itycons" ntycons; 
             itypars= new_osgn_inmap Typar.NewUnlinked (fun osgn tg -> osgn.Link(tg)) (fun osgn -> osgn.IsLinked) "itypars" ntypars; 
             ivals=   new_osgn_inmap Val.NewUnlinked   (fun osgn tg -> osgn.Link(tg)) (fun osgn -> osgn.IsLinked) "ivals" nvals;
             istrings = stringTab;
             ipubpaths = pubpathTab;
             inlerefs = nlerefTab;
             isimpletyps = simpletypTab;
             ifile=file }
        let res = u st1
        res

    {RawData=data; FixupThunks=Array.toList ccuTab.itbl_rows }
    

//=========================================================================
// PART II *)
//=========================================================================

//---------------------------------------------------------------------------
// Pickle/unpickle for Abstract IL data, up to IL instructions 
//---------------------------------------------------------------------------

let u_ILPublicKey st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_bytes st |> PublicKey 
    | 1 -> u_bytes st |> PublicKeyToken 
    | _ -> ufailwith st "u_ILPublicKey"

let u_ILVersion st = u_tup4 u_uint16 u_uint16 u_uint16 u_uint16 st

let u_ILModuleRef st = 
    let (a,b,c) = u_tup3 u_string u_bool (u_option u_bytes) st
    ILModuleRef.Create(a, b, c)

let u_ILAssemblyRef st =
    let tag = u_byte st
    match tag with
    | 0 -> 
        let a,b,c,d,e,f = u_tup6 u_string (u_option u_bytes) (u_option u_ILPublicKey) u_bool (u_option u_ILVersion) (u_option u_string) st
        ILAssemblyRef.Create(a, b, c, d, e, f)
    | _ -> ufailwith st "u_ILAssemblyRef"

// IL scope references are rescoped as they are unpickled.  This means 
// the pickler accepts IL fragments containing ScopeRef_local and adjusts them 
// to be absolute scope references.
let u_ILScopeRef st = 
    let res = 
        let tag = u_byte st
        match tag with
        | 0 -> ScopeRef_local
        | 1 -> u_ILModuleRef st |> ScopeRef_module 
        | 2 -> u_ILAssemblyRef st |> ScopeRef_assembly 
        | _ -> ufailwith st "u_ILScopeRef"  
    let res = rescope_scoref st.iilscope res 
    res

let u_ILBasicCallConv st = 
    match u_byte st with 
    | 0 -> CC_default 
    | 1 -> CC_cdecl  
    | 2 -> CC_stdcall 
    | 3 -> CC_thiscall 
    | 4 -> CC_fastcall 
    | 5 -> CC_vararg
    | _ -> ufailwith st "u_ILBasicCallConv"

let u_ILHasThis st = 
    match u_byte st with 
    | 0 -> CC_instance 
    | 1 -> CC_instance_explicit 
    | 2 -> CC_static 
    | _ -> ufailwith st "u_ILHasThis"

let u_ILCallConv st = let a,b = u_tup2 u_ILHasThis u_ILBasicCallConv st in Callconv(a,b)
let u_ILTypeRef st = let a,b,c = u_tup3 u_ILScopeRef u_strings u_string st in ILTypeRef.Create(a, b, c) 
let u_ILArrayShape = u_wrap (fun x -> ILArrayShape x) (u_list (u_tup2 (u_option u_int32) (u_option u_int32)))


let rec u_ILType st = 
    let tag = u_byte st
    match tag with
    | 0 -> Type_void
    | 1 -> u_tup2 u_ILArrayShape u_ILType  st     |> Type_array 
    | 2 -> u_ILTypeSpec st                        |> Type_value 
    | 3 -> u_ILTypeSpec st                        |> Type_boxed 
    | 4 -> u_ILType st                            |> Type_ptr 
    | 5 -> u_ILType st                            |> Type_byref
    | 6 -> u_ILCallSig st                         |> Type_fptr 
    | 7 -> u_uint16 st                            |> Type_tyvar 
    | 8 -> u_tup3 u_bool u_ILTypeRef u_ILType  st |> Type_modified 
    | _ -> ufailwith st "u_ILType"
and u_ILTypes st = u_list u_ILType st
and u_ILCallSig = u_wrap (fun (a,b,c) -> {callsigCallconv=a; callsigArgs=b; callsigReturn=c}) (u_tup3 u_ILCallConv u_ILTypes u_ILType)
and u_ILTypeSpec st = let a,b = u_tup2 u_ILTypeRef u_ILTypes st in ILTypeSpec.Create(a,b)



let u_ILMethodRef st = 
    let x1,x2,x3,x4,x5,x6 = u_tup6 u_ILTypeRef u_ILCallConv u_int u_string u_ILTypes u_ILType st
    ILMethodRef.Create(x1,x2,x4,x3,x5,x6)

let u_ILFieldRef st = 
    let x1,x2,x3 = u_tup3 u_ILTypeRef u_string u_ILType st 
    {frefParent=x1;frefName=x2;frefType=x3}

let u_ILMethodSpec st = 
    let x1,x2,x3 = u_tup3 u_ILMethodRef u_ILType u_ILTypes st 
    ILMethodSpec.Create(x2,x1,x3)

let u_ILFieldSpec st = 
    let x1,x2 = u_tup2 u_ILFieldRef u_ILType st 
    {fspecFieldRef=x1;fspecEnclosingType=x2}

let u_ILBasicType st = 
    match u_int st with  
    | 0 -> DT_R 
    | 1 -> DT_I1 
    | 2 -> DT_U1 
    | 3 -> DT_I2 
    | 4 -> DT_U2 
    | 5 -> DT_I4 
    | 6 -> DT_U4 
    | 7 -> DT_I8 
    | 8 -> DT_U8 
    | 9 -> DT_R4 
    | 10 -> DT_R8 
    | 11 -> DT_I 
    | 12 -> DT_U 
    | 13 -> DT_REF 
    | _ -> ufailwith st "u_ILBasicType"
    
let u_ILVolatility st = (match u_int st with  0 -> Volatile | 1 -> Nonvolatile | _ -> ufailwith st "u_ILVolatility" )
let u_ILReadonly   st = (match u_int st with  0 -> ReadonlyAddress | 1 -> NormalAddress | _ -> ufailwith st "u_ILReadonly" )
  
let itag_nop           = 0 
let itag_ldarg         = 1
let itag_ldnull        = 2 
let itag_ilzero        = 3
let itag_call          = 4 
let itag_add           = 5
let itag_sub           = 6 
let itag_mul           = 7
let itag_div           = 8 
let itag_div_un        = 9 
let itag_rem           = 10 
let itag_rem_un        = 11 
let itag_and           = 12 
let itag_or            = 13 
let itag_xor           = 14 
let itag_shl           = 15 
let itag_shr           = 16 
let itag_shr_un        = 17 
let itag_neg           = 18 
let itag_not           = 19 
let itag_conv          = 20
let itag_conv_un       = 21 
let itag_conv_ovf      = 22
let itag_conv_ovf_un   = 23
let itag_callvirt      = 24 
let itag_ldobj         = 25 
let itag_ldstr         = 26 
let itag_castclass     = 27 
let itag_isinst        = 28 
let itag_unbox         = 29 
let itag_throw         = 30 
let itag_ldfld         = 31 
let itag_ldflda        = 32 
let itag_stfld         = 33 
let itag_ldsfld        = 34 
let itag_ldsflda       = 35 
let itag_stsfld        = 36 
let itag_stobj         = 37 
let itag_box           = 38 
let itag_newarr        = 39 
let itag_ldlen         = 40 
let itag_ldelema       = 41 
let itag_ckfinite      = 42 
let itag_ldtoken       = 43 
let itag_add_ovf       = 44 
let itag_add_ovf_un    = 45 
let itag_mul_ovf       = 46 
let itag_mul_ovf_un    = 47 
let itag_sub_ovf       = 48 
let itag_sub_ovf_un    = 49 
let itag_ceq           = 50
let itag_cgt           = 51
let itag_cgt_un        = 52
let itag_clt           = 53
let itag_clt_un        = 54
let itag_ldvirtftn     = 55 
let itag_localloc      = 56 
let itag_rethrow       = 57 
let itag_sizeof        = 58
let itag_ldelem_any    = 59
let itag_stelem_any    = 60
let itag_unbox_any     = 61
let itag_ldlen_multi   = 62

let simple_instrs = 
    [ itag_add,        I_arith AI_add;
      itag_add_ovf,    I_arith AI_add_ovf;
      itag_add_ovf_un, I_arith AI_add_ovf_un;
      itag_and,        I_arith AI_and;  
      itag_div,        I_arith AI_div; 
      itag_div_un,     I_arith AI_div_un;
      itag_ceq,        I_arith AI_ceq;  
      itag_cgt,        I_arith AI_cgt ;
      itag_cgt_un,     I_arith AI_cgt_un;
      itag_clt,        I_arith AI_clt;
      itag_clt_un,     I_arith AI_clt_un;
      itag_mul,        I_arith AI_mul  ;
      itag_mul_ovf,    I_arith AI_mul_ovf;
      itag_mul_ovf_un, I_arith AI_mul_ovf_un;
      itag_rem,        I_arith AI_rem  ;
      itag_rem_un,     I_arith AI_rem_un ; 
      itag_shl,        I_arith AI_shl ; 
      itag_shr,        I_arith AI_shr ; 
      itag_shr_un,     I_arith AI_shr_un;
      itag_sub,        I_arith AI_sub  ;
      itag_sub_ovf,    I_arith AI_sub_ovf;
      itag_sub_ovf_un, I_arith AI_sub_ovf_un; 
      itag_xor,        I_arith AI_xor;  
      itag_or,         I_arith AI_or;     
      itag_neg,        I_arith AI_neg;     
      itag_not,        I_arith AI_not;     
      itag_ldnull,     I_arith AI_ldnull;   
      itag_ckfinite,   I_arith AI_ckfinite;
      itag_nop,        I_arith AI_nop;
      itag_localloc,   I_localloc;
      itag_throw,      I_throw;
      itag_ldlen,      I_ldlen;
      itag_rethrow,    I_rethrow;    ]

let encode_table = Dictionary<_,_>(300, HashIdentity.Structural)
let _ = List.iter (fun (icode,i) -> encode_table.[i] <- icode) simple_instrs
let encode_instr si = encode_table.[si]
let is_noarg_instr s = encode_table.ContainsKey s

let decoders = 
   [ itag_ldarg,       u_uint16                            >> I_ldarg;
     itag_call,        u_ILMethodSpec                      >> (fun a -> I_call (Normalcall,a,None));
     itag_callvirt,    u_ILMethodSpec                      >> (fun a -> I_callvirt (Normalcall,a,None));
     itag_ldvirtftn,   u_ILMethodSpec                      >> I_ldvirtftn;
     itag_conv,        u_ILBasicType                       >> (fun a -> I_arith (AI_conv a));
     itag_conv_ovf,    u_ILBasicType                       >> (fun a -> I_arith (AI_conv_ovf a));
     itag_conv_ovf_un, u_ILBasicType                       >> (fun a -> I_arith (AI_conv_ovf_un a));
     itag_ldfld,       u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (b,c) -> I_ldfld (Aligned,b,c));
     itag_ldflda,      u_ILFieldSpec                       >> I_ldflda;
     itag_ldsfld,      u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (a,b) -> I_ldsfld (a,b));
     itag_ldsflda,     u_ILFieldSpec                       >> I_ldsflda;
     itag_stfld,       u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (b,c) -> I_stfld (Aligned,b,c));
     itag_stsfld,      u_tup2 u_ILVolatility u_ILFieldSpec >> (fun (a,b) -> I_stsfld (a,b));
     itag_ldtoken,     u_ILType                            >> (fun a -> I_ldtoken (Token_type a));
     itag_ldstr,       u_string                            >> I_ldstr;
     itag_box,         u_ILType                            >> I_box;
     itag_unbox,       u_ILType                            >> I_unbox;
     itag_unbox_any,   u_ILType                            >> I_unbox_any;
     itag_newarr,      u_tup2 u_ILArrayShape u_ILType      >> (fun (a,b) -> I_newarr(a,b));
     itag_stelem_any,  u_tup2 u_ILArrayShape u_ILType      >> (fun (a,b) -> I_stelem_any(a,b));
     itag_ldelem_any,  u_tup2 u_ILArrayShape u_ILType      >> (fun (a,b) -> I_ldelem_any(a,b));
     itag_ldelema,     u_tup3 u_ILReadonly u_ILArrayShape u_ILType >> (fun (a,b,c) -> I_ldelema(a,b,c));
     itag_castclass,   u_ILType                            >> I_castclass;
     itag_isinst,      u_ILType                            >> I_isinst;
     itag_ldobj,       u_ILType                            >> (fun c -> I_ldobj (Aligned,Nonvolatile,c));
     itag_stobj,       u_ILType                            >> (fun c -> I_stobj (Aligned,Nonvolatile,c));
     itag_sizeof,      u_ILType                            >> I_sizeof;
     itag_ldlen_multi, u_tup2 u_int32 u_int32              >> (fun (a,b) -> EI_ldlen_multi (a,b));
     itag_ilzero,      u_ILType                            >> EI_ilzero; ] 

let decode_tab = 
    let tab = Array.init 256 (fun n -> (fun st -> ufailwith st ("no decoder for instruction "+string n)))
    let add_instr (icode,f) =  tab.[icode] <- f
    List.iter add_instr decoders;
    List.iter (fun (icode,mk) -> add_instr (icode,(fun _ -> mk))) simple_instrs;
    tab


let u_ILInstr st = 
    let n = u_byte st
    decode_tab.[n] st

//---------------------------------------------------------------------------
// Pickle/unpickle for F# types and module signatures
//---------------------------------------------------------------------------

let u_qlist uv = u_wrap QueueList.ofList (u_list uv)

let u_pos st = let a = u_int st in let b = u_int st in mk_pos a b
let u_range st = let a = u_string st in let b = u_pos st in let c = u_pos st in mk_range a b c

// Most ranges (e.g. on optimization expressions) can be elided from stored data 
let u_dummy_range : range unpickler = fun _st -> range0
let u_ident st = let a = u_string st in let b = u_range st in ident(a,b)
let u_xmldoc st = XmlDoc (u_array u_string st)

let u_local_item_ref tab st = u_osgn_ref tab st

let u_tcref st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_local_item_ref st.itycons  st |> ERef_local 
    | 1 -> u_nleref                     st |> ERef_nonlocal 
    | _ -> ufailwith st "u_item_ref"
    
let u_ucref st  = let a,b = u_tup2 u_tcref u_string st in UCRef(a,b)

let u_rfref st = let a,b = u_tup2 u_tcref u_string st in RFRef(a,b)

let u_tpref st = u_local_item_ref st.itypars st
let fill_u_typ,u_typ = u_hole()
let u_typs = (u_list u_typ)
let fill_u_attribs,u_attribs = u_hole()

let u_nonlocal_val_ref st = 
    let a = u_tcref st 
    let b1 = u_option u_string st
    let b2 = u_bool st
    let b3 = u_string st
    let c = u_int st
    let d = u_option u_typ st
    {EnclosingEntity = a; ItemKey=ValLinkageFullKey({ MemberParentMangledName=b1; MemberIsOverride=b2;LogicalName=b3; TotalArgCount=c }, d) }
  
let u_vref st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_local_item_ref st.ivals st |> (fun x -> VRef_local x)
    | 1 -> u_nonlocal_val_ref st |> (fun x -> VRef_nonlocal x)
    | _ -> ufailwith st "u_item_ref"
    
let u_vrefs = u_list u_vref 



let u_kind st =
    match u_byte st with
    | 0 -> KindType
    | 1 -> KindMeasure
    | _ -> ufailwith st "u_kind"

let u_member_kind st = 
    match u_byte st with 
    | 0 -> MemberKindMember 
    | 1 -> MemberKindPropertyGet  
    | 2 -> MemberKindPropertySet 
    | 3 -> MemberKindConstructor
    | 4 -> MemberKindClassConstructor
    | _ -> ufailwith st "u_member_kind"

let u_MemberFlags st = 
    let x2,_x3UnusedBoolInFormat,x4,x5,x6,x7 = u_tup6 u_bool u_bool u_bool u_bool u_bool u_member_kind st
    { MemberIsInstance=x2;
      MemberIsDispatchSlot=x4;
      MemberIsOverrideOrExplicitImpl=x5;
      MemberIsFinal=x6;
      MemberKind=x7}

// We have to store trait solutions since they can occur in optimization data
let u_trait_sln st = 
    let tag = u_byte st
    match tag with 
    | 0 -> 
        let (a,b,c,d) = u_tup4 u_typ (u_option u_ILTypeRef) u_ILMethodRef u_typs st
        ILMethSln(a,b,c,d) 
    | 1 -> 
        let (a,b,c) = u_tup3 u_typ u_vref u_typs st
        FSMethSln(a,b,c)
    | 2 -> 
        BuiltInSln
    | _ -> ufailwith st "u_trait_sln" 

let u_trait st = 
    let a,b,c,d,e,f = u_tup6 u_typs u_string u_MemberFlags u_typs (u_option u_typ) (u_option u_trait_sln) st
    TTrait (a,b,c,d,e,ref f)

let rec u_measure_expr st =
    let tag = u_byte st
    match tag with
    | 0 -> let a = u_tcref st in MeasureCon a
    | 1 -> let a = u_measure_expr st in MeasureInv a
    | 2 -> let a,b = u_tup2 u_measure_expr u_measure_expr st in MeasureProd (a,b)
    | 3 -> let a = u_tpref st in MeasureVar a
    | 4 -> MeasureOne
    | _ -> ufailwith st "u_measure_expr"

let u_typar_constraint st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_typ  st             |> (fun a     _ -> TTyparCoercesToType (a,range0) )
    | 1 -> u_trait st            |> (fun a     _ -> TTyparMayResolveMemberConstraint(a,range0))
    | 2 -> u_typ st              |> (fun a  ridx -> TTyparDefaultsToType(ridx,a,range0))
    | 3 ->                          (fun       _ -> TTyparSupportsNull range0)
    | 4 ->                          (fun       _ -> TTyparIsNotNullableValueType range0)
    | 5 ->                          (fun       _ -> TTyparIsReferenceType range0)
    | 6 ->                          (fun       _ -> TTyparRequiresDefaultConstructor range0)
    | 7 -> u_typs st             |> (fun a     _ -> TTyparSimpleChoice(a,range0))
    | 8 -> u_typ  st             |> (fun a     _ -> TTyparIsEnum(a,range0))
    | 9 -> u_tup2 u_typ u_typ st |> (fun (a,b) _ -> TTyparIsDelegate(a,b,range0))
    | 10 ->                         (fun       _ -> TTyparSupportsComparison range0)
    | 11 ->                         (fun       _ -> TTyparSupportsEquality range0)
    | 12 ->                         (fun       _ -> TTyparIsUnmanaged range0)
    | _ -> ufailwith st "u_typar_constraint" 


let u_typar_constraints = (u_list_revi u_typar_constraint)


let u_typar_spec_data st = 
    let a,c,d,e,g = u_tup5 u_ident u_attribs u_int64 u_typar_constraints u_xmldoc st
    { typar_id=a; 
      typar_il_name=None;
      typar_stamp=newStamp();
      typar_attribs=c;
      typar_flags=TyparFlags(int32 d);
      typar_constraints=e;
      typar_solution=None;
      typar_xmldoc=g }

let u_typar_spec st = 
    u_osgn_decl st.itypars u_typar_spec_data st 

let u_typar_specs = (u_list u_typar_spec)


let _ = fill_u_typ (fun st ->
    let tag = u_byte st
    match tag with
    | 0 -> let l = u_typs st                               in TType_tuple l
    | 1 -> u_simpletyp st 
    | 2 -> let tc = u_tcref st in let tinst = u_typs st    in TType_app (tc,tinst)
    | 3 -> let d = u_typ st    in let r = u_typ st         in TType_fun (d,r)
    | 4 -> let r = u_tpref st                              in r.AsType
    | 5 -> let tps = u_typar_specs st in let r = u_typ st  in TType_forall (tps,r)
    | 6 -> let unt = u_measure_expr st                     in TType_measure unt
    | 7 -> let uc = u_ucref st in let tinst = u_typs st    in TType_ucase (uc,tinst)
    | _ -> ufailwith st "u_typ")
  

let fill_u_binds,u_binds = u_hole()
let fill_u_targets,u_targets = u_hole()
let fill_u_Exprs,u_Exprs = u_hole()
let fill_u_FlatExprs,u_FlatExprs = u_hole()
let fill_u_constraints,u_constraints = u_hole()
let fill_u_Vals,u_Vals = u_hole()
let fill_u_FlatVals,u_FlatVals = u_hole()

let u_TopArgInfo st = 
    let a = u_attribs st 
    let b = u_option u_ident st 
    match a,b with 
    | [],None -> TopValInfo.unnamedTopArg1 
    | _ -> { Attribs = a; Name = b } 

let u_TopTyparInfo st = 
    let a = u_ident st
    let b = u_kind st 
    TopTyparInfo(a,b)

let u_ValTopReprInfo st = 
    let a = u_list u_TopTyparInfo st
    let b = u_list (u_list u_TopArgInfo) st
    let c = u_TopArgInfo st
    TopValInfo (a,b,c)

let u_ranges st = u_option (u_tup2 u_range u_range) st

let u_istype st = 
    let tag = u_byte st
    match tag with
    | 0 -> FSharpModuleWithSuffix 
    | 1 -> FSharpModule  
    | 2 -> Namespace 
    | _ -> ufailwith st "u_istype"

let u_cpath  st = let a,b = u_tup2 u_ILScopeRef (u_list (u_tup2 u_string u_istype)) st in (CompPath(a,b))


let rec dummy x = x
and u_tycon_repr st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_rfield_table            st |> TRecdRepr 
    | 1 -> u_list u_unioncase_spec   st |> MakeUnionRepr
    | 2 -> u_ILType                  st |> TAsmRepr
    | 3 -> u_tycon_objmodel_data     st |> TFsObjModelRepr
    | 4 -> u_typ                     st |> TMeasureableRepr 
    | _ -> ufailwith st "u_tycon_repr"
  
and u_tycon_objmodel_data st = 
    let x1,x2,x3 = u_tup3 u_tycon_objmodel_kind u_vrefs u_rfield_table st
    {fsobjmodel_kind=x1; fsobjmodel_vslots=x2; fsobjmodel_rfields=x3 }
  
and u_unioncase_spec st = 
    let a,b,c,d,e,f,i = u_tup7 u_rfield_table u_typ u_string u_ident u_attribs u_string u_access st
    {ucase_rfields=a; ucase_rty=b; ucase_il_name=c; ucase_id=d; ucase_attribs=e;ucase_xmldoc=emptyXmlDoc;ucase_xmldocsig=f;ucase_access=i }
    
and u_exnc_spec_data st = u_entity_spec_data st 

and u_exnc_repr st =
    let tag = u_byte st
    match tag with
    | 0 -> u_tcref        st |> TExnAbbrevRepr
    | 1 -> u_ILTypeRef    st |> TExnAsmRepr
    | 2 -> u_rfield_table st |> TExnFresh
    | 3 -> TExnNone
    | _ -> ufailwith st "u_exnc_repr"
  
and u_exnc_spec st = u_tycon_spec st

and u_access st = 
    match u_list u_cpath st with 
    | [] -> taccessPublic // save unnecessary allocations 
    | res -> TAccess res

and u_recdfield_spec st = 
    let a,b,c1,c2,c2b,c3,d,e1,e2,f,g = 
        u_tup11 
            u_bool 
            u_bool 
            u_typ 
            u_bool 
            u_bool 
            (u_option u_const) 
            u_ident 
            u_attribs 
            u_attribs 
            u_string
            u_access 
            st
    { rfield_mutable=a;  
      rfield_volatile=b;  
      rfield_type=c1; 
      rfield_static=c2; 
      rfield_secret=c2b; 
      rfield_const=c3; 
      rfield_id=d; 
      rfield_pattribs=e1;
      rfield_fattribs=e2;
      rfield_xmldoc=emptyXmlDoc;
      rfield_xmldocsig=f; 
      rfield_access=g }

and u_rfield_table st = MakeRecdFieldsTable (u_list u_recdfield_spec st)

and u_entity_spec_data st = 
    let x1,x2a,x2b,x2c,x3,(x4a,x4b),x6,x7,x8,x9,x10,x10b,x11,x12,x13,x14,_space = 
       u_tup17
          u_typar_specs
          u_string
          (u_option u_string)
          u_range
          (u_option u_pubpath)
          (u_tup2 u_access u_access)
          u_attribs
          (u_option u_tycon_repr)
          (u_option u_typ) 
          u_tcaug 
          u_string 
          u_kind
          u_int64
          (u_option u_cpath )
          (u_lazy u_modul_typ) 
          u_exnc_repr 
          (u_space 1)
          st
    { entity_typars=LazyWithContext<_,_>.NotLazy x1;
      entity_stamp=newStamp();
      entity_logical_name=x2a;
      entity_compiled_name=x2b;
      entity_range=x2c;
      entity_pubpath=x3;
      entity_accessiblity=x4a;
      entity_tycon_repr_accessibility=x4b;
      entity_attribs=x6;
      entity_tycon_repr=x7;
      entity_tycon_abbrev=x8;
      entity_tycon_tcaug=x9;
      entity_xmldoc=emptyXmlDoc;
      entity_xmldocsig=x10;
      entity_kind=x10b;
      entity_flags=EntityFlags(x11);
      entity_cpath=x12;
      entity_modul_contents= x13;
      entity_exn_info=x14;
      entity_il_repr_cache=newCache();  } 

and u_tcaug st = 
    let a1,a2,a3,b2,c,d,e,g,_space = 
      u_tup9
        (u_option (u_tup2 u_vref u_vref))
        (u_option u_vref)
        (u_option (u_tup3 u_vref u_vref u_vref))
        (u_option (u_tup2 u_vref u_vref))
        (u_list (u_tup2 u_string u_vref))
        (u_list (u_tup3 u_typ u_bool u_dummy_range)) 
        (u_option u_typ)
        u_bool 
        (u_space 1)
        st 
    {tcaug_compare=a1; 
     tcaug_compare_withc=a2; 
     tcaug_hash_and_equals_withc=a3; 
     tcaug_equals=b2; 
     // only used for code generation and checking - hence don't care about the values when reading back in
     tcaug_hasObjectGetHashCode=false; 
     tcaug_adhoc_list= new ResizeArray<_> (List.map snd c); 
     tcaug_adhoc=NameMultiMap.ofList c; 
     tcaug_interfaces=d;
     tcaug_super=e;
     // pickled type definitions are always closed (i.e. no more intrinsic members allowed)
     tcaug_closed=true; 
     tcaug_abstract=g}
 
and u_tycon_spec st = 
    u_osgn_decl st.itycons u_entity_spec_data st 

and u_parentref st = 
    let tag = u_byte st
    match tag with
    | 0 -> ParentNone
    | 1 -> u_tcref st |> Parent 
    | _ -> ufailwith st "u_attribkind" 

and u_attribkind st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_ILMethodRef st |> ILAttrib 
    | 1 -> u_vref        st |> FSAttrib 
    | _ -> ufailwith st "u_attribkind" 

and u_attrib st : Attrib = 
    let a,b,c,d,e,f = u_tup6 u_tcref u_attribkind (u_list u_attrib_expr) (u_list u_attrib_arg) u_bool u_dummy_range st
    Attrib(a,b,c,d,e,f)

and u_attrib_expr st = 
    let a,b = u_tup2 u_expr u_expr st 
    AttribExpr(a,b)

and u_attrib_arg st  = 
    let a,b,c,d = u_tup4 u_string u_typ u_bool u_attrib_expr st 
    AttribNamedArg(a,b,c,d)

and u_member_info st = 
    let x2,x3,x4,x5 = u_tup4 u_tcref u_MemberFlags (u_list u_slotsig) u_bool st
    { ApparentParent=x2;
      MemberFlags=x3;
      ImplementedSlotSigs=x4;
      IsImplemented=x5  }

and u_tycon_objmodel_kind st = 
    let tag = u_byte st
    match tag with
    | 0 -> TTyconClass 
    | 1 -> TTyconInterface  
    | 2 -> TTyconStruct 
    | 3 -> u_slotsig st |> TTyconDelegate
    | 4 -> TTyconEnum 
    | _ -> ufailwith st "u_tycon_objmodel_kind"

and u_mustinline st = 
    match u_byte st with 
    | 0 -> PseudoValue 
    | 1 -> AlwaysInline  
    | 2 -> OptionalInline 
    | 3 -> NeverInline 
    | _ -> ufailwith st "u_mustinline"

and u_basethis st = 
    match u_byte st with 
    | 0 -> BaseVal 
    | 1 -> CtorThisVal  
    | 2 -> NormalVal 
    | 3 -> MemberThisVal
    | _ -> ufailwith st "u_basethis"

and u_vrefFlags st = 
    match u_byte st with 
    | 0 -> NormalValUse 
    | 1 -> CtorValUsedAsSuperInit
    | 2 -> CtorValUsedAsSelfInit
    | 3 -> PossibleConstrainedCall (u_typ st)
    | 4 -> VSlotDirectCall
    | _ -> ufailwith st "u_vrefFlags"

and u_ValData st =
    let x1,x1z,x1a,x2,x4,x8,x9,x10,x12,x13,x13b,x14,_space = 
      u_tup13
        u_string
        (u_option u_string)
        u_ranges
        u_typ 
        u_int64
        (u_option u_member_info) 
        u_attribs 
        (u_option u_ValTopReprInfo)
        u_string
        u_access
        u_parentref
        (u_option u_const) 
        (u_space 1) st
    { val_logical_name=x1;
      val_compiled_name=x1z;
      val_range=(match x1a with None -> range0 | Some(a,_) -> a);
      val_defn_range=(match x1a with None -> range0 | Some(_,b) -> b);
      val_type=x2;
      val_stamp=newStamp();
      val_flags=ValFlags(x4);
      val_defn = None;
      val_member_info=x8;
      val_attribs=x9;
      val_top_repr_info=x10;
      val_xmldoc=emptyXmlDoc;
      val_xmldocsig=x12;
      val_access=x13;
      val_actual_parent=x13b;
      val_const=x14;
    }

and u_Val st = u_osgn_decl st.ivals u_ValData st 


and u_modul_typ st = 
    let x1,x3,x5 = 
        u_tup3
          u_istype
          (u_qlist u_Val)
          (u_qlist u_tycon_spec) st
    ModuleOrNamespaceType(x1,x3,x5)


//---------------------------------------------------------------------------
// Pickle/unpickle for F# expressions (for optimization data)
//---------------------------------------------------------------------------

and u_const st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_bool st           |> TConst_bool  
    | 1 -> u_int8 st           |> TConst_sbyte 
    | 2 -> u_uint8 st          |> TConst_byte 
    | 3 -> u_int16 st          |> TConst_int16 
    | 4 -> u_uint16 st         |> TConst_uint16 
    | 5 -> u_int32 st          |> TConst_int32 
    | 6 -> u_uint32 st         |> TConst_uint32 
    | 7 -> u_int64 st          |> TConst_int64
    | 8 -> u_uint64 st         |> TConst_uint64
    | 9 -> u_int64 st          |> TConst_nativeint
    | 10 -> u_uint64 st        |> TConst_unativeint
    | 11 -> u_single st        |> TConst_float32
    | 12 -> u_int64 st         |> float_of_bits |> TConst_float
    | 13 -> u_char st          |> TConst_char 
    | 14 -> u_string st        |> TConst_string
    | 15 -> TConst_unit
    | 16 -> TConst_zero
    | 17 -> u_array u_int32 st |> (fun bits -> TConst_decimal (new System.Decimal(bits)))
    | _ -> ufailwith st "u_const" 


and u_dtree st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_tup4 u_expr (u_list u_dtree_case) (u_option u_dtree) u_dummy_range st |> TDSwitch 
    | 1 -> u_tup2 u_FlatExprs u_int                                             st |> TDSuccess
    | 2 -> u_tup2 u_bind u_dtree                                                st |> TDBind
    | _ -> ufailwith st "u_dtree" 

and u_dtree_case st = let a,b = u_tup2 u_dtree_discrim u_dtree st in (TCase(a,b)) 

and u_dtree_discrim st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_tup2 u_ucref u_typs st |> TTest_unionconstr 
    | 1 -> u_const st               |> TTest_const 
    | 2 ->                             TTest_isnull 
    | 3 -> u_tup2 u_typ u_typ st    |> TTest_isinst
    | 4 -> u_tup2 u_int u_typ st    |> TTest_array_length
    | _ -> ufailwith st "u_dtree_discrim" 

and u_target st = let a,b = u_tup2 u_FlatVals u_expr st in (TTarget(a,b,SuppressSequencePointAtTarget)) 

and u_bind st = let a = u_Val st in let b = u_expr st in TBind(a,b,NoSequencePointAtStickyBinding)

and u_lval_op_kind st =
    match u_byte st with 
    | 0 -> LGetAddr 
    | 1 -> LByrefGet 
    | 2 -> LSet 
    | 3 -> LByrefSet 
    | _ -> ufailwith st "uval_op_kind"

  
and u_op st = 
    let tag = u_byte st
    match tag with
    | 0 -> let a = u_ucref st
           TOp_ucase a
    | 1 -> let a = u_tcref st
           TOp_exnconstr a
    | 2 -> TOp_tuple 
    | 3 -> let b = u_tcref st
           TOp_recd (RecdExpr,b) 
    | 4 -> let a = u_rfref st
           TOp_rfield_set a 
    | 5 -> let a = u_rfref st
           TOp_rfield_get a 
    | 6 -> let a = u_tcref st
           TOp_ucase_tag_get a 
    | 7 -> let a = u_ucref st
           let b = u_int st
           TOp_ucase_field_get (a,b) 
    | 8 -> let a = u_ucref st
           let b = u_int st
           TOp_ucase_field_set (a,b) 
    | 9 -> let a = u_tcref st
           let b = u_int st
           TOp_exnconstr_field_get (a,b) 
    | 10 -> let a = u_tcref st
            let b = u_int st
            TOp_exnconstr_field_set (a,b) 
    | 11 -> let a = u_int st
            TOp_tuple_field_get a 
    | 12 -> let a = (u_list u_ILInstr) st
            let b = u_typs st
            TOp_asm (a,b) 
    | 13 -> TOp_get_ref_lval 
    | 14 -> let a = u_ucref st
            TOp_ucase_proof a 
    | 15 -> TOp_coerce
    | 16 -> let a = u_trait st
            TOp_trait_call a
    | 17 -> let a = u_lval_op_kind st
            let b = u_vref st
            TOp_lval_op (a,b) 
    | 18 -> let (a1,a2,a3,a4,a5,a7,a8,a9) = (u_tup8 u_bool u_bool u_bool u_bool u_vrefFlags u_bool u_bool  u_ILMethodRef) st
            let b = u_typs st
            let c = u_typs st
            let d = u_typs st
            TOp_ilcall (a1,a2,a3,a4,a5,a7,a8,a9,b,c,d) 
    | 19 -> TOp_array
    | 20 -> TOp_while (NoSequencePointAtWhileLoop, NoSpecialWhileLoopMarker)
    | 21 -> let dir = match u_int st with 0 -> FSharpForLoopUp | 1 -> CSharpForLoopUp | 2 -> FSharpForLoopDown | _ -> failwith "unknown for loop"
            TOp_for (NoSequencePointAtForLoop, dir)
    | 22 -> TOp_bytes (u_bytes st)
    | 23 -> TOp_try_catch(NoSequencePointAtTry,NoSequencePointAtWith)
    | 24 -> TOp_try_finally(NoSequencePointAtTry,NoSequencePointAtFinally)
    | 25 -> let a = u_rfref st
            TOp_field_get_addr a
    | 26 -> TOp_uint16s (u_array u_uint16 st)
    | 27 -> TOp_reraise
    | _ -> ufailwith st "u_op" 

and u_expr st = 
    let tag = u_byte st
    match tag with
    | 0 -> let a = u_const st
           let b = u_dummy_range st
           let c = u_typ st
           TExpr_const (a,b,c) 
    | 1 -> let a = u_vref st
           let b = u_vrefFlags st
           let c = u_dummy_range st
           TExpr_val (a,b,c) 
    | 2 -> let a = u_op st
           let b = u_typs st
           let c = u_Exprs st
           let d = u_dummy_range st
           TExpr_op (a,b,c,d)
    | 3 -> let a = u_expr st
           let b = u_expr st
           let c = u_int st
           let d = u_dummy_range  st
           TExpr_seq (a,b,(match c with 0 -> NormalSeq | 1 -> ThenDoSeq | _ -> ufailwith st "specialSeqFlag"),SuppressSequencePointOnExprOfSequential,d) 
    | 4 -> let a0 = u_option u_Val st
           let b0 = u_option u_Val st
           let b1 = u_Vals st
           let c = u_expr st
           let d = u_dummy_range st
           let e = u_typ st
           TExpr_lambda (newUnique(),a0,b0,b1,c,d,e,SkipFreeVarsCache()) 
    | 5  -> let b = u_typar_specs st
            let c = u_expr st
            let d = u_dummy_range st
            let e = u_typ st
            TExpr_tlambda (newUnique(),b,c,d,e,SkipFreeVarsCache()) 
    | 6 ->  let a1 = u_expr st
            let a2 = u_typ st
            let b = u_typs st
            let c = u_Exprs st
            let d = u_dummy_range st
            TExpr_app (a1,a2,b,c,d) 
    | 7 ->  let a = u_binds st
            let b = u_expr st
            let c = u_dummy_range st
            TExpr_letrec (a,b,c,NewFreeVarsCache()) 
    | 8 ->  let a = u_bind st
            let b = u_expr st
            let c = u_dummy_range st
            TExpr_let (a,b,c,NewFreeVarsCache()) 
    | 9 ->  let a = u_dummy_range st
            let b = u_dtree st
            let c = u_targets st
            let d = u_dummy_range st
            let e = u_typ st
            TExpr_match (NoSequencePointAtStickyBinding,a,b,c,d,e,SkipFreeVarsCache()) 
    | 10 -> let b = u_typ st
            let c = (u_option u_Val) st
            let d = u_expr st
            let e = u_methods st
            let f = u_intfs st
            let g = u_dummy_range st
            TExpr_obj (newUnique(),b,c,d,e,f,g,SkipFreeVarsCache())
    | 11 -> let a = u_constraints st
            let b = u_expr st
            let c = u_expr st
            let d = u_dummy_range st
            TExpr_static_optimization (a,b,c,d)
    | 12 -> let a = u_typar_specs st
            let b = u_expr st
            let c = u_dummy_range st
            TExpr_tchoose (a,b,c)
    | 13 -> let b = u_expr st
            let c = u_dummy_range st
            let d = u_typ st
            TExpr_quote (b,ref None,c,d)
    | _ -> ufailwith st "u_expr" 

and u_static_optimization_constraint st = 
    let tag = u_byte st
    match tag with
    | 0 -> u_tup2 u_typ u_typ st |> TTyconEqualsTycon
    | 1 -> u_typ              st |> TTyconIsStruct
    | _ -> ufailwith st "u_static_optimization_constraint" 

and u_slotparam st = 
    let a,b,c,d,e,f = u_tup6 (u_option u_string) u_typ u_bool u_bool u_bool u_attribs st 
    TSlotParam(a,b,c,d,e,f)

and u_slotsig st = 
    let a,b,c,d,e,f = u_tup6 u_string u_typ u_typar_specs u_typar_specs (u_list (u_list u_slotparam)) (u_option u_typ) st
    TSlotSig(a,b,c,d,e,f)

and u_method st = 
    let a,b,c,d,e,f = u_tup6 u_slotsig u_attribs u_typar_specs (u_list u_Vals) u_expr u_dummy_range st 
    TObjExprMethod(a,b,c,d,e,f)

and u_methods st = u_list u_method st

and u_intf st = u_tup2 u_typ u_methods st

and u_intfs st = u_list u_intf st

let _ = fill_u_binds (u_FlatList u_bind)
let _ = fill_u_targets (u_array u_target)
let _ = fill_u_constraints (u_list u_static_optimization_constraint)
let _ = fill_u_Exprs (u_list u_expr)
let _ = fill_u_FlatExprs (u_FlatList u_expr)
let _ = fill_u_attribs (u_list u_attrib)
let _ = fill_u_Vals (u_list u_Val)
let _ = fill_u_FlatVals (u_FlatList u_Val)

//---------------------------------------------------------------------------
// Pickle/unpickle F# interface data 
//---------------------------------------------------------------------------

let unpickle_modul_spec st = u_tycon_spec st 
  
let unpickleModuleInfo st = 
    let a,b,c,_space = u_tup4 unpickle_modul_spec u_string u_bool (u_space 3) st 
    { mspec=a; compileTimeWorkingDir=b; usesQuotations=c }
