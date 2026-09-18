// --------------------------------------------------------------------------------------
// F# CodeFormat (NiceSignaturePrint.fs)
// --------------------------------------------------------------------------------------

module internal FSharp.Formatting.CodeFormat.NiceSignaturePrint

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Formatting.Internal

/// The parameters of a signature, and the result under them.
[<Literal>]
let ArgumentIndent = 4

[<Literal>]
let ResultIndent = 8

/// What the type is fed to the parser as. Only the length matters afterwards, to take the
/// reported columns back to offsets in the type itself.
[<Literal>]
let FedPrefix = "val X: "

let fedFile = "fsdocs-signature.fsi"

let parsingOptions =
    { FSharpParsingOptions.Default with
        SourceFiles = [| fedFile |]
    }

/// The type the compiler read out of `text`, or None when it could not read one.
let parseType (text: string) : SynType option =
    let results =
        FSharpAssemblyHelper.checker.ParseFile(
            fedFile,
            SourceText.ofString ("module F\n" + FedPrefix + text),
            parsingOptions
        )
        |> Async.RunSynchronously

    let failed =
        results.Diagnostics
        |> Array.exists (fun diagnostic -> diagnostic.Severity = FSharpDiagnosticSeverity.Error)

    if failed then
        None
    else
        match results.ParseTree with
        | ParsedInput.SigFile(ParsedSigFileInput(contents = [ SynModuleOrNamespaceSig(decls = decls) ])) ->
            decls
            |> List.tryPick (function
                | SynModuleSigDecl.Val(valSig = SynValSig(synType = synType)) -> Some synType
                | _ -> None)
        | _ -> None

/// The index just past the colon that separates the name of a signature from its type, or -1
/// when the line carries no such colon.
///
/// The colon has to be the one at the top level: `member Foo: x: int -> int` has three, and only
/// the first ends the name. `:>` and `::` are not it either, and the `>` closing an arrow is not
/// a bracket.
let nameLength (text: string) =
    let mutable depth = 0
    let mutable index = 0
    let mutable result = -1

    while result < 0 && index < text.Length do
        match text[index] with
        | '-' when index + 1 < text.Length && text[index + 1] = '>' -> index <- index + 2
        | '('
        | '['
        | '<'
        | '{' ->
            depth <- depth + 1
            index <- index + 1
        | ')'
        | ']'
        | '>'
        | '}' ->
            depth <- depth - 1
            index <- index + 1
        | ':' when depth = 0 ->
            if index + 1 < text.Length && (text[index + 1] = '>' || text[index + 1] = ':') then
                index <- index + 2
            else
                result <- index + 1
        | _ -> index <- index + 1

    result

type private Break = { Offset: int; Indent: int }

/// The points a signature reads best when broken at: after each parameter of a tuple, after each
/// arrow, and after the colon that ends the name. Offsets are into the whole line.
let private breaksOf (typeStart: int) (synType: SynType) =
    // the reported column is into the text handed to the parser, which is the type with a
    // `val X: ` in front of it, on the line after a `module F`
    let offsetOf (range: range) =
        typeStart + range.EndColumn - FedPrefix.Length

    let rec arrows synType =
        match synType with
        | SynType.Fun(argType = argType; returnType = returnType; trivia = trivia) ->
            (offsetOf trivia.ArrowRange, argType) :: arrows returnType
        | _ -> []

    let stars synType =
        match synType with
        | SynType.Tuple(path = segments) ->
            segments
            |> List.choose (function
                | SynTupleTypeSegment.Star range -> Some(offsetOf range)
                | _ -> None)
        | _ -> []

    let tupleBreaks synType =
        stars synType
        |> List.map (fun offset ->
            {
                Offset = offset
                Indent = ArgumentIndent
            })

    match arrows synType with
    | [] -> tupleBreaks synType
    | chain ->
        let lastArrow = fst (List.last chain)

        [
            for (arrow, argument) in chain do
                yield! tupleBreaks argument

                // the result sits under the parameters, so the arrow that introduces it is the
                // one that indents further
                yield
                    {
                        Offset = arrow
                        Indent = if arrow = lastArrow then ResultIndent else ArgumentIndent
                    }
        ]

/// Cuts the runs into lines at the given offsets, indenting each line after the first.
let private cut (breaks: Break list) (runs: (TokenKind * string) list) =
    let lines = ResizeArray<(TokenKind * string) list>()
    let current = ResizeArray<TokenKind * string>()
    let mutable indent = 0
    let mutable position = 0

    let closeLine () =
        let line =
            current
            |> List.ofSeq
            |> List.filter (fun (_, text) -> text <> "")
            |> function
                // the first line keeps whatever indentation it came with
                | line when lines.Count = 0 -> line
                | [] -> []
                | (kind, head) :: rest ->
                    // the space that followed the separator belongs to neither line
                    (kind, head.TrimStart(' ')) :: rest

        // two breaks can land on the same offset, and a line of nothing but indentation is not
        // a line
        let line =
            match line with
            | [] -> []
            | line when indent = 0 -> line
            | line -> (TokenKind.Default, System.String(' ', indent)) :: line

        lines.Add line
        current.Clear()

    for (kind, text) in runs do
        let mutable start = 0

        for br in breaks do
            if br.Offset > position && br.Offset <= position + text.Length then
                let at = br.Offset - position
                current.Add(kind, text.Substring(start, at - start))
                closeLine ()
                indent <- br.Indent
                start <- at

        current.Add(kind, text.Substring start)
        position <- position + text.Length

    closeLine ()

    lines |> List.ofSeq |> List.filter (List.isEmpty >> not)

let layout (width: int) (runs: (TokenKind * string) list) : (TokenKind * string) list list =
    let text = runs |> List.map snd |> String.concat ""

    if text.Length <= width then
        [ runs ]
    else

        match nameLength text with
        | -1 -> [ runs ]
        | nameLength ->

            let typePart = text.Substring nameLength
            let trimmed = typePart.TrimStart ' '
            let typeStart = nameLength + (typePart.Length - trimmed.Length)

            match parseType trimmed with
            | None -> [ runs ]
            | Some synType ->

                // a member of a type is shown indented under it, and its continuation lines
                // are indented from where it starts rather than from the margin
                let leading = text.Length - text.TrimStart(' ').Length

                // the name keeps the line it is on, and the type starts under it
                let breaks =
                    {
                        Offset = nameLength
                        Indent = ArgumentIndent
                    }
                    :: breaksOf typeStart synType
                    |> List.choose (fun br ->
                        if br.Offset < text.Length then
                            Some { br with Indent = br.Indent + leading }
                        else
                            None)
                    |> List.sortBy (fun br -> br.Offset)

                match breaks with
                | []
                | [ _ ] -> [ runs ]
                | breaks -> cut breaks runs
