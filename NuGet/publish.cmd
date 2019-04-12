@for %%f in (..\bin\*.nupkg) do @..\packages\build\NuGet.CommandLine\tools\NuGet.exe push %%f
