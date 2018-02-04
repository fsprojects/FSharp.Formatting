
REM Install .NET Core (https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "&([scriptblock]::Create((Invoke-WebRequest -useb 'https://dot.net/v1/dotnet-install.ps1'))) -Channel 2.1.4"

SET PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%
dotnet restore dotnet-fake.csproj
dotnet fake %*