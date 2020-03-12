#!/usr/bin/env bash

cd `dirname $0`

IMAGE_LABEL="fsharp-formatting-build"

# docker build
docker build -t $IMAGE_LABEL .

# dotnet build, test & nuget publish
docker run -t --rm \
           -e GITHUB_TOKEN=$GITHUB_TOKEN \
           -e NUGET_KEY=$NUGET_KEY \
           $IMAGE_LABEL \
           ./build.sh "$@"