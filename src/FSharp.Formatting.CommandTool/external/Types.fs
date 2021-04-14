namespace Ionide2.ProjInfo

open System

module Types =

    type ProjectSdkInfo =
        { IsTestProject: bool
          Configuration: string // Debug
          IsPackable: bool // true
          TargetFramework: string // netcoreapp1.0
          TargetFrameworkIdentifier: string // .NETCoreApp
          TargetFrameworkVersion: string // v1.0

          MSBuildAllProjects: string list //;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\FSharp.NET.Sdk\Sdk\Sdk.props;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\Sdk\Sdk.props;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Sdk.props;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Sdk.DefaultItems.props;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.SupportedTargetFrameworks.props;e:\github\DotnetNewFsprojTestingSamples\sdk1.0\sample1\c1\obj\c1.fsproj.nuget.g.props;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\FSharp.NET.Sdk\Sdk\Sdk.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\Sdk\Sdk.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Sdk.BeforeCommon.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.DefaultAssemblyInfo.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.DefaultOutputPaths.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.TargetFrameworkInference.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.RuntimeIdentifierInference.targets;C:\Users\e.sada\.nuget\packages\fsharp.net.sdk\1.0.5\build\FSharp.NET.Core.Sdk.targets;e:\github\DotnetNewFsprojTestingSamples\sdk1.0\sample1\c1\c1.fsproj;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Microsoft.Common.CurrentVersion.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\NuGet.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\15.0\Microsoft.Common.targets\ImportAfter\Microsoft.TestPlatform.ImportAfter.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Microsoft.TestPlatform.targets;e:\github\DotnetNewFsprojTestingSamples\sdk1.0\sample1\c1\obj\c1.fsproj.nuget.g.targets;e:\github\DotnetNewFsprojTestingSamples\sdk1.0\sample1\c1\obj\c1.fsproj.proj-info.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Sdk.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Sdk.Common.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.PackageDependencyResolution.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Sdk.DefaultItems.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.DisableStandardFrameworkResolution.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.GenerateAssemblyInfo.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.Publish.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\Microsoft.NET.Sdk\build\Microsoft.NET.PreserveCompilationContext.targets;C:\dotnetcli\dotnet-dev-win-x64.1.0.4\sdk\1.0.4\Sdks\NuGet.Build.Tasks.Pack\build\NuGet.Build.Tasks.Pack.targets
          MSBuildToolsVersion: string // 15.0

          ProjectAssetsFile: string // e:\github\DotnetNewFsprojTestingSamples\sdk1.0\sample1\c1\obj\project.assets.json
          RestoreSuccess: bool // True

          Configurations: string list // Debug;Release
          TargetFrameworks: string list // netcoreapp1.0;netstandard1.6

          //may not exists
          RunArguments: string option // exec "e:\github\DotnetNewFsprojTestingSamples\sdk1.0\sample1\c1\bin\Debug\netcoreapp1.0\c1.dll"
          RunCommand: string option // dotnet

          //from 2.0
          IsPublishable: bool option } // true

    type ProjectReference =
        { RelativePath: string
          ProjectFileName: string
          TargetFramework: string }

    type Property = { Name: string; Value: string }

    type PackageReference =
        { Name: string
          Version: string
          FullPath: string }

    type ProjectOutputType =
        | Library
        | Exe
        | Custom of string

    type ProjectItem = Compile of name: string * fullpath: string

    type ProjectOptions =
        { ProjectId: string option
          ProjectFileName: string
          TargetFramework: string
          SourceFiles: string list
          OtherOptions: string list
          ReferencedProjects: ProjectReference list
          PackageReferences: PackageReference list
          LoadTime: DateTime
          TargetPath: string
          ProjectOutputType: ProjectOutputType
          ProjectSdkInfo: ProjectSdkInfo
          Items: ProjectItem list
          CustomProperties: Property list }

    type CompileItem =
        { Name: string
          FullPath: string
          Link: string option }

    type ToolsPath = internal ToolsPath of string


    type GetProjectOptionsErrors =
        // projFile is duplicated in WorkspaceProjectState???
        | ProjectNotRestored of projFile: string
        | ProjectNotFound of projFile: string
        | LanguageNotSupported of projFile: string
        | ProjectNotLoaded of projFile: string
        | MissingExtraProjectInfos of projFile: string
        | InvalidExtraProjectInfos of projFile: string * error: string
        | ReferencesNotLoaded of projFile: string * referenceErrors: seq<string * GetProjectOptionsErrors>
        | GenericError of projFile: string * string
        member x.ProjFile =
            match x with
            | ProjectNotRestored projFile
            | LanguageNotSupported projFile
            | ProjectNotLoaded projFile
            | MissingExtraProjectInfos projFile
            | InvalidExtraProjectInfos (projFile, _)
            | ReferencesNotLoaded (projFile, _)
            | GenericError (projFile, _) -> projFile
            | ProjectNotFound (projFile) -> projFile

    [<RequireQualifiedAccess>]
    type WorkspaceProjectState =
        | Loading of string
        | Loaded of loadedProject: ProjectOptions * knownProjects: ProjectOptions list * fromCache: bool
        | Failed of string * GetProjectOptionsErrors

        member x.ProjFile =
            match x with
            | Loading proj -> proj
            | Loaded (lp, _, _) -> lp.ProjectFileName
            | Failed (proj, _) -> proj

        member x.DebugPrint =
            match x with
            | Loading proj -> "Loading: " + proj
            | Loaded (lp, _, _) -> "Loaded: " + lp.ProjectFileName
            | Failed (proj, _) -> "Failed: " + proj
