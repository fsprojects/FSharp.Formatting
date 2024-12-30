module ReferenceProject.Functions

/// <summary>
/// This function calculates the sum of two numbers.
/// </summary>
/// <param name="a">The first number.</param>
/// <param name="b">The second number.</param>
/// <returns>The sum of the two numbers.</returns>
let add (a: int) b (c : System.Action) = a + b

let emptyFunction () = ()

let tupleArguments (a: int, b: int) = a + b
