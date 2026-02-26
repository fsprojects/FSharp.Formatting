namespace fsdocs

/// Shared utility functions for the fsdocs command-line tool
module Common =

    /// Returns Some s when s is non-empty, otherwise None
    let evalString s =
        if System.String.IsNullOrEmpty s then None else Some s

    // https://stackoverflow.com/questions/4126351
    /// Yields successive pairs from a sequence: (item0, item1), (item2, item3), …
    let private pairs (xs: _ seq) =
        seq {
            use enumerator = xs.GetEnumerator()

            while enumerator.MoveNext() do
                let first = enumerator.Current

                if enumerator.MoveNext() then
                    let second = enumerator.Current
                    yield first, second
        }

    /// Interprets an array as key/value pairs; returns None when the array is blank or empty
    let evalPairwiseStrings (a: string array) =
        match Array.tryExactlyOne a with
        | Some v when System.String.IsNullOrWhiteSpace v -> None
        | _ -> a |> pairs |> List.ofSeq |> Some

    /// Like <see cref="evalPairwiseStrings"/> but returns an empty list instead of None
    let evalPairwiseStringsNoOption (a: string array) =
        evalPairwiseStrings a |> Option.defaultValue []

    /// Joins an array of strings with spaces, returning "" when the result is a single space
    let concat a =
        let s = String.concat " " a
        if s = " " then "" else s

    /// If b is true, prints a prompt and blocks until the user presses Ctrl+C
    let waitForKey b =
        if b then
            printf "\nPress Ctrl+C to stop ..."
            let exiting = new System.Threading.ManualResetEventSlim(false)

            System.Console.CancelKeyPress.AddHandler(fun _ ea ->
                ea.Cancel <- true
                exiting.Set())

            exiting.Wait()
