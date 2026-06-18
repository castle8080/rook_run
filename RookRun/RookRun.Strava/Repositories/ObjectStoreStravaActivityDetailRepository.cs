using RookRun.ObjectStore;
using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Stores Strava activity details in the configured object store.
/// Each activity detail is stored as a single JSON file: activities/strava_detail/{activity_id}.json.br
/// </summary>
public sealed class ObjectStoreStravaActivityDetailRepository : IStravaActivityDetailRepository
{
    private const string ActivityDetailPrefix = "activities/strava_detail/";
    private const string FileExtension = ".json.br";

    private readonly IObjectStore _objectStore;
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreStravaActivityDetailRepository"/> class.
    /// </summary>
    /// <param name="objectStore">The object store used for persistence.</param>
    /// <param name="prefix">Optional prefix for all paths (e.g., "strava/" to store under "strava/activities/strava_detail/...").</param>
    public ObjectStoreStravaActivityDetailRepository(IObjectStore objectStore, string prefix = "")
    {
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _prefix = NormalizePrefix(prefix);
    }

    /// <inheritdoc />
    public async Task<StravaActivityDetail?> GetByIdAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(activityId);
        var objectValue = await _objectStore.TryReadObjectAsync<StravaActivityDetail>(path, cancellationToken: cancellationToken);
        return objectValue.IsFound ? objectValue.Value : null;
    }

    /// <inheritdoc />
    public async Task SaveAsync(StravaActivityDetail detail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detail);
        
        var path = BuildPath(detail.Id);
        await _objectStore.StoreObjectAsync(path, detail, overwrite: true, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(activityId);
        var objectValue = await _objectStore.TryReadObjectAsync<StravaActivityDetail>(path, cancellationToken: cancellationToken);
        return objectValue.IsFound;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> ListActivityIdsAsync(CancellationToken cancellationToken = default)
    {
        var objects = await _objectStore.ListObjectsAsync(BuildPrefix(), cancellationToken);

        return objects
            .Select(TryGetActivityIdFromPath)
            .Where(activityId => activityId.HasValue)
            .Select(activityId => activityId!.Value)
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(activityId);
        await _objectStore.TryDeleteObjectAsync(path, cancellationToken);
    }

    /// <summary>
    /// Builds the object store path for an activity detail.
    /// </summary>
    private string BuildPath(long activityId)
    {
        var fileName = $"{activityId}{FileExtension}";
        return _prefix.Length == 0 
            ? $"{ActivityDetailPrefix}{fileName}" 
            : $"{_prefix}/{ActivityDetailPrefix}{fileName}";
    }

    /// <summary>
    /// Builds the object store prefix used to enumerate cached detail files.
    /// </summary>
    private string BuildPrefix()
    {
        return _prefix.Length == 0
            ? ActivityDetailPrefix
            : $"{_prefix}/{ActivityDetailPrefix}";
    }

    /// <summary>
    /// Attempts to extract an activity ID from a detail file path.
    /// </summary>
    private static long? TryGetActivityIdFromPath(string path)
    {
        var fileName = Path.GetFileName(path);
        if (!fileName.EndsWith(FileExtension, StringComparison.Ordinal))
        {
            return null;
        }

        var activityIdText = fileName[..^FileExtension.Length];
        return long.TryParse(activityIdText, out var activityId) ? activityId : null;
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
}
