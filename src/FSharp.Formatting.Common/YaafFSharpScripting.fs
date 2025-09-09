namespace FSharp.Formatting.Internal

open System
open System.IO
open System.Text
open System.Reflection
open System.Reflection.Emit
open System.Diagnostics
open System.Runtime.CompilerServices
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Symbols

#nowarn "25" // Binding incomplete: let [ t ] = list

[<assembly: InternalsVisibleTo("FSharp.Formatting.CodeFormat")>]
[<assembly: InternalsVisibleTo("FSharp.Formatting.ApiDocs")>]
[<assembly: InternalsVisibleTo("fsdocs")>]
[<assembly: InternalsVisibleTo("FSharp.Formatting.CSharpFormat")>]
[<assembly: InternalsVisibleTo("FSharp.Formatting.Literate")>]
[<assembly: InternalsVisibleTo("FSharp.Formatting.TestHelpers")>]
[<assembly: InternalsVisibleTo("FSharp.Formatting.Markdown")>]
do ()

module internal Env =
    let inline isNull o = obj.ReferenceEquals(null, o)
    let (++) a b = System.IO.Path.Combine(a, b)

    let (=?) s1 s2 =
        System.String.Equals(s1, s2, System.StringComparison.OrdinalIgnoreCase)

    let (<>?) s1 s2 = not (s1 =? s2)

    let isNetCoreApp = true

open Env

module internal Log =
    let source = new TraceSource("FSharp.Formatting.Internal")

    let traceEventf t f =
        Printf.kprintf (fun s -> source.TraceEvent(t, 0, s)) f

    let infof f =
        traceEventf TraceEventType.Information f

    let errorf f = traceEventf TraceEventType.Error f
    let warnf f = traceEventf TraceEventType.Warning f
    let critf f = traceEventf TraceEventType.Critical f
    let verbf f = traceEventf TraceEventType.Verbose f

    let formatArgs (args: _ seq) =
        System.String.Join("\n  ", args) |> sprintf "\n  %s"

    let formatPaths paths =
        System.String.Join("\n  ", paths |> Seq.map (sprintf "\"%s\""))
        |> sprintf "\n[ %s ]"

[<AutoOpen>]
module internal CompilerServiceExtensions =

    module FSharpAssemblyHelper =
        let checker = FSharpChecker.Create()
        let defaultFrameworkVersion = "4.6.1"

        let getLib dir nm = dir ++ nm + ".dll"

        let getNetCoreAppFrameworkDependencies =
            lazy
                (let options, _ =
                    checker.GetProjectOptionsFromScript(
                        "foo.fsx",
                        SourceText.ofString "module Foo",
                        assumeDotNetFramework = false
                    )
                    |> Async.RunSynchronously

                 printfn "isNetCoreApp = %b" isNetCoreApp
                 //for r in options.OtherOptions do
                 //    printfn "option: %s" r

                 options.OtherOptions
                 |> Array.filter (fun path -> path.StartsWith("-r:", StringComparison.Ordinal))
                 //|> Seq.choose (fun path -> if path.StartsWith "-r:" then path.Substring 3 |> Some else None)
                 //|> Seq.map (fun path -> path.Replace("\\\\", "\\"))
                 |> Array.toList)

        let fscoreResolveDirs libDirs =
            [ yield System.AppContext.BaseDirectory

              yield! libDirs
              yield System.IO.Directory.GetCurrentDirectory() ]

        let tryCheckFsCore fscorePath =
            if File.Exists fscorePath then Some fscorePath else None

        let findFSCore dllFiles libDirs =
            // lets find ourself some FSharp.Core.dll
            let tried =
                dllFiles
                @ (fscoreResolveDirs libDirs
                   |> List.map (fun (l: string) -> getLib l "FSharp.Core"))

            match tried |> List.tryPick tryCheckFsCore with
            | Some s -> s
            | None ->
                let paths = Log.formatPaths tried
                printfn "Could not find a FSharp.Core.dll in %s" paths
                failwithf "Could not find a FSharp.Core.dll in %s" paths

        let isAssembly asm l =
            l |> List.exists (fun (a: string) -> Path.GetFileNameWithoutExtension a =? asm)

        let getCheckerArguments
            frameworkVersion
            defaultReferences
            hasFsCoreLib
            (fsCoreLib: _ option)
            dllFiles
            libDirs
            otherFlags
            =
            ignore frameworkVersion
            ignore defaultReferences
            let base1 = Path.GetTempFileName()
            let dllName = Path.ChangeExtension(base1, ".dll")
            let xmlName = Path.ChangeExtension(base1, ".xml")
            let fileName1 = Path.ChangeExtension(base1, ".fs")
            let projFileName = Path.ChangeExtension(base1, ".fsproj")
            File.WriteAllText(fileName1, """module M""")

            let args =
                [| //yield "--debug:full"
                   //yield "--define:DEBUG"
                   //yield "--optimize-"
                   yield "--langversion:preview"
                   yield "--nooptimizationdata"
                   yield "--noframework"

                   if isNetCoreApp then
                       yield "--targetprofile:netcore"

                   for r in getNetCoreAppFrameworkDependencies.Value do
                       let suppressFSharpCore =
                           ((hasFsCoreLib || fsCoreLib.IsSome)
                            && Path.GetFileNameWithoutExtension r = "FSharp.Core")

                       if not suppressFSharpCore then
                           yield r

                   yield "--out:" + dllName
                   yield "--doc:" + xmlName
                   yield "--warn:3"
                   yield "--fullpaths"
                   yield "--flaterrors"
                   yield "--target:library"
                   for dllFile in dllFiles do
                       yield "-r:" + dllFile
                   for libDir in libDirs do
                       yield "-I:" + libDir
                   match fsCoreLib with
                   | None -> ()
                   | Some fsCoreLib -> yield $"-r:%s{fsCoreLib}"

                   yield! otherFlags
                   yield fileName1 |]

            projFileName, args

        let getProjectReferences frameworkVersion otherFlags (libDirs: string list option) (dllFiles: string list) =
            let otherFlags = defaultArg otherFlags Seq.empty
            let libDirs = defaultArg libDirs []

            let hasAssembly asm =
                // we are explicitely requested
                isAssembly asm dllFiles
                || libDirs
                   |> List.exists (fun lib ->
                       Directory.EnumerateFiles(lib)
                       |> Seq.filter (fun file ->
                           // If we find a FSharp.Core in a lib path, we check if is suited for us...
                           Path.GetExtension file =? ".dll"
                           && (Path.GetFileNameWithoutExtension file <>? "FSharp.Core"
                               || (tryCheckFsCore file |> Option.isSome)))
                       |> Seq.toList
                       |> isAssembly asm)

            let hasFsCoreLib = hasAssembly "FSharp.Core"

            let fsCoreLib =
                if not hasFsCoreLib then
                    Some(findFSCore dllFiles libDirs)
                else
                    None

            let projFileName, args =
                getCheckerArguments
                    frameworkVersion
                    ignore
                    hasFsCoreLib
                    (fsCoreLib: _ option)
                    dllFiles
                    libDirs
                    otherFlags
            //Log.verbf "Checker Arguments: %O" (Log.formatArgs args)

            let options = checker.GetProjectOptionsFromCommandLineArgs(projFileName, args)

            let results = checker.ParseAndCheckProject(options) |> Async.RunSynchronously

            let mapError (err: FSharpDiagnostic) =
                sprintf
                    "**** %s: %s"
                    (if err.Severity = FSharpDiagnosticSeverity.Error then
                         "error"
                     else
                         "warning")
                    err.Message

            if results.HasCriticalErrors then
                let errors = results.Diagnostics |> Seq.map mapError

                let errorMsg =
                    sprintf "Parsing and checking project failed: \n\t%s" (System.String.Join("\n\t", errors))

                Log.errorf "%s" errorMsg
                failwith errorMsg
            else if results.Diagnostics.Length > 0 then
                let warnings = results.Diagnostics |> Seq.map mapError
                Log.warnf "Parsing and checking warnings: \n\t%s" (System.String.Join("\n\t", warnings))

            let references = results.ProjectContext.GetReferencedAssemblies()

            references

        let referenceMap references =
            references
            |> List.choose (fun (r: FSharpAssembly) -> r.FileName |> Option.map (fun f -> f, r))

        let resolve (dllFiles: string list) references =
            let referenceDict = referenceMap references |> dict

            dllFiles
            |> List.map (fun file ->
                file,
                (match referenceDict.TryGetValue file with
                 | true, refFile -> Some refFile
                 | false, _ -> None))

        let getProjectReferencesSimple frameworkVersion (dllFiles: string list) =
            getProjectReferences frameworkVersion None None dllFiles |> resolve dllFiles

        let getProjectReferenceFromFile frameworkVersion dllFile =
            getProjectReferencesSimple frameworkVersion [ dllFile ]
            |> List.exactlyOne
            |> snd

        let rec enumerateEntities (e: FSharpEntity) =
            [ yield e; yield! e.NestedEntities |> Seq.collect enumerateEntities ]

    type Type with

        /// The FullName but without any generic parameter types.
        member x.NamespaceName =
            x.FullName.Substring(
                0,
                match x.FullName.IndexOf '[' with
                | -1 -> x.FullName.Length
                | i -> i
            )

    type FSharpAssembly with

        static member LoadFiles(dllFiles: string list, ?libDirs: string list, ?otherFlags) =
            let libDirs = defaultArg libDirs []

            let findReferences libDir =
                Directory.EnumerateFiles(libDir, "*.dll")
                // Filter files already referenced directly
                |> Seq.filter (fun file ->
                    let fileName = Path.GetFullPath file |> Path.GetFileName

                    let dllNotAlreadyReferenced =
                        dllFiles
                        |> List.exists (fun (dllFile: string) -> Path.GetFileName dllFile =? fileName)
                        |> not

                    let checkFSharpCore =
                        if Path.GetFileName file =? "FSharp.Core.dll" then
                            FSharpAssemblyHelper.tryCheckFsCore file |> Option.isSome
                        else
                            true

                    dllNotAlreadyReferenced && checkFSharpCore)
                |> Seq.toList

            // See https://github.com/tpetricek/FSharp.Formatting/commit/5d14f45cd7e70c2164a7448ea50a6b9995166489
            let _dllFiles, _libDirs = libDirs |> List.collect findReferences |> List.append dllFiles, List.empty

            let frameworkVersion = FSharpAssemblyHelper.defaultFrameworkVersion

            let refs = FSharpAssemblyHelper.getProjectReferences frameworkVersion otherFlags (Some _libDirs) _dllFiles

            let result = FSharpAssemblyHelper.resolve dllFiles refs

            result

        member x.FindType(t: Type) =
            x.Contents.Entities
            |> Seq.collect FSharpAssemblyHelper.enumerateEntities
            |> Seq.tryPick (fun entity ->
                let namespaceName = t.NamespaceName.Replace("+", ".")

                match entity.TryFullName with
                | Some fullName when namespaceName = fullName -> Some entity
                | _ -> None)

type internal OutputData =
    { FsiOutput: string
      ScriptOutput: string
      Merged: string }

type internal InteractionOutputs =
    { Output: OutputData
      Error: OutputData }

/// This exception indicates that an exception happened while compiling or executing given F# code.
type internal FsiEvaluationException
    (msg: string, input: string, args: string list option, result: InteractionOutputs, inner: System.Exception) =
    inherit Exception(msg, inner)

    member x.Result = result
    member x.Input = input

    override x.ToString() =
        let nl (s: string) = s.Replace("\n", "\n\t")

        match args with
        | None ->
            sprintf
                "FsiEvaluationException:\n\nError: %s\n\nOutput: %s\n\nInput: %s\n\nException: %s"
                (nl x.Result.Error.Merged)
                (nl x.Result.Output.Merged)
                (nl x.Input)
                (base.ToString())
        | Some args ->
            sprintf
                "FsiEvaluationException:\n\nError: %s\n\nOutput: %s\n\nInput: %s\n\Arguments: %s\n\nException: %s"
                (nl x.Result.Error.Merged)
                (nl x.Result.Output.Merged)
                (nl x.Input)
                (Log.formatArgs args)
                (base.ToString())

/// Exception for invalid expression types
type internal FsiExpressionTypeException =
    val private value: obj option
    val private expected: System.Type
    inherit FsiEvaluationException

    new(msg: string, input: string, result: InteractionOutputs, expect: System.Type, ?value: obj) =
        { inherit FsiEvaluationException(msg, input, None, result, null)
          expected = expect
          value = value }

    member x.Value = x.value
    member x.ExpectedType = x.expected

type internal HandledResult<'a> =
    | InvalidExpressionType of FsiExpressionTypeException
    | InvalidCode of FsiEvaluationException
    | Result of 'a

module internal Shell =
    /// Represents a simple (fake) event loop for the 'fsi' object
    type SimpleEventLoop() =
        member _.Run() = ()
        member _.Invoke<'T>(f: unit -> 'T) = f ()
        member _.ScheduleRestart() = ()

    /// Implements a simple 'fsi' object to be passed to the FSI evaluator
    [<Sealed>]
    type InteractiveSettings() =
        let mutable evLoop = (new SimpleEventLoop())
        let mutable showIDictionary = true
        let mutable showDeclarationValues = true
        let mutable args = System.Environment.GetCommandLineArgs()
        let mutable fpfmt = "g10"

        let mutable fp = (System.Globalization.CultureInfo.InvariantCulture :> System.IFormatProvider)

        let mutable printWidth = 78
        let mutable printDepth = 100
        let mutable printLength = 100
        let mutable printSize = 10000
        let mutable showIEnumerable = true
        let mutable showProperties = true
        let mutable addedPrinters = []

        member _.FloatingPointFormat
            with get () = fpfmt
            and set v = fpfmt <- v

        member _.FormatProvider
            with get () = fp
            and set v = fp <- v

        member _.PrintWidth
            with get () = printWidth
            and set v = printWidth <- v

        member _.PrintDepth
            with get () = printDepth
            and set v = printDepth <- v

        member _.PrintLength
            with get () = printLength
            and set v = printLength <- v

        member _.PrintSize
            with get () = printSize
            and set v = printSize <- v

        member _.ShowDeclarationValues
            with get () = showDeclarationValues
            and set v = showDeclarationValues <- v

        member _.ShowProperties
            with get () = showProperties
            and set v = showProperties <- v

        member _.ShowIEnumerable
            with get () = showIEnumerable
            and set v = showIEnumerable <- v

        member _.ShowIDictionary
            with get () = showIDictionary
            and set v = showIDictionary <- v

        member _.AddedPrinters
            with get () = addedPrinters
            and set v = addedPrinters <- v

        member _.CommandLineArgs
            with get () = args
            and set v = args <- v

        member _.AddPrinter(printer: 'T -> string) =
            addedPrinters <- Choice1Of2(typeof<'T>, unbox >> printer) :: addedPrinters

        member _.EventLoop
            with get () = evLoop
            and set (_: SimpleEventLoop) = ()

        member _.AddPrintTransformer(printer: 'T -> obj) =
            addedPrinters <- Choice2Of2(typeof<'T>, unbox >> printer) :: addedPrinters

module internal ArgParser =
    let (|StartsWith|_|) (start: string) (s: string) =
        if s.StartsWith(start, StringComparison.Ordinal) then
            StartsWith(s.Substring(start.Length)) |> Some
        else
            None

    [<return: Struct>]
    let (|FsiBoolArg|_|) argName s =
        match s with
        | StartsWith argName rest ->
            if String.IsNullOrWhiteSpace rest then
                ValueSome true
            else
                match rest with
                | "+" -> ValueSome true
                | "-" -> ValueSome false
                | _ -> ValueNone
        | _ -> ValueNone

open ArgParser

[<Struct>]
type internal DebugMode =
    | Full
    | PdbOnly
    | Portable
    | NoDebug

[<Struct>]
type internal OptimizationType =
    | NoJitOptimize
    | NoJitTracking
    | NoLocalOptimize
    | NoCrossOptimize
    | NoTailCalls

/// See https://learn.microsoft.com/en-us/dotnet/fsharp/language-reference/fsharp-interactive-options
type internal FsiOptions =
    {
        Checked: bool option
        Codepage: int option
        CrossOptimize: bool option
        Debug: DebugMode option
        Defines: string list
        Exec: bool
        FullPaths: bool
        Gui: bool option
        LibDirs: string list
        Loads: string list
        NoFramework: bool
        NoLogo: bool
        NonInteractive: bool
        NoWarns: int list
        Optimize: (bool * OptimizationType list) list
        Quiet: bool
        QuotationsDebug: bool
        ReadLine: bool option
        References: string list
        TailCalls: bool option
        Uses: string list
        Utf8Output: bool
        /// Sets a warning level (0 to 5). The default level is 3. Each warning is given a level based on its severity. Level 5 gives more, but less severe, warnings than level 1.
        /// Level 5 warnings are: 21 (recursive use checked at runtime), 22 (let rec evaluated out of order), 45 (full abstraction), and 52 (defensive copy). All other warnings are level 2.
        WarnLevel: int option
        WarnAsError: bool option
        WarnAsErrorList: (bool * int list) list
        MultiEmit: bool option
        ScriptArgs: string list
    }

    static member Empty =
        { Checked = None
          Codepage = None
          CrossOptimize = None
          Debug = None
          Defines = []
          Exec = false
          FullPaths = false
          Gui = None
          LibDirs = []
          Loads = []
          NoFramework = false
          NoLogo = false
          NonInteractive = false
          NoWarns = []
          Optimize = []
          Quiet = false
          QuotationsDebug = false
          ReadLine = None
          References = []
          TailCalls = None
          Uses = []
          Utf8Output = false
          WarnLevel = None
          WarnAsError = None
          WarnAsErrorList = []
          MultiEmit = None
          ScriptArgs = [] }

    static member Default =
        let includes = []

        if Env.isNetCoreApp then
            { FsiOptions.Empty with
                LibDirs = includes
                NonInteractive = true }
        else
            let fsCore = FSharpAssemblyHelper.findFSCore [] includes

            Log.verbf "Using FSharp.Core: %s" fsCore

            { FsiOptions.Empty with
                LibDirs = includes
                NoFramework = true
                References = [ fsCore ]
                NonInteractive = true }

    static member ofArgs args =
        args
        |> Seq.fold
            (fun (parsed, state) (arg: string) ->
                match state, arg with
                | (false, Some cont), _ when not (arg.StartsWith("--", StringComparison.Ordinal)) ->
                    let parsed, (userArgs, newCont) = cont arg
                    parsed, (userArgs, unbox newCont)
                | _, "--" -> parsed, (true, None)
                | (true, _), a ->
                    { parsed with
                        ScriptArgs = a :: parsed.ScriptArgs },
                    state
                | _, FsiBoolArg "--checked" enabled -> { parsed with Checked = Some enabled }, state
                | _, StartsWith "--codepage:" res -> { parsed with Codepage = Some(int res) }, state
                | _, FsiBoolArg "--crossoptimize" enabled ->
                    { parsed with
                        CrossOptimize = Some enabled },
                    state
                | _, StartsWith "--debug:" "pdbonly"
                | _, StartsWith "-g:" "pdbonly" ->
                    { parsed with
                        Debug = Some DebugMode.PdbOnly },
                    state
                | _, StartsWith "--debug:" "portable"
                | _, StartsWith "-g:" "portable" ->
                    { parsed with
                        Debug = Some DebugMode.Portable },
                    state
                | _, StartsWith "--debug:" "full"
                | _, StartsWith "-g:" "full"
                | _, FsiBoolArg "--debug" true
                | _, FsiBoolArg "-g" true ->
                    { parsed with
                        Debug = Some DebugMode.Full },
                    state
                | _, FsiBoolArg "--debug" false
                | _, FsiBoolArg "-g" false ->
                    { parsed with
                        Debug = Some DebugMode.NoDebug },
                    state
                | _, StartsWith "-d:" def
                | _, StartsWith "--define:" def ->
                    { parsed with
                        Defines = def :: parsed.Defines },
                    state
                | _, "--exec" -> { parsed with Exec = true }, state
                | _, "--noninteractive" -> { parsed with NonInteractive = true }, state
                | _, "--fullpaths" -> { parsed with FullPaths = true }, state
                | _, FsiBoolArg "--gui" enabled -> { parsed with Gui = Some enabled }, state
                | _, StartsWith "-I:" lib
                | _, StartsWith "--lib:" lib ->
                    { parsed with
                        LibDirs = lib :: parsed.LibDirs },
                    state
                | _, StartsWith "--load:" load ->
                    { parsed with
                        Loads = load :: parsed.Loads },
                    state
                | _, "--noframework" -> { parsed with NoFramework = true }, state
                | _, "--nologo" -> { parsed with NoLogo = true }, state
                | _, StartsWith "--nowarn:" warns ->
                    let noWarns = warns.Split([| ',' |]) |> Seq.map int |> Seq.toList

                    { parsed with
                        NoWarns = noWarns @ parsed.NoWarns },
                    state
                | _, FsiBoolArg "--optimize" enabled ->
                    let cont (arg: string) =
                        let optList =
                            arg.Split([| ',' |])
                            |> Seq.map (function
                                | "nojitoptimize" -> NoJitOptimize
                                | "nojittracking" -> NoJitTracking
                                | "nolocaloptimize" -> NoLocalOptimize
                                | "nocrossoptimize" -> NoCrossOptimize
                                | "notailcalls" -> NoTailCalls
                                | unknown -> failwithf "Unknown optimization option %s" unknown)
                            |> Seq.toList

                        { parsed with
                            Optimize = (enabled, optList) :: parsed.Optimize },
                        (false, box None)

                    { parsed with
                        Optimize = (enabled, []) :: parsed.Optimize },
                    (false, Some cont)
                | _, "--quiet" -> { parsed with Quiet = true }, state
                | _, "--quotations-debug" -> { parsed with QuotationsDebug = true }, state
                | _, FsiBoolArg "--readline" enabled -> { parsed with ReadLine = Some enabled }, state
                | _, StartsWith "-r:" ref
                | _, StartsWith "--reference:" ref ->
                    { parsed with
                        References = ref :: parsed.References },
                    state
                | _, FsiBoolArg "--tailcalls" enabled -> { parsed with TailCalls = Some enabled }, state
                | _, StartsWith "--use:" useFile ->
                    { parsed with
                        Uses = useFile :: parsed.Uses },
                    state
                | _, "--utf8output" -> { parsed with Utf8Output = true }, state
                | _, StartsWith "--warn:" warn ->
                    { parsed with
                        WarnLevel = Some(int warn) },
                    state
                | _, FsiBoolArg "--warnaserror" enabled ->
                    { parsed with
                        WarnAsError = Some enabled },
                    state
                | _, StartsWith "--warnaserror" warnOpts ->
                    let parseList (l: string) =
                        l.Split [| ',' |] |> Seq.map int |> Seq.toList

                    match warnOpts.[0], (if warnOpts.Length > 1 then Some warnOpts.[1] else None) with
                    | ':', _ ->
                        { parsed with
                            WarnAsErrorList = (true, parseList (warnOpts.Substring 1)) :: parsed.WarnAsErrorList },
                        state
                    | '+', Some ':' ->
                        { parsed with
                            WarnAsErrorList = (true, parseList (warnOpts.Substring 2)) :: parsed.WarnAsErrorList },
                        state
                    | '-', Some ':' ->
                        { parsed with
                            WarnAsErrorList = (false, parseList (warnOpts.Substring 2)) :: parsed.WarnAsErrorList },
                        state
                    | _ -> failwithf "invalid --warnaserror argument: %s" arg
                | _, FsiBoolArg "--multiemit" enabled -> { parsed with MultiEmit = Some enabled }, state
                | _, unknown ->
                    { parsed with
                        ScriptArgs = unknown :: parsed.ScriptArgs },
                    (true, None))
            (FsiOptions.Empty, (false, None))
        |> fst
        |> (fun p ->
            { p with
                ScriptArgs = p.ScriptArgs |> List.rev
                Defines = p.Defines |> List.rev
                References = p.References |> List.rev
                LibDirs = p.LibDirs |> List.rev
                Loads = p.Loads |> List.rev
                Uses = p.Uses |> List.rev })

    member x.AsArgs =
        let maybeArg opt =
            match opt with
            | Some a -> Seq.singleton a
            | None -> Seq.empty

        let maybeArgMap opt f = opt |> Option.map f |> maybeArg
        let getMinusPlus b = if b then "+" else "-"

        let getFsiBoolArg name opt =
            maybeArgMap opt (getMinusPlus >> sprintf "%s%s" name)

        let getSimpleBoolArg name b =
            if b then Some name else None
            |> maybeArg

        [| yield! getFsiBoolArg "--checked" x.Checked
           yield! maybeArgMap x.Codepage (fun i -> sprintf "--codepage:%d" i)
           yield! getFsiBoolArg "--crossoptimize" x.CrossOptimize
           // ! -g[+|-|:full|:pdbonly] is not working, see https://github.com/Microsoft/visualfsharp/issues/311
           yield!
               maybeArgMap x.Debug (function
                   | Full -> "--debug:full"
                   | PdbOnly -> "--debug:pdbonly"
                   | Portable -> "--debug:portable"
                   | NoDebug -> "--debug-")
           yield! x.Defines |> Seq.map (sprintf "--define:%s")
           yield! getSimpleBoolArg "--exec" x.Exec
           yield! getSimpleBoolArg "--fullpaths" x.FullPaths
           yield! getFsiBoolArg "--gui" x.Gui
           yield! x.LibDirs |> Seq.map (sprintf "-I:%s")
           yield! x.Loads |> Seq.map (sprintf "--load:%s")
           yield! getSimpleBoolArg "--noframework" x.NoFramework
           yield! getSimpleBoolArg "--nologo" x.NoLogo
           yield! getSimpleBoolArg "--noninteractive" x.NonInteractive

           yield!
               (match x.NoWarns with
                | [] -> None
                | l -> l |> Seq.map string<int> |> String.concat "," |> sprintf "--nowarn:%s" |> Some)
               |> maybeArg
           yield!
               match x.Optimize with
               | [] -> Seq.empty
               | opts ->
                   opts
                   |> Seq.collect (fun (enable, types) ->
                       seq {
                           yield sprintf "--optimize%s" (getMinusPlus enable)

                           match types with
                           | [] -> ()
                           | _ ->
                               yield
                                   types
                                   |> Seq.map (function
                                       | NoJitOptimize -> "nojitoptimize"
                                       | NoJitTracking -> "nojittracking"
                                       | NoLocalOptimize -> "nolocaloptimize"
                                       | NoCrossOptimize -> "nocrossoptimize"
                                       | NoTailCalls -> "notailcalls")
                                   |> String.concat ","
                       })

           yield! getSimpleBoolArg "--quiet" x.Quiet
           yield! getSimpleBoolArg "--quotations-debug" x.QuotationsDebug
           yield! getFsiBoolArg "--readline" x.ReadLine

           yield! x.References |> Seq.map (sprintf "-r:%s")

           yield! getFsiBoolArg "--tailcalls" x.TailCalls
           yield! x.Uses |> Seq.map (sprintf "--use:%s")

           yield! getSimpleBoolArg "--utf8output" x.Utf8Output

           yield! maybeArgMap x.WarnLevel (fun i -> sprintf "--warn:%d" i)

           yield! getFsiBoolArg "--warnaserror" x.WarnAsError
           yield! getFsiBoolArg "--multiemit" x.MultiEmit

           yield!
               x.WarnAsErrorList
               |> Seq.map (fun (enable, warnNums) ->
                   warnNums
                   |> Seq.map string<int>
                   |> String.concat ","
                   |> sprintf "--warnaserror%s:%s" (getMinusPlus enable))

           match x.ScriptArgs with
           | [] -> ()
           | l ->
               yield "--"
               yield! l |]

[<AutoOpen>]
module internal Helper =

    type ForwardTextWriter(f) =
        inherit TextWriter()
        override __.Flush() = ()
        override __.Write(c: char) = f (string<char> c)

        override __.Write(c: string) =
            if isNull c |> not then
                f c

        override __.WriteLine(c: string) =
            f <| sprintf "%s%s" c Environment.NewLine

        override __.WriteLine() = f Environment.NewLine

        override __.Dispose(r) =
            base.Dispose r

            if r then
                f null

        override __.Encoding = Encoding.UTF8
        static member Create f = new ForwardTextWriter(f) :> TextWriter

    type CombineTextWriter(l: TextWriter list) =
        inherit TextWriter()
        do assert (l.Length > 0)
        let doAll f = l |> Seq.iter f
        override __.Flush() = doAll (fun t -> t.Flush())
        override __.Write(c: char) = doAll (fun t -> t.Write c)

        override __.Write(c: string) =
            if not (System.String.IsNullOrEmpty c) then
                doAll (fun t -> t.Write c)

        override __.WriteLine(c: string) = doAll (fun t -> t.WriteLine c)
        override __.WriteLine() = doAll (fun t -> t.WriteLine())

        override __.Dispose(r) =
            base.Dispose r

            if r then
                doAll (fun t -> t.Dispose())

        override __.Encoding = Encoding.UTF8
        static member Create l = new CombineTextWriter(l) :> TextWriter

    type OutStreamHelper(saveGlobal, liveOutWriter: _ option, liveFsiWriter: _ option) =
        let globalFsiOut = new StringBuilder()
        let globalStdOut = new StringBuilder()
        let globalMergedOut = new StringBuilder()

        let fsiOut = new StringBuilder()
        let stdOut = new StringBuilder()
        let mergedOut = new StringBuilder()
        let fsiOutStream = new StringWriter(fsiOut) :> TextWriter
        let stdOutStream = new StringWriter(stdOut) :> TextWriter

        let mergedOutStream = new StringWriter(mergedOut) :> TextWriter

        let fsiOutWriter =
            CombineTextWriter.Create
                [ yield fsiOutStream
                  yield mergedOutStream
                  match liveFsiWriter with
                  | None -> ()
                  | Some liveFsiWriter -> yield liveFsiWriter ]

        let stdOutWriter =
            CombineTextWriter.Create
                [ yield stdOutStream
                  yield mergedOutStream
                  match liveOutWriter with
                  | None -> ()
                  | Some liveFsiWriter -> yield liveFsiWriter ]

        let all = [ globalFsiOut, fsiOut; globalStdOut, stdOut; globalMergedOut, mergedOut ]

        member _.FsiOutWriter = fsiOutWriter
        member _.StdOutWriter = stdOutWriter

        member _.GetOutputAndResetLocal() =
            let mapped =
                all
                |> List.map (fun (global', local) ->
                    let data = local.ToString()

                    if saveGlobal then
                        global'.Append(data) |> ignore

                    local.Clear() |> ignore
                    data)

            match mapped with
            | [ fsi; std; merged ] ->
                { FsiOutput = fsi
                  ScriptOutput = std
                  Merged = merged }
            | _ -> failwith $"Expected three StringBuilders, got %A{mapped}"

    let consoleCapture out err f =
        let defOut = Console.Out
        let defErr = Console.Error

        try
            Console.SetOut out
            Console.SetError err
            f ()
        finally
            Console.SetOut defOut
            Console.SetError defErr

type internal FsiSession
    (fsi: obj, options: FsiOptions, reportGlobal, liveOut, liveOutFsi, liveErr, liveErrFsi, discardStdOut) =
    // Intialize output and input streams
    let out = new OutStreamHelper(reportGlobal, liveOut, liveOutFsi)

    let err = new OutStreamHelper(reportGlobal, liveErr, liveErrFsi)

    let sbInput = new StringBuilder()
    let inStream = new StringReader("")

    // Build command line arguments & start FSI session
    let args = [| yield "C:\\fsi.exe"; yield! options.AsArgs |]

    do Log.verbf "Starting nested fsi.exe with args: %s" (Log.formatArgs args)

    let saveOutput () =
        let out = out.GetOutputAndResetLocal()
        let err = err.GetOutputAndResetLocal()
        { Output = out; Error = err }

    let getMessages () =
        let out = out.GetOutputAndResetLocal()
        let err = err.GetOutputAndResetLocal()
        let inp = sbInput.ToString()
        err, out, inp

    let redirectOut f =
        let captureOut, captureErr =
            if discardStdOut then
                out.StdOutWriter, err.StdOutWriter
            else
                let defOut = Console.Out
                let defErr = Console.Error

                (CombineTextWriter.Create [ defOut; out.StdOutWriter ]),
                (CombineTextWriter.Create [ defErr; err.StdOutWriter ])

        consoleCapture captureOut captureErr f

    let fsiSession =
        try
            let fsiConfig = FsiEvaluationSession.GetDefaultConfiguration(fsi, false)

            redirectOut (fun () ->
                let session =
                    FsiEvaluationSession.Create(fsiConfig, args, inStream, out.FsiOutWriter, err.FsiOutWriter)

                saveOutput () |> ignore
                session)
        with e ->
            let err, out, _ = getMessages ()

            raise
            <| new FsiEvaluationException(
                "Error while creating a fsi session.",
                sprintf "Fsi Arguments: %s" (Log.formatArgs args),
                args |> Array.toList |> Some,
                { Output = out; Error = err },
                e
            )

    let save_ f text =
        try
            redirectOut (fun () ->
                let res = f text
                saveOutput (), res)
        with e ->
            let err, out, inp = getMessages ()

            raise
            <| new FsiEvaluationException(
                "Error while compiling or executing fsharp snippet.",
                (if reportGlobal then inp else text),
                args |> Array.toList |> Some,
                { Output = out; Error = err },
                e
            )

    let save f =
        save_ (fun text ->
            if reportGlobal then
                sbInput.AppendLine(text) |> ignore

            f text)

    let evalInteraction = save fsiSession.EvalInteractionNonThrowing

    let evalExpression = save fsiSession.EvalExpressionNonThrowing

    let diagsToString (diags: FSharpDiagnostic array) =
        [ for d in diags -> d.ToString() + Environment.NewLine ] |> String.concat ""

    let addDiagsToFsiOutput (o: InteractionOutputs) diags =
        { o with
            Output =
                { o.Output with
                    FsiOutput = diagsToString diags + o.Output.FsiOutput } }

    member _.EvalInteraction text =
        let i, (r, diags) = evalInteraction text
        let i2 = addDiagsToFsiOutput i diags

        let res =
            match r with
            | Choice1Of2 v -> Ok v
            | Choice2Of2 exn -> Error exn

        i2, res

    member _.TryEvalExpression text =
        let i, (r, diags) = evalExpression text
        let i2 = addDiagsToFsiOutput i diags

        let res =
            match r with
            | Choice1Of2 v -> Ok(v |> Option.map (fun r -> r.ReflectionValue, r.ReflectionType))
            | Choice2Of2 exn -> Error exn

        i2, res

    member _.Dispose() = (fsiSession :> IDisposable).Dispose()

    /// Change the current directory (so that relative paths within scripts work properly).
    /// Returns a handle to change the current directory back to it's initial state
    /// (Because this will change the current directory of the currently running code as well!).
    member x.ChangeCurrentDirectory dir =
        let oldDir = Directory.GetCurrentDirectory()

        let cd dir =
            x.EvalInteraction(sprintf "System.Environment.CurrentDirectory <- @\"%s\"" dir)
            |> ignore

            x.EvalInteraction(sprintf "#cd @\"%s\"" dir) |> ignore

        cd dir
        let isDisposed = ref false

        { new System.IDisposable with
            member _.Dispose() =
                if not !isDisposed then
                    cd oldDir
                    isDisposed := true }

    /// Same as ChangeCurrentDirectory but takes a function for the scope.
    member x.WithCurrentDirectory dir f =
        use __ = x.ChangeCurrentDirectory dir
        f ()

type internal ScriptHost() =

    /// Create a new IFsiSession by specifying all fsi arguments manually.
    static member Create
        (
            opts: FsiOptions,
            ?fsiObj: obj,
            ?reportGlobal,
            ?outWriter: TextWriter,
            ?fsiOutWriter: TextWriter,
            ?errWriter: TextWriter,
            ?fsiErrWriter: TextWriter,
            ?discardStdOut
        ) =

        let fsiObj = defaultArg fsiObj (FSharp.Compiler.Interactive.Shell.Settings.fsi :> obj)

        let reportGlobal = defaultArg reportGlobal false
        let discardStdOut = defaultArg discardStdOut false
        FsiSession(fsiObj, opts, reportGlobal, outWriter, fsiOutWriter, errWriter, fsiErrWriter, discardStdOut)

    /// Quickly create a new IFsiSession with some sane defaults
    static member CreateNew
        (
            ?defines: string list,
            ?fsiObj: obj,
            ?reportGlobal,
            ?outWriter: TextWriter,
            ?fsiOutWriter: TextWriter,
            ?errWriter: TextWriter,
            ?fsiErrWriter: TextWriter,
            ?discardStdOut
        ) =
        let opts =
            { FsiOptions.Default with
                Defines = defaultArg defines [] }

        ScriptHost.Create(
            opts,
            ?fsiObj = fsiObj,
            ?reportGlobal = reportGlobal,
            ?outWriter = outWriter,
            ?fsiOutWriter = fsiOutWriter,
            ?errWriter = errWriter,
            ?fsiErrWriter = fsiErrWriter,
            ?discardStdOut = discardStdOut
        )
