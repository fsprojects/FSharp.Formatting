namespace FSharp.Formatting.CommandTool

module Common =

    let evalString s = if s = "" then None else Some s

    let evalStrings a =
        match Seq.tryExactlyOne a with
        | Some "" -> None
        | _ -> Some(List.ofSeq a)

    // https://stackoverflow.com/questions/4126351
    let private pairs (xs: _ seq) =
        seq {
            use enumerator = xs.GetEnumerator()

            while enumerator.MoveNext() do
                let first = enumerator.Current

                if enumerator.MoveNext() then
                    let second = enumerator.Current
                    yield first, second
        }

    let evalPairwiseStrings a =
        match Seq.tryExactlyOne a with
        | Some "" -> None
        | _ -> a |> pairs |> List.ofSeq |> Some

    let evalPairwiseStringsNoOption a = evalPairwiseStrings a |> Option.defaultValue []

    let concat a =
        let s = String.concat " " a
        if s = " " then "" else s

    let waitForKey b =
        if b then
            printf "\nPress any key to continue ..."
            System.Console.ReadKey() |> ignore
