#if INTERACTIVE
#r "../../bin/FSharp.Formatting.CodeFormat.dll"
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/test/FsUnit/lib/net45/FsUnit.NUnit.dll"
#else
[<NUnit.Framework.TestFixture>]
module FSharp.CodeFormat.Tests
#endif

open FsUnit
open NUnit.Framework
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.CodeFormat.Constants
open FsUnitTyped

// --------------------------------------------------------------------------------------
// Initialization - find F# compiler dll, setup formatting CodeFormatter
// --------------------------------------------------------------------------------------

// Check that snippet constains a specific span
let containsSpan f snips =
    snips
    |> Seq.exists (fun (Snippet (_, lines)) -> lines |> Seq.exists (fun (Line (_, spans)) -> spans |> Seq.exists f))

// Check that tool tips contains a specified token
let (|ToolTipWithLiteral|_|) text tips =
    if
        Seq.exists
            (function
            | Literal (tip) -> tip.Contains(text: string)
            | _ -> false)
            tips then
        Some()
    else
        None

// --------------------------------------------------------------------------------------
// Test that some basic things work
// --------------------------------------------------------------------------------------

[<Test>]
let ``Simple code snippet is formatted with tool tips`` () =
    let source = """let hello = 10"""

    let snips, errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    errors |> shouldEqual [||]

    snips
    |> containsSpan (function
        | TokenSpan.Token (_, "hello", Some (ToolTipWithLiteral "val hello : int")) -> true
        | _ -> false)
    |> shouldEqual true

// Note: 'nameof' will not be in preview forever, so this test may need to be updated in the future
[<Test>]
let ``nameof language feature from FSharp Core is supported`` () =
    let source =
        """
let x = 12
nameof x
"""

    let snips, errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    errors |> shouldEqual [||]

    snips
    |> containsSpan (function
        | TokenSpan.Token (_, "nameof", Some (ToolTipWithLiteral "val nameof : 'T -> string")) -> true
        | _ -> false)
    |> shouldEqual true

// Note: applicative CEs won't be in preview forever, so this test may need to change over time
[<Test>]
let ``Preview language feature is supported`` () =
    let source =
        """
// First, define a 'zip' function
module Result =
    let zip x1 x2 =
        match x1,x2 with
        | Ok x1res, Ok x2res -> Ok (x1res, x2res)
        | Error e, _ -> Error e
        | _, Error e -> Error e

// Next, define a builder with 'MergeSources' and 'BindReturn'
type ResultBuilder() =
    member _.MergeSources(t1: Result<'T,'U>, t2: Result<'T1,'U>) = Result.zip t1 t2
    member _.BindReturn(x: Result<'T,'U>, f) = Result.map f x

let result = ResultBuilder()

let run r1 r2 r3 =
    // And here is our applicative!
    let res1: Result<int, string> =
        result {
            let! a = r1
            and! b = r2
            and! c = r3
            return a + b - c
        }

    match res1 with
    | Ok x -> printfn "%s is: %d" (nameof res1) x
    | Error e -> printfn "%s is: %s" (nameof res1) e

let printApplicatives () =
    let r1 = Ok 2
    let r2 = Ok 3 // Error "fail!"
    let r3 = Ok 4

    run r1 r2 r3
    run r1 (Error "failure!") r3
printApplicatives()
"""

    let _, errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    errors |> shouldEqual [||]

let getContentAndToolTip (source: string) =
    let snips, _errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    let res = CodeFormat.FormatHtml(snips, "fstips")
    (Seq.head res.Snippets).Content, res.ToolTip

let getContent = getContentAndToolTip >> fst

[<Test>]
let ``Simple code snippet is formatted as HTML`` () =
    let content, tooltip = getContentAndToolTip """let hello = 10"""

    content
    |> shouldContainText (sprintf "<span class=\"%s\">let</span>" CSS.Keyword)

    content |> shouldContainText ">hello<"

    content |> shouldContainText (sprintf "<span class=\"%s\">10</span>" CSS.Number)

    tooltip |> shouldContainText "val hello : int"

[<Test>]
let ``Non-unicode characters do not cause exception`` () =
    // TODO:  This does not return any snippet text because of F.C.S. error
    // https://github.com/fsharp/FSharp.Compiler.Service/issues/291
    // But at least, it should not cause a crash.
    let source =
        """
// [snippet:16]
✘ let add I J = I+J
// [/snippet]"""

    let _snips, errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    errors.Length |> shouldBeGreaterThan 0
    let (SourceError (_, _, _, msg)) = errors.[0]
    msg |> shouldContainText "✘"

[<Test>]
let ``Plain string is in span of 's' class when it's the last token in the line`` () =
    getContent """let _ = "str" """
    |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" CSS.String)

[<Test>]
let ``Plain string is in span of 's' class, there are several other tokens next to it`` () =
    let content = getContent """let _ = "str", 1 """

    content
    |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" CSS.String)

    content
    |> shouldNotContainText (sprintf "<span class=\"%s\">,</span>" CSS.String)

    content |> shouldContainText ","

[<Test>]
let ``Plain string is in span of 's' class, there is punctuation next to it`` () =
    let content = getContent """let _ = ("str")"""

    content
    |> shouldContainText (sprintf "<span class=\"%s\">(</span>" CSS.Punctuation)

    content
    |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" CSS.String)

    content
    |> shouldContainText (sprintf "<span class=\"%s\">)</span>" CSS.Punctuation)

[<Test>]
let ``Modules and types are in spans of 't' class`` () =
    let content =
        getContent
            """
module Module =
  type Type() = class end
"""

    content |> shouldContainText (sprintf "class=\"%s\">Module</span>" CSS.Module)

    content
    |> shouldContainText (sprintf "class=\"%s\">Type</span>" CSS.ReferenceType)

[<Test>]
let ``Functions and methods are in spans of 'f' class`` () =
    let content =
        getContent
            """
module M =
    type T() =
        let func1 x = ()
        member __.Method x = ()
    let func2 x y = x + y
"""

    content |> shouldContainText (sprintf "class=\"%s\">func1</span>" CSS.Function)

    content |> shouldContainText (sprintf "class=\"%s\">Method</span>" CSS.Function)

    content |> shouldContainText (sprintf "class=\"%s\">func2</span>" CSS.Function)

[<Test>]
let ``Printf formatters are in spans of 'pf' class`` () =
    let content = getContent """let _ = sprintf "a %A b %0.3fD" """

    content |> shouldContainText (sprintf "class=\"%s\">&quot;a </span>" CSS.String)

    content |> shouldContainText (sprintf "class=\"%s\">%%A</span>" CSS.Printf)

    content |> shouldContainText (sprintf "class=\"%s\"> b </span>" CSS.String)

    content |> shouldContainText (sprintf "class=\"%s\">%%0.3f</span>" CSS.Printf)

    content |> shouldContainText (sprintf "class=\"%s\">D&quot;</span>" CSS.String)

[<Test>]
[<Ignore "FCS doesn't currently have semantic highlighting for escaped chars in a string">]
let ``Escaped characters are in spans of 'esc' class`` () =
    let content = getContent """let _ = sprintf "a \n\tD\uA0A0 \t" """

    content |> shouldContainText (sprintf "class=\"%s\">&quot;a </span>" CSS.String)

    content |> shouldContainText (sprintf "class=\"%s\">\\n</span>" CSS.Escaped)

    content |> shouldContainText (sprintf "class=\"%s\">\\t</span>" CSS.Escaped)

    content |> shouldContainText (sprintf "class=\"%s\">D</span>" CSS.String)

    content |> shouldContainText (sprintf "class=\"%s\">\\uA0A0</span>" CSS.Escaped)

    content |> shouldContainText (sprintf "class=\"%s\"> </span>" CSS.String)

    content |> shouldContainText (sprintf "class=\"%s\">\\t</span>" CSS.Escaped)

// --------------------------------------------------------------------------------------
// Test with custom css
// --------------------------------------------------------------------------------------

let customCss kind =
    match kind with
    | TokenKind.Comment -> "Comment"
    | TokenKind.Default -> "Default"
    | TokenKind.Identifier -> "Identifier"
    | TokenKind.Inactive -> "Inactive"
    | TokenKind.Keyword -> "Keyword"
    | TokenKind.Number -> "Number"
    | TokenKind.Operator -> "Operator"
    | TokenKind.Preprocessor -> "Preprocessor"
    | TokenKind.String -> "String"
    | TokenKind.Module -> "Module"
    | TokenKind.ReferenceType -> "ReferenceType"
    | TokenKind.ValueType -> "ValueType"
    | TokenKind.Function -> "Function"
    | TokenKind.Pattern -> "Pattern"
    | TokenKind.MutableVar -> "MutableVar"
    | TokenKind.Printf -> "Printf"
    | TokenKind.Escaped -> "Escaped"
    | TokenKind.Disposable -> "Disposable"
    | TokenKind.TypeArgument -> "TypeArgument"
    | TokenKind.Punctuation -> "Punctuation"
    | TokenKind.Enumeration -> "Enumeration"
    | TokenKind.Interface -> "Interface"
    | TokenKind.Property -> "Property"
    | TokenKind.UnionCase -> "UnionCase"


let getContentAndToolTipCustom (source: string) =
    let snips, _errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    let res = CodeFormat.FormatHtml(snips, "fstips", tokenKindToCss = customCss)

    (Seq.head res.Snippets).Content, res.ToolTip

let getContentCustom = getContentAndToolTipCustom >> fst

[<Test>]
let ``Simple code snippet is formatted as HTML - custom CSS`` () =
    let content, tooltip = getContentAndToolTipCustom """let hello = 10"""

    content |> shouldContainText (sprintf "<span class=\"%s\">let</span>" "Keyword")

    content |> shouldContainText ">hello<"

    content |> shouldContainText (sprintf "<span class=\"%s\">10</span>" "Number")

    tooltip |> shouldContainText "val hello : int"


[<Test>]
let ``Plain string is in span of 's' class when it's the last token in the line - custom CSS`` () =
    getContentCustom """let _ = "str" """
    |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" "String")

[<Test>]
let ``Plain string is in span of 's' class, there are several other tokens next to it - custom CSS`` () =
    let content = getContentCustom """let _ = "str", 1 """

    content
    |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" "String")

    content |> shouldNotContainText (sprintf "<span class=\"%s\">,</span>" "String")

    content |> shouldContainText ","

[<Test>]
let ``Plain string is in span of 's' class, there is punctuation next to it - custom CSS`` () =
    let content = getContentCustom """let _ = ("str")"""

    content
    |> shouldContainText (sprintf "<span class=\"%s\">(</span>" "Punctuation")

    content
    |> shouldContainText (sprintf "<span class=\"%s\">&quot;str&quot;</span>" "String")

    content
    |> shouldContainText (sprintf "<span class=\"%s\">)</span>" "Punctuation")

[<Test>]
let ``Modules and types are in spans of 't' class - custom CSS`` () =
    let content =
        getContentCustom
            """
module Module =
  type Type() = class end
"""

    content |> shouldContainText (sprintf "class=\"%s\">Module</span>" "Module")

    content
    |> shouldContainText (sprintf "class=\"%s\">Type</span>" "ReferenceType")

[<Test>]
let ``Functions and methods are in spans of 'f' class - custom CSS`` () =
    let content =
        getContentCustom
            """
module M =
    type T() =
        let func1 x = ()
        member __.Method x = ()
    let func2 x y = x + y
"""

    content |> shouldContainText (sprintf "class=\"%s\">func1</span>" "Function")

    content |> shouldContainText (sprintf "class=\"%s\">Method</span>" "Function")

    content |> shouldContainText (sprintf "class=\"%s\">func2</span>" "Function")

[<Test>]
let ``Printf formatters are in spans of 'pf' class - custom CSS`` () =
    let content = getContentCustom """let _ = sprintf "a %A b %0.3fD" """

    content |> shouldContainText (sprintf "class=\"%s\">&quot;a </span>" "String")

    content |> shouldContainText (sprintf "class=\"%s\">%%A</span>" "Printf")

    content |> shouldContainText (sprintf "class=\"%s\"> b </span>" "String")

    content |> shouldContainText (sprintf "class=\"%s\">%%0.3f</span>" "Printf")

    content |> shouldContainText (sprintf "class=\"%s\">D&quot;</span>" "String")

[<Test>]
[<Ignore "FCS doesn't currently have semantic highlighting for escaped chars in a string">]
let ``Escaped characters are in spans of 'esc' class - custom CSS`` () =
    let content = getContentCustom """let _ = sprintf "a \n\tD\uA0A0 \t" """

    content |> shouldContainText (sprintf "class=\"%s\">&quot;a </span>" "String")

    content |> shouldContainText (sprintf "class=\"%s\">\\n</span>" "Escaped")

    content |> shouldContainText (sprintf "class=\"%s\">\\t</span>" "Escaped")

    content |> shouldContainText (sprintf "class=\"%s\">D</span>" "String")

    content |> shouldContainText (sprintf "class=\"%s\">\\uA0A0</span>" "Escaped")

    content |> shouldContainText (sprintf "class=\"%s\"> </span>" "String")

    content |> shouldContainText (sprintf "class=\"%s\">\\t</span>" "Escaped")

let getLatex (source: string) =
    let snips, _errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    let res = CodeFormat.FormatLatex(snips)
    (Seq.head res.Snippets).Content

[<Test>]
let ``Simple code snippet is formatted as Latex`` () =
    let content = getLatex """let hello = 10"""

    content |> shouldContainText (sprintf @"\begin{Verbatim}")

    content |> shouldContainText (sprintf @"\kwd{let} \id{hello} \ops{=} \num{10}")

    content |> shouldContainText (sprintf @"\end{Verbatim}")

let getFsx (source: string) =
    let snips, _errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    let res = CodeFormat.FormatFsx(snips)
    (Seq.head res.Snippets).Content

[<Test>]
let ``Simple code snippet is formatted as code cell content`` () =
    let content = getFsx """let hello = 10"""
    content |> shouldEqual "let hello = 10"
