namespace CrossAssemblyB

open CrossAssemblyA

/// A discriminated union in Assembly B whose Account case holds a Did value
/// from Assembly A.  Used to test cross-assembly tooltip rendering (issue #1085).
[<RequireQualifiedAccess>]
type Subject =
    | Account of Did
    | Record of string

/// A record in Assembly B whose fields mix types from Assembly A (Did),
/// primitive types, and generic wrapper types from the BCL.
/// Used to test cross-assembly tooltip rendering (issue #1085).
type TeamMember =
    { Did: Did
      Name: string
      Active: bool
      JoinedAt: System.DateTimeOffset option }
