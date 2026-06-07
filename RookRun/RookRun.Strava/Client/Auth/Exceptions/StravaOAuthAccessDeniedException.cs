namespace RookRun.Strava.Client.Auth.Exceptions;

/// <summary>
/// Represents a Strava OAuth failure caused by the user denying authorization.
/// </summary>
public sealed class StravaOAuthAccessDeniedException : StravaOAuthException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaOAuthAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StravaOAuthAccessDeniedException(string message)
        : base(message)
    {
    }
}
