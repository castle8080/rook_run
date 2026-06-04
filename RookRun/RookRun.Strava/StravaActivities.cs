using Microsoft.Extensions.Options;
using RookRun.Strava.Models;
using RookRun.Strava.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RookRun.Strava;

public sealed class StravaActivities : IStravaActivities, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StravaOptions _options;
    private readonly SemaphoreSlim _accessTokenLock = new(1, 1);

    private string _refreshToken;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;

    public StravaActivities(IHttpClientFactory httpClientFactory, IOptions<StravaOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _refreshToken = _options.RefreshToken;
    }

    public Task<IReadOnlyList<StravaActivity>> ListActivitiesAsync(CancellationToken cancellationToken = default) =>
        SearchActivitiesAsync(new StravaActivityQuery(), cancellationToken);

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

    public void Dispose()
    {
        _accessTokenLock.Dispose();
    }

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

            var httpClient = _httpClientFactory.CreateClient("StravaActivities");
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildTokenUri())
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["client_secret"] = _options.ClientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken
                })
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var tokenResponse = await response.Content.ReadFromJsonAsync<StravaTokenResponse>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Strava token endpoint returned an empty response.");

            _accessToken = tokenResponse.AccessToken;
            _refreshToken = tokenResponse.RefreshToken;
            _accessTokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(tokenResponse.ExpiresAt);

            return _accessToken;
        }
        finally
        {
            _accessTokenLock.Release();
        }
    }

    private bool HasValidAccessToken() =>
        !string.IsNullOrWhiteSpace(_accessToken) && _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);

    private Uri BuildTokenUri() => new(new Uri($"{_options.AuthorizationBaseUrl.TrimEnd('/')}/"), "token");

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

    private sealed record StravaTokenResponse
    {
        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; init; }
    }
}