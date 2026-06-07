namespace RookRun.Strava.Client.Auth.Coordination;

/// <summary>
/// Stores the in-memory state for a single in-progress Strava OAuth authorization flow.
/// </summary>
public sealed class PendingAuthorizationFlow
{
    /// <summary>
    /// Gets the expected OAuth state value for the flow.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the task completion source observed by the waiting caller.
    /// </summary>
    public required TaskCompletionSource<string> Completion { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the flow was created.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
