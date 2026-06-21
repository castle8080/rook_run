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

## Authentication Setup

RookRun requires Microsoft Entra ID authentication. You will need an Entra app registration configured before the app will start (startup validation enforces this).

### Entra App Registration

1. Register an app in the [Microsoft Entra admin center](https://entra.microsoft.com).
2. Set the redirect URI (Web platform): `https://localhost:7009/signin-oidc`
3. Set the sign-out redirect URI: `https://localhost:7009/signout-callback-oidc`
4. Create a client secret under Certificates & Secrets and note the secret value.
5. Enable the `email` optional claim on the ID token (Token configuration > Optional claims).

### Dev Certificates

Authentication requires HTTPS. Trust the local dev certificate once:

```bash
dotnet dev-certs https --trust
```

On Linux this has limited browser trust effect; you may need to import the certificate into your browser's trust store manually.

### Local Secrets

Use dotnet user-secrets to supply Entra credentials for local development (run from `RookRun.Api/`):

```bash
dotnet user-secrets set "Authentication:Entra:TenantId" "<your-tenant-id>"
dotnet user-secrets set "Authentication:Entra:ClientId" "<your-client-id>"
dotnet user-secrets set "Authentication:Entra:ClientSecret" "<your-client-secret>"
dotnet user-secrets set "Authentication:AllowedEmailAddresses:0" "you@example.com"
```

The `var/` directory is git-ignored and safe for scratch files like a local secrets command list. Never commit secrets to source control.

## Run Web API and UI

The API and UI are hosted together by RookRun.Api. Authentication requires HTTPS, so use the `https` launch profile:

1. Start the host from repository root:

```bash
dotnet run --project RookRun.Api/RookRun.Api.csproj --launch-profile https
```

2. Open the app:
- UI landing page: https://localhost:7009/
- Jobs page: https://localhost:7009/jobs
- Jobs API list endpoint: https://localhost:7009/api/jobs

### Notes

- HTTPS port 7009 and HTTP port 5231 are both active when running the `https` profile.
- The `http` profile (default `dotnet run`) listens on HTTP only and will not work with Entra authentication.
- For production, supply credentials via environment variables using `__` as the config separator (e.g. `Authentication__Entra__TenantId`).

## Optional: Run CLI Jobs

```bash
dotnet run --project RookRun.Cli/RookRun.Cli.csproj
```
