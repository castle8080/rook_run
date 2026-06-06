# Strava OAuth Client Design

## Goal

Design a reusable C# library that performs Strava OAuth using Strava's own authorization flow while presenting a simple async experience to callers.

The desired consumer experience is:

- Call a single async method such as `AcquireTokenAsync(...)`
- The library starts a temporary local HTTP listener using ASP.NET Core minimal APIs
- The library opens the system browser to Strava's authorization endpoint
- Strava redirects back to the local callback endpoint with an authorization code
- The callback handler exchanges the code for tokens
- The original caller asynchronously awaits completion and receives the token result
- The temporary listener shuts down cleanly when complete

## Non-Goals

This design does not initially cover:

- Full persistent token storage implementation
- Multi-user web application sign-in
- Background refresh daemon design
- Support for identity providers other than Strava
- Additional OAuth flow variants beyond the documented Strava authorization code flow used by this design

## Desired User Experience

The consuming code should look roughly like this:

- `await stravaOAuthClient.AcquireTokenAsync(cancellationToken)`

Behavior from the caller's perspective:

1. The method starts work asynchronously
2. A browser window opens for the user
3. The task remains pending while the user authenticates and consents
4. Once Strava redirects back and the code is exchanged, the task completes with the token payload
5. On failure, the task faults with a meaningful exception
6. On cancellation or timeout, the task is canceled and the temporary listener is shut down

## High-Level Architecture

The library should be composed of the following pieces:

1. `IStravaOAuthClient`
2. `StravaOAuthClient`
3. `StravaOAuthListenerHost`
4. `StravaOAuthCallbackCoordinator`
5. `IStravaAuthorizationLauncher`
6. `IStravaTokenExchangeClient`

### Core idea

The central coordination primitive should be a `TaskCompletionSource<T>` owned by a short-lived in-memory coordinator. The callback endpoint completes that task when the authorization flow succeeds or fails. The outer client awaits that task, making the consumer-facing API naturally asynchronous.

## Proposed Public API

```csharp
public interface IStravaOAuthClient
{
	Task<StravaOAuthTokenResult> AcquireTokenAsync(CancellationToken cancellationToken = default);
}
```

Naming note:

- The public interface should avoid exposing implementation details such as browser interactivity in the method name.
- The method name `AcquireTokenAsync` expresses the consumer intent while allowing different implementations behind the abstraction.
- The public interface should also avoid exposing client configuration details that belong to construction-time options.

Design note:

- Configuration such as client ID, client secret, callback host, callback path, authorization URLs, scopes, timeout, and browser-launch behavior belongs in client options or construction-time dependencies, not in a public request model.
- The public interface should stay minimal and represent token acquisition intent only.

### Result model

```csharp
public sealed record StravaOAuthTokenResult
{
	public required string AccessToken { get; init; }
	public required string RefreshToken { get; init; }
	public required long ExpiresAtUnixTimeSeconds { get; init; }
	public required string TokenType { get; init; }
	public required IReadOnlyList<string> GrantedScopes { get; init; }
	public string? RawScope { get; init; }
	public JsonElement? Athlete { get; init; }
	public Dictionary<string, JsonElement> AdditionalData { get; init; } = [];
	public string RawResponseJson { get; init; } = string.Empty;
}
```

Notes on response preservation:

- `JsonElement` is intentionally exposed in V1 for lossless payload preservation.
- This keeps the contract flexible while Strava-specific response shapes evolve.
- Higher-level typed wrappers can be added later without losing access to the raw response fields.

Port selection behavior:

- If the configured `CallbackPort` is not specified, the listener binds to port `0` and lets the OS choose an open loopback port.
- If the configured `CallbackPort` is specified, the listener attempts to bind to that exact port.
- The library must build the final `redirect_uri` from the actual bound address after listener startup.

## Main Sequence

1. Caller invokes `AcquireTokenAsync`
2. Client validates input
3. Client creates a unique flow state value
4. Client creates a coordinator instance containing:
   - flow identifier
   - expected state
   - `TaskCompletionSource<StravaOAuthTokenResult>`
   - timeout metadata
5. Client starts a temporary minimal API host bound to localhost on an automatically selected open port by default
6. Client reads the effective callback URI from the running listener session
7. Client constructs the Strava authorize URL using that effective callback URI
8. Client opens the system browser to the authorize URL
9. User authenticates and approves scopes in Strava
10. Strava redirects to the local callback endpoint with `code` and `state`
11. Callback endpoint validates state
12. Callback endpoint exchanges the authorization code for tokens
13. Callback endpoint completes the coordinator task result in memory
14. Callback endpoint returns a small HTML success page to the browser
15. Outer client awaits the task completion source and receives the token result
16. Outer client stops and disposes the temporary host
17. Caller receives the final token result as the awaited async result

## Component Design

### 1. `IStravaOAuthClient`

Responsibility:

- Entry point for the interactive OAuth flow
- Owns the overall orchestration
- Presents the async wait experience to the caller

Key behavior:

- One public async method
- Creates and tears down the temporary listener host
- Creates state and coordinator objects
- Applies timeout and cancellation
- Starts the temporary listener during method invocation and tears it down before the method returns on success, failure, cancellation, or timeout

### 2. `StravaOAuthListenerHost`

Responsibility:

- Starts and stops the temporary minimal API server on localhost
- Maps the callback and success endpoints

Possible API:

```csharp
public interface IStravaOAuthListenerHost
{
	Task<IStravaOAuthListenerSession> StartAsync(
		StravaOAuthListenerOptions options,
		CancellationToken cancellationToken = default);
}
```

A returned session object should allow clean disposal of the host.

Important details:

- Use ASP.NET Core minimal APIs
- Bind only to loopback, with `localhost` as the default configured host
- Prefer automatic port allocation by binding to port `0`
- Return the effective bound callback URI to the caller after startup
- Allow callers to request a fixed port only as an override
- If a fixed port is requested and already in use, fail clearly
- Keep the HTTP pipeline intentionally lean: no authentication middleware, no MVC, no static files, no routing beyond the callback and optional success endpoint, and no services unrelated to the authorization flow
- Keep the listener self-contained: it must not depend on the main application host, unrelated app service registrations, shared web middleware, or ambient routing configuration

### 3. `StravaOAuthCallbackCoordinator`

Responsibility:

- Bridge the callback endpoint and the awaiting caller
- Maintain in-memory state for exactly one authorization flow or a small set of concurrent flows

Recommended design:

- Internal class using `ConcurrentDictionary<string, PendingAuthorizationFlow>`
- Each pending flow contains:
  - `State`
  - `TaskCompletionSource<StravaOAuthTokenResult>`
  - `CancellationTokenSource` or timeout metadata
  - Created timestamp

Core methods:

- `CreatePendingFlow(...)`
- `TryGetPendingFlow(state, out flow)`
- `CompleteSuccess(state, result)`
- `CompleteFailure(state, exception)`
- `Cancel(state)`
- `Remove(state)`

This is the in-memory mechanism that lets the callback endpoint notify the original async caller.

Concurrency and cleanup rules:

- Each pending flow must move to exactly one terminal state.
- The pending-flow entry must be created in the active dictionary before the browser is opened.
- Completion methods should use `TrySetResult`, `TrySetException`, and `TrySetCanceled`.
- Races between callback completion, timeout, and cancellation are expected and must be harmless.
- `Remove(state)` must be idempotent and should use `TryRemove`.
- Every flow must be removed exactly once from the active dictionary, even if multiple terminal signals arrive.
- Success, callback error, timeout, and cancellation must all converge on the same terminal cleanup pattern.

### 4. `IStravaAuthorizationLauncher`

Responsibility:

- Open the user's browser to the Strava authorize URL

Suggested default implementation:

- Use `Process.Start` with `UseShellExecute = true`
- This should open the default system browser

V1 behavior:

- If browser launch fails, throw a browser-launch exception and terminate the flow

### 5. `IStravaTokenExchangeClient`

Responsibility:

- Call Strava's token endpoint
- Exchange authorization code for token result
- Optionally support refresh-token exchange in the future

Suggested methods:

```csharp
public interface IStravaTokenExchangeClient
{
	Task<StravaOAuthTokenResult> ExchangeAuthorizationCodeAsync(
		StravaAuthorizationCodeExchangeRequest request,
		CancellationToken cancellationToken = default);
}
```

This component should isolate HTTP concerns from the listener and coordinator.

## Minimal API Endpoints

The temporary host should define at least these endpoints.

### GET callback endpoint

Example path:

- `/auth/strava/callback`

Query parameters expected from Strava:

- `code`
- `state`
- possibly `scope`
- possibly `error`

Processing steps:

1. Read query parameters
2. If `error` exists, complete the pending flow with failure
3. Validate `state` against the coordinator
4. Exchange `code` for tokens with Strava
5. Complete the pending flow task
6. Redirect or render a simple success page

### GET success endpoint

Example path:

- `/auth/strava/success`

Purpose:

- Show a friendly confirmation page that includes completion status and, when available, granted scopes and access-token expiration time

This endpoint is optional. The callback endpoint may also directly render success content.

## Browser Page Responses

The temporary listener should return small human-readable HTML responses for all terminal browser-visible outcomes.

Required pages or responses:

- success: a friendly confirmation page that shows completion status, granted scopes, and access-token expiration time, while never showing secrets
- user denied consent: "Authorization was denied. You may close this window."
- invalid or expired state: "This authorization session is invalid or expired. You may close this window."
- token exchange failure: "Authorization completed but token exchange failed. You may close this window."
- timeout or canceled flow: "This authorization session is no longer active. You may close this window."

These responses should be intentionally simple and should not display secrets, authorization codes, or raw server exceptions.

## UX Design for Browser Pages

The temporary callback pages should be generated inline at runtime using simple HTML strings produced by C# code. No separate static assets are required for V1.

UX goals:

- make it immediately obvious whether authorization succeeded or failed
- provide just enough information for the user to trust the result
- avoid exposing secrets or overly technical details
- use a clean layout that works well in a small browser window

### Success page content

The success page should show:

- a clear success title
- a short confirmation message
- granted scopes returned by Strava
- access-token expiration time
- a note that the window can be closed

The success page must not show:

- access token
- refresh token
- authorization code
- client secret

Suggested wireframe:

```text
+------------------------------------------------------+
|  [Success Icon]  Strava authorization completed      |
|                                                      |
|  Your Strava connection was established successfully.|
|                                                      |
|  Granted scopes                                      |
|  - activity:read_all                                 |
|                                                      |
|  Access token expires                                |
|  2026-06-15 20:45:00 UTC                             |
|                                                      |
|  You may now close this window and return to the app.|
+------------------------------------------------------+
```

### Failure page content

Failure pages should share a common visual shape but vary the main message depending on the failure mode.

Failure variants:

- authorization denied by user
- invalid or expired state
- token exchange failure
- timed out or canceled flow

Failure pages should show:

- a clear failure title
- a short explanation in plain language
- optional non-sensitive next-step guidance
- a note that the window can be closed

Failure pages must not show:

- raw exception stack traces
- access token
- refresh token
- authorization code
- client secret

Suggested wireframe:

```text
+------------------------------------------------------+
|  [Error Icon]  Strava authorization failed           |
|                                                      |
|  The authorization could not be completed.           |
|                                                      |
|  Reason                                               |
|  The authorization session expired or was canceled.  |
|                                                      |
|  Next step                                            |
|  Return to the application and try again.            |
|                                                      |
|  You may now close this window.                      |
+------------------------------------------------------+
```

### Visual style guidance

- use inline CSS only
- use a centered card layout with readable spacing
- use a distinct success and failure accent color
- keep typography simple and system-font based
- ensure the page remains readable without external assets or scripts
- avoid any dependency on images, icons, or fonts that require network access

### Data formatting guidance

- render scopes as a short bullet list or comma-separated list
- render expiration time in UTC with a clear label
- if scope or expiration data is unavailable, omit that section rather than displaying placeholder secrets or raw JSON

## In-Memory Notification Mechanism

The essential requirement is that the user-facing API feels like an async wait. The cleanest mechanism is:

- `TaskCompletionSource<StravaOAuthTokenResult>`

Why this works well:

- It directly models one awaited async result
- The callback handler can complete it from another execution path
- It integrates naturally with cancellation, timeout, and exception propagation

Recommended configuration:

- Use `TaskCreationOptions.RunContinuationsAsynchronously`

This avoids inline continuation execution inside the HTTP request pipeline.

Example internal shape:

```csharp
internal sealed class PendingAuthorizationFlow
{
	public required string State { get; init; }
	public required TaskCompletionSource<StravaOAuthTokenResult> Completion { get; init; }
	public required DateTimeOffset CreatedAtUtc { get; init; }
}
```

## State and Security Requirements

The design must include the following protections.

### State validation

- Generate a cryptographically random `state`
- Include it in the authorize URL
- Require exact match on callback
- Reject callbacks with missing or unknown state

### Loopback binding

- Bind only to localhost or `127.0.0.1`
- Do not expose the temporary callback server publicly

### Timeout

- Default timeout of 5 minutes
- If timeout expires before callback completion, cancel the pending task and shut down the host

### Single-use flow

- A state value can be used exactly once
- Remove pending flow from memory immediately after completion

## Flow State Machine

Each authorization flow should follow this state model:

- `Pending`
- `Succeeded`
- `Failed`
- `Canceled`
- `TimedOut`

Rules:

- Every flow begins in `Pending`.
- Only one terminal state may be observed externally.
- Callback success transitions `Pending -> Succeeded`.
- Callback error or token exchange error transitions `Pending -> Failed`.
- Caller cancellation transitions `Pending -> Canceled`.
- Timeout transitions `Pending -> TimedOut`.
- Any later signal after a terminal state must be ignored except for cleanup.

### Error propagation

- If Strava returns `error=access_denied` or similar, surface a meaningful exception to the caller
- If code exchange fails, propagate the HTTP error details in a controlled way

## Security Design

This library handles short-lived authorization codes, access tokens, and long-lived refresh tokens. The implementation must treat these values as secrets and keep the temporary callback surface as narrow as possible.

### Security goals

- prevent unauthorized completion of a pending OAuth flow
- prevent accidental disclosure of authorization codes or tokens
- minimize the exposure window of the temporary listener
- ensure the callback endpoint is usable only for the intended in-progress authorization flow

### Loopback listener restrictions

- Bind only to `127.0.0.1` or `localhost`.
- Do not bind to `0.0.0.0`, machine host names, or externally reachable interfaces.
- Keep the listener alive only for the duration of an active authorization flow.
- Expose only the callback endpoint and the optional success endpoint.

### State parameter requirements

The `state` parameter is mandatory and is the primary defense against unsolicited or cross-process callback completion.

Requirements:

- generate `state` using a cryptographically secure random value
- associate the generated `state` with exactly one pending flow in memory
- require an exact match on callback before exchanging the authorization code
- reject missing, unknown, expired, or already-consumed `state` values
- remove the stored pending-flow state immediately after a terminal result

### Authorization code handling

- treat the authorization `code` as a secret
- never log the authorization `code`
- never persist the authorization `code`
- exchange the authorization `code` immediately and only once

### Token handling

- treat both `access_token` and `refresh_token` as secrets
- never log tokens
- never return tokens in browser-visible HTML responses
- prefer secure persistence outside this library, using OS-backed or otherwise protected storage where available
- when refresh returns a new refresh token, persist the new value atomically with the new access token and expiry information

### Browser-visible response rules

- success and failure pages must be simple and human-readable
- do not include raw exceptions, tokens, authorization codes, or client secrets in the browser response
- do not include diagnostic details that would help another local process replay or interfere with the flow

### Logging rules

Allowed logging examples:

- listener started
- listener stopped
- callback received for known or unknown state
- token exchange succeeded or failed

Disallowed logging examples:

- client secret
- authorization code
- access token
- refresh token
- raw token response bodies containing secrets

### Scope minimization

- request only the scopes the caller actually needs
- preserve granted scopes from Strava so the application can validate what was actually approved
- do not assume requested scopes were granted unchanged

### Local machine threat considerations

Because the callback listener runs on loopback, another local process on the same machine could attempt to send requests to it.

Mitigations:

- use unpredictable `state` values
- keep the listener short-lived
- allow each `state` value to complete only one pending flow
- stop the listener immediately after a terminal outcome

### Security posture summary

The minimum mandatory security measures for V1 are:

- loopback-only binding
- cryptographically strong single-use `state`
- no logging of codes or tokens
- minimal browser responses
- short listener lifetime with prompt cleanup
- secure handling and persistence of refresh tokens outside the interactive auth layer

## Proposed Internal Flow Pseudocode

```csharp
public async Task<StravaOAuthTokenResult> AcquireTokenAsync(...)
{
	var state = StateGenerator.Create();
	using var timeoutCts = new CancellationTokenSource(clientOptions.DefaultTimeout);
	using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

	// Register the pending flow before opening the browser so the callback can always resolve state.
	var pendingFlow = coordinator.CreatePendingFlow(state);
	await using var listener = await listenerHost.StartAsync(..., linkedCts.Token);

	var authorizeUrl = authorizationUrlBuilder.Build(clientOptions, listener.CallbackUri, state);

	if (clientOptions.AutoOpenBrowser)
	{
		await browserLauncher.OpenAsync(authorizeUrl, linkedCts.Token);
	}

	using var registration = linkedCts.Token.Register(() =>
		coordinator.Cancel(state));

	try
	{
		return await pendingFlow.Completion.Task.WaitAsync(linkedCts.Token);
	}
	finally
	{
		// Terminal cleanup must always remove the flow entry, regardless of success, error, timeout, or cancellation.
		coordinator.Remove(state);
	}
}
```

Callback pseudocode:

```csharp
app.MapGet("/auth/strava/callback", async context =>
{
	var state = context.Request.Query["state"];
	var code = context.Request.Query["code"];
	var error = context.Request.Query["error"];

	if (!coordinator.TryGetPendingFlow(state, out var flow))
	{
		context.Response.StatusCode = 400;
		await context.Response.WriteAsync("Unknown or expired OAuth state.");
		return;
	}

	if (!StringValues.IsNullOrEmpty(error))
	{
		coordinator.CompleteFailure(state, new StravaOAuthException(...));
		await context.Response.WriteAsync("Authorization failed. You may close this window.");
		return;
	}

	try
	{
		var token = await tokenExchangeClient.ExchangeAuthorizationCodeAsync(...);
		coordinator.CompleteSuccess(state, token);
		context.Response.Redirect("/auth/strava/success");
	}
	catch (Exception ex)
	{
		coordinator.CompleteFailure(state, ex);
		context.Response.StatusCode = 500;
		await context.Response.WriteAsync("Token exchange failed. You may close this window.");
	}
});
```

## Strava Authorization URL Requirements

The authorize URL builder should include at minimum:

- `client_id`
- `redirect_uri`
- `response_type=code`
- `approval_prompt=auto` or configurable value
- `scope` with requested Strava scopes
- `state`

Example conceptual URL:

- `https://www.strava.com/oauth/authorize?client_id=...&redirect_uri=http://localhost:54123/auth/strava/callback&response_type=code&approval_prompt=auto&scope=activity:read_all&state=...`

## Provider Compatibility Constraints

The implementation must follow Strava's documented authorization flow rather than assumptions from Microsoft Entra or generic OAuth samples.

Confirmed from Strava's authentication documentation:

- Strava redirects the user agent to the supplied `redirect_uri` after authorization.
- The successful callback includes `code` and `scope` in the query string.
- The application must exchange that one-time authorization `code` by calling `POST https://www.strava.com/oauth/token`.
- Strava's documented code exchange requires `client_id` and `client_secret`.
- Strava documents scope handling explicitly and notes that the granted scopes may differ from the requested scopes.

Design implications:

- The library must preserve the granted scope information returned by Strava.
- The callback listener must know its final bound URI before the authorization URL is generated.
- Strava's published documentation states that the `redirect_uri` must be within the callback domain specified by the application and that `localhost` and `127.0.0.1` are white-listed.
- Based on that documented behavior, loopback callback URIs using `localhost` or `127.0.0.1` are supported and dynamic loopback port allocation is a valid default design choice.
- The library should default to dynamic loopback port allocation, but fixed-port override is required for compatibility if app registration rules demand it.

Implementation note:

- The documentation supports loopback redirect URIs by host, not only by publicly routable domains.
- Even with that documented guidance, the implementation should retain fixed-port override in case an application's callback-domain registration or operational environment imposes narrower constraints in practice.

## Token Exchange Requirements

The code exchange should POST to Strava's token endpoint with:

- `client_id`
- `client_secret`
- `code`
- `grant_type=authorization_code`

The response should be mapped into `StravaOAuthTokenResult`.

The response may also include:

- athlete information
- granted scope information depending on Strava response shape

The initial design should preserve the full response shape where useful, including:

- the raw response JSON
- athlete payload as JSON
- unmapped response fields captured as extension data

Important:

- The token exchange implementation must follow Strava's documentation for the token endpoint specifically.
- Do not infer token-endpoint authentication behavior from Strava revoke or deauthorize endpoint examples.
- If Strava updates the token endpoint to require a different authentication style, that endpoint-specific guidance takes precedence.

## Error Model

Recommended custom exception types:

- `StravaOAuthException`
- `StravaOAuthTimeoutException`
- `StravaOAuthStateMismatchException`
- `StravaOAuthBrowserLaunchException`
- `StravaOAuthTokenExchangeException`

The public client should normalize low-level failures into these where practical.

## Lifetime and Resource Management

The temporary listener host should:

- Start only when `AcquireTokenAsync` is invoked
- Stay alive only for the duration of one auth flow
- Shut down on success, failure, cancellation, or timeout

The coordinator entry should:

- Be created before browser launch
- Be removed after any terminal state

The browser is not managed beyond launch.

## Concurrency Model

Version 1 should support concurrent `AcquireTokenAsync` calls by keying each pending flow by unique `state`.

Recommended V1 approach:

- Allow multiple concurrent flows by unique state
- Keep the in-memory coordinator responsible for isolating concurrent pending flows

## Configuration

Suggested options object:

```csharp
public sealed record StravaOAuthClientOptions
{
	public required string ClientId { get; init; }
	public required string ClientSecret { get; init; }
	public string AuthorizationBaseUrl { get; init; } = "https://www.strava.com/oauth";
	public string ApiBaseUrl { get; init; } = "https://www.strava.com/api/v3";
	public string CallbackHost { get; init; } = "localhost";
	public int? CallbackPort { get; init; }
	public string CallbackPath { get; init; } = "/auth/strava/callback";
	public string SuccessPath { get; init; } = "/auth/strava/success";
	public IReadOnlyList<string> DefaultScopes { get; init; } = ["activity:read_all"];
	public bool AutoOpenBrowser { get; init; } = true;
	public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
```

Port behavior should be:

- default to `CallbackPort = null`, which means bind to port `0` and let the OS choose an available loopback port
- capture the actual bound port after listener startup
- build the final redirect URI from that bound port
- support explicit fixed-port override only when required by the caller or deployment constraints

## Integration with Existing `RookRun.Strava`

This new OAuth library can coexist with the existing `IStravaActivities` client.

Suggested integration path:

1. Build the interactive OAuth flow inside the existing `RookRun.Strava` project under a dedicated namespace and folder, for example:
   - `RookRun.Strava.Auth`
2. Use it only for obtaining the initial token set
3. Store the resulting refresh token in configuration or a secure store
4. Continue using the existing `StravaActivities` class for headless activity access

This keeps concerns separated:

- interactive setup flow for initial authorization
- non-interactive API client for normal day-to-day use

## Recommended Namespace and Folder Layout

Recommended project:

- existing project: `RookRun.Strava`

Recommended root namespace:

- auth sub-namespace: `RookRun.Strava.Auth`

Suggested folders:

- `Abstractions`
  - `IStravaOAuthClient.cs`
  - `IStravaAuthorizationLauncher.cs`
  - `IStravaTokenExchangeClient.cs`
  - `IStravaOAuthListenerHost.cs`
- `Models`
  - `StravaOAuthTokenResult.cs`
  - `StravaAuthorizationCodeExchangeRequest.cs`
- `Hosting`
  - `StravaOAuthListenerHost.cs`
  - `StravaOAuthListenerSession.cs`
- `Coordination`
  - `StravaOAuthCallbackCoordinator.cs`
  - `PendingAuthorizationFlow.cs`
- `Browser`
  - `DefaultStravaAuthorizationLauncher.cs`
- `Http`
  - `StravaTokenExchangeClient.cs`
  - `StravaAuthorizationUrlBuilder.cs`
- `Exceptions`
  - `StravaOAuthException.cs`
  - related exception types
- `Options`
  - `StravaOAuthClientOptions.cs`
- `DependencyInjection`
  - `ServiceCollectionExtensions.cs`

Recommended public namespace shape:

- `RookRun.Strava.Auth`
- `RookRun.Strava.Auth.Models`
- `RookRun.Strava.Auth.Options`
- `RookRun.Strava.Auth.Exceptions`

Recommended adjacent layering:

- `RookRun.Strava.Auth` for interactive token acquisition inside the existing `RookRun.Strava` project
- `RookRun.Strava.Tokens` as a logical layer or future namespace for persistence, caching, and refresh behavior if that concern grows later
- `RookRun.Strava` for API clients such as activities access

This separation keeps the browser/listener flow isolated from token lifecycle management and from the Strava API surface itself.

## Recommended Project Layout

Keep the auth implementation inside the existing `RookRun.Strava` project.

Suggested folder layout:

- `RookRun.Strava/Auth/Abstractions`
  - `IStravaOAuthClient.cs`
  - `IStravaAuthorizationLauncher.cs`
  - `IStravaTokenExchangeClient.cs`
  - `IStravaOAuthListenerHost.cs`
- `RookRun.Strava/Auth/Models`
  - `StravaOAuthTokenResult.cs`
  - `StravaAuthorizationCodeExchangeRequest.cs`
- `RookRun.Strava/Auth/Hosting`
  - `StravaOAuthListenerHost.cs`
  - `StravaOAuthListenerSession.cs`
- `RookRun.Strava/Auth/Coordination`
  - `StravaOAuthCallbackCoordinator.cs`
  - `PendingAuthorizationFlow.cs`
- `RookRun.Strava/Auth/Browser`
  - `DefaultStravaAuthorizationLauncher.cs`
- `RookRun.Strava/Auth/Http`
  - `StravaTokenExchangeClient.cs`
  - `StravaAuthorizationUrlBuilder.cs`
- `RookRun.Strava/Auth/Exceptions`
  - `StravaOAuthException.cs`
  - related exception types
- `RookRun.Strava/Auth/Options`
  - `StravaOAuthClientOptions.cs`
- `RookRun.Strava/Auth/DependencyInjection`
  - `ServiceCollectionExtensions.cs`

This keeps the auth-specific code isolated by namespace and folder while avoiding the overhead of another project.

## Observability

The library should log:

- listener start and stop
- callback URL in use
- browser launch attempt
- callback receipt
- state validation failure
- token exchange success or failure
- timeout and cancellation

Logs must not include:

- client secret
- access token
- refresh token
- raw authorization code

## Testing and Testability

This design is not trivial to test end-to-end as a pure unit because it coordinates browser launch, temporary HTTP hosting, callback handling, and asynchronous completion. To keep it testable, orchestration concerns must remain separated behind small abstractions.

Testability goals:

- keep browser launch abstracted behind `IStravaAuthorizationLauncher`
- keep listener hosting abstracted behind `IStravaOAuthListenerHost`
- keep token exchange abstracted behind `IStravaTokenExchangeClient`
- keep the state-tracking coordinator independently testable without a live web server
- keep URL construction and response parsing in small deterministic components

Design rule:

- avoid collapsing orchestration, browser launch, listener hosting, callback handling, and token exchange into one large class, because that would make race handling and failure paths much harder to test reliably

Recommended test split:

- unit tests for deterministic logic and orchestration decisions
- integration tests for the temporary minimal API listener and callback flow
- manual verification for real browser and Strava authorization behavior

Unit tests for this design should be added to the existing `RookRun.UnitTest` project.

### Unit tests

- authorize URL includes expected parameters
- state generation and validation
- callback success completes the awaiting task
- callback error completes task with exception
- timeout cancels the awaiting task
- unknown state returns failure

### Integration tests

- start local minimal API host
- simulate callback request with state and code
- stub token exchange client
- verify async caller receives expected token result

### Manual test

- start auth flow locally
- browser opens
- login to Strava
- redirect returns to localhost
- success page shown
- console caller receives token result

## Implementation Readiness Checklist

The design is ready to implement only when all of the following are satisfied:

- the token endpoint request format has been confirmed against current Strava documentation
- the listener session contract includes actual callback URI and disposal semantics
- the coordinator terminal-state and cleanup rules are implemented with race-safe completion methods
- browser-visible terminal responses are defined for success and all failure paths
- the listener host remains self-contained and minimal
- the token result preserves granted scopes and raw response data

## Open Questions

No major open design questions remain for V1.

## Recommendation

Proceed with a Strava-specific library first.

Recommended V1 design:

- `IStravaOAuthClient.AcquireTokenAsync(...)`
- temporary localhost minimal API host
- dynamic loopback port allocation by default
- browser launch via shell execution
- callback-to-caller coordination via `TaskCompletionSource<StravaOAuthTokenResult>`
- code exchange through a dedicated HTTP client component
- clear shutdown and cleanup on all terminal paths

This design directly matches the required user experience: the caller simply awaits a token result while the library handles the listener, browser redirect, callback processing, and in-memory notification behind the scenes.

## Configuration Boundary Recommendation

The abstraction should distinguish clearly between configuration and request-time input.

Configuration owned by the client instance:

- `ClientId`
- `ClientSecret`
- `AuthorizationBaseUrl`
- `ApiBaseUrl`
- `CallbackHost`
- `CallbackPort`
- `CallbackPath`
- `SuccessPath`
- default scopes
- default timeout
- default browser-launch behavior

This keeps the public API from leaking transport and registration details that the caller should not have to supply each time a token is requested.
