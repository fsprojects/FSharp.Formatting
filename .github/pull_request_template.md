# Before you go

Hi there! Thank you for your contribution to our project. To help maintain the quality of our codebase, our Continuous Integration (CI) system will automatically run several checks on your submission. To streamline the process, we kindly ask you to perform these checks locally before pushing your changes:

To execute all build scripts, please run:

```shell
dotnet fsi build.fsx
```

If you encounter any formatting issues, you can auto-correct them by running:

```shell
dotnet fantomas build.fsx src tests docs
```

Should any tests fail, please review and adjust your changes accordingly.

We appreciate your efforts to contribute and look forward to reviewing your pull request!

**Please remove this placeholder from the PR description after reading it and replace it with a clear, meaningful description of the changes in this PR.**