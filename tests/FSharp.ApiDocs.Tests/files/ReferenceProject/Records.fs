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
