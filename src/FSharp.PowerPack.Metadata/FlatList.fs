// (c) Microsoft Corporation. Apache 2.0 License(

namespace Microsoft.FSharp.Metadata.Reader.Internal

open System.Collections
open System.Collections.Generic

type internal FlatList<'T> ='T list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module internal FlatList =
    let empty<'T> : 'T list = []
    let collect (f: 'T -> FlatList<'T>) (x:FlatList<_>) =  List.collect f x
    let exists f (x:FlatList<_>) = List.exists f x
    let filter f (x:FlatList<_>) = List.filter f x
    let fold f acc (x:FlatList<_>) = List.fold f acc x
    let fold2 f acc (x:FlatList<_>) (y:FlatList<_>) = List.fold2 f acc x y
    let foldBack f (x:FlatList<_>) acc  = List.foldBack f x acc
    let foldBack2 f (x:FlatList<_>) (y:FlatList<_>) acc = List.foldBack2 f x y acc
    let map2 f (x:FlatList<_>) (y:FlatList<_>) = List.map2 f x y
    let forall f (x:FlatList<_>) = List.forall f x
    let forall2 f (x1:FlatList<_>) (x2:FlatList<_>) = List.forall2 f x1 x2
    let iter2 f (x1:FlatList<_>) (x2:FlatList<_>) = List.iter2 f x1 x2 
    let partition f (x:FlatList<_>) = List.partition f x
    let (* inline *) sum (x:FlatList<int>) = List.sum x
    let (* inline *) sumBy (f: 'T -> int) (x:FlatList<'T>) = List.sumBy f x
    let unzip (x:FlatList<_>) = List.unzip x
    let physicalEquality (x:FlatList<_>) (y:FlatList<_>) = (LanguagePrimitives.PhysicalEquality x y)
    let tryFind f (x:FlatList<_>) = List.tryFind f x
    let concat (x:FlatList<_>) = List.concat x
    let isEmpty (x:FlatList<_>) = List.isEmpty x
    let one(x) = [x]
    let toMap (x:FlatList<_>) = Map.ofList x
    let length (x:FlatList<_>) = List.length x
    let map f (x:FlatList<_>) = List.map f x
    let mapi f (x:FlatList<_>) = List.mapi f x
    let iter f (x:FlatList<_>) = List.iter f x
    let iteri f (x:FlatList<_>) = List.iteri f x
    let toList (x:FlatList<_>) = x
    let ofSeq (x:seq<_>) = List.ofSeq x
    let append(l1 : FlatList<'T>) (l2 : FlatList<'T>) =  List.append l1 l2
    let ofList(l) = l
    let init n f = List.init n f
    let zip (x:FlatList<_>) (y:FlatList<_>) = List.zip x y
