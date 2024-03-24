(**
# Literate Notebooks

Content may be created using  [.NET interactive](https://github.com/dotnet/interactive/tree/main) polyglot notebooks as the input file. Notebooks are processed by converting the notebook to a literate `.fsx` script and then passing the script through the script processing pipeline. Markdown notebook cells are passed through as comments surrounded by `(**` and `*)`, F# code cells are passed through as code, and non-F# code is passed through as markdown fenced code blocks between `(**` and `*)` comment markers.

The `fsdocs` tool uses [dotnet-repl](https://github.com/jonsequitur/dotnet-repl) to evaluate polyglot notebooks. You need this tool to evaluate notebooks using `dotnet fsdocs [build|watch] --eval`. It can be installed into your local tool manifest using the command `dotnet tool install dotnet-repl`.

F# Formatting tries to faithfully reproduce a notebook's native appearance when generating documents. Notebook cell outputs are passed through unchanged to preserve the notebook's html output. The below snippet demonstrates a notebook's html output for F# records, which differs from the output you would get with the same code inside a literate scripts.

*)
type ContactCard =
    { Name: string
      Phone: string
      ZipCode: string }

// Create a new record
{ Name = "Alf"; Phone = "(555) 555-5555"; ZipCode = "90210" }
(**
<p><details open="open" class="dni-treeview"><summary><span class="dni-code-hint"><code>{ Name = &quot;Alf&quot;\n  Phone = &quot;(555) 555-5555&quot;\n  ZipCode = &quot;90210&quot; }</code></span></summary><div><table><thead><tr></tr></thead><tbody><tr><td>Name</td><td><div class="dni-plaintext"><pre>Alf</pre></div></td></tr><tr><td>Phone</td><td><div class="dni-plaintext"><pre>(555) 555-5555</pre></div></td></tr><tr><td>ZipCode</td><td><div class="dni-plaintext"><pre>90210</pre></div></td></tr></tbody></table></div></details><style>
.dni-code-hint {
    font-style: italic;
    overflow: hidden;
    white-space: nowrap;
}
.dni-treeview {
    white-space: nowrap;
}
.dni-treeview td {
    vertical-align: top;
    text-align: start;
}
details.dni-treeview {
    padding-left: 1em;
}
table td {
    text-align: start;
}
table tr { 
    vertical-align: top; 
    margin: 0em 0px;
}
table tr td pre 
{ 
    vertical-align: top !important; 
    margin: 0em 0px !important;
} 
table th {
    text-align: start;
}
</style>
</p>
*)

