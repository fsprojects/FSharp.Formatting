#!/usr/bin/env bash

cd `dirname $0`

IMAGE_LABEL="fsharp-formatting-build"

# docker build
docker build \
    --build-arg "GIT_USER_NAME=$(git config user.name)" \
    --build-arg "GIT_USER_EMAIL=$(git config user.email)" \
    -t $IMAGE_LABEL \
    .

# run fake in docker container
docker run --rm \
    -e GITHUB_TOKEN=$GITHUB_TOKEN \
    -e NUGET_KEY=$NUGET_KEY \
    $IMAGE_LABEL \
    ./build.sh "$@"