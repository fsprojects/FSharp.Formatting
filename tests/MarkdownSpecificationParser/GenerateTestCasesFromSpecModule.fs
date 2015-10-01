module MarkdownSpecificationParser.GenerateTestCasesFromSpecModule

let specHtmlFile = "tests/commonmark_spec.htm"
let findTestCasesRegexString = """<pre><code class="language-markdown">(?<markdown>(((?!(>|<))(\w|\W|\s))|(<span class="space"> </span>))*)</code></pre>(\w|(?!(>|<))\W|\s)*<pre><code class="language-html">(?<html>(((?!(>|<))(\w|\W|\s))|(<span class="space"> </span>))*)</code></pre>"""
let findTestCasesRegex = System.Text.RegularExpressions.Regex(findTestCasesRegexString)
let getTestCases specHtmlFile =
  let specHtml = System.IO.File.ReadAllText(specHtmlFile)

  let matches = findTestCasesRegex.Matches(specHtml)
  let normalizeString (s:string) =
    s.Replace("→", "\t")
     .Replace("<span class=\"space\"> </span>", " ")

  seq {
    for g in matches do
      if g.Success then
        let markdown = g.Groups.["markdown"].Value
        let html = g.Groups.["html"].Value
        yield normalizeString markdown, System.Net.WebUtility.HtmlDecode(normalizeString html)
  }

let toFSharpStringLiteral (s:string) =
  sprintf "\"%s\"" (s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r"))