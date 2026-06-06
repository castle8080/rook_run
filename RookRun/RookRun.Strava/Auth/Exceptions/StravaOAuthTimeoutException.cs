namespace RookRun.Strava.Auth.Exceptions;

/// <summary>
/// Represents a Strava OAuth flow that timed out before completion.
/// </summary>
public sealed class StravaOAuthTimeoutException : StravaOAuthException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StravaOAuthTimeoutException(string message)
        : base(message)
    {
    }
}
