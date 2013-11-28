(* (c) Microsoft Corporation 2005-2009.  *)



module internal Microsoft.FSharp.Metadata.Reader.Internal.Env

open System.Collections.Generic 
open Microsoft.FSharp.Metadata.Reader.Internal.AbstractIL.IL
open Microsoft.FSharp.Metadata.Reader.Internal.PrettyNaming
open Microsoft.FSharp.Metadata.Reader.Internal.Prelude
open Microsoft.FSharp.Metadata.Reader.Internal.Tast

[<NoEquality; NoComparison>]
type TcGlobals = 
    { nativeptr_tcr:TyconRef;
      nativeint_tcr:TyconRef;
      tuple1_tcr      : TyconRef;
      tuple2_tcr      : TyconRef;
      tuple3_tcr      : TyconRef;
      tuple4_tcr      : TyconRef;
      tuple5_tcr      : TyconRef;
      tuple6_tcr      : TyconRef;
      tuple7_tcr      : TyconRef;
      tuple8_tcr      : TyconRef;
      fastFunc_tcr    : TyconRef;
      fslibCcu : CcuThunk
      byref_tcr:TyconRef;
      il_arr1_tcr:TyconRef;
      il_arr2_tcr:TyconRef;
      il_arr3_tcr:TyconRef;
      il_arr4_tcr:TyconRef;
      unit_tcr:TyconRef; }

