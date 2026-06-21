namespace RookRun.Api.Authentication;

/// <summary>
/// Provides names for authorization policies used by the API host.
/// </summary>
public static class RookRunAuthorizationPolicyNames
{
    /// <summary>
    /// Policy requiring an authenticated user whose email is in the configured allowlist.
    /// </summary>
    public const string AllowlistedUser = "AllowlistedUser";
}