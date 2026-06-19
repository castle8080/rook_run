using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Provides persistence for the activity-to-image-id index.
/// </summary>
public interface IStravaActivityImageIdIndexRepository
{
    /// <summary>
    /// Loads the current index and its ETag, if present.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The loaded index and its ETag, or an empty index and null ETag when missing.</returns>
    Task<(StravaActivityImageIdIndex Index, string? ETag)> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the supplied index using optimistic concurrency.
    /// </summary>
    /// <param name="index">The index to save.</param>
    /// <param name="ifMatchETag">Optional ETag that must match the current stored object.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAsync(
        StravaActivityImageIdIndex index,
        string? ifMatchETag,
        CancellationToken cancellationToken = default);
}