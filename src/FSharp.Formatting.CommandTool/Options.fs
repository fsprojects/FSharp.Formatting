namespace FSharp.Formatting.Options

open System.Collections.Generic
open Microsoft.FSharp.Reflection
open CommandLine
open CommandLine.Text

module Common =
    let parsingErrorMessage (errors: IList<ParsingError>) = 
        let mutable res = ""
        try
            for i in errors do
                if i.ViolatesFormat then res <- res + (sprintf "invalid format of option '%s'\n" i.BadOption.LongName)
                if i.ViolatesMutualExclusiveness then res <- res + (sprintf "mutually exclusive option '%s'\n" i.BadOption.LongName)
                if i.ViolatesRequired then res <- res + (sprintf "missing required option '%s' or invalid option value\n" i.BadOption.LongName)
        with 
        | ex -> ignore ex
        res

    let evalString s =
        if s = "" then None
        else Some s

    let evalStringArray a =
        if a = [|""|] then None
        else Some (List.ofArray a)

    let evalPairwiseStringArray a =
        if a = [|""|] then None
        else Some (a |> Seq.pairwise |> Array.ofSeq |> List.ofArray)

    let concat (a) =
        let mutable s = ""
        for i in a do s <- (sprintf "%s %s" s i)
        if s = " " then s <- ""
        s 

    let waitForKey b =
        if b then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore