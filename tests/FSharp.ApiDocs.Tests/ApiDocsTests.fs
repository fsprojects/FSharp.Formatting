[<NUnit.Framework.TestFixture>]
module ApiDocs.Tests

open System
open FsUnit
open System.IO
open NUnit.Framework
open FSharp.Formatting.ApiDocs
open FSharp.Formatting.Templating
open FsUnitTyped

// --------------------------------------------------------------------------------------
// Run the metadata formatter on sample project
// --------------------------------------------------------------------------------------

type OutputFormat =
    | Html
    | Markdown

    member x.Extension =
        match x with
        | Html -> "html"
        | Markdown -> "md"

    member x.ExtensionInUrl =
        match x with
        | Html -> ".html"
        | Markdown -> ""

type DocsGenerator(format: OutputFormat) =
    member _.Run(input, output, collectionName, template, substitutions, ?libDirs, ?root) =
        let root = defaultArg root "/"
        let libDirs = defaultArg libDirs []

        match format with
        | Html ->
            ApiDocs.GenerateHtml(
                input,
                output,
                collectionName = collectionName,
                template = template,
                substitutions = substitutions,
                libDirs = libDirs,
                root = root
            )
        | Markdown ->
            ApiDocs.GenerateMarkdown(
                input,
                output,
                collectionName = collectionName,
                template = template,
                substitutions = substitutions,
                libDirs = libDirs,
                root = root
            )

let formats = [ Html; Markdown ]

let (</>) a b = Path.Combine(a, b)
let fullpath = Path.GetFullPath
let fullpaths = List.map fullpath

let root = __SOURCE_DIRECTORY__ |> fullpath


// NOTE - For these tests to run properly they require the output of all the metadata
// test project to be directed to the directory below
let testBin = AttributeTests.testBin

let getOutputDir (format: OutputFormat) (uniq: string) =
    let outDir = __SOURCE_DIRECTORY__ + "/output/" + format.Extension + "/" + uniq

    while (try
               Directory.Exists outDir
           with _ ->
               false) do
        Directory.Delete(outDir, true)

    Directory.CreateDirectory(outDir).FullName

let removeWhiteSpace (str: string) =
    str.Replace("\n", "").Replace("\r", "").Replace(" ", "")

let docTemplate (format: OutputFormat) =
    root </> (sprintf "../../docs/_template.%s" format.Extension)

let substitutions =
    [ ParamKeys.``fsdocs-collection-name``, "F# TestProject"
      ParamKeys.``fsdocs-authors``, "Your Name"
      ParamKeys.``fsdocs-repository-link``, "http://github.com/fsprojects/fsharp-test-project"
      ParamKeys.root, "/root/"
      ParamKeys.``fsdocs-favicon-src``, "img/favicon.ico" ]

let generateApiDocs (libraries: string list) (format: OutputFormat) useMdComments uniq =
    try
        let output = getOutputDir format uniq

        let inputs = [ for x in libraries -> ApiDocInput.FromFile(x, mdcomments = useMdComments) ]

        let _metadata =
            DocsGenerator(format)
                .Run(
                    inputs,
                    output = output,
                    collectionName = "Collection",
                    template = docTemplate format,
                    substitutions = substitutions,
                    libDirs = [ root ]
                )

        let fileNames = Directory.GetFiles(output </> "reference")

        let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

        files

    with e ->
        printfn "Failed to Generate API Docs -\n%s\n\n%s\n" e.Message e.StackTrace

        System.AppDomain.CurrentDomain.GetAssemblies()
        |> Seq.iter (fun x ->
            try
                sprintf "%s\n - %s" x.FullName x.Location |> System.Console.WriteLine
            with e ->
                sprintf "\nError On - %A\n -- %s\n" x e.Message |> System.Console.WriteLine)

        reraise ()

do FSharp.Formatting.TestHelpers.enableLogging ()

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs seealso can find members`` (format: OutputFormat) =
    let library = testBin </> "TestLib3.dll" |> fullpath

    let files = generateApiDocs [ library ] format false "TestLib3"

    let (textA, textB) =
        if format = OutputFormat.Html then
            "seealso.html#disposeOnUnmount", "seealso.html#unsubscribeOnUnmount"
        else
            "seealso#disposeOnUnmount", "seealso#unsubscribeOnUnmount"

    files.[(sprintf "test-seealso.%s" format.Extension)] |> shouldContainText textA

    files.[(sprintf "test-seealso.%s" format.Extension)] |> shouldContainText textB

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs excludes items`` (format: OutputFormat) =
    let library = testBin </> "TestLib3.dll" |> fullpath

    let files = generateApiDocs [ library ] format false "TestLib3"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldContainText "Returns unit"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeOmitted"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded1"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded2"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded3"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded4"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded5"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded6"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcluded7"

    files.[(sprintf "fslib-partiallydocumented.%s" format.Extension)]
    |> shouldNotContainText "shouldBeExcludedCompilerHidden"

    // We can only expect a warning for "wishItWasExcluded1" & "WishItWasExcluded2"

    files.ContainsKey(sprintf "fslib-partiallydocumented-notdocumented1.%s" format.Extension)
    |> shouldEqual false

    files.ContainsKey(sprintf "fslib-partiallydocumented-notdocumented2.%s" format.Extension)
    |> shouldEqual false

    files.ContainsKey(sprintf "fslib-partiallydocumented-notdocumented3.%s" format.Extension)
    |> shouldEqual false

    files.ContainsKey(sprintf "fslib-undocumentedmodule.%s" format.Extension)
    |> shouldEqual false

    files.ContainsKey(sprintf "test-dom.%s" format.Extension) |> shouldEqual true

    files.ContainsKey(sprintf "test-dom-domaction.%s" format.Extension)
    |> shouldEqual false

    files.[(sprintf "test-dom.%s" format.Extension)]
    |> shouldNotContainText "DomAction"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs works on sample Deedle assembly`` (format: OutputFormat) =
    let library = root </> "files" </> "Deedle.dll"
    let output = getOutputDir format "Deedle"

    let input =
        ApiDocInput.FromFile(
            library,
            mdcomments = true,
            sourceRepo = "https://github.com/fslaborg/Deedle/",
            sourceFolder = "c:/dev/FSharp.DataFrame"
        )

    let _model, _index =
        DocsGenerator(format)
            .Run(
                [ input ],
                output,
                collectionName = "Deedle",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = [ testBin ]
            )

    let files = Directory.GetFiles(output </> "reference")

    let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith(sprintf "index.%s" format.Extension))

    optIndex.IsSome |> shouldEqual true

    let optSeriesMod = files |> Seq.tryFind (fun s -> s.Contains "seriesmodule")

    optSeriesMod.IsSome |> shouldEqual true

[<Test; Ignore "Ignore by default to make tests run reasonably fast">]
[<TestCaseSource("formats")>]
let ``ApiDocs works on sample FAKE assembly`` (format: OutputFormat) =
    let library = root </> "files" </> "FAKE" </> "FakeLib.dll"

    let output = getOutputDir format "FakeLib"

    let input = ApiDocInput.FromFile(library, mdcomments = true)

    let _model, _index =
        DocsGenerator(format)
            .Run(
                [ input ],
                output,
                collectionName = "FAKE",
                template = docTemplate format,
                substitutions = substitutions
            )

    let files = Directory.GetFiles(output </> "reference")

    files |> Seq.length |> shouldEqual 166

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs works on two sample F# assemblies`` (format: OutputFormat) =
    let libraries = [ testBin </> "FsLib1.dll"; testBin </> "FsLib2.dll" ]

    let output = getOutputDir format "FsLib12"

    let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = true, substitutions = substitutions) ]

    let _model, searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "FsLib",
                template = docTemplate format,
                root = "http://root.io/root/",
                substitutions = substitutions,
                libDirs = [ testBin ]
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    // Check that all comments appear in the output
    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldContainText "Readonly int property"

    files.[(sprintf "fslib-record.%s" format.Extension)]
    |> shouldContainText "This is name"

    files.[(sprintf "fslib-record.%s" format.Extension)]
    |> shouldContainText "Additional member"

    files.[(sprintf "fslib-union.%s" format.Extension)]
    |> shouldContainText "Hello of int"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "Sample class"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "Union sample"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "Record sample"

    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText "Somewhat nested type"

    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText "Somewhat nested module"

    files.[(sprintf "fslib-nested-nestedtype.%s" format.Extension)]
    |> shouldContainText "Very nested member"

    files.[(sprintf "fslib-nested-submodule.%s" format.Extension)]
    |> shouldContainText "Very nested field"

    // Check that union fields are correctly generated
    files.[(sprintf "fslib-union.%s" format.Extension)]
    |> shouldContainText "<span>World(<span>string,&#32;int</span>)</span>"

    files.[(sprintf "fslib-union.%s" format.Extension)]
    |> shouldContainText "<span>Naming(<span>rate,&#32;string</span>)</span>"

    // Check that operators are encoded

    // arg0 <&> arg1
    files[$"fslib-operatorswithfsi.%s{format.Extension}"]
    |> shouldContainText "arg0&#32;&lt;&amp;&gt;&#32;arg1"

    // x ?<? y
    files[$"fslib-operatorswithfsi.%s{format.Extension}"]
    |> shouldContainText "x&#32;?&lt;?&#32;y"

    // <?arg0
    files[$"fslib-operatorswithfsi.%s{format.Extension}"]
    |> shouldContainText "&lt;?arg0"

    // <?>x
    files[$"fslib-operatorswithfsi.%s{format.Extension}"]
    |> shouldContainText "&lt;?&gt;x"

    (* This may be addressed in a separate issue or removed if not an issue.
  // Check that implict cast operator is generated correctly
  files.[(sprintf "fslib-space-missing-implicit-cast.%s" format.Extension)] |> shouldContainText "<code><span>op_Implicit&#32;<span>source</span></span></code>"
  files.[(sprintf "fslib-space-missing-implicit-cast.%s" format.Extension)] |> match format with
                                                                               | Html -> shouldContainText "<code><span>!|>&#32;<span>value</span></span></code>"
                                                                               | Markdown -> shouldContainText "<code><span>!&#124;>&#32;<span>value</span></span></code>"
  *)

    (*
  // Check that methods with no arguments are correctly generated (#113)
  files.[(sprintf "fslib-record.%s" format.Extension)] |> shouldNotContainText "Foo2(arg1)"
  files.[(sprintf "fslib-record.%s" format.Extension)] |> shouldContainText "Foo2()"
  files.[(sprintf "fslib-record.%s" format.Extension)] |> shouldContainText "Signature"
  files.[(sprintf "fslib-record.%s" format.Extension)] |> shouldContainText "unit -&gt; int"
  files.[(sprintf "fslib-class.%s" format.Extension)] |> shouldContainText "Class()"
  files.[(sprintf "fslib-class.%s" format.Extension)] |> shouldContainText "unit -&gt; Class"

  // Check that properties are correctly generated (#114)
  files.[(sprintf "fslib-class.%s" format.Extension)] |> removeWhiteSpace |> shouldNotContainText ">this.Member(arg1)<"
  files.[(sprintf "fslib-class.%s" format.Extension)] |> removeWhiteSpace |> shouldNotContainText ">this.Member()<"
  files.[(sprintf "fslib-class.%s" format.Extension)] |> removeWhiteSpace |> shouldContainText ">this.Member<"
  files.[(sprintf "fslib-class.%s" format.Extension)] |> shouldNotContainText "unit -&gt; int"
  //files.[(sprintf "fslib-class.%s" format.Extension)] |> shouldContainText "Signature:"

  // Check that formatting is correct
  files.[(sprintf "fslib-test_issue472_r.%s" format.Extension)] |> shouldContainText "Test_Issue472_R.fmultipleargs x y"
  files.[(sprintf "fslib-test_issue472_r.%s" format.Extension)] |> shouldContainText "Test_Issue472_R.ftupled(x, y)"
  files.[(sprintf "fslib-test_issue472.%s" format.Extension)] |> shouldContainText "fmultipleargs x y"
  files.[(sprintf "fslib-test_issue472.%s" format.Extension)] |> shouldContainText "ftupled(x, y)"
  files.[(sprintf "fslib-test_issue472_t.%s" format.Extension)] |> shouldContainText "this.MultArg(arg1, arg2)"
  files.[(sprintf "fslib-test_issue472_t.%s" format.Extension)] |> shouldContainText "this.MultArgTupled(arg)"
  files.[(sprintf "fslib-test_issue472_t.%s" format.Extension)] |> shouldContainText "this.MultPartial arg1 arg2"

*)
    let indxTxt = System.Text.Json.JsonSerializer.Serialize searchIndex

    // Test a few entries in the search index
    indxTxt |> shouldContainText "\"uri\""
    indxTxt |> shouldContainText "\"content\""
    indxTxt |> shouldContainText "\"title\""

    indxTxt
    |> shouldContainText (
        sprintf "http://root.io/root/reference/fslib-nested-submodule-verynestedtype%s#Member" format.ExtensionInUrl
    )

    indxTxt
    |> shouldContainText (sprintf "http://root.io/root/reference/fslib-test_issue472_t%s#MultArg" format.ExtensionInUrl)

    indxTxt |> shouldContainText """ITest_Issue229.Name \nName \n"""

[<Test>]
[<TestCaseSource("formats")>]
let ``Namespace summary generation works on two sample F# assemblies using XML docs`` (format: OutputFormat) =
    let libraries = [ testBin </> "TestLib1.dll"; testBin </> "TestLib2.dll" ]

    let output = getOutputDir format "TestLib12_Namespaces"

    let inputs =
        [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = false, substitutions = substitutions) ]

    let _model, _searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "TestLibs",
                template = docTemplate format,
                root = "http://root.io/root/",
                substitutions = substitutions,
                libDirs = [ testBin ]
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    files.[(sprintf "index.%s" format.Extension)]
    |> shouldContainText "FsLib is a good namespace"

    files.[(sprintf "index.%s" format.Extension)]
    |> shouldNotContainText "I tell you again FsLib is good"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "FsLib is a good namespace"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "I tell you again FsLib is good"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs model generation works on two sample F# assemblies`` (_format: OutputFormat) =
    let libraries = [ testBin </> "FsLib1.dll"; testBin </> "FsLib2.dll" ]

    let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = true) ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    model.Collection.Assemblies.Length |> shouldEqual 2

    model.Collection.Assemblies.[0].Name |> shouldEqual "FsLib1"

    model.Collection.Assemblies.[1].Name |> shouldEqual "FsLib2"

    model.Collection.Namespaces.Length |> shouldEqual 1

    model.Collection.Namespaces.[0].Name |> shouldEqual "FsLib"

    model.Collection.Namespaces.[0].Entities
    |> List.filter (fun c -> c.IsTypeDefinition)
    |> function
        | x -> x.Length |> shouldEqual 13

    let assemblies = [ for t in model.Collection.Namespaces.[0].Entities -> t.Assembly.Name ]

    assemblies |> List.distinct |> List.sort |> shouldEqual [ "FsLib1"; "FsLib2" ]

[<Test>]
let ``ApiDocs ReturnInfo.ReturnType is Some for properties with setters (issue 734)`` () =
    let libraries = [ testBin </> "FsLib2.dll" ]
    let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = true) ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let typeEntity =
        model.Collection.Namespaces.[0].Entities
        |> List.find (fun e -> e.Name = "Test_Issue734")

    let members = typeEntity.AllMembers

    let getReturnType name =
        members
        |> List.tryFind (fun m -> m.Name = name)
        |> Option.bind (fun m -> m.ReturnInfo.ReturnType)

    // Getter-only: ReturnType should be Some string
    getReturnType "GetterOnly" |> Option.isSome |> shouldEqual true

    // Getter+setter: ReturnType should be Some string
    getReturnType "GetterAndSetter" |> Option.isSome |> shouldEqual true

    // Setter-only: ReturnType should be Some string (value type)
    getReturnType "SetterOnly" |> Option.isSome |> shouldEqual true

[<Test>]
let ``ApiDocs InheritedMembers is populated for derived types (issue 590)`` () =
    let libraries = [ testBin </> "FsLib2.dll" ]
    let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = false) ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let derivedEntity =
        model.Collection.Namespaces.[0].Entities
        |> List.tryFind (fun e -> e.Name = "DerivedClassForInheritance")

    derivedEntity.IsSome |> shouldEqual true

    let inherited = derivedEntity.Value.InheritedMembers

    // Should have at least one inherited group (from BaseClassForInheritance)
    inherited.IsEmpty |> shouldEqual false

    let _, members = inherited.[0]

    let memberNames = members |> List.map (fun m -> m.Name) |> List.sort
    memberNames |> shouldContain "BaseMethod"
    memberNames |> shouldContain "BaseStaticMethod"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs renders inherited members section in output (issue 590)`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath
    let files = generateApiDocs [ library ] format false "FsLib2_inherited"

    let derivedKey = files.Keys |> Seq.tryFind (fun k -> k.Contains("derivedclassforinheritance"))

    derivedKey.IsSome |> shouldEqual true

    files.[derivedKey.Value] |> shouldContainText "Inherited"
    files.[derivedKey.Value] |> shouldContainText "BaseClassForInheritance"
    files.[derivedKey.Value] |> shouldContainText "BaseMethod"
    files.[derivedKey.Value] |> shouldContainText "BaseStaticMethod"

[<Test>]
let ``ApiDocs ShowInheritedMembers false suppresses InheritedMembers on model (issue 590)`` () =
    let libraries = [ testBin </> "FsLib2.dll" ]

    let inputs =
        [ for lib in libraries ->
              { ApiDocInput.FromFile(lib, mdcomments = false) with
                  ShowInheritedMembers = false } ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let derivedEntity =
        model.Collection.Namespaces.[0].Entities
        |> List.tryFind (fun e -> e.Name = "DerivedClassForInheritance")

    derivedEntity.IsSome |> shouldEqual true

    // With ShowInheritedMembers = false the inherited list should be empty
    derivedEntity.Value.InheritedMembers.IsEmpty |> shouldEqual true

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs ShowInheritedMembers false suppresses inherited section in output (issue 590)`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath
    let output = getOutputDir format "FsLib2_no_inherited"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              ShowInheritedMembers = false } ]

    let _metadata =
        DocsGenerator(format)
            .Run(
                inputs,
                output = output,
                collectionName = "Collection",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = [ root ]
            )

    let fileNames = Directory.GetFiles(output </> "reference")
    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
    let derivedKey = files.Keys |> Seq.tryFind (fun k -> k.Contains("derivedclassforinheritance"))

    derivedKey.IsSome |> shouldEqual true

    // The inherited-from section should NOT appear
    files.[derivedKey.Value] |> shouldNotContainText "Inherited from"
    // The base class's members should not appear as inherited
    files.[derivedKey.Value] |> shouldNotContainText "BaseMethod"

[<Test>]
let ``ApiDocs TypeConstraintDisplayMode None hides constraints on model`` () =
    let library = testBin </> "FsLib2.dll"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.None } ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let constraintModule =
        model.Collection.Namespaces
        |> List.collect (fun ns -> ns.Entities)
        |> List.tryFind (fun e -> e.Name = "TypeConstraintTests")

    constraintModule.IsSome |> shouldEqual true

    let requiresEqualityMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresEquality")

    requiresEqualityMember.IsSome |> shouldEqual true
    requiresEqualityMember.Value.Constraints |> shouldEqual []

[<Test>]
let ``ApiDocs TypeConstraintDisplayMode Short shows constraints inline (default)`` () =
    let library = testBin </> "FsLib2.dll"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.Short } ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let constraintModule =
        model.Collection.Namespaces
        |> List.collect (fun ns -> ns.Entities)
        |> List.tryFind (fun e -> e.Name = "TypeConstraintTests")

    constraintModule.IsSome |> shouldEqual true

    let requiresEqualityMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresEquality")

    requiresEqualityMember.IsSome |> shouldEqual true
    requiresEqualityMember.Value.Constraints |> shouldNotEqual []

    requiresEqualityMember.Value.TypeConstraintDisplayMode
    |> shouldEqual TypeConstraintDisplayMode.Short

    // FormatTypeConstraints returns full form "'T : equality"
    requiresEqualityMember.Value.FormatTypeConstraints.IsSome |> shouldEqual true

    requiresEqualityMember.Value.FormatTypeConstraints.Value
    |> shouldContainText "equality"

    // FormatShortTypeConstraints returns abbreviated form "equality" (no type-variable prefix)
    requiresEqualityMember.Value.FormatShortTypeConstraints.IsSome
    |> shouldEqual true

    requiresEqualityMember.Value.FormatShortTypeConstraints.Value
    |> shouldEqual "equality"

    let requiresComparisonMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresComparison")

    requiresComparisonMember.IsSome |> shouldEqual true
    requiresComparisonMember.Value.Constraints |> shouldNotEqual []

    requiresComparisonMember.Value.FormatShortTypeConstraints.Value
    |> shouldEqual "comparison"

[<Test>]
let ``ApiDocs TypeConstraintDisplayMode Short FormatShortTypeConstraints abbreviates constraints correctly`` () =
    let library = testBin </> "FsLib2.dll"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.Short } ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let constraintModule =
        model.Collection.Namespaces
        |> List.collect (fun ns -> ns.Entities)
        |> List.tryFind (fun e -> e.Name = "TypeConstraintTests")

    constraintModule.IsSome |> shouldEqual true

    // Coercion constraint: FormatShortTypeConstraints should give ":> System.IComparable"
    let requiresCoercionMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresCoercion")

    requiresCoercionMember.IsSome |> shouldEqual true
    requiresCoercionMember.Value.Constraints |> shouldNotEqual []

    // Full form contains the type variable prefix
    requiresCoercionMember.Value.FormatTypeConstraints.Value
    |> shouldContainText ":>"

    // Short form strips the type variable prefix but keeps the operator
    requiresCoercionMember.Value.FormatShortTypeConstraints.Value
    |> shouldContainText ":>"

    requiresCoercionMember.Value.FormatShortTypeConstraints.Value
    |> shouldContainText "IComparable"

    // FormatShortTypeConstraints should NOT contain the type variable prefix "'T"
    requiresCoercionMember.Value.FormatShortTypeConstraints.Value
    |> shouldNotContainText "'T"

    // Struct constraint
    let requiresStructMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresStruct")

    requiresStructMember.IsSome |> shouldEqual true

    requiresStructMember.Value.Constraints |> shouldNotEqual []

    requiresStructMember.Value.FormatShortTypeConstraints.Value
    |> shouldContainText "struct"

    requiresStructMember.Value.FormatShortTypeConstraints.Value
    |> shouldNotContainText "'T"

[<Test>]
let ``ApiDocs TypeConstraintDisplayMode Full shows constraints in separate section`` () =
    let library = testBin </> "FsLib2.dll"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.Full } ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let constraintModule =
        model.Collection.Namespaces
        |> List.collect (fun ns -> ns.Entities)
        |> List.tryFind (fun e -> e.Name = "TypeConstraintTests")

    constraintModule.IsSome |> shouldEqual true

    let requiresEqualityMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresEquality")

    requiresEqualityMember.IsSome |> shouldEqual true
    requiresEqualityMember.Value.Constraints |> shouldNotEqual []

    requiresEqualityMember.Value.TypeConstraintDisplayMode
    |> shouldEqual TypeConstraintDisplayMode.Full

    // Full mode: FormatTypeConstraints gives full form "'T : equality" (with type-variable prefix)
    requiresEqualityMember.Value.FormatTypeConstraints.Value
    |> shouldContainText "'T"

    requiresEqualityMember.Value.FormatTypeConstraints.Value
    |> shouldContainText "equality"

[<Test>]
let ``ApiDocs TypeConstraintDisplayMode default is Short`` () =
    let library = testBin </> "FsLib2.dll"

    // Use default - should be Short
    let inputs = [ ApiDocInput.FromFile(library, mdcomments = false) ]

    let model =
        ApiDocs.GenerateModel(inputs, collectionName = "FsLib", substitutions = substitutions, libDirs = [ testBin ])

    let constraintModule =
        model.Collection.Namespaces
        |> List.collect (fun ns -> ns.Entities)
        |> List.tryFind (fun e -> e.Name = "TypeConstraintTests")

    constraintModule.IsSome |> shouldEqual true

    let requiresEqualityMember =
        constraintModule.Value.ValuesAndFuncs
        |> List.tryFind (fun m -> m.Name = "requiresEquality")

    requiresEqualityMember.IsSome |> shouldEqual true
    // Default is Short - constraints are computed
    requiresEqualityMember.Value.TypeConstraintDisplayMode
    |> shouldEqual TypeConstraintDisplayMode.Short

    requiresEqualityMember.Value.Constraints |> shouldNotEqual []

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs TypeConstraintDisplayMode Short renders 'requires' form inline in output`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath
    let output = getOutputDir format "FsLib2_constraints_short"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.Short } ]

    let _metadata =
        DocsGenerator(format)
            .Run(
                inputs,
                output = output,
                collectionName = "Collection",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = [ root ]
            )

    let fileNames = Directory.GetFiles(output </> "reference")
    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
    // Find the module page specifically (not sub-type pages like comparisonwrapper)
    let constraintKey =
        files.Keys
        |> Seq.tryFind (fun k -> k.Contains("typeconstrainttests") && not (k.Contains("comparisonwrapper")))

    constraintKey.IsSome |> shouldEqual true

    // Short mode: '(requires ...)' clause should appear inline with type parameters
    files.[constraintKey.Value] |> shouldContainText "requires"
    // Short mode shows specific constraint keywords
    files.[constraintKey.Value] |> shouldContainText "equality"
    files.[constraintKey.Value] |> shouldContainText "comparison"
    // Should NOT have a separate "Constraints:" label
    if format = Html then
        files.[constraintKey.Value] |> shouldNotContainText "Constraints:"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs TypeConstraintDisplayMode Full renders separate Constraints section in output`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath
    let output = getOutputDir format "FsLib2_constraints_full"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.Full } ]

    let _metadata =
        DocsGenerator(format)
            .Run(
                inputs,
                output = output,
                collectionName = "Collection",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = [ root ]
            )

    let fileNames = Directory.GetFiles(output </> "reference")
    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
    // Find the module page specifically (not sub-type pages like comparisonwrapper)
    let constraintKey =
        files.Keys
        |> Seq.tryFind (fun k -> k.Contains("typeconstrainttests") && not (k.Contains("comparisonwrapper")))

    constraintKey.IsSome |> shouldEqual true

    // Full mode: "Constraints:" label should appear in HTML output
    if format = Html then
        files.[constraintKey.Value] |> shouldContainText "Constraints:"
        // Full mode shows constraints with type-variable prefix like "'T : equality"
        files.[constraintKey.Value] |> shouldContainText "equality"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs TypeConstraintDisplayMode None renders no constraint information in output`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath
    let output = getOutputDir format "FsLib2_constraints_none"

    let inputs =
        [ { ApiDocInput.FromFile(library, mdcomments = false) with
              TypeConstraintDisplayMode = TypeConstraintDisplayMode.None } ]

    let _metadata =
        DocsGenerator(format)
            .Run(
                inputs,
                output = output,
                collectionName = "Collection",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = [ root ]
            )

    let fileNames = Directory.GetFiles(output </> "reference")
    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
    // Find the module page specifically (not sub-type pages like comparisonwrapper)
    let constraintKey =
        files.Keys
        |> Seq.tryFind (fun k -> k.Contains("typeconstrainttests") && not (k.Contains("comparisonwrapper")))

    constraintKey.IsSome |> shouldEqual true

    // None mode: no "Constraints:", no constraint keywords from type params section
    if format = Html then
        files.[constraintKey.Value] |> shouldNotContainText "Constraints:"


    let libraries = [ testBin </> "FsLib1.dll"; testBin </> "FsLib2.dll" ] |> fullpaths

    let inputs =
        [ for lib in libraries ->
              ApiDocInput.FromFile(
                  lib,
                  mdcomments = true,
                  sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
                  sourceFolder = (root </> "../..")
              ) ]

    let output = getOutputDir format "FsLib12_SourceLinks"

    printfn "Output: %s" output

    let _model, _searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "FsLib",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = ([ testBin ] |> fullpaths)
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    let onlyInHtml value =
        match format with
        | Html -> value
        | Markdown -> ""

    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldContainText ("fsdocs-source-link" |> onlyInHtml)

    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldContainText
        "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib2/Library2.fs#L"

    files.[(sprintf "fslib-record.%s" format.Extension)]
    |> shouldContainText ("fsdocs-source-link" |> onlyInHtml)

    files.[(sprintf "fslib-record.%s" format.Extension)]
    |> shouldContainText
        "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib1/Library1.fs#L"

    files.[(sprintf "fslib-union.%s" format.Extension)]
    |> shouldContainText ("fsdocs-source-link" |> onlyInHtml)

    files.[(sprintf "fslib-union.%s" format.Extension)]
    |> shouldContainText
        "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib1/Library1.fs#L"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs test that cref generation works`` (format: OutputFormat) =
    let libraries =
        [ testBin </> "crefLib1.dll"
          testBin </> "crefLib2.dll"
          testBin </> "crefLib3.dll"
          testBin </> "crefLib4.dll" ]
        |> fullpaths

    let output = getOutputDir format "crefLibs"
    printfn "Output: %s" output

    let inputs =
        [ for lib in libraries ->
              ApiDocInput.FromFile(
                  lib,
                  sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
                  sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
                  mdcomments = false
              ) ]

    let _model, _searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "CrefLibs",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = ([ testBin ] |> fullpaths)
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    // C# tests
    // reference class in same assembly
    files.[(sprintf "creflib4-class1.%s" format.Extension)]
    |> shouldContainText "Class2"

    files.[(sprintf "creflib4-class1.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib4-class2%s" format.ExtensionInUrl)
    // reference to another assembly
    files.[(sprintf "creflib4-class2.%s" format.Extension)]
    |> shouldContainText "Class1"

    files.[(sprintf "creflib4-class2.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib1-class1%s" format.ExtensionInUrl)
    // + no crash on unresolved reference.

    files.[(sprintf "creflib4-class2.%s" format.Extension)]
    |> shouldContainText "Unknown__Reference"

    // reference to a member works.
    files.[(sprintf "creflib4-class3.%s" format.Extension)]
    |> shouldContainText "Class2.Other"

    files.[(sprintf "creflib4-class3.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib4-class2%s" format.ExtensionInUrl)

    // references to members work and give correct links
    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "<a href=\"/reference/creflib2-class2%s#Other\">Class2.Other</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-class2%s#Method0\">Class2.Method0</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-class2%s#Method1\">Class2.Method1</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-class2%s#Method2\">Class2.Method2</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-genericclass2-1%s\">GenericClass2</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "and <a href=\"/reference/creflib2-genericclass2-1%s#Property\">GenericClass2.Property</a>"
            format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "and <a href=\"/reference/creflib2-genericclass2-1%s#NonGenericMethod\">GenericClass2.NonGenericMethod</a>"
            format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "and <a href=\"/reference/creflib2-genericclass2-1%s#GenericMethod\">GenericClass2.GenericMethod</a>"
            format.ExtensionInUrl
    )

    // references to non-existent members where the type resolves give an approximation
    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-class2%s\">Class2.NotExistsProperty</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class3.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-class2%s\">Class2.NotExistsMethod</a>" format.ExtensionInUrl
    )

    // reference to a corelib class works.
    files.[(sprintf "creflib4-class4.%s" format.Extension)]
    |> shouldContainText "Assembly"

    files.[(sprintf "creflib4-class4.%s" format.Extension)]
    |> shouldContainText "https://learn.microsoft.com/dotnet/api/system.reflection.assembly"

    // F# tests (at least we not not crash for them, compiler doesn't resolve anything)
    // reference class in same assembly
    files.[(sprintf "creflib2-class1.%s" format.Extension)]
    |> shouldContainText "Class2"
    // tolerant resolution: unqualified type name now resolves within the same assembly set
    files.[(sprintf "creflib2-class1.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib2-class2%s" format.ExtensionInUrl)
    // reference to another assembly
    files.[(sprintf "creflib2-class2.%s" format.Extension)]
    |> shouldContainText "Class1"
    //files.[(sprintf "creflib2-class2.%s" format.Extension)] |> shouldContainText (sprintf "creflib1-class1%s" format.ExtensionInUrl)
    files.[(sprintf "creflib2-class2.%s" format.Extension)]
    |> shouldContainText "Unknown__Reference"

    files.[(sprintf "creflib2-class4.%s" format.Extension)]
    |> shouldContainText "Assembly"
    //files.[(sprintf "creflib2-class4.%s" format.Extension)] |> shouldContainText "https://learn.microsoft.com/dotnet/api/system.reflection.assembly"

    // F# tests (fully quallified)
    // reference class in same assembly
    files.[(sprintf "creflib2-class5.%s" format.Extension)]
    |> shouldContainText "Class2"

    files.[(sprintf "creflib2-class5.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib2-class2%s" format.ExtensionInUrl)
    // reference to another assembly
    files.[(sprintf "creflib2-class6.%s" format.Extension)]
    |> shouldContainText "Class1"

    files.[(sprintf "creflib2-class6.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib1-class1%s" format.ExtensionInUrl)

    files.[(sprintf "creflib2-class6.%s" format.Extension)]
    |> shouldContainText "Unknown__Reference"

    files.[(sprintf "creflib2-class7.%s" format.Extension)]
    |> shouldContainText "Class2.Other"

    files.[(sprintf "creflib2-class7.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib2-class2%s" format.ExtensionInUrl)

    files.[(sprintf "creflib2-class8.%s" format.Extension)]
    |> shouldContainText "Assembly"

    files.[(sprintf "creflib2-class8.%s" format.Extension)]
    |> shouldContainText "https://learn.microsoft.com/dotnet/api/system.reflection.assembly"

    // F# tests - tolerant resolution: unqualified type reference
    files.[(sprintf "creflib2-class9.%s" format.Extension)]
    |> shouldContainText "Class2"

    files.[(sprintf "creflib2-class9.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib2-class2%s" format.ExtensionInUrl)

    // F# tests - tolerant resolution: unqualified Type.Member reference
    files.[(sprintf "creflib2-class10.%s" format.Extension)]
    |> shouldContainText (
        sprintf "<a href=\"/reference/creflib2-class2%s#Other\">Class2.Other</a>" format.ExtensionInUrl
    )

    files.[(sprintf "creflib2-class10.%s" format.Extension)]
    |> shouldContainText (
        sprintf "and <a href=\"/reference/creflib2-class2%s#Method0\">Class2.Method0</a>" format.ExtensionInUrl
    )

    // F# tests - tolerant resolution: unqualified generic type reference
    files.[(sprintf "creflib2-class11.%s" format.Extension)]
    |> shouldContainText (sprintf "creflib2-genericclass2-1%s" format.ExtensionInUrl)

    files.[(sprintf "creflib2-class11.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "<a href=\"/reference/creflib2-genericclass2-1%s#Property\">GenericClass2.Property</a>"
            format.ExtensionInUrl
    )

[<Test>]
[<TestCaseSource("formats")>]
let ``Math in XML generated ok`` (format: OutputFormat) =
    let libraries = [ testBin </> "crefLib1.dll"; testBin </> "crefLib2.dll" ] |> fullpaths

    let output = getOutputDir format "crefLibs_math"
    printfn "Output: %s" output

    let inputs =
        [ for lib in libraries ->
              ApiDocInput.FromFile(
                  lib,
                  sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
                  sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
                  mdcomments = false
              ) ]

    let _model, _searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "CrefLibs",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = ([ testBin ] |> fullpaths)
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    // math is emitted ok
    files.[(sprintf "creflib2-mathtest.%s" format.Extension)]
    |> shouldContainText """This is XmlMath1 \(f(x)\)"""

    files.[(sprintf "creflib2-mathtest.%s" format.Extension)]
    |> shouldContainText
        """This is XmlMath2 \(\left\lceil \frac{\text{end} - \text{start}}{\text{step}} \right\rceil\)"""

    files.[(sprintf "creflib2-mathtest.%s" format.Extension)]
    |> shouldContainText """<p class='fsdocs-para'>XmlMath3</p>"""

    files.[(sprintf "creflib2-mathtest.%s" format.Extension)]
    |> shouldContainText """1 &lt; 2 &lt; 3 &gt; 0"""

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs test that csharp (publiconly) support works`` (format: OutputFormat) =
    let libraries = [ testBin </> "csharpSupport.dll" ] |> fullpaths

    let output = getOutputDir format "csharpSupport"
    printfn "Output: %s" output

    let inputs =
        [ for lib in libraries ->
              ApiDocInput.FromFile(
                  lib,
                  sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
                  sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
                  publicOnly = true,
                  mdcomments = false
              ) ]

    let _model, _searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "CSharpSupport",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = ([ testBin ] |> fullpaths)
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    // C# tests

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Sample_Class"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Constructor"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Event"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Constructor"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Event"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Event"


    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Static_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Static_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Static_Event"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Sample_Class"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Method"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Property"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Event"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Static_Method"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Static_Property"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldNotContainText "My_Private_Static_Event"

//#if INTERACTIVE
//System.Diagnostics.Process.Start(output)
//#endif


[<Ignore "Ignored because publicOnly=false is currently not working, see https://github.com/fsprojects/FSharp.Formatting/pull/259">]
[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs test that csharp support works`` (format: OutputFormat) =
    let libraries = [ testBin </> "csharpSupport.dll" ] |> fullpaths

    let output = getOutputDir format "csharpSupport_private"

    printfn "Output: %s" output

    let inputs =
        [ for lib in libraries ->
              ApiDocInput.FromFile(
                  lib,
                  sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
                  sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
                  publicOnly = false,
                  mdcomments = false
              ) ]

    let _model, _searchIndex =
        DocsGenerator(format)
            .Run(
                inputs,
                output,
                collectionName = "CSharpSupport",
                template = docTemplate format,
                substitutions = substitutions,
                libDirs = ([ testBin ] |> fullpaths)
            )

    let fileNames = Directory.GetFiles(output </> "reference")

    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

    // C# tests

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Sample_Class"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Constructor"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Event"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Constructor"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Event"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Event"


    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Static_Method"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Static_Property"

    files.[(sprintf "csharpsupport-sampleclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Static_Event"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Sample_Class"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Method"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Property"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Static_Event"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Static_Method"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Static_Property"

    files.[(sprintf "csharpsupport-samplestaticclass.%s" format.Extension)]
    |> shouldContainText "My_Private_Static_Event"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs process XML comments in two sample F# assemblies`` (format: OutputFormat) =
    let libraries = [ testBin </> "TestLib1.dll"; testBin </> "TestLib2.dll" ] |> fullpaths

    let files = generateApiDocs libraries format false "TestLibs"

    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldContainText "Readonly int property"

    files.[(sprintf "fslib-record.%s" format.Extension)]
    |> shouldContainText "This is name"

    files.[(sprintf "fslib-record.%s" format.Extension)]
    |> shouldContainText "Additional member"

    files.[(sprintf "fslib-union.%s" format.Extension)]
    |> shouldContainText "Hello of int"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "Sample class"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "Union sample"

    files.[(sprintf "fslib.%s" format.Extension)]
    |> shouldContainText "Record sample"

    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText "Somewhat nested type"

    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText "Somewhat nested module"

    files.[(sprintf "fslib-nested-nestedtype.%s" format.Extension)]
    |> shouldContainText "Very nested member"

    files.[(sprintf "fslib-nested-submodule.%s" format.Extension)]
    |> shouldContainText "Very nested field"


[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs highlights code snippets in Markdown comments`` (format: OutputFormat) =
    let library = testBin </> "TestLib1.dll" |> fullpath

    let files = generateApiDocs [ library ] format true "TestLib1"

    files.[(sprintf "fslib-myclass.%s" format.Extension)]
    |> shouldContainText """<span class="k">let</span>"""

    files.[(sprintf "fslib-myclass.%s" format.Extension)]
    |> shouldContainText """<span class="k">var</span>"""

    files.[(sprintf "fslib-myclass.%s" format.Extension)]
    |> shouldContainText """val a: FsLib.MyClass"""

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs handles c# dlls`` (format: OutputFormat) =
    let library = testBin </> "FSharp.Formatting.CSharpFormat.dll" |> fullpath

    let files = (generateApiDocs [ library ] format false "CSharpFormat").Keys

    let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith(sprintf "index.%s" format.Extension))

    optIndex.IsSome |> shouldEqual true

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs processes C# types and includes xml comments in docs`` (format: OutputFormat) =
    let library = __SOURCE_DIRECTORY__ </> "files" </> "CSharpFormat.dll" |> fullpath

    let files = generateApiDocs [ library ] format false "CSharpFormat2"

    files.[(sprintf "manoli-utils-csharpformat.%s" format.Extension)]
    |> shouldContainText "CLikeFormat"

    files.[(sprintf "manoli-utils-csharpformat.%s" format.Extension)]
    |> shouldContainText "Provides a base class for formatting languages similar to C."

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs processes C# properties on types and includes xml comments in docs`` (format: OutputFormat) =
    let library = __SOURCE_DIRECTORY__ </> "files" </> "CSharpFormat.dll" |> fullpath

    let files = generateApiDocs [ library ] format false "CSharpFormat3"

    files.[(sprintf "manoli-utils-csharpformat-clikeformat.%s" format.Extension)]
    |> shouldContainText "CommentRegEx"

    files.[(sprintf "manoli-utils-csharpformat-clikeformat.%s" format.Extension)]
    |> shouldContainText "Regular expression string to match single line and multi-line"

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs generates module link in nested types`` (format: OutputFormat) =

    let library = testBin </> "FsLib2.dll"

    let files = generateApiDocs [ library ] format false "FsLib2"

    let namespaceReference =
        match format with
        | Html -> """<a href="/reference/fslib.html">"""
        | Markdown -> "[FsLib](/reference/fslib)"

    // Check that the modules and type files have namespace information
    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldContainText "Namespace:"

    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldContainText namespaceReference

    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText "Namespace:"

    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText namespaceReference

    files.[(sprintf "fslib-nested-nestedtype.%s" format.Extension)]
    |> shouldContainText "Namespace:"

    files.[(sprintf "fslib-nested-nestedtype.%s" format.Extension)]
    |> shouldContainText namespaceReference

    files.[(sprintf "fslib-nested-submodule.%s" format.Extension)]
    |> shouldContainText "Namespace:"

    files.[(sprintf "fslib-nested-submodule.%s" format.Extension)]
    |> shouldContainText namespaceReference

    files.[(sprintf "fslib-nested-submodule-verynestedtype.%s" format.Extension)]
    |> shouldContainText "Namespace:"

    files.[(sprintf "fslib-nested-submodule-verynestedtype.%s" format.Extension)]
    |> shouldContainText namespaceReference

    // Check that the link to the module is correctly generated
    let parentModuleReference =
        match format with
        | Html -> """<a href="/reference/fslib-nested.html">"""
        | Markdown -> "[Nested](/reference/fslib-nested)"

    files.[(sprintf "fslib-nested-nestedtype.%s" format.Extension)]
    |> shouldContainText "Parent Module:"

    files.[(sprintf "fslib-nested-nestedtype.%s" format.Extension)]
    |> shouldContainText parentModuleReference

    // Only for nested types
    files.[(sprintf "fslib-class.%s" format.Extension)]
    |> shouldNotContainText "Parent Module:"

    // Check that the link to the module is correctly generated for types in nested modules
    let nestedParentModuleReference =
        match format with
        | Html -> """<a href="/reference/fslib-nested-submodule.html">"""
        | Markdown -> "[Submodule](/reference/fslib-nested-submodule)"

    files.[(sprintf "fslib-nested-submodule-verynestedtype.%s" format.Extension)]
    |> shouldContainText "Parent Module:"

    files.[(sprintf "fslib-nested-submodule-verynestedtype.%s" format.Extension)]
    |> shouldContainText nestedParentModuleReference

    // Check that nested submodules have links to its module
    files.[(sprintf "fslib-nested-submodule.%s" format.Extension)]
    |> shouldContainText "Parent Module:"

    files.[(sprintf "fslib-nested-submodule.%s" format.Extension)]
    |> shouldContainText parentModuleReference

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs omit works without markdown`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] format false "FsLib2_omit"

    // Omitted items shouldn't have generated a file
    files.ContainsKey(sprintf "fslib-test_omit.%s" format.Extension)
    |> shouldEqual false

[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs test FsLib1`` (format: OutputFormat) =
    let library = testBin </> "FsLib1.dll" |> fullpath

    let files = generateApiDocs [ library ] format false "FsLib1_omit"

    files.ContainsKey(sprintf "fslib-test_omit.%s" format.Extension)
    |> shouldEqual false

[<Test>]
let ``ApiDocs test examples`` () =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] OutputFormat.Html false "FsLib2_examples"

    let testFile = sprintf "fslib-commentexamples.%s" OutputFormat.Html.Extension

    files.ContainsKey testFile |> shouldEqual true
    let content = files.[testFile]
    content.Contains "has-id" |> shouldEqual true

    // Check that the favicon href points to the root.
    let testFile = $"fslib.{OutputFormat.Html.Extension}"
    files.ContainsKey testFile |> shouldEqual true
    let content = files[testFile]
    content.Contains "href=\"/root/img/favicon.ico\"" |> shouldEqual true

// -------------------Markdown section-based layout----------------------------------
[<Test>]
let ``ApiDocs Markdown uses section-based member layout instead of tables`` () =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] OutputFormat.Markdown false "FsLib2_markdown_sections"

    let nestedContent = files.["fslib-nested.md"]

    // Each member should have an HTML anchor for backward-compatible links
    nestedContent |> shouldContainText """<a name="f"></a>"""

    // Each member should have a #### heading
    nestedContent |> shouldContainText "#### "

    // No markdown table header for members (the old layout had this)
    nestedContent |> shouldNotContainText "Function or value | Description | Source"

[<Test>]
let ``ApiDocs Markdown generates Example and Note section headings`` () =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] OutputFormat.Markdown false "FsLib2_markdown_examples"

    let commentExamplesContent = files.["fslib-commentexamples.md"]

    // Examples should use ##### headings, not inline <br/>-separated text
    commentExamplesContent |> shouldContainText "##### Example"

    // Should not use the old <br/>-embedded example format
    commentExamplesContent |> shouldNotContainText "Example<br/>"

[<Test>]
let ``ApiDocs Markdown generates Parameters section for members with parameters`` () =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] OutputFormat.Markdown false "FsLib2_markdown_params"

    let issueContent = files.["fslib-test_issue472_t.md"]

    // Members with parameters should have a bold Parameters heading
    issueContent |> shouldContainText "**Parameters:**"

    // Each parameter should be listed individually, not embedded with <br/>
    issueContent |> shouldContainText "**arg1**"
    issueContent |> shouldContainText "**arg2**"

// -------------------Indirect links----------------------------------
[<Test>]
[<TestCaseSource("formats")>]
let ``ApiDocs generates cross-type links for Indirect Links`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] format true "FsLib2_indirect"

    // Check that a link to MyType exists when using Full Name of the type
    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "This function returns a <a href=\"/reference/fslib-nested-mytype%s\" title=\"MyType\">FsLib.Nested.MyType</a>"
            format.ExtensionInUrl
    )

    // Check that a link to OtherType exists when using Logical Name of the type only
    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "This function returns a <a href=\"/reference/fslib-nested-othertype%s\" title=\"OtherType\">OtherType</a>"
            format.ExtensionInUrl
    )

    // Check that a link to a module is created when using Logical Name only
    files.[(sprintf "fslib-duplicatedtypename.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "This type name will be duplicated in <a href=\"/reference/fslib-nested%s\" title=\"Nested\">Nested</a>"
            format.ExtensionInUrl
    )

    // Check that a link to a type with a duplicated name is created when using full name
    files.[(sprintf "fslib-nested-duplicatedtypename.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "This type has the same name as <a href=\"/reference/fslib-duplicatedtypename%s\" title=\"DuplicatedTypeName\">FsLib.DuplicatedTypeName</a>"
            format.ExtensionInUrl
    )

(*
  // Check that a link to a type with a duplicated name is created even when using Logical name only
  files.[(sprintf "fslib-nested.%s" format.Extension)] |> shouldContainText (sprintf "This function returns a <a href=\"/reference/fslib-duplicatedtypename%s\" title=\"DuplicatedTypeName\">DuplicatedTypeName</a> multiplied by 4."
  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.[(sprintf "fslib-nested.%s" format.Extension)] |> shouldContainText (sprintf "This function returns a [InexistentTypeName] multiplied by 5."
*)

// -------------------Inline code----------------------------------
[<Test>]
[<TestCaseSource("formats")>]
let ``Metadata generates cross-type links for Inline Code`` (format: OutputFormat) =
    let library = testBin </> "FsLib2.dll" |> fullpath

    let files = generateApiDocs [ library ] format true "FsLib2_inline"

    // Check that a link to MyType exists when using Full Name of the type in a inline code
    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "You will notice that <a href=\"/reference/fslib-nested-mytype%s\" title=\"MyType\"><code>FsLib.Nested.MyType</code></a> is just an <code>int</code>"
            format.ExtensionInUrl
    )

    // Check that a link to MyType exists when using Full Name of the type in a inline code
    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "You will notice that <a href=\"/reference/fslib-nested-othertype%s\" title=\"OtherType\"><code>OtherType</code></a> is just an <code>int</code>"
            format.ExtensionInUrl
    )

    // Check that a link to a type with a duplicated name is not created when using Logical name only
    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "<a href=\"/reference/fslib-duplicatedtypename%s\" title=\"DuplicatedTypeName\"><code>DuplicatedTypeName</code></a> is duplicated"
            format.ExtensionInUrl
    )

    // Check that a link to a type with a duplicated name is not created when using Logical name only
    files.[(sprintf "fslib-nested.%s" format.Extension)]
    |> shouldContainText "<code>InexistentTypeName</code> does not exists so it should no add a cross-type link"

    // Check that a link to a module is created when using Logical Name only
    files.[(sprintf "fslib-duplicatedtypename.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "This type name will be duplicated in <a href=\"/reference/fslib-nested%s\" title=\"Nested\"><code>Nested</code></a>"
            format.ExtensionInUrl
    )

    // Check that a link to a type with a duplicated name is created when using full name
    files.[(sprintf "fslib-nested-duplicatedtypename.%s" format.Extension)]
    |> shouldContainText (
        sprintf
            "This type has the same name as <a href=\"/reference/fslib-duplicatedtypename%s\" title=\"DuplicatedTypeName\"><code>FsLib.DuplicatedTypeName</code></a>"
            format.ExtensionInUrl
    )

[<Test>]
[<TestCaseSource("formats")>]
let ``Phased generation allows a custom menu template folder`` (format: OutputFormat) =
    let library = testBin </> "FsLib1.dll" |> fullpath
    let inputs = ApiDocInput.FromFile(library)
    let output = getOutputDir format "Phased"
    let templateFolder = DirectoryInfo(Directory.GetCurrentDirectory() </> "menu")
    templateFolder.Create()

    File.WriteAllText(
        templateFolder.FullName </> "_menu_template.html",
        """
HEADER: {{fsdocs-menu-header-content}}
HEADER ID: {{fsdocs-menu-header-id}}
ITEMS: {{fsdocs-menu-items}}
"""
    )

    File.WriteAllText(
        templateFolder.FullName </> "_menu-item_template.html",
        """
LINK: {{fsdocs-menu-item-link}}
LINK ID: {{fsdocs-menu-item-id}}
CONTENT: {{fsdocs-menu-item-content}}
"""
    )

    let _, substitutions, _, _ =
        match format with
        | OutputFormat.Html ->
            ApiDocs.GenerateHtmlPhased([ inputs ], output, "Collection", [], menuTemplateFolder = "menu")
        | OutputFormat.Markdown ->
            ApiDocs.GenerateMarkdownPhased([ inputs ], output, "Collection", [], menuTemplateFolder = "menu")

    let listOfNamespaces =
        substitutions
        |> Seq.choose (function
            | key, content ->
                if key = ParamKeys.``fsdocs-list-of-namespaces`` then
                    Some content
                else
                    None)
        |> Seq.head
        |> fun s ->
            s.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun s -> s.Trim())
            |> String.concat "\n"

    Assert.AreEqual(
        $"""HEADER: API Reference
HEADER ID: api_reference
ITEMS:
LINK: /reference/index{format.ExtensionInUrl}
LINK ID: all_namespaces
CONTENT: All Namespaces"""
            .Replace("\r", ""),
        listOfNamespaces
    )

let runtest testfn =
    try
        testfn ()
    with e ->
        printfn "Error -\n%s\n\nStackTrace -\n%s\n\n\TargetSite -\n%s\n" e.Message e.StackTrace e.TargetSite.Name
#if INTERACTIVE
printfn "Metadata generates cross-type links for Inline Code"
runtest ``Metadata generates cross-type links for Inline Code``

printfn "Metadata generates cross-type links for Indirect Links"
runtest ``Metadata generates cross-type links for Indirect Links``

printfn "ApiDocs test FsLib1"
runtest ``ApiDocs test FsLib1``

printfn "ApiDocs omit works without markdown"
runtest ``ApiDocs omit works without markdown``

printfn "ApiDocs generates module link in nested types"
runtest ``ApiDocs generates module link in nested types``
runtest ``ApiDocs processes C# properties on types and includes xml comments in docs``

printfn "ApiDocs handles c# dlls"
runtest ``ApiDocs handles c# dlls``

printfn "ApiDocs highlights code snippets in Markdown comments"
runtest ``ApiDocs highlights code snippets in Markdown comments``

printfn "ApiDocs process XML comments in two sample F# assemblies"
runtest ``ApiDocs process XML comments in two sample F# assemblies``

printfn "ApiDocs works on sample Deedle assembly"
runtest ``ApiDocs works on sample Deedle assembly``

printfn "ApiDocs works on two sample F# assemblies"
runtest ``ApiDocs works on two sample F# assemblies``

printfn "ApiDocs test that csharp (publiconly) support works"
runtest ``ApiDocs test that csharp (publiconly) support works``

printfn "ApiDocs test that cref generation works"
runtest ``ApiDocs test that cref generation works``

#endif
