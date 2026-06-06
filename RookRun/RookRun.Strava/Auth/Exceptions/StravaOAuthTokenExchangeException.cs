namespace RookRun.Strava.Auth.Exceptions;

/// <summary>
/// Represents a failure while exchanging a Strava authorization code for tokens.
/// </summary>
public sealed class StravaOAuthTokenExchangeException : StravaOAuthException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthTokenExchangeException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StravaOAuthTokenExchangeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthTokenExchangeException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The underlying exchange failure.</param>
    public StravaOAuthTokenExchangeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
