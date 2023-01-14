/// <summary>
/// DOM low-level helper functions
/// </summary>
module Test.DOM

/// <exclude/>
type DomAction =
    | Append
    | Replace of string
    | Nothing

/// <exclude/>
let edit (_ : DomAction) = ()

let build() = ()