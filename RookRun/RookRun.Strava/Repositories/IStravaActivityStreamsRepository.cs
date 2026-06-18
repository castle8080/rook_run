using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Provides persistence operations for storing and retrieving Strava activity streams.
/// </summary>
public interface IStravaActivityStreamsRepository
{
    /// <summary>
    /// Gets a stream record for a specific activity ID.
    /// </summary>
    /// <param name="activityId">The activity ID to retrieve streams for.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The stream record, or null if not found.</returns>
    Task<StravaActivityStreams?> GetByActivityIdAsync(long activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates stream data for an activity.
    /// </summary>
    /// <param name="streams">The stream document to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAsync(StravaActivityStreams streams, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a stream record exists for an activity.
    /// </summary>
    /// <param name="activityId">The activity ID to check.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>True when cached, otherwise false.</returns>
    Task<bool> ExistsAsync(long activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity IDs for all cached stream records.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Activity IDs with cached streams.</returns>
    Task<IReadOnlyList<long>> ListActivityIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the stream record for an activity.
    /// </summary>
    /// <param name="activityId">The activity ID to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task DeleteAsync(long activityId, CancellationToken cancellationToken = default);
}
