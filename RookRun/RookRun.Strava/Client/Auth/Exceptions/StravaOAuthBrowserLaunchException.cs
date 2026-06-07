namespace RookRun.Strava.Client.Auth.Exceptions;

/// <summary>
/// Represents a failure to open the system browser for authorization.
/// </summary>
public sealed class StravaOAuthBrowserLaunchException : StravaOAuthException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthBrowserLaunchException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying launch failure.</param>
    public StravaOAuthBrowserLaunchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
