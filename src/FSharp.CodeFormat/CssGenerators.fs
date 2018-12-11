namespace FSharp.CodeFormat.Css

open System
open FSharp.CodeFormat.Constants

type CssColor = {
    CssClass : string
    Color    : string
} with
    static member create (cssClass:string) (color:string) =
        { CssClass = cssClass; Color = color }



/// Stores the Css classes used to construct the
/// style sheet and tooltips
type SourceCodeColors = {
    Comment         : CssColor
    Default         : CssColor
    Identifier      : CssColor
    Inactive        : CssColor
    Keyword         : CssColor
    Number          : CssColor
    Operator        : CssColor
    Preprocessor    : CssColor
    String          : CssColor
    Module          : CssColor
    ReferenceType   : CssColor
    ValueType       : CssColor
    Function        : CssColor
    Pattern         : CssColor
    MutableVar      : CssColor
    Printf          : CssColor
    Escaped         : CssColor
    Disposable      : CssColor
    TypeArgument    : CssColor
    Punctuation     : CssColor
    Enumeration     : CssColor
    Interface       : CssColor
    Property        : CssColor
    UnionCase       : CssColor
    LineNumber      : CssColor
    FsiOutput       : CssColor
    Omitted         : CssColor
} with
    static member DefaultStyle = {
        Comment       = CssColor.create CSS.Comment       Colors.Comment      
        Default       = CssColor.create CSS.Default       Colors.Default      
        Identifier    = CssColor.create CSS.Identifier    Colors.Identifier   
        Inactive      = CssColor.create CSS.Inactive      Colors.Inactive     
        Keyword       = CssColor.create CSS.Keyword       Colors.Keyword      
        Number        = CssColor.create CSS.Number        Colors.Number       
        Operator      = CssColor.create CSS.Operator      Colors.Operator     
        Preprocessor  = CssColor.create CSS.Preprocessor  Colors.Preprocessor 
        String        = CssColor.create CSS.String        Colors.String       
        Module        = CssColor.create CSS.Module        Colors.Module       
        ReferenceType = CssColor.create CSS.ReferenceType Colors.ReferenceType
        ValueType     = CssColor.create CSS.ValueType     Colors.ValueType    
        Function      = CssColor.create CSS.Function      Colors.Function     
        Pattern       = CssColor.create CSS.Pattern       Colors.Pattern      
        MutableVar    = CssColor.create CSS.MutableVar    Colors.MutableVar   
        Printf        = CssColor.create CSS.Printf        Colors.Printf       
        Escaped       = CssColor.create CSS.Escaped       Colors.Escaped      
        Disposable    = CssColor.create CSS.Disposable    Colors.Disposable   
        TypeArgument  = CssColor.create CSS.TypeArgument  Colors.TypeArgument 
        Punctuation   = CssColor.create CSS.Punctuation   Colors.Punctuation  
        Enumeration   = CssColor.create CSS.Enumeration   Colors.Enumeration  
        Interface     = CssColor.create CSS.Interface     Colors.Interface    
        Property      = CssColor.create CSS.Property      Colors.Property     
        UnionCase     = CssColor.create CSS.UnionCase     Colors.UnionCase    
        LineNumber    = CssColor.create CSS.LineNumber    Colors.LineNumber   
        FsiOutput     = CssColor.create CSS.FsiOutput     Colors.FsiOutput    
        Omitted       = CssColor.create CSS.Omitted       Colors.Omitted      
    }


type CssProperty = {
    Name  : string
    Value : string
} with
    static member create (name:string, value:string) =
        { Name = name; Value = value }


type CssPropertyGroup = {
    CssClasses : string list
    Properties : CssProperty list
}

type SourceCodeProperties = {
    Snippet : string 
    Tooltip : string 
    Source  : string 
} with
    static member Default = {
        Snippet = CSS.Snippet
        Tooltip = CSS.Tooltip
        Source  = CSS.Source
    }


/// Stores the default sets of CSS Properties used to style the output
/// of FSharp.Formatting.
module PropertyDefaults =

    /// Properties for '.omitted'
    let omitted =
        {   CssClasses = [ "omitted" ]
            Properties =
                [   "background"    , "#3c4e52"
                    "border-radius" , "5px"
                    "color"         , "#808080"
                ] |> List.map CssProperty.create
        }

    /// Properties for 'table.pre, pre.fssnip, pre'
    let snippetTable =
        {   CssClasses = [ "table.pre"; "pre.fssnip"; "pre" ]
            Properties =
                [   "line-height"       , "13pt"
                    "border"            , "1px solid #d8d8d8"
                    "border-collapse"   , "separate"
                    "white-space"       , "pre"
                    "font"              , "9pt 'Droid Sans Mono',consolas,monospace"
                    "width"             , "fit-content"
                    "margin"            , "10px 20px 20px 20px"
                    "background-color"  , "#18353c"
                    "padding"           , "10px"
                    "border-radius"     , "5px"
                    "color"             , "#d1d1d1"
                    "max-width"         , "none"
                ] |> List.map CssProperty.create
        }

    let tableData =
        {   CssClasses = ["table.pre td"]
            Properties =
                [   "padding"       , "0px"
                    "white-space"   , "normal"
                    "margin"        , "0px"
                ] |> List.map CssProperty.create
        }

    let tableSource =
        {   CssClasses = ["table.pre pre"]
            Properties =
                [   "padding"       , "0px"
                    "border-radius" , "0px"
                    "margin"        , "0px"
                    "width"         , "100%"
                ] |> List.map CssProperty.create
        }

    let tableLines =
        {   CssClasses = ["table.pre td.lines"]
            Properties =
                [ "width", "30px"
                ] |> List.map CssProperty.create
        }

    let snippet =
        {   CssClasses = ["pre.fssnip"]
            Properties =
                [   "font"          , "9pt 'Droid Sans Mono',consolas,monospace"
                    "padding-left"  , "20px"
                ] |> List.map CssProperty.create
        }




open System.Text
open FSharp.Reflection
open FSharp.CodeFormat

module Generators =

    let cssColors (srcStyle:SourceCodeColors) =
        let styles =
            FSharpType.GetRecordFields<SourceCodeColors>()
            |> Array.map (fun prop -> prop.GetValue srcStyle :?> CssColor)

        (StringBuilder(), styles)
        ||> Array.fold (fun sb style ->
            if String.IsNullOrWhiteSpace style.Color then sb else
            sb.AppendLine <| sprintf ".%s { color: %s; }" style.CssClass style.Color
        ) |> string

    let x = ()




