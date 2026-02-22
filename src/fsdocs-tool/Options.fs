namespace fsdocs

module Common =

    let evalString s =
        if System.String.IsNullOrEmpty s then None else Some s

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

    let evalPairwiseStrings (a: string array) =
        match Array.tryExactlyOne a with
        | Some v when System.String.IsNullOrWhiteSpace v -> None
        | _ -> a |> pairs |> List.ofSeq |> Some

    let evalPairwiseStringsNoOption (a: string array) =
        evalPairwiseStrings a |> Option.defaultValue []

    let concat a =
        let s = String.concat " " a
        if s = " " then "" else s

    let waitForKey b =
        if b then
            printf "\nPress Ctrl+C to stop ..."
            let exiting = new System.Threading.ManualResetEventSlim(false)

            System.Console.CancelKeyPress.AddHandler(fun _ ea ->
                ea.Cancel <- true
                exiting.Set())

            exiting.Wait()
