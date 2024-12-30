module ReferenceProject.DiscriminatedUnions

type SingleCase =
    | SingleCase of string

type NamedArguments =
    | NamedArguments of prefix: string * indentSize: int

type MultipleCases =
    | Case1
    | Case2 of string
    | Case3 of message : string * callback : (int -> int)