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
open System.Collections.Concurrent
open RazorEngine
open RazorEngine.Text
open RazorEngine.Templating
open RazorEngine.Configuration
open RazorEngine.Compilation.ReferenceResolver

type GetMemberBinderImpl (name) =
    inherit GetMemberBinder(name, false)
    let notImpl () = raise <| new NotImplementedException()
    override x.FallbackGetMember(v, sug) = notImpl()
    
type RazorRender(layoutRoots, namespaces, templateName:string, ?modeltype:System.Type, ?references : string list) =
  let templateName =
    if templateName.EndsWith(".cshtml") then
        templateName.Substring(0, templateName.Length - 7)
    else templateName
  let templateCache = new ConcurrentDictionary<string[] * string, ITemplateSource>()
  // Create resolver & set it to the global static field 
  let templateManager = 
    { new ITemplateManager with
        member x.GetKey (name, resolveType, context) = new NameOnlyTemplateKey(name, resolveType, context) :> ITemplateKey
        member x.AddDynamic (key, source) = failwith "dynamic templates are not supported!"
        member x.Resolve templateKey = 
            let name = templateKey.Name
            let key = Array.ofSeq layoutRoots, name
            templateCache.GetOrAdd (key, fun (layoutRoots, name) -> 
                let file = RazorRender.Resolve(layoutRoots, name + ".cshtml")
                new LoadedTemplateSource(File.ReadAllText(file), file) :> ITemplateSource) }

  // Configure templating engine
  let config = new TemplateServiceConfiguration()
  do config.EncodedStringFactory <- new RawStringFactory()
  do config.TemplateManager <- templateManager
  do
    match references with
    | Some r -> 
      config.ReferenceResolver <- 
        { new IReferenceResolver with 
            member x.GetReferences (_, _) =
                r |> List.toSeq |> Seq.map (CompilerReference.From) }
    | None -> ()

  do namespaces |> Seq.iter (config.Namespaces.Add >> ignore)
  do config.BaseTemplateType <- typedefof<DocPageTemplateBase<_>>
  do config.Debug <- true 
  let razorEngine = RazorEngineService.Create(config)

  let handleCompile source f =
    try
      f ()
    with 
    | :? TemplateCompilationException as ex -> 
        let csharp = Path.GetTempFileName() + ".cs"
        File.WriteAllText(csharp, ex.SourceCode)
        Log.run (fun () ->
          use _c = Log.colored ConsoleColor.Red
          printfn "\nProcessing the file '%s' failed\nSource written to: '%s'\nCompilation errors:" source csharp
          for error in ex.CompilerErrors do
            let errorType = if error.IsWarning then "warning" else "error"
            printfn " - %s: (%d, %d) %s" errorType error.Line error.Column error.ErrorText
          printfn ""
        )
        Log.close() // wait for the message to be printed completly
        failwith "Generating HTML failed."

  let withProperties properties (oldViewbag:DynamicViewBag) = 
    let viewBag = new DynamicViewBag() 
    // TODO: use new DynamicViewBag(oldViewbag) and remove GetMemberBinderImpl
    for old in oldViewbag.GetDynamicMemberNames() do
        match oldViewbag.TryGetMember(new GetMemberBinderImpl(old)) with
        | true, v -> viewBag.AddValue(old, v)
        | _ -> ()
    for k, v in defaultArg properties [] do
      viewBag.AddValue(k, v)
    viewBag
  
  /// Find file in one of the specified layout roots
  static member TryResolve(layoutRoots, name) =
    layoutRoots |> Seq.tryPick (fun layoutRoot ->
      let partFile = Path.Combine(layoutRoot, name)
      if File.Exists(partFile) then Some partFile else None)
  static member Resolve(layoutRoots, name) =
    match RazorRender.TryResolve(layoutRoots, name) with
    | Some f -> f
    | None -> 
        failwithf "Could not find template file: %s\nSearching in: %A" name layoutRoots
      
  member internal x.HandleCompile source f = handleCompile source f
  member internal x.TemplateName = templateName
  member internal x.WithProperties properties = withProperties properties x.ViewBag

  /// Dynamic object with more properties (?)
  member val ViewBag = new DynamicViewBag() with get, set

  member x.ProcessFile(?properties) = x.ProcessFileModel(null, null, ?properties = properties)
  member x.ProcessFileDynamic(model:obj,?properties) = x.ProcessFileModel(null, model, ?properties = properties)
      
  member x.ProcessFileModel(modelType : System.Type,model:obj,?properties) =
    handleCompile templateName (fun _ ->
      razorEngine.RunCompile(templateName, modelType, model, x.WithProperties(properties)))

and RazorRender<'model>(layoutRoots, namespaces, templateName, ?references) =
    inherit RazorRender(layoutRoots, namespaces, templateName, modeltype = typeof<'model>, ?references = references)

    member x.ProcessFile(model:'model, ?properties) = 
      x.ProcessFileModel(typeof<'model>, model, ?properties = properties)

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

  member x.RenderPart(name : string, model:obj) =
    x.Include(name, model)
