using System.Net;

namespace RookRun.Strava.Client;

/// <summary>
/// Represents a Strava API rate-limit response (HTTP 429).
/// </summary>
public sealed class StravaRateLimitException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaRateLimitException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by Strava.</param>
    /// <param name="responseBody">The response body returned by Strava, if any.</param>
    /// <param name="headers">Response headers captured from the Strava response.</param>
    /// <param name="retryAfter">Optional retry-after value if Strava supplied one.</param>
    public StravaRateLimitException(
        HttpStatusCode statusCode,
        string? responseBody,
        IReadOnlyDictionary<string, string[]> headers,
        TimeSpan? retryAfter = null)
        : base(BuildMessage(statusCode, responseBody), null, statusCode)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        RetryAfter = retryAfter;
    }

    /// <summary>
    /// Gets the HTTP status code returned by Strava.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Gets the response body returned by Strava, if any.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Gets the response headers captured from the Strava response.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; }

    /// <summary>
    /// Gets the retry-after value supplied by Strava, if any.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string? responseBody)
    {
        var message = $"Strava rate limit exceeded ({(int)statusCode} {statusCode}).";
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            message += $" Body: {responseBody}";
        }

        return message;
    }
}