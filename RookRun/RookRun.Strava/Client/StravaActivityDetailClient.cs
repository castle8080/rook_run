using Microsoft.Extensions.Options;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RookRun.Strava.Client;

/// <summary>
/// Provides access to the authenticated Strava activity detail API.
/// Retrieves comprehensive activity information and downloads associated images.
/// </summary>
public sealed class StravaActivityDetailClient : IStravaActivityDetailClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StravaClientOptions _options;
    private readonly IStravaAccessTokenProvider _accessTokenProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivityDetailClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory used to create Strava API clients.</param>
    /// <param name="options">The configured Strava client options.</param>
    /// <param name="accessTokenProvider">The Strava access token provider used to supply bearer tokens.</param>
    public StravaActivityDetailClient(
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
    public async Task<StravaActivityDetail?> GetActivityDetailAsync(
        long activityId,
        bool includeAllEfforts = false,
        CancellationToken cancellationToken = default)
    {
        if (activityId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activityId), "Activity ID must be greater than zero.");
        }

        var httpClient = _httpClientFactory.CreateClient("StravaActivities");
        var uri = BuildActivityDetailUri(activityId, includeAllEfforts);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _accessTokenProvider.GetAccessTokenAsync(cancellationToken));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        // Return null for 404 Not Found (activity may have been deleted)
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        var detail = await response.Content.ReadFromJsonAsync<StravaActivityDetail>(cancellationToken: cancellationToken);
        return detail;
    }

    /// <inheritdoc />
    public IReadOnlyList<StravaActivityImage> ExtractActivityImages(
        StravaActivityDetail activityDetail)
    {
        ArgumentNullException.ThrowIfNull(activityDetail);

        var images = new List<StravaActivityImage>();

        if (activityDetail.Photos == null)
        {
            return images;
        }

        try
        {
            // Parse photos object - could be a JsonElement or object
            JsonElement photosElement;
            
            if (activityDetail.Photos is JsonElement je)
            {
                photosElement = je;
            }
            else if (activityDetail.Photos != null)
            {
                photosElement = JsonSerializer.SerializeToElement(activityDetail.Photos);
            }
            else
            {
                return images;
            }

            // Extract primary photo if present
            if (photosElement.TryGetProperty("primary", out var primaryPhoto))
            {
                ExtractPhotoUrls(primaryPhoto, activityDetail.Id, images, "primary");
            }

            // Extract photo array if present
            if (photosElement.TryGetProperty("photos", out var photosArray))
            {
                if (photosArray.ValueKind == JsonValueKind.Array)
                {
                    int index = 0;
                    foreach (var photo in photosArray.EnumerateArray())
                    {
                        ExtractPhotoUrls(photo, activityDetail.Id, images, $"photo_{index}");
                        index++;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silently fail on malformed photos object
            // Return whatever images were successfully extracted
        }

        return images;
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image URL cannot be null or empty.", nameof(imageUrl));
        }

        try
        {
            // Use a named HTTP client for CDN downloads (not authenticated)
            using var httpClient = _httpClientFactory.CreateClient("StravaImages");
            using var response = await httpClient.GetAsync(imageUrl, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await ThrowRateLimitExceptionAsync(response, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (StravaRateLimitException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            // Network errors or invalid URLs
            return null;
        }
    }

    /// <summary>
    /// Extracts photo URLs from a photo JSON object and adds them to the images list.
    /// </summary>
    private static void ExtractPhotoUrls(
        JsonElement photoElement,
        long activityId,
        List<StravaActivityImage> images,
        string photoIdentifier)
    {
        // Use unique_id if present, otherwise fall back to the caller-supplied identifier
        string imageId = photoIdentifier;
        if (photoElement.TryGetProperty("unique_id", out var uniqueId) && uniqueId.ValueKind == JsonValueKind.String)
        {
            imageId = uniqueId.GetString() ?? photoIdentifier;
        }

        // Select only the largest resolution URL (highest numeric size key, e.g. "600" over "100")
        if (photoElement.TryGetProperty("urls", out var urlsElement) && urlsElement.ValueKind == JsonValueKind.Object)
        {
            string? bestUrl = null;
            string? bestSizeKey = null;
            int bestSize = -1;

            foreach (var urlProp in urlsElement.EnumerateObject())
            {
                if (urlProp.Value.ValueKind == JsonValueKind.String
                    && int.TryParse(urlProp.Name, out var size)
                    && size > bestSize)
                {
                    bestSize = size;
                    bestSizeKey = urlProp.Name;
                    bestUrl = urlProp.Value.GetString();
                }
            }

            if (!string.IsNullOrEmpty(bestUrl))
            {
                images.Add(CreateImageFromUrl(activityId, imageId, bestUrl, bestSizeKey!));
            }
        }
    }

    /// <summary>
    /// Creates a StravaActivityImage record from a URL.
    /// </summary>
    private static StravaActivityImage CreateImageFromUrl(long activityId, string imageId, string url, string sizeVariant)
    {
        var extension = ExtractExtensionFromUrl(url);
        
        return new StravaActivityImage
        {
            ActivityId = activityId,
            ImageId = imageId,
            ImageUrl = url,
            Extension = extension,
            SizeVariant = sizeVariant
        };
    }

    /// <summary>
    /// Extracts file extension from a URL path.
    /// </summary>
    private static string ExtractExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path).TrimStart('.');
            return string.IsNullOrEmpty(extension) ? "jpg" : extension;
        }
        catch
        {
            return "jpg"; // Default to jpg if we can't parse the URL
        }
    }

    private static string BuildActivityDetailUri(long activityId, bool includeAllEfforts)
    {
        var uri = $"activities/{activityId.ToString(CultureInfo.InvariantCulture)}";
        
        if (includeAllEfforts)
        {
            uri += "?include_all_efforts=true";
        }

        return uri;
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
                header => header.Key,
                header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var retryAfter = response.Headers.RetryAfter?.Delta;
        throw new StravaRateLimitException(response.StatusCode, body, headers, retryAfter);
    }
}
