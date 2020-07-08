namespace FSharp.Formatting.CommandTool

module Common =

    let evalString s =
        if s = "" then None
        else Some s

    let evalStrings a =
        let a = Seq.toArray a
        if a = [|""|] then None
        else Some (List.ofArray a)

    let evalPairwiseStrings a =
        let a = Seq.toArray a
        if a = [|""|] then None
        else Some (a |> Seq.pairwise |> Array.ofSeq |> List.ofArray)

    let concat a =
        let mutable s = ""
        for i in a do s <- (sprintf "%s %s" s i)
        if s = " " then s <- ""
        s 

    let waitForKey b =
        if b then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore