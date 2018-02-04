#!/bin/bash

# Install .NET Core (https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel Current

PATH="~/.dotnet:$PATH"
dotnet restore dotnet-fake.csproj
dotnet fake $@