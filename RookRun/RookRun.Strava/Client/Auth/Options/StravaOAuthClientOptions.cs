using System.Net;

namespace RookRun.Strava.Client.Auth;

/// <summary>
/// Provides configuration for the interactive Strava OAuth client.
/// </summary>
public sealed record StravaOAuthClientOptions
{
    /// <summary>
    /// Gets the configuration section name used for Strava settings.
    /// </summary>
    public const string SectionName = "StravaOAuthClient";

    /// <summary>
    /// Gets the Strava client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the Strava client secret.
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Gets the Strava OAuth base URL.
    /// </summary>
    public string AuthorizationBaseUrl { get; init; } = "https://www.strava.com/oauth";

    /// <summary>
    /// Gets the Strava API base URL.
    /// </summary>
    public string ApiBaseUrl { get; init; } = "https://www.strava.com/api/v3";

    /// <summary>
    /// Gets the loopback host used by the temporary listener.
    /// </summary>
    public string CallbackHost { get; init; } = IPAddress.Loopback.ToString();

    /// <summary>
    /// Gets the fixed callback port, or <see langword="null"/> to use an ephemeral port.
    /// </summary>
    public int? CallbackPort { get; init; }

    /// <summary>
    /// Gets the callback route handled by the temporary listener.
    /// </summary>
    public string CallbackPath { get; init; } = "/auth/strava/callback";

    /// <summary>
    /// Gets the success route handled by the temporary listener.
    /// </summary>
    public string SuccessPath { get; init; } = "/auth/strava/success";

    /// <summary>
    /// Gets the scopes requested during authorization.
    /// </summary>
    public IReadOnlyList<string> DefaultScopes { get; init; } = new[] { "activity:read_all" };

    /// <summary>
    /// Gets the approval prompt value passed to Strava.
    /// </summary>
    public string ApprovalPrompt { get; init; } = "auto";

    /// <summary>
    /// Gets a value indicating whether the system browser should be opened automatically.
    /// </summary>
    public bool AutoOpenBrowser { get; init; } = true;

    /// <summary>
    /// Gets the default timeout for the interactive authorization flow.
    /// </summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
