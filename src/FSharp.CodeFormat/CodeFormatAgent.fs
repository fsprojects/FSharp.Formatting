// --------------------------------------------------------------------------------------
// F# CodeFormat (CodeFormatAgent.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
namespace FSharp.CodeFormat

open System
open System.IO
open System.Runtime.ExceptionServices
open FSharp.Compiler
open FSharp.Compiler.Range
open FSharp.Compiler.Text
open FSharp.Compiler.SourceCodeServices
open FSharp.CodeFormat
open FSharp.CodeFormat.CommentFilter
open FSharp.Formatting.Common
open Yaaf.FSharp.Scripting
// --------------------------------------------------------------------------------------
// ?
// --------------------------------------------------------------------------------------

module private Helpers =

  let inline ediRaise (e : exn) : 'T = ExceptionDispatchInfo.Capture(e).Throw(); Unchecked.defaultof<_>

  /// Mapping table that translates F# compiler representation to our union
  let getTokenKind = function
  | FSharpTokenColorKind.Comment             -> TokenKind.Comment
  | FSharpTokenColorKind.Identifier          -> TokenKind.Identifier
  | FSharpTokenColorKind.InactiveCode        -> TokenKind.Inactive
  | FSharpTokenColorKind.Keyword             -> TokenKind.Keyword
  | FSharpTokenColorKind.Number              -> TokenKind.Number
  | FSharpTokenColorKind.Operator            -> TokenKind.Operator
  | FSharpTokenColorKind.Punctuation         -> TokenKind.Punctuation
  | FSharpTokenColorKind.PreprocessorKeyword -> TokenKind.Preprocessor
  | FSharpTokenColorKind.String              -> TokenKind.String
  | FSharpTokenColorKind.UpperIdentifier     -> TokenKind.Identifier
  | FSharpTokenColorKind.Text
  | FSharpTokenColorKind.Default | _         -> TokenKind.Default

  // Parse command line options - split string by space, but if there is something
  // enclosed in double quotes "..." then ignore spaces in the quoted text
  let parseOptions (str:string) =
    let rec loop i opts current =
      let opts =
        if i < str.Length && str.[i] <> ' ' then opts
        else (System.String(current |> List.rev |> Array.ofSeq))::opts
      if i = str.Length then opts
      elif str.[i] = ' ' then loop (i+1) opts []
      elif str.[i] = '"' then
        let endp = str.IndexOf('"', i+1)
        let chars = str.Substring(i+1, endp - i - 1) |> List.ofSeq |> List.rev
        loop (endp + 1) opts (chars @ current)
      else loop (i + 1) opts (str.[i] :: current)

    loop 0 [] [] |> Array.ofSeq


  /// Use the F# compiler's SourceTokenizer to split a snippet (array of strings)
  /// into a snippet with token information and line numbers.
  let getTokens file defines (lines:string[]) : Snippet =

    // Get defined directives
    let defines = defines |> Option.map (fun (s:string) ->
      s.Split([| ' '; ';'; ',' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofSeq)
    // Create source tokenizer
    let sourceTok =   FSharpSourceTokenizer(defaultArg defines [], file)

    // Parse lines using the tokenizer
    let indexedSnippetLines =
        [   let state = ref FSharpTokenizerLexState.Initial
            for n, line in lines |> Seq.zip [ 0 .. lines.Length ] do
                let tokenizer = sourceTok.CreateLineTokenizer(line)
                let rec parseLine() = seq {
                    match tokenizer.ScanToken(!state) with
                    | Some(tok), nstate ->
                        let str = line.Substring(tok.LeftColumn, tok.RightColumn - tok.LeftColumn + 1)
                        yield str, tok
                        state := nstate
                        yield! parseLine()
                    | None, nstate -> state := nstate
                }
                yield { StartLine = n; StartColumn = 0; EndLine = n; EndColumn = 0 }, parseLine() |> List.ofSeq
        ]
    indexedSnippetLines

  // Count the minimal number of spaces at the beginning of lines
  // (so that we can remove spaces for indented text)
  let countStartingSpaces (lines:Snippet) =
    [ for _, (toks:_ list) in lines do
        match toks with
        | ((text:string), info)::_ when info.TokenName = "WHITESPACE" ->
            yield text.Length - text.TrimStart([| ' ' |]).Length
        | [] -> ()
        | _ -> yield 0
    ] |> Seq.fold min 0

// --------------------------------------------------------------------------------------
// Main type that implements parsing and uses F# services
// --------------------------------------------------------------------------------------



type [<Struct>] Range = {
    LeftCol : int
    RightCol : int
} with
  static member Create leftCol rightCol =
    { LeftCol = leftCol; RightCol = rightCol }

/// Uses agent to handle formatting requests
type CodeFormatAgent() =
    // Create keys for query tooltips for double-backtick identifiers
    let processDoubleBackticks (body : string) =
        if body.StartsWith "``" then
            sprintf "( %s )" <| body.Trim '`'
        else body

    let categoryToTokenKind = function
    | SemanticClassificationType.Enumeration -> Some TokenKind.Enumeration
    | SemanticClassificationType.Function -> Some TokenKind.Function
    | SemanticClassificationType.Interface -> Some TokenKind.Interface
    | SemanticClassificationType.Module -> Some TokenKind.Module
    | SemanticClassificationType.MutableVar -> Some TokenKind.MutableVar
    | SemanticClassificationType.Printf -> Some TokenKind.Printf
    | SemanticClassificationType.Property -> Some TokenKind.Property
    | SemanticClassificationType.ReferenceType -> Some TokenKind.ReferenceType
    | SemanticClassificationType.UnionCase -> Some TokenKind.UnionCase
    | SemanticClassificationType.ValueType  -> Some TokenKind.ValueType
    | SemanticClassificationType.Disposable -> Some TokenKind.Disposable
    | SemanticClassificationType.ComputationExpression -> Some TokenKind.Keyword
    | SemanticClassificationType.TypeArgument -> Some TokenKind.TypeArgument
    | SemanticClassificationType.Operator -> Some TokenKind.Operator
    | SemanticClassificationType.IntrinsicFunction -> Some TokenKind.Keyword


    // Processes a single line of the snippet
    let processSnippetLine (checkResults: FSharpCheckFileResults) (semanticRanges: struct(Range.range * SemanticClassificationType)[])
                            (lines: string[]) (line: int, lineTokens: SnippetLine) =
        let lineStr = lines.[line]

        // Recursive processing of tokens on the line (keeps a long identifier 'island')
        let rec loop island (tokens: SnippetLine) (stringRange: Range option) = seq {
            match tokens with
            | [] -> ()
            | (body, token)::rest when token.ColorClass = FSharpTokenColorKind.Keyword ->
                yield FSharp.CodeFormat.Token (TokenKind.Keyword, body, None)
                yield! loop [] rest None
            | (body, token) :: rest ->
            let stringRange, completedStringRange, rest =
                match rest with
                // it's the last token in the string
                | [] ->
                    match token.ColorClass, stringRange with
                    | FSharpTokenColorKind.String, None ->
                        None, Some (Range.Create token.LeftColumn token.RightColumn), rest
                    | FSharpTokenColorKind.String, Some range ->
                        None, Some { range with RightCol = token.RightColumn }, rest
                    | _, Some range -> None, Some range, tokens
                    | _ -> None, None, rest
                | _ ->
                    match token.ColorClass, stringRange with
                    | FSharpTokenColorKind.String, None ->
                        Some (Range.Create token.LeftColumn token.RightColumn), None, rest
                    | FSharpTokenColorKind.String, Some range ->
                        Some { range with RightCol = token.RightColumn }, None, rest
                    | _, Some range -> None, Some range, tokens
                    | _ -> None, None, rest

            match stringRange, completedStringRange with
            | None, None ->
              // Update the current identifier island (long identifier e.g. Collections.List.map)
                let island =
                    match token.TokenName with
                    | "DOT" -> island         // keep what we have found so far
                    | "IDENT" -> processDoubleBackticks body::island  // add current identifier
                    | _ -> []                 // drop everything - not in island
                // Find tootltip using F# compiler service & the identifier island
                let tip =
                    // If we're processing an identfier, see if it has any tool tip
                    if (token.TokenName = "IDENT") then
                        let island = List.rev island
                        let tip = checkResults.GetToolTipText(line + 1, token.LeftColumn + 1, lines.[line], island,FSharpTokenTag.IDENT)
                        match Async.RunSynchronously tip |> fun (tooltip) ->
                            //tooltip.
                            ToolTipReader.tryFormatTip tooltip with
                        | Some(_) as res -> res
                        | _ -> None
                    else None

                if token.TokenName.StartsWith("OMIT") then
                // Special OMIT tag - add tool tip stored in token name
                // (The text immediately follows the keyword "OMIT")
                    yield Omitted(body, token.TokenName.Substring(4))
                elif token.TokenName = "FSI" then
                // F# Interactive output - return as Output token
                    yield Output(body)
                else
                    match tip with
                    | Some (Literal msg::_) when msg.StartsWith("custom operation:") ->
                        // If the tool-tip says this is a custom operation, then
                        // we want to treat it as keyword (not sure if there is a better
                        // way to detect this, but Visual Studio also colors these later)
                        yield FSharp.CodeFormat.Token(TokenKind.Keyword, body, tip)
                    | _ ->
                    let kind =
                        semanticRanges
                        |> Array.tryFind (fun struct(range,_) -> range.StartColumn  = token.LeftColumn)
                        |> Option.bind (fun struct(_,category) -> categoryToTokenKind category)
                        |> Option.defaultValue (Helpers.getTokenKind token.ColorClass)
                    yield FSharp.CodeFormat.Token (kind, body, tip)
                // Process the rest of the line
                yield! loop island rest stringRange
            | Some _x, None -> yield! loop island rest stringRange

            | _x, Some { LeftCol = strLeftCol; RightCol = strRightCol } ->
              let printfOrEscapedSpans =
                  semanticRanges
                  |> Array.filter (fun struct(range,category) ->
                      (category = SemanticClassificationType.Printf) &&
                      range.StartColumn >= strLeftCol &&
                      range.EndColumn <= strRightCol)

              match printfOrEscapedSpans with
              | [||] -> yield FSharp.CodeFormat.Token (TokenKind.String, lineStr.[strLeftCol..strRightCol], None)
              | spans ->
                  let data =
                    spans
                    |> Array.fold (fun points struct(range,category) ->
                        points
                        |> Set.add range.StartColumn
                        |> Set.add (range.EndColumn - 1)) Set.empty
                    |> Set.add (strLeftCol - 1)
                    |> Set.add (strRightCol + 1)
                    |> Set.toSeq
                    |> Seq.pairwise
                    |> Seq.map (fun (leftPoint, rightPoint) ->
                        printfOrEscapedSpans
                        |> Array.tryFind (fun struct(range,category) -> range.StartColumn = leftPoint)
                        |> Option.bind (fun struct(range,category)->
                             categoryToTokenKind category
                             |> Option.map (fun kind -> range.StartColumn, range.EndColumn, kind))
                        |> Option.defaultValue (leftPoint+1, rightPoint, TokenKind.String))

                  for leftPoint, rightPoint, kind in data do
                    yield FSharp.CodeFormat.Token (kind, lineStr.[leftPoint..rightPoint-1], None)
              // Process the rest of the line
              yield! loop island rest stringRange
        }

        // Process the current line & return info about it
        Line (loop [] (List.ofSeq lineTokens) None |> List.ofSeq)

    /// Process snippet
    let processSnippet checkResults categorizedRanges lines (snippet: Snippet) =
        snippet
        |> List.map (fun snippetLine ->

            processSnippetLine
                checkResults
                (categorizedRanges
                 |> Map.tryFind ((fst snippetLine).StartLine + 1)
                 |> function None -> [||] | Some spans -> Array.ofSeq spans)
                lines
                ((fst snippetLine).StartLine, snd snippetLine))

    // --------------------------------------------------------------------------------------

    // Create an instance of an InteractiveChecker (which does background analysis
    // in a typical IntelliSense editor integration for F#)
    let fsChecker = FSharpAssemblyHelper.checker // FSharpChecker.Create()

    // ------------------------------------------------------------------------------------

    let processSourceCode (filePath, source, options, defines) = async {
        Log.verbf "starting to process source code from '%s'" filePath
        // Read the source code into an array of lines
        use reader = new StringReader(source)
        let sourceLines = [|
            let line = ref ""
            while (line := reader.ReadLine(); line.Value <> null) do
                yield line.Value
        |]
        // Get options for a standalone script file (this adds some
        // default references and doesn't require full project information)
        let frameworkVersion = FSharpAssemblyHelper.defaultFrameworkVersion
        let fsiOptions = (Option.map (Helpers.parseOptions >>  FsiOptions.ofArgs) options) |> Option.defaultValue FsiOptions.Empty

        let fsCore = FSharpAssemblyHelper.findFSCore [] fsiOptions.LibDirs
        let defaultReferences = Seq.empty

        let projFileName, args = FSharpAssemblyHelper.getCheckerArguments frameworkVersion defaultReferences None [] [] []
        // filter invalid args
        let refCorLib = args |> Seq.tryFind (fun i -> i.EndsWith "mscorlib.dll") |> Option.defaultValue "-r:netstandard.dll"
        let args =
            args |> Array.filter (fun item ->
                not <| item.StartsWith "--target" &&
                not <| item.StartsWith "--doc" &&
                not <| item.StartsWith "--out" &&
                not <| item.StartsWith "--nooptimizationdata" &&
                not <| item.EndsWith "mscorlib.dll")
        Log.verbf "getting project options ('%s', \"\"\"%s\"\"\", now, args, assumeDotNetFramework = false): \n\t%s" filePath source (System.String.Join("\n\t", args))// fscore
        let! (opts,_errors) = fsChecker.GetProjectOptionsFromScript(filePath, SourceText.ofString source, loadedTimeStamp = DateTime.Now, otherFlags = args, assumeDotNetFramework = false)
        let formatError (e:FSharpErrorInfo) =
             sprintf "%s (%d,%d)-(%d,%d): %A FS%04d: %s" e.FileName e.StartLineAlternate e.StartColumn e.EndLineAlternate e.EndColumn e.Severity e.ErrorNumber e.Message
        let formatErrors errors =
            System.String.Join("\n", errors |> Seq.map formatError)
        // filter duplicates
        let opts =
            let mutable known = Set.empty
            { opts with
                OtherOptions =
                    [|
                        yield sprintf "-r:%s" fsCore
                        yield refCorLib
                        if Env.isNetCoreApp then yield "--targetprofile:netcore"

                        yield! opts.OtherOptions
                    |]
                    |> Array.filter (fun item ->
                        if item.StartsWith "-r:" then
                            let fullPath = item.Substring 3
                            let name = System.IO.Path.GetFileName fullPath
                            if known.Contains name then
                                false
                            else
                                known <- known.Add name
                                true
                        else
                            if known.Contains item then
                                false
                            else
                                known <- known.Add item
                                true)
                }
        // Override default options if the user specified something
        let opts =
            match options with
            | Some(str:string) when not(System.String.IsNullOrEmpty(str)) ->
                { opts with OtherOptions = [| yield! Helpers.parseOptions str; yield! opts.OtherOptions |] }
            | _ -> opts
        //// add our file
        //let opts =
        //    { opts with
        //        UseScriptResolutionRules = true
        //        //UnresolvedReferences = Some ( UnresolvedReferencesSet.UnresolvedAssemblyReference [])
        //        ProjectFileNames = [| filePath |] }

        Log.verbf "project options '%A', OtherOptions: \n\t%s" { opts with OtherOptions = [||] } (System.String.Join("\n\t", opts.OtherOptions))
        //let! results = fsChecker.ParseAndCheckProject(opts)
        //let _errors = results.Errors
        
        if _errors |> List.filter (fun e -> e.Severity = FSharpErrorSeverity.Error) |> List.length > 0 then
            Log.warnf "errors from GetProjectOptionsFromScript '%s'" (formatErrors _errors)

        // Run the second phase - perform type checking
        Log.verbf "starting to ParseAndCheckDocument from '%s'" filePath
        let! res = fsChecker.ParseAndCheckDocument(filePath, source,opts,false)
        //fsChecker.InvalidateConfiguration(opts)
        //results.
        match res with
        | Some (_parseResults, parsedInput, checkResults) ->
            Log.verbf "starting to GetAllUsesOfAllSymbolsInFile from '%s'" filePath
            let! symbolUses = checkResults.GetAllUsesOfAllSymbolsInFile ()
            let errors = checkResults.Errors
            let classifications =
                checkResults.GetSemanticClassification (Some parsedInput.Range)
                |> Seq.groupBy (fun struct(r,c) -> r.StartLine)
                |> Map.ofSeq


            /// Parse source file into a list of lines consisting of tokens
            let tokens = Helpers.getTokens (Some filePath) defines sourceLines

            // --------------------------------------------------------------------------------
            // When type-checking completes and we have a parsed file (as tokens), we can
            // put the information together - this processes tokens and adds information such
            // as color and tool tips (for identifiers)

            // Process "omit" meta-comments in the source
            let source = shrinkOmittedParts tokens |> List.ofSeq

            // Split source into snippets if it contains meta-comments
            let snippets : NamedSnippet list =
                match getSnippets None [] source sourceLines with
                | [] -> [null, source]
                | snippets -> snippets |> List.rev

            // Generate a list of snippets
            let parsedSnippets =
                snippets |> List.map (fun (title, lines) ->
                    if lines.Length = 0 then
                        // Skip empty snippets
                        Snippet(title, [])
                    else
                    // Process the current snippet
                    let parsed = processSnippet checkResults classifications sourceLines lines

                    // Remove additional whitespace from start of lines
                    let spaces = Helpers.countStartingSpaces lines
                    let parsed =  parsed |> List.map (function
                        | Line ((Token(kind, body, tip))::rest) ->
                            let body = body.Substring(spaces)
                            Line ((Token(kind, body, tip))::rest)
                        | line -> line
                        )
                    // Return parsed snippet as 'Snippet' value
                    Snippet(title, parsed)
                )

            let sourceErrors = [|
                for errInfo in errors do
                    if errInfo.Message <> "Multiple references to 'mscorlib.dll' are not permitted" then
                        yield SourceError(
                            (errInfo.StartLineAlternate - 1, errInfo.StartColumn),
                            (errInfo.EndLineAlternate - 1, errInfo.EndColumn),
                            (if errInfo.Severity = FSharpErrorSeverity.Error then ErrorKind.Error else ErrorKind.Warning),
                            errInfo.Message
                        )
            |]
            return Some (parsedSnippets, sourceErrors)
        | None ->
            // TODO: Return something better than None, we probably should return the info in _errors somehow.
            return None
    }

      // ------------------------------------------------------------------------------------
      // Agent that implements the parsing & formatting

    let agent = MailboxProcessor.Start (fun agent -> async {
        while true do
          // Receive parameters for the next parsing request
          let! request, (chnl:AsyncReplyChannel<_>) = agent.Receive()
          try
            let! result = processSourceCode request
            match result with
            | Some (res,errs) ->
                chnl.Reply(Choice1Of2(res |> Array.ofList, errs))
            | None ->
                chnl.Reply(Choice2Of2(exn "No result from source code processing")) // new Exception(Utilities.formatException e, e)))
          with e ->
            chnl.Reply(Choice2Of2(e)) // new Exception(Utilities.formatException e, e)))
        })

    /// Parse the source code specified by 'source', assuming that it
    /// is located in a specified 'file'. Optional arguments can be used
    /// to give compiler command line options and preprocessor definitions
    member __.AsyncParseSource(file, source, ?options, ?defines) = async {
        let! res = agent.PostAndAsyncReply(fun chnl -> (file, source, options, defines), chnl)
        match res with
        | Choice1Of2 res -> return res
        | Choice2Of2 exn -> return Helpers.ediRaise exn }

    /// Parse the source code specified by 'source', assuming that it
    /// is located in a specified 'file'. Optional arguments can be used
    /// to give compiler command line options and preprocessor definitions
    member x.ParseSourceAsync(file, source, options, defines) =
        x.AsyncParseSource(file, source, options, defines)
        |> Async.StartAsTask

    /// Parse the source code specified by 'source', assuming that it
    /// is located in a specified 'file'. Optional arguments can be used
    /// to give compiler command line options and preprocessor definitions
    member __.ParseSource(file, source, ?options, ?defines) =
        let res = agent.PostAndReply(fun chnl -> (file, source, options, defines), chnl)
        match res with
        | Choice1Of2 res -> res
        | Choice2Of2 exn -> Helpers.ediRaise exn
