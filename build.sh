#!/bin/bash
if test "$OS" = "Windows_NT"
then
  cmd /C build.cmd
else
  dotnet tool install fake-cli --tool-path .fake --version 5.12.6
  # dotnet tool install paket --tool-path .paket

  mono .paket/paket.exe restore
  exit_code=$?
  if [ $exit_code -ne 0 ]; then
    exit $exit_code
  fi
  
 .fake/fake run build.fsx $@
fi