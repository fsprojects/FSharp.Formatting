module ReferenceProject.Classes

open System.Runtime.InteropServices

// TODO:
// - SRTP syntax
// - Abstract classes
// - Attributes ? Like AllowNullLiteral

type Empty =
    class end

type EmptyConstructor() =
    class end

type SeveralConstructors() =
    new (_prefix : int) = SeveralConstructors()
    new (_prefix : int, _indentSize : int) = SeveralConstructors()

// https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/explicit-fields-the-val-keyword
type ExplicitFields () =
    member val ExplicitFieldGetSet = 0 with get, set
    member val ExplicitFieldGet = 0 with get

// https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/properties
type Properties () =
    static member StaticProperty = 0
    static member StaticPropertyGetOnly with get () = 0
    static member StaticPropertySetOnly with set (_value : int) = ()
    static member StaticPropertyGetSet with get() = 0 and set(_value : int) = ()
    static member val StaticPropertyWithAutoImpl = 0 with get, set

// https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/members/methods
type InstanceMethods () =
    member this.Void () = ()

    // Non curried arguments
    member this.Echo (_message : string) = _message
    member this.Add (a : int) (b : int) = a + b

    // Curried arguments
    member this.AddCurried (a : int, b : int) = a + b

    // Methods with overloads
    member this.Log (_message : string) = ()
    member this.Log (_message : string, _level : int) = ()
    member this.Log (_message : string, _level : int, ?_prefix : string) = ()

type StaticMethods () =
    static member StaticVoid () = ()

    // Non curried arguments
    static member StaticEcho (_message : string) = _message
    static member StaticAdd (a : int) (b : int) = a + b

    // Curried arguments
    static member StaticAddCurried (a : int, b : int) = a + b

    // Static methods with overloads
    static member StaticLog (_message : string) = ()
    static member StaticLog (_message : string, _level : int) = ()
    static member StaticLog (_message : string, _level : int, ?_prefix : string) = ()

[<AbstractClass>]
type AbstractMethods () =
    abstract member AbstractMethod : unit -> unit
    abstract member AbstractMethodWithUnknownArguments : string -> int -> unit
    abstract member AbstractMethodWithNamedArguments : message : string -> level : int -> unit

type OptionalInterop () =
    member _.AddOne([<Optional; DefaultParameterValue(12)>] i) = i + 1

type InterfaceImplementation () =
    interface Interfaces.InterfaceC with
        member this.MethodA () = ()
        member this.MethodB () = ()
        member this.MethodC () = ()