# Feature: RookRun Authentication

## 1. Overview

### 1.1 Goal
- Status: Draft
- Owner: TBD
- Requested by: bryanc
- Target milestone/date: Next feature after current deployment baseline (target: June 2026)

Protect the deployed RookRun app behind Microsoft Entra ID authentication so all pages and API endpoints require sign-in. Restrict access to a configured allowlist of email addresses in application settings, even within a single-tenant deployment. Ensure unauthenticated users cannot view app data, run jobs, or access pages without first authenticating.

### 1.2 Project Context
RookRun is a hosted app where RookRun.Api serves both API endpoints and the Blazor WebAssembly client (RookRun.Web). The app currently exposes job execution, Strava activities, and run stats through endpoints that are called from client-side typed API clients. Adding authentication at the host/API boundary is the correct place to secure both UX and API surfaces in one deployment.

### 1.3 Problem Statement
- Current behavior: Pages and API endpoints are publicly accessible once deployed.
- Pain points: Any internet user could reach app pages and data endpoints if the host is publicly routable.
- Why now: Authentication is the next required feature before broader deployment confidence.

### 1.4 Success Metrics
- Metric 1: 100% of app pages and API endpoints return authenticated-only behavior (challenge, 401, or 403 for unauthorized users).
- Metric 2: Only users whose email appears in configured allowlist can access the app after Entra sign-in.
- Metric 3: Zero anonymous successful calls to `api/jobs`, `api/jobs/run`, `api/strava/activities`, or `api/strava/run-stats` in validation tests.

## 2. Scope

### 2.1 In Scope
- [x] Microsoft Entra ID login integration for app host (OpenID Connect + cookie auth).
- [x] Enforced authenticated access for pages and API endpoints.
- [x] Email allowlist authorization policy configured via app settings.
- [x] API authn/z enforcement and unauthorized response behavior definition.
- [x] Configuration model for Entra settings and allowed email addresses.
- [x] Setup guide for Entra application registration.

### 2.2 Out of Scope
- [x] Multi-tenant onboarding or self-service user management UI.
- [x] Role/group admin UI inside RookRun.
- [x] Token acquisition for downstream Microsoft Graph APIs.
- [x] Social identity providers other than Microsoft Entra ID.

### 2.3 Constraints
- Technical: Existing architecture is hosted Blazor WASM served by RookRun.Api; auth should fit this shape without splitting into a separate identity service.
- Product: Primary usage is one user today, but deployment must still be protected.
- Operational: Allowlist must be editable via configuration (appsettings/environment) without code changes.
- Operational: Production secrets strategy for v1 is environment variables.

## 3. Acceptance Criteria

1. Given an unauthenticated user opens the app root, when they navigate to `/`, `/jobs`, `/activities`, or `/run-stats`, then they are challenged to sign in with Microsoft Entra ID before page content is available.
2. Given an unauthenticated client calls `GET /api/jobs` or other protected API endpoints, when request reaches controller pipeline, then response is unauthorized (or challenged for browser navigation) and controller action is not executed.
3. Given an authenticated Entra user whose email is not in allowlist, when they access any protected page or API, then access is denied with forbidden behavior.
4. Given an authenticated Entra user whose email is in allowlist, when they access pages and APIs, then all current app functionality remains available.
5. Given allowed emails are changed in configuration and app restarted, when users sign in, then authorization decisions reflect the updated allowlist.
6. Given static assets are requested directly (for example framework or css/js files), when requested anonymously, then they remain reachable while protected app routes and APIs still require authentication.

## 4. Technical Design

### 4.1 Related Existing Areas
- Components/services/files reviewed:
  - `RookRun.Api/Program.cs` (pipeline and service registration, currently no auth)
  - `RookRun.Api/Controllers/JobsController.cs`
  - `RookRun.Api/Controllers/StravaActivitiesController.cs`
  - `RookRun.Api/Controllers/RunStatsController.cs`
  - `RookRun.Api/Middleware/RequestResponseLoggingMiddleware.cs`
  - `RookRun.Web/Program.cs` (typed HttpClient registration)
  - `RookRun.Web/Pages/Home.razor`
  - `RookRun.Web/Pages/Jobs.razor`
  - `RookRun.Web/Pages/Activities.razor`
  - `RookRun.Web/Pages/RunStats.razor`
  - `RookRun.Web/Services/JobsApiClient.cs`
  - `RookRun.Web/Services/StravaActivitiesApiClient.cs`
- Existing contracts/models involved:
  - No contract DTO changes required for v1 auth gate.
  - New auth options model(s) required in API project for Entra + allowlist binding.

### 4.2 Design Options Considered

#### Option A
- Summary: API-hosted OpenID Connect sign-in with cookie auth, global authorization requirement, and allowlist policy using email claim from Entra ID token.
- Pros:
  - Best fit for current hosted architecture where API serves UI and endpoints.
  - One centralized authn/z implementation protects both pages and APIs.
  - Minimal changes to RookRun.Web typed clients (same-origin cookie-based calls).
  - Simplifies deployment by avoiding client-side token plumbing.
- Cons:
  - Requires secure server-side OIDC client configuration and secret management.
  - Requires careful challenge behavior for API requests vs page requests.
- Risks:
  - Missing `email` claim in ID token can block allowed users if token configuration is incomplete.
  - Reverse proxy headers/HTTPS misconfiguration can break redirect URI flows.

#### Option B
- Summary: Blazor WASM authenticates directly with Entra (MSAL), obtains JWT access token, API validates bearer token and applies allowlist policy.
- Pros:
  - Pure token-based API boundary.
  - Easier future expansion to multi-API token forwarding scenarios.
- Cons:
  - More moving parts in WASM and API (token acquisition, token forwarding, scopes/audience).
  - Higher implementation and troubleshooting complexity for current single-app need.
- Risks:
  - CORS/scope/audience misconfiguration can produce hard-to-debug failures.
  - More invasive changes to existing client API service patterns.

### 4.3 Selected Design
- Chosen option: Option A (API-hosted OIDC + cookies + allowlist policy).
- Rationale: Matches current hosted app topology and delivers the required security outcome quickly with lower risk and lower code churn.
- Rejected alternatives and why:
  - Option B rejected for v1 due to added token complexity not required for immediate goal.

### 4.4 Data, Contracts, and Persistence Changes
- API/DTO changes:
  - No changes to existing endpoint request/response DTO contracts.
  - Add optional lightweight endpoint(s) for sign-in/sign-out/status only if needed for UX flow clarity.
- Domain/model changes:
  - Add auth configuration model, for example:
    - `Authentication:Entra:TenantId`
    - `Authentication:Entra:ClientId`
    - `Authentication:Entra:ClientSecret` (or certificate reference)
    - `Authentication:Entra:CallbackPath`
    - `Authentication:AllowedEmailAddresses` (string array)
  - Authorization behavior:
    - Canonical allowlist claim: `email`
    - Matching mode: exact email entries only, case-insensitive compare
- Storage/repository/index changes:
  - No object store or Strava repository persistence schema changes.

### 4.5 Reliability and Error Handling
- Failure modes:
  - OIDC challenge/redirect failure due to invalid authority/redirect URI.
  - Missing/empty `email` claim prevents allowlist evaluation.
  - Authenticated but unauthorized users receive forbidden response.
- Retry/idempotency/concurrency:
  - No additional retry semantics required for auth handshake.
  - Existing job concurrency and object store behavior unchanged.
- Observability/logging:
  - Add structured logging around authentication events and authorization denials (without logging tokens/secrets).
  - Keep request logging middleware in pipeline and include authenticated identity (non-sensitive) where possible.

## 5. UX Design (Optional)

### 5.1 UX Goals
- User cannot access protected content before sign-in.
- Unauthorized users get a clear, non-technical access-denied message.
- Auth flow remains simple and fast for the primary user.

### 5.2 Interaction Notes
- Entry points:
  - First navigation to app root triggers Entra sign-in challenge when unauthenticated.
  - Optional explicit login/logout controls can be added to layout/navigation.
- Happy path:
  - User opens app -> redirected to Entra -> signs in -> returns to requested route -> API calls succeed.
- Edge/error states:
  - Entra sign-in canceled or fails: show retryable error message.
  - Authenticated email not in allowlist: show access denied page with configured contact/help text.
  - Session expires: next protected request triggers challenge.
- Accessibility notes:
  - Ensure focus management and readable messaging on denied/error pages.
  - Keep sign-in/denied text screen-reader friendly.

### 5.3 ASCII Wireframes

#### Screen A (Unauthenticated Redirect Entry)
```text
+--------------------------------------------------------------+
| RookRun                                                      |
+--------------------------------------------------------------+
| You need to sign in to continue.                             |
|                                                              |
| [ Sign in with Microsoft ]                                   |
|                                                              |
| Security: Access is restricted to approved email addresses.  |
+--------------------------------------------------------------+
```

#### Screen B (Authenticated But Not Allowlisted)
```text
+--------------------------------------------------------------+
| RookRun                                                      |
+--------------------------------------------------------------+
| Access denied                                                |
|                                                              |
| Your account is authenticated, but is not approved to use    |
| this application.                                            |
|                                                              |
| [ Sign out ]   [ Retry sign in ]                             |
+--------------------------------------------------------------+
```

## 6. Implementation Plan

### 6.1 Milestones
- [x] M1: Discovery and final design
- [ ] M2: Core implementation
- [ ] M3: Testing and hardening
- [ ] M4: Documentation and release prep

### 6.2 Work Breakdown
- [ ] Add auth configuration models and settings binding in API.
- [ ] Add Entra OIDC + cookie authentication registration in `RookRun.Api/Program.cs`.
- [ ] Add authorization policy for allowed email list and configure fallback policy requiring authenticated users.
- [ ] Configure authorization exclusions so static assets remain accessible while API and app routes stay protected.
- [ ] Apply authorization to controllers (global fallback or `[Authorize]` attributes) and validate API behavior.
- [ ] Add UI support for denied/error/login state as needed in `RookRun.Web` pages/layout.
- [ ] Add sign-out endpoint/flow.
- [ ] Update appsettings samples and deployment documentation with Entra setup.
- [ ] Add/adjust tests in API and Web unit test projects.

### 6.3 Dependencies
- Internal:
  - API host startup and middleware pipeline in `RookRun.Api/Program.cs`.
  - Web page behavior in `RookRun.Web` for unauthorized/forbidden user messaging.
- External:
  - Microsoft Entra tenant and app registration.
  - Secure secret storage for client secret/certificate.

### 6.4 Risks and Mitigations
- Risk: `email` claim not emitted by tenant app registration/token settings.
- Mitigation: Configure optional claims for ID token and validate `email` presence during smoke test.
- Risk: Locking out primary user due to misconfigured allowlist.
- Mitigation: Include startup validation + explicit logs for allowlist load count and redacted diagnostics.
- Risk: Redirect URI mismatch between environment and Entra app registration.
- Mitigation: Document exact redirect URI values for dev/prod and validate during smoke test.

## 7. Tracking

### 7.1 Status Board
- Overall status: Not Started
- Last updated: 2026-06-19

### 7.2 Decision Log
- [2026-06-19] Decision: Use API-hosted OIDC + cookie auth (Option A). Reason: aligns with hosted architecture and least implementation complexity for v1.
- [2026-06-19] Decision: Authorize by configured allowlist of email identities. Reason: explicit access control for single-user deployment.
- [2026-06-19] Decision: Use `email` claim as canonical authorization identity. Reason: requester requirement and explicit human identity mapping.
- [2026-06-19] Decision: Allowlist matching is exact-email only. Reason: reduces accidental over-broad access.
- [2026-06-19] Decision: Static assets do not require auth gating. Reason: requester requirement and simpler static-file hosting.
- [2026-06-19] Decision: Production secret source for v1 is environment variables. Reason: requester requirement for current rollout stage.

### 7.3 Implementation Notes and Lessons
- [2026-06-19] Initial design completed from current architecture review; no existing auth pipeline currently configured.

### 7.4 Open Questions
- [ ] Should production move from environment variables to certificate credentials or Key Vault after v1 stabilization?

### 7.5 Code Review Checklist
- [ ] Authentication middleware ordering is correct (`UseAuthentication` before `UseAuthorization`).
- [ ] No secrets committed to source-controlled appsettings files.
- [ ] Authorization policy is enforced on all API endpoints and app routes.
- [ ] Denied/unauthorized behavior is intentional and user-visible.
- [ ] Logging excludes tokens and sensitive claims.

## 8. Test Plan and Evidence

### 8.1 Test Plan
- Unit tests:
  - Authorization handler/policy tests for allowlist evaluation.
  - Startup/options validation tests for missing auth config.
- Integration tests:
  - API endpoint responses for anonymous (401/challenge), authenticated-not-allowed (403), authenticated-allowed (200/202).
- UI tests (if applicable):
  - Blazor page tests for unauthorized/forbidden messaging and happy-path rendering post-auth.
- Manual validation:
  - Dev and deployed environment login flow validation with one allowed and one denied user.

### 8.2 Test Evidence
- Build/test command:
  - `dotnet build RookRun.slnx -v minimal`
  - `dotnet test RookRun.slnx -v minimal`
- Result summary:
  - Pending implementation.
- Links to relevant artifacts:
  - Pending implementation.

## 9. Implementer Expectations

Implementers are expected to:
- Write production code aligned with this design.
- Add or update automated tests for behavior changes and edge cases.
- Update documentation as behavior and design evolve.
- Capture implementation decisions and lessons in this feature doc.
- Run self-review and request code review before merge.
- Validate Entra setup and configuration in at least one deployment-like environment.

## 10. Documentation Updates

- [ ] Update this feature document with final implementation decisions and test evidence.
- [ ] Update deployment docs with Entra app registration, redirect URIs, and secret configuration.
- [ ] Update architecture/project docs if auth flow or middleware conventions change.

## 11. Entra Setup Steps (Initial Runbook)

1. In Microsoft Entra admin center, create a new App registration:
   - Name: RookRun (or environment-specific equivalent).
   - Supported account types: Single tenant (default for this use case).
2. Configure authentication:
   - Platform: Web.
   - Redirect URI (dev): `https://localhost:<api-port>/signin-oidc`.
   - Redirect URI (prod): `https://<your-host>/signin-oidc`.
  - Add sign-out callback redirect URI: `https://<your-host>/signout-callback-oidc`.
  - Note: Microsoft docs indicate the sign-out callback URI should be added to the app registration; otherwise sign-out redirect can fail.
3. Create credentials:
   - Add a client secret (or certificate for higher security).
  - For this feature, store secret in environment variables (not appsettings source-controlled files).
4. Configure token settings/claims:
  - Ensure `email` claim is emitted for ID token via Entra Token configuration (optional claims) if not already present.
  - Validate that authenticated user token contains `email` before enforcing allowlist in production.
5. Update API configuration with tenant/client/secret and allowed emails.
6. Deploy and validate with:
   - One allowlisted test account.
   - One non-allowlisted test account.

## 12. Discovery Summary (Phase 1 Confirmation Snapshot)

Confirmed from requester input:
- Feature name: RookRun Authentication.
- Main goal: Require login/authentication to access app and data.
- Identity provider: Microsoft Entra ID.
- Authorization rule: Configurable email allowlist via app settings.
- API requirement: Endpoints must enforce authn/z.
- UX relevance: Yes, includes login/denied states.

Assumptions carried into v1 design:
- Single primary user now, but implementation is robust for small allowlist.
- No backward-compatibility migration constraints were specified.
- Entra app registration guidance is required and included above.

## 13. External Guidance Validation (2026-06-19)

The feature design was validated against current Microsoft docs, including:

- ASP.NET Core OIDC web authentication guidance (`view=aspnetcore-9.0`, last updated 2026-01-22):
  - Use confidential interactive client (code flow) for web apps.
  - Prefer backend-authenticated web app flow over public browser-only auth patterns.
  - Enforce auth globally with fallback policy and opt out via AllowAnonymous where needed.
  - Middleware ordering requires `UseRouting` before `UseAuthorization`, and `UseAuthentication` before `UseAuthorization`.
- ASP.NET Core authentication overview (`view=aspnetcore-9.0`, last updated 2026-02-26):
  - Clarifies challenge vs forbid behavior and scheme defaults.
- Entra app registration quickstart (last updated 2026-06-05):
  - Recommends single-tenant selection for organizational/internal apps.
  - Confirms required app identifiers and registration basics.
- Entra optional claims guidance:
  - Optional claims can be configured in Token configuration to ensure claims like `email` are emitted when needed.

Reference URLs:
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-9.0
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-9.0
- https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app
- https://learn.microsoft.com/en-us/entra/identity-platform/optional-claims
