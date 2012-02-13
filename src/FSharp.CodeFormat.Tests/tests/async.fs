// [snippet:0]
open System
open System.Net
open System.Text.RegularExpressions

/// Extracts the content of the <title> element
let extractTitle html = 
  let regTitle = new Regex(@"\<title\>([^\<]+)\</title\>")
  regTitle.Match(html).Groups.[1].Value

/// Asynchronously downloads a page and extracts the title
/// (uses a proxy to enable cross-domain downloads)
let downloadTitle url = async {
  let wc = new WebClient()
  let proxy = "http://tomasp.net/tryjoinads/proxy.aspx?url=" + url
  let! html = wc.AsyncDownloadString(Uri(proxy)) 
  return extractTitle html }
// [/snippet]

// [snippet:1]
open FSharp.Extensions.Joinads

let fsharp = "http://www.fsharp.net"
let csharp = "http://www.csharp.net"

/// Download titles of two pages in parallel
let titles = async {
  match! downloadTitle fsharp, downloadTitle csharp with
  | title1, title2 -> 
      printfn "Downloaded:\n   - %s\n   - %s" title1 title2 }

titles |> Async.Start
// [/snippet]

// [snippet:2]
let main = "http://msdn.microsoft.com/en-us/vstudio/hh388569.aspx"
let backup = "http://www.fsharp.net"

/// Start two downloads and return the first available result
let getFirst = async {
  match! downloadTitle main, downloadTitle backup with
  | res, ? -> printfn "Main: %s" res
  | ?, res -> printfn "Backup: %s" res }

getFirst |> Async.Start
// [/snippet]

// [snippet:3]
let good = "http://www.fsharp.net"
let bad = "http://www.f#.net"

/// Wraps 'downloadTitle' with an exception handler and returns
/// None if an exception occurs (or Some when download succeeds)
let tryDownloadTitle url = (*[omit(...)]*)async {
  try
    let! res = downloadTitle url
    return Some res
  with e -> return None }(*[/omit]*)

/// Try to download first available title. If both downloads
/// fail, then the value 'None' is returned.
let tryGetFirst = async {
  match! tryDownloadTitle good, tryDownloadTitle bad with
  | Some res, ? -> return Some ("First: " + res)
  | ?, Some res -> return Some ("Second: " + res)
  | None, None  -> return None }

// Run the download synchronously and wait for the result
let res = tryGetFirst |> Async.RunSynchronously
printfn "Result: %A" res
// [/snippet]

// [snippet:4]
(*[omit:Import necessary namespaces]*)
open System.Windows
open System.Windows.Controls
open FSharp.Console
open FSharp.Extensions.Joinads(*[/omit]*)

/// Creates a label that shows the current count and
/// buttons that increment and decrement the number
let createUserInterface() = (*[omit:(...)]*)
  let addControl (left, top) (ctrl:#UIElement) = 
    App.Console.Canvas.Children.Add(ctrl)
    Canvas.SetTop(ctrl, top)
    Canvas.SetLeft(ctrl, left)
    ctrl

  let label = addControl (20.0, 20.0) (TextBlock(FontSize = 20.0))
  let incBtn = addControl (20.0, 60.0) (Button(Content="Increment", Width = 80.0)) 
  let decBtn = addControl (110.0, 60.0) (Button(Content="Decrement", Width = 80.0)) 
  label, incBtn, decBtn(*[/omit]*)

/// Runs the specified workflow on the main
/// user-interface thread of the F# console
let runUserInterface work = (*[omit:(...)]*)
  App.Dispatch (fun() -> 
    App.Console.ClearCanvas()
    Async.StartImmediate work
    App.Console.CanvasPosition <- CanvasPosition.Right )(*[/omit]*)

/// Main workflow of the widget - creates the user interface and then
/// starts a recursive async function that implements user interaction.
let main = async {
  let label, inc, dec = createUserInterface() 

  /// Recursive workflow that keeps the current count as an argument
  let rec counter n : Async<unit> = async {
    // Update the text on the label
    label.Text <- sprintf "Count: %d" n
    // Wait for click on one of the two buttons 
    match! Async.AwaitEvent inc.Click, Async.AwaitEvent dec.Click with
    | _, ? -> return! counter (n + 1)
    | ?, _ -> return! counter (n - 1) }

  // Start the counter user interaction
  return! counter 0 }

// Start the main computation on GUI thread
runUserInterface main
// [/snippet]