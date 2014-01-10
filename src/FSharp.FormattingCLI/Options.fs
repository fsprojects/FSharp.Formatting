namespace Options

//open System.Collections
open System.Collections.Generic
//open System.Reflection
open Microsoft.FSharp.Reflection
open CommandLine
open CommandLine.Text


//type OptionListItem =
//    | String of string
//    | StringArray of string[]
//    | Bool of bool
//
//
//type OptionList = List<OptionListItem>

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

//    /// http://stackoverflow.com/questions/2920094/how-can-i-convert-between-f-list-and-f-tuple
//    let listToTuple l =
//        let l' = List.toArray l
//        let types = l' |> Array.map (fun o -> o.GetType())
//        let tupleType = Microsoft.FSharp.Reflection.FSharpType.MakeTupleType types
//        Microsoft.FSharp.Reflection.FSharpValue.MakeTuple (l' , tupleType)

//    let f_validStringOption (s: OptionListItem) =
//        match s with 
//        | String(_) -> if s = OptionListItem.String("") then false else true
//        | _ -> false
//
//    let f_validStringArrayOption (a: OptionListItem) =
//        match a with 
//        | StringArray(_) -> if a = OptionListItem.StringArray([|""|]) then false else true
//        | _ -> false
//
//    let f_addBoolOption (b: OptionListItem) = 
//        match b with
//        | Bool(_) -> true
//        | _ -> false
//
//    let check (f : OptionListItem -> bool) (o: OptionListItem) (l: OptionList) : OptionList = 
//        let mutable l2 = l
//        if (f o) then (l2.Add o); l2
//        else l
//
//    let mapToObj (l: OptionList) = List.map (fun x -> (x :> obj)) (List.ofSeq l) |> List.ofSeq

    let evalString s =
        if s = "" then None
        else Some s

    let evalStringArray a =
        if a = [|""|] then None
        else Some (List.ofArray a)

    let concat (a) =
        let mutable s = ""
        for i in a do s <- (sprintf "%s %s" s i)
        if s = " " then s <- ""
        s 

    let waitForKey b =
        if b then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore