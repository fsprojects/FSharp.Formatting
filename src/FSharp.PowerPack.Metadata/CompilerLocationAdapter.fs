namespace Internal.Utilities
open System
open System.IO
open System.Configuration
open System.Reflection
open Microsoft.Win32
open System.Runtime.InteropServices

#nowarn "44" // ConfigurationSettings is obsolete but the new stuff is horribly complicated. 

module internal FSharpEnvironment =
    open FSharp.CompilerBinding

    let FSharpCoreLibRunningVersion = 
        FSharpEnvironment.FSharpCoreLibRunningVersion
    let BinFolderOfDefaultFSharpCoreReferenceAssembly = 
        FSharpEnvironment.FolderOfDefaultFSharpCore(FSharpCompilerVersion.LatestKnown, FSharpTargetFramework.NET_4_0)
    let BinFolderOfDefaultFSharpCompiler = 
        FSharpEnvironment.BinFolderOfDefaultFSharpCompiler FSharpCompilerVersion.LatestKnown
    let BinFolderOfFSharpPowerPack = 
        try 
            // Check for an app.config setting to redirect the default compiler location
            // Like fsharp-compiler-location
            let result = FSharpEnvironment.tryAppConfig "fsharp-powerpack-location"
            match result with 
            | Some _ ->  result 
            | None -> 

                let key20 = @"Software\Microsoft\.NETFramework\AssemblyFolders\FSharp.PowerPack-" + FSharpEnvironment.FSharpTeamVersionNumber 
                let result = FSharpEnvironment.tryRegKey key20
                match result with 
                | Some _ ->  result 
                | None ->                       
                    FSharpEnvironment.tryCurrentDomain()

        with e -> 
            System.Diagnostics.Debug.Assert(false, "Error while determining default location of F# power pack tools")
            None
