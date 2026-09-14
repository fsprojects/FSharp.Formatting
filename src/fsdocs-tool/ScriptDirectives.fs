/// The files a script depends on through its hash directives.
module fsdocs.ScriptDirectives

open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

/// A string argument, resolved against the folder of the script
[<return: Struct>]
let (|LocalPath|_|) (folder: string) (arg: ParsedHashDirectiveArgument) =
    match arg with
    | ParsedHashDirectiveArgument.String(value = value) ->
        try
            ValueSome(Path.GetFullPath(Path.Combine(folder, value)))
        with _ ->
            ValueNone
    | _ -> ValueNone

/// The files of a '#load', whether they exist or not
[<return: Struct>]
let (|Load|_|) (folder: string) (ParsedHashDirective(ident, args, _)) =
    if ident <> "load" then
        ValueNone
    else
        args
        |> List.choose (function
            | LocalPath folder file -> Some file
            | _ -> None)
        |> ValueSome

/// The existing local files of a '#r': the other forms ('nuget:', assembly names) are not
/// files the site can watch
[<return: Struct>]
let (|Reference|_|) (folder: string) (ParsedHashDirective(ident, args, _)) =
    if ident <> "r" then
        ValueNone
    else
        args
        |> List.choose (function
            | LocalPath folder file when File.Exists file -> Some file
            | _ -> None)
        |> ValueSome

let dependenciesOf (path: string) (text: string) : string list =
    let folder = Path.GetDirectoryName path

    try
        let parsed =
            FSharp.Formatting.Internal.CompilerServiceExtensions.FSharpAssemblyHelper.checker.ParseFile(
                path,
                SourceText.ofString text,
                { FSharpParsingOptions.Default with
                    SourceFiles = [| path |]
                    IsInteractive = true
                }
            )
            |> Async.RunSynchronously

        let directives =
            match parsed.ParseTree with
            | ParsedInput.SigFile _ -> []
            | ParsedInput.ImplFile(ParsedImplFileInput(hashDirectives = directives; contents = modules)) ->
                [
                    yield! directives
                    for SynModuleOrNamespace(decls = decls) in modules do
                        for decl in decls do
                            match decl with
                            | SynModuleDecl.HashDirective(hashDirective = directive) -> yield directive
                            | _ -> ()
                ]

        [
            for directive in directives do
                match directive with
                | Load folder files
                | Reference folder files -> yield! files
                | _ -> ()
        ]
        |> List.distinct
    with _ ->
        []
