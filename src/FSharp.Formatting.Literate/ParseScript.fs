namespace FSharp.Formatting.Literate

open System.Collections.Generic
open FSharp.Patterns
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Markdown

/// Parsing of F# Script files with Markdown commands. Given a parsed script file, we
/// split it into a sequence of comments, snippets and commands (comment starts with
/// <c>(**</c> and ending with <c>*)</c> are translated to Markdown, snippet is all other F# code
/// and command looks like <c>(*** key1:value, key2:value ***)</c> (and should be single line).
module internal CodeBlockUtils =
    type Block =
        | BlockComment of string
        | BlockSnippet of Line list
        | BlockCommand of IDictionary<string, string>

    /// Trim blank lines from both ends of a lines list & reverse it (we accumulate
    /// lines & we want to remove all blanks before returning BlockSnippet)
    let private trimBlanksAndReverse lines =
        lines
        |> Seq.skipWhile (function
            | Line (_, []) -> true
            | _ -> false)
        |> List.ofSeq
        |> List.rev
        |> Seq.skipWhile (function
            | Line (_, []) -> true
            | _ -> false)
        |> List.ofSeq

    let splitLines (s: string) =
        s.Replace("\r\n", "\n").Split([| '\n' |])

    /// Succeeds when a line (list of tokens) contains only Comment
    /// tokens and returns the text from the comment as a string
    /// (Comment may also be followed by Whitespace that is skipped)
    let private (|ConcatenatedComments|_|) (Line (_, tokens)) =
        let rec readComments inWhite acc =
            function
            | TokenSpan.Token (TokenKind.Comment, text, _) :: tokens when not inWhite ->
                readComments false (text :: acc) tokens
            | TokenSpan.Token (TokenKind.Default, String.WhiteSpace _, _) :: tokens -> readComments true acc tokens
            | [] -> Some(String.concat "" (List.rev acc))
            | _ -> None

        readComments false [] tokens

    // Process lines of an F# script file. Simple state machine with two states
    //  * collectComment - we're parsing a comment and waiting for the end
    //  * collectSnippet - we're in a normal F# code and we're waiting for a comment
    //    (in both states, we also need to recognize (*** commands ***)

    /// Waiting for the end of a comment
    let rec private collectComment (comment: string) lines =
        seq {
            let findCommentEnd (comment: string) =
                let cend = comment.LastIndexOf("*)")

                if cend = -1 then
                    failwith "A (* comment was not closed"

                cend

            match lines with
            | (ConcatenatedComments (String.StartsAndEndsWith ("(***", "***)") (ParseCommands cmds))) :: lines ->
                // Ended with a command, yield comment, command & parse the next as a snippet
                let cend = findCommentEnd comment
                yield BlockComment(comment.Substring(0, cend))
                yield BlockCommand cmds
                yield! collectSnippet [] lines

            | (ConcatenatedComments text) :: _ when comment.LastIndexOf("*)") <> -1 && text.Trim().StartsWith("//") ->
                // Comment ended, but we found a code snippet starting with // comment
                let cend = findCommentEnd comment
                yield BlockComment(comment.Substring(0, cend))
                yield! collectSnippet [] lines

            | (Line (_, [ TokenSpan.Token (TokenKind.Comment, String.StartsWith "(**" text, _) ])) :: lines ->
                // Another block of Markdown comment starting...
                // Yield the previous snippet block and continue parsing more comments
                let cend = findCommentEnd comment
                yield BlockComment(comment.Substring(0, cend))

                if lines <> [] then
                    yield! collectComment text lines

            | (ConcatenatedComments text) :: lines ->
                // Continue parsing comment
                yield! collectComment (comment + "\n" + text) lines

            | lines ->
                // Ended - yield comment & continue parsing snippet
                let cend = findCommentEnd comment
                yield BlockComment(comment.Substring(0, cend))

                if lines <> [] then
                    yield! collectSnippet [] lines
        }

    /// Collecting a block of F# snippet
    and private collectSnippet acc lines =
        let blockSnippet acc =
            let res = trimBlanksAndReverse acc
            BlockSnippet res

        seq {
            match lines with
            | (ConcatenatedComments (String.StartsAndEndsWith ("(***", "***)") (ParseCommands cmds))) :: lines ->
                // Found a special command, yield snippet, command and parse another snippet
                if acc <> [] then yield blockSnippet acc
                yield BlockCommand cmds
                yield! collectSnippet [] lines

            | (Line (_, [ TokenSpan.Token (TokenKind.Comment, String.StartsWith "(**" text, _) ])) :: lines ->
                // Found a comment - yield snippet & switch to parsing comment state
                // (Also trim leading spaces to support e.g.: `(** ## Hello **)`)
                if acc <> [] then yield blockSnippet acc
                yield! collectComment (text.TrimStart()) lines

            | x :: xs -> yield! collectSnippet (x :: acc) xs
            | [] -> yield blockSnippet acc
        }

    /// Parse F# script file into a sequence of snippets, comments and commands
    let parseScriptFile lines = collectSnippet [] lines

open CodeBlockUtils
// --------------------------------------------------------------------------------------
// LiterateScript module
// --------------------------------------------------------------------------------------

/// Turns the content of fsx file into LiterateDocument that contains
/// formatted F# snippets and parsed Markdown document. Handles commands such
/// as hide, define and include.
type internal ParseScript(parseOptions, ctx: CompilerContext) =

    let getVisibility cmds =
        match cmds with
        | Command "hide" _ -> LiterateCodeVisibility.HiddenCode
        | Command "define" name -> LiterateCodeVisibility.NamedCode name
        | _ -> LiterateCodeVisibility.VisibleCode

    let getEvaluate noEval (cmds: IDictionary<_, _>) =
        not (noEval || cmds.ContainsKey("do-not-eval"))

    let getParaOptions cmds =
        match cmds with
        | Command "condition" name when not (System.String.IsNullOrWhiteSpace name) -> { Condition = Some name }
        | _ -> { Condition = None }

    /// Transform list of code blocks (snippet/comment/command)
    /// into a formatted Markdown document, with link definitions
    let rec transformBlocks isFirst prevCodeId count noEval acc defs blocks =
        match blocks with
        // Disable evaluation for the rest of the file
        | BlockCommand (Command "do-not-eval-file" _) :: blocks -> transformBlocks false None count true acc defs blocks

        // Reference to code snippet defined later
        | BlockCommand ((Command "include" ref) as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(CodeReference(ref, popts), None)

            transformBlocks false None count noEval (p :: acc) defs blocks

        // Include console output (stdout) of previous block
        | BlockCommand (Command "include-output" "" as cmds) :: blocks when prevCodeId.IsSome ->
            let popts = getParaOptions cmds

            let p1 = EmbedParagraphs(OutputReference(prevCodeId.Value, popts), None)

            transformBlocks false prevCodeId count noEval (p1 :: acc) defs blocks

        // Include console output (stdout) of a named block
        | BlockCommand (Command "include-output" ref as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(OutputReference(ref, popts), None)

            transformBlocks false prevCodeId count noEval (p :: acc) defs blocks

        // Include FSI output (stdout) of previous block
        | BlockCommand (Command "include-fsi-output" "" as cmds) :: blocks when prevCodeId.IsSome ->
            let popts = getParaOptions cmds

            let p1 = EmbedParagraphs(FsiOutputReference(prevCodeId.Value, popts), None)

            transformBlocks false prevCodeId count noEval (p1 :: acc) defs blocks

        // Include FSI output (stdout) of a named block
        | BlockCommand (Command "include-fsi-output" ref as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(FsiOutputReference(ref, popts), None)

            transformBlocks false prevCodeId count noEval (p :: acc) defs blocks

        // Include the merge of the console and FSI output (stdout) of previous block
        | BlockCommand (Command "include-fsi-merged-output" "" as cmds) :: blocks when prevCodeId.IsSome ->
            let popts = getParaOptions cmds

            let p1 = EmbedParagraphs(FsiMergedOutputReference(prevCodeId.Value, popts), None)

            transformBlocks false prevCodeId count noEval (p1 :: acc) defs blocks

        // Include the merge of the console and FSI output (stdout) of a named block
        | BlockCommand (Command "include-fsi-merged-output" ref as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(FsiMergedOutputReference(ref, popts), None)

            transformBlocks false prevCodeId count noEval (p :: acc) defs blocks

        // Include formatted 'it' of previous block
        | BlockCommand ((Command "include-it" "") as cmds) :: blocks when prevCodeId.IsSome ->
            let popts = getParaOptions cmds

            let p1 = EmbedParagraphs(ItValueReference(prevCodeId.Value, popts), None)

            transformBlocks false prevCodeId count noEval (p1 :: acc) defs blocks

        // Include formatted 'it' of a named block
        | BlockCommand (Command "include-it" ref as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(ItValueReference(ref, popts), None)

            transformBlocks false None count noEval (p :: acc) defs blocks

        // Include unformatted 'it' of previous block
        | BlockCommand ((Command "include-it-raw" "") as cmds) :: blocks when prevCodeId.IsSome ->
            let popts = getParaOptions cmds

            let p1 = EmbedParagraphs(ItRawReference(prevCodeId.Value, popts), None)

            transformBlocks false prevCodeId count noEval (p1 :: acc) defs blocks

        // Include unformatted 'it' of a named block
        | BlockCommand (Command "include-it-raw" ref as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(ItRawReference(ref, popts), None)

            transformBlocks false None count noEval (p :: acc) defs blocks

        // Include formatted named value
        | BlockCommand (Command "include-value" ref as cmds) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(ValueReference(ref, popts), None)

            transformBlocks false None count noEval (p :: acc) defs blocks

        // Include code without evaluation
        | BlockCommand (Command "raw" _ as cmds) :: BlockSnippet (snip) :: blocks ->
            let popts = getParaOptions cmds

            let p = EmbedParagraphs(RawBlock(snip, popts), None)

            transformBlocks false None count noEval (p :: acc) defs blocks

        // Parse commands in [foo=bar,zoo], followed by a source code snippet
        //  * hide - the snippet will not be shown
        //  * do-not-eval - the snippet will not be evaluated
        //  * define:foo - specifies the name of this snippet (for inclusion later)
        //  * define-output - defines the name for the snippet's output
        | BlockCommand (cmds) :: BlockSnippet (snip) :: blocks ->
            let outputName =
                match cmds with
                | Command "define-output" name -> name
                | _ ->
                    incr count
                    "cell" + string count.Value

            let opts =
                { Evaluate = getEvaluate noEval cmds
                  ExecutionCount = None
                  OutputName = outputName
                  Visibility = getVisibility cmds }

            let popts = getParaOptions cmds

            let code = EmbedParagraphs(LiterateCode(snip, opts, popts), None)

            transformBlocks false (Some outputName) count noEval (code :: acc) defs blocks

        // Unknown command
        | BlockCommand (cmds) :: _ ->
            failwithf "Unknown command: %A" [ for (KeyValue (k, v)) in cmds -> sprintf "%s:%s" k v ]

        // Skip snippets with no content
        | BlockSnippet ([]) :: blocks -> transformBlocks isFirst prevCodeId count noEval acc defs blocks

        // Ordinary F# code snippet
        | BlockSnippet (snip) :: blocks ->
            let id =
                incr count
                "cell" + string count.Value

            let opts =
                { Evaluate = not noEval
                  ExecutionCount = None
                  OutputName = id
                  Visibility = LiterateCodeVisibility.VisibleCode }

            let popts = { Condition = None }

            let p = EmbedParagraphs(LiterateCode(snip, opts, popts), None)

            transformBlocks false (Some id) count noEval (p :: acc) defs blocks

        // Markdown documentation block
        | BlockComment (text) :: blocks ->
            // yaml frontmatter
            let parseOptions =
                if isFirst then
                    match parseOptions with
                    | Some o -> Some(o ||| MarkdownParseOptions.AllowYamlFrontMatter)
                    | None -> Some MarkdownParseOptions.AllowYamlFrontMatter
                else
                    parseOptions

            let doc = Markdown.Parse(text, ?parseOptions = parseOptions)

            let defs = doc.DefinedLinks :: defs
            let acc = (List.rev doc.Paragraphs) @ acc
            transformBlocks false None count noEval acc defs blocks

        | [] ->
            // Union all link definitions & return Markdown doc
            let allDefs =
                [ for def in defs do
                      for (KeyValue (k, v)) in def -> k, v ]
                |> dict

            List.rev acc, allDefs

    /// Parse script file with specified name and content
    /// and return LiterateDocument with the content
    member _.ParseAndCheckScriptFile(filePath, content, rootInputFolder, onError) =
        let defines =
            match ctx.ConditionalDefines with
            | [] -> None
            | l -> Some(String.concat "," l)

        let sourceSnippets, diagnostics =
            CodeFormatter.ParseAndCheckSource(filePath, content, ctx.CompilerOptions, defines, onError)

        let mutable fail = false

        for (SourceError ((l0, c0), (l1, c1), kind, msg)) in diagnostics do
            printfn
                "   %s: %s(%d,%d)-(%d,%d) %s"
                filePath
                (if kind = ErrorKind.Error then
                     fail <- true
                     "error"
                 else
                     "warning")
                l0
                c0
                l1
                c1
                msg

        if fail then
            ctx.OnError "errors parsing or checking script"

        let parsedBlocks =
            [ for Snippet (name, lines) in sourceSnippets do
                  if name <> null then
                      yield BlockComment("## " + name)

                  yield! parseScriptFile (lines) ]

        let paragraphs, defs = transformBlocks true None (ref 0) false [] [] (List.ofSeq parsedBlocks)

        LiterateDocument(
            paragraphs,
            "",
            defs,
            LiterateSource.Script sourceSnippets,
            filePath,
            diagnostics = diagnostics,
            rootInputFolder = rootInputFolder
        )
