namespace FSharp.Formatting.CommandTool

module Common =

    let evalString s = if s = "" then None else Some s

    let evalStrings a =
        match Seq.tryExactlyOne a with
        | Some "" -> None
        | _ -> Some (List.ofSeq a)

    let evalPairwiseStrings a =
        match Seq.tryExactlyOne a with
        | Some "" -> None
        | _ -> a |> Seq.pairwise |> List.ofSeq |> Some

    let evalPairwiseStringsNoOption a =
        evalPairwiseStrings a |> Option.defaultValue []

    let concat a =
        let s = String.concat " " a
        if s = " " then "" else s

    let waitForKey b =
        if b then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore