using Microsoft.Extensions.Options;
using RookRun.Strava.Client.Auth;
using RookRun.Strava.Client.Auth.Coordination;
using RookRun.Strava.Client.Auth.Exceptions;
using RookRun.Strava.Client.Auth.Models;
using System.Net;
using System.Text;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Focused flow and exchange tests for <see cref="StravaOAuthClient"/>.
/// </summary>
public sealed class StravaOAuthClientFlowTests
{
    /// <summary>
    /// Verifies AcquireTokenAsync completes successfully through listener callback and token exchange.
    /// </summary>
    [Fact]
    public async Task AcquireTokenAsync_ReturnsTokenResultOnSuccessfulFlow()
    {
        var tokenJson = """
            {
              "token_type": "Bearer",
              "access_token": "access-token",
              "refresh_token": "refresh-token",
              "expires_at": 1735689600,
              "scope": "activity:read_all"
            }
            """;

        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v3/token", request.RequestUri!.PathAndQuery);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
            });
        });

        var session = new StubListenerSession(new Uri("http://127.0.0.1:52431/auth/strava/callback"));
        var listenerHost = new StubListenerHost(async (_, coordinator, _) =>
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(10);
                var state = GetSinglePendingState(coordinator);
                coordinator.CompleteSuccess(state, "auth-code-123");
            });

            return session;
        });

        var launcher = new StubAuthorizationLauncher();
        var client = CreateClient(listenerHost, launcher, handler, options: null);

        var result = await client.AcquireTokenAsync();

        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(["activity:read_all"], result.GrantedScopes);
        Assert.True(launcher.WasCalled);
        Assert.NotNull(launcher.LastAuthorizationUri);
        Assert.True(session.DisposeCalled);
    }

    /// <summary>
    /// Verifies AcquireTokenAsync surfaces timeout when listener startup exceeds configured timeout.
    /// </summary>
    [Fact]
    public async Task AcquireTokenAsync_ThrowsTimeoutExceptionWhenStartupTimesOut()
    {
        var listenerHost = new StubListenerHost(async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new StubListenerSession(new Uri("http://127.0.0.1:1/auth/strava/callback"));
        });

        var launcher = new StubAuthorizationLauncher();
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var client = CreateClient(
            listenerHost,
            launcher,
            handler,
            new StravaOAuthClientOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                DefaultTimeout = TimeSpan.FromMilliseconds(30)
            });

        await Assert.ThrowsAsync<StravaOAuthTimeoutException>(() => client.AcquireTokenAsync());
        Assert.False(launcher.WasCalled);
    }

    /// <summary>
    /// Verifies caller cancellation is propagated from AcquireTokenAsync.
    /// </summary>
    [Fact]
    public async Task AcquireTokenAsync_ThrowsOperationCanceledWhenCallerCancels()
    {
        var listenerHost = new StubListenerHost((_, _, cancellationToken) =>
            Task.FromResult<IStravaOAuthListenerSession>(new BlockingListenerSession(cancellationToken)));

        var launcher = new StubAuthorizationLauncher();
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = CreateClient(listenerHost, launcher, handler, options: null);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(30);

        await Assert.ThrowsAsync<TaskCanceledException>(() => client.AcquireTokenAsync(cts.Token));
    }

    /// <summary>
    /// Verifies token endpoint non-success responses are wrapped in StravaOAuthTokenExchangeException.
    /// </summary>
    [Fact]
    public async Task RefreshAccessTokenAsync_ThrowsTokenExchangeExceptionForNonSuccessResponse()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Bad Request",
                Content = new StringContent("bad-token-request", Encoding.UTF8, "text/plain")
            }));

        var client = CreateClient(new NoopListenerHost(), new StubAuthorizationLauncher(), handler, options: null);

        var exception = await Assert.ThrowsAsync<StravaOAuthTokenExchangeException>(() =>
            client.RefreshAccessTokenAsync("refresh-token"));

        Assert.Contains("failed with status code 400", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies invalid JSON token responses are wrapped in StravaOAuthTokenExchangeException.
    /// </summary>
    [Fact]
    public async Task RefreshAccessTokenAsync_ThrowsTokenExchangeExceptionForInvalidJson()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            }));

        var client = CreateClient(new NoopListenerHost(), new StubAuthorizationLauncher(), handler, options: null);

        var exception = await Assert.ThrowsAsync<StravaOAuthTokenExchangeException>(() =>
            client.RefreshAccessTokenAsync("refresh-token"));

        Assert.Contains("invalid JSON", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(exception.InnerException);
    }

    /// <summary>
    /// Verifies refresh flow rejects blank refresh tokens.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefreshAccessTokenAsync_ThrowsForBlankRefreshToken(string refreshToken)
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var client = CreateClient(new NoopListenerHost(), new StubAuthorizationLauncher(), handler, options: null);

        await Assert.ThrowsAsync<ArgumentException>(() => client.RefreshAccessTokenAsync(refreshToken));
    }

    /// <summary>
    /// Creates a configured client instance for tests.
    /// </summary>
    private static StravaOAuthClient CreateClient(
        IStravaOAuthListenerHost listenerHost,
        IStravaAuthorizationLauncher launcher,
        HttpMessageHandler handler,
        StravaOAuthClientOptions? options)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.strava.com/api/v3/")
        };

        var httpClientFactory = new StubHttpClientFactory(httpClient);

        var resolvedOptions = options ?? new StravaOAuthClientOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            DefaultTimeout = TimeSpan.FromSeconds(5)
        };

        return new StravaOAuthClient(listenerHost, launcher, httpClientFactory, Options.Create(resolvedOptions));
    }

    /// <summary>
    /// Gets the single pending state currently tracked by the coordinator.
    /// </summary>
    private static string GetSinglePendingState(StravaOAuthCallbackCoordinator coordinator)
    {
        var field = typeof(StravaOAuthCallbackCoordinator)
            .GetField("_pendingFlows", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(field);

        var dictionary = field!.GetValue(coordinator) as System.Collections.IDictionary;
        Assert.NotNull(dictionary);
        Assert.Single(dictionary!.Keys.Cast<object>());

        return dictionary.Keys.Cast<string>().Single();
    }

    /// <summary>
    /// Simple listener host implementation for deterministic test flows.
    /// </summary>
    private sealed class StubListenerHost : IStravaOAuthListenerHost
    {
        private readonly Func<StravaOAuthListenerOptions, StravaOAuthCallbackCoordinator, CancellationToken, Task<IStravaOAuthListenerSession>> _start;

        /// <summary>
        /// Initializes the listener start delegate.
        /// </summary>
        public StubListenerHost(Func<StravaOAuthListenerOptions, StravaOAuthCallbackCoordinator, CancellationToken, Task<IStravaOAuthListenerSession>> start)
        {
            _start = start;
        }

        /// <summary>
        /// Starts the configured listener delegate.
        /// </summary>
        public Task<IStravaOAuthListenerSession> StartAsync(StravaOAuthListenerOptions options, StravaOAuthCallbackCoordinator coordinator, CancellationToken cancellationToken = default)
        {
            return _start(options, coordinator, cancellationToken);
        }
    }

    /// <summary>
    /// No-op listener used by refresh-only tests.
    /// </summary>
    private sealed class NoopListenerHost : IStravaOAuthListenerHost
    {
        /// <summary>
        /// Returns a disposable session placeholder.
        /// </summary>
        public Task<IStravaOAuthListenerSession> StartAsync(StravaOAuthListenerOptions options, StravaOAuthCallbackCoordinator coordinator, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IStravaOAuthListenerSession>(new StubListenerSession(new Uri("http://127.0.0.1:1/auth/strava/callback")));
        }
    }

    /// <summary>
    /// Captures browser launch requests.
    /// </summary>
    private sealed class StubAuthorizationLauncher : IStravaAuthorizationLauncher
    {
        /// <summary>
        /// Gets whether OpenAsync was called.
        /// </summary>
        public bool WasCalled { get; private set; }

        /// <summary>
        /// Gets the last authorization URI passed to OpenAsync.
        /// </summary>
        public Uri? LastAuthorizationUri { get; private set; }

        /// <summary>
        /// Records the authorization URI and completes immediately.
        /// </summary>
        public Task OpenAsync(Uri authorizationUri, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastAuthorizationUri = authorizationUri;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Listener session implementation used in tests.
    /// </summary>
    private sealed class StubListenerSession : IStravaOAuthListenerSession
    {
        /// <summary>
        /// Initializes a new test session.
        /// </summary>
        public StubListenerSession(Uri callbackUri)
        {
            CallbackUri = callbackUri;
            SuccessUri = new Uri(callbackUri, "/auth/strava/success");
        }

        /// <summary>
        /// Gets the callback URI.
        /// </summary>
        public Uri CallbackUri { get; }

        /// <summary>
        /// Gets the success URI.
        /// </summary>
        public Uri? SuccessUri { get; }

        /// <summary>
        /// Gets whether DisposeAsync was called.
        /// </summary>
        public bool DisposeCalled { get; private set; }

        /// <summary>
        /// Marks the session as disposed.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Listener session that blocks until cancellation to simulate user waiting.
    /// </summary>
    private sealed class BlockingListenerSession : IStravaOAuthListenerSession
    {
        /// <summary>
        /// Initializes the blocking session.
        /// </summary>
        public BlockingListenerSession(CancellationToken token)
        {
            CallbackUri = new Uri("http://127.0.0.1:1/auth/strava/callback");
            SuccessUri = new Uri("http://127.0.0.1:1/auth/strava/success");
            _token = token;
        }

        private readonly CancellationToken _token;

        /// <summary>
        /// Gets the callback URI.
        /// </summary>
        public Uri CallbackUri { get; }

        /// <summary>
        /// Gets the success URI.
        /// </summary>
        public Uri? SuccessUri { get; }

        /// <summary>
        /// Waits for cancellation before completing disposal.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, _token);
        }
    }

    /// <summary>
    /// Fixed-name HttpClient factory used by OAuth client tests.
    /// </summary>
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Initializes the factory with a fixed client instance.
        /// </summary>
        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Returns the fixed client instance for any requested name.
        /// </summary>
        public HttpClient CreateClient(string name) => _httpClient;
    }

    /// <summary>
    /// Lightweight handler for deterministic HTTP responses.
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        /// <summary>
        /// Initializes the response delegate.
        /// </summary>
        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Dispatches HTTP requests through the configured delegate.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
