using Microsoft.Extensions.Options;
using RookRun.Strava.Auth;
using RookRun.Strava.Models;
using RookRun.Strava.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RookRun.Strava;

/// <summary>
/// Provides access to the authenticated Strava activities API.
/// </summary>
public sealed class StravaActivities : IStravaActivities
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StravaOptions _options;
    private readonly IStravaAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivities"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to create Strava API clients.</param>
    /// <param name="options">The configured Strava client options.</param>
    /// <param name="accessTokenProvider">The Strava access token provider used to supply bearer tokens.</param>
    public StravaActivities(IHttpClientFactory httpClientFactory, IOptions<StravaOptions> options, IStravaAccessTokenProvider accessTokenProvider)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
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
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _accessTokenProvider.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var activities = await response.Content.ReadFromJsonAsync<List<StravaActivity>>(cancellationToken: cancellationToken);
        return activities ?? [];
    }

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