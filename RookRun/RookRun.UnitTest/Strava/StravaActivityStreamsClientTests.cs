using Microsoft.Extensions.Options;
using Moq;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// HTTP behavior tests for <see cref="StravaActivityStreamsClient"/>.
/// </summary>
public sealed class StravaActivityStreamsClientTests
{
    /// <summary>
    /// Verifies successful stream fetch includes auth headers and query parameters.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_SendsBearerTokenAndQueryParameters()
    {
        HttpRequestMessage? capturedRequest = null;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            const string payload = """
            {
              "time": {
                "type": "time",
                "data": [0, 1],
                "series_type": "time",
                "resolution": "high",
                "original_size": 2
              }
            }
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, "token-xyz");
        var streams = await client.GetActivityStreamsAsync(123, [StravaStreamKeys.Time, StravaStreamKeys.Distance]);

        Assert.NotNull(streams);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("token-xyz", capturedRequest.Headers.Authorization.Parameter);

        var query = capturedRequest.RequestUri!.Query;
        Assert.Contains("key_by_type=true", query, StringComparison.Ordinal);
        var unescapedQuery = Uri.UnescapeDataString(query);
        Assert.Contains("keys=time,distance", unescapedQuery, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies canonical key_by_type object payload is parsed.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_ParsesCanonicalObjectPayload()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            const string payload = """
            {
              "time": {
                "type": "time",
                "data": [0, 1, 2],
                "series_type": "time",
                "resolution": "high",
                "original_size": 3
              },
              "heartrate": {
                "type": "heartrate",
                "data": [90, 91, 92],
                "series_type": "time",
                "resolution": "high",
                "original_size": 3
              }
            }
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, "token-xyz");
        var result = await client.GetActivityStreamsAsync(456, StravaStreamKeys.DefaultPhase1);

        Assert.NotNull(result);
        Assert.Equal(456, result!.ActivityId);
        Assert.Equal(2, result.Streams.Count);
        Assert.Contains("time", result.Streams.Keys);
        Assert.Contains("heartrate", result.Streams.Keys);
        Assert.Equal(JsonValueKind.Array, result.Streams["time"].Data.ValueKind);
        Assert.Equal(3, result.Streams["time"].OriginalSize);
    }

    /// <summary>
    /// Verifies array payload is parsed as compatibility fallback.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_ParsesArrayPayloadFallback()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            const string payload = """
            [
              {
                "type": "distance",
                "data": [1.0, 2.0],
                "series_type": "distance",
                "resolution": "high",
                "original_size": 2
              },
              {
                "type": "altitude",
                "data": [100.1, 100.2],
                "series_type": "distance",
                "resolution": "high",
                "original_size": 2
              }
            ]
            """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, "token-xyz");
        var result = await client.GetActivityStreamsAsync(789, [StravaStreamKeys.Distance, StravaStreamKeys.Altitude]);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Streams.Count);
        Assert.Contains("distance", result.Streams.Keys);
        Assert.Contains("altitude", result.Streams.Keys);
        Assert.Equal("distance", result.Streams["distance"].Type);
        Assert.Equal("altitude", result.Streams["altitude"].Type);
    }

    /// <summary>
    /// Verifies 404 responses return null without throwing.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_ReturnsNullFor404()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = CreateClient(handler, "token-xyz");

        var result = await client.GetActivityStreamsAsync(123, [StravaStreamKeys.Time]);

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies 429 responses surface as a rate-limit exception.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_ThrowsRateLimitExceptionFor429()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                ReasonPhrase = "Too Many Requests",
                Content = new StringContent("rate limited", Encoding.UTF8, "text/plain")
            }));

        var client = CreateClient(handler, "token-xyz");

        var exception = await Assert.ThrowsAsync<RateLimitException>(() =>
            client.GetActivityStreamsAsync(123, [StravaStreamKeys.Time]));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Contains("rate limited", exception.ResponseBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies non-success statuses (except 404/429) throw HttpRequestException.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_ThrowsHttpRequestExceptionForOtherFailureStatuses()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Server Error",
                Content = new StringContent("boom", Encoding.UTF8, "text/plain")
            }));

        var client = CreateClient(handler, "token-xyz");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetActivityStreamsAsync(123, [StravaStreamKeys.Time]));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Contains("boom", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies guard clauses for invalid arguments.
    /// </summary>
    [Fact]
    public async Task GetActivityStreamsAsync_ThrowsForInvalidArguments()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new InvalidOperationException());
        var client = CreateClient(handler, "token-xyz");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetActivityStreamsAsync(0, [StravaStreamKeys.Time]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetActivityStreamsAsync(1, []));
    }

    /// <summary>
    /// Creates a client instance with a controllable HTTP pipeline.
    /// </summary>
    private static StravaActivityStreamsClient CreateClient(HttpMessageHandler handler, string accessToken)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.strava.com/api/v3/")
        };

        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(factory => factory.CreateClient("StravaActivities"))
            .Returns(httpClient);

        var tokenProvider = new Mock<IStravaAccessTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(provider => provider.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessToken);

        return new StravaActivityStreamsClient(
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

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
