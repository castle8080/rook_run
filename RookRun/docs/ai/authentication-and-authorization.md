# Authentication and Authorization

This document describes the current auth model for the hosted app (`RookRun.Api` + `RookRun.Web`) and coding guidance for future changes.

## Runtime Model

- Authentication:
  - Cookie auth for server session.
  - OpenID Connect challenge/sign-out via Microsoft Entra ID.
- Authorization:
  - Global fallback policy requires authenticated user plus allowlist requirement.
  - Anonymous access must be explicit via `[AllowAnonymous]`.

## Core Pipeline (API Host)

In `RookRun.Api/Program.cs`:

- `UseAuthentication()` runs before `UseAuthorization()`.
- Cookie events return `401/403` for `/api/*` requests instead of HTML redirects.
- `MapControllers()` exposes API and auth controller endpoints.
- `MapFallback("/api/{*path}", ... NotFound())` ensures unknown API routes are `404`.
- `MapFallbackToFile("index.html")` serves Blazor SPA routes and is protected with authorization metadata.

## Policy and Requirement

- `FallbackPolicy` enforces:
  - `RequireAuthenticatedUser()`
  - `AllowedEmailRequirement`
- `AllowedEmailAuthorizationHandler` resolves email from claims and compares against configured allowlist.

Claim resolution order in handler:

1. `email`
2. `ClaimTypes.Email`
3. `preferred_username`
4. `upn`

## Auth Endpoints

In `RookRun.Api/Controllers/AuthController.cs`:

- `GET /auth/sign-in`: starts OIDC challenge.
- `GET /auth/sign-out`: signs out cookie + OIDC session.
- `GET /auth/access-denied`: user-facing deny page.
- `GET /auth/signed-out`: post-logout confirmation page.
- `GET /auth/me`: lightweight auth-state endpoint used by WASM frontend.

## Frontend Auth State (Blazor WASM)

In `RookRun.Web`:

- `ApiAuthenticationStateProvider` calls `/auth/me` and creates `ClaimsPrincipal`.
- `Program.cs` registers `AddAuthorizationCore()` and provider.
- `App.razor` wraps router in `CascadingAuthenticationState`.

This keeps client auth state aligned with server cookie state after sign-in/sign-out.

## Configuration

`Authentication` config binds to `RookRunAuthenticationOptions` and validates on startup:

- `Entra:TenantId`
- `Entra:ClientId`
- `Entra:ClientSecret`
- `Entra:CallbackPath`
- `Entra:SignedOutCallbackPath`
- `AllowedEmailAddresses` (non-empty)

## Common Pitfalls

- Do not use relative `post_logout_redirect_uri` values; Entra requires absolute URI.
- Do not serve SPA shell for unknown API routes.
- Keep static files before auth middleware so framework assets can load during challenge flow.
- Prefer fallback-policy protection over per-controller `[Authorize]` sprawl; use `[AllowAnonymous]` only where needed.
