#r @"..\..\..\bin\FSharp.Compiler.Service.dll"
#r @"..\..\..\bin\FSharp.CodeFormat.dll"


open FSharp.CodeFormat
open Microsoft.FSharp.Compiler.SourceCodeServices

let agent = CodeFormat.CreateAgent()

let formatSrcCss (source: string) =
    let snips, _errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
    CodeFormat.FormatCss(snips, "fstips")
    
let formatSrcHtml (source: string) =
    let snips, _errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
    CodeFormat.FormatHtml(snips, "fstips")


let checker = FSharpChecker.Create()

let evalSrc (source:string) =
    CodeFormatAgent.processSourceCode(checker,"/somewhere/test.fsx", source.Trim(),None,None)
    |> Async.RunSynchronously
    |> function
       | None -> ([],[||])
       | Some (a,b) -> (a,b)

let sample = """
open System
open System.Text

type System.Text.StringBuilder with
    member private self.Yield (_) = self
    [<CustomOperation("append")>]
    member __.append (sb:StringBuilder, str:string) = sb.Append str
    [<CustomOperation("appendLine")>]
    member __.appendLine (sb:StringBuilder, str:string) = sb.AppendLine str
    [<CustomOperation("appendf")>]
    member __.appendf (sb:StringBuilder, txt, x) =(sprintf txt x:string) |> sb.Append
    [<CustomOperation("appendf2")>]
    member __.appendf2 (sb:StringBuilder, txt, x, y) =(sprintf txt x y :string) |> sb.Append
    [<CustomOperation("appendf3")>]
    member __.appendf3 (sb:StringBuilder, txt, x, y, z) =(sprintf txt x y z :string) |> sb.Append
    [<CustomOperation("appendFormat")>]
    member __.appendFormat (sb:StringBuilder, str:string, [<ParamArray>] args) = sb.AppendFormat(str,args)
    member private __.Run sb = string sb

module ContainerStore = 
    let inline fn (x:'a list) (y:'b []) (z:'c seq) = 
        (Seq.append x  (y :> obj [])) |> Seq.append (z :> obj seq)

    let sb = StringBuilder()

    let example = 
        sb{ appendf2 "%s %s" "a" "b"
            appendf3 "%s %i %M" "z" 20 5.M
            appendf "%i" 100
        } |> string 
// customary
type Enumeration =
    | One = 1 | Two = 2 | Three = 3 

type Union = A | B | C
// commentary
[<Struct>]   
type REKT = {
    Uno : bool
    Dos : float 
}
#if SOME_DEFINE
#else 
#endif 

[<Interface>]
type Iffy = 
    abstract member Zoom : float -> string -> int
    abstract member File : string with get, set 

let (|Lie|Cheat|) (a:bool) = if a then Lie else Cheat
let (<|>) a b = a + b  
"""
;;

//evalSrc sample
//;; formatSrcHtml sample
;;
formatSrcCss sample


