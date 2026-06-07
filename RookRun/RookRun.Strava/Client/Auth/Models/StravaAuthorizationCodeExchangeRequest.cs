namespace RookRun.Strava.Client.Auth.Models;

/// <summary>
/// Describes the inputs required to exchange a Strava authorization code for tokens.
/// </summary>
public sealed record StravaAuthorizationCodeExchangeRequest
{
    /// <summary>
    /// Gets the Strava client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the Strava client secret.
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Gets the one-time authorization code returned by Strava.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the redirect URI used for the authorization flow.
    /// </summary>
    public required Uri RedirectUri { get; init; }
}
