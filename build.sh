#!/usr/bin/env bash

cd `dirname $0`

dotnet tool restore && \
dotnet paket restore && \
dotnet fake run build.fsx "$@"