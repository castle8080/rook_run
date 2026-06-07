namespace RookRun.Strava.Client.Auth.Exceptions;

/// <summary>
/// Represents a base error for Strava OAuth failures.
/// </summary>
public class StravaOAuthException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StravaOAuthException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying cause of the failure.</param>
    public StravaOAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
