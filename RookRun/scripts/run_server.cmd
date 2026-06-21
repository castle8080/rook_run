@echo off

cd /d "%~dp0.."

dotnet run --project RookRun.Api/RookRun.Api.csproj --launch-profile https -c Release
