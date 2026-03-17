namespace CrossAssemblyA

/// A simple single-case discriminated union that is defined in Assembly A.
/// It is referenced from Assembly B to test cross-assembly tooltip resolution (issue #1085).
type Did = private Did of string

module Did =
    /// Construct a Did value.
    let create (s: string) = Did s
