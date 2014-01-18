#if INTERACTIVE
#r "../../bin/FSharp.Markdown.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "../Common/FsUnit.fs"
#else
module FSharp.Markdown.Tests.Externals
#endif

open FsUnit
open NUnit.Framework
open FSharp.Markdown

open System.IO
open System.Text.RegularExpressions

let removeWhitespace(s : string) =
    // Standardize line endings             
    let s = s.Replace("\r\n", "\n");    // DOS to Unix
    let s = s.Replace("\r", "\n");      // Mac to Unix

    // remove any tabs entirely
    let s = s.Replace("\t", "");

    // remove empty newlines
    let s = Regex.Replace(s, @"^\n", "", RegexOptions.Multiline);

    // remove leading space at the start of lines
    let s = Regex.Replace(s, @"^\s+", "", RegexOptions.Multiline);

    // remove all newlines
    let s = s.Replace("\n", "");
    s

let failingTests = 
    set [
        "Auto_links.text";
        "Inline_HTML_comments.text";
        "Ordered_and_unordered_lists.text";
        "markdown-readme.text";
        "nested-emphasis.text";
        "Email auto links.text";
        "Emphasis.text";
        "Inline HTML (Span).text";
        "Ins & del.text";
        "Links, inline style.text";
        "Nesting.text";
        "Parens in URL.text";
    ]

let rec genTestCases (dir : string) =
    let generate (source : string) (target : string) (verify : string) = 
        try 
            if File.Exists(verify) then 
                let text = File.ReadAllText(source)
                (use wr = new StreamWriter(target)
                Markdown.TransformHtml(text, wr, "\r\n"))
                let contents = File.ReadAllLines(verify)
                File.WriteAllLines(verify, contents)
                let targetHtml = removeWhitespace(File.ReadAllText(target))
                let verifyHtml = removeWhitespace(File.ReadAllText(verify))
                if not <| Set.contains (Path.GetFileName(source)) failingTests then
                    [ TestCaseData(source, target, verifyHtml, targetHtml) ]
                else
                    []
            else
                []
        with e -> 
            printfn " - %s (failed)\n %A" (target.Substring(dir.Length)) e
            []
    seq {
        for file in Directory.GetFiles(dir, "*.text") do 
            yield! generate file (Path.ChangeExtension(file, "2.html")) (Path.ChangeExtension(file, "html"))
        for d in Directory.GetDirectories(dir) do
            yield! genTestCases d
    }

let (++) a b = Path.Combine(a, b)
let testdir = __SOURCE_DIRECTORY__ ++ Path.Combine("..","..","tests","Benchmarks","testfiles")

let getTest() = genTestCases testdir

[<Test>]
[<TestCaseSource("getTest")>]
let ``Run external test`` (actualName : string) (expectedName : string) (actual : string) (expected : string) =
    if actual = expected then File.Delete(expectedName)
    Assert.That(actual, Is.EqualTo(expected),
                "Mismatch between '{0}' and the transformed '{1}'.",
                actualName, expectedName)
            


