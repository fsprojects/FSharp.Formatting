// --------------------------------------------------------------------------------------
// F# Markdown (Collections.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

namespace FSharp.Collections


// --------------------------------------------------------------------------------------
// Various helper functions for working with lists 
// that are useful when writing parsers by hand.
// --------------------------------------------------------------------------------------

module List = 
  /// Returns a singleton list containing a specified value
  let singleton v = [v]

  /// Skips the specified number of elements. Fails if the list is smaller.
  let rec skip count = function
    | xs when count = 0 -> xs
    | _::xs when count > 0 -> skip (count - 1) xs
    | _ -> invalidArg "" "Insufficient length"

  /// Skips elements while the predicate returns 'true' and then 
  /// returns the rest of the list as a result.
  let rec skipWhile p = function
    | hd::tl when p hd -> skipWhile p tl
    | rest -> rest

  /// Partitions list into an initial sequence (while the 
  /// specified predicate returns true) and a rest of the list.
  let partitionWhile p input = 
    let rec loop acc = function
      | hd::tl when p hd -> loop (hd::acc) tl
      | rest -> List.rev acc, rest
    loop [] input

  /// Partitions list into an initial sequence (while the specified predicate 
  /// returns true) and a rest of the list. The predicate gets the entire 
  /// tail of the list and can perform lookahead.
  let partitionWhileLookahead p input = 
    let rec loop acc = function
      | hd::tl when p (hd::tl) -> loop (hd::acc) tl
      | rest -> List.rev acc, rest
    loop [] input

  /// Partitions list into an initial sequence (while the 
  /// specified predicate returns 'false') and a rest of the list.
  let partitionUntil p input = partitionWhile (p >> not) input

  /// Iterates over the elements of the list and calls the first function for 
  /// every element. Between each two elements, the second function is called.
  let rec iterInterleaved f g input =
    match input with 
    | x::y::tl -> f x; g (); iterInterleaved f g (y::tl)
    | x::[] -> f x
    | [] -> ()

  /// Tests whether a list starts with the elements of another
  /// list (specified as the first parameter)
  let inline startsWith start (list:'T list) = 
    let rec loop start (list:'T list) = 
      match start, list with
      | x::xs, y::ys when x = y -> loop xs ys
      | [], _ -> true
      | _ -> false
    loop start list

  /// Partitions the input list into two parts - the break is added 
  /// at a point where the list starts with the specified sub-list.
  let partitionUntilEquals endl input = 
    let rec loop acc = function
      | input when startsWith endl input -> Some(List.rev acc, input)
      | x::xs -> loop (x::acc) xs
      | [] -> None
    loop [] input    

  /// A function that nests items of the input sequence 
  /// that do not match a specified predicate under the 
  /// last item that matches the predicate. 
  let nestUnderLastMatching f input = 
    let rec loop input = seq {
      let normal, other = partitionUntil f input
      match List.rev normal with
      | last::prev ->
          for p in List.rev prev do yield p, []
          let other, rest = partitionUntil (f >> not) other
          yield last, other 
          yield! loop rest
      | [] when other = [] -> ()
      | _ -> invalidArg "" "Should start with true" }
    loop input |> List.ofSeq


// --------------------------------------------------------------------------------------
// Simple tree type and a function for turning list with indentation into a tree
// --------------------------------------------------------------------------------------

/// Represents a tree with nodes containing values an a list of children
type Tree<'T> = Node of 'T * list<Tree<'T>>

module Tree = 
  /// Takes all elements at the specified level and turns them into nodes
  let rec private takeAtLevel indent tail = 
    match tail with 
    | (i, value)::tail when i >= indent ->  // >= instead of = to handle odd cases
      let nested, tail = takeDeeperThan i tail
      let following, tail = takeAtLevel indent tail
      Node(value, nested) :: following, tail
    | tail -> [], tail

  /// Takes elements that are deeper (children) and turns them into nodes
  and private takeDeeperThan indent tail = 
    match tail with 
    | (i, value)::tail when i > indent ->
      let nested, tail = takeDeeperThan i tail
      let following, tail = takeAtLevel i tail
      Node(value, nested) :: following, tail
    | tail -> [], tail

  /// Turns a list of items with an indentation specified by an integer
  /// into a tree where indented items are children.
  let ofIndentedList input =
    let res, tail = takeAtLevel 0 input
    if tail <> [] then failwith "Wrong indentation"
    res