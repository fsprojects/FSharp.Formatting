namespace SameNameLib

/// <summary>
/// A type whose name is the same as its enclosing namespace.
/// This is a regression test for https://github.com/fsprojects/FSharp.Formatting/issues/944
/// </summary>
type SameNameLib(value: string) =

    /// The value stored in this instance
    member _.Value = value

    /// A static factory method
    static member Create(v: string) = SameNameLib(v)

    override _.ToString() = value
