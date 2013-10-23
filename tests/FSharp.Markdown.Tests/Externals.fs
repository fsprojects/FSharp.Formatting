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
                [ TestCaseData(source, target, verifyHtml, targetHtml) ]
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
let testdir = __SOURCE_DIRECTORY__ ++ "..\\..\\tests\\Benchmarks\\testfiles\\"

let getTest() = genTestCases testdir

[<Test;Ignore>]
[<TestCaseSource("getTest")>]
let ``Run external tests`` (actualName : string) (expectedName : string) (actual : string) (expected : string) =
    if actual = expected then File.Delete(expectedName)  
    Assert.That(actual, Is.EqualTo(expected),
                "Mismatch between '{0}' and the transformed '{1}'.",
                actualName, expectedName)
            


