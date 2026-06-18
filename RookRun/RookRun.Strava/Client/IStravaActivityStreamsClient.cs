using RookRun.Strava.Models;

namespace RookRun.Strava.Client;

/// <summary>
/// Provides access to authenticated Strava activity streams.
/// </summary>
public interface IStravaActivityStreamsClient
{
    /// <summary>
    /// Gets stream data for a single activity.
    /// </summary>
    /// <param name="activityId">The Strava activity identifier.</param>
    /// <param name="keys">The stream keys to request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stream document, or null when the activity cannot be found.</returns>
    Task<StravaActivityStreams?> GetActivityStreamsAsync(
        long activityId,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken = default);
}
