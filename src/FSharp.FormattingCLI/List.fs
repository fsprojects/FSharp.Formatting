module List

let contains l e = 
    let ll = l |> List.filter (fun s -> s = e)
    if ll.Length > 0 then true
    else false

let rec pairwise l =
    match l with
    | [] | [_] -> []
    | h1 :: ((h2 :: _) as t) -> (h1, h2) :: pairwise t