(**
# Literate script example

- A multi-line comment starting with (** and ending with *) is turned into text and is processed using the F# Markdown processor (which supports standard Markdown commands).

- A single-line comment starting with (*** and ending with ***) is treated as a special command. The command can consist of key, key: value or key=value pairs.

The rest is just plain F# code goodness:
*)

let helloWorld () = printfn "Hello world!"

(**
## Using fsi evaluation

If you run fsdocs with the `--eval` flag, literate scripts will be evaluated by fsi and the output will can be included in the documentation using special commands such as 

`(*** include-value ***)` or `(*** include-it ***)` :

*)

// include a value by name via (*** include-value: name ***)
let numbers = [ 0..99 ]
(*** include-value: numbers ***)

// include the fsi output of an expression via (*** include-it ***)
List.sum numbers
(*** include-it ***)

(**
Literate scripts can move your documentation to the next level, because you can make sure that the code in your documentation is always up-to-date and shows results from the latest version.
*)
