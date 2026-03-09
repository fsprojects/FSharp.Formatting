(**
# Example F# Script

This is a **sample** F# literate script for testing `fsdocs convert`.

## Basic F# Code
*)

let greet name = printfn "Hello, %s!" name

greet "World"

(**
## Pattern Matching
*)

let describe x =
    match x with
    | 0 -> "zero"
    | n when n > 0 -> "positive"
    | _ -> "negative"

let result = describe 42
printfn "42 is %s" result

(**
## A List
*)

let numbers = [ 1 .. 5 ]
let doubled = numbers |> List.map ((*) 2)

printfn "%A" doubled

(**
## Inline Code and Emphasis

Here is some `inline code`, **bold text**, and _italic text_.

External link: [FSharp.org](https://fsharp.org)
*)
