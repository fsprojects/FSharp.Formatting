#r @"..\..\..\bin\FSharp.Compiler.Service.dll"
#r @"..\..\..\bin\FSharp.CodeFormat.dll"


open FSharp.CodeFormat
open Microsoft.FSharp.Compiler.SourceCodeServices

let agent = CodeFormat.CreateAgent()

let formatSrc (source: string) =
    let snips, _errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
    CodeFormat.FormatCss(snips, "fstips")
    

let checker = FSharpChecker.Create()

let evalSrc (source:string) =
    CodeFormatAgent.processSourceCode(checker,"/somewhere/test.fsx", source.Trim(),None,None)
    |> Async.RunSynchronously
    |> function
       | None -> ([],[||])
       | Some (a,b) -> (a,b)

let sample = """
type Digraph<'n> when 'n : comparison =
  Map<'n, Set<'n>>

module Digraph =

    let addNode (n: 'n) (g: Digraph<'n>) : Digraph<'n> =
        match Map.tryFind n g with
        | None -> Map.add n Set.empty g
        | Some _ -> g

    let addEdge ((n1, n2): 'n * 'n) (g: Digraph<'n>) : Digraph<'n> =
        let g' =
          match Map.tryFind n2 g with
          | None -> addNode n2 g
          | Some _ -> g
        match Map.tryFind n1 g with
        | None -> Map.add n1 (Set.singleton n2) g'
        | Some ns -> Map.add n1 (Set.add n2 ns) g'

    let nodes (g: Digraph<'n>) =
        Map.fold (fun xs k _ -> k::xs) [] g

    let roots (g: Digraph<'n>) : 'n list=
        List.filter (fun n -> not (Map.exists (fun _ v -> Set.contains n v) g)) (nodes g)

    let topSort (h: Digraph<'n>) =
        let rec dfs (g: Digraph<'n>, order, rts) =
          if List.isEmpty rts then
            order
          else
            let n = List.head rts
            let order' = n::order
            let g' = Map.remove n g
            let rts' = roots g'
            dfs (g', order', rts')
        dfs (h, [], roots h)
"""
;;

evalSrc sample
;;
formatSrc sample