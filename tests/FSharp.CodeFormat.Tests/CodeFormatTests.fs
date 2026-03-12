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
    |> Seq.exists (fun (Snippet(_, lines)) -> lines |> Seq.exists (fun (Line(_, spans)) -> spans |> Seq.exists f))

// Check that tool tips contains a specified token
[<return: Struct>]
let (|ToolTipWithLiteral|_|) text tips =
    if
        Seq.exists
            (function
            | Literal(tip) -> tip.Contains(text: string)
            | _ -> false)
            tips
    then
        ValueSome()
    else
        ValueNone

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
        | TokenSpan.Token(_, "hello", Some(ToolTipWithLiteral "val hello: int")) -> true
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
        | TokenSpan.Token(_, "nameof", Some(ToolTipWithLiteral "val nameof: 'T -> string")) -> true
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

    tooltip |> shouldContainText "val hello: int"

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
    let (SourceError(_, _, _, msg)) = errors.[0]
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
        member _.Method x = ()
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

    tooltip |> shouldContainText "val hello: int"


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
        member _.Method x = ()
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

// --------------------------------------------------------------------------------------
// Tests for parameter attribute stripping in tooltips (issue #858)
// --------------------------------------------------------------------------------------

open FSharp.Formatting.CodeFormat.ToolTipReader

[<Test>]
let ``stripParameterAttributes removes Optional attribute annotation`` () =
    stripParameterAttributes "[<Optional>] x: obj" |> shouldEqual " x: obj"

[<Test>]
let ``stripParameterAttributes removes DefaultParameterValue attribute annotation`` () =
    stripParameterAttributes "[<DefaultParameterValue(null)>] x: obj"
    |> shouldEqual " x: obj"

[<Test>]
let ``stripParameterAttributes removes attribute annotation with leading whitespace`` () =
    stripParameterAttributes "    [<Optional>]" |> shouldEqual ""

[<Test>]
let ``stripParameterAttributes leaves array types unaffected`` () =
    stripParameterAttributes "x: int[]" |> shouldEqual "x: int[]"

[<Test>]
let ``stripParameterAttributes leaves plain parameter type unaffected`` () =
    stripParameterAttributes "member Foo.Bar: x: int -> int"
    |> shouldEqual "member Foo.Bar: x: int -> int"

[<Test>]
let ``stripParameterAttributes removes inline attribute from method signature`` () =
    stripParameterAttributes "member Foo.Bar: [<Optional>] x: obj -> obj"
    |> shouldEqual "member Foo.Bar: x: obj -> obj"

[<Test>]
let ``Parameter attribute annotations are stripped from HTML tooltips`` () =
    let source =
        """
open System.Runtime.InteropServices

type MyClass() =
    member _.Method([<Optional; DefaultParameterValue(null: obj)>] x: obj) = x

let c = MyClass()
c.Method()
"""

    let _content, tooltip = getContentAndToolTip source
    // Attribute annotations should not appear in rendered tooltips
    tooltip |> shouldNotContainText "[&lt;Optional"
    tooltip |> shouldNotContainText "[&lt;DefaultParameterValue"

// --------------------------------------------------------------------------------------
// Tests for cross-assembly type resolution in tooltips (issue #1085)
// --------------------------------------------------------------------------------------

/// Find the directory where the currently executing test assembly lives.
/// Both CrossAssemblyA.dll and CrossAssemblyB.dll are copied there as project references.
let private testBinDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)

/// Return the tooltip text for the first token named `tokenText` that carries a tip.
let private tipForToken (tokenText: string) (snips: Snippet[]) =
    snips
    |> Seq.tryPick (fun (Snippet(_, lines)) ->
        lines
        |> Seq.tryPick (fun (Line(_, spans)) ->
            spans
            |> Seq.tryPick (function
                | TokenSpan.Token(_, t, Some tips) when t = tokenText -> Some tips
                | _ -> None)))

/// Render tooltip spans to a single concatenated string for easy assertion.
let private renderTips (spans: ToolTipSpans) =
    spans
    |> List.map (function
        | Literal s -> s
        | Emphasis inner ->
            inner
            |> List.map (function
                | Literal s -> s
                | _ -> "")
            |> String.concat ""
        | HardLineBreak -> "\n")
    |> String.concat ""

[<Test>]
let ``Cross-assembly DU field type is shown as actual type name not obj in tooltip`` () =
    // Reproduce issue #1085: `Subject.Account of Did` should show `Did`, not `obj`
    let assemblyAPath = System.IO.Path.Combine(testBinDir, "CrossAssemblyA.dll")
    let assemblyBPath = System.IO.Path.Combine(testBinDir, "CrossAssemblyB.dll")

    let source =
        $"""#r "{assemblyAPath}"
#r "{assemblyBPath}"
open CrossAssemblyA
open CrossAssemblyB

let subject = Subject.Account (CrossAssemblyA.Did.create "test")
"""

    let snips, errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    errors |> shouldEqual [||]

    // Find the tooltip for the `Subject` identifier and check its text
    let subjectTip =
        snips
        |> tipForToken "Subject"
        |> Option.map renderTips
        |> Option.defaultValue ""

    subjectTip |> shouldNotEqual ""

    // The tooltip for Subject should show the actual DU case type, not `obj`
    subjectTip |> shouldNotContainText "obj"
    subjectTip |> shouldContainText "Did"

[<Test>]
let ``Cross-assembly record field types are shown as actual type names not obj in tooltip`` () =
    // Reproduce issue #1085: `TeamMember` record fields referencing types from another
    // assembly should show the correct type names (`Did`, `DateTimeOffset option`)
    // rather than `obj`.
    let assemblyAPath = System.IO.Path.Combine(testBinDir, "CrossAssemblyA.dll")
    let assemblyBPath = System.IO.Path.Combine(testBinDir, "CrossAssemblyB.dll")

    let source =
        $"""#r "{assemblyAPath}"
#r "{assemblyBPath}"
open CrossAssemblyB

let m : TeamMember = Unchecked.defaultof<TeamMember>
"""

    let snips, errors = CodeFormatter.ParseAndCheckSource("/somewhere/test.fsx", source.Trim(), None, None, ignore)

    errors |> shouldEqual [||]

    let teamMemberTip =
        snips
        |> tipForToken "TeamMember"
        |> Option.map renderTips
        |> Option.defaultValue ""

    teamMemberTip |> shouldNotEqual ""

    // Record field types from a different assembly should not appear as `obj`
    teamMemberTip |> shouldContainText "Did"
    teamMemberTip |> shouldContainText "DateTimeOffset"

[<Test>]
let ``Cross-assembly DU field types are correct when using relative hash-r references`` () =
    // Reproduce the user scenario from issue #1085: using relative #r paths where the
    // script file is in the same directory as the referenced assemblies.
    // Set up a temporary directory containing the DLLs and a script path pointing there.
    let tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fsharp-formatting-issue1085")
    System.IO.Directory.CreateDirectory(tempDir) |> ignore

    try
        // Copy the test assemblies to the temp directory so relative #r can resolve them.
        let srcA = System.IO.Path.Combine(testBinDir, "CrossAssemblyA.dll")
        let srcB = System.IO.Path.Combine(testBinDir, "CrossAssemblyB.dll")
        System.IO.File.Copy(srcA, System.IO.Path.Combine(tempDir, "CrossAssemblyA.dll"), overwrite = true)
        System.IO.File.Copy(srcB, System.IO.Path.Combine(tempDir, "CrossAssemblyB.dll"), overwrite = true)

        // Script path is inside the temp dir; FCS uses this to resolve relative #r paths.
        let scriptPath = System.IO.Path.Combine(tempDir, "test.fsx")

        let source =
            """#r "CrossAssemblyA.dll"
#r "CrossAssemblyB.dll"
open CrossAssemblyA
open CrossAssemblyB

let subject = Subject.Account (CrossAssemblyA.Did.create "test")
"""

        let snips, errors = CodeFormatter.ParseAndCheckSource(scriptPath, source.Trim(), None, None, ignore)

        errors |> shouldEqual [||]

        let subjectTip =
            snips
            |> tipForToken "Subject"
            |> Option.map renderTips
            |> Option.defaultValue ""

        subjectTip |> shouldNotEqual ""

        // With correctly-resolved relative paths the tooltip should show Did, not obj.
        subjectTip |> shouldNotContainText "of obj"
        subjectTip |> shouldContainText "Did"

    finally
        // Clean up
        try
            System.IO.Directory.Delete(tempDir, recursive = true)
        with _ ->
            ()
