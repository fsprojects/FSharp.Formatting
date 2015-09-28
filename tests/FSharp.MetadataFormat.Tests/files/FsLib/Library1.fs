namespace FsLib

/// Union sample
type Union =
  /// Hello of int
  | Hello of int
  /// World of string and int
  | World of string * int
#if FSHARP_31
  /// Naming of rate:float and string
  | Naming of rate: float * string
#endif

/// Record sample
type Record =
  { /// This is name
    Name : string
    /// This is age
    Age : int }
  /// Additional members
  member x.Foo = 0
  member x.Foo2() = 0

type ITestInterface =
  abstract Test : unit -> RazorEngine.Templating.IRazorEngineService
  abstract FixScript : string -> string

/// Issue 201 docs
[<System.Runtime.CompilerServices.Extension>]
module Test_Issue201 =
  /// Extension docs
  [<System.Runtime.CompilerServices.Extension>]
  let MyExtension (o : ITestInterface) =
    ignore <| o.Test().GetKey(null)

[<AutoOpen>]
module Test_Issue201Extensions =
  type ITestInterface with
    member x.MyExtension() =
     Test_Issue201.MyExtension x