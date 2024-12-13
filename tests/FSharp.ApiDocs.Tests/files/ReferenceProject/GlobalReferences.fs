module ReferenceProject.GlobalReferences

// Types in this file are used to be referenced by other files in the project.
// This is to test things like type resolution and cross-file linking.

type CallBack = unit -> unit

type UserClass(firstName: string, lastName: string) =
    member this.FirstName = firstName
    member this.LastName = lastName
    member this.FullName = firstName + " " + lastName

type UserRecord =
    {
        FirstName: string
        LastName: string
    }
