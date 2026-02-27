namespace FsLib

/// This type name will be duplicated in [Nested]
/// This type name will be duplicated in `Nested`
type DuplicatedTypeName = int

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

    /// This is My type
    type MyType = int

    /// This is other type
    type OtherType = int

    /// This type has the same name as [FsLib.DuplicatedTypeName]
    /// This type has the same name as `FsLib.DuplicatedTypeName`
    type DuplicatedTypeName = int

    /// This function returns a [FsLib.Nested.MyType] multiplied by 2.
    /// You will notice that `FsLib.Nested.MyType` is just an `int`
    let f x : MyType = x * 2

    /// This function returns a [OtherType] multiplied by 3.
    /// You will notice that `OtherType` is just an `int`
    let f2 x : OtherType = x * 3

    /// This function returns a [DuplicatedTypeName] multiplied by 4.
    /// `DuplicatedTypeName` is duplicated so it should no add a cross-type link
    let f3 x : OtherType = x * 4

    /// This function returns a [InexistentTypeName] multiplied by 5.
    /// `InexistentTypeName` does not exists so it should no add a cross-type link
    let f4 x : OtherType = x * 5

type ITest_Issue229 =
    abstract member Name: string

type Test_Issue229(name) =
    /// instance comment
    member x.Name = name

    interface ITest_Issue229 with
        /// interface comment
        member x.Name = name

type Test_Issue287() =
    /// Function Foo!
    abstract member Foo: int -> unit
    /// Empty function for signature
    default x.Foo a = ()

[<RequireQualifiedAccess>]
module Test_Issue472_R =

    /// test function with tupled arguments
    let ftupled (x: int, y: int) = x * y

    /// test function with multiple arguments
    let fmultipleargs (x: int) (y: int) = x * y

module Test_Issue472 =

    /// test function with tupled arguments
    let ftupled (x: int, y: int) = x * y

    /// test function with multiple arguments
    let fmultipleargs (x: int) (y: int) = x * y

type Test_Issue472_T() =
    /// Function MultPartial!
    member x.MultPartial (arg1: int) (arg2: int) = ()
    /// Function MultArg!
    member x.MultArg(arg1: int, arg2: int) = ()
    /// Function MultArgTupled!
    member x.MultArgTupled(arg: (int * int)) = ()

(*
type ITestInterface =
  abstract Test : unit -> DotLiquid.Templating.IDotLiquidService
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
*)

/// [omit]
type Test_Omit() =
    /// This Should not be displayed
    member x.Foo a = ()

/// Test ` ` test
type Test_Empty_Code_Block() =
    let empty = ()

module ``Space-Missing`` =

    /// Implicit cast operator test
    type ``Implicit-Cast``(value: int) = class end


/// Test type with setter-only and getter+setter properties (issue #734)
type Test_Issue734() =
    let mutable _value = ""

    /// Getter-only property
    member _.GetterOnly = "getter"

    /// Setter-only property
    member _.SetterOnly
        with set (value: string) = _value <- value

    /// Getter and setter property
    member _.GetterAndSetter
        with get () = _value
        and set (value: string) = _value <- value

module CommentExamples =
    /// <summary>this does the thing</summary>
    /// <example>this is an example
    /// </example>
    let dothing () = ()

    /// <summary>this does the thing</summary>
    /// <example id="has-id">this is an example with an id
    /// </example>
    let dothing2 () = ()

    /// <summary>this does the thing</summary>
    /// <example id="double-id">this is an example with an id
    /// </example>
    /// <example id="double-id">this is an example with an id
    /// </example>
    let doubleExampleId () = ()

    /// <summary>AAA</summary>
    /// <example>
    /// <code lang="fsharp">
    /// [&lt;ReflectedDefinition&gt;]
    ///  let f x = (x, x)
    /// </code>
    /// </example>
    ///
    let attribInCodeExample () = ()

/// Base class for testing inherited members (issue #590)
type BaseClassForInheritance() =
    /// A documented instance method on the base class
    member x.BaseMethod() = 42

    /// A documented static method on the base class
    static member BaseStaticMethod() = "hello"

/// Derived class that inherits documented members from BaseClassForInheritance (issue #590)
type DerivedClassForInheritance() =
    inherit BaseClassForInheritance()

    /// A method on the derived class
    member x.DerivedMethod() = 100

/// Module with functions that have type constraints for testing constraint display modes
module TypeConstraintTests =

    /// A function that requires equality constraint
    let requiresEquality (x: 'T) (y: 'T) : bool when 'T: equality = x = y

    /// A function that requires comparison constraint
    let requiresComparison (items: seq<'T>) : 'T list when 'T: comparison = items |> Seq.toList |> List.sort

    /// A generic class with a type constraint
    type ComparisonWrapper<'T when 'T: comparison>(value: 'T) =
        /// Gets the wrapped value
        member _.Value = value
        /// Compares with another wrapper
        member x.CompareTo(other: ComparisonWrapper<'T>) = compare x.Value other.Value
