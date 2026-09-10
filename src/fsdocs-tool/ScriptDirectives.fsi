/// The files a script depends on through its hash directives.
module fsdocs.ScriptDirectives

/// The full paths of the files loaded with '#load' and the local files referenced with '#r',
/// from the syntax tree (comments and strings are not fooled). A '#load' target is returned
/// whether it exists or not, a '#r' only when it names an existing file: the other forms
/// ('nuget:', assembly names) are not files the site can watch.
val dependenciesOf: path: string -> text: string -> string list
