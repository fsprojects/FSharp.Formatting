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