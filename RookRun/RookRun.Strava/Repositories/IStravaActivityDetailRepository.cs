using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Provides persistence operations for storing and retrieving Strava activity details.
/// </summary>
public interface IStravaActivityDetailRepository
{
    /// <summary>
    /// Gets a detail record for a specific activity ID.
    /// </summary>
    /// <param name="activityId">The activity ID to retrieve details for.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The activity detail, or null if not found.</returns>
    Task<StravaActivityDetail?> GetByIdAsync(long activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates a detail record for an activity.
    /// </summary>
    /// <param name="detail">The activity detail to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAsync(StravaActivityDetail detail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a detail record exists for an activity.
    /// </summary>
    /// <param name="activityId">The activity ID to check.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>True if detail exists, false otherwise.</returns>
    Task<bool> ExistsAsync(long activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the activity IDs for all cached detail records.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The activity IDs with cached details.</returns>
    Task<IReadOnlyList<long>> ListActivityIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a detail record for an activity.
    /// </summary>
    /// <param name="activityId">The activity ID to delete.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task DeleteAsync(long activityId, CancellationToken cancellationToken = default);
}
