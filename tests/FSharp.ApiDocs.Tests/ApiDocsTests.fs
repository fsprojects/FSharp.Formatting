[<NUnit.Framework.TestFixture>]
module ApiDocs.Tests

open FsUnit
open System.IO
open NUnit.Framework
open FSharp.Formatting.ApiDocs
open FsUnitTyped

// --------------------------------------------------------------------------------------
// Run the metadata formatter on sample project
// --------------------------------------------------------------------------------------

let (</>) a b = Path.Combine(a, b)
let fullpath = Path.GetFullPath
let fullpaths = List.map fullpath

let root = __SOURCE_DIRECTORY__ |> fullpath


// NOTE - For these tests to run properly they require the output of all the metadata
// test project to be directed to the directory below
let testBin = AttributeTests.testBin

let getOutputDir (uniq: string)  =
  let outDir = __SOURCE_DIRECTORY__ + "/output/" + uniq
  while (try Directory.Exists outDir with _ -> false) do
      Directory.Delete(outDir, true)
  Directory.CreateDirectory(outDir).FullName

let removeWhiteSpace (str:string) =
    str.Replace("\n", "").Replace("\r", "").Replace(" ", "")

let docTemplate =
  root </> "../../misc/templates/_template.html"

let parameters =
  [ "project-name", "F# TestProject"
    "authors", "Your Name"
    "repository-url", "http://github.com/fsprojects/fsharp-test-project"
    "root", "http://fsprojects.github.io/fsharp-test-project" ]

let generateApiDocs (libraries:string list) useMarkdown uniq =
    try
        let output = getOutputDir uniq
        let metadata = ApiDocs.GenerateModel (libraries,parameters=parameters,libDirs = [root],markDownComments = useMarkdown)
        ApiDocs.GenerateHtmlFromModel (metadata, output, docTemplate)

        let fileNames = Directory.GetFiles(output)
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
let ``ApiDocs works on sample Deedle assembly``() =
  let library = root </> "files" </> "Deedle.dll"
  let output = getOutputDir "Deedle"

  let index =
      ApiDocs.GenerateHtml
        ( [library], output, template=docTemplate, parameters=parameters, libDirs = [testBin],
          sourceRepo = "https://github.com/fslaborg/Deedle/",
          sourceFolder = "c:/dev/FSharp.DataFrame")
  let files = Directory.GetFiles(output)

  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith "index.html")
  optIndex.IsSome |> shouldEqual true

  let optSeriesMod = files |> Seq.tryFind (fun s -> s.Contains "seriesmodule")
  optSeriesMod.IsSome |> shouldEqual true

[<Test; Ignore "Ignore by default to make tests run reasonably fast">]
let ``ApiDocs works on sample FAKE assembly``() =
  let library = root </> "files" </> "FAKE" </> "FakeLib.dll"
  let output = getOutputDir "FakeLib"
  let searchIndex = ApiDocs.GenerateHtml([library], output, template=docTemplate, parameters=parameters)
  let files = Directory.GetFiles(output)
  files |> Seq.length |> shouldEqual 166


[<Test>]
let ``ApiDocs works on two sample F# assemblies``() =
  let libraries =
    [ testBin </> "FsLib1.dll"
      testBin </> "FsLib2.dll" ]
  let output = getOutputDir "FsLib12"
  let searchIndex = ApiDocs.GenerateHtml(libraries, output, template=docTemplate, rootUrl="http://root.io/root", parameters=parameters, libDirs = [testBin])

  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // Check that all comments appear in the output
  files.["fslib-class.html"] |> shouldContainText "Readonly int property"
  files.["fslib-record.html"] |> shouldContainText "This is name"
  files.["fslib-record.html"] |> shouldContainText "Additional member"
  files.["fslib-union.html"] |> shouldContainText "Hello of int"
  files.["index.html"] |> shouldContainText "Sample class"
  files.["index.html"] |> shouldContainText "Union sample"
  files.["index.html"] |> shouldContainText "Record sample"
  files.["fslib-nested.html"] |> shouldContainText "Somewhat nested type"
  files.["fslib-nested.html"] |> shouldContainText "Somewhat nested module"
  files.["fslib-nested-nestedtype.html"] |> shouldContainText "Very nested member"
  files.["fslib-nested-submodule.html"] |> shouldContainText "Very nested field"

  // Check that union fields are correctly generated
  files.["fslib-union.html"] |> shouldContainText "World(string, int)"
  files.["fslib-union.html"] |> shouldContainText "Naming(rate, string)"

  // Check that methods with no arguments are correctly generated (#113)
  files.["fslib-record.html"] |> shouldNotContainText "Foo2(arg1)"
  files.["fslib-record.html"] |> shouldContainText "Foo2()"
  files.["fslib-record.html"] |> shouldContainText "Signature"
  files.["fslib-record.html"] |> shouldContainText "unit -&gt; int"
  files.["fslib-class.html"] |> shouldContainText "Class()"
  files.["fslib-class.html"] |> shouldContainText "unit -&gt; Class"

  // Check that properties are correctly generated (#114)
  files.["fslib-class.html"] |> removeWhiteSpace |> shouldNotContainText ">this.Member(arg1)<"
  files.["fslib-class.html"] |> removeWhiteSpace |> shouldNotContainText ">this.Member()<"
  files.["fslib-class.html"] |> removeWhiteSpace |> shouldContainText ">this.Member<"
  files.["fslib-class.html"] |> shouldNotContainText "unit -&gt; int"
  files.["fslib-class.html"] |> shouldContainText "Signature:"

  // Check that formatting is correct
  files.["fslib-test_issue472_r.html"] |> shouldContainText "Test_Issue472_R.fmultipleargs x y"
  files.["fslib-test_issue472_r.html"] |> shouldContainText "Test_Issue472_R.ftupled(x, y)"
  files.["fslib-test_issue472.html"] |> shouldContainText "fmultipleargs x y"
  files.["fslib-test_issue472.html"] |> shouldContainText "ftupled(x, y)"
  files.["fslib-test_issue472_t.html"] |> shouldContainText "this.MultArg(arg1, arg2)"
  files.["fslib-test_issue472_t.html"] |> shouldContainText "this.MultArgTupled(arg)"
  files.["fslib-test_issue472_t.html"] |> shouldContainText "this.MultPartial arg1 arg2"

  let indxTxt = searchIndex |> Newtonsoft.Json.JsonConvert.SerializeObject

  // Test a few entries in the search index
  indxTxt |> shouldContainText "\"uri\""
  indxTxt |> shouldContainText "\"content\""
  indxTxt |> shouldContainText "\"title\""
  indxTxt |> shouldContainText "http://root.io/root/reference/fslib-nested-submodule-verynestedtype.html#Member"
  indxTxt |> shouldContainText "http://root.io/root/reference/fslib-test_issue472_t.html#MultArg"
  indxTxt |> shouldContainText """ITest_Issue229.Name \nName \n"""
  indxTxt |> shouldContainText """DuplicatedTypeName \n<p>This type name will be duplicated in"""

[<Test>]
let ``ApiDocs model generation works on two sample F# assemblies``() =
  let libraries =
    [ testBin </> "FsLib1.dll"
      testBin </> "FsLib2.dll" ]
  let model = ApiDocs.GenerateModel(libraries, parameters=parameters, libDirs = [testBin])
  model.AssemblyGroup.Assemblies.Length |> shouldEqual 2
  model.AssemblyGroup.Assemblies.[0].Name |> shouldEqual "FsLib1"
  model.AssemblyGroup.Assemblies.[1].Name |> shouldEqual "FsLib2"
  model.AssemblyGroup.Namespaces.Length |> shouldEqual 1
  model.AssemblyGroup.Namespaces.[0].Name |> shouldEqual "FsLib"
  model.AssemblyGroup.Namespaces.[0].Types.Length |> shouldEqual 9
  let assemblies = [ for t in model.AssemblyGroup.Namespaces.[0].Types -> t.Assembly.Name ]
  assemblies |> List.distinct |> List.sort |> shouldEqual ["FsLib1"; "FsLib2"]

[<Test>]
let ``ApiDocs generates Go to GitHub source links``() =
  let libraries =
    [ testBin  </> "FsLib1.dll"
      testBin  </> "FsLib2.dll" ] |> fullpaths
  let output = getOutputDir "FsLib12_SourceLinks"
  printfn "Output: %s" output
  let searchIndex =
    ApiDocs.GenerateHtml
      ( libraries, output, template=docTemplate, parameters=parameters, libDirs = ([testBin] |> fullpaths),
        sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
        sourceFolder = (root </> "../..") )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.["fslib-class.html"] |> shouldContainText "github-link"
  files.["fslib-class.html"] |> shouldContainText "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib2/Library2.fs#L"
  files.["fslib-record.html"] |> shouldContainText "github-link"
  files.["fslib-record.html"] |> shouldContainText "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib1/Library1.fs#L"
  files.["fslib-union.html"] |> shouldContainText "github-link"
  files.["fslib-union.html"] |> shouldContainText "https://github.com/fsprojects/FSharp.Formatting/tree/master/tests/FSharp.ApiDocs.Tests/files/FsLib1/Library1.fs#L"

[<Test>]
let ``ApiDocs test that cref generation works``() =
  let libraries =
    [ testBin  </> "crefLib1.dll"
      testBin  </> "crefLib2.dll"
      testBin  </> "crefLib3.dll"
      testBin  </> "crefLib4.dll" ] |> fullpaths
  let output = getOutputDir "crefLibs"
  printfn "Output: %s" output
  let searchIndex =
    ApiDocs.GenerateHtml
      ( libraries, output, template=docTemplate, parameters=parameters, libDirs = ([testBin]  |> fullpaths),
        sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
        sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
        markDownComments = false )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests
  // reference class in same assembly
  files.["creflib4-class1.html"] |> shouldContainText "Class2"
  files.["creflib4-class1.html"] |> shouldContainText "creflib4-class2.html"
  // reference to another assembly
  files.["creflib4-class2.html"] |> shouldContainText "Class1"
  files.["creflib4-class2.html"] |> shouldContainText "creflib1-class1.html"
  /// + no crash on unresolved reference.
  files.["creflib4-class2.html"] |> shouldContainText "Unknown__Reference"
  /// reference to a member works.
  files.["creflib4-class3.html"] |> shouldContainText "Class2.Other"
  files.["creflib4-class3.html"] |> shouldContainText "creflib4-class2.html"

  /// reference to a corelib class works.
  files.["creflib4-class4.html"] |> shouldContainText "Assembly"
  files.["creflib4-class4.html"] |> shouldContainText "http://msdn.microsoft.com/en-us/library/System.Reflection.Assembly"


  // F# tests (at least we not not crash for them, compiler doesn't resolve anything)
  // reference class in same assembly
  files.["creflib2-class1.html"] |> shouldContainText "Class2"
  //files.["creflib2-class1.html"] |> shouldContainText "creflib2-class2.html"
  // reference to another assembly
  files.["creflib2-class2.html"] |> shouldContainText "Class1"
  //files.["creflib2-class2.html"] |> shouldContainText "creflib1-class1.html"
  /// + no crash on unresolved reference.
  files.["creflib2-class2.html"] |> shouldContainText "Unknown__Reference"
  /// reference to a member works.
  files.["creflib2-class3.html"] |> shouldContainText "Class2.Other"
  //files.["creflib2-class3.html"] |> shouldContainText "creflib2-class2.html"

  /// reference to a corelib class works.
  files.["creflib2-class4.html"] |> shouldContainText "Assembly"
  //files.["creflib2-class4.html"] |> shouldContainText "http://msdn.microsoft.com/en-us/library/System.Reflection.Assembly"

  // F# tests (fully quallified)
  // reference class in same assembly
  files.["creflib2-class5.html"] |> shouldContainText "Class2"
  files.["creflib2-class5.html"] |> shouldContainText "creflib2-class2.html"
  // reference to another assembly
  files.["creflib2-class6.html"] |> shouldContainText "Class1"
  files.["creflib2-class6.html"] |> shouldContainText "creflib1-class1.html"
  /// + no crash on unresolved reference.
  files.["creflib2-class6.html"] |> shouldContainText "Unknown__Reference"
  /// reference to a member works.
  files.["creflib2-class7.html"] |> shouldContainText "Class2.Other"
  files.["creflib2-class7.html"] |> shouldContainText "creflib2-class2.html"

  /// reference to a corelib class works.
  files.["creflib2-class8.html"] |> shouldContainText "Assembly"
  files.["creflib2-class8.html"] |> shouldContainText "http://msdn.microsoft.com/en-us/library/System.Reflection.Assembly"

[<Test>]
let ``ApiDocs test that csharp (publiconly) support works``() =
  let libraries =
    [ testBin </> "csharpSupport.dll" ] |> fullpaths
  let output = getOutputDir "csharpSupport"
  printfn "Output: %s" output
  let searchIndex =
    ApiDocs.GenerateHtml
      ( libraries, output, template=docTemplate, parameters=parameters, libDirs = ([testBin]  |> fullpaths),
        sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
        sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
        publicOnly = true,
        markDownComments = false )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Sample_Class"

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Constructor"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Event"

  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Constructor"
  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Event"

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Static_Event"


  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldNotContainText "My_Private_Static_Event"

  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Sample_Class"

  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Event"

  files.["csharpsupport-samplestaticclass.html"] |> shouldNotContainText "My_Private_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> shouldNotContainText "My_Private_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> shouldNotContainText "My_Private_Static_Event"

  //#if INTERACTIVE
  //System.Diagnostics.Process.Start(output)
  //#endif


[<Ignore "Ignored because publicOnly=false is currently not working, see https://github.com/fsprojects/FSharp.Formatting/pull/259" >]
[<Test>]
let ``ApiDocs test that csharp support works``() =
  let libraries =
    [ testBin </> "csharpSupport.dll" ] |> fullpaths
  let output = getOutputDir "csharpSupport_private"
  printfn "Output: %s" output
  let searchIndex =
    ApiDocs.GenerateHtml
      ( libraries, output, template=docTemplate, parameters=parameters, libDirs = ([testBin] |> fullpaths),
        sourceRepo = "https://github.com/fsprojects/FSharp.Formatting/tree/master",
        sourceFolder = (__SOURCE_DIRECTORY__ </> "../.."),
        publicOnly = false,
        markDownComments = false )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Sample_Class"

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Constructor"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Event"

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Constructor"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Event"

  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Static_Event"


  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> shouldContainText "My_Private_Static_Event"

  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Sample_Class"

  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Static_Event"

  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Private_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Private_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> shouldContainText "My_Private_Static_Event"

[<Test>]
let ``ApiDocs process XML comments in two sample F# assemblies``() =
  let libraries =
    [ testBin  </> "TestLib1.dll"
      testBin </> "TestLib2.dll" ] |> fullpaths
  let files = generateApiDocs libraries  false "TestLibs"
  files.["fslib-class.html"] |> shouldContainText "Readonly int property"
  files.["fslib-record.html"] |> shouldContainText "This is name"
  files.["fslib-record.html"] |> shouldContainText "Additional member"
  files.["fslib-union.html"] |> shouldContainText "Hello of int"
  files.["index.html"] |> shouldContainText "Sample class"
  files.["index.html"] |> shouldContainText "Union sample"
  files.["index.html"] |> shouldContainText "Record sample"
  files.["fslib-nested.html"] |> shouldContainText "Somewhat nested type"
  files.["fslib-nested.html"] |> shouldContainText "Somewhat nested module"
  files.["fslib-nested-nestedtype.html"] |> shouldContainText "Very nested member"
  files.["fslib-nested-submodule.html"] |> shouldContainText "Very nested field"

[<Test>]
let ``ApiDocs highlights code snippets in Markdown comments``() =
  let library = testBin </> "TestLib1.dll" |> fullpath

  let files = generateApiDocs [library] true "TestLib1"

  files.["fslib-myclass.html"] |> shouldContainText """<span class="k">let</span>"""
  files.["fslib-myclass.html"] |> shouldContainText """<span class="k">var</span>"""
  files.["fslib-myclass.html"] |> shouldContainText """val a : FsLib.MyClass"""

[<Test>]
let ``ApiDocs handles c# dlls`` () =
  let library = testBin </> "FSharp.Formatting.CSharpFormat.dll" |> fullpath

  let files = (generateApiDocs [library] false "CSharpFormat").Keys

  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith "index.html")
  optIndex.IsSome |> shouldEqual true

[<Test>]
let ``ApiDocs processes C# types and includes xml comments in docs`` () =
    let library = __SOURCE_DIRECTORY__ </> "files" </> "CSharpFormat.dll" |> fullpath

    let files = generateApiDocs [library]  false "CSharpFormat2"

    files.["index.html"] |> shouldContainText "CLikeFormat"
    files.["index.html"] |> shouldContainText "Provides a base class for formatting languages similar to C."

[<Test>]
let ``ApiDocs processes C# properties on types and includes xml comments in docs`` () =
    let library = __SOURCE_DIRECTORY__ </> "files" </> "CSharpFormat.dll" |> fullpath

    let files = generateApiDocs [library] false "CSharpFormat3"

    files.["manoli-utils-csharpformat-clikeformat.html"] |> shouldContainText "CommentRegEx"
    files.["manoli-utils-csharpformat-clikeformat.html"] |> shouldContainText "Regular expression string to match single line and multi-line"

[<Test>]
let ``ApiDocs generates module link in nested types``() =

  let library =  testBin  </> "FsLib2.dll"

  let files = generateApiDocs [library] false "FsLib2"

  // Check that the modules and type files have namespace information
  files.["fslib-class.html"] |> shouldContainText "Namespace: FsLib"
  files.["fslib-nested.html"] |> shouldContainText "Namespace: FsLib"
  files.["fslib-nested-nestedtype.html"] |> shouldContainText "Namespace: FsLib"
  files.["fslib-nested-submodule.html"] |> shouldContainText "Namespace: FsLib"
  files.["fslib-nested-submodule-verynestedtype.html"] |> shouldContainText "Namespace: FsLib"

  // Check that the link to the module is correctly generated
  files.["fslib-nested-nestedtype.html"] |> shouldContainText "Parent Module:"
  files.["fslib-nested-nestedtype.html"] |> shouldContainText "<a href=\"fslib-nested.html\">"

  // Only for nested types
  files.["fslib-class.html"] |> shouldNotContainText "Parent Module:"

  // Check that the link to the module is correctly generated for types in nested modules
  files.["fslib-nested-submodule-verynestedtype.html"] |> shouldContainText "Parent Module:"
  files.["fslib-nested-submodule-verynestedtype.html"] |> shouldContainText "<a href=\"fslib-nested-submodule.html\">"

  // Check that nested submodules have links to its module
  files.["fslib-nested-submodule.html"] |> shouldContainText "Parent Module:"
  files.["fslib-nested-submodule.html"] |> shouldContainText "<a href=\"fslib-nested.html\">"

open System.Diagnostics
open FSharp.Formatting.Common

[<Test>]
let ``ApiDocs omit works without markdown``() =
  let library = testBin </> "FsLib2.dll" |> fullpath

  let files = generateApiDocs [library] false "FsLib2_omit"

  files.ContainsKey "fslib-test_omit.html" |> shouldEqual false

[<Test>]
let ``ApiDocs test FsLib1``() =
  let library = testBin </> "FsLib1.dll" |> fullpath

  let files = generateApiDocs [library] false "FsLib1_omit"

  files.ContainsKey "fslib-test_omit.html" |> shouldEqual false

// -------------------Indirect links----------------------------------
[<Test>]
let ``ApiDocs generates cross-type links for Indirect Links``() =
  let library = testBin </> "FsLib2.dll" |> fullpath

  let files = generateApiDocs [library]  true "FsLib2_indirect"

  // Check that a link to MyType exists when using Full Name of the type
  files.["fslib-nested.html"] |> shouldContainText "This function returns a <a href=\"fslib-nested-mytype.html\" title=\"MyType\">FsLib.Nested.MyType</a>"

  // Check that a link to OtherType exists when using Logical Name of the type only
  files.["fslib-nested.html"] |> shouldContainText "This function returns a <a href=\"fslib-nested-othertype.html\" title=\"OtherType\">OtherType</a>"

  // Check that a link to a module is created when using Logical Name only
  files.["fslib-duplicatedtypename.html"] |> shouldContainText "This type name will be duplicated in <a href=\"fslib-nested.html\" title=\"Nested\">Nested</a>"

  // Check that a link to a type with a duplicated name is created when using full name
  files.["fslib-nested-duplicatedtypename.html"] |> shouldContainText "This type has the same name as <a href=\"fslib-duplicatedtypename.html\" title=\"DuplicatedTypeName\">FsLib.DuplicatedTypeName</a>"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> shouldContainText "This function returns a [DuplicatedTypeName] multiplied by 4."

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> shouldContainText "This function returns a [InexistentTypeName] multiplied by 5."

  // -------------------Inline code----------------------------------
[<Test>]
let ``Metadata generates cross-type links for Inline Code``() =
  let library = testBin </> "FsLib2.dll" |> fullpath

  let files = generateApiDocs [library] true "FsLib2_inline"

  // Check that a link to MyType exists when using Full Name of the type in a inline code
  files.["fslib-nested.html"] |> shouldContainText "You will notice that <a href=\"fslib-nested-mytype.html\" title=\"MyType\"><code>FsLib.Nested.MyType</code></a> is just an <code>int</code>"

    // Check that a link to MyType exists when using Full Name of the type in a inline code
  files.["fslib-nested.html"] |> shouldContainText "You will notice that <a href=\"fslib-nested-othertype.html\" title=\"OtherType\"><code>OtherType</code></a> is just an <code>int</code>"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> shouldContainText "<code>DuplicatedTypeName</code> is duplicated so it should no add a cross-type link"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> shouldContainText "<code>InexistentTypeName</code> does not exists so it should no add a cross-type link"

  // Check that a link to a module is created when using Logical Name only
  files.["fslib-duplicatedtypename.html"] |> shouldContainText "This type name will be duplicated in <a href=\"fslib-nested.html\" title=\"Nested\"><code>Nested</code></a>"

  // Check that a link to a type with a duplicated name is created when using full name
  files.["fslib-nested-duplicatedtypename.html"] |> shouldContainText "This type has the same name as <a href=\"fslib-duplicatedtypename.html\" title=\"DuplicatedTypeName\"><code>FsLib.DuplicatedTypeName</code></a>"


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
