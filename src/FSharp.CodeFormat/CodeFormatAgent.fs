// --------------------------------------------------------------------------------------
// F# CodeFormat (CodeFormatAgent.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
namespace FSharp.CodeFormat

open System
open System.IO
open FSharp.CodeFormat
open FSharp.CodeFormat.CommentFilter
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

// --------------------------------------------------------------------------------------
// ?
// --------------------------------------------------------------------------------------

module private Helpers = 
  
  /// Mapping table that translates F# compiler representation to our union
  let private colorMap =
    [ FSharpTokenColorKind.Comment, TokenKind.Comment
      FSharpTokenColorKind.Identifier, TokenKind.Identifier
      FSharpTokenColorKind.InactiveCode, TokenKind.Inactive
      FSharpTokenColorKind.Keyword, TokenKind.Keyword
      FSharpTokenColorKind.Number, TokenKind.Number
      FSharpTokenColorKind.Operator, TokenKind.Operator
      FSharpTokenColorKind.PreprocessorKeyword, TokenKind.Preprocessor
      FSharpTokenColorKind.String, TokenKind.String
      FSharpTokenColorKind.UpperIdentifier, TokenKind.Identifier ] |> Map.ofSeq

  /// Return the TokenKind corresponding to the specified F# compiler token
  let getTokenKind key = 
    defaultArg (Map.tryFind key colorMap) TokenKind.Default

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
    let sourceTok = SourceTokenizer(defaultArg defines [], file)

    // Parse lines using the tokenizer
    [ let state = ref 0L
      for n, line in lines |> Seq.zip [ 0 .. lines.Length ] do
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        let rec parseLine() = seq {
          match tokenizer.ScanToken(!state) with
          | Some(tok), nstate ->
              let str = line.Substring(tok.LeftColumn, tok.RightColumn - tok.LeftColumn + 1)
              yield str, tok
              state := nstate
              yield! parseLine()
          | None, nstate -> state := nstate }
        yield n, parseLine() |> List.ofSeq ]

  // Count the minimal number of spaces at the beginning of lines
  // (so that we can remove spaces for indented text)
  let countStartingSpaces (lines:Snippet) = 
    [ for _, (toks:_ list) in lines do
        match toks with
        | ((text:string), info)::_ when info.TokenName = "WHITESPACE" ->
            yield text.Length - text.TrimStart([| ' ' |]).Length
        | [] -> ()
        | _ -> yield 0 ] |> Seq.fold min 0

// --------------------------------------------------------------------------------------
// Main type that implements parsing and uses F# services
// --------------------------------------------------------------------------------------

open FSharpVSPowerTools


type Range = 
  { LeftCol : int
    RightCol : int }
  static member Create leftCol rightCol = 
    { LeftCol = leftCol; RightCol = rightCol }

/// Uses agent to handle formatting requests
type CodeFormatAgent() = 
  // Create keys for query tooltips for double-backtick identifiers
  let processDoubleBackticks (body : string) = 
    if body.StartsWith "``" then
      sprintf "( %s )" <| body.Trim('`')
    else body

  let categoryToTokenKind = function
    | Category.ReferenceType 
    | Category.ValueType
    | Category.Module -> Some TokenKind.TypeOrModule
    | Category.Function -> Some TokenKind.Function
    | Category.PatternCase -> Some TokenKind.Pattern
    | Category.MutableVar -> Some TokenKind.MutableVar
    | Category.Printf -> Some TokenKind.Printf
    | Category.Escaped -> Some TokenKind.Escaped 
    | _ -> None

  // Processes a single line of the snippet
  let processSnippetLine (checkResults: ParseAndCheckResults) (spans: CategorizedColumnSpan<_> list) 
                         (lines: string[]) (line: int, lineTokens: SnippetLine) =
    let lineStr = lines.[line]

    // Recursive processing of tokens on the line (keeps a long identifier 'island')
    let rec loop island (tokens: SnippetLine) (stringRange: Range option) = seq {
      match tokens with 
      | [] -> ()
      | (body, token) :: rest -> 
        let stringRange, completedStringRange, rest =
            match rest with
            // it's the last token in the string
            | [] ->
              match token.ColorClass, stringRange with
              | FSharpTokenColorKind.String, None -> 
                  None, Some (Range.Create token.LeftColumn token.RightColumn), rest
              | FSharpTokenColorKind.String, Some range -> None, Some { range with RightCol = token.RightColumn }, rest
              | _, Some range -> None, Some range, tokens
              | _ -> None, None, rest
            | _ ->
              match token.ColorClass, stringRange with
              | FSharpTokenColorKind.String, None -> Some (Range.Create token.LeftColumn token.RightColumn), None, rest
              | FSharpTokenColorKind.String, Some range -> Some { range with RightCol = token.RightColumn }, None, rest
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
              let tip = checkResults.GetIdentTooltip(line + 1, token.LeftColumn + 1, lines.[line], island)
              match Async.RunSynchronously tip |> Option.bind ToolTipReader.tryFormatTip with 
              | Some(_) as res -> res
              | _ when island.Length > 1 ->
                  // Try to find some information about the last part of the identifier 
                  let tip = checkResults.GetIdentTooltip(line + 1, token.LeftColumn + 2, lines.[line], [ processDoubleBackticks body ])
                  Async.RunSynchronously tip |> Option.bind ToolTipReader.tryFormatTip
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
                yield Token(TokenKind.Keyword, body, tip)
            | _ -> 
              let kind = 
                spans
                |> List.tryFind (fun span -> span.WordSpan.StartCol = token.LeftColumn)
                |> Option.bind (fun span -> categoryToTokenKind span.Category)
                |> Option.getOrElse (Helpers.getTokenKind token.ColorClass)
              yield Token (kind, body, tip)
          // Process the rest of the line
          yield! loop island rest stringRange

        | Some _, None -> yield! loop island rest stringRange

        | _, Some { LeftCol = strLeftCol; RightCol = strRightCol } ->
          let printfOrEscapedSpans = 
              spans 
              |> List.filter (fun span -> 
                  (span.Category = Category.Escaped || span.Category = Category.Printf) &&
                  span.WordSpan.StartCol >= strLeftCol &&
                  span.WordSpan.EndCol <= strRightCol)

          match printfOrEscapedSpans with
          | [] -> yield Token (TokenKind.String, lineStr.[strLeftCol..strRightCol], None)
          | spans ->
              let data =
                spans
                |> List.fold (fun points span ->
                    points 
                    |> Set.add span.WordSpan.StartCol
                    |> Set.add (span.WordSpan.EndCol - 1)) Set.empty
                |> Set.add (strLeftCol - 1)
                |> Set.add (strRightCol + 1)
                |> Set.toSeq
                |> Seq.pairwise
                |> Seq.map (fun (leftPoint, rightPoint) ->
                    printfOrEscapedSpans 
                    |> List.tryFind (fun span -> span.WordSpan.StartCol = leftPoint) 
                    |> Option.bind (fun span ->
                         categoryToTokenKind span.Category
                         |> Option.map (fun kind -> span.WordSpan.StartCol, span.WordSpan.EndCol, kind))
                    |> Option.getOrElse (leftPoint+1, rightPoint, TokenKind.String))
              
              for leftPoint, rightPoint, kind in data do
                yield Token (kind, lineStr.[leftPoint..rightPoint-1], None)
          // Process the rest of the line
          yield! loop island rest stringRange }

    // Process the current line & return info about it
    Line (loop [] (List.ofSeq lineTokens) None |> List.ofSeq)

  /// Process snippet
  let processSnippet checkResults categorizedSpans lines (snippet: Snippet) =
    snippet 
    |> List.map (fun snippetLine ->
        processSnippetLine 
            checkResults 
            (categorizedSpans 
             |> Map.tryFind ((fst snippetLine) + 1) 
             |> function None -> [] | Some spans -> List.ofSeq spans) 
            lines 
            snippetLine)

// --------------------------------------------------------------------------------------

  // Create an instance of an InteractiveChecker (which does background analysis
  // in a typical IntelliSense editor integration for F#)
  let languageService = LanguageService() 

  /// Type-checking takes some time and doesn't return information on the
  /// first call, so this function creates workflow that tries repeatedly
  let getTypeCheckInfo(file, source, opts) = async {
      let! checkResults = languageService.ParseAndCheckFileInProject(opts, file, source, AllowStaleResults.No)
      let! symbolUses = languageService.GetAllUsesOfAllSymbolsInFile (opts, file, source, AllowStaleResults.No, false, new Profiler())
      return checkResults, symbolUses
  }
   
  // ------------------------------------------------------------------------------------

  let processSourceCode (file, source, options, defines) = async {

    // Read the source code into an array of lines
    use reader = new StringReader(source)
    let sourceLines = 
      [| let line = ref ""
         while (line := reader.ReadLine(); line.Value <> null) do
           yield line.Value |]

    // Get options for a standalone script file (this adds some 
    // default references and doesn't require full project information)
    let! opts = languageService.RawChecker.GetProjectOptionsFromScript(file, source, DateTime.Now)
    
    // Override default options if the user specified something
    let opts = 
      match options with 
      | Some(str:string) when not(String.IsNullOrEmpty(str)) -> 
          { opts with OtherOptions = Helpers.parseOptions str }
      | _ -> opts

    // Run the second phase - perform type checking
    let checkResults, symbolUses = getTypeCheckInfo(file, source, opts) |> Async.RunSynchronously
    let errors = checkResults.GetErrors()

    let lexer = 
        { new LexerBase() with
            member __.GetSymbolFromTokensAtLocation (_, line, col) =
                let lineStr = sourceLines.[line]
                Lexer.getSymbol source line col lineStr SymbolLookupKind.ByRightColumn opts.OtherOptions Lexer.queryLexState
            member __.TokenizeLine line =
                let lineStr = sourceLines.[line]
                Lexer.tokenizeLine source opts.OtherOptions line lineStr Lexer.queryLexState
            member __.LineCount = sourceLines.Length } 

    let categorizedSpans = 
        SourceCodeClassifier.getCategoriesAndLocations(
            symbolUses, checkResults, lexer, (fun line -> sourceLines.[line]), [], None)
        |> Seq.groupBy (fun span -> span.WordSpan.Line)
        |> Map.ofSeq

    /// Parse source file into a list of lines consisting of tokens 
    let tokens = Helpers.getTokens file defines sourceLines

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
          let parsed = processSnippet checkResults categorizedSpans sourceLines lines

          // Remove additional whitespace from start of lines
          let spaces = Helpers.countStartingSpaces lines
          let parsed =  parsed |> List.map (function
            | Line ((Token(kind, body, tip))::rest) ->
                let body = body.Substring(spaces)
                Line ((Token(kind, body, tip))::rest)
            | line -> line)
          
          // Return parsed snippet as 'Snippet' value
          Snippet(title, parsed))
  
    let sourceErrors = 
      match errors with
      | Some errors ->
          [| for errInfo in errors do
              if errInfo.Message <> "Multiple references to 'mscorlib.dll' are not permitted" then
               yield 
                 SourceError
                   ( (errInfo.StartLineAlternate - 1, errInfo.StartColumn), (errInfo.EndLineAlternate - 1, errInfo.EndColumn),
                     (if errInfo.Severity = FSharpErrorSeverity.Error then ErrorKind.Error else ErrorKind.Warning),
                     errInfo.Message ) |]
      | None -> [||]
    return parsedSnippets, sourceErrors 
  }
 
  // ------------------------------------------------------------------------------------
  // Agent that implements the parsing & formatting
  
  let agent = MailboxProcessor.Start(fun agent -> async {
    while true do
      // Receive parameters for the next parsing request
      let! request, (chnl:AsyncReplyChannel<_>) = agent.Receive()
      try
        let! res, errs = processSourceCode request
        chnl.Reply(Choice1Of2(res |> Array.ofList, errs))
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
    | Choice2Of2 exn -> return raise exn }

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
    | Choice2Of2 exn -> raise exn 
