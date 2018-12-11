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

/// To use this from C#
///
///     [lang=csharp]
///     var a = new MyClass()
///
/// To use this from F#
///
///     let a = FsLib.MyClass()
///
type MyClass() = 
  member x.Nothing = 0