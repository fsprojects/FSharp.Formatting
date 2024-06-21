(**
# Upgrading to fsdocs

Here are the typical steps to upgrade a repo based on `ProjectScaffold` to use `fsdocs`

0 Run
  

      [lang=text]
      dotnet new tool
      dotnet tool install fsdocs-tool
  

1 Delete all of `docs\tools` particularly `docs\tool\generate.fsx`.  Keep a copy of any templates for reference as you'll have to copy some bits across to the new template.
  

2 Put your docs directory so it reflects the final shape of the site. For example move the content of `docs\input\*` and `docs\files\*` directly to `docs\*`
  

3 Follow the notes in [styling](styling.html) to start to style your site.
  

4 Run
  

      [lang=text]
      dotnet fsdocs watch
  

  and edit and test your docs.
  

5 If using FAKE adjust `build.fsx` e.g.
  

      [lang=text]
      Target.create "GenerateDocs" (fun _ ->
         Shell.cleanDir ".fsdocs"
         DotNet.exec id "fsdocs" "build --clean" |> ignore
      )
      
      Target.create "ReleaseDocs" (fun _ ->
          Git.Repository.clone "" projectRepo "temp/gh-pages"
          Git.Branches.checkoutBranch "temp/gh-pages" "gh-pages"
          Shell.copyRecursive "output" "temp/gh-pages" true |> printfn "%A"
          Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" "add ." |> printfn "%s"
          let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" release.NugetVersion
          Git.CommandHelper.runSimpleGitCommand "temp/gh-pages" cmd |> printfn "%s"
          Git.Branches.push "temp/gh-pages"
      )
  

6 Consider creating `docs\_template.fsx` and `docs\_template.ipynb` to enable co-generation of F# scripts and F# notebooks.
  

  If you add support for notebooks and scripts, consider adding mybinder links to each of your literate executable content pages. For example [like this](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/literate.fsx#L19).
  

  Also add load sections to make sure your notebooks and scripts contain the right content to load packages out of repo.  For example [like this](https://github.com/fsprojects/FSharp.Formatting/blob/master/docs/literate.fsx#L1).
  

Sample commands:

    [lang=text]
    dotnet tool install fsdocs-tool --local
    git add dotnet-tools.json   
    git rm -fr docs/tools
    git mv docs/input/* docs
    git mv docs/files/* docs
    
    <manually download and fixup the _template.html>

    dotnet fsdocs watch

    touch docs/_template.fsx
    touch docs/_template.ipynb
    git add docs/_template.fsx
    git add docs/_template.ipynb

Here is an example PR: [https://github.com/fsprojects/FSharp.Control.AsyncSeq/pull/116](https://github.com/fsprojects/FSharp.Control.AsyncSeq/pull/116)

*)

