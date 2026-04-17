namespace rec FSharp.Formatting.ApiDocs

open System
open System.IO
open System.Collections.Generic
open System.Text
open System.Web
open System.Xml
open System.Xml.Linq

open FSharp.Formatting.Common
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown
open FSharp.Patterns

/// XML documentation comment reading and combining utilities.
[<AutoOpen>]
module internal XmlDocReader =
    /// Normalises leading whitespace from a multi-line XML doc comment string, removing
    /// the common indentation prefix that .NET compilers emit.
    let removeSpaces (comment: string) =
        use reader = new StringReader(comment)

        let lines =
            [ let mutable line = ""

              while (line <- reader.ReadLine()
                     not (isNull line)) do
                  yield line ]

        String.removeSpaces lines

    /// Converts a Markdown-style literate document (used when a doc comment was written as
    /// free Markdown text) into an <see cref="T:FSharp.Formatting.ApiDocs.ApiDocComment"/>.
    /// Sections headed with <c>## Returns</c>, <c>## Examples</c>, <c>## Notes</c>, and
    /// <c>## Remarks</c> are mapped to the corresponding ApiDocComment fields.
    let readMarkdownCommentAsHtml el (doc: LiterateDocument) =
        let groups = System.Collections.Generic.List<(_ * _)>()

        let mutable current = "<default>"
        groups.Add(current, [])

        let raw =
            match doc.Source with
            | LiterateSource.Markdown(string) -> [ KeyValuePair(current, string) ]
            | LiterateSource.Script _ -> []

        for par in doc.Paragraphs do
            match par with
            | Heading(2, [ Literal(text, _) ], _) ->
                current <- text.Trim()
                groups.Add(current, [ par ])
            | par -> groups.[groups.Count - 1] <- (current, par :: snd (groups.[groups.Count - 1]))

        // TODO: properly crack exceptions and parameters section of markdown docs, which have structure
        let groups = groups |> Seq.toList

        let summary, rest = groups |> List.partition (fun (section, _) -> section = "<default>")

        let notes, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Notes")

        let examples, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Examples")

        let returns, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Returns")

        let remarks, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Remarks")
        //let exceptions, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Exceptions")
        //let parameters, rest = rest |> Seq.toList |> List.partition (fun (section, _) -> section = "Parameters")

        // tailOrEmpty drops the section headings, though not for summary which is implicit
        let summary = summary |> List.collect (snd >> List.rev)

        let returns = returns |> List.collect (snd >> List.rev) |> List.tailOrEmpty

        let examples = examples |> List.map (snd >> List.rev) |> List.tailOrEmpty

        let notes = notes |> List.map (snd >> List.rev) |> List.tailOrEmpty
        //let exceptions = exceptions |> List.collect (snd >> List.rev) |> List.tailOrEmpty
        //let parameters = parameters |> List.collect (snd >> List.rev) |> List.tailOrEmpty

        // All unclassified things go in 'remarks'
        let remarks =
            (remarks |> List.collect (snd >> List.rev) |> List.tailOrEmpty)
            @ (rest |> List.collect (snd >> List.rev))

        let summary = ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = summary)), None)

        let remarks =
            if remarks.IsEmpty then
                None
            else
                Some(ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = remarks)), None))
        //let exceptions = [ for e in exceptions -> ApiDocHtml(Literate.ToHtml(doc.With(paragraphs=[e]))) ]
        let notes = [ for e in notes -> ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = e)), None) ]

        let examples = [ for e in examples -> ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = e)), None) ]

        let returns =
            if returns.IsEmpty then
                None
            else
                Some(ApiDocHtml(Literate.ToHtml(doc.With(paragraphs = returns)), None))

        ApiDocComment(
            xmldoc = Some el,
            summary = summary,
            remarks = remarks,
            parameters = [],
            returns = returns,
            examples = examples,
            notes = notes,
            exceptions = [],
            rawData = raw
        )

    /// Tries to parse a <c>[key:value]</c> command embedded inside an XML doc text node.
    /// Returns <c>Some (key, value)</c> on success or <c>None</c> if the text is not a command.
    let findCommand cmd =
        match cmd with
        | StringPosition.StartsWithWrapped ("[", "]") (ParseCommand(k, v), _rest) -> Some(k, v)
        | _ -> None

    /// Wraps the summary content in a <pre> tag if it is multiline and has different column indentations.
    let readXmlElementAsSingleSummary (e: XElement) =
        let text = e.Value

        let nonEmptyLines =
            e.Value.Split([| '\n' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.filter (String.IsNullOrWhiteSpace >> not)

        if nonEmptyLines.Length = 1 then
            nonEmptyLines.[0]
        else
            let allLinesHaveSameColumn =
                nonEmptyLines
                |> Array.map (fun line -> line.Length - line.TrimStart([| ' ' |]).Length)
                |> Array.distinct
                |> Array.length
                |> (=) 1

            let trimmed = text.TrimStart([| '\n'; '\r' |]).TrimEnd()

            if allLinesHaveSameColumn then
                trimmed
            else
                $"<pre>%s{trimmed}</pre>"

    /// Recursively converts an <see cref="T:System.Xml.Linq.XElement"/> to an HTML string,
    /// resolving <c>&lt;see cref="…"/&gt;</c> references via <paramref name="urlMap"/> and
    /// collecting any embedded <c>[key:value]</c> commands into <paramref name="cmds"/>.
    /// When <paramref name="anyTagsOK"/> is <c>true</c>, unknown XML elements are passed
    /// through as raw HTML; otherwise they are silently dropped.
    let rec readXmlElementAsHtml
        anyTagsOK
        (urlMap: CrossReferenceResolver)
        (cmds: IDictionary<_, _>)
        (html: StringBuilder)
        (e: XElement)
        =
        for x in e.Nodes() do
            if x.NodeType = XmlNodeType.Text then
                let text = (x :?> XText).Value

                match findCommand (text, MarkdownRange.zero) with
                | Some(k, v) -> cmds.Add(k, v)
                | None -> html.Append(HttpUtility.HtmlEncode text) |> ignore
            elif x.NodeType = XmlNodeType.Element then
                let elem = x :?> XElement

                match elem.Name.LocalName with
                | "list" ->
                    html.Append("<ul>") |> ignore
                    readXmlElementAsHtml anyTagsOK urlMap cmds html elem
                    html.Append("</ul>") |> ignore
                | "item" ->
                    html.Append("<li>") |> ignore
                    readXmlElementAsHtml anyTagsOK urlMap cmds html elem
                    html.Append("</li>") |> ignore
                | "para" ->
                    html.Append("<p class='fsdocs-para'>") |> ignore
                    readXmlElementAsHtml anyTagsOK urlMap cmds html elem
                    html.Append("</p>") |> ignore
                | "paramref" ->
                    let name = elem.Attribute(XName.Get "name")
                    let nameAsHtml = HttpUtility.HtmlEncode name.Value

                    if not (isNull name) then
                        html.AppendFormat("<span class=\"fsdocs-param-name\">{0}</span>", nameAsHtml)
                        |> ignore
                | "see"
                | "seealso" ->
                    let cref = elem.Attribute(XName.Get "cref")

                    if not (isNull cref) then
                        if System.String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
                            printfn "ignoring invalid cref specified in: %A" e

                        // Older FSharp.Core cref listings don't start with "T:", see https://github.com/dotnet/fsharp/issues/9805
                        let cname = cref.Value

                        let cname = if cname.Contains(":") then cname else "T:" + cname

                        match urlMap.ResolveCref cname with
                        | Some reference ->
                            html.AppendFormat("<a href=\"{0}\">{1}</a>", reference.ReferenceLink, reference.NiceName)
                            |> ignore
                        | _ ->
                            urlMap.ResolveCref cname |> ignore
                            let crefAsHtml = HttpUtility.HtmlEncode cref.Value
                            html.Append(crefAsHtml) |> ignore
                | "c" ->
                    html.Append("<code>") |> ignore

                    let code = elem.Value.TrimEnd('\r', '\n', ' ')
                    let codeAsHtml = HttpUtility.HtmlEncode code
                    html.Append(codeAsHtml) |> ignore

                    html.Append("</code>") |> ignore
                | "code" ->
                    let code =
                        let code = Literate.ParseMarkdownString("```\n" + elem.Value.TrimEnd('\r', '\n', ' ') + "\n```")
                        Literate.ToHtml(code, lineNumbers = false)

                    html.Append(code) |> ignore
                // 'a' is not part of the XML doc standard but is widely used
                | "a" -> html.Append(elem.ToString()) |> ignore
                // This allows any HTML to be transferred through
                | _ ->
                    if anyTagsOK then
                        let elemAsXml = elem.ToString()
                        html.Append(elemAsXml) |> ignore

    /// Active pattern that matches a <c>&lt;summary&gt;</c> element that contains only text
    /// (no child elements). Used to take a fast path that avoids the full HTML builder.
    let (|SummaryWithoutChildren|_|) (e: XElement) =
        if e.Name.LocalName = "summary" && not e.HasElements then
            Some e
        else
            None

    /// Core function that processes a standard C#/F# XML documentation element (the root
    /// <c>&lt;member …&gt;</c> or <c>&lt;doc&gt;</c> element) into an
    /// <see cref="T:FSharp.Formatting.ApiDocs.ApiDocComment"/> plus an optional list of
    /// <c>&lt;namespacedoc&gt;</c> sub-elements (for namespace-level summaries).
    /// When <paramref name="summaryExpected"/> is <c>true</c>, a missing <c>&lt;summary&gt;</c>
    /// child is treated as raw HTML content.
    let readXmlCommentAsHtmlAux
        summaryExpected
        (urlMap: CrossReferenceResolver)
        (doc: XElement)
        (cmds: IDictionary<_, _>)
        =
        let rawData = new Dictionary<string, string>()
        // not part of the XML doc standard
        let nsels =
            let ds = doc.Elements(XName.Get "namespacedoc")

            if Seq.length ds > 0 then Some(Seq.toList ds) else None

        let summary =
            match Seq.tryExactlyOne (doc.Elements()) with
            | Some(SummaryWithoutChildren e) -> ApiDocHtml(readXmlElementAsSingleSummary e, None)
            | Some _
            | None ->
                if summaryExpected then
                    let summaries = doc.Elements(XName.Get "summary") |> Seq.toList

                    let html = new StringBuilder()

                    for (id, e) in List.indexed summaries do
                        let n = if id = 0 then "summary" else "summary-" + string<int> id

                        rawData.[n] <- e.Value
                        readXmlElementAsHtml true urlMap cmds html e

                    ApiDocHtml(html.ToString(), None)
                else
                    let html = new StringBuilder()
                    readXmlElementAsHtml false urlMap cmds html doc
                    ApiDocHtml(html.ToString(), None)

        let paramNodes = doc.Elements(XName.Get "param") |> Seq.toList

        let parameters =
            [ for e in paramNodes do
                  let paramName = e.Attribute(XName.Get "name").Value
                  let phtml = new StringBuilder()
                  readXmlElementAsHtml true urlMap cmds phtml e
                  let paramHtml = ApiDocHtml(phtml.ToString(), None)
                  paramName, paramHtml ]

        for e in doc.Elements(XName.Get "exclude") do
            cmds.["exclude"] <- e.Value

        for e in doc.Elements(XName.Get "omit") do
            cmds.["omit"] <- e.Value

        for e in doc.Elements(XName.Get "category") do
            match e.Attribute(XName.Get "index") with
            | null -> ()
            | a -> cmds.["categoryindex"] <- a.Value

            cmds.["category"] <- e.Value

        let remarks =
            let remarkNodes = doc.Elements(XName.Get "remarks") |> Seq.toList

            if not (List.isEmpty remarkNodes) then
                let html = new StringBuilder()

                for (id, e) in List.indexed remarkNodes do
                    let n = if id = 0 then "remarks" else "remarks-" + string<int> id

                    rawData.[n] <- e.Value
                    readXmlElementAsHtml true urlMap cmds html e

                ApiDocHtml(html.ToString(), None) |> Some
            else
                None

        let returns =
            let html = new StringBuilder()

            let returnNodes = doc.Elements(XName.Get "returns") |> Seq.toList

            if returnNodes.Length > 0 then
                for (id, e) in List.indexed returnNodes do
                    let n = if id = 0 then "returns" else "returns-" + string<int> id

                    rawData.[n] <- e.Value
                    readXmlElementAsHtml true urlMap cmds html e

                Some(ApiDocHtml(html.ToString(), None))
            else
                None

        let exceptions =
            let exceptionNodes = doc.Elements(XName.Get "exception") |> Seq.toList

            [ for e in exceptionNodes do
                  let cref = e.Attribute(XName.Get "cref")

                  if not (isNull cref) then
                      if String.IsNullOrEmpty(cref.Value) || cref.Value.Length < 3 then
                          printfn "Warning: Invalid cref specified in: %A" doc

                      else
                          // FSharp.Core cref listings don't start with "T:", see https://github.com/dotnet/fsharp/issues/9805
                          let cname = cref.Value

                          let cname =
                              if cname.StartsWith("T:", StringComparison.Ordinal) then
                                  cname
                              else
                                  "T:" + cname // FSharp.Core exception listings don't start with "T:"

                          match urlMap.ResolveCref cname with
                          | Some reference ->
                              let html = new StringBuilder()
                              let referenceLinkId = "exception-" + reference.NiceName
                              rawData.[referenceLinkId] <- reference.ReferenceLink
                              readXmlElementAsHtml true urlMap cmds html e
                              reference.NiceName, Some reference.ReferenceLink, ApiDocHtml(html.ToString(), None)
                          | _ ->
                              let html = new StringBuilder()
                              readXmlElementAsHtml true urlMap cmds html e
                              cname, None, ApiDocHtml(html.ToString(), None) ]

        let examples =
            let exampleNodes = doc.Elements(XName.Get "example") |> Seq.toList

            [ for (id, e) in List.indexed exampleNodes do
                  let html = new StringBuilder()

                  let exampleId =
                      match e.TryAttr "id" with
                      | None -> if id = 0 then "example" else "example-" + string<int> id
                      | Some attrId -> attrId

                  rawData.[exampleId] <- e.Value
                  readXmlElementAsHtml true urlMap cmds html e
                  ApiDocHtml(html.ToString(), Some exampleId) ]

        let notes =
            let noteNodes = doc.Elements(XName.Get "note") |> Seq.toList
            // 'note' is not part of the XML doc standard but is supported by Sandcastle and other tools
            [ for (id, e) in List.indexed noteNodes do
                  let html = new StringBuilder()

                  let n = if id = 0 then "note" else "note-" + string<int> id

                  rawData.[n] <- e.Value
                  readXmlElementAsHtml true urlMap cmds html e
                  ApiDocHtml(html.ToString(), None) ]

        // put the non-xmldoc sections into rawData
        doc.Descendants()
        |> Seq.filter (fun n ->
            let ln = n.Name.LocalName

            ln <> "summary"
            && ln <> "param"
            && ln <> "exceptions"
            && ln <> "example"
            && ln <> "note"
            && ln <> "returns"
            && ln <> "remarks")
        |> Seq.groupBy (fun n -> n.Name.LocalName)
        |> Seq.iter (fun (n, lst) ->
            let lst = Seq.toList lst

            match lst with
            | [ x ] -> rawData.[n] <- x.Value
            | lst -> lst |> List.iteri (fun id el -> rawData.[n + "-" + string<int> id] <- el.Value))

        let rawData = rawData |> Seq.toList

        let comment =
            ApiDocComment(
                xmldoc = Some doc,
                summary = summary,
                remarks = remarks,
                parameters = parameters,
                returns = returns,
                examples = examples,
                notes = notes,
                exceptions = exceptions,
                rawData = rawData
            )

        comment, nsels

    /// Concatenates the HTML text of two <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> values,
    /// separated by a newline.
    let combineHtml (h1: ApiDocHtml) (h2: ApiDocHtml) =
        ApiDocHtml(String.concat "\n" [ h1.HtmlText; h2.HtmlText ], None)

    /// Combines two optional <see cref="T:FSharp.Formatting.ApiDocs.ApiDocHtml"/> values:
    /// returns the non-<c>None</c> side, or concatenates both when both are present.
    let combineHtmlOptions (h1: ApiDocHtml option) (h2: ApiDocHtml option) =
        match h1, h2 with
        | x, None -> x
        | None, x -> x
        | Some x, Some y -> Some(combineHtml x y)

    /// Merges two <see cref="T:FSharp.Formatting.ApiDocs.ApiDocComment"/> values by
    /// concatenating their HTML sections (summary, remarks, parameters, examples, etc.).
    /// Used when a symbol has documentation spread across multiple XML doc elements.
    let combineComments (c1: ApiDocComment) (c2: ApiDocComment) =
        ApiDocComment(
            xmldoc =
                (match c1.Xml with
                 | None -> c2.Xml
                 | v -> v),
            summary = combineHtml c1.Summary c2.Summary,
            remarks = combineHtmlOptions c1.Remarks c2.Remarks,
            parameters = c1.Parameters @ c2.Parameters,
            examples = c1.Examples @ c2.Examples,
            returns = combineHtmlOptions c1.Returns c2.Returns,
            notes = c1.Notes @ c2.Notes,
            exceptions = c1.Exceptions @ c2.Exceptions,
            rawData = c1.RawData @ c2.RawData
        )

    /// Reduces a list of optional <see cref="T:FSharp.Formatting.ApiDocs.ApiDocComment"/> values
    /// (namespace-level doc fragments) by combining all non-<c>None</c> entries into one.
    /// Returns <c>None</c> when the list is empty or all entries are <c>None</c>.
    let combineNamespaceDocs nspDocs =
        nspDocs
        |> List.choose id
        |> function
            | [] -> None
            | xs -> Some(List.reduce combineComments xs)

    /// Top-level XML doc reader: parses a <c>&lt;member&gt;</c> element (or similar) into an
    /// <see cref="T:FSharp.Formatting.ApiDocs.ApiDocComment"/> and a separate namespace-doc
    /// comment extracted from any embedded <c>&lt;namespacedoc&gt;</c> elements.
    let rec readXmlCommentAsHtml (urlMap: CrossReferenceResolver) (doc: XElement) (cmds: IDictionary<_, _>) =
        let doc, nsels = readXmlCommentAsHtmlAux true urlMap doc cmds

        let nsdocs = readNamespaceDocs urlMap nsels
        doc, nsdocs

    /// Reads the contents of <c>&lt;namespacedoc&gt;</c> elements into a combined namespace
    /// <see cref="T:FSharp.Formatting.ApiDocs.ApiDocComment"/>, or returns <c>None</c> when
    /// the list is empty.
    and readNamespaceDocs (urlMap: CrossReferenceResolver) (nsels: XElement list option) =
        let nscmds = Dictionary() :> IDictionary<_, _>

        nsels
        |> Option.map (
            List.map (fun n -> fst (readXmlCommentAsHtml urlMap n nscmds))
            >> List.reduce combineComments
        )
