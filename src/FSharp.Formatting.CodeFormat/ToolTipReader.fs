// --------------------------------------------------------------------------------------
// F# CodeFormat (ToolTipReader.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------

/// Internal module that reads FSharp.Compiler.Service tool-tip data and converts it
/// into the <see cref="T:FSharp.Formatting.CodeFormat.ToolTipSpan"/> representation
/// used for rendering hover documentation in code snippets.
module internal FSharp.Formatting.CodeFormat.ToolTipReader

open System.Text
open System.Text.RegularExpressions
open System.Xml.Linq
open FSharp.Collections
open FSharp.Compiler.Symbols
open FSharp.Compiler.EditorServices
open FSharp.Compiler.Text

// --------------------------------------------------------------------------------------
// Implements formatting of tool tips
// --------------------------------------------------------------------------------------

/// How wide a signature is allowed to get before it is laid out over several lines.
[<Literal>]
let private signatureWidth = 80

/// Maps the tag the compiler attached to a run of tool tip text onto the token kind
/// the snippet formatter already knows how to color, so a tool tip is highlighted by
/// the same rules as the code it describes.
let private tokenKindOfTextTag (tag: TextTag) =
    match tag with
    | TextTag.Keyword -> TokenKind.Keyword
    | TextTag.Class
    | TextTag.Record
    | TextTag.Union
    | TextTag.Delegate
    | TextTag.Event -> TokenKind.ReferenceType
    | TextTag.Struct -> TokenKind.ValueType
    | TextTag.Interface -> TokenKind.Interface
    | TextTag.Enum -> TokenKind.Enumeration
    | TextTag.Module
    | TextTag.Namespace
    // the qualifier of a fully written name, `System.` in `System.DateTime`
    | TextTag.UnknownEntity -> TokenKind.Module
    | TextTag.TypeParameter -> TokenKind.TypeArgument
    | TextTag.UnionCase
    | TextTag.ActivePatternCase
    | TextTag.ActivePatternResult -> TokenKind.UnionCase
    | TextTag.Function
    | TextTag.Method
    | TextTag.Member
    | TextTag.ModuleBinding -> TokenKind.Function
    | TextTag.Property
    | TextTag.Field
    | TextTag.RecordField -> TokenKind.Property
    | TextTag.NumericLiteral -> TokenKind.Number
    | TextTag.StringLiteral -> TokenKind.String
    | TextTag.Operator -> TokenKind.Operator
    | TextTag.Punctuation -> TokenKind.Punctuation
    | TextTag.Parameter
    | TextTag.Local -> TokenKind.Identifier
    // `string`, `list`, `option` and `array` are type abbreviations, which read as types even
    // though the compiler tags them apart from the ones they abbreviate. Leaving them out of the
    // type color puts `string` and `int` in two colors in the same signature.
    | TextTag.Alias -> TokenKind.ReferenceType
    | TextTag.Text
    | TextTag.Space
    | TextTag.LineBreak
    | TextTag.UnknownType -> TokenKind.Default

/// Converts an array of <see cref="T:FSharp.Compiler.Text.TaggedText"/> values emitted by
/// the F# compiler into lines of runs. Each run keeps the token kind its tag maps to, so
/// the classification survives into the rendered tip.
let linesFromTaggedText (tags: TaggedText array) =
    seq {
        let line = ResizeArray<TokenKind * string>()

        for tag in tags do
            let kind = tokenKindOfTextTag tag.Tag

            // The compiler does not hand us a line break in a tag of its own. It arrives
            // inside a Space or a Text run, often with the indentation of the next line
            // attached, so split on the newline itself rather than on the tag.
            let parts = tag.Text.Replace("\r\n", "\n").Split('\n')

            for index in 0 .. parts.Length - 1 do
                if index > 0 then
                    yield List.ofSeq line
                    line.Clear()

                if parts[index] <> "" then
                    line.Add(kind, parts[index])
        // yield any remaining text
        if line.Count <> 0 then
            yield List.ofSeq line
    }

/// Strips F# attribute annotations (e.g. [<Optional>], [<DefaultParameterValue(null)>])
/// from a tooltip line. These are technically accurate but make tooltips hard to read,
/// particularly for methods with many attributed optional parameters.
let private attributeAnnotationPattern = Regex(@"\s*\[<[^>]*>\]")

let internal stripParameterAttributes (runs: (TokenKind * string) list) =
    let text = runs |> List.map snd |> String.concat ""
    let matches = attributeAnnotationPattern.Matches(text)

    if matches.Count = 0 then
        runs
    else
        // The pattern spans several runs, so mark the characters it removes and rebuild
        // the runs around them. Every surviving character keeps the kind it came in with.
        let dropped = Array.zeroCreate<bool> text.Length

        for m in matches do
            for i in m.Index .. m.Index + m.Length - 1 do
                dropped[i] <- true

        let mutable pos = 0

        [
            for (kind, run) in runs do
                let kept = StringBuilder()

                for ch in run do
                    if not dropped[pos] then
                        kept.Append(ch) |> ignore

                    pos <- pos + 1

                if kept.Length > 0 then
                    yield kind, string<StringBuilder> kept
        ]

/// Turn tagged lines into a sequence of tool tip spans interleaved with line breaks
let private formatTaggedLines (lines: (TokenKind * string) list list) =
    [
        for line in lines do
            yield HardLineBreak

            for (kind, text) in line do
                yield Token(kind, text)
    ]
    |> function
        | [] -> []
        | _ :: rest -> rest

/// Turn string into a sequence of lines interleaved with line breaks
let formatMultilineString (lines: string array) =
    [
        for line in lines do
            yield HardLineBreak
            yield Literal line
    ]
    |> List.tail

/// Splits text into lines and removes the indentation every line shares, which for a doc
/// comment is the space after the "///".
let private toLines (text: string) =
    let lines =
        text.Replace("\r\n", "\n").Split('\n')
        |> Array.map (fun line -> line.TrimEnd())
        |> Array.skipWhile System.String.IsNullOrWhiteSpace
        |> Array.rev
        |> Array.skipWhile System.String.IsNullOrWhiteSpace
        |> Array.rev

    let indent =
        lines
        |> Array.choose (fun line ->
            if System.String.IsNullOrWhiteSpace line then
                None
            else
                Some(line.Length - line.TrimStart(' ').Length))
        |> function
            | [||] -> 0
            | indents -> Array.min indents

    lines
    |> Array.map (fun line -> if line.Length <= indent then "" else line.Substring(indent))

/// The name a cref points at, without the "T:" kind prefix and without the namespace.
let private crefName (cref: string) =
    let withoutKind =
        if cref.Length > 1 && cref[1] = ':' then
            cref.Substring(2)
        else
            cref

    withoutKind.Split('.') |> Array.last

/// Reads the summary text and the per-parameter details out of one doc comment fragment.
/// Returns None when the fragment is not well formed XML, which a doc comment need not be.
let private tryReadXmlDoc (text: string) =
    try
        // a doc comment is a fragment of sibling elements rather than a single rooted
        // document, so give it a root of its own before parsing
        let root = XElement.Parse("<fsdocs>" + text + "</fsdocs>", LoadOptions.PreserveWhitespace)

        // a <see cref="T:Foo.Bar"/> keeps its text in an attribute, so it would leave a
        // hole in the sentence around it once we ask an element for its text
        for reference in List.ofSeq (root.Descendants()) do
            match reference.Name.LocalName with
            | "see"
            | "seealso"
            | "paramref"
            | "typeparamref" ->
                let named =
                    [ "cref"; "name" ]
                    |> List.tryPick (fun name ->
                        match reference.Attribute(XName.Get name) with
                        | null -> None
                        | attribute -> Some attribute.Value)

                match named with
                | Some value when System.String.IsNullOrWhiteSpace reference.Value ->
                    reference.ReplaceWith(XText(crefName value))
                | _ -> reference.ReplaceWith(XText(reference.Value))
            | _ -> ()

        // Anything the author did not put in an element of its own reads as the summary,
        // and most doc comments are written that way.
        let summary = StringBuilder()

        for node in root.Nodes() do
            match node with
            | :? XElement as element ->
                match element.Name.LocalName with
                | "param"
                | "typeparam"
                | "returns"
                | "exception" -> ()
                | _ -> summary.Append(element.Value) |> ignore
            | :? XText as text -> summary.Append(text.Value) |> ignore
            | _ -> ()

        let details =
            [|
                for element in root.Elements() do
                    match element.Name.LocalName with
                    | "param"
                    | "typeparam" ->
                        let name =
                            match element.Attribute(XName.Get "name") with
                            | null -> element.Name.LocalName
                            | attribute -> attribute.Value

                        yield sprintf "%s: %s" name (element.Value.Trim())
                    | "returns" -> yield sprintf "returns: %s" (element.Value.Trim())
                    | _ -> ()
            |]

        Some(string<StringBuilder> summary, details)
    with _ ->
        None

/// Reads the text out of the doc comment elements a signature tool tip cares about. The
/// compiler hands the comment over as the XML the author wrote, and a tip is no place for
/// markup. A doc comment is not required to be well formed, so fall back to the raw lines.
let private xmlDocLines (xmlDoc: FSharp.Compiler.Xml.XmlDoc) =
    // GetXmlText is no use here: it escapes the whole comment whenever it has to supply
    // the <summary> wrapper itself, which turns a <see cref="..."/> into literal text
    let raw = xmlDoc.UnprocessedLines

    // The compiler applies that same escaping when it writes the xml documentation file,
    // so markup can survive a round of parsing as text and needs unwrapping once more.
    let rec read unwrapped text =
        match tryReadXmlDoc text with
        | Some(summary, details) when not unwrapped && summary.Contains "<" ->
            match read true summary with
            | Some(inner, extra) -> Some(inner, Array.append details extra)
            | None -> Some(summary, details)
        | result -> result

    match read false (String.concat "\n" raw) with
    | Some(summary, details) ->
        match toLines summary, details with
        | [||], details -> details
        | summary, [||] -> summary
        | summary, details -> Array.concat [ summary; [| "" |]; details ]
    | None ->
        // An "unprocessed line" can itself span several lines, and a tool tip is flowing
        // text, so split them here or the browser collapses the break away.
        raw |> Array.collect (fun line -> line.Replace("\r\n", "\n").Split('\n'))


/// Format comment in the tool tip
let private formatComment xmlDoc =
    match xmlDoc with
    | FSharpXmlDoc.FromXmlText(xmlDoc) ->
        match xmlDocLines xmlDoc with
        | [||] -> []
        | lines -> [ Emphasis(formatMultilineString lines); HardLineBreak ]
    | _ ->
        // TODO: For 'XmlCommentSignature' we could get documentation
        // from 'xml' files, but we don't know where to get them...
        []

/// Format the element of a tool tip (comment, overloads, etc.)
let private formatElement tooltip =
    match tooltip with
    | ToolTipElement.None -> []
    //| FSharpToolTipElement.(it, comment) ->
    //    [ yield! formatMultilineString it
    //      yield HardLineBreak
    //      yield! formatComment comment ]
    | ToolTipElement.Group(items) ->
        // Trim the items to at most 10 displayed in a tool tip
        let items, trimmed =
            if items.Length <= 10 then
                items, false
            else
                items |> Seq.take 10 |> List.ofSeq, true

        [
            for it in items do
                // the doc comment reads first and the signature it describes follows it
                yield! formatComment it.XmlDoc

                yield!
                    it.MainDescription
                    |> linesFromTaggedText
                    |> Seq.choose (fun line ->
                        // a line left holding nothing but whitespace carries no information,
                        // and the tip reads better without it
                        match stripParameterAttributes line with
                        | stripped when
                            stripped
                            |> List.exists (fun (_, text) -> not (System.String.IsNullOrWhiteSpace text))
                            ->
                            Some stripped
                        | _ -> None)
                    |> List.ofSeq
                    |> List.collect (NiceSignaturePrint.layout signatureWidth)
                    |> formatTaggedLines

                yield HardLineBreak

                // Add note with the number of omitted overloads
                if trimmed then
                    let msg = sprintf "(+%d other overloads)" (items.Length - 10)

                    yield Literal "   "
                    yield Emphasis [ Literal(msg) ]
                    yield HardLineBreak
        ]
    //| FSharpToolTipElement.SingleParameter(_paramType,_doc,_name) ->
    //  [   yield ToolTipSpan.Literal _paramType
    //      yield ToolTipSpan.HardLineBreak
    //      yield! formatComment _doc
    //  ]
    | ToolTipElement.CompositionError(_err) -> []

/// Format entire tool tip as a value of type ToolTipSpans
let private formatTip tip =
    let spans =
        match tip with
        | ToolTipText([ single ]) -> formatElement single
        | ToolTipText(items) ->
            [
                yield Literal "Multiple items"
                yield HardLineBreak
                for first, item in Seq.mapi (fun i it -> i = 0, it) items do
                    if not first then
                        yield HardLineBreak
                        yield Literal "--------------------"
                        yield HardLineBreak

                    yield! formatElement item
            ]

    // Remove unnecessary line breaks
    spans
    |> List.skipWhile ((=) HardLineBreak)
    |> List.rev
    |> List.skipWhile ((=) HardLineBreak)
    |> List.rev

/// Format a tool tip, but first make sure that there is actually
/// some text in the tip. Returns None if no information is available
let tryFormatTip =
    function
    | ToolTipText(elems) when
        elems
        |> List.forall (function
            | ToolTipElement.None -> true
            | _ -> false)
        ->
        None
    | tip -> Some(formatTip tip)
