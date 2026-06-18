using System.Net;

namespace RookRun.Common.Exceptions;

/// <summary>
/// Represents an HTTP rate-limit response (typically HTTP 429).
/// Can be reused across external clients that enforce request quotas.
/// </summary>
public sealed class RateLimitException : HttpRequestException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the upstream service.</param>
    /// <param name="responseBody">The response body returned by the upstream service, if any.</param>
    /// <param name="headers">Response headers captured from the upstream response.</param>
    /// <param name="retryAfter">Optional retry-after value if supplied by the upstream service.</param>
    /// <param name="sourceSystem">Optional source system label (for example, "Strava API").</param>
    public RateLimitException(
        HttpStatusCode statusCode,
        string? responseBody,
        IReadOnlyDictionary<string, string[]> headers,
        TimeSpan? retryAfter = null,
        string? sourceSystem = null)
        : base(BuildMessage(statusCode, responseBody, sourceSystem), null, statusCode)
    {
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        ResponseBody = responseBody;
        RetryAfter = retryAfter;
        SourceSystem = sourceSystem;
    }

    /// <summary>
    /// Gets the response body returned by the upstream service, if any.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Gets the response headers captured from the upstream response.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Headers { get; }

    /// <summary>
    /// Gets the retry-after value supplied by the upstream service, if any.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Gets the optional source system label for this rate-limit response.
    /// </summary>
    public string? SourceSystem { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string? responseBody, string? sourceSystem)
    {
        var source = string.IsNullOrWhiteSpace(sourceSystem) ? "Upstream service" : sourceSystem;
        var message = $"{source} rate limit exceeded ({(int)statusCode} {statusCode}).";

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            message += $" Body: {responseBody}";
        }

        return message;
    }
}
