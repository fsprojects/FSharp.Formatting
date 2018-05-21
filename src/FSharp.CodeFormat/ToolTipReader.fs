// --------------------------------------------------------------------------------------
// F# CodeFormat (ToolTipReader.fs)
// (c) Tomas Petricek, 2012, Available under Apache 2.0 license.
// --------------------------------------------------------------------------------------
#if INTERACTIVE


#r "../../packages/FSharp.Compiler.Service/lib/net45/FSharp.Compiler.Service.dll"
#r "../../packages/System.Reflection.Metadata/lib/portable-net45+win8/System.Reflection.Metadata.dll"
#r "System.Core"
#r "System.Data"
#r "System.Web"
#r "../../bin/FSharp.Formatting.Common.dll"
#load "../../src/Common/Collections.fs"
#load "../../src/Common/StringParsing.fs"
#load "Pervasive.fs"
#load "Constants.fs"
#load "CommentFilter.fs"
#load "SourceCode.fs"
module FSharp.CodeFormat.ToolTipReader

#else
module private FSharp.CodeFormat.ToolTipReader
#endif


open System
open System.IO
open FSharp.Collections

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open FSharp.CodeFormat

// --------------------------------------------------------------------------------------
// Implements formatting of tool tips 
// --------------------------------------------------------------------------------------

/// Turn string into a sequence of lines interleaved with line breaks
let formatMultilineString (s:string) = 
    [ for line in s.Split '\n' do
        yield HardLineBreak
        yield Literal line
    ] |> List.tail


/// Format comment in the tool tip
let private formatComment = function
| FSharpXmlDoc.Text s -> 
    [   Emphasis (formatMultilineString s); HardLineBreak ]
| _ ->
    // TODO: For 'XmlCommentSignature' we could get documentation 
    // from 'xml' files, but we don't know where to get them...
    []


/// Format the element of a tool tip (comment, overloads, etc.)
let private formatElement = function
| FSharpToolTipElement.None -> []
//| FSharpToolTipElement.(it, comment) -> 
//    [ yield! formatMultilineString it
//      yield HardLineBreak
//      yield! formatComment comment ]
| FSharpToolTipElement.Group items -> 
      // Trim the items to at most 10 displayed in a tool tip
    let items, trimmed = 
        if items.Length <= 10 then items, false
        else items |> Seq.take 10 |> List.ofSeq, true
    [ for it in items do
        yield! formatMultilineString it.MainDescription
        yield HardLineBreak
        yield! formatComment it.XmlDoc
        // Add note with the number of omitted overloads
        if trimmed then 
            let msg = sprintf "(+%d other overloads)" (items.Length - 10)
            yield Literal "   "
            yield Emphasis [ Literal msg ]
            yield HardLineBreak
    ]
  //| FSharpToolTipElement.SingleParameter(_paramType,_doc,_name) -> 
  //  [   yield ToolTipSpan.Literal _paramType
  //      yield ToolTipSpan.HardLineBreak
  //      yield! formatComment _doc     
  //  ]
  | FSharpToolTipElement.CompositionError _err -> []


/// Format entire tool tip as a value of type ToolTipSpans      
let private formatTip tip = 
    let spans = 
        match tip with
        | FSharpToolTipText [single] -> formatElement single
        | FSharpToolTipText items -> 
            [   yield Literal "Multiple items"
                yield HardLineBreak
                for first, item in Seq.mapi (fun i it -> i = 0, it) items do
                    if not first then 
                        yield HardLineBreak
                        yield Literal "--------------------"
                        yield HardLineBreak
                    yield! formatElement item
            ]
    // Remove unnecessary line breaks
    spans 
    |> List.skipWhile ((=) HardLineBreak) |> List.rev
    |> List.skipWhile ((=) HardLineBreak) |> List.rev

/// Format a tool tip, but first make sure that there is actually 
/// some text in the tip. Returns None if no information is available
let tryFormatTip = function
  | FSharpToolTipText elems
      when elems |> List.forall (function 
        FSharpToolTipElement.None -> true | _ -> false) -> None
  | tip -> Some (formatTip tip)




//// --------------------------------------------------------------------------------------
//// (c) Tomas Petricek, http://tomasp.net/blog
//// --------------------------------------------------------------------------------------
//module FsAutoComplete.TipFormatter

open System
open System.IO
open System.Xml
open System.Text.RegularExpressions
open Microsoft.FSharp.Compiler.SourceCodeServices

// TODO: Improve this parser. Is there any other XmlDoc parser available?
type private XmlDocMember (doc: XmlDocument) =
  let nl = Environment.NewLine
  let readContent (node: XmlNode) =
    match node with
    | null -> null
    | _ ->
        // Many definitions contain references like <paramref name="keyName" /> or <see cref="T:System.IO.IOException">
        // Replace them by the attribute content (keyName and System.IO.Exception in the samples above)
        // Put content in single quotes for possible formatting improvements on editor side.
        Regex.Replace(node.InnerXml,"""<\w+ \w+="(?:\w:){0,1}(.+?)" />""", "`$1`")
  let readChildren name (doc: XmlDocument) =
    doc.DocumentElement.GetElementsByTagName name
    |> Seq.cast<XmlNode>
    |> Seq.map (fun node -> node.Attributes.[0].InnerText.Replace("T:",""), readContent node)
    |> Map.ofSeq
  let summary = readContent doc.DocumentElement.ChildNodes.[0]
  let pars = readChildren "param" doc
  let exceptions = readChildren "exception" doc
  override x.ToString() =
    summary + nl + nl +
    (pars |> Seq.map (fun kv -> "`" + kv.Key + "`" + ": " + kv.Value) |> String.concat nl) +
    (if exceptions.Count = 0 then ""
     else nl + nl + "Exceptions:" + nl +
          (exceptions |> Seq.map (fun kv -> "\t" + "`" + kv.Key + "`" + ": " + kv.Value) |> String.concat nl))

let rec private readXmlDoc (reader: XmlReader) (acc: Map<string,XmlDocMember>) =
  let acc' =
    match reader.Read() with
    | false -> None
    | true when reader.Name = "member" && reader.NodeType = XmlNodeType.Element ->
      try
        let key = reader.GetAttribute("name")
        use subReader = reader.ReadSubtree()
        let doc = XmlDocument()
        doc.Load(subReader)
        acc |> Map.add key (XmlDocMember doc) |> Some
      with
      | _ -> Some acc
    | _ -> Some acc
  match acc' with
  | None -> acc
  | Some acc' -> readXmlDoc reader acc'

let private getXmlDoc =
  let xmlDocCache = Collections.Concurrent.ConcurrentDictionary<string, Map<string, XmlDocMember>>()
  fun dllFile ->
    let xmlFile = Path.ChangeExtension(dllFile, ".xml")
    if xmlDocCache.ContainsKey xmlFile then
      Some xmlDocCache.[xmlFile]
    else
        let rec exists filePath tryAgain =
            match File.Exists filePath, tryAgain with
            | true, _ -> Some filePath
            | false, false -> None
            | false, true ->
              // In Linux, we need to check for upper case extension separately
              let filePath = Path.ChangeExtension(filePath, Path.GetExtension(filePath).ToUpper())
              exists filePath false

        match exists xmlFile true with
        | None -> None
        | Some actualXmlFile ->
            // Prevent other threads from tying to add the same doc simultaneously
            xmlDocCache.AddOrUpdate(xmlFile, Map.empty, fun _ _ -> Map.empty) |> ignore
            try
                use reader = XmlReader.Create actualXmlFile
                let xmlDoc = readXmlDoc reader Map.empty
                xmlDocCache.AddOrUpdate(xmlFile, xmlDoc, fun _ _ -> xmlDoc) |> ignore
                Some xmlDoc
            with _ ->
                None  // TODO: Remove the empty map from cache to try again in the next request?

// --------------------------------------------------------------------------------------
// Formatting of tool-tip information displayed in F# IntelliSense
// --------------------------------------------------------------------------------------
let private buildFormatComment cmt =
    match cmt with
    | FSharpXmlDoc.Text s -> s
    | FSharpXmlDoc.XmlDocFileSignature(dllFile, memberName) ->
       match getXmlDoc dllFile with
       | Some doc when doc.ContainsKey memberName -> string doc.[memberName]
       | _ -> ""
    | _ -> ""

let format_DLLTip (FSharpToolTipText tips) : (string * string) list list =
    tips |> List.choose (function
    | FSharpToolTipElement.Group items ->
        let getRemarks (it : FSharpToolTipElementData<string>) = defaultArg (it.Remarks |> Option.map (fun n -> if String.IsNullOrWhiteSpace n then n else "\n\n" + n)) ""
        Some (items |> List.map (fun (it) ->  (it.MainDescription + getRemarks it, buildFormatComment it.XmlDoc)))
    | FSharpToolTipElement.CompositionError (error) -> Some [("<Note>", error)]
    | _ -> None)

let formatTipEnhanced (FSharpToolTipText tips) (signature : string) (footer : string) : (string * string * string) list list =
    tips |> List.choose (function
    | FSharpToolTipElement.Group items ->
        Some (items |> List.map (fun (it) ->  (signature, buildFormatComment it.XmlDoc, footer)))
    | FSharpToolTipElement.CompositionError (error) -> Some [("<Note>", error, "")]
    | _ -> None)

let extractSignature (FSharpToolTipText tips) =
    let getSignature (str: string) =
        let nlpos = str.IndexOfAny([|'\r';'\n'|])
        let firstLine =
            if nlpos > 0 then str.[0..nlpos-1]
            else str

        if firstLine.StartsWith("type ", StringComparison.Ordinal) then
            let index = firstLine.LastIndexOf("=", StringComparison.Ordinal)
            if index > 0 then firstLine.[0..index-1]
            else firstLine
        else firstLine

    let firstResult x =
        match x with
        | FSharpToolTipElement.Group gs -> List.tryPick (fun (t : FSharpToolTipElementData<string>) -> if not (String.IsNullOrWhiteSpace t.MainDescription) then Some t.MainDescription else None) gs
        | _ -> None

    tips
    |> Seq.tryPick firstResult
    |> Option.map getSignature
    |> Option.defaultValue ""
