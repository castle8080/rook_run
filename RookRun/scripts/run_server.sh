#!/bin/sh

cd "$(dirname "$0")/.." || exit 1

exec dotnet run --project RookRun.Api/RookRun.Api.csproj --launch-profile https -c Release
