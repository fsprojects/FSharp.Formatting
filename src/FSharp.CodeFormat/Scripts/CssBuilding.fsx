#r @"..\..\..\packages\FSharp.Compiler.Service\lib\netstandard2.0\FSharp.Compiler.Service.dll"
#r @"..\..\..\bin\netstandard2.0\FSharp.CodeFormat.dll"
#r "netstandard.dll"
#r "System.Windows.Forms"

System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__

open System
open FSharp.CodeFormat
open Microsoft.FSharp.Compiler.SourceCodeServices
open FSharp.CodeFormat.CodeFormatAgent



let agent = CodeFormat.CreateAgent()

let formatSrcCss (source: string) =
    let snips, _errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
    CodeFormat.FormatCss(snips, "fstips") |> String.Concat
    
let formatSrcHtml (source: string) =
    let snips, _errors = agent.ParseSource("/somewhere/test.fsx", source.Trim())
    CodeFormat.FormatHtml(snips, "fstips")


//let checker = FSharpChecker.Create()
let config = CheckerConfig.Empty
let evalSrc (source:string) =
    CodeFormatAgent.processSourceCode("/somewhere/test.fsx", source.Trim(),config,None)
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

let fscode = formatSrcCss sample
;;
//open System.Windows.Forms
//open System.Drawing

//// -- Setup Windows form with browser to test rendering HTML 

//let form        = new Form (Visible=true, Text="FsFormatting Viewer")
//let browser     = new WebBrowser ()
//let textpane    = new RichTextBox ()
//let splitSide   = new SplitContainer ()

//form.Size           <- Size (1200, 800)
//textpane.Font       <- new Font ("Consolas", 9.f)
//textpane.WordWrap   <- false

//browser.Dock    <- DockStyle.Fill
//textpane.Dock   <- DockStyle.Fill
//splitSide.Dock  <- DockStyle.Fill
//form.Dock       <- DockStyle.Fill
//browser.Refresh()

//browser.DocumentText <- fscode
//textpane.Text        <- fscode


//textpane.TextChanged.Add (fun _ ->
//    browser.Refresh()
//    browser.DocumentText <- textpane.Text
//)

//splitSide.Panel1.Controls.Add browser
//splitSide.Panel2.Controls.Add textpane

//form.Controls.Add splitSide
//form.Show ()
//;;
fscode;;
open System.IO
Directory.CreateDirectory <| Path.GetFullPath @"..\..\..\testoutput\"
let destination = Path.GetFullPath @"..\..\..\testoutput\sample.html"
printfn "Writing generated page to - %s\n" destination

File.WriteAllText( destination,fscode)
;;
