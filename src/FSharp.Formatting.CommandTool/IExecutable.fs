module FSharp.Formatting.IExecutable

open System.Collections.Generic
open CommandLine

/// Represents a top-level command 
/// (There are two instances, one for MetadataFormat and one for Literate)
type IExecutable =
    // Invoke the command
    abstract Execute : unit -> int
    /// Returns the command-specific parser error message
    abstract GetErrorText : unit -> string
    /// Returns the command-specific help text
    abstract GetUsage : unit -> string
