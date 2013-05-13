// --------------------------------------------------------------------------------------
// (c) Tomas Petricek, http://tomasp.net/blog
// This code released under the terms of the Microsoft Public License (MS-PL)
// --------------------------------------------------------------------------------------
namespace FSharp.IntelliSense

open System
open System.IO
open System.Web
open System.Text
open System.Collections.Generic

open FSharp.IntelliSense
open FSharp.IntelliSense.TextProcessing
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

// --------------------------------------------------------------------------------------
// Color map and various types returned from source code processing
// --------------------------------------------------------------------------------------

/// A mapping from kinds of tokens to CSS classes used by the formatter
module Colors = 
  let colorMap =
    [ TokenColorKind.Comment, "c"
      TokenColorKind.Identifier, "i"
      TokenColorKind.InactiveCode, "inactive"
      TokenColorKind.Keyword, "k"
      TokenColorKind.Number, "n"
      TokenColorKind.Operator, "o"
      TokenColorKind.PreprocessorKeyword, "prep"
      TokenColorKind.String, "s"
      TokenColorKind.UpperIdentifier, "i" ] |> Map.ofSeq


/// Stores information about tool tip for an identifier
type ToolTip private (str) = 

  /// Remove additional (unnecessary) spaces from beginning of all lines
  static let rec removeSpaces (lines:seq<string>) =
    let remove = 
      seq { for s in lines do
              let ts = s.TrimStart [|' '|]
              if ts <> "" then yield s.Length - ts.Length } |> Seq.min
    if remove > 0 then 
      seq { for s in lines -> 
              if s.TrimStart [|' '|] = "" then s
              else s.Substring(remove) }
    else lines

  /// Format lines as HTML - replace initial spaces with entities and use <br/>
  static let formatLines (lines:seq<string>) = 
    [ for l in lines do 
        let trim = l.TrimStart(' ')
        let dif = l.Length - trim.Length 
        yield (String.replicate dif "&#160;") + trim + "<br />" ]
    |> String.concat ""

  /// Returns tool tip text formatted as a HTML
  member x.ToolTipHtml = str

  /// Creates a tool tip - returns 'None' if it contains no data
  static member FromString(str:string) = 
    let str = HttpUtility.HtmlEncode(str.Trim [| '\n'; '\r' |])
    let lines = str.Split [| '\n' |] |> removeSpaces
    ToolTip(formatLines lines)

  /// Creates a tool tip - returns 'None' if it contains no data
  static member TryCreate(tip:DataTipText) =
    match tip with 
    | DataTipText(elems) 
        when elems |> List.forall (function 
          DataTipElementNone -> true | _ -> false) -> None
    | _ -> 
      // Format the tool tip as a HTML 
      let lines = (TipFormatter.formatTip tip).Split([| '\n' |]) 
      let str = formatLines lines
      Some(ToolTip(str))


/// Stores information about a single token (including tip & color)
type TokenInfo = 
  { Token : TokenInformation
    Text : string 
    Color : string option
    Tip : ToolTip option  }


/// Stores information about line in the source code
type LineInfo = 
  { Index : int
    LineNumber : int
    Tokens : TokenInfo list }

/// Stores information about source code snippet
type SnippetInfo = 
  { Lines : LineInfo list
    Title : string }

/// Represents information about error message
type ErrorInfo =
  { StartColumn : int
    StartLine : int
    EndColumn : int
    EndLine : int
    IsError : bool
    Message : string }

// --------------------------------------------------------------------------------------
// Main type that implements parsing and uses F# services
// --------------------------------------------------------------------------------------

/// Parses the specified file using F# compiler (by calling 'TokenizeSource'),
/// performs type checking (using 'RunTypeCheck') and then creates information
/// for the formatter (using 'ProcessSourceTokens')
// [snippet:Async]
type SourceFile(file, source, lines:string[], ?options, ?defines) = 
  (*[omit:(construction of interactive checker and compiler options omitted)]*)
  
  // Create an instance of an InteractiveChecker (which does background analysis
  // in a typical IntelliSense editor integration for F#)
  let checker = InteractiveChecker.Create(ignore)
  // Get options for a standalone script file (this adds some 
  // default references and doesn't require full project information)
  let opts = checker.GetCheckOptionsFromScriptRoot(file, source) 

  // Print additional information for debugging
  let trace = false
  
  // Parse command line options - split string by space, but if there is something
  // enclosed in double quotes "..." then ignore spaces in the quoted text
  let rec parseOptions (str:string) i opts current = 
    let opts = 
      if i < str.Length && str.[i] <> ' ' then opts
      else (String(current |> List.rev |> Array.ofSeq))::opts
    if i = str.Length then opts
    elif str.[i] = ' ' then parseOptions str (i+1) opts []
    elif str.[i] = '"' then
      let endp = str.IndexOf('"', i+1)
      let chars = str.Substring(i+1, endp - i - 1) |> List.ofSeq |> List.rev
      parseOptions str (endp + 1) opts (chars @ current)
    else parseOptions str (i + 1) opts (str.[i] :: current)
    
  // Override default options if the user specified something
  let opts = 
    match options with 
    | Some(str:string) when not(String.IsNullOrEmpty(str)) -> 
        opts.WithProjectOptions(parseOptions str 0 [] [] |> Array.ofSeq)
    | _ -> opts

  // Run first parsing phase - parse source into AST without type information 
  let untypedInfo = checker.UntypedParse(file, source, opts) 
    
  // Creates an empty "Identifier" token (we need it when getting ToolTip)
  let identToken = 179 // 179 in 4.3, 176 in 4.0, 178 in joinads(*[/omit]*)

  /// Type-checking takes some time and doesn't return information on the
  /// first call, so this function creates workflow that tries repeatedly
  let rec getTypeCheckInfo() = async {
    let obs = IsResultObsolete(fun () -> false)
    let info = checker.TypeCheckSource(untypedInfo, file, 0, source, opts, obs) 
    match info with
    | TypeCheckSucceeded(res) when res.TypeCheckInfo.IsSome ->
        let errs = (*[omit:(copying of errors omitted)]*)
          seq { for e in res.Errors ->
                  { StartColumn = e.StartColumn; StartLine = e.StartLine
                    Message = e.Message; IsError = e.Severity = Error
                    EndColumn = e.EndColumn; EndLine = e.EndLine } }(*[/omit]*)
        return res.TypeCheckInfo.Value, errs
    | _ -> 
        do! Async.Sleep(500)
        return! getTypeCheckInfo() }

  /// Runs type checking and allows specifying a timeout
  member x.RunTypeCheck(?timeout) =
    Async.RunSynchronously(getTypeCheckInfo(), ?timeout = timeout)
// [/snippet]

  /// Parse source file into a list of lines consisting of tokens 
  member x.TokenizeSource() =
    let defines = defines |> Option.map (fun (s:string) -> 
      s.Split([| ' '; ';'; ',' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofSeq)
    let sourceTok = SourceTokenizer(defaultArg defines [], file)
    [ let state = ref 0L
      for n, line in lines |> Seq.zip [ 0 .. lines.Length ] do
        let tokenizer = sourceTok.CreateLineTokenizer(line)
        tokenizer.StartNewLine()
        let rec parseLine() = seq {
          match tokenizer.ScanToken(!state) with
          | Some(tok), nstate ->
              let str = line.Substring(tok.LeftColumn, tok.RightColumn - tok.LeftColumn + 1)
              yield str, tok
              state := nstate
              yield! parseLine()
          | None, nstate -> state := nstate }
        yield n, parseLine() |> List.ofSeq ]

  /// When type-checking completes and we have a parsed file (as tokens), we can
  /// put the information together - this processes tokens and adds information such
  /// as color and tool tips (for identifiers)
  member x.ProcessSourceTokens(checkInfo:TypeCheckInfo, source) =
    
    // Process "omit" meta-comments in the source
    let source = shrinkOmittedParts source |> List.ofSeq

    // Split source into snippets if it contains meta-comments
    let snippets = 
      match getSnippets None [] source lines with
      | [] -> ["Untitled", source]
      | snippets -> snippets |> List.rev

    let processSnippet source = [
      for i, (line, lineTokens) in source |> List.zip [ 1 .. source.Length ] do
        // Recursive processing of tokens on the line (keeps a long identifier 'island')
        // [snippet:line]
        let rec processLine island tokens = seq {
          match tokens with 
          | [] -> ()
          | (str, (tok:TokenInformation))::rest ->
            (*[omit:(updating of long identifier information omitted)]*)
            // Update the current identifier island 
            // (long identifier e.g. Collections.List.map)
            let island =
              match tok.TokenName with
              | "DOT" -> island         // keep what we have found so far
              | "IDENT" -> str::island  // add current identifier
              | _ -> []                 // drop everything - not in island
            (*[/omit]*)
            let tip =
              // If we're processing an identfier, see if it has any tool tip
              if (tok.TokenName = "IDENT") then
                let island = island |> List.rev
                let pos = (line, tok.LeftColumn + 1)
                let tip = checkInfo.GetDataTipText(pos, lines.[line], island, identToken)
                match ToolTip.TryCreate(tip) with
                | Some(_) as res -> res
                | _ when island.Length > 1 -> (*[omit:(alternative attempt omitted)]*)
                    // Try to find some information about the last part of the identifier 
                    let pos = (line, tok.LeftColumn + 2)
                    let tip = checkInfo.GetDataTipText(pos, lines.[line], [ str ], identToken)
                    ToolTip.TryCreate(tip)(*[/omit]*)
                | _ -> None
              elif tok.TokenName.StartsWith("OMIT") then (*[omit:(...)]*)
                // Special omit tag - add tool tip stored in token name
                Some(ToolTip.FromString(tok.TokenName.Substring(4)))(*[/omit]*)
              else None

            // Find color for the current token
            let color = 
              if tok.TokenName = "FSI" then Some("fsi")
              elif tok.TokenName.StartsWith("OMIT") then Some("omitted")
              else Colors.colorMap.TryFind(tok.ColorClass)
            // Return all information about token and continue
            yield { Token = tok; Text = str; Color = color; Tip = tip }
            yield! processLine island rest }
        // [/snippet]

        // Process the current line & return info about it
        let lineInfos = processLine [] (List.ofSeq lineTokens) |> List.ofSeq
        yield { Index = i; LineNumber = line; Tokens = lineInfos } ]

    // Generate a list of snippets
    [ for title, lines in snippets do
        // Print debug information

        if trace then printfn "\n\n\n%A" lines
        // Count the minimal number of spaces at the beginning of lines
        // (so that we can remove spaces for indented text)
        let spaces = 
          [ for l, (toks:_ list) in lines do
              match toks with
              | ((text:string), info)::_ when info.TokenName = "WHITESPACE" ->
                  yield text.Length - text.TrimStart([| ' ' |]).Length
              | [] -> ()
              | _ -> yield 0 ] |> Seq.min
        
        // Process the current snippet
        let res = processSnippet lines

        // Remove additional whitespace from start of lines
        let res = 
          [ for line in res do
              match line.Tokens with
              | first::rest ->  
                  let tokens = { first with Text = first.Text.Substring(spaces) }::rest
                  yield { line with Tokens = tokens }
              | _ -> yield line ]
        yield { Title = title; Lines = res } ] 