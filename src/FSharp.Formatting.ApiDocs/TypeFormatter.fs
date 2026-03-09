namespace rec FSharp.Formatting.ApiDocs

open System
open System.Reflection
open System.Collections.Generic
open System.Text
open System.IO
open System.Web
open System.Xml
open System.Xml.Linq

open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Formatting.Common
open FSharp.Formatting.Internal
open FSharp.Formatting.CodeFormat
open FSharp.Formatting.Literate
open FSharp.Formatting.Markdown
open FSharp.Formatting.HtmlModel
open FSharp.Formatting.HtmlModel.Html
open FSharp.Formatting.Templating
open FSharp.Patterns
open FSharp.Compiler.Syntax

/// Internal module for formatting F# types, type arguments, constraints, and member
/// signatures as HTML elements suitable for use in API documentation pages.
[<AutoOpen>]
module internal TypeFormatter =

    /// Cross-reference resolver used to generate hyperlinks when rendering type names.
    type TypeFormatterParams = CrossReferenceResolver

    /// Wraps an <see cref="HtmlElement"/> as an <see cref="ApiDocHtml"/> with no source location.
    let convHtml (html: HtmlElement) = ApiDocHtml(html.ToString(), None)

    /// We squeeze the spaces out of anything where whitespace layout must be exact - any deliberate
    /// whitespace must use &#32;
    ///
    /// This kind of sucks but stems from the fact the formatting for the internal HTML DSL is freely
    /// adding spaces which are actually significant when formatting F# type information.
    let codeHtml html =
        let html = code [] [ html ]

        ApiDocHtml(
            html.ToString().Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("<ahref", "<a href"),
            None
        )

    /// Computes a URL to a source-repository hyperlink for the given file location,
    /// by combining the source-folder root and the repository base URL.
    /// Returns <c>None</c> when no source-folder/repository pair is configured.
    let formatSourceLocation
        (urlRangeHighlight: Uri -> int -> int -> string)
        (sourceFolderRepo: (string * string) option)
        (location: range option)
        =
        location
        |> Option.bind (fun location ->
            sourceFolderRepo
            |> Option.map (fun (sourceFolder, sourceRepo) ->
                let sourceFolderPath = Uri(Path.GetFullPath(sourceFolder)).ToString()

                let docPath = Uri(Path.GetFullPath(location.FileName)).ToString()

                // Even though ignoring case might be wrong, we do that because
                // one path might be file:///C:\... and the other file:///c:\...  :-(
                if
                    not
                    <| docPath.StartsWith(sourceFolderPath, StringComparison.InvariantCultureIgnoreCase)
                then
                    Log.verbf "Current source file '%s' doesn't reside in source folder '%s'" docPath sourceFolderPath
                    ""
                else
                    let relativePath = docPath.[sourceFolderPath.Length ..]
                    let uriBuilder = UriBuilder(sourceRepo)
                    uriBuilder.Path <- uriBuilder.Path + relativePath
                    urlRangeHighlight uriBuilder.Uri location.StartLine location.EndLine))

    /// Formats a single generic parameter as text, using <c>^</c> for SRTP parameters
    /// and <c>'</c> for ordinary type parameters.
    let formatTypeArgumentAsText (typar: FSharpGenericParameter) =
        (if typar.IsSolveAtCompileTime then "^" else "'") + typar.Name

    /// Formats a list of generic parameters as a list of text strings.
    let formatTypeArgumentsAsText (typars: FSharpGenericParameter list) =
        List.map formatTypeArgumentAsText typars

    /// Formats a single type constraint as a text string in F# source syntax,
    /// returning <c>None</c> for unrecognised or unsupported constraint kinds.
    let formatConstraintAsText (typar: FSharpGenericParameter) (cx: FSharpGenericParameterConstraint) =
        let typarName = formatTypeArgumentAsText typar

        if cx.IsEqualityConstraint then
            Some $"%s{typarName} : equality"
        elif cx.IsComparisonConstraint then
            Some $"%s{typarName} : comparison"
        elif cx.IsNonNullableValueTypeConstraint then
            Some $"%s{typarName} : struct"
        elif cx.IsReferenceTypeConstraint then
            Some $"%s{typarName} : not struct"
        elif cx.IsRequiresDefaultConstructorConstraint then
            Some $"%s{typarName} : (new : unit -> %s{typarName})"
        elif cx.IsUnmanagedConstraint then
            Some $"%s{typarName} : unmanaged"
        elif cx.IsCoercesToConstraint then
            Some $"%s{typarName} :> %s{cx.CoercesToTarget.Format(FSharpDisplayContext.Empty)}"
        elif cx.IsEnumConstraint then
            Some $"%s{typarName} : enum<%s{cx.EnumConstraintTarget.Format(FSharpDisplayContext.Empty)}>"
        elif cx.IsDelegateConstraint then
            let d = cx.DelegateConstraintData

            Some
                $"%s{typarName} : delegate<%s{d.DelegateTupledArgumentType.Format(FSharpDisplayContext.Empty)}, %s{d.DelegateReturnType.Format(FSharpDisplayContext.Empty)}>"
        elif cx.IsMemberConstraint then
            let d = cx.MemberConstraintData

            let argTypes =
                d.MemberArgumentTypes
                |> Seq.map (fun t -> t.Format(FSharpDisplayContext.Empty))
                |> String.concat " * "

            let retType = d.MemberReturnType.Format(FSharpDisplayContext.Empty)
            let staticKw = if d.MemberIsStatic then "static member" else "member"
            Some $"%s{typarName} : (%s{staticKw} %s{d.MemberName} : %s{argTypes} -> %s{retType})"
        else
            None

    /// Collects all constraints from a list of type parameters as text strings,
    /// skipping any constraints that cannot be represented in F# source syntax.
    let formatConstraintsAsText (typars: FSharpGenericParameter list) =
        [ for typar in typars do
              for cx in typar.Constraints do
                  match formatConstraintAsText typar cx with
                  | Some s -> yield s
                  | None -> () ]

    /// Wraps an <see cref="HtmlElement"/> in parentheses.
    let bracketHtml (str: HtmlElement) = span [] [ !!"("; str; !!")" ]

    /// Wraps an <see cref="HtmlElement"/> in parentheses only if its text representation
    /// contains whitespace (i.e. is non-atomic).
    let bracketNonAtomicHtml (str: HtmlElement) =
        if str.ToString().Contains("&#32;") then
            bracketHtml str
        else
            str

    /// Conditionally wraps an <see cref="HtmlElement"/> in parentheses.
    let bracketHtmlIf cond str = if cond then bracketHtml str else str

    /// Renders a type constructor reference as an HTML element, hyperlinking to the API
    /// documentation page when a cross-reference URL is available.
    let formatTyconRefAsHtml (ctx: TypeFormatterParams) (tcref: FSharpEntity) =
        let core = !!tcref.DisplayName.Replace(" ", "&#32;")

        match ctx.TryResolveEntity tcref with
        | None -> core
        | Some url -> a [ Href url.ReferenceLink ] [ core ]

    /// Formats a generic type application as HTML, handling both prefix (<c>Foo&lt;'T&gt;</c>)
    /// and postfix (<c>'T list</c>) display styles. Uses precedence to decide when to parenthesize.
    let rec formatTypeApplicationAsHtml ctx (tcref: FSharpEntity) typeName prec prefix args : HtmlElement =
        if prefix then
            match args with
            | [] -> typeName
            | [ arg ] -> span [] [ typeName; !!"&lt;"; (formatTypeWithPrecAsHtml ctx 4 arg); !!"&gt;" ]
            | args ->
                bracketHtmlIf
                    (prec <= 1)
                    (span [] [ typeName; !!"&lt;"; formatTypesWithPrecAsHtml ctx 2 ",&#32;" args; !!"&gt;" ])
        else
            match args with
            | [] -> typeName
            | [ arg ] ->
                if tcref.DisplayName.StartsWith '[' then
                    span [] [ formatTypeWithPrecAsHtml ctx 2 arg; !!tcref.DisplayName ]
                else
                    span [] [ formatTypeWithPrecAsHtml ctx 2 arg; !!"&#32;"; typeName ]
            | args ->
                bracketHtmlIf
                    (prec <= 1)
                    (span [] [ bracketNonAtomicHtml (formatTypesWithPrecAsHtml ctx 2 ",&#32;" args); typeName ])

    /// Formats a list of types as HTML with a given separator string, at a given precedence level.
    and formatTypesWithPrecAsHtml ctx prec sep typs =
        typs |> List.map (formatTypeWithPrecAsHtml ctx prec) |> Html.sepWith sep

    /// Formats a single <see cref="FSharpType"/> as an HTML element at a given precedence level.
    /// Handles measure types, named types, tuples, function types, and generic parameters.
    and formatTypeWithPrecAsHtml ctx prec (typ: FSharpType) =
        // Measure types are stored as named types with 'fake' constructors for products, "1" and inverses
        // of measures in a normalized form (see Andrew Kennedy technical reports). Here we detect this
        // embedding and use an approximate set of rules for layout out normalized measures in a nice way.
        match typ with
        | MeasureProd(ty, MeasureOne)
        | MeasureProd(MeasureOne, ty) -> formatTypeWithPrecAsHtml ctx prec ty
        | MeasureProd(ty1, MeasureInv ty2)
        | MeasureProd(ty1, MeasureProd(MeasureInv ty2, MeasureOne)) ->
            span [] [ formatTypeWithPrecAsHtml ctx 2 ty1; !!"/"; formatTypeWithPrecAsHtml ctx 2 ty2 ]
        | MeasureProd(ty1, MeasureProd(ty2, MeasureOne))
        | MeasureProd(ty1, ty2) ->
            span [] [ formatTypeWithPrecAsHtml ctx 2 ty1; !!"*"; formatTypeWithPrecAsHtml ctx 2 ty2 ]
        | MeasureInv ty -> span [] [ !!"/"; formatTypeWithPrecAsHtml ctx 1 ty ]
        | MeasureOne -> !!"1"
        | _ when typ.HasTypeDefinition ->
            let tcref = typ.TypeDefinition
            let tyargs = typ.GenericArguments |> Seq.toList
            // layout postfix array types
            formatTypeApplicationAsHtml ctx tcref (formatTyconRefAsHtml ctx tcref) prec tcref.UsesPrefixDisplay tyargs
        | _ when typ.IsTupleType ->
            let tyargs = typ.GenericArguments |> Seq.toList
            bracketHtmlIf (prec <= 2) (formatTypesWithPrecAsHtml ctx 2 "&#32;*&#32;" tyargs)
        | _ when typ.IsFunctionType ->
            let rec loop soFar (typ: FSharpType) =
                if typ.IsFunctionType then
                    let domainTyp, retType = typ.GenericArguments.[0], typ.GenericArguments.[1]

                    loop (soFar @ [ formatTypeWithPrecAsHtml ctx 4 domainTyp; !!"&#32;->&#32;" ]) retType
                else
                    span [] (soFar @ [ formatTypeWithPrecAsHtml ctx 5 typ ])

            bracketHtmlIf (prec <= 4) (loop [] typ)
        | _ when typ.IsGenericParameter -> !!(formatTypeArgumentAsText typ.GenericParameter)
        | _ -> !!"(type)"

    /// Formats a <see cref="FSharpType"/> at the default (outermost) precedence level.
    let formatTypeAsHtml ctx (typ: FSharpType) = formatTypeWithPrecAsHtml ctx 5 typ

    /// Resolves an argument name from its optional name and type, generating a placeholder
    /// name (e.g. <c>arg1</c>) for unnamed arguments.
    let formatArgNameAndTypePair i (argName, argType) =
        let argName =
            match argName with
            | None -> if isUnitType argType then "()" else "arg" + string<int> i
            | Some nm -> nm

        argName, argType

    /// Formats an argument name and type from a <see cref="FSharpParameter"/>, handling
    /// optional arguments (prefixing the name with <c>?</c> and stripping the option wrapper).
    let formatArgNameAndType i (arg: FSharpParameter) =
        let argName, argType = formatArgNameAndTypePair i (arg.Name, arg.Type)

        let isOptionalArg = arg.IsOptionalArg || hasAttrib<OptionalArgumentAttribute> arg.Attributes

        let argName = if isOptionalArg then "?" + argName else argName

        let argType =
            // Strip off the 'option' type for optional arguments
            if isOptionalArg && argType.HasTypeDefinition && argType.GenericArguments.Count = 1 then
                argType.GenericArguments.[0]
            else
                argType

        argName, argType

    /// Formats the usage text for a single argument (just its name, not the type annotation).
    let formatArgUsageAsHtml i (arg: FSharpParameter) =
        let argName, _argType = formatArgNameAndType i arg
        !!argName

    /// Formats a <c>(name: type)</c> pair as HTML for a single argument.
    let formatArgNameAndTypePairUsageAsHtml ctx (argName0, argType) =
        span
            []
            [ !!(match argName0 with
                 | None -> ""
                 | Some argName -> argName + ":&#32;")
              formatTypeWithPrecAsHtml ctx 2 argType ]

    /// Formats the full curried argument list for a member or function as HTML,
    /// parenthesising argument groups as appropriate.
    let formatCurriedArgsUsageAsHtml preferNoParens isItemIndexer curriedArgs =
        let counter =
            let mutable n = 0

            fun () ->
                n <- n + 1
                n

        curriedArgs
        |> List.map (fun args ->
            let argTuple = args |> List.map (formatArgNameAndType (counter ()) >> fst)

            match argTuple with
            | [] -> !!"()"
            | [ argName ] when argName = "()" -> !!"()"
            | [ argName ] when preferNoParens -> !!argName
            | args ->
                let argText = args |> List.map (!!) |> Html.sepWith ",&#32;"

                if isItemIndexer then argText else bracketHtml argText)
        |> Html.sepWith "&#32;"

    /// Formats a delegate signature <c>nm(args -> returnType)</c> as HTML.
    let formatDelegateSignatureAsHtml ctx nm (typ: FSharpDelegateSignature) =
        let args =
            typ.DelegateArguments
            |> List.ofSeq
            |> List.map (formatArgNameAndTypePairUsageAsHtml ctx)
            |> Html.sepWith "&#32;*&#32;"

        span [] ([ !!nm; !!"("; args; !!"&#32;->&#32;"; formatTypeAsHtml ctx typ.DelegateReturnType; !!")" ])
