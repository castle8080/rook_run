using Microsoft.Extensions.Options;
using RookRun.Strava.Auth;
using RookRun.Strava.Models;
using RookRun.Strava.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RookRun.Strava;

/// <summary>
/// Provides access to the authenticated Strava activities API and coordinates token acquisition.
/// </summary>
public sealed class StravaActivities : IStravaActivities, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StravaOptions _options;
    private readonly IStravaOAuthClient _stravaOAuthClient;
    private readonly SemaphoreSlim _accessTokenLock = new(1, 1);

    private string? _refreshToken;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivities"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to create Strava API clients.</param>
    /// <param name="options">The configured Strava client options.</param>
    /// <param name="stravaOAuthClient">The Strava OAuth client used to perform token refreshes.</param>
    public StravaActivities(IHttpClientFactory httpClientFactory, IOptions<StravaOptions> options, IStravaOAuthClient stravaOAuthClient)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _stravaOAuthClient = stravaOAuthClient ?? throw new ArgumentNullException(nameof(stravaOAuthClient));
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StravaActivity>> ListActivitiesAsync(CancellationToken cancellationToken = default) =>
        SearchActivitiesAsync(new StravaActivityQuery(), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StravaActivity>> SearchActivitiesAsync(StravaActivityQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var httpClient = _httpClientFactory.CreateClient("StravaActivities");
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildActivitiesUri(query));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var activities = await response.Content.ReadFromJsonAsync<List<StravaActivity>>(cancellationToken: cancellationToken);
        return activities ?? [];
    }

    /// <summary>
    /// Releases the synchronization resources used by the client.
    /// </summary>
    public void Dispose()
    {
        _accessTokenLock.Dispose();
    }

    /// <summary>
    /// Returns a valid access token, refreshing or acquiring it when the cached token is missing or near expiry.
    /// </summary>
    /// <param name="cancellationToken">Cancels the token retrieval or refresh operation.</param>
    /// <returns>A valid Strava access token.</returns>
    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (HasValidAccessToken())
        {
            return _accessToken!;
        }

        await _accessTokenLock.WaitAsync(cancellationToken);
        try
        {
            if (HasValidAccessToken())
            {
                return _accessToken!;
            }

            var tokenResponse = await GetFreshTokenAsync(cancellationToken);

            _accessToken = tokenResponse.AccessToken;
            _refreshToken = tokenResponse.RefreshToken;
            _accessTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAtUnixTimeSeconds);

            return _accessToken;
        }
        finally
        {
            _accessTokenLock.Release();
        }
    }

    /// <summary>
    /// Determines whether the cached access token can still be used for outgoing API requests.
    /// </summary>
    /// <returns><see langword="true"/> when the cached token is still valid; otherwise, <see langword="false"/>.</returns>
    private bool HasValidAccessToken() =>
        !string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);

    /// <summary>
    /// Validates the configured Strava options before the client starts making requests.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    private static void ValidateOptions(StravaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiBaseUrl);

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("ApiBaseUrl must be an absolute URI.", nameof(options));
        }
    }

    /// <summary>
    /// Retrieves a fresh token result, preferring refresh-token exchange and falling back to interactive authorization.
    /// </summary>
    /// <param name="cancellationToken">Cancels the token retrieval operation.</param>
    /// <returns>A fresh OAuth token result.</returns>
    private async Task<Auth.Models.StravaOAuthTokenResult> GetFreshTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _stravaOAuthClient.RefreshAccessTokenAsync(_refreshToken, cancellationToken);
        }
        catch (InvalidOperationException) when (string.IsNullOrWhiteSpace(_refreshToken))
        {
            return await _stravaOAuthClient.AcquireTokenAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Builds the activities endpoint path and query string for the supplied search request.
    /// </summary>
    /// <param name="query">The activity query options.</param>
    /// <returns>The relative activities request URI.</returns>
    private static string BuildActivitiesUri(StravaActivityQuery query)
    {
        ValidateQuery(query);

        var parameters = new List<string>();

        if (query.Before is not null)
        {
            parameters.Add($"before={query.Before.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}");
        }

        if (query.After is not null)
        {
            parameters.Add($"after={query.After.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}");
        }

        if (query.Page is not null)
        {
            parameters.Add($"page={query.Page.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (query.PerPage is not null)
        {
            parameters.Add($"per_page={query.PerPage.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return parameters.Count == 0
            ? "athlete/activities"
            : $"athlete/activities?{string.Join("&", parameters)}";
    }

    /// <summary>
    /// Validates the activity query before it is sent to Strava.
    /// </summary>
    /// <param name="query">The query to validate.</param>
    private static void ValidateQuery(StravaActivityQuery query)
    {
        if (query.Page is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be greater than zero.");
        }

        if (query.PerPage is <= 0 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "PerPage must be between 1 and 200.");
        }
    }

    /// <summary>
    /// Throws an exception when a Strava HTTP response is unsuccessful.
    /// </summary>
    /// <param name="response">The HTTP response to validate.</param>
    /// <param name="cancellationToken">Cancels reading the error response body.</param>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        throw new HttpRequestException(
            $"Strava request failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}",
            null,
            response.StatusCode);
    }

}