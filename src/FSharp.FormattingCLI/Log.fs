module Log

// http://litemedia.info/application-logging-for-fsharp

//let private _log = log4net.LogManager.GetLogger("litemedia")
//let debug format = Printf.ksprintf _log.Debug format

let debug = printfn "[DEBUG]: %s"
let error = printfn "[ERROR]: %s"
let warn = printfn "[WARN]: %s"
let info = printfn "[INFO]: %s" 
