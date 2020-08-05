namespace FSharp.Formatting.Templating

open System.IO

#if DEBUG
type ParamKey = ParamKey of string
#else
/// An abbreviation for 'string' representing a strong name for a parameter key
type ParamKey = string
#endif

/// A list of parameters indexed by parameter keys
type Parameters = (ParamKey * string) list

#if !DEBUG
[<AutoOpen>]
/// Defines the parameter keys known to FSharp.Formatting processing code
module internal ParamKeyUtils =
    let ParamKey (c: string) : ParamKey = c
    let (|ParamKey|) (c: ParamKey) : string = c
#endif

/// Defines the parameter keys known to FSharp.Formatting processing code
[<RequireQualifiedAccess>]
module ParamKeys =

    /// A parameter key known to FSharp.Formatting
    let ``root`` = ParamKey "root"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-authors`` = ParamKey "fsdocs-authors"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-collection-name`` = ParamKey "fsdocs-collection-name"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-content`` = ParamKey "fsdocs-content"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-collection-name-link`` = ParamKey "fsdocs-collection-name-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-copyright`` = ParamKey "fsdocs-copyright"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-license-link`` = ParamKey "fsdocs-license-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-list-of-namespaces`` = ParamKey "fsdocs-list-of-namespaces"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-list-of-documents`` = ParamKey "fsdocs-list-of-documents"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-logo-link`` = ParamKey "fsdocs-logo-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-logo-src`` = ParamKey "fsdocs-logo-src"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-navbar-position`` = ParamKey "fsdocs-navbar-position"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-license-expression`` = ParamKey "fsdocs-package-license-expression"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-project-url`` = ParamKey "fsdocs-package-project-url"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-tags`` = ParamKey "fsdocs-package-tags"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-version`` = ParamKey "fsdocs-package-version"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-package-icon-url`` = ParamKey "fsdocs-package-icon-url"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-page-title`` = ParamKey "fsdocs-page-title"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-page-source`` = ParamKey "fsdocs-page-source"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-release-notes-link`` = ParamKey "fsdocs-release-notes-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-branch`` = ParamKey "fsdocs-repository-branch"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-commit`` = ParamKey "fsdocs-repository-commit"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-link`` = ParamKey "fsdocs-repository-link"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-repository-type`` = ParamKey "fsdocs-repository-type"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-theme`` = ParamKey "fsdocs-theme"

    /// A parameter key known to FSharp.Formatting
    let ``fsdocs-tooltips`` = ParamKey "fsdocs-tooltips"

module internal SimpleTemplating =

    // Replace '{{xyz}}' or '{xyz}' in template text
    let replaceParameters (parameters:seq<ParamKey * string>) (templateTextOpt: string option) =
      match templateTextOpt with
      | None | Some "" ->
          // If there is no template or the template is an empty file, return just document + tooltips (tooltips empty if not HTML)
          let lookup = parameters |> dict
          (if lookup.ContainsKey ParamKeys.``fsdocs-content`` then lookup.[ParamKeys.``fsdocs-content``] else "") +
          (if lookup.ContainsKey ParamKeys.``fsdocs-tooltips`` then "\n\n" + lookup.[ParamKeys.``fsdocs-tooltips``] else "")
      | Some templateText ->
          // First replace {{key}} or {key} with some uglier keys and then replace them with values
          // (in case one of the keys appears in some other value)
          let id = System.Guid.NewGuid().ToString("d")
          let temp =
              (templateText, parameters) ||> Seq.fold (fun text (ParamKey key, _value) ->
                let key2 = "{{" + key + "}}"
                let rkey = "{" + key + id + "}"
                let text = text.Replace(key2, rkey) 
                text)
          let result =
              (temp, parameters) ||> Seq.fold (fun text (ParamKey key, value) ->
                  text.Replace("{" + key + id + "}", value)) 
          result

    let UseFileAsSimpleTemplate (parameters, templateOpt, outputFile) =
        let templateTextOpt = templateOpt |> Option.map System.IO.File.ReadAllText
        let outputText = replaceParameters parameters templateTextOpt
        try
            let path = Path.GetFullPath(outputFile) |> Path.GetDirectoryName
            Directory.CreateDirectory(path) |> ignore
        with _ -> ()
        File.WriteAllText(outputFile, outputText)

    // Replace '{{xyz}}' in text
    let ReplaceParametersInText (parameters:seq<ParamKey * string>) (text: string) =
        let id = System.Guid.NewGuid().ToString("d")
        let temp =
            (text, parameters) ||> Seq.fold (fun text (ParamKey key, _value) ->
                let key2 = "{{" + key + "}}"
                let rkey = "{" + key + id + "}"
                let text = text.Replace(key2, rkey)
                text)
        let result =
            (temp, parameters) ||> Seq.fold (fun text (ParamKey key, value) ->
                text.Replace("{" + key + id + "}", value)) 
        result
