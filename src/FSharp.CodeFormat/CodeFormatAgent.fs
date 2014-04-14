// --------------------------------------------------------------------------------------
// F# CodeFormat (CodeFormatAgent.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
namespace FSharp.CodeFormat

open System
open System.IO
open System.Web
open System.Text
open System.Collections.Generic

open FSharp.CodeFormat
open FSharp.CodeFormat.CommentFilter

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

module FsParser = Microsoft.FSharp.Compiler.Parser

// --------------------------------------------------------------------------------------
// ?
// --------------------------------------------------------------------------------------

module private Helpers = 
  
  /// Mapping table that translates F# compiler representation to our union
  let private colorMap =
    [ TokenColorKind.Comment, TokenKind.Comment
      TokenColorKind.Identifier, TokenKind.Identifier
      TokenColorKind.InactiveCode, TokenKind.Inactive
      TokenColorKind.Keyword, TokenKind.Keyword
      TokenColorKind.Number, TokenKind.Number
      TokenColorKind.Operator, TokenKind.Operator
      TokenColorKind.PreprocessorKeyword, TokenKind.Preprocessor
      TokenColorKind.String, TokenKind.String
      TokenColorKind.UpperIdentifier, TokenKind.Identifier ] |> Map.ofSeq

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
    [ for l, (toks:_ list) in lines do
        match toks with
        | ((text:string), info)::_ when info.TokenName = "WHITESPACE" ->
            yield text.Length - text.TrimStart([| ' ' |]).Length
        | [] -> ()
        | _ -> yield 0 ] |> Seq.min

// --------------------------------------------------------------------------------------
// Main type that implements parsing and uses F# services
// --------------------------------------------------------------------------------------

/// Uses agent to handle formatting requests
type CodeFormatAgent() = 

  // Get the number of "IDENT" token in the F# compiler
  // (This is needed when calling compiler, and it varies depending
  // on the version i.e. when new keywords are added). Use reflection hack!
  let identToken = FsParser.tagOfToken(FsParser.token.IDENT("")) 
  
  // Create keys for query tooltips for double-backtick identifiers
  let processDoubleBackticks (body : string) = 
    if body.StartsWith "``" then
      sprintf "( %s )" <| body.Trim('`')
    else body

  // Processes a single line of the snippet
  let processSnippetLine (checkInfo:CheckFileResults) (lines:string[]) (line, lineTokens) =

    // Recursive processing of tokens on the line 
    // (keeps a long identifier 'island')
    let rec loop island (tokens:SnippetLine) = seq {
      match tokens with 
      | [] -> ()
      | (body, tokenInfo)::rest ->
        // Update the current identifier island 
        // (long identifier e.g. Collections.List.map)
        let island =
          match tokenInfo.TokenName with
          | "DOT" -> island         // keep what we have found so far
          | "IDENT" -> processDoubleBackticks body::island  // add current identifier
          | _ -> []                 // drop everything - not in island

        // Find tootltip using F# compiler service & the identifier island
        let tip =
          // If we're processing an identfier, see if it has any tool tip
          if (tokenInfo.TokenName = "IDENT") then
            let island = island |> List.rev
            let tip = checkInfo.GetToolTipText(line, tokenInfo.LeftColumn + 1, lines.[line], island, identToken)
            match ToolTipReader.tryFormatTip tip with
            | Some(_) as res -> res
            | _ when island.Length > 1 ->
                // Try to find some information about the last part of the identifier 
                let tip = checkInfo.GetToolTipText(line, tokenInfo.LeftColumn + 2, lines.[line], [ processDoubleBackticks body ], identToken)
                ToolTipReader.tryFormatTip tip
            | _ -> None
          else None

        if tokenInfo.TokenName.StartsWith("OMIT") then 
          // Special OMIT tag - add tool tip stored in token name
          // (The text immediately follows the keyword "OMIT")
          yield Omitted(body, tokenInfo.TokenName.Substring(4))       
        elif tokenInfo.TokenName = "FSI" then
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
              // Lookup token kind & return information about token
              yield Token(Helpers.getTokenKind tokenInfo.ColorClass, body, tip)

        // Process the rest of the line
        yield! loop island rest }

    // Process the current line & return info about it
    Line (loop [] (List.ofSeq lineTokens) |> List.ofSeq)


  /// Process snippet
  let processSnippet checkInfo lines (source:Snippet) =
    source |> List.map (processSnippetLine checkInfo lines)

// --------------------------------------------------------------------------------------

  // Create an instance of an InteractiveChecker (which does background analysis
  // in a typical IntelliSense editor integration for F#)
  let checker = InteractiveChecker.Create()

  /// Type-checking takes some time and doesn't return information on the
  /// first call, so this function creates workflow that tries repeatedly
  let getTypeCheckInfo(untypedInfo, file, source, opts) = 
      match checker.CheckFileInProject(untypedInfo, file, 0, source, opts) |> Async.RunSynchronously with 
      | CheckFileAnswer.Succeeded(res) -> res
      | CheckFileAnswer.Aborted -> failwith "unexpected abort"


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
    let! opts = checker.GetProjectOptionsFromScript(file, source, DateTime.Now) 
    
    // Override default options if the user specified something
    let opts = 
      match options with 
      | Some(str:string) when not(String.IsNullOrEmpty(str)) -> 
          { opts with ProjectOptions = Helpers.parseOptions str }
      | _ -> opts

    // Run the first phase - parse source into AST without type information 
    let untypedInfo = checker.ParseFileInProject(file, source, opts)  |> Async.RunSynchronously
    // Run the second phase - perform type checking
    let checkInfo = getTypeCheckInfo(untypedInfo, file, source, opts)
    let errors = checkInfo.Errors

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
      | [] -> ["Untitled", source]
      | snippets -> snippets |> List.rev

    // Generate a list of snippets
    let parsedSnippets = 
      snippets |> List.map (fun (title, lines) -> 
        if lines.Length = 0 then
          // Skip empty snippets
          Snippet(title, [])
        else
          // Process the current snippet
          let parsed = processSnippet checkInfo sourceLines lines

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
      [| for errInfo in errors do
          if errInfo.Message <> "Multiple references to 'mscorlib.dll' are not permitted" then
           yield 
             SourceError
               ( (errInfo.StartLine, errInfo.StartColumn), (errInfo.EndLine, errInfo.EndColumn),
                 (if errInfo.Severity = Severity.Error then ErrorKind.Error else ErrorKind.Warning),
                 errInfo.Message ) |]
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
  member x.AsyncParseSource(file, source, ?options, ?defines) = async {
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
  member x.ParseSource(file, source, ?options, ?defines) =
    let res = agent.PostAndReply(fun chnl -> (file, source, options, defines), chnl)
    match res with
    | Choice1Of2 res -> res
    | Choice2Of2 exn -> raise exn 
