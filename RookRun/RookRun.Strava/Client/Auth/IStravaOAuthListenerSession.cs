namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Represents an active temporary OAuth listener instance.
/// </summary>
public interface IStravaOAuthListenerSession : IAsyncDisposable
{
    /// <summary>
    /// Gets the effective callback URI bound by the loopback listener.
    /// </summary>
    Uri CallbackUri { get; }

    /// <summary>
    /// Gets the effective success URI, when one is exposed.
    /// </summary>
    Uri? SuccessUri { get; }
}
