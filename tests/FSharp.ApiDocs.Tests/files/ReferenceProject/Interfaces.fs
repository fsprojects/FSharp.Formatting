module ReferenceProject.Interfaces

type Empty =
    interface end

type InstanceMethods =
    abstract member Method : unit -> unit

type StaticMethods =
    static member Version = "This is version 1.0"
    static member Log (_message: string) = ()

// Interfaces for inheritance testing
type InterfaceA =
    abstract member MethodA : unit -> unit

type InterfaceB =
    abstract member MethodB : unit -> unit

type InterfaceC =
    inherit InterfaceA
    inherit InterfaceB
    abstract member MethodC : unit -> unit