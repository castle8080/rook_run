# RookRun

RookRun is a .NET solution for fitness data workflows and job execution.

## Projects

- RookRun.Api: ASP.NET Core host for HTTP APIs and the Blazor WebAssembly UI
- RookRun.Web: Blazor WebAssembly client UI (served by RookRun.Api)
- RookRun.Contracts: Shared DTOs/contracts used by API and client
- RookRun.Cli: Existing CLI host for running jobs directly
- RookRun.Job and supporting libraries: Job implementations and integrations

## Prerequisites

- .NET SDK 10

## Build

From the repository root:

```bash
dotnet build RookRun.slnx -v minimal
```

## Run Web API and UI

The API and UI are hosted together by RookRun.Api.

1. Start the host from repository root:

```bash
dotnet run --project RookRun.Api/RookRun.Api.csproj
```

2. Open the app:
- UI landing page: http://localhost:5231/
- Jobs page: http://localhost:5231/jobs
- Jobs API list endpoint: http://localhost:5231/api/jobs

### Notes

- The default HTTP URL comes from RookRun.Api launch settings.
- If you run with the HTTPS launch profile, HTTPS is available at https://localhost:7009.

## Optional: Run CLI Jobs

```bash
dotnet run --project RookRun.Cli/RookRun.Cli.csproj
```
