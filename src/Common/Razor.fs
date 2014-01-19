#if METADATAFORMAT
namespace FSharp.MetadataFormat
#else
namespace FSharp.Literate
#endif

// --------------------------------------------------------------------------------------
// Helpers for parallel processing
// --------------------------------------------------------------------------------------

open System
open System.Threading.Tasks

module internal Parallel = 
  /// Parallel for loop with local state
  let pfor (input:seq<'TSource>) (localInit:unit -> 'TLocal) 
           (body:'TSource -> ParallelLoopState -> 'TLocal -> 'TLocal) =
    Parallel.ForEach
      ( input, Func<'TLocal>(localInit), 
        Func<'TSource, ParallelLoopState, 'TLocal, 'TLocal>(body), 
        Action<'TLocal>(fun _ -> ()) ) |> ignore

// --------------------------------------------------------------------------------------
// Tools for calling razor engine
// --------------------------------------------------------------------------------------

open System
open System.IO
open System.Dynamic
open System.Collections.Generic
open RazorEngine
open RazorEngine.Text
open RazorEngine.Templating
open RazorEngine.Configuration

type RazorRender(layoutRoots, namespaces) =
  // Create resolver & set it to the global static filed 
  let templateResolver = 
    { new ITemplateResolver with
        member x.Resolve name = File.ReadAllText(RazorRender.Resolve(layoutRoots, name + ".cshtml")) }
  do RazorRender.Resolver <- templateResolver

  // Configure templating engine
  let config = new TemplateServiceConfiguration()
  do config.EncodedStringFactory <- new RawStringFactory()
  do config.Resolver <- templateResolver
  do namespaces |> Seq.iter (config.Namespaces.Add >> ignore)
  do config.BaseTemplateType <- typedefof<DocPageTemplateBase<_>>
  do config.Debug <- true        
  let templateservice = new TemplateService(config)
  do Razor.SetTemplateService(templateservice)

  /// Global resolver (for use in 'DocPageTempalateBase')
  static member val Resolver = null with get, set
  /// Find file in one of the specified layout roots
  static member Resolve(layoutRoots, name) =
    let partFileOpt =
      layoutRoots |> Seq.tryPick (fun layoutRoot ->
        let partFile = Path.Combine(layoutRoot, name)
        if File.Exists(partFile) then Some partFile else None)
    match partFileOpt with
    | None -> failwithf "Could not find template file: %s\nSearching in: %A" name layoutRoots
    | Some partFile -> partFile 

  /// Model - whatever the user specifies for the page
  member val Model : obj = obj() with get, set
  /// Dynamic object with more properties (?)
  member val ViewBag = new DynamicViewBag() with get,set

  /// Process source file and return result as a string
  member x.ProcessFile(source, ?properties) = 
    try
      x.ViewBag <- new DynamicViewBag()
      for k, v in defaultArg properties [] do
        x.ViewBag.AddValue(k, v)
      let html = Razor.Parse(File.ReadAllText(source), x.Model, x.ViewBag, source)
      html
    with 
    | :? TemplateCompilationException as ex -> 
        let csharp = Path.GetTempFileName() + ".cs"
        File.WriteAllText(csharp, ex.SourceCode)
        Log.run (fun () ->
          use _c = Log.colored ConsoleColor.Red
          printfn "\nProcessing the file '%s' failed\nSource written to: '%s'\nCompilation errors:" source csharp
          for error in ex.Errors do
            printfn " - (%d, %d) %s" error.Line error.Column error.ErrorText
          printfn ""
        )
        failwith "Generating HTML failed."

and StringDictionary(dict:IDictionary<string, string>) =
  member x.Dictionary = dict
  /// Report more useful errors when key not found (.NET dictionary does not do this...)
  member x.Item 
    with get(k) = 
      if dict.ContainsKey(k) then dict.[k] 
      else raise (new KeyNotFoundException(sprintf "Key '%s' was not found." k))

and [<AbstractClass>] DocPageTemplateBase<'T>() =
  inherit RazorEngine.Templating.TemplateBase<'T>()

  member private x.tryGetViewBagValue<'C> key =  
    let vb = x.ViewBag :?> DynamicViewBag
    let memBinder =
        { new GetMemberBinder(key, false) with
            member x.FallbackGetMember(y,z) = failwith "not implemented" }
    let mutable output = ref (new Object ())
    let result = vb.TryGetMember(memBinder, output)
    if result && !output <> null then Some(!output :?> 'C) else None
        
  member private x.trySetViewBagValue<'C> key (value:'C) = 
    let vb = x.ViewBag :?> DynamicViewBag
    let memBinder =
        { new DeleteMemberBinder(key, false) with
            member x.FallbackDeleteMember(y,z) = failwith "not implemented" }
    let names = 
        vb.GetDynamicMemberNames()
        |> Seq.tryFind(fun x -> x = key)
    match names with
    | Some(v) ->             
        vb.TryDeleteMember(memBinder) |> ignore
        vb.AddValue(key, value)
    | _ -> vb.AddValue(key, value)

  member x.Title
    with get() = defaultArg (x.tryGetViewBagValue<string> "Title") ""
    and set value = x.trySetViewBagValue<string> "Title" value

  member x.Description
    with get() = defaultArg (x.tryGetViewBagValue<string> "Description") ""
    and set value = x.trySetViewBagValue<string> "Description" value

  member x.Properties
    with get() = StringDictionary(defaultArg (x.tryGetViewBagValue<IDictionary<string, string>> "Properties") (dict []))
    and set (value:StringDictionary) = x.trySetViewBagValue<IDictionary<string, string>> "Properties" value.Dictionary

  member x.Root = x.Properties.["root"]

  member x.RenderPart(name, model:obj) =  
    Razor.Parse(RazorRender.Resolver.Resolve(name), model)
