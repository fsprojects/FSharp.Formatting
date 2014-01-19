module Array

let contains a e = 
    let A = a |> Array.filter (fun s -> s = e)
    if A.Length > 0 then true
    else false