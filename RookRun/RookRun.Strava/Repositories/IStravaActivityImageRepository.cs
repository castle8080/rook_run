namespace RookRun.Strava.Repositories;

using RookRun.Strava.Models;

/// <summary>
/// Provides persistence operations for storing and retrieving Strava activity images.
/// </summary>
public interface IStravaActivityImageRepository
{
    /// <summary>
    /// Gets image bytes for a specific activity and image ID.
    /// </summary>
    /// <param name="activityId">The activity ID.</param>
    /// <param name="imageId">The image ID (unique_id from Strava).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Image bytes, or null if not found.</returns>
    Task<byte[]?> GetImageAsync(long activityId, string imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves image bytes for a specific activity and image ID.
    /// </summary>
    /// <param name="activityId">The activity ID.</param>
    /// <param name="imageId">The image ID (unique_id from Strava).</param>
    /// <param name="imageExtension">File extension (e.g., "jpg", "png").</param>
    /// <param name="imageBytes">The image data to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveImageAsync(
        long activityId,
        string imageId,
        string imageExtension,
        byte[] imageBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all image IDs stored for a specific activity.
    /// </summary>
    /// <param name="activityId">The activity ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Collection of image IDs (without file extensions).</returns>
    Task<IReadOnlyList<string>> ListImagesByActivityAsync(long activityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all cached image keys across all activities.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Collection of cached image keys.</returns>
    Task<IReadOnlyList<StravaActivityImageKey>> ListImageKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an image exists for a specific activity and image ID.
    /// </summary>
    /// <param name="activityId">The activity ID.</param>
    /// <param name="imageId">The image ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>True if image exists, false otherwise.</returns>
    Task<bool> ImageExistsAsync(long activityId, string imageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an image for a specific activity and image ID.
    /// </summary>
    /// <param name="activityId">The activity ID.</param>
    /// <param name="imageId">The image ID.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task DeleteImageAsync(long activityId, string imageId, CancellationToken cancellationToken = default);
}
