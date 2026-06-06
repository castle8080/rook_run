namespace RookRun.Strava.Auth.Exceptions;

/// <summary>
/// Represents a Strava OAuth callback that did not match an expected state value.
/// </summary>
public sealed class StravaOAuthStateMismatchException : StravaOAuthException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthStateMismatchException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StravaOAuthStateMismatchException(string message)
        : base(message)
    {
    }
}
