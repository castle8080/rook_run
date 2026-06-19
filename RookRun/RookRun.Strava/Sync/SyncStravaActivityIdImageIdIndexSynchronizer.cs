using System.Globalization;
using Microsoft.Extensions.Logging;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Strava.Sync;

/// <summary>
/// Builds and maintains the cached activity-to-image-id index from cached Strava activity details.
/// </summary>
public class SyncStravaActivityIdImageIdIndexSynchronizer
{
    private const int BatchSize = 50;

    private readonly IStravaActivityDetailClient _client;
    private readonly IStravaActivityDetailRepository _detailRepository;
    private readonly IStravaActivityImageIdIndexRepository _indexRepository;
    private readonly ILogger<SyncStravaActivityIdImageIdIndexSynchronizer> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityIdImageIdIndexSynchronizer"/> class.
    /// </summary>
    /// <param name="logger">Logger for sync events.</param>
    /// <param name="client">Client for reading activity photos.</param>
    /// <param name="detailRepository">Repository for cached activity details.</param>
    /// <param name="indexRepository">Repository for the activity-to-image-id index.</param>
    /// <param name="delayAsync">Optional delay hook for retry tests.</param>
    public SyncStravaActivityIdImageIdIndexSynchronizer(
        ILogger<SyncStravaActivityIdImageIdIndexSynchronizer> logger,
        IStravaActivityDetailClient client,
        IStravaActivityDetailRepository detailRepository,
        IStravaActivityImageIdIndexRepository indexRepository,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _detailRepository = detailRepository ?? throw new ArgumentNullException(nameof(detailRepository));
        _indexRepository = indexRepository ?? throw new ArgumentNullException(nameof(indexRepository));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>
    /// Synchronizes missing activity entries into the cached index.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of newly indexed activities.</returns>
    public virtual async Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Strava activity image-id index sync");

        var (currentIndex, currentETag) = await _indexRepository.LoadAsync(cancellationToken);
        var cachedActivityIds = await _detailRepository.ListActivityIdsAsync(cancellationToken);
        var missingActivityIds = cachedActivityIds
            .Where(activityId => !currentIndex.Items.ContainsKey(ActivityKey(activityId)))
            .ToList();
        var alreadyIndexedCount = cachedActivityIds.Count - missingActivityIds.Count;

        if (missingActivityIds.Count == 0)
        {
            _logger.LogInformation("No new activity image-id index entries to sync");
            return 0;
        }

        _logger.LogInformation(
            "Found {MissingCount} activities missing from the image-id index out of {TotalCount} cached details",
            missingActivityIds.Count,
            cachedActivityIds.Count);
        _logger.LogInformation(
            "Activities with image IDs already indexed: {AlreadyIndexedCount}",
            alreadyIndexedCount);

        var pendingEntries = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var processedSinceCheckpoint = 0;
        var indexedCount = 0;
        var photosApiLookupCount = 0;
        Exception? loopException = null;

        foreach (var activityId in missingActivityIds)
        {
            try
            {
                var detail = await _detailRepository.GetByIdAsync(activityId, cancellationToken);
                if (detail == null)
                {
                    _logger.LogDebug("Activity detail {ActivityId} not cached, skipping image-id index entry", activityId);
                    continue;
                }

                if (detail.TotalPhotoCount > 1)
                {
                    photosApiLookupCount++;
                }

                var imageIds = await ResolveImageIdsAsync(
                    activityId,
                    detail,
                    async ct =>
                    {
                        if (pendingEntries.Count == 0)
                        {
                            return;
                        }

                        try
                        {
                            (currentIndex, currentETag) = await TryFlushAsync(
                                currentIndex,
                                currentETag,
                                pendingEntries,
                                loopException != null,
                                ct);
                            pendingEntries.Clear();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to flush pending index entries before retrying a rate-limited photo lookup.");
                        }
                    },
                    cancellationToken);
                pendingEntries[ActivityKey(activityId)] = imageIds;
                indexedCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                loopException ??= ex;
                _logger.LogError(ex, "Error indexing images for activity {ActivityId}", activityId);
            }

            processedSinceCheckpoint++;
            if (processedSinceCheckpoint % BatchSize == 0 && pendingEntries.Count > 0)
            {
                (currentIndex, currentETag) = await TryFlushAsync(
                    currentIndex,
                    currentETag,
                    pendingEntries,
                    loopException != null,
                    cancellationToken);
                pendingEntries.Clear();
            }
        }

        if (pendingEntries.Count > 0)
        {
            (currentIndex, currentETag) = await TryFlushAsync(
                currentIndex,
                currentETag,
                pendingEntries,
                loopException != null,
                cancellationToken);
            pendingEntries.Clear();
        }

        if (loopException != null)
        {
            throw loopException;
        }

        _logger.LogInformation(
            "Activities requiring photos API lookup: {PhotosApiLookupCount}",
            photosApiLookupCount);
        _logger.LogInformation("Completed Strava activity image-id index sync. Indexed {IndexedCount} activities", indexedCount);
        return indexedCount;
    }

    /// <summary>
    /// Resolves the image IDs for an activity using the photos endpoint and falling back to cached detail extraction.
    /// </summary>
    private async Task<List<string>> ResolveImageIdsAsync(
        long activityId,
        StravaActivityDetail detail,
        Func<CancellationToken, Task>? beforeRetryDelayAsync,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StravaActivityImage> images;

        if (detail.TotalPhotoCount <= 1)
        {
            images = _client.ExtractActivityImages(detail) ?? Array.Empty<StravaActivityImage>();

            return images
                .Select(image => image.ImageId)
                .Where(imageId => !string.IsNullOrWhiteSpace(imageId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(imageId => imageId, StringComparer.Ordinal)
                .ToList();
        }

        try
        {
            images = await StravaRetryPolicy.ExecuteWithRetryAsync(
                operation: ct => _client.GetActivityPhotosAsync(activityId, ct),
                logger: _logger,
                operationName: $"activity photo lookup for {activityId}",
                cancellationToken: cancellationToken,
                beforeDelayAsync: beforeRetryDelayAsync,
                delayAsync: _delayAsync);

            if (images.Count == 0)
            {
                images = _client.ExtractActivityImages(detail) ?? Array.Empty<StravaActivityImage>();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load activity photos for {ActivityId}; falling back to cached detail extraction.",
                activityId);
            images = _client.ExtractActivityImages(detail) ?? Array.Empty<StravaActivityImage>();
        }

        return images
            .Select(image => image.ImageId)
            .Where(imageId => !string.IsNullOrWhiteSpace(imageId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(imageId => imageId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Attempts to flush pending index entries, retrying once against the latest store state on precondition failure.
    /// </summary>
    private async Task<(StravaActivityImageIdIndex Index, string? ETag)> TryFlushAsync(
        StravaActivityImageIdIndex currentIndex,
        string? currentETag,
        Dictionary<string, List<string>> pendingEntries,
        bool suppressFailure,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SavePendingAsync(currentIndex, currentETag, pendingEntries, cancellationToken);
        }
        catch (PreconditionFailedException ex)
        {
            _logger.LogWarning(ex, "Image-id index save conflicted; reloading and retrying the merge.");

            var (latestIndex, latestETag) = await _indexRepository.LoadAsync(cancellationToken);
            var merged = MergeMissingOnly(latestIndex, pendingEntries);

            try
            {
                return await SavePendingAsync(merged, latestETag, pendingEntries, cancellationToken);
            }
            catch (Exception retryException) when (suppressFailure)
            {
                _logger.LogWarning(retryException, "Image-id index save failed after retry, but a loop error already occurred.");
                return (latestIndex, latestETag);
            }
        }
    }

    /// <summary>
    /// Saves the merged index using the current ETag and reloads the store state after success.
    /// </summary>
    private async Task<(StravaActivityImageIdIndex Index, string? ETag)> SavePendingAsync(
        StravaActivityImageIdIndex currentIndex,
        string? currentETag,
        IReadOnlyDictionary<string, List<string>> pendingEntries,
        CancellationToken cancellationToken)
    {
        var mergedIndex = MergeMissingOnly(currentIndex, pendingEntries) with
        {
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        await _indexRepository.SaveAsync(mergedIndex, currentETag, cancellationToken);

        var reloaded = await _indexRepository.LoadAsync(cancellationToken);
        return (reloaded.Index, reloaded.ETag);
    }

    /// <summary>
    /// Merges pending additions into an existing index without overwriting concurrent entries.
    /// </summary>
    private static StravaActivityImageIdIndex MergeMissingOnly(
        StravaActivityImageIdIndex baseIndex,
        IReadOnlyDictionary<string, List<string>> pendingEntries)
    {
        var items = baseIndex.Items.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList(),
            StringComparer.Ordinal);

        foreach (var pendingEntry in pendingEntries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!items.ContainsKey(pendingEntry.Key))
            {
                items[pendingEntry.Key] = pendingEntry.Value
                    .Where(imageId => !string.IsNullOrWhiteSpace(imageId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(imageId => imageId, StringComparer.Ordinal)
                    .ToList();
            }
        }

        return baseIndex with
        {
            Items = items
        };
    }

    /// <summary>
    /// Builds the stable string key for a cached activity.
    /// </summary>
    private static string ActivityKey(long activityId) => activityId.ToString(CultureInfo.InvariantCulture);
}