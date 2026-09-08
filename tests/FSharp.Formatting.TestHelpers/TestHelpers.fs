module FSharp.Formatting.TestHelpers

open System.Diagnostics
open FSharp.Formatting

/// Log everything to the console while the tests run.
let enableLogging () =
    FSharp.Formatting.Common.Logging.UseConsole(Microsoft.Extensions.Logging.LogLevel.Trace)
