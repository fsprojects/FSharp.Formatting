namespace FsLib

/// <summary>
/// Union sample
/// </summary>
type Union = 
  /// Hello of int
  | Hello of int
  /// World of string and int
  | World of string * int

/// <summary>
/// Record sample
/// </summary>
type Record = 
  { /// This is name
    Name : string
    /// This is age
    Age : int }
  /// Additional member
  member x.Foo = 0
