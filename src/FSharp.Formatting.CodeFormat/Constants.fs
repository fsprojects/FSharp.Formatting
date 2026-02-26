/// Internal CSS class name constants used when emitting syntax-highlighted HTML
module internal FSharp.Formatting.CodeFormat.Constants

/// CSS class name constants for syntax token categories
[<RequireQualifiedAccessAttribute>]
module CSS =

    /// CSS class for comment tokens
    [<Literal>]
    let Comment = "c"

    /// CSS class for default/plain tokens
    [<Literal>]
    let Default = ""

    /// CSS class for identifier tokens
    [<Literal>]
    let Identifier = "id"

    /// CSS class for inactive (excluded by conditional compilation) tokens
    [<Literal>]
    let Inactive = "inactive"

    /// CSS class for keyword tokens
    [<Literal>]
    let Keyword = "k"

    /// CSS class for numeric literal tokens
    [<Literal>]
    let Number = "n"

    /// CSS class for operator tokens
    [<Literal>]
    let Operator = "o"

    /// CSS class for preprocessor directive tokens
    [<Literal>]
    let Preprocessor = "pp"

    /// CSS class for string literal tokens
    [<Literal>]
    let String = "s"

    /// CSS class for module/namespace tokens
    [<Literal>]
    let Module = "m"

    /// CSS class for reference type tokens
    [<Literal>]
    let ReferenceType = "rt"

    /// CSS class for value type tokens
    [<Literal>]
    let ValueType = "vt"

    /// CSS class for function tokens
    [<Literal>]
    let Function = "fn"

    /// CSS class for pattern match tokens
    [<Literal>]
    let Pattern = "pat"

    /// CSS class for mutable variable tokens
    [<Literal>]
    let MutableVar = "mv"

    /// CSS class for printf format string tokens
    [<Literal>]
    let Printf = "pf"

    /// CSS class for escaped character tokens
    [<Literal>]
    let Escaped = "esc"

    /// CSS class for disposable tokens
    [<Literal>]
    let Disposable = "d"

    /// CSS class for type argument tokens
    [<Literal>]
    let TypeArgument = "ta"

    /// CSS class for punctuation tokens
    [<Literal>]
    let Punctuation = "pn"

    /// CSS class for enumeration tokens
    [<Literal>]
    let Enumeration = "en"

    /// CSS class for interface tokens
    [<Literal>]
    let Interface = "if"

    /// CSS class for property tokens
    [<Literal>]
    let Property = "prop"

    /// CSS class for union case tokens
    [<Literal>]
    let UnionCase = "uc"
