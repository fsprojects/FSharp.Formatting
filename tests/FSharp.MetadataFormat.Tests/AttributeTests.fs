[<NUnit.Framework.TestFixture>]
module FSharp.MetadataFormat.AttributeTests

open FsUnit
open System.IO
open NUnit.Framework
open FSharp.MetadataFormat
open FsUnitTyped
open FSharp.Compiler.SourceCodeServices
open NUnit.Framework.Internal
open System

// --------------------------------------------------------------------------------------
// Run the metadata formatter on sample project
// --------------------------------------------------------------------------------------

let (</>) a b = Path.Combine(a, b)
let fullpath = Path.GetFullPath
let fullpaths = List.map fullpath

let root = __SOURCE_DIRECTORY__ |> fullpath


// NOTE - For these tests to run properly they require the output of all the metadata
// test project to be directed to the directory below
let testBin = __SOURCE_DIRECTORY__ </> "files/bin/netstandard2.0" |> fullpath

#if INTERACTIVE 
;;
printfn "\n-- Root - %s" root;;
printfn "\n-- TestBin - %s" testBin;;
#endif 

do FSharp.Formatting.TestHelpers.enableLogging()
let library = testBin </> "AttributesTestLib.dll"

let findModule name (moduleInfos: ModuleInfo list)=
    moduleInfos
    |> List.map (fun m-> m.Module)
    |> List.find (fun m-> m.Name = name)

let findType name (typeInfos: TypeInfo list)=
    typeInfos
    |> List.map (fun t -> t.Type)
    |> List.find (fun t-> t.Name = name)

let info =
  [ "project-name", "FSharp.ProjectScaffold"
    "project-author", "Your Name"
    "project-summary", "A short summary of your project"
    "project-github", "http://github.com/pblasucci/fsharp-project-scaffold"
    "project-nuget", "http://nuget.com/packages/FSharp.ProjectScaffold"
    "root", "http://fsprojects.github.io/FSharp.FSharp.ProjectScaffold" ]

[<Test>]
let ``MetadataFormat extracts Attribute on Module``() =
  let modules = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let modul = modules |> findModule "SingleAttributeModule"
  let attribute = modul.Attributes.Head
  attribute.Name |> shouldEqual "ObsoleteAttribute"
  attribute.FullName |> shouldEqual "System.ObsoleteAttribute"
  attribute.ConstructorArguments |> shouldBeEmpty
  attribute.NamedConstructorArguments |> shouldBeEmpty

[<Test>]
let ``MetadataFormat extracts multiple Attributes on Module``() =
  let modules = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let modul = modules |> findModule "MultipleAttributesModule"
  let attributes = modul.Attributes
  attributes.Length |> shouldEqual 3

[<Test>]
let ``MetadataFormat extracts Attribute with argument``() =
  let modules = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let modul = modules |> findModule "SingleAttributeWithArgumentModule"
  let attribute = modul.Attributes.Head
  attribute.Name |> shouldEqual "ObsoleteAttribute"
  attribute.FullName |> shouldEqual "System.ObsoleteAttribute"
  attribute.ConstructorArguments |> shouldContain (box "obsolete")
  attribute.NamedConstructorArguments |> shouldBeEmpty


[<Test>]
let ``MetadataFormat extracts Attribute with named arguments``() =
  let modules = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let modul = modules |> findModule "SingleAttributeWithNamedArgumentsModule"
  let attribute = modul.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"
  attribute.ConstructorArguments |> shouldBeEmpty
  
  attribute.NamedConstructorArguments |>  shouldContain ("Int", box 1)
  attribute.NamedConstructorArguments |>  shouldContain ("String", box "test")
  let _,arrayArgument = attribute.NamedConstructorArguments |> Seq.find (fun (n,_)-> n="Array")
  arrayArgument |>  shouldEqual (box [| box "1"; box "2"|])
  ()  

[<Test>]
let ``MetadataFormat extracts Attribute on interface``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let interface' = typeInfos |> findType "AttributeInterface"
  let attribute = interface'.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"

[<Test>]
let ``MetadataFormat extracts Attribute on class``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let class' = typeInfos |> findType "AttributeClass"
  let attribute = class'.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"

[<Test>]
let ``MetadataFormat extracts Attribute on value in module``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = typeInfos |> findModule "ContentTestModule"
  let testValue = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="testValue")
  let attribute = testValue.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"

[<Test>]
let ``MetadataFormat extracts Attribute on function in module``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = typeInfos |> findModule "ContentTestModule"
  let testFunction = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="testFunction")
  let attribute = testFunction.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"

[<Test>]
let ``MetadataFormat extracts Attribute on instance member in class``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let class' = typeInfos |> findType "AttributeClass"
  let testMember = class'.InstanceMembers |> Seq.find (fun v -> v.Name ="TestMember")
  let attribute = testMember.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"

[<Test>]
let ``MetadataFormat extracts Attribute on class constructor``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let class' = typeInfos |> findType "AttributeClass"
  let ctor = class'.Constructors |> Seq.find (fun v -> v.Details.Signature ="i:int -> AttributeClass")
  let attribute = ctor.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"
  attribute.NamedConstructorArguments |>  shouldContain ("String", box "ctor")

[<Test>]
let ``MetadataFormat extracts Attribute on static member in class``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let class' = typeInfos |> findType "AttributeClass"
  let staticMember = class'.StaticMembers |> Seq.find (fun v -> v.Name ="TestStaticMember")
  let attribute = staticMember.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"

[<Test>]
let ``MetadataFormat extracts Attribute on union case``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let union = typeInfos |> findType "AttributeUnion"
  let case = union.UnionCases |> Seq.find (fun v -> v.Name ="TestCase")
  let attribute = case.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"
  attribute.NamedConstructorArguments |>  shouldContain ("String", box "union")

[<Test>]
let ``MetadataFormat extracts Attribute on record field``() =
  let typeInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).TypesInfos
  let union = typeInfos |> findType "AttributeRecord"
  let case = union.RecordFields |> Seq.find (fun v -> v.Name ="TestField")
  let attribute = case.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"
  attribute.NamedConstructorArguments |>  shouldContain ("String", box "record")

[<Test>]
let ``MetadataFormat formats attribute without arguments``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="noArguments")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"
  attribute.Format() |> shouldEqual "[<Test>]"

[<Test>]
let ``MetadataFormat formats attribute with single int argument``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleIntArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "IntTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.IntTestAttribute"
  attribute.Format() |> shouldEqual "[<IntTest(1)>]"

[<Test>]
let ``MetadataFormat formats attribute with single string argument``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleStringArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "StringTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.StringTestAttribute"
  attribute.Format() |> shouldEqual """[<StringTest("test")>]"""

[<Test>]
let ``MetadataFormat formats attribute with single bool argument``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleBoolArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "BoolTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.BoolTestAttribute"
  attribute.Format() |> shouldEqual "[<BoolTest(true)>]"


[<Test>]
let ``MetadataFormat formats attribute with single array argument``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleArrayArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "ArrayTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.ArrayTestAttribute"
  attribute.Format() |> shouldEqual """[<ArrayTest([|"test1"; "test2"|])>]"""

[<Test>]
let ``MetadataFormat formats attribute with multiple arguments``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="multipleArguments")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "MultipleTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.MultipleTestAttribute"
  attribute.Format() |> shouldEqual """[<MultipleTest("test", 1, [|1; 2|])>]"""

[<Test>]
let ``MetadataFormat formats attribute with multiple named arguments``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="multipleNamedArguments")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "TestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.TestAttribute"
  attribute.Format() |> shouldEqual """[<Test(Int = 1, String = "test", Array = [|"1"; "2"|])>]"""

[<Test>]
let ``MetadataFormat formats attribute with name and suffix``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleBoolArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "BoolTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.BoolTestAttribute"
  attribute.FormatLongForm() |> shouldEqual "[<BoolTestAttribute(true)>]"
[<Test>]
let ``MetadataFormat formats attribute with fullName``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleBoolArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "BoolTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.BoolTestAttribute"
  attribute.FormatFullName() |> shouldEqual "[<AttributeTestNamespace.BoolTest(true)>]"

[<Test>]
let ``MetadataFormat formats attribute with fullName and suffix``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "FormatTestModule"
  let value = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="singleBoolArgument")
  let attribute = value.Attributes.Head
  attribute.Name |> shouldEqual "BoolTestAttribute"
  attribute.FullName |> shouldEqual "AttributeTestNamespace.BoolTestAttribute"
  attribute.FormatFullNameLongForm() |> shouldEqual "[<AttributeTestNamespace.BoolTestAttribute(true)>]"

[<Test>]
let ``MetadataFormat IsObsolete returns true on obsolete attribute``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "ObsoleteTestModule"
  let noMessage = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="noMessage")
  noMessage.IsObsolete |> shouldEqual true
  noMessage.ObsoleteMessage |> shouldEqual ""

[<Test>]
let ``MetadataFormat IsObsolete returns true on obsolete attribute and finds obsolete message``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "ObsoleteTestModule"
  let withMessage = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="withMessage")
  withMessage.IsObsolete |> shouldEqual true
  withMessage.ObsoleteMessage |> shouldEqual "obsolete"

[<Test>]
let ``MetadataFormat IsObsolete returns false on not obsolete attribute and finds no obsolete message``() =
  let moduleInfos = MetadataFormat.Generate(library, info, libDirs = [testBin]).ModuleInfos
  let module' = moduleInfos |> findModule "ObsoleteTestModule"
  let notObsolete = module'.ValuesAndFuncs |> Seq.find (fun v -> v.Name ="notObsolete")
  notObsolete.IsObsolete |> shouldEqual false
  notObsolete.ObsoleteMessage |> shouldEqual ""
