using Microsoft.Extensions.Options;
using Moq;
using RookRun.Strava.Client;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// HTTP behavior tests for <see cref="StravaActivitiesClient"/>.
/// </summary>
public sealed class StravaActivitiesClientHttpTests
{
    /// <summary>
    /// Verifies successful searches include auth headers and query parameters.
    /// </summary>
    [Fact]
    public async Task SearchActivitiesAsync_SendsBearerTokenAndQueryParameters()
    {
        var before = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var after = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000);
        HttpRequestMessage? capturedRequest = null;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            const string payload = "[{\"id\":123}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, "token-123");

        var results = await client.SearchActivitiesAsync(new StravaActivityQuery
        {
            Before = before,
            After = after,
            Page = 2,
            PerPage = 50
        });

        Assert.Single(results);
        Assert.Equal(123, results[0].Id);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("/api/v3/athlete/activities?before=1700000000&after=1600000000&page=2&per_page=50", capturedRequest.RequestUri!.PathAndQuery);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("token-123", capturedRequest.Headers.Authorization.Parameter);
    }

    /// <summary>
    /// Verifies null JSON payloads map to an empty activity list.
    /// </summary>
    [Fact]
    public async Task SearchActivitiesAsync_ReturnsEmptyListWhenPayloadIsNull()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));

        var client = CreateClient(handler, "token-abc");

        var results = await client.ListActivitiesAsync();

        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies non-success responses surface status and body details.
    /// </summary>
    [Fact]
    public async Task SearchActivitiesAsync_ThrowsHttpRequestExceptionForErrorResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Bad Request",
                Content = new StringContent("invalid request", Encoding.UTF8, "text/plain")
            }));

        var client = CreateClient(handler, "token-abc");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.ListActivitiesAsync());

        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid request", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies options validation rejects non-absolute API base URLs.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsForRelativeApiBaseUrl()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var tokenProvider = new Mock<IStravaAccessTokenProvider>();

        Assert.Throws<ArgumentException>(() => new StravaActivitiesClient(
            httpClientFactory.Object,
            Options.Create(new StravaClientOptions { ApiBaseUrl = "not a uri" }),
            tokenProvider.Object));
    }

    /// <summary>
    /// Creates a client instance with a controllable HTTP pipeline.
    /// </summary>
    private static StravaActivitiesClient CreateClient(HttpMessageHandler handler, string accessToken)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.strava.com/api/v3/")
        };

        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(f => f.CreateClient("StravaActivities"))
            .Returns(httpClient);

        var tokenProvider = new Mock<IStravaAccessTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessToken);

        return new StravaActivitiesClient(
            httpClientFactory.Object,
            Options.Create(new StravaClientOptions { ApiBaseUrl = "https://www.strava.com/api/v3" }),
            tokenProvider.Object);
    }

    /// <summary>
    /// Minimal HTTP handler used to return deterministic responses in tests.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        /// <summary>
        /// Initializes the handler delegate.
        /// </summary>
        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Dispatches the outgoing request to the configured delegate.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
