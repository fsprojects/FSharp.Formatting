@echo off

set PAKET_SKIP_RESTORE_TARGETS true

dotnet tool restore
dotnet paket restore
dotnet run --project .\build\build.fsproj -- %*