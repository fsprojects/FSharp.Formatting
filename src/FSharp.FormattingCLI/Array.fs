module Array

let contains a e = 
    let A = a |> Array.filter (fun s -> s = e)
    if A.Length > 0 then true
    else false


let rec pairwise (a: array<_>) =
    if a.Length < 2 then [||]
    else (Array.append [|(a.[0], a.[1])|] (pairwise (Array.sub a 2 (a.Length-2))))
