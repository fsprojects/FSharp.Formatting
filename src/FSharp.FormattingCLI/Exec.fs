﻿module Exec

open Options
open Options.Literate
open Options.MetadataFormat
open Log
open IExecutable
open AssemblyInfo

open CommandLine
open CommandLine.Text
//open System.IO
open System.Collections.Generic
open FSharp.Data
open FSharp.Data.Json
open FSharp.Data.Json.Extensions
//open FSharp.Markdown


/// configuration of the supported FSharp.Formatting functions 
let OptionsMapping = new Dictionary<string, IExecutable>(HashIdentity.Structural)
OptionsMapping.["literate--processdirectory"] <- (ProcessDirectoryOptions() :> IExecutable)
OptionsMapping.["metadataformat--generate"] <- (GenerateOptions() :> IExecutable)


/// Execution environment
type Env (argv: string []) = 
    /// 1st level verb commands for use in combination with second level command option,
    /// options are case insensitive;
    /// usage for example : 
    /// 'fsformatting literate --processdirectory [more options]'
    let verbCommandGroup = ["literate"; "metadataformat"]
    //let specialOptions = ["--help","--waitforkey"]

    // need this for corner cases
    let waitForKey = Options.Common.waitForKey
    let waitForKey () =
        if Array.contains (argv |> Array.map (fun s -> s.ToLower())) "--waitforkey" then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore

    let fullHelp() = 
        for o in OptionsMapping.Values do
            printfn "%s" (o.getUsage())

    let exit x = 
        if x = -1 then fullHelp()
        waitForKey()
        x

    let check f result = 
        if not result then false
        else f() 
     

    ///  execute the FSharp.Formatting method referred to by 'command'
    member x.execute(command, options) = 
        let mutable validArgs = false
        /// inform about verb command-specific parser error
        let informUser(o: IExecutable) = 
            printfn "\ncould not parse %A" argv
            printfn "%s" (o.getErrorText())
            printfn "%s" (o.getUsage())
        /// (redundant) check to make execute() a safe stand alone command
        if not (command |> OptionsMapping.ContainsKey) then 
            printfn "received invalid commands %s (concatenated)" command
            exit -1
        else 
            let commandOptions = OptionsMapping.[command]
            let parser = CommandLine.Parser.Default
            try    
                validArgs <- parser.ParseArguments(options, commandOptions)
            with
                | ex -> Log.error (sprintf "received 'CommandLine' parser exception. %s" (ex.ToString()))
            match validArgs with
            | true -> commandOptions.execute() 
            | false -> 
                informUser commandOptions
                if Array.contains options "--help" then exit 0 
                else -1 

    /// handle corner cases, dispatch processing,
    /// print help text as necessary,
    /// return exit code 0/-1
    member x.run() = 
        if argv.Length < 1 then exit -1
        /// dispatch single command line argument
            // we may need platform specific argv handling,
            // on Linux, traditionally argv.[0] = 'program name'; probably not on Linux/Mono
        elif (argv.Length = 1) then 
            match argv.[0] with
            | "--help" -> fullHelp(); exit 0
            | "--version" -> printfn "\nfsformatting version %s" AssemblyInfo.assemblyVersion; exit 0
            | _ -> exit -1
        /// dispatch verb commands, combined 1st and 2nd level
        else 
            let options = (Array.sub argv 2 (argv.Length-2))
            /// combined verb command must be in first place,
            let combinedVerbCommand = (argv.[0]).ToLower()+(argv.[1]).ToLower()
            if not (combinedVerbCommand |> OptionsMapping.ContainsKey) then 
                printfn "received invalid commands %s %s" argv.[0] argv.[1]
                exit -1
            else 
                x.execute(combinedVerbCommand, options)
 
            
