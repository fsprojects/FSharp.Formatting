// --------------------------------------------------------------------------------------
// F# CodeFormat (FSharpCompiler.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

// Using 'Microsoft' namespace to make the API as similar to the actual one as possible
namespace Microsoft.FSharp.Compiler

// --------------------------------------------------------------------------------------
// Wrapper for the APIs in 'FSharp.Compiler.dll' and 'FSharp.Compiler.Server.Shared.dll'
// The API is currently internal, so we call it using the (?) operator and Reflection
// --------------------------------------------------------------------------------------

open System
open System.Reflection
open Microsoft.FSharp.Reflection
open System.Globalization

exception CompilerMissingException of string * string

/// Implements the (?) operator that makes it possible to access internal methods
/// and properties and contains definitions for F# assemblies
module Reflection =   
  // Various flags configurations for Reflection
  let staticFlags = BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Static 
  let instanceFlags = BindingFlags.NonPublic ||| BindingFlags.Public ||| BindingFlags.Instance
  let ctorFlags = instanceFlags
  let inline asMethodBase(a:#MethodBase) = a :> MethodBase
  

  // Caching to make the initial lookup a bit faster
  let typeInfoLookup = new System.Collections.Generic.Dictionary<_, option<Type * Type>>()
  let lookupTypeInfo typ f = 
    match typeInfoLookup.TryGetValue(typ) with
    | true, res -> res
    | false, _ ->
        let res = f()
        typeInfoLookup.Add(typ, res)
        res

  let (?) (o:obj) name : 'R =
    let extractTypeInfo () = 
      if FSharpType.IsFunction(typeof<'R>) then
        Some(FSharpType.GetFunctionElements(typeof<'R>))
      else None

    match lookupTypeInfo (typeof<'R>) extractTypeInfo with
    // The return type is a function, which means that we want to invoke a method
    | Some(argType, resType) -> 
      FSharpValue.MakeFunction(typeof<'R>, fun args ->
        // We treat elements of a tuple passed as argument as a list of arguments
        // When the 'o' object is 'System.Type', we call static methods
        let methods, instance, args = 
          let args = 
            if Object.Equals(argType, typeof<unit>) then [| |]
            elif not(FSharpType.IsTuple(argType)) then [| args |]
            else FSharpValue.GetTupleFields(args)
          if (typeof<System.Type>).IsAssignableFrom(o.GetType()) then 
            let methods = (unbox<Type> o).GetMethods(staticFlags) |> Array.map asMethodBase
            let ctors = (unbox<Type> o).GetConstructors(ctorFlags) |> Array.map asMethodBase
            Array.concat [ methods; ctors ], null, args
          else 
            o.GetType().GetMethods(instanceFlags) |> Array.map asMethodBase, o, args
        
        // A simple overload resolution based on the name and number of parameters only
        let methods = 
          [ for m in methods do
              if m.Name = name && m.GetParameters().Length = args.Length then yield m 
              if m.Name = name && m.IsGenericMethod &&
                 m.GetGenericArguments().Length + m.GetParameters().Length = args.Length then yield m ]
        match methods with 
        | [] -> failwithf "No method '%s' with %d arguments found" name args.Length
        | _::_::_ -> failwithf "Multiple methods '%s' with %d arguments found" name args.Length
        | [:? ConstructorInfo as c] -> c.Invoke(args)
        | [ m ] when m.IsGenericMethod ->
            let tyCount = m.GetGenericArguments().Length
            let tyArgs = args |> Seq.take tyCount 
            let actualArgs = args |> Seq.skip tyCount
            let gm = (m :?> MethodInfo).MakeGenericMethod [| for a in tyArgs -> unbox a |]
            gm.Invoke(instance, Array.ofSeq actualArgs)
        | [ m ] -> m.Invoke(instance, args) ) |> unbox<'R>
    | _ ->
      // When the 'o' object is 'System.Type', we access static properties
      let typ, flags, instance = 
        if (typeof<System.Type>).IsAssignableFrom(o.GetType()) then unbox o, staticFlags, null
        else o.GetType(), instanceFlags, o
      
      // Find a property that we can call and get the value
      let prop = typ.GetProperty(name, flags)
      if Object.Equals(prop, null) then 
        let fld = typ.GetField(name, flags)
        if Object.Equals(fld, null) then
          failwithf "Field or property '%s' not found in '%s' using flags '%A'." name typ.Name flags
        else
          fld.GetValue(instance) |> unbox<'R>
      else
        let meth = prop.GetGetMethod(true)
        if Object.Equals(prop, null) then failwithf "Property '%s' found, but doesn't have 'get' method." name
        meth.Invoke(instance, [| |]) |> unbox<'R>
        


  /// Convert list of type FSharpList to a list in the specified FSharp.Core assembly
  let convertList (list:obj) (fsharpCore:Assembly) : obj =
    // Find the generic arguments of the source
    let bases = list.GetType() |> Seq.unfold (fun ty -> 
      if ty.FullName = "System.Object" then None else Some(ty, ty.BaseType) )
    let sourceListTyp = bases |> Seq.find (fun ty -> ty.Name = "FSharpList`1")
    let tyArg = sourceListTyp.GetGenericArguments().[0]
        
    let listMod = fsharpCore.GetType("Microsoft.FSharp.Collections.ListModule")
    listMod?OfSeq(tyArg, list)


  /// Really simple closure type for creating delegates
  type Closure<'T, 'R>(f) =
    member x.Invoke(a : 'T) : 'R = unbox (f (box a))

  /// Convert function of type FSharpFunc to a function in the specified FSharp.Core assembly
  let convertFunction (func:obj) (fsharpCore:Assembly) : obj =
    // Find the generic arguments of the source
    let bases = func.GetType() |> Seq.unfold (fun ty -> 
      if ty.FullName = "System.Object" then None else Some(ty, ty.BaseType) )
    let sourceFuncTyp = bases |> Seq.find (fun ty -> ty.Name = "FSharpFunc`2")
    if not (sourceFuncTyp.IsGenericType) then failwith "FSharpFunc should be generic!"

    let args = sourceFuncTyp.GetGenericArguments()
    if args.Length <> 2 then failwith "FSharpFunc has wrong number of generic args!"
    if args.[1].Name = "FSharpFunc`2" then failwith "Curried functions not supported yet!"

    // Make target function type
    let targetArgs = args |> Array.map (fun arg -> 
      // Does not work for generic types, but does the trick for unit
      if arg.FullName.StartsWith("Microsoft.FSharp.Core") then
        fsharpCore.GetType(arg.FullName)
      else arg)

    // Create closure that invokes the source function using reflection
    let invoke = func.GetType().GetMethod("Invoke")
    let invokeFunc (arg:obj) = invoke.Invoke(func, [| arg |]) 
    let clo = typedefof<Closure<_, _>>.MakeGenericType(targetArgs)?``.ctor``(invokeFunc)

    // Create converter delegate from the closure and turn it to F# function
    // Assumes current runtime and the taget's runtime are the same...
    let funcTyp = fsharpCore.GetType("Microsoft.FSharp.Core.FSharpFunc`2")
    let boundFuncTyp = funcTyp.MakeGenericType(targetArgs)
    let boundConverter = typedefof<System.Converter<_, _>>.MakeGenericType(targetArgs)
    let converter = Delegate.CreateDelegate(boundConverter, clo, clo.GetType().GetMethod("Invoke"))
    boundFuncTyp?FromConverter(converter)


  
  /// Wrapper type for the 'FSharp.Compiler.dll' assembly - expose types we use
  type FSharpCompilerWrapper() =      

    let mutable fsharpCompiler = None
    let mutable fsharpCore = None

    /// Exposes the currently loaded FSharp.Compiler.dll
    member x.FSharpCompiler : Assembly = 
      match fsharpCompiler with 
      | None -> failwith "Assembly FSharp.Compiler is not configured!"
      | Some asm -> asm
    /// Returns the referenced 'FSharp.Core.dll' assembly
    member x.ReferencedFSharpCore = 
      match fsharpCore with 
      | None -> failwith "Assembly FSharp.Core is not configured!"
      | Some asm -> asm

    /// Configure the wrapper to use the specified FSharp.Compiler.dll
    member x.BindToAssembly(compiler) = 
      fsharpCompiler <- Some compiler
      // Determine FSharp.Core assmembly from the FSharpFunc`1 type given as
      // the argument to InteractiveChecker.Create (kind of hack..)
      let flags = BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic
      let createMi = (x.InteractiveChecker : System.Type).GetMethod("Create", flags)
      fsharpCore <- Some (createMi.GetParameters().[0].ParameterType.Assembly)

    member x.InteractiveChecker = x.FSharpCompiler.GetType("Microsoft.FSharp.Compiler.SourceCodeServices.InteractiveChecker")
    member x.IsResultObsolete = x.FSharpCompiler.GetType("Microsoft.FSharp.Compiler.SourceCodeServices.IsResultObsolete")
    member x.CheckOptions = x.FSharpCompiler.GetType("Microsoft.FSharp.Compiler.SourceCodeServices.CheckOptions")
    member x.SourceTokenizer = x.FSharpCompiler.GetType("Microsoft.FSharp.Compiler.SourceCodeServices.SourceTokenizer")
    member x.TokenInformation = x.FSharpCompiler.GetType("Microsoft.FSharp.Compiler.SourceCodeServices.TokenInformation")
    member x.``Parser.token.Tags`` = x.FSharpCompiler.GetType("Microsoft.FSharp.Compiler.Parser+token+Tags")

  let FSharpCompiler = new FSharpCompilerWrapper()

// Hide this part of code, because it is not needed in F# Snippets
// (but it is nice to leave it here and keep file in sync with MonoDevelop)
#if INTERACTIVE_SERVER
  /// Wrapper type for the 'FSharp.Compiler.Server.Shared.dll' assembly - expose types we use
  type FSharpCompilerServerShared private () =      
    static let asm = 
      lazy try Assembly.Load("FSharp.Compiler.Server.Shared, Version=2.0.0.0, Culture=neutral, PublicKeyToken=a19089b1c74d0809")
           with e -> raise (CompilerMissingException("FSharp.Compiler.Server.Shared", e.ToString()))
    static member InteractiveServer = asm.Value.GetType("Microsoft.FSharp.Compiler.Server.Shared.FSharpInteractiveServer")

// --------------------------------------------------------------------------------------
// Wrapper for 'Microsoft.Compiler.Server.Shared', which contains some API for
// controlling F# Interactive using reflection (e.g. for interrupt)
// --------------------------------------------------------------------------------------
    
module Server =
  module Shared = 
    open Reflection
    
    type FSharpInteractiveServer(wrapped:obj) =
      static member StartClient(channel:string) = 
        FSharpInteractiveServer
          (FSharpCompilerServerShared.InteractiveServer?StartClient(channel))
      member x.Interrupt() : unit = wrapped?Interrupt()
#endif

// --------------------------------------------------------------------------------------
// Source code services (Part 1) - contains wrappers for tokenization etc.     
// --------------------------------------------------------------------------------------

module SourceCodeServices =
  open Reflection

  type TokenColorKind =
    | Comment = 2
    | Default = 0
    | Identifier = 3
    | InactiveCode = 7
    | Keyword = 1
    | Number = 9
    | Operator = 10
    | PreprocessorKeyword = 8
    | String = 4
    | Text = 0
    | UpperIdentifier = 5

  type TokenCharKind =
    | Comment = 10
    | Default = 0
    | Delimiter = 6
    | Identifier = 2
    | Keyword = 1
    | LineComment = 9
    | Literal = 4
    | Operator = 5
    | String = 3
    | Text = 0
    | WhiteSpace = 8

  type TriggerClass(wrapped:obj) = 
    member x.Wrapped = wrapped
      
  type TokenInformation(wrapped:obj) =
    member x.LeftColumn : int = wrapped?LeftColumn
    member x.RightColumn : int = wrapped?RightColumn
    member x.Tag : int = wrapped?Tag
    member x.TokenName : string = wrapped?TokenName
    member x.ColorClass : TokenColorKind = enum<TokenColorKind>(unbox wrapped?ColorClass)
    member x.CharClass : TokenCharKind = enum<TokenCharKind>(unbox wrapped?CharClass)
    member x.TriggerClass : TriggerClass = TriggerClass(wrapped?TriggerClass)
    member x.WithRightColumn(rightColumn:int) = 
      TokenInformation
        ( FSharpCompiler.TokenInformation?``.ctor``
            ( x.LeftColumn, rightColumn, int x.ColorClass, int x.CharClass,
              x.TriggerClass.Wrapped, x.Tag, x.TokenName ) )
    member x.WithTokenName(tokenName:string) = 
      TokenInformation
        ( FSharpCompiler.TokenInformation?``.ctor``
            ( x.LeftColumn, x.RightColumn, x.ColorClass, x.CharClass,
              x.TriggerClass.Wrapped, x.Tag, tokenName ) )

  type LineTokenizer(wrapped:obj) = 
    member x.StartNewLine() : unit = wrapped?StartNewLine()
    member x.ScanToken(state:int64) = 
      let tup : obj = wrapped?ScanToken(state)
      let optInfo, newstate = tup?Item1, tup?Item2
      let optInfo = 
        if optInfo = null then None
        else Some(new TokenInformation(optInfo?Value))
      optInfo, newstate
      
  type SourceTokenizer(defines:string list, source:string) =
    let wrapped = FSharpCompiler.SourceTokenizer?``.ctor``(convertList defines FSharpCompiler.ReferencedFSharpCore, source)
    member x.CreateLineTokenizer(line:string) = 
      LineTokenizer(wrapped?CreateLineTokenizer(line))
    
  // ------------------------------------------------------------------------------------

  module Array = 
    let untypedMap f (a:System.Array) = 
      Array.init a.Length (fun i -> f (a.GetValue(i)))

  module List = 
    let rec untypedMap f (l:obj) =
      (l :?> System.Collections.IEnumerable) |> Seq.cast<obj> |> Seq.map f |> List.ofSeq
    
  module PrettyNaming = 
    let IsIdentifierPartCharacter (c:char) = 
      let cat = System.Char.GetUnicodeCategory(c)
      cat = UnicodeCategory.UppercaseLetter ||
      cat = UnicodeCategory.LowercaseLetter ||
      cat = UnicodeCategory.TitlecaseLetter ||
      cat = UnicodeCategory.ModifierLetter ||
      cat = UnicodeCategory.OtherLetter ||
      cat = UnicodeCategory.LetterNumber || 
      cat = UnicodeCategory.DecimalDigitNumber ||
      cat = UnicodeCategory.ConnectorPunctuation ||
      cat = UnicodeCategory.NonSpacingMark ||
      cat = UnicodeCategory.SpacingCombiningMark || c = '\''
    
  // ------------------------------------------------------------------------------------
  // Source code services (Part 2) - contains wrappers for parsing & type checking.     
  // ------------------------------------------------------------------------------------
    
  type Position = int * int

  type Names = string list 

  type NamesWithResidue = Names * string 

  type XmlComment(wrapped:obj) =
    member x.Wrapped = wrapped

  let (|XmlCommentNone|XmlCommentText|XmlCommentSignature|) (xml:XmlComment) = 
    if xml.Wrapped?IsXmlCommentNone then XmlCommentNone()
    elif xml.Wrapped?IsXmlCommentText then XmlCommentText(xml.Wrapped?Item : string)
    elif xml.Wrapped?IsXmlCommentSignature then 
      let it1, it2 : string * string = xml.Wrapped?Item1, xml.Wrapped?Item2
      XmlCommentSignature(it1, it2)
    else failwith "Unexpected XmlComment value!"

  type DataTipElement(wrapped:obj) = 
    member x.Wrapped = wrapped

  let (|DataTipElementNone|DataTipElement|DataTipElementGroup|DataTipElementCompositionError|) (el:DataTipElement) = 
    if el.Wrapped?IsDataTipElementNone then 
      DataTipElementNone
    elif el.Wrapped?IsDataTipElement then 
      let (s:string) = el.Wrapped?Item1
      let xml = XmlComment(el.Wrapped?Item2)
      DataTipElement(s, xml)
    elif el.Wrapped?IsDataTipElementGroup then  
      let list = el.Wrapped?Item |> List.untypedMap (fun tup ->
        let (s:string) = tup?Item1
        let xml = XmlComment(tup?Item2)
        s, xml )
      DataTipElementGroup(list)
    elif el.Wrapped?IsDataTipElementCompositionError then 
      DataTipElementCompositionError(el.Wrapped?Item : string)
    else 
      failwith "Unexpected DataTipElement value!"

  type DataTipText(wrapped:obj) = 
    member x.Wrapped = wrapped

  let (|DataTipText|) (d:DataTipText) = 
    d.Wrapped?Item |> List.untypedMap (fun o ->
      DataTipElement(o))
    
  type FileTypeCheckStateIsDirty = string -> unit
          
  /// Callback that indicates whether a requested result has become obsolete.    
  [<NoComparison;NoEquality>]
  type IsResultObsolete = 
      | IsResultObsolete of (unit->bool)

  type CheckOptions(wrapped:obj) =
    member x.Wrapped = wrapped
    member x.ProjectFileName : string = wrapped?ProjectFileName
    member x.ProjectFileNames : string array = wrapped?ProjectFileNames
    member x.ProjectOptions : string array = wrapped?ProjectOptions
    member x.IsIncompleteTypeCheckEnvironment : bool = wrapped?IsIncompleteTypeCheckEnvironment 
    member x.UseScriptResolutionRules : bool = wrapped?UseScriptResolutionRules
    member x.WithProjectOptions(options:string[]) = 
      CheckOptions.Create
        ( x.ProjectFileName, x.ProjectFileNames, options, 
          x.IsIncompleteTypeCheckEnvironment, x.UseScriptResolutionRules)
    static member Create(fileName:string, fileNames:string[], options:string[], incomplete:bool, scriptRes:bool) =
      CheckOptions
        (FSharpCompiler.CheckOptions?``.ctor``
           (fileName, fileNames, options, incomplete, scriptRes))
      
  type UntypedParseInfo(wrapped:obj) =
    member x.Wrapped = wrapped
    /// Name of the file for which this information were created
    //abstract FileName                       : string
    /// Get declaraed items and the selected item at the specified location
    //abstract GetNavigationItems             : unit -> NavigationItems
    /// Return the inner-most range associated with a possible breakpoint location
    //abstract ValidateBreakpointLocation : Position -> Range option
    /// When these files change then the build is invalid
    //abstract DependencyFiles : unit -> string list


  type Severity = Warning | Error

  type Declaration(wrapped:obj) =
    member x.Name : string = wrapped?Name
    member x.DescriptionText : DataTipText = DataTipText(wrapped?DescriptionText)
    member x.Glyph : int = wrapped?Glyph

  type DeclarationSet(wrapped:obj) =
    member x.Items = 
      wrapped?Items |> Array.untypedMap (fun o -> Declaration(o))

  type TypeCheckInfo(wrapped:obj) =
    /// Resolve the names at the given location to a set of declarations
    member x.GetDeclarations(pos:Position, line:string, names:NamesWithResidue, tokentag:int) =
      DeclarationSet(wrapped?GetDeclarations(pos, line, names, tokentag))
      
    /// Resolve the names at the given location to give a data tip 
    member x.GetDataTipText(pos:Position, line:string, names:Names, tokentag:int) : DataTipText =
      DataTipText(wrapped?GetDataTipText(pos, line, convertList names FSharpCompiler.ReferencedFSharpCore, tokentag))
      
    /// Resolve the names at the given location to give F1 keyword
    // member GetF1Keyword : Position * string * Names -> string option
    // Resolve the names at the given location to a set of methods
    // member GetMethods : Position * string * Names option * (*tokentag:*)int -> MethodOverloads
    /// Resolve the names at the given location to the declaration location of the corresponding construct
    // member GetDeclarationLocation : Position * string * Names * (*tokentag:*)int * bool -> FindDeclResult
    /// A version of `GetDeclarationLocation` augmented with the option (via the `bool`) parameter to force .fsi generation (even if source exists); this is primarily for testing
    // member GetDeclarationLocationInternal : bool -> Position * string * Names * (*tokentag:*)int * bool -> FindDeclResult


  type ErrorInfo(wrapped:obj) =
    member x.StartLine : int = wrapped?StartLine
    member x.EndLine : int = wrapped?EndLine
    member x.StartColumn : int = wrapped?StartColumn
    member x.EndColumn : int = wrapped?EndColumn
    member x.Severity : Severity = 
      if wrapped?Severity?IsError then Error else Warning
    member x.Message : string = wrapped?Message
    member x.Subcategory : string = wrapped?Subcategory
  
  /// A handle to the results of TypeCheckSource
  type TypeCheckResults(wrapped:obj) =
    /// The errors returned by parsing a source file
    member x.Errors : ErrorInfo array = 
      wrapped?Errors |> Array.untypedMap (fun e -> ErrorInfo(e))
      
    /// A handle to type information gleaned from typechecking the file. 
    member x.TypeCheckInfo : TypeCheckInfo option = 
      if wrapped?TypeCheckInfo = null then None 
      else Some(TypeCheckInfo(wrapped?TypeCheckInfo?Value))

  type TypeCheckAnswer(wrapped:obj) =
    member x.Wrapped = wrapped

  let (|NoAntecedant|Aborted|TypeCheckSucceeded|) (tc:TypeCheckAnswer) = 
    if tc.Wrapped?IsNoAntecedant then NoAntecedant() 
    elif tc.Wrapped?IsAborted then Aborted() 
    elif tc.Wrapped?IsTypeCheckSucceeded then 
      TypeCheckSucceeded(TypeCheckResults(tc.Wrapped?Item))
    else failwith "Unexpected TypeCheckAnswer value"    
  
  type TypeCheckSucceededImpl(tyres:TypeCheckResults) =
    member x.IsTypeCheckSucceeded = true
    member x.IsAborted = false
    member x.IsNoAntecedant = false
    member x.Item = tyres
    
  let TypeCheckSucceeded arg = 
    TypeCheckAnswer(TypeCheckSucceededImpl(arg))
    
  type InteractiveChecker(wrapped:obj) =
      /// Crate an instance of the wrapper
      static member Create (dirty:FileTypeCheckStateIsDirty) =
        InteractiveChecker(FSharpCompiler.InteractiveChecker?Create(convertFunction dirty FSharpCompiler.ReferencedFSharpCore))
        
      /// Parse a source code file, returning a handle that can be used for obtaining navigation bar information
      /// To get the full information, call 'TypeCheckSource' method on the result
      member x.UntypedParse(filename:string, source:string, options:CheckOptions) : UntypedParseInfo =
        UntypedParseInfo(wrapped?UntypedParse(filename, source, options.Wrapped))

      /// Typecheck a source code file, returning a handle to the results of the parse including
      /// the reconstructed types in the file.
      ///
      /// Return None if the background builder is not yet done prepring the type check results for the antecedent to the 
      /// file.
      member x.TypeCheckSource
          ( parsed:UntypedParseInfo, filename:string, fileversion:int, 
            source:string, options:CheckOptions, (IsResultObsolete f)) =
        TypeCheckAnswer
          ( wrapped?TypeCheckSource
              ( parsed.Wrapped, filename, fileversion, source, options.Wrapped, 
                FSharpCompiler.IsResultObsolete?NewIsResultObsolete(convertFunction f FSharpCompiler.ReferencedFSharpCore) ) : obj)
      
      /// For a given script file, get the CheckOptions implied by the #load closure
      member x.GetCheckOptionsFromScriptRoot(filename:string, source:string) : CheckOptions =
        CheckOptions(wrapped?GetCheckOptionsFromScriptRoot(filename, source))
          

      /// Try to get recent type check results for a file. This may arbitrarily refuse to return any
      /// results if the InteractiveChecker would like a chance to recheck the file, in which case
      /// UntypedParse and TypeCheckSource should be called. If the source of the file
      /// has changed the results returned by this function may be out of date, though may
      /// still be usable for generating intellsense menus and information.
      member x.TryGetRecentTypeCheckResultsForFile(filename:string, options:CheckOptions) =
        let res = wrapped?TryGetRecentTypeCheckResultsForFile(filename, options.Wrapped) : obj
        if res = null then None else
          let tuple = res?Value
          Some(UntypedParseInfo(tuple?Item1), TypeCheckResults(tuple?Item2), int tuple?Item3)

      /// Begin background parsing the given project.
      member x.StartBackgroundCompile(options:CheckOptions) =
        wrapped?StartBackgroundCompile(options.Wrapped)

      // Members that are not supported by the wrapper
      
      /// Parse a source code file, returning information about brace matching in the file
      /// Return an enumeration of the matching parethetical tokens in the file
      // member MatchBraces : filename : string * source: string * options: CheckOptions -> (Range * Range) array
                
      /// This function is called when the configuration is known to have changed for reasons not encoded in the CheckOptions.
      /// For example, dependent references may have been deleted or created.
      // member InvalidateConfiguration : options : CheckOptions -> unit    

      /// Stop the background compile.
      // member StopBackgroundCompile : unit -> unit
      /// Block until the background compile finishes.
      // member WaitForBackgroundCompile : unit -> unit
      
      /// Report a statistic for testability
      // static member GlobalForegroundParseCountStatistic : int

      /// Report a statistic for testability
      // static member GlobalForegroundTypeCheckCountStatistic : int

      // member GetSlotsCount : options : CheckOptions -> int
      // member UntypedParseForSlot : slot:int * options : CheckOptions -> UntypedParseInfo

module Utils = 
  open Reflection

  /// Format an exception as a readable string with all information
  /// (this also handles exceptions thrown by the F# language service)
  let formatException e = 
    let sb = new Text.StringBuilder()
    let rec printe s (e:exn) = 
      let name = e.GetType().FullName
      Printf.bprintf sb "%s: %s (%s)\n\nStack trace: %s\n\n" s name e.Message e.StackTrace
      if name = "Microsoft.FSharp.Compiler.ErrorLogger+Error" then
        let (tup:obj) = e?Data0 
        Printf.bprintf sb "Compile error (%d): %s" tup?Item1 tup?Item2
      elif name = "Microsoft.FSharp.Compiler.ErrorLogger+ReportedError" then
        let (inner:obj) = e?Data0 
        if inner = null then Printf.bprintf sb "Reported error is null"
        else printe "Reported error" (inner?Value)
      elif e.InnerException <> null then
        printe "Inner exception" e.InnerException
    printe "Exception" e
    sb.ToString()
