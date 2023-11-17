(**
Heading
=======

With some [hyperlink](http://tomasp.net)

dont-substitute-in-inline-code: `{{fsdocs-source-basename}}`

substitute-in-markdown: {{fsdocs-source-basename}}

[ABC](http://substitute-in-link: {{fsdocs-source-basename}})
[substitute-in-href-text: {{fsdocs-source-basename}}](http://google.com)

Another [hyperlink](simple1.fsx)
Another [hyperlink](simple2.md)
And another [hyperlink](simple3.ipynb)

*)
let hello = "Code sample"

let goodbye = "substitute-in-fsx-code: {{fsdocs-source-basename}}"

#if HTML

let test = 1

#endif
