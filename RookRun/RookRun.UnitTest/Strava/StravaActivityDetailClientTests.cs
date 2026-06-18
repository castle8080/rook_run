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
/// HTTP behavior tests for <see cref="StravaActivityDetailClient"/>.
/// </summary>
public sealed class StravaActivityDetailClientTests
{
    /// <summary>
    /// Verifies successful detail fetch includes auth headers and query parameters.
    /// </summary>
    [Fact]
    public async Task GetActivityDetailAsync_SendsBearerTokenAndQueryParameter()
    {
        HttpRequestMessage? capturedRequest = null;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            const string payload = @"{""id"":123,""name"":""My Run"",""distance"":5000.5,""moving_time"":1200}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, "token-xyz");

        var detail = await client.GetActivityDetailAsync(123, includeAllEfforts: true);

        Assert.NotNull(detail);
        Assert.Equal(123, detail.Id);
        Assert.Equal("My Run", detail.Name);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Contains("include_all_efforts=true", capturedRequest.RequestUri!.PathAndQuery, StringComparison.Ordinal);
        Assert.NotNull(capturedRequest.Headers.Authorization);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("token-xyz", capturedRequest.Headers.Authorization.Parameter);
    }

    /// <summary>
    /// Verifies 404 responses return null without throwing.
    /// </summary>
    [Fact]
    public async Task GetActivityDetailAsync_ReturnsNullFor404()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = CreateClient(handler, "token-xyz");

        var detail = await client.GetActivityDetailAsync(999);

        Assert.Null(detail);
    }

    /// <summary>
    /// Verifies error responses include status and body in exception.
    /// </summary>
    [Fact]
    public async Task GetActivityDetailAsync_ThrowsHttpRequestExceptionForErrorResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                ReasonPhrase = "Unauthorized",
                Content = new StringContent("invalid token", Encoding.UTF8, "text/plain")
            }));

        var client = CreateClient(handler, "bad-token");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetActivityDetailAsync(123));

        Assert.Contains("401", exception.Message, StringComparison.Ordinal);
        Assert.Contains("invalid token", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies 429 responses surface as a rate-limit exception.
    /// </summary>
    [Fact]
    public async Task GetActivityDetailAsync_ThrowsRateLimitExceptionFor429()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                ReasonPhrase = "Too Many Requests",
                Content = new StringContent("rate limited", Encoding.UTF8, "text/plain")
            }));

        var client = CreateClient(handler, "token-xyz");

        var exception = await Assert.ThrowsAsync<RateLimitException>(() => client.GetActivityDetailAsync(123));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Contains("rate limited", exception.ResponseBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies extraction of images from primary photo.
    /// </summary>
    [Fact]
    public void ExtractActivityImages_ExtractsPrimaryPhoto()
    {
        var detail = new StravaActivityDetail
        {
            Id = 123,
            Photos = JsonDocument.Parse(@"{
                ""primary"": {
                    ""unique_id"": ""photo-1"",
                    ""urls"": {
                        ""100"": ""https://example.com/img_100.jpg"",
                        ""600"": ""https://example.com/img_600.jpg""
                    }
                }
            }").RootElement
        };

        var client = CreateClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException()), "token");
        var images = client.ExtractActivityImages(detail);

        Assert.Single(images);
        Assert.Equal("photo-1", images[0].ImageId);
        Assert.Equal("600", images[0].SizeVariant);
        Assert.Equal("https://example.com/img_600.jpg", images[0].ImageUrl);
    }

    /// <summary>
    /// Verifies extraction handles missing photos gracefully.
    /// </summary>
    [Fact]
    public void ExtractActivityImages_ReturnsEmptyForNullPhotos()
    {
        var detail = new StravaActivityDetail { Id = 123, Photos = null };

        var client = CreateClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException()), "token");
        var images = client.ExtractActivityImages(detail);

        Assert.Empty(images);
    }

    /// <summary>
    /// Verifies extraction handles photo array.
    /// </summary>
    [Fact]
    public void ExtractActivityImages_ExtractsPhotoArray()
    {
        var detail = new StravaActivityDetail
        {
            Id = 456,
            Photos = JsonDocument.Parse(@"{
                ""photos"": [
                    {
                        ""unique_id"": ""photo-a"",
                        ""urls"": { ""600"": ""https://example.com/a_600.jpg"" }
                    },
                    {
                        ""unique_id"": ""photo-b"",
                        ""urls"": { ""600"": ""https://example.com/b_600.jpg"" }
                    }
                ]
            }").RootElement
        };

        var client = CreateClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException()), "token");
        var images = client.ExtractActivityImages(detail);

        Assert.Equal(2, images.Count);
        Assert.Contains(images, img => img.ImageId == "photo-a");
        Assert.Contains(images, img => img.ImageId == "photo-b");
    }

    /// <summary>
    /// Verifies GetActivityPhotosAsync parses activity photos endpoint arrays.
    /// </summary>
    [Fact]
    public async Task GetActivityPhotosAsync_ExtractsPhotoArray()
    {
        HttpRequestMessage? capturedRequest = null;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request;
            const string payload = @"[
                {
                    ""unique_id"": ""photo-a"",
                    ""urls"": {
                        ""100"": ""https://example.com/a_100.jpg"",
                        ""600"": ""https://example.com/a_600.jpg""
                    }
                },
                {
                    ""unique_id"": ""photo-b"",
                    ""urls"": {
                        ""600"": ""https://example.com/b_600.jpg""
                    }
                }
            ]";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, "token-xyz");

        var images = await client.GetActivityPhotosAsync(456);

        Assert.Equal(2, images.Count);
        Assert.Contains(images, img => img.ImageId == "photo-a" && img.ImageUrl == "https://example.com/a_600.jpg");
        Assert.Contains(images, img => img.ImageId == "photo-b" && img.ImageUrl == "https://example.com/b_600.jpg");
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Contains("activities/456/photos", capturedRequest.RequestUri!.PathAndQuery, StringComparison.Ordinal);
        Assert.Contains("size=600", capturedRequest.RequestUri!.PathAndQuery, StringComparison.Ordinal);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("token-xyz", capturedRequest.Headers.Authorization?.Parameter);
    }

    /// <summary>
    /// Verifies GetActivityPhotosAsync handles not found by returning an empty set.
    /// </summary>
    [Fact]
    public async Task GetActivityPhotosAsync_ReturnsEmptyFor404()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var client = CreateClient(handler, "token-xyz");

        var images = await client.GetActivityPhotosAsync(999);

        Assert.Empty(images);
    }

    /// <summary>
    /// Verifies invalid URLs are rejected.
    /// </summary>
    [Fact]
    public async Task GetActivityDetailAsync_ThrowsForInvalidActivityId()
    {
        var client = CreateClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException()), "token");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetActivityDetailAsync(0));
    }

    /// <summary>
    /// Verifies image downloads surface 429 responses as a rate-limit exception.
    /// </summary>
    [Fact]
    public async Task DownloadImageAsync_ThrowsRateLimitExceptionFor429()
    {
        var httpClientFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        httpClientFactory
            .Setup(f => f.CreateClient("StravaImages"))
            .Returns(new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    ReasonPhrase = "Too Many Requests",
                    Content = new StringContent("rate limited", Encoding.UTF8, "text/plain")
                })))
            {
                BaseAddress = new Uri("https://www.strava.com/api/v3/")
            });

        var tokenProvider = new Mock<IStravaAccessTokenProvider>(MockBehavior.Strict);
        tokenProvider
            .Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-xyz");

        var client = new StravaActivityDetailClient(
            httpClientFactory.Object,
            Options.Create(new StravaClientOptions { ApiBaseUrl = "https://www.strava.com/api/v3" }),
            tokenProvider.Object);

        var exception = await Assert.ThrowsAsync<RateLimitException>(() => client.DownloadImageAsync("https://example.com/photo.jpg"));

        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
    }

    /// <summary>
    /// Creates a client instance with a controllable HTTP pipeline.
    /// </summary>
    private static StravaActivityDetailClient CreateClient(HttpMessageHandler handler, string accessToken)
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

        return new StravaActivityDetailClient(
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
