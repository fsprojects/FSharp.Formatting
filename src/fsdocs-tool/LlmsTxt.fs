namespace fsdocs

open FSharp.Formatting.ApiDocs

/// Helpers for generating llms.txt and llms-full.txt content.
module internal LlmsTxt =

    // Compiled once at module load; reused across all llms.txt page entries.
    let private multipleNewlinesRegex =
        System.Text.RegularExpressions.Regex(@"\n{3,}", System.Text.RegularExpressions.RegexOptions.Compiled)

    let private whitespaceRunRegex =
        System.Text.RegularExpressions.Regex(@"\s+", System.Text.RegularExpressions.RegexOptions.Compiled)

    /// Decode HTML entities (e.g. &quot; → ", &gt; → >) in a string.
    let private decodeHtml (s: string) = System.Net.WebUtility.HtmlDecode(s)

    /// Strip FSharp.Formatting --eval warning lines from content.
    let private stripEvalWarnings (s: string) =
        s.Split('\n')
        |> Array.filter (fun line ->
            not (
                line
                    .TrimStart()
                    .StartsWith(
                        "Warning: Output, it-value and value references require --eval",
                        System.StringComparison.Ordinal
                    )
            ))
        |> String.concat "\n"

    /// Collapse three or more consecutive newlines into at most two.
    let private collapseBlankLines (s: string) =
        multipleNewlinesRegex.Replace(s, "\n\n")

    /// Normalise a title: trim and collapse internal whitespace/newlines to a single space.
    let private normaliseTitle (s: string) =
        whitespaceRunRegex.Replace(s.Trim(), " ")

    /// Decode HTML entities and remove --eval noise from content.
    let private cleanContent (s: string) =
        s |> decodeHtml |> stripEvalWarnings |> collapseBlankLines |> fun t -> t.Trim()

    /// Build a section of llms.txt from a set of search index entries.
    /// When <c>withContent</c> is true, entry content is appended under a heading per entry.
    /// When false, entries are listed as bullet-point links (index format).
    /// <c>uriTransform</c> is applied to each entry's URI before rendering.
    let buildSection
        sectionTitle
        (entries: ApiDocsSearchIndexEntry array)
        withContent
        (uriTransform: string -> string)
        =
        if entries.Length = 0 then
            ""
        else
            let sb = System.Text.StringBuilder()
            sb.Append(sprintf "## %s\n\n" sectionTitle) |> ignore

            for e in entries do
                let title = normaliseTitle e.title
                let uri = uriTransform e.uri

                if withContent then
                    sb.Append(sprintf "### [%s](%s)\n\n" title uri) |> ignore

                    if not (System.String.IsNullOrWhiteSpace(e.content)) then
                        sb.Append(cleanContent e.content) |> ignore
                        sb.Append("\n\n") |> ignore
                else
                    sb.Append(sprintf "- [%s](%s)\n" title uri) |> ignore

            sb.ToString()

    /// Returns a URI transformer that rewrites links to use .md when markdown output is available.
    /// <c>docContentUsesMarkdown</c> – doc pages were generated with a _template.md.
    /// <c>apiDocUsesMarkdown</c> – API reference was generated with GenerateMarkdownPhased
    ///   (URIs have no file extension; .md must be appended).
    let buildUriTransform (docContentUsesMarkdown: bool) (apiDocUsesMarkdown: bool) (entryType: string) =
        fun (uri: string) ->
            match entryType with
            | "content" when docContentUsesMarkdown ->
                if uri.EndsWith(".html", System.StringComparison.OrdinalIgnoreCase) then
                    uri.[.. uri.Length - 6] + ".md"
                else
                    uri
            | "apiDocs" when apiDocUsesMarkdown ->
                // In markdown mode InUrl="" so URIs have no extension; append .md.
                // Strip any #anchor before appending, then re-attach it.
                let hashIdx = uri.IndexOf('#')

                if hashIdx >= 0 then
                    uri.[.. hashIdx - 1] + ".md" + uri.[hashIdx..]
                else
                    uri + ".md"
            | _ -> uri

    /// Generate the text content of llms.txt (index) and llms-full.txt (with content).
    /// Returns a tuple of (llms.txt content, llms-full.txt content).
    /// When <c>docContentUsesMarkdown</c> is true, doc page links use .md extensions.
    /// When <c>apiDocUsesMarkdown</c> is true, API reference links use .md extensions.
    let buildContent
        (collectionName: string)
        (entries: ApiDocsSearchIndexEntry array)
        (docContentUsesMarkdown: bool)
        (apiDocUsesMarkdown: bool)
        =
        let contentEntries = entries |> Array.filter (fun e -> e.``type`` = "content")
        let apiEntries = entries |> Array.filter (fun e -> e.``type`` = "apiDocs")
        // For the index, exclude per-member entries (identified by a '#' anchor in the URI).
        let apiIndexEntries = apiEntries |> Array.filter (fun e -> not (e.uri.Contains("#")))
        let header = sprintf "# %s\n\n" collectionName

        let contentTransform = buildUriTransform docContentUsesMarkdown apiDocUsesMarkdown "content"
        let apiDocTransform = buildUriTransform docContentUsesMarkdown apiDocUsesMarkdown "apiDocs"

        let llmsTxt =
            header
            + buildSection "Docs" contentEntries false contentTransform
            + buildSection "API Reference" apiIndexEntries false apiDocTransform

        let llmsFullTxt =
            header
            + buildSection "Docs" contentEntries true contentTransform
            + buildSection "API Reference" apiEntries true apiDocTransform

        llmsTxt, llmsFullTxt
