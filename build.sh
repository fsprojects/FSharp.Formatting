#!/bin/bash
if [ ! -f packages/FAKE/tools/FAKE.exe ]; then
  mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion -Prerelease
fi
mono packages/FAKE/tools/FAKE.exe build.fsx $@
