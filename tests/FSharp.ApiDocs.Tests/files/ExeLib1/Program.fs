/// A sample module in an executable project.
module ExeLib1.Greeter

/// Greet the given name.
let greet (name: string) = printfn "Hello, %s!" name

[<EntryPoint>]
let main argv =
    greet "World"
    0
