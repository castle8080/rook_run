using RookRun.ObjectStore;
using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Stores Strava activity images in the configured object store.
/// Each image is stored as: activities/strava_images/{activity_id}/{image_id}.{extension}
/// </summary>
public sealed class ObjectStoreStravaActivityImageRepository : IStravaActivityImageRepository
{
    private const string ImagePrefix = "activities/strava_images/";

    private readonly IObjectStore _objectStore;
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreStravaActivityImageRepository"/> class.
    /// </summary>
    /// <param name="objectStore">The object store used for persistence.</param>
    /// <param name="prefix">Optional prefix for all paths (e.g., "strava/" to store under "strava/activities/strava_images/...").</param>
    public ObjectStoreStravaActivityImageRepository(IObjectStore objectStore, string prefix = "")
    {
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _prefix = NormalizePrefix(prefix);
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetImageAsync(long activityId, string imageId, CancellationToken cancellationToken = default)
    {
        ValidateImageId(imageId);
        
        var imagePath = await FindImagePathAsync(activityId, imageId, cancellationToken);
        if (imagePath == null)
        {
            return null;
        }

        var streamResult = await _objectStore.TryReadStreamAsync(imagePath, cancellationToken: cancellationToken);
        if (!streamResult.IsFound || streamResult.Value == null)
        {
            return null;
        }

        using (streamResult.Value)
        {
            using (var memoryStream = new MemoryStream())
            {
                await streamResult.Value.CopyToAsync(memoryStream, cancellationToken);
                return memoryStream.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public async Task SaveImageAsync(
        long activityId,
        string imageId,
        string imageExtension,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        ValidateImageId(imageId);
        ValidateImageExtension(imageExtension);
        ArgumentNullException.ThrowIfNull(imageBytes);

        // First, delete any existing image with this ID (different extension)
        var existingPath = await FindImagePathAsync(activityId, imageId, cancellationToken);
        if (existingPath != null)
        {
            await _objectStore.TryDeleteObjectAsync(existingPath, cancellationToken);
        }

        var path = BuildPath(activityId, imageId, imageExtension);
        using (var stream = new MemoryStream(imageBytes))
        {
            await _objectStore.StoreStreamAsync(path, stream, overwrite: true, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListImagesByActivityAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var activityPrefix = BuildActivityPrefix(activityId);
        var objects = await _objectStore.ListObjectsAsync(activityPrefix, cancellationToken);

        return objects
            .Select(GetImageIdFromPath)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StravaActivityImageKey>> ListImageKeysAsync(CancellationToken cancellationToken = default)
    {
        var objects = await _objectStore.ListObjectsAsync(BuildRootPrefix(), cancellationToken);
        var imageKeys = new List<StravaActivityImageKey>();

        foreach (var objectPath in objects)
        {
            if (TryGetImageKeyFromPath(objectPath, out var imageKey))
            {
                imageKeys.Add(imageKey);
            }
        }

        return imageKeys
            .Distinct()
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> ImageExistsAsync(long activityId, string imageId, CancellationToken cancellationToken = default)
    {
        ValidateImageId(imageId);
        
        var imagePath = await FindImagePathAsync(activityId, imageId, cancellationToken);
        return imagePath != null;
    }

    /// <inheritdoc />
    public async Task DeleteImageAsync(long activityId, string imageId, CancellationToken cancellationToken = default)
    {
        ValidateImageId(imageId);
        
        var imagePath = await FindImagePathAsync(activityId, imageId, cancellationToken);
        if (imagePath != null)
        {
            await _objectStore.TryDeleteObjectAsync(imagePath, cancellationToken);
        }
    }

    /// <summary>
    /// Finds the full path to an image by searching for any file matching the image ID.
    /// This is necessary because we may not know the exact extension.
    /// </summary>
    private async Task<string?> FindImagePathAsync(long activityId, string imageId, CancellationToken cancellationToken)
    {
        var activityPrefix = BuildActivityPrefix(activityId);
        var objects = await _objectStore.ListObjectsAsync(activityPrefix, cancellationToken);

        return objects.FirstOrDefault(p => GetFileNameWithoutExtension(p) == imageId);
    }

    /// <summary>
    /// Builds the object store path for an image file.
    /// </summary>
    private string BuildPath(long activityId, string imageId, string extension)
    {
        var fileName = $"{imageId}.{extension.TrimStart('.')}";
        var activityPath = $"{ImagePrefix}{activityId}/{fileName}";
        return _prefix.Length == 0 ? activityPath : $"{_prefix}{activityPath}";
    }

    /// <summary>
    /// Builds the prefix path for all images of an activity.
    /// </summary>
    private string BuildActivityPrefix(long activityId)
    {
        var activityPath = $"{ImagePrefix}{activityId}/";
        return _prefix.Length == 0 ? activityPath : $"{_prefix}{activityPath}";
    }

    /// <summary>
    /// Builds the root prefix path for all image objects.
    /// </summary>
    private string BuildRootPrefix()
    {
        return _prefix.Length == 0 ? ImagePrefix : $"{_prefix}{ImagePrefix}";
    }

    /// <summary>
    /// Extracts the image ID (filename without extension) from a full path.
    /// </summary>
    private static string GetImageIdFromPath(string path)
    {
        var fileName = Path.GetFileName(path);
        return GetFileNameWithoutExtension(fileName);
    }

    /// <summary>
    /// Gets the filename without extension from a path or filename.
    /// </summary>
    private static string GetFileNameWithoutExtension(string pathOrFileName)
    {
        var fileName = Path.GetFileName(pathOrFileName);
        var lastDotIndex = fileName.LastIndexOf('.');
        return lastDotIndex > 0 ? fileName.Substring(0, lastDotIndex) : fileName;
    }

    /// <summary>
    /// Attempts to parse an image key from an object path.
    /// </summary>
    private bool TryGetImageKeyFromPath(string objectPath, out StravaActivityImageKey imageKey)
    {
        var rootPrefix = BuildRootPrefix();
        if (!objectPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            imageKey = default;
            return false;
        }

        var relativePath = objectPath[rootPrefix.Length..];
        var slashIndex = relativePath.IndexOf('/');
        if (slashIndex <= 0)
        {
            imageKey = default;
            return false;
        }

        var activityIdText = relativePath[..slashIndex];
        var fileName = relativePath[(slashIndex + 1)..];
        if (fileName.Contains('/', StringComparison.Ordinal) || fileName.Contains('\\', StringComparison.Ordinal))
        {
            imageKey = default;
            return false;
        }

        if (!long.TryParse(activityIdText, out var activityId))
        {
            imageKey = default;
            return false;
        }

        var imageId = GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(imageId))
        {
            imageKey = default;
            return false;
        }

        imageKey = new StravaActivityImageKey(activityId, imageId);
        return true;
    }

    /// <summary>
    /// Normalizes a prefix to ensure it ends with a slash and doesn't have leading/trailing spaces.
    /// </summary>
    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var trimmed = prefix.Trim();
        return trimmed.EndsWith('/') ? trimmed : $"{trimmed}/";
    }

    /// <summary>
    /// Validates an image ID to ensure it can be safely embedded in a single path segment.
    /// </summary>
    private static void ValidateImageId(string imageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageId);

        if (imageId.Contains('/', StringComparison.Ordinal) || imageId.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Image ID cannot contain path separators.", nameof(imageId));
        }
    }

    /// <summary>
    /// Validates a file extension to ensure it can be safely embedded in a filename.
    /// </summary>
    private static void ValidateImageExtension(string imageExtension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageExtension);

        if (imageExtension.Contains('/', StringComparison.Ordinal) || imageExtension.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Image extension cannot contain path separators.", nameof(imageExtension));
        }
    }
}
