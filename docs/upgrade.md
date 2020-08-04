

# Upgrading to fsdocs

Here are the typical steps to upgrade a repo based on `ProjectScaffold` to use `fsdocs`

1. Run

       dotnet new tool
       dotnet tool install FSharp.Formatting.CommandTool

2. Delete all of `docs\tools` particularly `docs\tool\generate.fsx`.  Keep a copy of any templates for reference as you'll have to copy some bits across to the new template.

3. Put your docs directory so it refelcts the final shape of the site. For example move the content of `docs\input\*` and `docs\files\*` directly to `docs\*`

4. Create `docs\_template.html`, starting with [this file](https://github.com/fsprojects/FSharp.Formatting/blob/master/misc/templates/_template.html) and 
   copying across any snippets from old templates.

5. Run

       dotnet fsdocs watch

   and edit and test your docs.

6. If using FAKE adjust `build.fsx` e.g.

       Target.create "GenerateDocs" (fun _ ->
          Shell.cleanDir ".fsdocs"
          DotNet.exec id "fsdocs" "build --clean" |> ignore
       )

7. Consider creating `docs\_template.fsx` and `docs\_template.ipynb` to enable co-generation of F# scripts and F# notebooks.

   If you add support for notebooks and scripts, consider adding mybinder links to each of your literate executable content pages. [For example like this](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/literate.fsx#L19).

   Also add load sections to make sure your notebooks and scripts contain the right content to load packages out of repo.  [For example like this](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/literate.fsx#L1)

Sample commands: 

    dotnet tool install FSharp.Formatting.CommandTool --local
    git add dotnet-tools.json   
    git rm -fr docs\tools
    git mv docs\input\* docs
    git mv docs\files\* docs
    
    <manually download and fixup the _template.html>

    dotnet fsdocs watch

    touch docs\_template.fsx
    touch docs\_template.ipynb
    git add docs\_template.fsx
    git add docs\_template.ipynb

Here is an example PR: https://github.com/fsprojects/FSharp.Control.AsyncSeq/pull/116

