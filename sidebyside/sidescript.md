# Example: Using Literate Script Content

This file demonstrates how to write literate F# script
files (`*.fsx`) that can be transformed into nice HTML
using the `literate.fsx` script from the [F# Formatting
package](http://fsprojects.github.io/FSharp.Formatting).

As you can see, a comment starting with a double asterisk
is treated as part of the document and is transformed
using Markdown, which means that you can use:

* Unordered or ordered lists

* Text formatting including **bold** and **emphasis**

And numerous other [Markdown](http://daringfireball.net/projects/markdown) features.

## Writing F# code

Code that is not inside the comment will be formatted as
a sample snippet.

```fsharp
/// The Hello World of functional languages!
let rec factorial x =
    if x = 0 then 1 else x * (factorial (x - 1))

let f10 = factorial 10
```

## Hiding code

If you want to include some code in the source code,
but omit it from the output, you can use the `hide`
command.

The value will be defined in the F# code and so you
can use it from other (visible) code and get the correct
tooltips:

```fsharp
let answer = hidden
```

## Moving code around

Sometimes, it is useful to first explain some code that
has to be located at the end of the snippet (perhaps
because it uses some definitions discussed in the middle).
This can be done using `include` and `define` commands.

The following snippet gets the correct tooltips, even though
it uses `laterFunction`:

```fsharp
let sample = laterFunction () |> printfn "Got: %s"
```

Then, we can explain how `laterFunction` is defined:

```fsharp
let laterFunction () = "Not very difficult, is it?"
```

This example covers pretty much all features that are
currently implemented in `literate.fsx`, but feel free
to [fork the project on GitHub](https://github.com/fsprojects/FSharp.Formatting) and add more
features or report bugs!


