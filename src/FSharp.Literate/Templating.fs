// --------------------------------------------------------------------------------------
// Generates file using HTML or CSHTML (Razor) template
// --------------------------------------------------------------------------------------

module internal FSharp.Literate.Templating
open System.IO
open System.Globalization
open FSharp.Literate
open RazorEngine.Templating

/// Replace {parameter} in the input string with 
/// values defined in the specified list
let replaceParameters (contentTag:string) (parameters:seq<string * string>) input = 
  match input with 
  | None ->
      // If there is no template, return just document + tooltips
      let lookup = parameters |> dict
      lookup.[contentTag] + "\n\n" + lookup.["tooltips"]
  | Some input ->
      // First replace keys with some uglier keys and then replace them with values
      // (in case one of the keys appears in some other value)
      let id = System.Guid.NewGuid().ToString("d")
      let input = parameters |> Seq.fold (fun (html:string) (key, value) -> 
        html.Replace("{" + key + "}", "{" + key + id + "}")) input
      let result = parameters |> Seq.fold (fun (html:string) (key, value) -> 
        html.Replace("{" + key + id + "}", value)) input
      result 

let generateFile contentTag parameters templateOpt output layoutRoots =
  match templateOpt with
  | Some (file:string) when file.EndsWith("cshtml", true, CultureInfo.InvariantCulture) -> 
      let razor = RazorRender(layoutRoots, [])
      let props = [ "Properties", dict parameters ]
      let generated = razor.ProcessFile(file, props)
      File.WriteAllText(output, generated)      
  | _ ->
      let templateOpt = templateOpt |> Option.map File.ReadAllText
      File.WriteAllText(output, replaceParameters contentTag parameters templateOpt)

