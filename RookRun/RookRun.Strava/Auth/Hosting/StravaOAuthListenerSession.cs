using Microsoft.AspNetCore.Builder;

namespace RookRun.Strava.Auth.Hosting;

/// <summary>
/// Represents a running temporary Strava OAuth loopback listener.
/// </summary>
public sealed class StravaOAuthListenerSession : IStravaOAuthListenerSession
{
    private readonly WebApplication _application;

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthListenerSession"/> class.
    /// </summary>
    /// <param name="application">The running web application instance.</param>
    /// <param name="callbackUri">The effective callback URI.</param>
    /// <param name="successUri">The effective success URI, when available.</param>
    public StravaOAuthListenerSession(WebApplication application, Uri callbackUri, Uri? successUri)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        CallbackUri = callbackUri ?? throw new ArgumentNullException(nameof(callbackUri));
        SuccessUri = successUri;
    }

    /// <inheritdoc />
    public Uri CallbackUri { get; }

    /// <inheritdoc />
    public Uri? SuccessUri { get; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
