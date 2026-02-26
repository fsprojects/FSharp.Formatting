namespace fsdocs

module Common =

    /// Returns <c>Some s</c> if the string is non-empty/non-null, otherwise <c>None</c>.
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

    /// Interprets a flat string array as key-value pairs (alternating key, value).
    /// Returns <c>None</c> if the array contains a single whitespace-only string; otherwise
    /// returns <c>Some</c> of the paired list.
    let evalPairwiseStrings (a: string array) =
        match Array.tryExactlyOne a with
        | Some v when System.String.IsNullOrWhiteSpace v -> None
        | _ -> a |> pairs |> List.ofSeq |> Some

    /// Like <see cref="evalPairwiseStrings"/> but returns an empty list instead of <c>None</c>.
    let evalPairwiseStringsNoOption (a: string array) =
        evalPairwiseStrings a |> Option.defaultValue []

    /// Joins the given strings with a space separator, returning an empty string when the
    /// result would be a single space.
    let concat a =
        let s = String.concat " " a
        if s = " " then "" else s

    /// When <paramref name="b"/> is true, waits indefinitely for Ctrl+C before returning.
    /// Useful for keeping a watch server alive in the foreground.
    let waitForKey b =
        if b then
            printf "\nPress Ctrl+C to stop ..."
            let exiting = new System.Threading.ManualResetEventSlim(false)

            System.Console.CancelKeyPress.AddHandler(fun _ ea ->
                ea.Cancel <- true
                exiting.Set())

            exiting.Wait()
