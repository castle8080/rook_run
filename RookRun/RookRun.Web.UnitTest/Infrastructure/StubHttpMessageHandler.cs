using System.Net;
using System.Net.Http.Json;

namespace RookRun.Web.UnitTest.Infrastructure;

/// <summary>
/// Provides deterministic HTTP responses for component tests.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> requests = [];
    private readonly List<Route> routes = [];

    /// <summary>
    /// Gets the total number of HTTP requests observed by this handler.
    /// </summary>
    public int RequestCount => this.requests.Count;

    /// <summary>
    /// Registers a JSON response for an exact HTTP method and path/query match.
    /// </summary>
    /// <typeparam name="TPayload">The JSON payload type.</typeparam>
    /// <param name="method">The expected HTTP method.</param>
    /// <param name="pathAndQuery">The expected relative path and query, such as "api/jobs".</param>
    /// <param name="payload">The response payload to serialize.</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    public void AddJsonResponse<TPayload>(
        HttpMethod method,
        string pathAndQuery,
        TPayload payload,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        this.routes.Add(new Route(
            request => MatchesMethodAndPath(request, method, pathAndQuery),
            () => new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(payload)
            }));
    }

    /// <summary>
    /// Registers an arbitrary response factory for matching requests.
    /// </summary>
    /// <param name="matcher">Predicate used to match an incoming request.</param>
    /// <param name="responseFactory">Factory that produces a response for a matched request.</param>
    public void AddResponse(Func<HttpRequestMessage, bool> matcher, Func<HttpResponseMessage> responseFactory)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(responseFactory);
        this.routes.Add(new Route(matcher, responseFactory));
    }

    /// <summary>
    /// Sends a configured response when a route matches, otherwise returns 404.
    /// </summary>
    /// <param name="request">The outgoing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task containing the response message.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        this.requests.Add(request);

        var route = this.routes.FirstOrDefault(candidate => candidate.Matcher(request));
        if (route is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request
            });
        }

        var response = route.ResponseFactory();
        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    /// <summary>
    /// Compares the request against an expected method and path/query.
    /// </summary>
    /// <param name="request">The request to compare.</param>
    /// <param name="method">The expected HTTP method.</param>
    /// <param name="expectedPathAndQuery">The expected path and query value.</param>
    /// <returns><c>true</c> when method and path/query both match; otherwise <c>false</c>.</returns>
    private static bool MatchesMethodAndPath(HttpRequestMessage request, HttpMethod method, string expectedPathAndQuery)
    {
        var normalizedExpected = NormalizePathAndQuery(expectedPathAndQuery);
        var normalizedActual = request.RequestUri is null ? string.Empty : NormalizePathAndQuery(request.RequestUri.PathAndQuery);

        return request.Method == method &&
               string.Equals(normalizedActual, normalizedExpected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a path/query string for stable matching.
    /// </summary>
    /// <param name="pathAndQuery">The path and query string to normalize.</param>
    /// <returns>The normalized path/query with a leading slash.</returns>
    private static string NormalizePathAndQuery(string pathAndQuery)
    {
        if (string.IsNullOrWhiteSpace(pathAndQuery))
        {
            return "/";
        }

        return pathAndQuery.StartsWith("/", StringComparison.Ordinal)
            ? pathAndQuery
            : $"/{pathAndQuery}";
    }

    /// <summary>
    /// Stores route matching and response creation behavior.
    /// </summary>
    /// <param name="Matcher">The request match predicate.</param>
    /// <param name="ResponseFactory">The response factory.</param>
    private sealed record Route(Func<HttpRequestMessage, bool> Matcher, Func<HttpResponseMessage> ResponseFactory);
}