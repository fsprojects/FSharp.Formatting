#if INTERACTIVE
#I "../../bin"
#r "FSharp.MetadataFormat.dll"
#r "FSharp.Compiler.Service.dll"
#r "RazorEngine.dll"
#r "../../packages/NUnit/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.MetadataFormat.Tests
#endif

open FsUnit
open System.IO
open NUnit.Framework
open FSharp.MetadataFormat

// --------------------------------------------------------------------------------------
// Run the metadata formatter on sample project 
// --------------------------------------------------------------------------------------

let (@@) a b = Path.Combine(a, b)

let root = __SOURCE_DIRECTORY__ 

let getOutputDir()  = 
  let tempFile = Path.GetTempFileName()
  File.Delete(tempFile)
  Directory.CreateDirectory(tempFile).FullName

let layoutRoots = 
  [ root @@ "../../misc/templates"
    root @@ "../../misc/templates/reference" ]

let info =
  [ "project-name", "FSharp.ProjectScaffold"
    "project-author", "Your Name"
    "project-summary", "A short summary of your project"
    "project-github", "http://github.com/pblasucci/fsharp-project-scaffold"
    "project-nuget", "http://nuget.com/packages/FSharp.ProjectScaffold"
    "root", "http://tpetricek.github.io/FSharp.FSharp.ProjectScaffold" ]

[<Test>]
let ``MetadataFormat works on sample Deedle assembly``() = 
  let library = root @@ "files" @@ "Deedle.dll"
  let output = getOutputDir()
  MetadataFormat.Generate
    ( library, output, layoutRoots, info, libDirs = [root @@ "../../lib"],
      sourceRepo = "https://github.com/BlueMountainCapital/Deedle/tree/master/",
      sourceFolder = "c:/dev/FSharp.DataFrame")
  let files = Directory.GetFiles(output)
  
  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith "index.html")
  optIndex.IsSome |> shouldEqual true
  
  let optSeriesMod = files |> Seq.tryFind (fun s -> s.Contains "seriesmodule")
  optSeriesMod.IsSome |> shouldEqual true

  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

// Ignore by default to make tests run reasonably fast
[<Test; Ignore>]
let ``MetadataFormat works on sample FAKE assembly``() = 
  let library = root @@ "files" @@ "FAKE" @@ "FakeLib.dll"
  let output = getOutputDir()
  MetadataFormat.Generate(library, output, layoutRoots, info)
  let files = Directory.GetFiles(output)
  files |> Seq.length |> shouldEqual 166

let removeWhiteSpace (str:string) =
    str.Replace("\n", "").Replace("\r", "").Replace(" ", "")

[<Test>]
let ``MetadataFormat works on two sample F# assemblies``() =
  let binDir = root @@ "files" @@ "FsLib" @@ "bin" @@ "Debug" 
  let libraries = 
    [ binDir @@ "FsLib1.dll"
      binDir @@ "FsLib2.dll" ]
  let output = getOutputDir()
  MetadataFormat.Generate(libraries, output, layoutRoots, info, libDirs = [binDir; root @@ "../../lib"])
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // Check that all comments appear in the output
  files.["fslib-class.html"] |> should contain "Readonly int property"
  files.["fslib-record.html"] |> should contain "This is name"
  files.["fslib-record.html"] |> should contain "Additional member"
  files.["fslib-union.html"] |> should contain "Hello of int"
  files.["index.html"] |> should contain "Sample class"
  files.["index.html"] |> should contain "Union sample"
  files.["index.html"] |> should contain "Record sample"
  files.["fslib-nested.html"] |> should contain "Somewhat nested type"
  files.["fslib-nested.html"] |> should contain "Somewhat nested module"
  files.["fslib-nested-nestedtype.html"] |> should contain "Very nested member"
  files.["fslib-nested-submodule.html"] |> should contain "Very nested field"

  // Check that union fields are correctly generated
  files.["fslib-union.html"] |> should contain "World(string,int)"
#if FSHARP_31
  files.["fslib-union.html"] |> should contain "Naming(rate,string)"
#endif

  // Check that methods with no arguments are correctly generated (#113)
  files.["fslib-record.html"] |> should notContain "Foo2(arg1)"
  files.["fslib-record.html"] |> should contain "Foo2()"
  files.["fslib-record.html"] |> should contain "<strong>Signature:</strong> unit -&gt; int"
  files.["fslib-class.html"] |> should contain "new()"
  files.["fslib-class.html"] |> should contain "<strong>Signature:</strong> unit -&gt; Class"  

  // Check that properties are correctly generated (#114)
  files.["fslib-class.html"] |> removeWhiteSpace |> should notContain ">Member(arg1)<"
  files.["fslib-class.html"] |> removeWhiteSpace |> should notContain ">Member()<"
  files.["fslib-class.html"] |> removeWhiteSpace |> should contain ">Member<"
  files.["fslib-class.html"] |> should notContain "<strong>Signature:</strong> unit -&gt; int"
  files.["fslib-class.html"] |> should contain "<strong>Signature:</strong> int"
  
  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

[<Test>]
let ``MetadataFormat generates Go to GitHub source links``() =
  let binDir = root @@ "files" @@ "FsLib" @@ "bin" @@ "Debug"
  let libraries = 
    [ binDir @@ "FsLib1.dll"
      binDir @@ "FsLib2.dll" ]
  let output = getOutputDir()
  printfn "Output: %s" output
  MetadataFormat.Generate
    ( libraries, output, layoutRoots, info, libDirs = [binDir; root @@ "../../lib"], 
      sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/tree/master",
      sourceFolder = root @@ "../.." )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.["fslib-class.html"] |> should contain "github-link"
  files.["fslib-class.html"] |> should contain "https://github.com/tpetricek/FSharp.Formatting/tree/master/tests/FSharp.MetadataFormat.Tests/files/FsLib/Library2.fs#L"
  files.["fslib-record.html"] |> should contain "github-link"
  files.["fslib-record.html"] |> should contain "https://github.com/tpetricek/FSharp.Formatting/tree/master/tests/FSharp.MetadataFormat.Tests/files/FsLib/Library1.fs#L"
  files.["fslib-union.html"] |> should contain "github-link"
  files.["fslib-union.html"] |> should contain "https://github.com/tpetricek/FSharp.Formatting/tree/master/tests/FSharp.MetadataFormat.Tests/files/FsLib/Library1.fs#L"
  
  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

[<Test>]
let ``MetadataFormat test that cref generation works``() =
  let libraries =
    [ root @@ "files/crefLib/bin/Debug" @@ "crefLib1.dll"
      root @@ "files/crefLib/bin/Debug" @@ "crefLib2.dll"
      root @@ "files/crefLib/bin/Debug" @@ "crefLib3.dll"
      root @@ "files/crefLib/bin/Debug" @@ "crefLib4.dll" ]
  let output = getOutputDir()
  printfn "Output: %s" output
  MetadataFormat.Generate
    ( libraries, output, layoutRoots, info, libDirs = [root @@ "../../lib"],
      sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ "../..",
      markDownComments = false )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests
  // reference class in same assembly
  files.["creflib4-class1.html"] |> should contain "Class2"
  files.["creflib4-class1.html"] |> should contain "creflib4-class2.html"
  // reference to another assembly
  files.["creflib4-class2.html"] |> should contain "Class1"
  files.["creflib4-class2.html"] |> should contain "creflib1-class1.html"
  /// + no crash on unresolved reference.
  files.["creflib4-class2.html"] |> should contain "Unknown__Reference"
  /// reference to a member works.
  files.["creflib4-class3.html"] |> should contain "Class2.Other"
  files.["creflib4-class3.html"] |> should contain "creflib4-class2.html"

  /// reference to a corelib class works.
  files.["creflib4-class4.html"] |> should contain "Assembly"
  files.["creflib4-class4.html"] |> should contain "http://msdn.microsoft.com/en-us/library/System.Reflection.Assembly"


  // F# tests (at least we not not crash for them, compiler doesn't resolve anything)
  // reference class in same assembly
  files.["creflib2-class1.html"] |> should contain "Class2"
  //files.["creflib2-class1.html"] |> should contain "creflib2-class2.html"
  // reference to another assembly
  files.["creflib2-class2.html"] |> should contain "Class1"
  //files.["creflib2-class2.html"] |> should contain "creflib1-class1.html"
  /// + no crash on unresolved reference.
  files.["creflib2-class2.html"] |> should contain "Unknown__Reference"
  /// reference to a member works.
  files.["creflib2-class3.html"] |> should contain "Class2.Other"
  //files.["creflib2-class3.html"] |> should contain "creflib2-class2.html"

  /// reference to a corelib class works.
  files.["creflib2-class4.html"] |> should contain "Assembly"
  //files.["creflib2-class4.html"] |> should contain "http://msdn.microsoft.com/en-us/library/System.Reflection.Assembly"

  // F# tests (fully quallified)
  // reference class in same assembly
  files.["creflib2-class5.html"] |> should contain "Class2"
  files.["creflib2-class5.html"] |> should contain "creflib2-class2.html"
  // reference to another assembly
  files.["creflib2-class6.html"] |> should contain "Class1"
  files.["creflib2-class6.html"] |> should contain "creflib1-class1.html"
  /// + no crash on unresolved reference.
  files.["creflib2-class6.html"] |> should contain "Unknown__Reference"
  /// reference to a member works.
  files.["creflib2-class7.html"] |> should contain "Class2.Other"
  files.["creflib2-class7.html"] |> should contain "creflib2-class2.html"

  /// reference to a corelib class works.
  files.["creflib2-class8.html"] |> should contain "Assembly"
  files.["creflib2-class8.html"] |> should contain "http://msdn.microsoft.com/en-us/library/System.Reflection.Assembly"

  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

[<Test>]
let ``MetadataFormat test that csharp (publiconly) support works``() =
  let libraries =
    [ root @@ "files/csharpSupport/bin/Debug" @@ "csharpSupport.dll" ]
  let output = getOutputDir()
  printfn "Output: %s" output
  MetadataFormat.Generate
    ( libraries, output, layoutRoots, info, libDirs = [root @@ "../../lib"],
      sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ "../..",
      publicOnly = true,
      markDownComments = false )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Sample_Class"

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Constructor"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Method"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Property"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Event"

  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Constructor")
  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Method")
  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Property")
  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Event")

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Static_Event"
  
  
  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Static_Method")
  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Static_Property")
  files.["csharpsupport-sampleclass.html"] |> should not' (contain "My_Private_Static_Event")

  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Sample_Class"
  
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Event"
  
  files.["csharpsupport-samplestaticclass.html"] |> should not' (contain "My_Private_Static_Method")
  files.["csharpsupport-samplestaticclass.html"] |> should not' (contain "My_Private_Static_Property")
  files.["csharpsupport-samplestaticclass.html"] |> should not' (contain "My_Private_Static_Event")
  
  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

  
[<Ignore>] // Ignored because publicOnly=false is currently not working, see https://github.com/tpetricek/FSharp.Formatting/pull/259
[<Test>]
let ``MetadataFormat test that csharp support works``() =
  let libraries =
    [ root @@ "files/csharpSupport/bin/Debug" @@ "csharpSupport.dll" ]
  let output = getOutputDir()
  printfn "Output: %s" output
  MetadataFormat.Generate
    ( libraries, output, layoutRoots, info, libDirs = [root @@ "../../lib"],
      sourceRepo = "https://github.com/tpetricek/FSharp.Formatting/tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ "../..",
      publicOnly = false,
      markDownComments = false )
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // C# tests

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Sample_Class"

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Constructor"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Method"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Property"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Event"

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Constructor"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Method"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Property"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Event"

  files.["csharpsupport-sampleclass.html"] |> should contain "My_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Static_Event"
  
  
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Static_Method"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Static_Property"
  files.["csharpsupport-sampleclass.html"] |> should contain "My_Private_Static_Event"

  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Sample_Class"
  
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Static_Event"
  
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Private_Static_Method"
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Private_Static_Property"
  files.["csharpsupport-samplestaticclass.html"] |> should contain "My_Private_Static_Event"
  
  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

[<Test>]
let ``MetadataFormat process XML comments in two sample F# assemblies``() = 
  let libraries = 
    [ root @@ "files/TestLib/bin/Debug" @@ "TestLib1.dll"
      root @@ "files/TestLib/bin/Debug" @@ "TestLib2.dll" ]
  let output = getOutputDir()
  MetadataFormat.Generate(libraries, output, layoutRoots, info, libDirs = [root @@ "../../lib"], markDownComments = false)
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.["fslib-class.html"] |> should contain "Readonly int property"
  files.["fslib-record.html"] |> should contain "This is name"
  files.["fslib-record.html"] |> should contain "Additional member"
  files.["fslib-union.html"] |> should contain "Hello of int"
  files.["index.html"] |> should contain "Sample class"
  files.["index.html"] |> should contain "Union sample"
  files.["index.html"] |> should contain "Record sample"
  files.["fslib-nested.html"] |> should contain "Somewhat nested type"
  files.["fslib-nested.html"] |> should contain "Somewhat nested module"
  files.["fslib-nested-nestedtype.html"] |> should contain "Very nested member"
  files.["fslib-nested-submodule.html"] |> should contain "Very nested field"

[<Test>]
let ``MetadataFormat highlights code snippets in Markdown comments``() = 
  let library = root @@ "files/TestLib/bin/Debug" @@ "TestLib1.dll"
  let output = getOutputDir()
  MetadataFormat.Generate([library], output, layoutRoots, info, libDirs = [root @@ "../../lib"], markDownComments = true)
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.["fslib-myclass.html"] |> should contain """<span class="k">let</span>"""
  files.["fslib-myclass.html"] |> should contain """<span class="k">var</span>"""
  files.["fslib-myclass.html"] |> should contain """val a : FsLib.MyClass"""

[<Test>]
let ``MetadataFormat handles c# dlls`` () =
  let library = root @@ "files" @@ "CSharpFormat.dll"
  let output = getOutputDir()
  MetadataFormat.Generate
    ( library, output, layoutRoots, info, libDirs = [root @@ "../../lib"])
  let files = Directory.GetFiles(output)
  
  let optIndex = files |> Seq.tryFind (fun s -> s.EndsWith "index.html")
  optIndex.IsSome |> shouldEqual true

  #if INTERACTIVE
  System.Diagnostics.Process.Start(output)
  #endif

[<Test>]
let ``MetadataFormat processes C# types and includes xml comments in docs`` () =
    let library = root @@ "files" @@ "CSharpFormat.dll"
    let output = getOutputDir()
    MetadataFormat.Generate
        ( library, output, layoutRoots, info, libDirs = [root @@ "../../lib"])
    let fileNames = Directory.GetFiles(output)
    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
    files.["index.html"] |> should contain "CLikeFormat"
    files.["index.html"] |> should contain "Provides a base class for formatting languages similar to C."

[<Test>]
let ``MetadataFormat processes C# properties on types and includes xml comments in docs`` () =
    let library = root @@ "files" @@ "CSharpFormat.dll"
    let output = getOutputDir()
    MetadataFormat.Generate
        ( library, output, layoutRoots, info, libDirs = [root @@ "../../lib"])
    let fileNames = Directory.GetFiles(output)
    let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ] 
    
    files.["manoli-utils-csharpformat-clikeformat.html"] |> should contain "CommentRegEx"
    files.["manoli-utils-csharpformat-clikeformat.html"] |> should contain "Regular expression string to match single line and multi-line"

[<Test>]
let ``MetadataFormat generates module link in nested types``() =
  let binDir = root @@ "files/FsLib/bin/Debug"
  let library = binDir @@ "FsLib2.dll"
  let output = getOutputDir()
  MetadataFormat.Generate([library], output, layoutRoots, info, libDirs = [binDir; root @@ "../../lib"], markDownComments = true)
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  
  // Check that the modules and type files have namespace information
  files.["fslib-class.html"] |> should contain "Namespace: FsLib"
  files.["fslib-nested.html"] |> should contain "Namespace: FsLib"
  files.["fslib-nested-nestedtype.html"] |> should contain "Namespace: FsLib"
  files.["fslib-nested-submodule.html"] |> should contain "Namespace: FsLib"
  files.["fslib-nested-submodule-verynestedtype.html"] |> should contain "Namespace: FsLib"

  // Check that the link to the module is correctly generated
  files.["fslib-nested-nestedtype.html"] |> should contain "Parent Module:"
  files.["fslib-nested-nestedtype.html"] |> should contain "<a href=\"fslib-nested.html\">Nested</a>"

  // Only for nested types
  files.["fslib-class.html"] |> should notContain "Parent Module:"

  // Check that the link to the module is correctly generated for types in nested modules
  files.["fslib-nested-submodule-verynestedtype.html"] |> should contain "Parent Module:"
  files.["fslib-nested-submodule-verynestedtype.html"] |> should contain "<a href=\"fslib-nested-submodule.html\">Submodule</a>"

  // Check that nested submodules have links to its module
  files.["fslib-nested-submodule.html"] |> should contain "Parent Module:"
  files.["fslib-nested-submodule.html"] |> should contain "<a href=\"fslib-nested.html\">Nested</a>"

open System.Diagnostics
open FSharp.Formatting.Common

[<Test>]
let ``MetadataFormat omit works without markdown``() =
  let binDir = root @@ "files/FsLib/bin/Debug"
  let library = binDir @@ "FsLib2.dll"
  let output = getOutputDir()
  MetadataFormat.Generate
    ([library], output, layoutRoots, info, libDirs = [binDir; root @@ "../../lib"],
     markDownComments = false)
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  
  files.ContainsKey "fslib-test_omit.html" |> should equal false

[<Test>]
let ``MetadataFormat test FsLib1``() =
  let binDir = root @@ "files/FsLib/bin/Debug"
  let library = binDir @@ "FsLib1.dll"
  let output = getOutputDir()
  MetadataFormat.Generate
    ([ library ], output, layoutRoots, info, libDirs = [ binDir; root @@ "../../lib" ],
     markDownComments = false)
  let fileNames = Directory.GetFiles(output)

  let files =
      dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]
  files.ContainsKey "fslib-test_omit.html" |> should equal false

// -------------------Indirect links----------------------------------
[<Test>]
let ``Metadata generates cross-type links for Indirect Links``() =
  let library = root @@ "files/FsLib/bin/Debug" @@ "FsLib2.dll"
  let output = getOutputDir()
  MetadataFormat.Generate([library], output, layoutRoots, info, libDirs = [root @@ "../../lib"], markDownComments = true)
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // Check that a link to MyType exists when using Full Name of the type
  files.["fslib-nested.html"] |> should contain "This function returns a <a href=\"fslib-nested-mytype.html\" title=\"MyType\">FsLib.Nested.MyType</a>"

  // Check that a link to OtherType exists when using Logical Name of the type only
  files.["fslib-nested.html"] |> should contain "This function returns a <a href=\"fslib-nested-othertype.html\" title=\"OtherType\">OtherType</a>"

  // Check that a link to a module is created when using Logical Name only
  files.["fslib-duplicatedtypename.html"] |> should contain "This type name will be duplicated in <a href=\"fslib-nested.html\" title=\"Nested\">Nested</a>"

  // Check that a link to a type with a duplicated name is created when using full name
  files.["fslib-nested-duplicatedtypename.html"] |> should contain "This type has the same name as <a href=\"fslib-duplicatedtypename.html\" title=\"DuplicatedTypeName\">FsLib.DuplicatedTypeName</a>"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> should contain "This function returns a [DuplicatedTypeName] multiplied by 4."

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> should contain "This function returns a [InexistentTypeName] multiplied by 5."

  // -------------------Inline code----------------------------------
let ``Metadata generates cross-type links for Inline Code``() =
  let library = root @@ "files/FsLib/bin/Debug" @@ "FsLib2.dll"
  let output = getOutputDir()
  MetadataFormat.Generate([library], output, layoutRoots, info, libDirs = [root @@ "../../lib"], markDownComments = true)
  let fileNames = Directory.GetFiles(output)
  let files = dict [ for f in fileNames -> Path.GetFileName(f), File.ReadAllText(f) ]

  // Check that a link to MyType exists when using Full Name of the type in a inline code
  files.["fslib-nested.html"] |> should contain "You will notice that <a href=\"fslib-nested-mytype.html\" title=\"MyType\"><code>FsLib.Nested.MyType</code></a> is just an <code>int</code>"

    // Check that a link to MyType exists when using Full Name of the type in a inline code
  files.["fslib-nested.html"] |> should contain "You will notice that <a href=\"fslib-nested-othertype.html\" title=\"OtherType\"><code>OtherType</code></a> is just an <code>int</code>"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> should contain "<code>DuplicatedTypeName</code> is duplicated so it should no add a cross-type link"

  // Check that a link to a type with a duplicated name is not created when using Logical name only
  files.["fslib-nested.html"] |> should contain "<code>InexistentTypeName</code> does not exists so it should no add a cross-type link"

  // Check that a link to a module is created when using Logical Name only
  files.["fslib-duplicatedtypename.html"] |> should contain "This type name will be duplicated in <a href=\"fslib-nested.html\" title=\"Nested\"><code>Nested</code></a>"

  // Check that a link to a type with a duplicated name is created when using full name
  files.["fslib-nested-duplicatedtypename.html"] |> should contain "This type has the same name as <a href=\"fslib-duplicatedtypename.html\" title=\"DuplicatedTypeName\"><code>FsLib.DuplicatedTypeName</code></a>"