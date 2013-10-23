namespace FSharp.Literate

open System.IO
open System.Reflection
open FSharp.CodeFormat
open FSharp.Literate.SourceProcessors

// --------------------------------------------------------------------------------------
// Public API
// --------------------------------------------------------------------------------------

/// This type provides three simple methods for calling the literate programming tool.
/// The `ProcessMarkdown` and `ProcessScriptFile` methods process a single Markdown document
/// and F# script, respectively. The `ProcessDirectory` method handles an entire directory tree
/// (ooking for `*.fsx` and `*.md` files).
type Literate = 
  /// Provides default values for all optional parameters
  static member private DefaultArguments
      ( input, templateFile, output,format, fsharpCompiler, prefix, compilerOptions, 
        lineNumbers, references, replacements, includeSource, errorHandler, layoutRoots) = 
    let defaultArg v f = match v with Some v -> v | _ -> f()

    let outputKind = defaultArg format (fun _ -> OutputKind.Html)

    let output = defaultArg output (fun () ->
      let dir = Path.GetDirectoryName(input)
      let file = Path.GetFileNameWithoutExtension(input)
      Path.Combine(dir, sprintf "%s.%O" file outputKind))
    let fsharpCompiler = defaultArg fsharpCompiler (fun () -> 
      Assembly.Load("FSharp.Compiler"))
    
    // Build & return processing context
    let ctx = 
      { FormatAgent = CodeFormat.CreateAgent(fsharpCompiler) 
        TemplateFile = templateFile 
        Prefix = defaultArg prefix (fun () -> "fs")
        Options = defaultArg compilerOptions (fun () -> "")
        GenerateLineNumbers = defaultArg lineNumbers (fun () -> true)
        GenerateReferences = defaultArg references (fun () -> false)
        Replacements = defaultArg replacements (fun () -> []) 
        IncludeSource = defaultArg includeSource (fun () -> false) 
        OutputKind = outputKind
        ErrorHandler = errorHandler
        LayoutRoots = defaultArg layoutRoots (fun () -> []) }
    output, ctx

  /// Process Markdown document
  static member ProcessMarkdown
    ( input, ?templateFile, ?output, ?format, ?fsharpCompiler, ?prefix, ?compilerOptions, 
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?errorHandler, ?layoutRoots ) = 
    let output, ctx = 
      Literate.DefaultArguments
        ( input, templateFile, output, format, fsharpCompiler, prefix, compilerOptions, 
          lineNumbers, references, replacements, includeSource, errorHandler, layoutRoots)
    processMarkdown ctx input output 

  /// Process F# Script file
  static member ProcessScriptFile
    ( input, ?templateFile, ?output, ?format, ?fsharpCompiler, ?prefix, ?compilerOptions, 
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?errorHandler, ?layoutRoots ) = 
    let output, ctx = 
      Literate.DefaultArguments
        ( input, templateFile, output, format, fsharpCompiler, prefix, compilerOptions, 
          lineNumbers, references, replacements, includeSource, errorHandler, layoutRoots )
    processScriptFile ctx input output 

  /// Process directory containing a mix of Markdown documents and F# Script files
  static member ProcessDirectory
    ( inputDirectory, ?templateFile, ?outputDirectory, ?format, ?fsharpCompiler, ?prefix, ?compilerOptions, 
      ?lineNumbers, ?references, ?replacements, ?includeSource, ?errorHandler, ?layoutRoots ) = 
    let _, ctx = 
      Literate.DefaultArguments
        ( "", templateFile, Some "", format, fsharpCompiler, prefix, compilerOptions, 
          lineNumbers, references, replacements, includeSource, errorHandler, 
          layoutRoots )
 
    /// Recursively process all files in the directory tree
    let rec processDirectory indir outdir = 
      // Create output directory if it does not exist
      if Directory.Exists(outdir) |> not then
        try Directory.CreateDirectory(outdir) |> ignore 
        with _ -> failwithf "Cannot create directory '%s'" outdir

      let fsx = [ for f in Directory.GetFiles(indir, "*.fsx") -> processScriptFile, f ]
      let mds = [ for f in Directory.GetFiles(indir, "*.md") -> processMarkdown, f ]
      for func, file in fsx @ mds do
        let name = Path.GetFileNameWithoutExtension(file)
        let output = Path.Combine(outdir, sprintf "%s.%O" name ctx.OutputKind)

        // Update only when needed
        let changeTime = File.GetLastWriteTime(file)
        let generateTime = File.GetLastWriteTime(output)
        if changeTime > generateTime then
          printfn "Generating '%s.%O'" name ctx.OutputKind
          func ctx file output

    let outputDirectory = defaultArg outputDirectory inputDirectory
    processDirectory inputDirectory outputDirectory 