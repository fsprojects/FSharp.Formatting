If you want to upgrade the [template](https://github.com/fsprojects/ProjectScaffold/blob/master/docsrc/tools/generate.template) that turns into `generate.fsx` 

The minimum thing you need to do is change the following:

In `generate.fsx`

- Add `open FSharp.Formatting.Razor` in order to continue using Razor templates (since v2 uses them)
- `Literate` to `RazorLiterate`
- `ApiDocs` to `RazorMetadataFormat`

In the `paket.dependencies` you need to do the following:

- Add `source https://ci.appveyor.com/nuget/fsharp-formatting`
- And pick out the latest version (at this moment it's 
`3.0.0-beta09`), i.e. add `nuget FSharp.Formatting ~> 3.0.0-beta09`

If you have customized the generation, you might note that many of the DU types have additional parameters and are more type safe.