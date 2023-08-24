namespace AttributeTestNamespace

open System

type TestAttribute(?int: int, ?string: string, ?array: string array) =
    inherit Attribute()
    new() = TestAttribute()
    member val String = defaultArg string "" with get, set
    member val Int = defaultArg int 0 with get, set
    member val Array = defaultArg array [||] with get, set

type IntTestAttribute(_int: int) =
    inherit Attribute()

type BoolTestAttribute(_bool: bool) =
    inherit Attribute()

type StringTestAttribute(_string: string) =
    inherit Attribute()

type ArrayTestAttribute(_array: string array) =
    inherit Attribute()

type MultipleTestAttribute(_string: string, _int: int, _array: int array) =
    inherit Attribute()

[<Obsolete>]
module SingleAttributeModule =
    let x = 1

[<Obsolete("obsolete")>]
module SingleAttributeWithArgumentModule =
    let x = 1

[<TestAttribute(Int = 1, String = "test", Array = [| "1"; "2" |])>]
module SingleAttributeWithNamedArgumentsModule =
    let x = 1

[<TestAttribute>]
[<Obsolete>]
[<AutoOpen>]
module MultipleAttributesModule =
    let x = 1

[<TestAttribute>]
type AttributeInterface =
    [<TestAttribute>]
    abstract TestMember: int -> int -> int

[<TestAttribute>]
type AttributeClass() =
    [<TestAttribute(String = "ctor")>]
    new(_i: int) = AttributeClass()

    [<TestAttribute>]
    member _.TestMember = 1

    [<TestAttribute>]
    static member TestStaticMember = 2

type AttributeRecord =
    { [<TestAttribute(String = "record")>]
      TestField: int }

type AttributeUnion =
    | [<TestAttribute(String = "union")>] TestCase
    | TestCase2 of int

module ContentTestModule =
    [<TestAttribute>]
    let testValue = 1

    [<TestAttribute>]
    let testFunction a = a + 1


module FormatTestModule =
    [<TestAttribute>]
    let noArguments = 0

    [<IntTestAttribute(1)>]
    let singleIntArgument = 0

    [<StringTestAttribute("test")>]
    let singleStringArgument = 0

    [<ArrayTestAttribute([| "test1"; "test2" |])>]
    let singleArrayArgument = 0

    [<BoolTestAttribute(true)>]
    let singleBoolArgument = 0

    [<MultipleTestAttribute("test", 1, [| 1; 2 |])>]
    let multipleArguments = 0

    [<TestAttribute(Int = 1, String = "test", Array = [| "1"; "2" |])>]
    let multipleNamedArguments = 0

    [<TestAttribute>]
    [<IntTestAttribute(1)>]
    [<StringTestAttribute("test")>]
    let multipleAttributes = 0

module ObsoleteTestModule =
    [<Obsolete>]
    let noMessage = 0

    [<Obsolete("obsolete")>]
    let withMessage = 0

    let notObsolete = 0
