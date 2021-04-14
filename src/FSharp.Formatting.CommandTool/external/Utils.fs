namespace Ionide2.ProjInfo

module internal Paths =
    /// provides the path to the `dotnet` binary running this library, duplicated from
    /// https://github.com/dotnet/sdk/blob/b91b88aec2684e3d2988df8d838d3aa3c6240a35/src/Cli/Microsoft.DotNet.Cli.Utils/Muxer.cs#L39
    let dotnetRoot =
        System
            .Diagnostics
            .Process
            .GetCurrentProcess()
            .MainModule
            .FileName

module internal CommonHelpers =

    let chooseByPrefix (prefix: string) (s: string) =
        if s.StartsWith(prefix) then
            Some(s.Substring(prefix.Length))
        else
            None

    let chooseByPrefix2 prefixes (s: string) =
        prefixes |> List.tryPick (fun prefix -> chooseByPrefix prefix s)

    let splitByPrefix (prefix: string) (s: string) =
        if s.StartsWith(prefix) then
            Some(prefix, s.Substring(prefix.Length))
        else
            None

    let splitByPrefix2 prefixes (s: string) =
        prefixes |> List.tryPick (fun prefix -> splitByPrefix prefix s)

module internal FscArguments =

    open CommonHelpers
    open Types
    open System.IO

    let outType rsp =
        match List.tryPick (chooseByPrefix "--target:") rsp with
        | Some "library" -> ProjectOutputType.Library
        | Some "exe" -> ProjectOutputType.Exe
        | Some v -> ProjectOutputType.Custom v
        | None -> ProjectOutputType.Exe // default if arg is not passed to fsc

    let private outputFileArg = [ "--out:"; "-o:" ]

    let private makeAbs (projDir: string) (f: string) =
        if Path.IsPathRooted f then
            f
        else
            Path.Combine(projDir, f)

    let outputFile projDir rsp =
        rsp |> List.tryPick (chooseByPrefix2 outputFileArg) |> Option.map (makeAbs projDir)

    let isCompileFile (s: string) =
        let isArg = s.StartsWith("-") && s.Contains(":")
        (not isArg) && (s.EndsWith(".fs") || s.EndsWith(".fsi") || s.EndsWith(".fsx"))

    let references =
        //TODO valid also --reference:
        List.choose (chooseByPrefix "-r:")

    let useFullPaths projDir (s: string) =
        match s |> splitByPrefix2 outputFileArg with
        | Some (prefix, v) -> prefix + (v |> makeAbs projDir)
        | None ->
            if isCompileFile s then
                s |> makeAbs projDir |> Path.GetFullPath
            else
                s

    let isTempFile (name: string) =
        let tempPath = System.IO.Path.GetTempPath()
        let s = name.ToLower()
        s.StartsWith(tempPath.ToLower())

    let isDeprecatedArg n =
        // TODO put in FCS
        (n = "--times") || (n = "--no-jit-optimize")

    let isSourceFile (file: string): (string -> bool) =
        if System.IO.Path.GetExtension(file) = ".fsproj" then
            isCompileFile
        else
            (fun n -> n.EndsWith ".cs")

module internal CscArguments =
    open CommonHelpers
    open System.IO
    open Types

    let private outputFileArg = [ "--out:"; "-o:" ]

    let private makeAbs (projDir: string) (f: string) =
        if Path.IsPathRooted f then
            f
        else
            Path.Combine(projDir, f)

    let isCompileFile (s: string) =
        let isArg = s.StartsWith("-") && s.Contains(":")
        (not isArg) && s.EndsWith(".cs")

    let useFullPaths projDir (s: string) =
        if isCompileFile s then
            s |> makeAbs projDir |> Path.GetFullPath
        else
            s

    let isSourceFile (file: string): (string -> bool) =
        if System.IO.Path.GetExtension(file) = ".csproj" then
            isCompileFile
        else
            (fun n -> n.EndsWith ".fs")

    let outputFile projDir rsp =
        rsp |> List.tryPick (chooseByPrefix2 outputFileArg) |> Option.map (makeAbs projDir)

    let outType rsp =
        match List.tryPick (chooseByPrefix "/target:") rsp with
        | Some "library" -> ProjectOutputType.Library
        | Some "exe" -> ProjectOutputType.Exe
        | Some v -> ProjectOutputType.Custom v
        | None -> ProjectOutputType.Exe // default if arg is not passed to fsc
