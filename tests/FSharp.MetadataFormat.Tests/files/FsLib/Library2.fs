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