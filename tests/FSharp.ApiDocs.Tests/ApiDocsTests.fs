[<NUnit.Framework.TestFixture>]
module ApiDocs.Tests

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
with 
  member x.Extension =
    match x with
     | Html -> "html"
     | Markdown -> "md"
  member x.ExtensionInUrl =
    match x with
     | Html -> ".html"
     | Markdown -> ""

type DocsGenerator(format: OutputFormat) = class end
with
 member _.Run(input, output, collectionName, template, substitutions, ?libDirs, ?root) =
   let root = defaultArg root "/"
   let libDirs = defaultArg libDirs []
   match format with
    | Html -> ApiDocs.GenerateHtml(input, output, collectionName=collectionName, template=template, substitutions=substitutions, libDirs=libDirs, root=root)
    | Markdown -> ApiDocs.GenerateMarkdown(input, output, collectionName=collectionName, template=template, substitutions=substitutions, libDirs=libDirs, root=root)

let formats = [ Html; Markdown ]

let (</>) a b = Path.Combine(a, b)
let fullpath = Path.GetFullPath
let fullpaths = List.map fullpath

let root = __SOURCE_DIRECTORY__ |> fullpath


// NOTE - For these tests to run properly they require the output of all the metadata
// test project to be directed to the directory below
let testBin = AttributeTests.testBin

let getOutputDir (format:OutputFormat) (uniq: string)  =
  let outDir = __SOURCE_DIRECTORY__ + "/output/" + format.Extension + "/" + uniq
  while (try Directory.Exists outDir with _ -> false) do
      Directory.Delete(outDir, true)
  Directory.CreateDirectory(outDir).FullName

let removeWhiteSpace (str:string) =
    str.Replace("\n", "").Replace("\r", "").Replace(" ", "")

let docTemplate (format:OutputFormat) =
  root </> $"../../docs/_template.{format.Extension}"

let substitutions =
  [ ParamKeys.``fsdocs-collection-name``, "F# TestProject"
    ParamKeys.``fsdocs-authors``, "Your Name"
    ParamKeys.``fsdocs-repository-link``, "http://github.com/fsprojects/fsharp-test-project"
    ParamKeys.``root``, "/root/" ]

let generateApiDocs (libraries:string list) (format:OutputFormat) useMdComments uniq =
    try
        let output = getOutputDir format uniq
        let inputs = [ for x in libraries -> ApiDocInput.FromFile(x, mdcomments = useMdComments) ]
        let _metadata = DocsGenerator(format).Run (inputs, output=output, collectionName="Collection", template=docTemplate format, substitutions=substitutions, libDirs = [root])

        let fileNames = Directory.GetFiles(output </> "reference")
        let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
        files

    with e ->
        printfn "Failed to Generate API Docs -\n%s\n\n%s\n" e.Message e.StackTrace
        System.AppDomain.CurrentDomain.GetAssemblies ()
        |> Seq.iter (fun x ->
            try sprintf "%s\n - %s" x.FullName x.Location |> System.Console.WriteLine
            with e ->
                sprintf "\nError On - %A\n -- %s\n"  x e.Message |> System.Console.WriteLine
        )
        reraise ()

do FSharp.Formatting.TestHelpers.enableLogging()

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs works on sample Deedle assembly`` (format:OutputFormat) =
  let library = root </> "files" </> "Deedle.dll"
  let output = getOutputDir format "Deedle"

  let input =
      ApiDocInput.FromFile(library, mdcomments = true,
         sourceRepo = "https://github.com/fslaborg/Deedle/",
         sourceFolder = "c:/dev/FSharp.DataFrame")
  let _model, _index =
    DocsGenerator(format).Run([input], output, collectionName="Deedle", template=docTemplate format, substitutions=substitutions, libDirs = [testBin])
  let files = Directory.GetFiles(output </> "reference")

  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith $"index.{format.Extension}")
  optIndex.IsSome |> shouldEqual true

  let optSeriesMod = files |> Seq.tryFind (fun s -> s.Contains "seriesmodule")
  optSeriesMod.IsSome |> shouldEqual true

[<Test; Ignore "Ignore by default to make tests run reasonably fast">]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs works on sample FAKE assembly`` (format:OutputFormat) =
  let library = root </> "files" </> "FAKE" </> "FakeLib.dll"
  let output = getOutputDir format "FakeLib"
  let input = ApiDocInput.FromFile(library, mdcomments = true)
  let _model, _index = DocsGenerator(format).Run([input], output, collectionName="FAKE", template=docTemplate format, substitutions=substitutions)
  let files = Directory.GetFiles(output </> "reference")
  files |> Seq.length |> shouldEqual 166

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs works on two sample F# assemblies`` (format:OutputFormat) =
  let libraries =
    [ testBin </> "FsLib1.dll"
      testBin </> "FsLib2.dll" ]
  let output = getOutputDir format "FsLib12"
  let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = true, substitutions=substitutions) ]
  let _model, searchIndex =
      DocsGenerator(format).Run(inputs, output, collectionName="FsLib", template=docTemplate format,
          root="http://root.io/root/", substitutions=substitutions, libDirs = [testBin])

  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // Check that all comments appear in the output
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "Readonly int property"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "This is name"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "Additional member"
  files.[$"fslib-union.{format.Extension}"] |> shouldContainText "Hello of int"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "Sample class"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "Union sample"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "Record sample"
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "Somewhat nested type"
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "Somewhat nested module"
  files.[$"fslib-nested-nestedtype.{format.Extension}"] |> shouldContainText "Very nested member"
  files.[$"fslib-nested-submodule.{format.Extension}"] |> shouldContainText "Very nested field"

  // Check that union fields are correctly generated
  files.[$"fslib-union.{format.Extension}"] |> shouldContainText "<span>World(<span>string,&#32;int</span>)</span>"
  files.[$"fslib-union.{format.Extension}"] |> shouldContainText "<span>Naming(<span>rate,&#32;string</span>)</span>"

  (*
  // Check that methods with no arguments are correctly generated (#113)
  files.[$"fslib-record.{format.Extension}"] |> shouldNotContainText "Foo2(arg1)"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "Foo2()"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "Signature"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "unit -&gt; int"
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "Class()"
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "unit -&gt; Class"

  // Check that properties are correctly generated (#114)
  files.[$"fslib-class.{format.Extension}"] |> removeWhiteSpace |> shouldNotContainText ">this.Member(arg1)<"
  files.[$"fslib-class.{format.Extension}"] |> removeWhiteSpace |> shouldNotContainText ">this.Member()<"
  files.[$"fslib-class.{format.Extension}"] |> removeWhiteSpace |> shouldContainText ">this.Member<"
  files.[$"fslib-class.{format.Extension}"] |> shouldNotContainText "unit -&gt; int"
  //files.[$"fslib-class.{format.Extension}"] |> shouldContainText "Signature:"

  // Check that formatting is correct
  files.[$"fslib-test_issue472_r.{format.Extension}"] |> shouldContainText "Test_Issue472_R.fmultipleargs x y"
  files.[$"fslib-test_issue472_r.{format.Extension}"] |> shouldContainText "Test_Issue472_R.ftupled(x, y)"
  files.[$"fslib-test_issue472.{format.Extension}"] |> shouldContainText "fmultipleargs x y"
  files.[$"fslib-test_issue472.{format.Extension}"] |> shouldContainText "ftupled(x, y)"
  files.[$"fslib-test_issue472_t.{format.Extension}"] |> shouldContainText "this.MultArg(arg1, arg2)"
  files.[$"fslib-test_issue472_t.{format.Extension}"] |> shouldContainText "this.MultArgTupled(arg)"
  files.[$"fslib-test_issue472_t.{format.Extension}"] |> shouldContainText "this.MultPartial arg1 arg2"

*)
  let indxTxt = System.Text.Json.JsonSerializer.Serialize searchIndex

  // Test a few entries in the search index
  indxTxt |> shouldContainText "\"uri\""
  indxTxt |> shouldContainText "\"content\""
  indxTxt |> shouldContainText "\"title\""
  indxTxt |> shouldContainText "http://root.io/root/reference/fslib-nested-submodule-verynestedtype.html#Member"
  indxTxt |> shouldContainText "http://root.io/root/reference/fslib-test_issue472_t.html#MultArg"
  indxTxt |> shouldContainText """ITest_Issue229.Name \nName \n"""

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``Namespace summary generation works on two sample F# assemblies using XML docs`` (format:OutputFormat) =
  let libraries =
    [ testBin </> "TestLib1.dll"
      testBin </> "TestLib2.dll" ]
  let output = getOutputDir format "TestLib12_Namespaces"
  let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = false, substitutions=substitutions) ]
  let _model, _searchIndex =
      DocsGenerator(format).Run(inputs, output, collectionName="TestLibs", template=docTemplate format,
          root="http://root.io/root/", substitutions=substitutions, libDirs = [testBin])

  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.[$"index.{format.Extension}"] |> shouldContainText "FsLib is a good namespace"
  files.[$"index.{format.Extension}"] |> shouldNotContainText "I tell you again FsLib is good"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "FsLib is a good namespace"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "I tell you again FsLib is good"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs model generation works on two sample F# assemblies`` (format:OutputFormat) =
  let libraries =
    [ testBin </> "FsLib1.dll"
      testBin </> "FsLib2.dll" ]
  let inputs = [ for lib in libraries -> ApiDocInput.FromFile(lib, mdcomments = true) ]
  let model = ApiDocs.GenerateModel(inputs, collectionName="FsLib", substitutions=substitutions, libDirs = [testBin])
  model.Collection.Assemblies.Length |> shouldEqual 2
  model.Collection.Assemblies.[0].Name |> shouldEqual "FsLib1"
  model.Collection.Assemblies.[1].Name |> shouldEqual "FsLib2"
  model.Collection.Namespaces.Length |> shouldEqual 1
  model.Collection.Namespaces.[0].Name |> shouldEqual "FsLib"
  model.Collection.Namespaces.[0].Entities |> List.filter (fun c -> c.IsTypeDefinition) |> function x -> x.Length |> shouldEqual 10
  let assemblies = [ for t in model.Collection.Namespaces.[0].Entities -> t.Assembly.Name ]
  assemblies |> List.distinct |> List.sort |> shouldEqual ["FsLib1"; "FsLib2"]

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs generates Go to GitHub source links`` (format:OutputFormat) =
  let libraries =
    [ testBin  </> "FsLib1.dll"
      testBin  </> "FsLib2.dll" ] |> fullpaths
  let inputs =
     [ for lib in libraries ->
         ApiDocInput.FromFile(lib, mdcomments = true,
           sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
           sourceFolder = (root </> "../..")) ]
  let output = getOutputDir format "FsLib12_SourceLinks"
  printfn "Output: %s" output
  let _model, _searchIndex =
    DocsGenerator(format).Run
      ( inputs, output, collectionName="FsLib", template=docTemplate format,
        substitutions=substitutions, libDirs = ([testBin] |> fullpaths))
  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "fsdocs-source-link"
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib2/Library2.fs#L"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "fsdocs-source-link"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib1/Library1.fs#L"
  files.[$"fslib-union.{format.Extension}"] |> shouldContainText "fsdocs-source-link"
  files.[$"fslib-union.{format.Extension}"] |> shouldContainText "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib1/Library1.fs#L"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs test that cref generation works`` (format:OutputFormat) =
  let libraries =
    [ testBin  </> "crefLib1.dll"
      testBin  </> "crefLib2.dll"
      testBin  </> "crefLib3.dll"
      testBin  </> "crefLib4.dll" ] |> fullpaths
  let output = getOutputDir format "crefLibs"
  printfn "Output: %s" output
  let inputs =
     [ for lib in libraries ->
         ApiDocInput.FromFile(lib,
           sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
           sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
           mdcomments = false) ]
  let _model, _searchIndex =
    DocsGenerator(format).Run
      ( inputs, output, collectionName="CrefLibs", template=docTemplate format,
      substitutions=substitutions, libDirs = ([testBin]  |> fullpaths))
  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests
  // reference class in same assembly
  files.[$"creflib4-class1.{format.Extension}"] |> shouldContainText "Class2"
  files.[$"creflib4-class1.{format.Extension}"] |> shouldContainText $"creflib4-class2{format.ExtensionInUrl}"
  // reference to another assembly
  files.[$"creflib4-class2.{format.Extension}"] |> shouldContainText "Class1"
  files.[$"creflib4-class2.{format.Extension}"] |> shouldContainText $"creflib1-class1{format.ExtensionInUrl}"
  /// + no crash on unresolved reference.
  files.[$"creflib4-class2.{format.Extension}"] |> shouldContainText "Unknown__Reference"

  /// reference to a member works.
  files.[$"creflib4-class3.{format.Extension}"] |> shouldContainText "Class2.Other"
  files.[$"creflib4-class3.{format.Extension}"] |> shouldContainText $"creflib4-class2{format.ExtensionInUrl}"

  /// references to members work and give correct links
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """<a href="/reference/creflib2-class2.html#Other">Class2.Other</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-class2.html#Method0">Class2.Method0</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-class2.html#Method1">Class2.Method1</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-class2.html#Method2">Class2.Method2</a>"""

  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-genericclass2-1.html">GenericClass2</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-genericclass2-1.html#Property">GenericClass2.Property</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-genericclass2-1.html#NonGenericMethod">GenericClass2.NonGenericMethod</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-genericclass2-1.html#GenericMethod">GenericClass2.GenericMethod</a>"""

  /// references to non-existent members where the type resolves give an approximation
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-class2.html">Class2.NotExistsProperty</a>"""
  files.[$"creflib2-class3.{format.Extension}"] |> shouldContainText """and <a href="/reference/creflib2-class2.html">Class2.NotExistsMethod</a>"""

  /// reference to a corelib class works.
  files.[$"creflib4-class4.{format.Extension}"] |> shouldContainText "Assembly"
  files.[$"creflib4-class4.{format.Extension}"] |> shouldContainText "https://docs.microsoft.com/dotnet/api/system.reflection.assembly"

  // F# tests (at least we not not crash for them, compiler doesn't resolve anything)
  // reference class in same assembly
  files.[$"creflib2-class1.{format.Extension}"] |> shouldContainText "Class2"
  //files.[$"creflib2-class1.{format.Extension}"] |> shouldContainText "creflib2-class2{format.ExtensionInUrl}"
  // reference to another assembly
  files.[$"creflib2-class2.{format.Extension}"] |> shouldContainText "Class1"
  //files.[$"creflib2-class2.{format.Extension}"] |> shouldContainText "creflib1-class1{format.ExtensionInUrl}"
  /// + no crash on unresolved reference.
  files.[$"creflib2-class2.{format.Extension}"] |> shouldContainText "Unknown__Reference"

  /// reference to a corelib class works.
  files.[$"creflib2-class4.{format.Extension}"] |> shouldContainText "Assembly"
  //files.[$"creflib2-class4.{format.Extension}"] |> shouldContainText "https://docs.microsoft.com/dotnet/api/system.reflection.assembly"

  // F# tests (fully quallified)
  // reference class in same assembly
  files.[$"creflib2-class5.{format.Extension}"] |> shouldContainText "Class2"
  files.[$"creflib2-class5.{format.Extension}"] |> shouldContainText "creflib2-class2{format.ExtensionInUrl}"
  // reference to another assembly
  files.[$"creflib2-class6.{format.Extension}"] |> shouldContainText "Class1"
  files.[$"creflib2-class6.{format.Extension}"] |> shouldContainText "creflib1-class1{format.ExtensionInUrl}"
  /// + no crash on unresolved reference.
  files.[$"creflib2-class6.{format.Extension}"] |> shouldContainText "Unknown__Reference"
  /// reference to a member works.
  files.[$"creflib2-class7.{format.Extension}"] |> shouldContainText "Class2.Other"
  files.[$"creflib2-class7.{format.Extension}"] |> shouldContainText "creflib2-class2{format.ExtensionInUrl}"

  /// reference to a corelib class works.
  files.[$"creflib2-class8.{format.Extension}"] |> shouldContainText "Assembly"
  files.[$"creflib2-class8.{format.Extension}"] |> shouldContainText "https://docs.microsoft.com/dotnet/api/system.reflection.assembly"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``Math in XML generated ok`` (format:OutputFormat) =
  let libraries =
    [ testBin  </> "crefLib1.dll"
      testBin  </> "crefLib2.dll" ] |> fullpaths
  let output = getOutputDir format "crefLibs_math"
  printfn "Output: %s" output
  let inputs =
     [ for lib in libraries ->
         ApiDocInput.FromFile(lib,
           sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
           sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
           mdcomments = false) ]
  let _model, _searchIndex =
    DocsGenerator(format).Run
      ( inputs, output, collectionName="CrefLibs", template=docTemplate format,
      substitutions=substitutions, libDirs = ([testBin]  |> fullpaths))
  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  /// math is emitted ok
  files.[$"creflib2-mathtest.{format.Extension}"] |> shouldContainText """This is XmlMath1 \(f(x)\)"""
  files.[$"creflib2-mathtest.{format.Extension}"] |> shouldContainText """This is XmlMath2 \(\left\lceil \frac{\text{end} - \text{start}}{\text{step}} \right\rceil\)"""
  files.[$"creflib2-mathtest.{format.Extension}"] |> shouldContainText """<p class='fsdocs-para'>XmlMath3</p>"""
  files.[$"creflib2-mathtest.{format.Extension}"] |> shouldContainText """1 < 2 < 3 > 0"""

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs test that csharp (publiconly) support works`` (format:OutputFormat) =
  let libraries =
    [ testBin </> "csharpSupport.dll" ] |> fullpaths
  let output = getOutputDir format "csharpSupport"
  printfn "Output: %s" output
  let inputs =
     [ for lib in libraries ->
         ApiDocInput.FromFile(lib,
            sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
            sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
            publicOnly = true,
            mdcomments = false) ]
  let _model, _searchIndex =
    DocsGenerator(format).Run
      ( inputs, output, collectionName="CSharpSupport",
        template=docTemplate format, substitutions=substitutions, libDirs = ([testBin]  |> fullpaths) )
  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Sample_Class"

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Constructor"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Event"

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Constructor"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Event"

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Static_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Static_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Static_Event"


  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Static_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Static_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldNotContainText "My_Private_Static_Event"

  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Sample_Class"

  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Method"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Property"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Event"

  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldNotContainText "My_Private_Static_Method"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldNotContainText "My_Private_Static_Property"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldNotContainText "My_Private_Static_Event"

  //#if INTERACTIVE
  //System.Diagnostics.Process.Start(output)
  //#endif


[<Ignore "Ignored because publicOnly=false is currently not working, see https://github.com/fsprojects/FSharp.Formatting/pull/259" >]
[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs test that csharp support works`` (format:OutputFormat) =
  let libraries =
    [ testBin </> "csharpSupport.dll" ] |> fullpaths
  let output = getOutputDir format "csharpSupport_private"
  printfn "Output: %s" output
  let inputs =
     [ for lib in libraries ->
         ApiDocInput.FromFile(lib,
            sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
            sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
            publicOnly = false,
            mdcomments = false) ]
  let _model, _searchIndex =
    DocsGenerator(format).Run
      ( inputs, output, collectionName="CSharpSupport",
        template=docTemplate format, substitutions=substitutions, libDirs = ([testBin] |> fullpaths))
  let fileNames = Directory.GetFiles(output </> "reference")
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Sample_Class"

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Constructor"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Event"

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Constructor"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Event"

  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Static_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Static_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Static_Event"


  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Static_Method"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Static_Property"
  files.[$"csharpsupport-sampleclass.{format.Extension}"] |> shouldContainText "My_Private_Static_Event"

  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Sample_Class"

  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Method"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Property"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Static_Event"

  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Private_Static_Method"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Private_Static_Property"
  files.[$"csharpsupport-samplestaticclass.{format.Extension}"] |> shouldContainText "My_Private_Static_Event"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs process XML comments in two sample F# assemblies`` (format:OutputFormat) =
  let libraries =
    [ testBin  </> "TestLib1.dll"
      testBin </> "TestLib2.dll" ] |> fullpaths
  let files = generateApiDocs libraries format false "TestLibs"
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "Readonly int property"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "This is name"
  files.[$"fslib-record.{format.Extension}"] |> shouldContainText "Additional member"
  files.[$"fslib-union.{format.Extension}"] |> shouldContainText "Hello of int"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "Sample class"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "Union sample"
  files.[$"fslib.{format.Extension}"] |> shouldContainText "Record sample"
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "Somewhat nested type"
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "Somewhat nested module"
  files.[$"fslib-nested-nestedtype.{format.Extension}"] |> shouldContainText "Very nested member"
  files.[$"fslib-nested-submodule.{format.Extension}"] |> shouldContainText "Very nested field"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs highlights code snippets in Markdown comments`` (format:OutputFormat) =
  let library = testBin </> "TestLib1.dll" |> fullpath

  let files = generateApiDocs [library] format true "TestLib1"

  files.[$"fslib-myclass.{format.Extension}"] |> shouldContainText """<span class="k">let</span>"""
  files.[$"fslib-myclass.{format.Extension}"] |> shouldContainText """<span class="k">var</span>"""
  files.[$"fslib-myclass.{format.Extension}"] |> shouldContainText """val a : FsLib.MyClass"""

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs handles c# dlls`` (format:OutputFormat) =
  let library = testBin </> "FSharp.Formatting.CSharpFormat.dll" |> fullpath

  let files = (generateApiDocs [library] format false "CSharpFormat").Keys

  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith "index.{format.Extension}")
  optIndex.IsSome |> shouldEqual true

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs processes C# types and includes xml comments in docs`` (format:OutputFormat) =
    let library = __SOURCE_DIRECTORY__ </> "files" </> "CSharpFormat.dll" |> fullpath

    let files = generateApiDocs [library] format false "CSharpFormat2"

    files.[$"manoli-utils-csharpformat.{format.Extension}"] |> shouldContainText "CLikeFormat"
    files.[$"manoli-utils-csharpformat.{format.Extension}"] |> shouldContainText "Provides a base class for formatting languages similar to C."

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs processes C# properties on types and includes xml comments in docs`` (format:OutputFormat) =
    let library = __SOURCE_DIRECTORY__ </> "files" </> "CSharpFormat.dll" |> fullpath

    let files = generateApiDocs [library] format false "CSharpFormat3"

    files.[$"manoli-utils-csharpformat-clikeformat.{format.Extension}"] |> shouldContainText "CommentRegEx"
    files.[$"manoli-utils-csharpformat-clikeformat.{format.Extension}"] |> shouldContainText "Regular expression string to match single line and multi-line"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs generates module link in nested types`` (format:OutputFormat) =

  let library =  testBin  </> "FsLib2.dll"

  let files = generateApiDocs [library] format false "FsLib2"

  // Check that the modules and type files have namespace information
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "Namespace:"
  files.[$"fslib-class.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib.html\">"
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "Namespace:"
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib.html\">"
  files.[$"fslib-nested-nestedtype.{format.Extension}"] |> shouldContainText "Namespace:"
  files.[$"fslib-nested-nestedtype.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib.html\">"
  files.[$"fslib-nested-submodule.{format.Extension}"] |> shouldContainText "Namespace:"
  files.[$"fslib-nested-submodule.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib.html\">"
  files.[$"fslib-nested-submodule-verynestedtype.{format.Extension}"] |> shouldContainText "Namespace:"
  files.[$"fslib-nested-submodule-verynestedtype.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib.html\">"

  // Check that the link to the module is correctly generated
  files.[$"fslib-nested-nestedtype.{format.Extension}"] |> shouldContainText "Parent Module:"
  files.[$"fslib-nested-nestedtype.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib-nested.html\">"

  // Only for nested types
  files.[$"fslib-class.{format.Extension}"] |> shouldNotContainText "Parent Module:"

  // Check that the link to the module is correctly generated for types in nested modules
  files.[$"fslib-nested-submodule-verynestedtype.{format.Extension}"] |> shouldContainText "Parent Module:"
  files.[$"fslib-nested-submodule-verynestedtype.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib-nested-submodule.html\">"

  // Check that nested submodules have links to its module
  files.[$"fslib-nested-submodule.{format.Extension}"] |> shouldContainText "Parent Module:"
  files.[$"fslib-nested-submodule.{format.Extension}"] |> shouldContainText "<a href=\"/reference/fslib-nested.html\">"

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs omit works without markdown`` (format:OutputFormat) =
  let library = testBin </> "FsLib2.dll" |> fullpath

  let files = generateApiDocs [library] format false "FsLib2_omit"

  // Actually, the thing gets generated it's just not in the index
  files.ContainsKey "fslib-test_omit.{format.Extension}" |> shouldEqual true

[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs test FsLib1`` (format:OutputFormat) =
  let library = testBin </> "FsLib1.dll" |> fullpath

  let files = generateApiDocs [library] format false "FsLib1_omit"

  files.ContainsKey "fslib-test_omit.{format.Extension}" |> shouldEqual false

// -------------------Indirect links----------------------------------
[<Test>]
[<TestCaseSource(nameof formats)>]
let ``ApiDocs generates cross-type links for Indirect Links`` (format:OutputFormat) =
  let library = testBin </> "FsLib2.dll" |> fullpath

  let files = generateApiDocs [library] format true "FsLib2_indirect"

  // Check that a link to MyType exists when using Full Name of the type
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "This function returns a <a href=\"/reference/fslib-nested-mytype.html\" title=\"MyType\">FsLib.Nested.MyType</a>"

  // Check that a link to OtherType exists when using Logical Name of the type only
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "This function returns a <a href=\"/reference/fslib-nested-othertype.html\" title=\"OtherType\">OtherType</a>"

  // Check that a link to a module is created when using Logical Name only
  files.[$"fslib-duplicatedtypename.{format.Extension}"] |> shouldContainText "This type name will be duplicated in <a href=\"/reference/fslib-nested.html\" title=\"Nested\">Nested</a>"

  // Check that a link to a type with a duplicated name is created when using full name
  files.[$"fslib-nested-duplicatedtypename.{format.Extension}"] |> shouldContainText "This type has the same name as <a href=\"/reference/fslib-duplicatedtypename.html\" title=\"DuplicatedTypeName\">FsLib.DuplicatedTypeName</a>"

(*
  // Check that a link to a type with a duplicated name is created even when using Logical name only
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "This function returns a <a href=\"/reference/fslib-duplicatedtypename.html\" title=\"DuplicatedTypeName\">DuplicatedTypeName</a> multiplied by 4."
  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "This function returns a [InexistentTypeName] multiplied by 5."
*)

  // -------------------Inline code----------------------------------
[<Test>]
[<TestCaseSource(nameof formats)>]
let ``Metadata generates cross-type links for Inline Code`` (format:OutputFormat) =
  let library = testBin </> "FsLib2.dll" |> fullpath

  let files = generateApiDocs [library] format true "FsLib2_inline"

  // Check that a link to MyType exists when using Full Name of the type in a inline code
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText $"You will notice that <a href=\"/reference/fslib-nested-mytype{format.ExtensionInUrl}\" title=\"MyType\"><code>FsLib.Nested.MyType</code></a> is just an <code>int</code>"

    // Check that a link to MyType exists when using Full Name of the type in a inline code
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText $"You will notice that <a href=\"/reference/fslib-nested-othertype{format.ExtensionInUrl}\" title=\"OtherType\"><code>OtherType</code></a> is just an <code>int</code>"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText $"<a href=\"/reference/fslib-duplicatedtypename{format.ExtensionInUrl}\" title=\"DuplicatedTypeName\"><code>DuplicatedTypeName</code></a> is duplicated"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.[$"fslib-nested.{format.Extension}"] |> shouldContainText "<code>InexistentTypeName</code> does not exists so it should no add a cross-type link"

  // Check that a link to a module is created when using Logical Name only
  files.[$"fslib-duplicatedtypename.{format.Extension}"] |> shouldContainText $"This type name will be duplicated in <a href=\"/reference/fslib-nested{format.ExtensionInUrl}\" title=\"Nested\"><code>Nested</code></a>"

  // Check that a link to a type with a duplicated name is created when using full name
  files.[$"fslib-nested-duplicatedtypename.{format.Extension}"] |> shouldContainText $"This type has the same name as <a href=\"/reference/fslib-duplicatedtypename{format.ExtensionInUrl}\" title=\"DuplicatedTypeName\"><code>FsLib.DuplicatedTypeName</code></a>"


let runtest testfn =
    try testfn ()
    with e -> printfn "Error -\n%s\n\nStackTrace -\n%s\n\n\TargetSite -\n%s\n" e.Message e.StackTrace e.TargetSite.Name
#if INTERACTIVE
;;
printfn "Metadata generates cross-type links for Inline Code"
runtest ``Metadata generates cross-type links for Inline Code``;;

printfn "Metadata generates cross-type links for Indirect Links"
runtest ``Metadata generates cross-type links for Indirect Links``;;

printfn "ApiDocs test FsLib1"
runtest ``ApiDocs test FsLib1``;;

printfn "ApiDocs omit works without markdown"
runtest ``ApiDocs omit works without markdown``;;

printfn "ApiDocs generates module link in nested types"
runtest ``ApiDocs generates module link in nested types``;;
runtest ``ApiDocs processes C# properties on types and includes xml comments in docs``;;

printfn "ApiDocs handles c# dlls"
runtest ``ApiDocs handles c# dlls``;;

printfn "ApiDocs highlights code snippets in Markdown comments"
runtest ``ApiDocs highlights code snippets in Markdown comments``;;

printfn "ApiDocs process XML comments in two sample F# assemblies"
runtest ``ApiDocs process XML comments in two sample F# assemblies``;;

printfn "ApiDocs works on sample Deedle assembly"
runtest ``ApiDocs works on sample Deedle assembly``;;

printfn "ApiDocs works on two sample F# assemblies"
runtest ``ApiDocs works on two sample F# assemblies``;;

printfn "ApiDocs test that csharp (publiconly) support works"
runtest ``ApiDocs test that csharp (publiconly) support works``;;

printfn "ApiDocs test that cref generation works"
runtest ``ApiDocs test that cref generation works``;;

#endif
