using Microsoft.Extensions.Options;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RookRun.Strava.Client;

/// <summary>
/// Provides access to authenticated Strava activity streams.
/// </summary>
public sealed class StravaActivityStreamsClient : IStravaActivityStreamsClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StravaClientOptions _options;
    private readonly IStravaAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivityStreamsClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to create Strava API clients.</param>
    /// <param name="options">The configured Strava client options.</param>
    /// <param name="accessTokenProvider">The Strava access token provider used to supply bearer tokens.</param>
    public StravaActivityStreamsClient(
        IHttpClientFactory httpClientFactory,
        IOptions<StravaClientOptions> options,
        IStravaAccessTokenProvider accessTokenProvider)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public async Task<StravaActivityStreams?> GetActivityStreamsAsync(
        long activityId,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (activityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activityId), "Activity ID must be greater than zero.");
        }

        var normalizedKeys = NormalizeKeys(keys);

        var httpClient = _httpClientFactory.CreateClient("StravaActivities");
        var uri = BuildActivityStreamsUri(activityId, normalizedKeys);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _accessTokenProvider.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        var content = response.Content is null
            ? "{}"
            : await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(content);
        var streams = ParseStreams(document.RootElement);

        return new StravaActivityStreams
        {
            ActivityId = activityId,
            FetchedUtc = DateTimeOffset.UtcNow,
            RequestedKeys = normalizedKeys,
            Streams = streams
        };
    }

    /// <summary>
    /// Normalizes and validates stream keys.
    /// </summary>
    private static IReadOnlyList<string> NormalizeKeys(IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var normalized = keys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one stream key is required.", nameof(keys));
        }

        return normalized;
    }

    /// <summary>
    /// Parses streams from a canonical key-by-type object payload.
    /// Supports an array fallback for compatibility.
    /// </summary>
    private static Dictionary<string, StravaStreamData> ParseStreams(JsonElement root)
    {
        var streams = new Dictionary<string, StravaStreamData>(StringComparer.Ordinal);

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                var stream = TryDeserializeStream(property.Value);
                if (stream is null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(stream.Type))
                {
                    stream = stream with { Type = property.Name };
                }

                streams[property.Name] = stream;
            }

            return streams;
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var stream = TryDeserializeStream(item);
                if (stream is null || string.IsNullOrWhiteSpace(stream.Type))
                {
                    continue;
                }

                streams[stream.Type] = stream;
            }
        }

        return streams;
    }

    /// <summary>
    /// Attempts to deserialize one stream object.
    /// </summary>
    private static StravaStreamData? TryDeserializeStream(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StravaStreamData>(element.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Builds the relative URI for activity streams.
    /// </summary>
    private static string BuildActivityStreamsUri(long activityId, IReadOnlyList<string> keys)
    {
        var keyCsv = string.Join(",", keys);
        var encodedKeys = Uri.EscapeDataString(keyCsv);

        return $"activities/{activityId.ToString(CultureInfo.InvariantCulture)}/streams?keys={encodedKeys}&key_by_type=true";
    }

    private static void ValidateOptions(StravaClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiBaseUrl);

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("ApiBaseUrl must be an absolute URI.", nameof(options));
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            await ThrowRateLimitExceptionAsync(response, cancellationToken);
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        throw new HttpRequestException(
            $"Strava request failed with status code {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}",
            null,
            response.StatusCode);
    }

    private static async Task ThrowRateLimitExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        var headers = response.Headers
            .ToDictionary(
                static header => header.Key,
                static header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var retryAfter = response.Headers.RetryAfter?.Delta;
        throw new RateLimitException(response.StatusCode, body, headers, retryAfter, sourceSystem: "Strava API");
    }
}
