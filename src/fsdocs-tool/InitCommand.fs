namespace fsdocs

open System.IO
open CommandLine

[<Verb("init", HelpText = "initialize the necessary folder structure and files for creating documentation with fsdocs.")>]
type InitCommand() =
    class
    end
