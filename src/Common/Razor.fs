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

/// [omit]
module PathHelper =
  let private isUnix =
    match Environment.OSVersion.Platform with
    | PlatformID.Unix -> true
    | PlatformID.MacOSX -> true
    | _ -> false
  let normalizePath p =
    let fullPath = Path.GetFullPath(p).TrimEnd(Path.AltDirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
    if isUnix then fullPath else fullPath.ToLowerInvariant()
      
/// [omit]
type PathTemplateKey(name, path, t, context) =
  let path = PathHelper.normalizePath path
  member private x.Path = path
  interface ITemplateKey with
    member x.GetUniqueKeyString () = path
    member x.Name = name
    member x.TemplateType = t
    member x.Context = context
  static member Create (name, path, ?t, ?context) = 
    let t = defaultArg t ResolveType.Global
    let context = defaultArg context null
    PathTemplateKey(name, path, t, context)
  override x.Equals (other) =
    match other with
    | :? PathTemplateKey as p -> x.Path.Equals(p.Path)
    | _ -> false
  override x.GetHashCode () = x.Path.GetHashCode()

/// [omit]
type GetMemberBinderImpl (name) =
    inherit GetMemberBinder(name, false)
    let notImpl () = raise <| new NotImplementedException()
    override x.FallbackGetMember(v, sug) = notImpl()
    
/// [omit]
type StringDictionary(dict:IDictionary<string, string>) =
  member x.Dictionary = dict
  /// Report more useful errors when key not found (.NET dictionary does not do this...)
  member x.Item
    with get(k) =
      if dict.ContainsKey(k) then dict.[k]
      else raise (new KeyNotFoundException(sprintf "Key '%s' was not found." k))

/// [omit]
type [<AbstractClass>] DocPageTemplateBase<'T>() =
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

/// A simple RazorEngine caching strategy, this implementation assumes that the current directory never changes.
///
/// [omit]
module RazorEngineCache =
  let cachingProvider =
    let arg =
      if System.AppDomain.CurrentDomain.IsDefaultAppDomain() then
         new System.Action<string>(fun s -> ())
      else null
    new DefaultCachingProvider(arg) :> ICachingProvider

  /// Find file in one of the specified layout roots
  let private tryResolve(layoutRoots, name) =
    layoutRoots |> Seq.tryPick (fun layoutRoot ->
      let partFile = Path.Combine(layoutRoot, name)
      if File.Exists(partFile) then Some partFile else None)
  let private resolve(layoutRoots, name) =
    match tryResolve(layoutRoots, name) with
    | Some f -> f
    | None ->
        failwithf "Could not find template file: %s\nSearching in: %A" name layoutRoots

  /// Caching mechanism for IRazorEngineService instances.
  let private razorCache = new ConcurrentDictionary<string list, IRazorEngineService * string list option * string list>()
  let private createNew layoutRoots (references:string list option) namespaces =
    let resolveCache = new ConcurrentDictionary<string, string>()
    // create manager
    let templateManager =
      { new ITemplateManager with
          member x.GetKey (name, resolveType, context) =
            let file = resolveCache.GetOrAdd(name, (fun _ -> resolve(layoutRoots, name + ".cshtml")))
            new PathTemplateKey(name, file, resolveType, context) :> ITemplateKey
          member x.AddDynamic (key, source) = failwith "dynamic templates are not supported!"
          member x.Resolve templateKey =
            let file = templateKey.GetUniqueKeyString()
            new LoadedTemplateSource(File.ReadAllText(file), file) :> ITemplateSource }
    // Configure templating engine
    let config = new TemplateServiceConfiguration()
    config.EncodedStringFactory <- new RawStringFactory()
    config.TemplateManager <- templateManager
    // NOTE: this is good in the context of running F# Formatting via F# scripts,
    // however when using F# Formatting as library this hides a memory leak.
    // We cannot really know which case this is, so applications using this library
    // should make sure they are not in the default AppDomain.
    // And F# scripts on the other hand will not explode the temp directory.
    config.DisableTempFileLocking  <- System.AppDomain.CurrentDomain.IsDefaultAppDomain()
    config.Debug <- not config.DisableTempFileLocking
    config.CachingProvider <- cachingProvider
    
    match references with
    | Some r -> 
      config.ReferenceResolver <- 
        { new IReferenceResolver with 
            member x.GetReferences (_, _) =
                r |> List.toSeq |> Seq.map (CompilerReference.From) }
    | None -> ()

    namespaces |> Seq.iter (config.Namespaces.Add >> ignore)
    config.BaseTemplateType <- typedefof<DocPageTemplateBase<_>>

    RazorEngineService.Create(config)

  let Get layoutRoots namespaces references =
    let engine, currentReferences, currentNamespaces =
      razorCache.GetOrAdd(layoutRoots, fun roots -> createNew layoutRoots references namespaces, references, namespaces)
    if (namespaces <> currentNamespaces) then failwith "cannot use different namespaces for the same layoutRoot"
    if (references <> currentReferences) then failwith "cannot use different references for the same layoutRoot"
    engine

/// [omit]
type RazorRender(layoutRoots, namespaces, template:string, ?references : string list) =
  // template is either a full path or a template name
  let templatePath, templateName =
    if Path.IsPathRooted (template) then
      Some template, Path.GetFileNameWithoutExtension template
    else
      None,
      if template.EndsWith(".cshtml") then
          template.Substring(0, template.Length - 7)
      else template

  let razorEngine = RazorEngineCache.Get layoutRoots namespaces references
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

  member internal x.HandleCompile source f = handleCompile source f
  member internal x.WithProperties properties = withProperties properties x.ViewBag

  /// Dynamic object with more properties (?)
  member val ViewBag = new DynamicViewBag() with get, set

  member x.ProcessFile(?properties) = x.ProcessFileModel(null, null, ?properties = properties)
  member x.ProcessFileDynamic(model:obj,?properties) = x.ProcessFileModel(null, model, ?properties = properties)

  member x.ProcessFileModel(modelType : System.Type,model:obj,?properties) =
    handleCompile templateName (fun _ ->
      let templateKey =
        match templatePath with
        | Some p -> new PathTemplateKey(templateName, p, ResolveType.Global, null) :> ITemplateKey
        | None ->  razorEngine.GetKey(templateName)
      razorEngine.RunCompile(templateKey, modelType, model, x.WithProperties(properties)))

/// [omit]
and RazorRender<'model>(layoutRoots, namespaces, template, ?references) =
    inherit RazorRender(layoutRoots, namespaces, template, ?references = references)

    member x.ProcessFile(model:'model, ?properties) = 
      x.ProcessFileModel(typeof<'model>, model, ?properties = properties)

