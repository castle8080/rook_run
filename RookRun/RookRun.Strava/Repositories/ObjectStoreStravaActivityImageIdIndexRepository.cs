using RookRun.ObjectStore;
using RookRun.Strava.Models;

namespace RookRun.Strava.Repositories;

/// <summary>
/// Stores the activity-to-image-id index in the configured object store.
/// The index is stored as a single compressed JSON document at activities/strava_views/activity_id_image_ids.json.br.
/// </summary>
public sealed class ObjectStoreStravaActivityImageIdIndexRepository : IStravaActivityImageIdIndexRepository
{
    private const string IndexPath = "activities/strava_views/activity_id_image_ids.json.br";

    private readonly IObjectStore _objectStore;
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectStoreStravaActivityImageIdIndexRepository"/> class.
    /// </summary>
    /// <param name="objectStore">The backing object store.</param>
    /// <param name="prefix">Optional path prefix for multi-tenant storage.</param>
    public ObjectStoreStravaActivityImageIdIndexRepository(IObjectStore objectStore, string prefix = "")
    {
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _prefix = NormalizePrefix(prefix);
    }

    /// <inheritdoc />
    public async Task<(StravaActivityImageIdIndex Index, string? ETag)> LoadAsync(CancellationToken cancellationToken = default)
    {
        var readResult = await _objectStore.TryReadObjectAsync<StravaActivityImageIdIndex>(BuildPath(), cancellationToken: cancellationToken);
        if (!readResult.IsFound || readResult.Value == null)
        {
            return (new StravaActivityImageIdIndex(), null);
        }

        return (Normalize(readResult.Value), readResult.ETag);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        StravaActivityImageIdIndex index,
        string? ifMatchETag,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(index);

        await _objectStore.StoreObjectAsync(BuildPath(), Normalize(index), overwrite: true, ifMatchETag: ifMatchETag, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Builds the object-store path for the index.
    /// </summary>
    private string BuildPath() => _prefix.Length == 0 ? IndexPath : $"{_prefix}{IndexPath}";

    /// <summary>
    /// Normalizes the index into a stable, deterministic shape for persistence.
    /// </summary>
    private static StravaActivityImageIdIndex Normalize(StravaActivityImageIdIndex index)
    {
        var items = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var entry in index.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            var normalizedValues = entry.Value
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();

            items[entry.Key] = normalizedValues;
        }

        return index with
        {
            Items = items
        };
    }

    /// <summary>
    /// Normalizes a prefix to ensure it ends with a slash.
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