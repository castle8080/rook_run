using RookRun.ObjectStore;
using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Stores Strava activity streams in the configured object store.
/// Each activity stream document is stored as: activities/strava_streams/{activity_id}.json.br
/// </summary>
public sealed class ObjectStoreStravaActivityStreamsRepository : IStravaActivityStreamsRepository
{
    private const string ActivityStreamsPrefix = "activities/strava_streams/";
    private const string FileExtension = ".json.br";

    private readonly IObjectStore _objectStore;
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreStravaActivityStreamsRepository"/> class.
    /// </summary>
    /// <param name="objectStore">The object store used for persistence.</param>
    /// <param name="prefix">Optional prefix for all paths.</param>
    public ObjectStoreStravaActivityStreamsRepository(IObjectStore objectStore, string prefix = "")
    {
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _prefix = NormalizePrefix(prefix);
    }

    /// <inheritdoc />
    public async Task<StravaActivityStreams?> GetByActivityIdAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(activityId);
        var objectValue = await _objectStore.TryReadObjectAsync<StravaActivityStreams>(path, cancellationToken: cancellationToken);
        return objectValue.IsFound ? objectValue.Value : null;
    }

    /// <inheritdoc />
    public async Task SaveAsync(StravaActivityStreams streams, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(streams);

        var path = BuildPath(streams.ActivityId);
        await _objectStore.StoreObjectAsync(path, streams, overwrite: true, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(activityId);
        var objectValue = await _objectStore.TryReadObjectAsync<StravaActivityStreams>(path, cancellationToken: cancellationToken);
        return objectValue.IsFound;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> ListActivityIdsAsync(CancellationToken cancellationToken = default)
    {
        var objects = await _objectStore.ListObjectsAsync(BuildPrefix(), cancellationToken);

        return objects
            .Select(TryGetActivityIdFromPath)
            .Where(static activityId => activityId.HasValue)
            .Select(static activityId => activityId!.Value)
            .ToList();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long activityId, CancellationToken cancellationToken = default)
    {
        var path = BuildPath(activityId);
        await _objectStore.TryDeleteObjectAsync(path, cancellationToken);
    }

    /// <summary>
    /// Builds the object store path for an activity stream document.
    /// </summary>
    private string BuildPath(long activityId)
    {
        var fileName = $"{activityId}{FileExtension}";
        return _prefix.Length == 0
            ? $"{ActivityStreamsPrefix}{fileName}"
            : $"{_prefix}{ActivityStreamsPrefix}{fileName}";
    }

    /// <summary>
    /// Builds the object store prefix used to enumerate cached stream files.
    /// </summary>
    private string BuildPrefix()
    {
        return _prefix.Length == 0
            ? ActivityStreamsPrefix
            : $"{_prefix}{ActivityStreamsPrefix}";
    }

    /// <summary>
    /// Attempts to extract an activity ID from a stream file path.
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
    /// Normalizes a prefix to ensure it ends with a slash and has no surrounding whitespace.
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
