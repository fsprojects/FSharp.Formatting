// (c) Microsoft Corporation 2005-2009. 

module internal  Microsoft.FSharp.Metadata.Reader.Internal.Tastops

open System.Collections.Generic 
open Microsoft.FSharp.Metadata.Reader.Internal.AbstractIL.IL
open Microsoft.FSharp.Metadata.Reader.Internal.PrettyNaming
open Microsoft.FSharp.Metadata.Reader.Internal.Prelude
open Microsoft.FSharp.Metadata.Reader.Internal.Tast
open Microsoft.FSharp.Metadata.Reader.Internal.Env

//---------------------------------------------------------------------------
// Basic data structures
//---------------------------------------------------------------------------

[<NoEquality; NoComparison>]
type TyparMap<'a> = TPMap of StampMap<'a>
let tpmap_find (v: Typar) (TPMap m) = m.[v.Stamp]
let tpmap_mem (v: Typar) (TPMap m) = m.ContainsKey(v.Stamp)
let tpmap_add (v: Typar) x (TPMap m) = TPMap (m.Add(v.Stamp,x))
let tpmap_empty () = TPMap Map.empty

//---------------------------------------------------------------------------
// Basic equalites
//---------------------------------------------------------------------------

let tcref_eq g tcref1 tcref2 = prim_entity_ref_eq tcref1 tcref2

//---------------------------------------------------------------------------
// Some basic type builders
//---------------------------------------------------------------------------

let mk_unit_typ g = TType_app (g.unit_tcr, [])
let mk_nativeint_typ g = TType_app (g.nativeint_tcr, [])

//--------------------------------------------------------------------------
// Tuple compilation (types)
//------------------------------------------------------------------------ 

let maxTuple = 8
let goodTupleFields = maxTuple-1

let compiled_tuple_tcref g tys = 
    let n = List.length tys 
    if   n = 1 then g.tuple1_tcr
    elif n = 2 then g.tuple2_tcr
    elif n = 3 then g.tuple3_tcr
    elif n = 4 then g.tuple4_tcr
    elif n = 5 then g.tuple5_tcr
    elif n = 6 then g.tuple6_tcr
    elif n = 7 then g.tuple7_tcr
    elif n = 8 then g.tuple8_tcr
    else failwithf "compiled_tuple_tcref, n = %d" n

let rec compiled_tuple_ty g tys = 
    let n = List.length tys 
    if n < maxTuple then TType_app (compiled_tuple_tcref g tys, tys)
    else 
        let tysA,tysB = List.splitAfter goodTupleFields tys
        TType_app (g.tuple8_tcr, tysA@[compiled_tuple_ty g tysB])

