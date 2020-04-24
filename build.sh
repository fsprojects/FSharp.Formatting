#!/usr/bin/env bash

cd `dirname $0`

export PAKET_SKIP_RESTORE_TARGETS=true

dotnet tool restore && \
dotnet paket restore && \
dotnet fake run build.fsx "$@"