#if METADATAFORMAT
namespace FSharp.MetadataFormat
#else
#if FSFCLI
namespace FSharp.Formatting.Options
#else
namespace FSharp.Literate
#endif
#endif

// --------------------------------------------------------------------------------------
// Tools for logging 
// --------------------------------------------------------------------------------------

module internal Log =
  open System

  type private LogMessage =
    | Print of string
    | Run of (unit -> unit)
    | Close of AsyncReplyChannel<unit>

  let private printer = MailboxProcessor.Start(fun inbox -> async {
    let sw = System.Diagnostics.Stopwatch.StartNew()
    while true do
      let! msg = inbox.Receive()
      match msg with 
      | Print msg -> printfn "[%d sec] %s" (sw.ElapsedMilliseconds / 1000L) msg 
      | Run cmd -> cmd ()
      | Close chnl -> chnl.Reply ()
    })

  /// Can be used to change the console color in a current scope
  /// (The result is `IDisposable` and can be bound using `use`)
  let colored color = 
    let prev = Console.ForegroundColor
    Console.ForegroundColor <- color
    { new IDisposable with
        member x.Dispose() = Console.ForegroundColor <- prev }

  /// Printf function that prints to a synchronized log
  let logf fmt = Printf.kprintf (Print >> printer.Post) fmt 
  /// Run the specified I/O interaction in the synchronized log
  let run f = printer.Post(Run f)

  let close () = printer.PostAndReply(Close)
