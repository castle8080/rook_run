namespace RookRun.Strava.Auth;

/// <summary>
/// Describes how the temporary OAuth listener should bind and which routes it should expose.
/// </summary>
public sealed record StravaOAuthListenerOptions
{
    /// <summary>
    /// Gets the loopback host used by the listener.
    /// </summary>
    public required string CallbackHost { get; init; }

    /// <summary>
    /// Gets the requested callback port, or <see langword="null"/> for an ephemeral port.
    /// </summary>
    public int? CallbackPort { get; init; }

    /// <summary>
    /// Gets the callback route.
    /// </summary>
    public required string CallbackPath { get; init; }

    /// <summary>
    /// Gets the success route.
    /// </summary>
    public required string SuccessPath { get; init; }
}
