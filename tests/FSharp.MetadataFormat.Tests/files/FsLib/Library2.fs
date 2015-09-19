namespace FsLib

/// Sample class
type Class() = 
  /// Readonly int property
  member x.Member = 0

/// Nested module
module Nested = 
  /// Somewhat nested module
  module Submodule = 
    /// Very nested field
    let supernested = 42

    /// Very nested type
    type VeryNestedType() =
      /// Super nested member
      member x.Member = ""

  /// Somewhat nested type
  type NestedType() = 
    /// Very nested member
    member x.Member = ""

type ITest_Issue229 = abstract member Name : string

type Test_Issue229 (name) =
    /// instance comment
    member x.Name = name

    interface ITest_Issue229 with
        /// interface comment
        member x.Name = name  

type Test_Issue287 () =
  /// Function Foo!
  abstract member Foo: int-> unit
  /// Empty function for signature
  default x.Foo a = ()

type ITestInterface =
  abstract Test : unit -> RazorEngine.Templating.IRazorEngineService
  abstract FixScript : string -> string

/// Issue 201 docs
[<System.Runtime.CompilerServices.Extension>]
module Test_Issue201 =
  let internal notImpl () =
        (raise <| System.NotSupportedException("Migration is not supported by this type, please implement GetMigrator."))
        : 'a
  /// Test FixScript_MSSQL Documentation
  let FixScript_MSSQL (script:string) = script
  /// Test FixScript_MySQL Documentation
  let FixScript_MySQL (script:string) =
    script.Replace(
      "from information_schema.columns where", 
      "FROM information_schema.columns WHERE table_schema = SCHEMA() AND")
 
  /// Extension docs
  [<System.Runtime.CompilerServices.Extension>]
  let MyExtension (o : ITestInterface) = 
    ignore <| o.Test().GetKey(null)
 
[<AutoOpen>]
module Test_Issue201Extensions =
  type ITestInterface with
    member x.MyExtension() =
     Test_Issue201.MyExtension x 

/// [omit]
type Test_Omit() =
  /// This Should not be displayed
  member x.Foo a = ()