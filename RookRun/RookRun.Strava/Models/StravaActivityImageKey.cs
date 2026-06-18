namespace RookRun.Strava.Models;

/// <summary>
/// Represents a unique identity for a cached Strava activity image.
/// </summary>
public readonly record struct StravaActivityImageKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StravaActivityImageKey"/> struct.
    /// </summary>
    /// <param name="activityId">The owning activity ID.</param>
    /// <param name="imageId">The Strava image ID.</param>
    public StravaActivityImageKey(long activityId, string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId))
        {
            throw new ArgumentException("Image ID cannot be null or whitespace.", nameof(imageId));
        }

        ActivityId = activityId;
        ImageId = imageId;
    }

    /// <summary>
    /// Gets the owning activity ID.
    /// </summary>
    public long ActivityId { get; }

    /// <summary>
    /// Gets the Strava image ID.
    /// </summary>
    public string ImageId { get; }
}