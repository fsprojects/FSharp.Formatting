module IExecutable

open System.Collections.Generic
open CommandLine

type IExecutable =
    /// assign the mandatory and optional params to the desired formatting command
    /// using http://stackoverflow.com/questions/7095620/propagating-optional-arguments
    abstract execute : unit -> int
    /// use the command-specific parser error message
    abstract getErrorText : unit -> string
    /// get the command-specific help text
    abstract getUsage : unit -> string
