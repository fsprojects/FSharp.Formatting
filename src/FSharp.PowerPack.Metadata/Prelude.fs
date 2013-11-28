
// (c) Microsoft Corporation. Apache 2.0 License

//---------------------------------------------------------------------------
// This file contains definitions that mimic definitions from parts of the F#
// compiler as necessary to allow the internal compiler typed abstract
// syntax tree and metadata deserialization code to be loaded independently
// of the rest of the compiler.
//
// This is used to give FSharp.PowerPack.Metadata.dll a minimal code base support
// to ensure we can publish the source code for that DLL as an independent
// sample. At some point we may fork the implementation of this DLL completely.
//
// Ideally this file would be empty, and gradually we will align the compiler
// source to allow this to be the case.
//---------------------------------------------------------------------------

module internal Microsoft.FSharp.Metadata.Reader.Internal.Prelude
open Microsoft.FSharp.Control

    
type PPLazyFailure(exn:exn) =
    static let undefined = PPLazyFailure(System.InvalidOperationException("a lazy value was accessed during its own initialization"))
    member x.Exception = exn
    static member Undefined = undefined

// Adding this StructuredFormatDisplay is _almost_ redundant, since it just calls ToString(). 
// However it means that when structured formatting is also printing
// properties then the properties are _not_ printed, which means the Value property is not explored.
[<StructuredFormatDisplay("{StructuredFormatDisplay}")>]                                                     
[<AllowNullLiteral>]
type PPLazy<'T>(value : 'T, funcOrException: obj) = 

    /// This field holds the result of a successful computation. It's initial value is Unchecked.defaultof
    let mutable value = value 

    /// This field holds either the function to run or a PPLazyFailure object recording the exception raised 
    /// from running the function. It is null if the thunk has been evaluated successfully.
    [<VolatileField>]
    let mutable funcOrException = funcOrException

    static member Create(f: (unit->'T)) : PPLazy<'T> = 
        PPLazy<'T> (value = Unchecked.defaultof<'T>, funcOrException = box(f) )
    static member CreateFromValue(x:'T) : PPLazy<'T> = 
        PPLazy<'T> (value = x, funcOrException = null)
    member x.IsValueCreated = (match funcOrException with null -> true | _ -> false)
    member x.Value =  
        match funcOrException with 
        | null -> value 
        | _ -> 
            // Enter the lock in case another thread is in the process of evaluting the result
            System.Threading.Monitor.Enter(x);
            try 
                match funcOrException with 
                | null -> value 
                | :? PPLazyFailure as res -> 
                      raise(res.Exception)
                | :? (unit -> 'T) as f -> 
                      funcOrException <- box(PPLazyFailure.Undefined)
                      try 
                          let res = f () 
                          value <- res; 
                          funcOrException <- null; 
                          res
                      with e -> 
                          funcOrException <- box(new PPLazyFailure(e)); 
                          reraise()
                | _ -> 
                    failwith "unreachable"
            finally
                System.Threading.Monitor.Exit(x)

    member internal x.StructuredFormatDisplay = x.ToString() // for the StructuredFormatDisplay attribute

    override x.ToString() = 
        if x.IsValueCreated then 
            if box x.Value = null then
                "<null>"
            else
                x.Value.ToString()
        else
            match funcOrException with 
            | :? PPLazyFailure as res -> 
                match res.Exception with 
                | e when System.Runtime.CompilerServices.RuntimeHelpers.Equals(e,PPLazyFailure.Undefined) -> "<evaluating>"
                | e ->  e.ToString()
            | _ -> 
                "<unevaluated>"
        
    member x.IsDelayed = not(x.IsValueCreated)

    member x.IsForced = x.IsValueCreated

    member x.IsException = false
    
    member x.Force() = x.Value
    
    member x.SynchronizedForce() = x.Value
    
    member x.UnsynchronizedForce() = x.Value

type 'T ``pplazy`` = PPLazy<'T>       

let isSome x = Option.isSome x
let isNull (x : 'T) = match (x :> obj) with null -> true | _ -> false

type pos =  { posLine: int; posCol: int }  

let mk_pos x y = { posLine=x; posCol=y }
type range = 
    { rangeFile: string;
      rangeBegin: pos;
      rangeEnd: pos }  

let mk_range file p1 p2 = 
    { rangeFile = file; rangeBegin=p1; rangeEnd=p2 }

let range0 =  mk_range "unknown" (mk_pos 1 0) (mk_pos 1 80)
let rangeN filename line =  mk_range filename (mk_pos line 0) (mk_pos line 80)

[<Sealed>]
type ident (text:string,range:range) = 
     member x.idText = text
     member x.idRange = range
     override x.ToString() = text
let mksyn_id m s = ident(s,m)

type MemberFlags =
  { MemberIsInstance: bool;
    MemberIsDispatchSlot: bool;
    MemberIsOverrideOrExplicitImpl: bool;
    MemberIsFinal: bool;
    MemberKind: MemberKind }

and MemberKind = 
    | MemberKindClassConstructor
    | MemberKindConstructor
    | MemberKindMember 
    | MemberKindPropertyGet 
    | MemberKindPropertySet    
    | MemberKindPropertyGetSet    

and TyparStaticReq = 
    | NoStaticReq 
    | HeadTypeStaticReq 

and [<NoEquality; NoComparison>]
    SynTypar = 
    | Typar of ident * TyparStaticReq * bool 

[<NoEquality; NoComparison>]
type LazyWithContext<'T,'ctxt> = 
    { v: PPLazy<'T> }
    member x.Force(_) = x.v.Force()
    static member NotLazy (x:'T)  = { v = PPLazy.CreateFromValue x }
    static member Create (f: unit -> 'T)  = { v = PPLazy.Create f }

type XmlDoc = XmlDoc of string[]

let emptyXmlDoc = XmlDoc[| |]
let MergeXmlDoc (XmlDoc lines) (XmlDoc lines') = XmlDoc (Array.append lines lines')

let notlazy v = PPLazy.CreateFromValue v
module String =


    let tryDropSuffix s t = 
        let lens = String.length s
        let lent = String.length t
        if (lens >= lent && (s.Substring (lens-lent, lent) = t)) then 
            Some (s.Substring (0,lens - lent))
        else
            None

    let hasSuffix s t = (tryDropSuffix s t).IsSome
    let dropSuffix s t = match (tryDropSuffix s t) with Some(res) -> res | None -> failwith "dropSuffix"

module List = 

    let lengthsEqAndForall2 p l1 l2 = 
        List.length l1 = List.length l2 &&
        List.forall2 p l1 l2
    let mapSquared f xss = xss |> List.map (List.map f)

    let frontAndBack l = 
        let rec loop acc l = 
            match l with
            | [] -> 
                System.Diagnostics.Debug.Assert(false, "empty list")
                invalidArg "l" "empty list" 
            | [h] -> List.rev acc,h
            | h::t -> loop  (h::acc) t
        loop [] l

    let mapq (f: 'T -> 'T) inp =
        assert not (typeof<'T>.IsValueType) 
        match inp with
        | [] -> inp
        | _ -> 
            let res = List.map f inp 
            let rec check l1 l2 = 
                match l1,l2 with 
                | h1::t1,h2::t2 -> 
                    System.Runtime.CompilerServices.RuntimeHelpers.Equals(h1,h2) && check t1 t2
                | _ -> true
            if check inp res then inp else res


    let splitAfter n l = 
        let rec split_after_acc n l1 l2 = if n <= 0 then List.rev l1,l2 else split_after_acc (n-1) ((List.head l2):: l1) (List.tail l2) 
        split_after_acc n [] l

module ListSet = 
    let rec mem f x l = 
        match l with 
        | [] -> false
        | x'::t -> f x x' || mem f x t

    let isSubsetOf f l1 l2 = List.forall (fun x1 -> mem f x1 l2) l1
    let isSupersetOf f l1 l2 = List.forall (fun x2 -> mem (fun y2 y1 ->  f y1 y2) x2 l1) l2
    let equals f l1 l2 = isSubsetOf f l1 l2 && isSupersetOf f l1 l2

    let rec remove f x l = 
        match l with 
        | (h::t) -> if f x h then t else h:: remove f x t
        | [] -> []

    (* NOTE: quadratic! *)
    let rec subtract f l1 l2 = 
      match l2 with 
      | (h::t) -> subtract f (remove (fun y2 y1 ->  f y1 y2) h l1) t
      | [] -> l1


module ListAssoc = 

    /// Treat a list of key-value pairs as a lookup collection.
    /// This function looks up a value based on a match from the supplied
    /// predicate function.
    let rec find f x l = 
      match l with 
      | [] -> raise (System.Collections.Generic.KeyNotFoundException())
      | (x',y)::t -> if f x x' then y else find f x t

    /// Treat a list of key-value pairs as a lookup collection.
    /// This function returns true if two keys are the same according to the predicate
    /// function passed in.
    let rec containsKey (f:'key->'key->bool) (x:'key) (l:('key*'value) list) : bool = 
      match l with 
      | [] -> false
      | (x',y)::t -> f x x' || containsKey f x t

type SequencePointInfoForBinding = unit
type SequencePointInfoForTarget = unit
type SequencePointInfoForTry = unit
type SequencePointInfoForWith = unit
type SequencePointInfoForFinally = unit
type SequencePointInfoForSeq = unit
type SequencePointInfoForForLoop =  unit
type SequencePointInfoForWhileLoop = unit

let SuppressSequencePointAtTarget = ()
let NoSequencePointAtStickyBinding = ()
let NoSequencePointAtTry = ()
let NoSequencePointAtWith = ()
let NoSequencePointAtFinally = ()
let NoSequencePointAtForLoop = ()
let NoSequencePointAtWhileLoop = ()
let SuppressSequencePointOnExprOfSequential = ()

let error e = raise e
let errorR e = raise e
let Error((n,s),m) = Failure s
let InternalError(s,m) = Failure s
let UnresolvedReferenceNoRange s = Failure ("unresolved reference " + s)
let UnresolvedPathReferenceNoRange (s,p) = Failure ("unresolved reference " + s + " for path " + p)
type NameMap<'T> = Map<string,'T>
type ExprData = unit
type NameMultiMap<'T> = 'T list NameMap
type MultiMap<'T,'U when 'T : comparison> = Map<'T,'U list>


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NameMap = 
    let empty = Map.empty
    let tryFind v (m: 'T NameMap) = Map.tryFind v m 
    let ofKeyedList f l = List.foldBack (fun x acc -> Map.add (f x) x acc) l Map.empty
    let range m = List.rev (Map.foldBack (fun _ x sofar -> x :: sofar) m [])
    let add v x (m: 'T NameMap) = Map.add v x m
    let foldRange f (l: 'T NameMap) acc = Map.foldBack (fun _ y acc -> f y acc) l acc
    let mem v (m: 'T NameMap) = Map.containsKey v m
    let find v (m: 'T NameMap) = Map.find v m

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module MultiMap = 
    let existsInRange f (m: MultiMap<_,_>) = Map.exists (fun _ l -> List.exists f l) m
    let find v (m: MultiMap<_,_>) = match Map.tryFind v m with None -> [] | Some r -> r
    let add v x (m: MultiMap<_,_>) = Map.add v (x :: find v m) m
    let range (m: MultiMap<_,_>) = Map.foldBack (fun _ x sofar -> x @ sofar) m []
    //let chooseRange f (m: MultiMap<_,_>) = Map.foldBack (fun _ x sofar -> List.choose f x @ sofar) m []
    let empty : MultiMap<_,_> = Map.empty
    let initBy f xs : MultiMap<_,_> = xs |> Seq.groupBy f |> Seq.map (fun (k,v) -> (k,List.ofSeq v)) |> Map.ofSeq 


type cache<'T> = NoCache
let newCache() = NoCache
let cacheOptRef _ f = f ()
let cached _ f = f()
let (===) x y = LanguagePrimitives.PhysicalEquality x y
let dprintf fmt = printf fmt
let text_of_path path = String.concat "." path
type SkipFreeVarsCache = unit
type FreeVarsCache = unit cache

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module NameMultiMap = 
    let find v (m: NameMultiMap<'T>) = match Map.tryFind v m with None -> [] | Some r -> r
    let add v x (m: NameMultiMap<'T>) = NameMap.add v (x :: find v m) m
    let empty : NameMultiMap<'T> = Map.empty
    let range (m: NameMultiMap<'T>) = Map.foldBack (fun _ x sofar -> x @ sofar) m []
    let rangeReversingEachBucket (m: NameMultiMap<'T>) = Map.foldBack (fun _ x sofar -> List.rev x @ sofar) m []
    let ofList (xs: (string * 'T) list) : NameMultiMap<'T> = xs |> Seq.groupBy fst |> Seq.map (fun (k,v) -> (k,List.ofSeq (Seq.map snd v))) |> Map.ofSeq 


type ByteStream = 
    { bytes: byte[]; 
      mutable pos: int; 
      max: int }

module Bytestream = 

    let of_bytes (b:byte[]) n len = 
        if n < 0 || (n+len) > b.Length then failwith "Bytestream.of_bytes";
        { bytes = b; pos = n; max = n+len }

    let read_byte b  = 
        if b.pos >= b.max then failwith "Bytestream.of_bytes.read_byte: end of stream";
        let res = b.bytes.[b.pos] 
        b.pos <- b.pos + 1;
        int32 res 
      
    let read_bytes b n  = 
        if b.pos + n > b.max then failwith "Bytestream.read_bytes: end of stream";
        let res = Array.sub b.bytes b.pos n 
        b.pos <- b.pos + n;
        res 

    let position b = b.pos 
    let skip b n = b.pos <- b.pos + n

    let read_utf8_bytes_as_string (b: ByteStream) n = 
        let res = System.Text.Encoding.UTF8.GetString(b.bytes,b.pos,n) 
        b.pos <- b.pos + n; res 

