# Project Map

## Solution Overview

- Solution: `RookRun.slnx`
- Runtime: .NET SDK 10, projects target `net10.0`
- Main hosted app shape:
  - `RookRun.Api`: ASP.NET Core host for APIs and static hosting for Blazor WebAssembly
  - `RookRun.Web`: Blazor WebAssembly UI
  - `RookRun.Contracts`: shared API DTO contracts

## Project Responsibilities

- `RookRun.Api`: HTTP endpoints, dependency wiring, host startup, serves UI
- `RookRun.Web`: pages, components, and browser-side services
- `RookRun.Contracts`: API request/response DTOs and enums
- `RookRun.Cli`: job runner host for local/ops workflows
- `RookRun.Job`: job implementations and orchestration
- `RookRun.Strava`: Strava clients, repositories, synchronizers
- `RookRun.ObjectStore`: persistence abstraction and concrete object stores
- `RookRun.Common`: reusable utilities and shared exceptions
- `RookRun.UnitTest`: backend and integration-style unit tests
- `RookRun.Web.UnitTest`: UI/unit tests for Blazor pages and components

## Key Architecture Boundaries

- Keep API DTOs in `RookRun.Contracts`; do not reuse internal persistence/domain models as API contracts.
- Prefer `IObjectStore`-based persistence through repository abstractions.
- Keep UI page routes in page files and move substantial view logic into child components.
- Put Strava-related tests in Strava folder/namespace under `RookRun.UnitTest`.

## Common Change Routing

- API contract change:
  1. Update `RookRun.Contracts`
  2. Update API endpoint usage in `RookRun.Api`
  3. Update client usage in `RookRun.Web`
  4. Add/update tests in `RookRun.UnitTest` and/or `RookRun.Web.UnitTest`

- Persistence/repository change:
  1. Update repository in `RookRun.Strava` or related domain project
  2. Keep object paths and serialization aligned with `objectstore-paths-and-serialization.md`
  3. Add tests in `RookRun.UnitTest/ObjectStore` or relevant folder

- UI change:
  1. Update page and extracted components in `RookRun.Web/Pages`
  2. Add/update tests in `RookRun.Web.UnitTest/Pages`

## Build and Run Quick Reference

- Build: `dotnet build RookRun.slnx -v minimal`
- Run hosted app: `dotnet run --project RookRun.Api/RookRun.Api.csproj`
- Run CLI: `dotnet run --project RookRun.Cli/RookRun.Cli.csproj`
