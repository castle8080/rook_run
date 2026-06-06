using Microsoft.Extensions.Options;
using RookRun.Strava.Auth.Coordination;
using RookRun.Strava.Auth.Exceptions;
using RookRun.Strava.Auth.Http;
using RookRun.Strava.Auth.Models;
using System.Text.Json;

namespace RookRun.Strava.Auth;

/// <summary>
/// Coordinates the interactive Strava OAuth authorization flow for callers.
/// </summary>
public sealed class StravaOAuthClient : IStravaOAuthClient
{
    private readonly IStravaOAuthListenerHost _listenerHost;
    private readonly IStravaAuthorizationLauncher _authorizationLauncher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StravaOAuthClientOptions _options;
    private readonly StravaAuthorizationUrlBuilder _authorizationUrlBuilder = new();
    private readonly StravaOAuthCallbackCoordinator _coordinator = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthClient"/> class.
    /// </summary>
    /// <param name="listenerHost">The temporary loopback listener host.</param>
    /// <param name="authorizationLauncher">The browser launcher used to open Strava authorization pages.</param>
    /// <param name="options">The configured OAuth client options.</param>
    public StravaOAuthClient(
        IStravaOAuthListenerHost listenerHost,
        IStravaAuthorizationLauncher authorizationLauncher,
        IHttpClientFactory httpClientFactory,
        IOptions<StravaOAuthClientOptions> options)
    {
        _listenerHost = listenerHost ?? throw new ArgumentNullException(nameof(listenerHost));
        _authorizationLauncher = authorizationLauncher ?? throw new ArgumentNullException(nameof(authorizationLauncher));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<StravaOAuthTokenResult> AcquireTokenAsync(CancellationToken cancellationToken = default)
    {
        ValidateOptions(_options);

        var state = _authorizationUrlBuilder.CreateState();
        var pendingFlow = _coordinator.CreatePendingFlow(state);
        using var timeoutCts = new CancellationTokenSource(_options.DefaultTimeout);
        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await using var listener = await _listenerHost.StartAsync(
                new StravaOAuthListenerOptions
                {
                    CallbackHost = _options.CallbackHost,
                    CallbackPort = _options.CallbackPort,
                    CallbackPath = NormalizePath(_options.CallbackPath),
                    SuccessPath = NormalizePath(_options.SuccessPath)
                },
                _coordinator,
                startupCts.Token);

            using var cancellationRegistration = cancellationToken.Register(static stateObject =>
            {
                var tuple = ((StravaOAuthCallbackCoordinator Coordinator, string State, CancellationToken Token))stateObject!;
                tuple.Coordinator.Cancel(tuple.State, tuple.Token);
            }, (_coordinator, state, cancellationToken));

            using var timeoutRegistration = timeoutCts.Token.Register(static stateObject =>
            {
                var tuple = ((StravaOAuthCallbackCoordinator Coordinator, string State))stateObject!;
                tuple.Coordinator.Timeout(tuple.State);
            }, (_coordinator, state));

            var authorizeUri = _authorizationUrlBuilder.BuildAuthorizeUri(_options, listener.CallbackUri, state);
            if (_options.AutoOpenBrowser)
            {
                await _authorizationLauncher.OpenAsync(authorizeUri, cancellationToken);
            }

            var authorizationCode = await pendingFlow.Completion.Task.ConfigureAwait(false);

            return await ExchangeAuthorizationCodeAsync(
                new StravaAuthorizationCodeExchangeRequest
                {
                    ClientId = _options.ClientId,
                    ClientSecret = _options.ClientSecret,
                    Code = authorizationCode,
                    RedirectUri = listener.CallbackUri
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new StravaOAuthTimeoutException("The Strava authorization flow timed out.");
        }
        finally
        {
            _coordinator.Remove(state);
        }
    }

    /// <inheritdoc />
    public Task<StravaOAuthTokenResult> RefreshAccessTokenAsync(string? refreshToken = null, CancellationToken cancellationToken = default)
    {
        ValidateOptions(_options);

        var tokenToRefresh = string.IsNullOrWhiteSpace(refreshToken)
            ? _options.RefreshToken
            : refreshToken;

        if (string.IsNullOrWhiteSpace(tokenToRefresh))
        {
            throw new InvalidOperationException("A Strava refresh token is required to refresh the access token.");
        }

        return ExchangeTokenAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = tokenToRefresh,
                ["grant_type"] = "refresh_token"
            },
            cancellationToken);
    }

    /// <summary>
    /// Exchanges an authorization code for a token payload.
    /// </summary>
    /// <param name="request">The authorization code exchange request.</param>
    /// <param name="cancellationToken">Cancels the outbound token exchange request.</param>
    /// <returns>The parsed token response.</returns>
    internal Task<StravaOAuthTokenResult> ExchangeAuthorizationCodeAsync(
        StravaAuthorizationCodeExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ExchangeTokenAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = request.ClientId,
                ["client_secret"] = request.ClientSecret,
                ["code"] = request.Code,
                ["grant_type"] = "authorization_code"
            },
            cancellationToken);
    }

    /// <summary>
    /// Validates the configured OAuth options before a flow starts.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    private static void ValidateOptions(StravaOAuthClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CallbackHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CallbackPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SuccessPath);

        if (options.DefaultTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "DefaultTimeout must be greater than zero.");
        }
    }

    /// <summary>
    /// Ensures a listener route begins with a leading slash.
    /// </summary>
    /// <param name="path">The route to normalize.</param>
    /// <returns>The normalized route.</returns>
    private static string NormalizePath(string path) => path.StartsWith('/') ? path : $"/{path}";

    /// <summary>
    /// Sends a Strava token exchange request and maps the response into a token result.
    /// </summary>
    /// <param name="formValues">The form values to send to the token endpoint.</param>
    /// <param name="cancellationToken">Cancels the outbound HTTP request.</param>
    /// <returns>The mapped token result.</returns>
    private async Task<StravaOAuthTokenResult> ExchangeTokenAsync(
        IReadOnlyDictionary<string, string> formValues,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("StravaOAuth");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "token")
        {
            Content = new FormUrlEncodedContent(formValues)
        };

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new StravaOAuthTokenExchangeException(
                $"Strava token exchange failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return MapResult(document, responseBody);
        }
        catch (JsonException ex)
        {
            throw new StravaOAuthTokenExchangeException("Strava token exchange returned invalid JSON.", ex);
        }
    }

    /// <summary>
    /// Maps a raw Strava token response document into the public token result model.
    /// </summary>
    /// <param name="document">The parsed JSON document.</param>
    /// <param name="rawResponseJson">The raw JSON response body.</param>
    /// <returns>The mapped token result.</returns>
    public static StravaOAuthTokenResult MapResult(JsonDocument document, string rawResponseJson)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        var grantedScopes = ParseGrantedScopes(root);
        JsonElement? athlete = root.TryGetProperty("athlete", out var athleteElement)
            ? athleteElement.Clone()
            : null;

        var additionalData = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("access_token") ||
                property.NameEquals("refresh_token") ||
                property.NameEquals("expires_at") ||
                property.NameEquals("token_type") ||
                property.NameEquals("scope") ||
                property.NameEquals("athlete"))
            {
                continue;
            }

            additionalData[property.Name] = property.Value.Clone();
        }

        return new StravaOAuthTokenResult
        {
            AccessToken = root.GetProperty("access_token").GetString() ?? string.Empty,
            RefreshToken = root.GetProperty("refresh_token").GetString() ?? string.Empty,
            ExpiresAtUnixTimeSeconds = root.GetProperty("expires_at").GetInt64(),
            TokenType = root.GetProperty("token_type").GetString() ?? string.Empty,
            RawScope = root.TryGetProperty("scope", out var scopeElement) ? scopeElement.GetString() : null,
            GrantedScopes = grantedScopes,
            Athlete = athlete,
            AdditionalData = additionalData,
            RawResponseJson = rawResponseJson
        };
    }

    /// <summary>
    /// Parses the granted scopes from a Strava token response payload.
    /// </summary>
    /// <param name="root">The response root element.</param>
    /// <returns>The parsed scope list, or an empty list when no scope data is present.</returns>
    public static IReadOnlyList<string> ParseGrantedScopes(JsonElement root)
    {
        if (!root.TryGetProperty("scope", out var scopeElement))
        {
            return [];
        }

        var rawScope = scopeElement.GetString();
        if (string.IsNullOrWhiteSpace(rawScope))
        {
            return [];
        }

        return rawScope
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
