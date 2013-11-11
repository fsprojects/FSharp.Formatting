@echo off
if not exist packages\FAKE\tools\Fake.exe ( 
  .nuget\nuget.exe install FAKE -OutputDirectory packages -Prerelease -ExcludeVersion  
)
packages\FAKE\tools\FAKE.exe build.fsx %*
pause
