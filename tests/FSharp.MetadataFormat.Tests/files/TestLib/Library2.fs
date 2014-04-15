namespace FsLib

/// <summary>Sample class</summary>
type Class() = 

  /// <summary>
  /// Readonly int property
  /// </summary>
  member x.Member = 0

/// <summary>
/// Nested module
/// </summary>
module Nested = 

  /// <summary>
  /// Somewhat nested module
  /// <para>This should show up in a separate paragrah</para>
  /// </summary>
  module Submodule = 
    /// <summary>Very nested field</summary>
    let supernested = 42

  /// <summary>
  /// Somewhat nested type
  /// </summary>
  type NestedType() = 
    /// <summary>Very nested member</summary>
    member x.Member = ""