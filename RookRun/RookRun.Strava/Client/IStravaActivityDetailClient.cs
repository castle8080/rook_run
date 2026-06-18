using RookRun.Strava.Models;

namespace RookRun.Strava.Client;

/// <summary>
/// Provides access to the authenticated Strava activity detail API.
/// Enables retrieving comprehensive activity information including segment efforts,
/// laps, splits, and associated photos.
/// </summary>
public interface IStravaActivityDetailClient
{
    /// <summary>
    /// Gets the detailed representation of a single activity by ID.
    /// Returns comprehensive activity data including segment efforts, laps, splits, and photos.
    /// 
    /// Requires activity:read scope for public/followers activities,
    /// or activity:read_all scope for private/only-me activities.
    /// </summary>
    /// <param name="activityId">The activity ID to fetch details for.</param>
    /// <param name="includeAllEfforts">
    /// Optional. If true, include all segment efforts (default may vary by Strava subscription).
    /// Default is false.
    /// </param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The detailed activity, or null if not found (404).</returns>
    Task<StravaActivityDetail?> GetActivityDetailAsync(
        long activityId,
        bool includeAllEfforts = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all photos available for an activity using Strava's activity photos endpoint.
    /// This complements activity detail payloads that only expose a primary photo plus count metadata.
    /// </summary>
    /// <param name="activityId">The activity ID whose photos should be listed.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// Collection of image metadata records with URLs and extensions.
    /// Returns an empty collection when no photos are found.
    /// </returns>
    Task<IReadOnlyList<StravaActivityImage>> GetActivityPhotosAsync(
        long activityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts image metadata from a detailed activity's photos object.
    /// Parses the photos.primary and photos array to produce image records ready for download.
    /// Does not perform any network operations.
    /// </summary>
    /// <param name="activityDetail">The detailed activity containing photos to extract.</param>
    /// <returns>
    /// Collection of image metadata records with URLs and extensions.
    /// Returns empty list if activity has no photos.
    /// </returns>
    IReadOnlyList<StravaActivityImage> ExtractActivityImages(
        StravaActivityDetail activityDetail);

    /// <summary>
    /// Downloads an image from the given URL.
    /// Images are typically hosted on Strava's CloudFront CDN.
    /// </summary>
    /// <param name="imageUrl">The image URL to download.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>Image bytes if successful, or null if download fails or returns non-2xx status.</returns>
    Task<byte[]?> DownloadImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default);
}
