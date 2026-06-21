namespace RookRun.Contracts;

/// <summary>
/// Represents the current authentication state and user identity.
/// </summary>
public sealed record UserAuthDto
{
    /// <summary>
    /// Gets a value indicating whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets the email address of the authenticated user, if authenticated.
    /// </summary>
    public string? Email { get; init; }
}
