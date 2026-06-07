using System.Text.Json;

namespace RookRun.Strava.Client.Auth.Models;

/// <summary>
/// Represents the token payload returned by Strava after a successful OAuth exchange.
/// </summary>
public sealed record StravaOAuthTokenResult
{
    /// <summary>
    /// Gets the access token used for Strava API calls.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// Gets the refresh token used to obtain future access tokens.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// Gets the UNIX timestamp at which the access token expires.
    /// </summary>
    public required long ExpiresAtUnixTimeSeconds { get; init; }

    /// <summary>
    /// Gets the token type returned by Strava.
    /// </summary>
    public required string TokenType { get; init; }

    /// <summary>
    /// Gets the granted scopes parsed from the Strava response.
    /// </summary>
    public required IReadOnlyList<string> GrantedScopes { get; init; }

    /// <summary>
    /// Gets the raw scope string returned by Strava, when present.
    /// </summary>
    public string? RawScope { get; init; }

    /// <summary>
    /// Gets the raw athlete payload returned by Strava, when present.
    /// </summary>
    public JsonElement? Athlete { get; init; }

    /// <summary>
    /// Gets any additional JSON properties that were not mapped to top-level members.
    /// </summary>
    public Dictionary<string, JsonElement> AdditionalData { get; init; } = [];

    /// <summary>
    /// Gets the raw JSON response body returned by Strava.
    /// </summary>
    public string RawResponseJson { get; init; } = string.Empty;
}
