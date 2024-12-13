module ReferenceProject.Records

type OneField =
    {
        Field: int
    }

type SeveralFields =
    {
        FirstName: string
        LastName: string
        Age: int
    }

type WithAnonymousRecord =
    {
        IndentationLevel: int
        Data:
            {|
                Prefix: string
                Time: System.DateTime
            |}
    }

type WithFunction =
    {
        Function: int -> int
    }

// Should we display something about the attributes? Some of them?
[<Struct>]
type WithAttributes =
    {
        Field: string
    }

type WithInstanceMethod =
    {
        FieldA : string
    }

    member this.MyVoidMethod () = ()
    member this.MyAddMethodCurry (a : int, b : int) = a + b
    member this.MyAddMethodUncurry (a : int) (b : int) = a + b
    member this.MyPropertyWithGetterWorksWithUnit with get () = ()
    member this.MyPropertyWithGetter with get () = 0
    member this.MyPropertyWithSetter with set (_value : int) = ()
    member this.MyPropertyWithGetterAndSetter with get () = 0 and set (_value : int) = ()

type WithStaticMethod =
    {
        FieldA : string
    }

    static member MyStaticMethod() = ()