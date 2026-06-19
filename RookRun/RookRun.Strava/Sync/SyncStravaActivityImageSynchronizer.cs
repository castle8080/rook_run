using System.Globalization;
using Microsoft.Extensions.Logging;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Strava.Sync;

/// <summary>
/// Synchronizes activity images from Strava API.
/// Uses the cached activity-image-id index to determine which images still need to be downloaded,
/// then resolves download metadata only for activities with missing image files.
/// Uses concurrent downloads (3 concurrent) to efficiently fetch from CDN while respecting rate limits.
/// </summary>
public class SyncStravaActivityImageSynchronizer
{
    private readonly IStravaActivityDetailClient _client;
    private readonly IStravaActivityImageRepository _imageRepository;
    private readonly IStravaActivityImageIdIndexRepository _indexRepository;
    private readonly ILogger<SyncStravaActivityImageSynchronizer> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>
    /// Number of concurrent image downloads. Images are fetched from CDN (not API),
    /// so this doesn't impact Strava rate limits.
    /// </summary>
    private const int ConcurrentDownloads = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityImageSynchronizer"/> class.
    /// </summary>
    /// <param name="logger">Logger for image sync events.</param>
    /// <param name="client">Client for resolving image metadata and downloading images from Strava CDN.</param>
    /// <param name="imageRepository">Repository for storing downloaded images.</param>
    /// <param name="indexRepository">Repository for reading the cached activity-image-id index.</param>
    /// <param name="delayAsync">Optional delay function for rate-limit retries.</param>
    public SyncStravaActivityImageSynchronizer(
        ILogger<SyncStravaActivityImageSynchronizer> logger,
        IStravaActivityDetailClient client,
        IStravaActivityImageRepository imageRepository,
        IStravaActivityImageIdIndexRepository indexRepository,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _imageRepository = imageRepository ?? throw new ArgumentNullException(nameof(imageRepository));
        _indexRepository = indexRepository ?? throw new ArgumentNullException(nameof(indexRepository));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>
    /// Synchronizes images for all activities with cached index entries.
    /// Extracts download metadata only for activities that still have missing cached images.
    /// </summary>
    /// <param name="activityIds">The activity IDs to sync images for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Number of images successfully downloaded and stored.</returns>
    public virtual async Task<int> SyncAsync(IReadOnlyList<long> activityIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityIds);

        if (activityIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Starting sync of images for {Count} activities", activityIds.Count);

        var sourceActivityIds = activityIds.ToHashSet();
        var (index, _) = await _indexRepository.LoadAsync(cancellationToken);
        var desiredImagesByActivity = BuildDesiredImagesByActivity(sourceActivityIds, index);

        if (desiredImagesByActivity.Count == 0)
        {
            _logger.LogInformation("No indexed activity image IDs were found for the requested activities");
            return 0;
        }

        var cachedImageKeys = (await _imageRepository.ListImageKeysAsync(cancellationToken))
            .Where(key => sourceActivityIds.Contains(key.ActivityId))
            .ToHashSet();

        var imagesToDownload = new List<StravaActivityImage>();
        var expectedImageKeyCount = 0;

        foreach (var (activityId, desiredImageIds) in desiredImagesByActivity)
        {
            try
            {
                var missingImageIds = desiredImageIds
                    .Where(imageId => !cachedImageKeys.Contains(new StravaActivityImageKey(activityId, imageId)))
                    .ToList();

                if (missingImageIds.Count == 0)
                {
                    _logger.LogDebug("Activity {ActivityId} already has all indexed images cached", activityId);
                    continue;
                }

                var imageMetadataById = await LoadImageMetadataByIdAsync(activityId, cancellationToken);
                expectedImageKeyCount += desiredImageIds.Count;

                foreach (var imageId in missingImageIds)
                {
                    if (!imageMetadataById.TryGetValue(imageId, out var image))
                    {
                        _logger.LogWarning(
                            "Indexed image ID {ImageId} for activity {ActivityId} could not be resolved from the photos endpoint",
                            imageId,
                            activityId);
                        continue;
                    }

                    imagesToDownload.Add(image);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting images for activity {ActivityId}", activityId);
            }
        }

        _logger.LogInformation(
            "Image sync lookup found {CachedCount} cached image keys and {MissingCount} missing images out of {ExpectedCount} expected images",
            cachedImageKeys.Count,
            imagesToDownload.Count,
            expectedImageKeyCount);

        if (imagesToDownload.Count == 0)
        {
            _logger.LogInformation("No new images to download");
            return 0;
        }

        _logger.LogInformation(
            "Downloading {ImageCount} images with {ConcurrentCount} concurrent workers",
            imagesToDownload.Count,
            ConcurrentDownloads);

        var semaphore = new SemaphoreSlim(ConcurrentDownloads, ConcurrentDownloads);
        try
        {
            var downloadTasks = imagesToDownload
                .Select(image => DownloadAndSaveImageAsync(image, semaphore, cancellationToken))
                .ToArray();

            var results = await Task.WhenAll(downloadTasks);
            var successCount = results.Count(r => r);

            _logger.LogInformation(
                "Image sync complete: {SuccessCount}/{TotalCount} images downloaded",
                successCount,
                imagesToDownload.Count);
            return successCount;
        }
        finally
        {
            semaphore.Dispose();
        }
    }

    /// <summary>
    /// Builds the desired image IDs keyed by activity ID from the cached index.
    /// </summary>
    private static IReadOnlyDictionary<long, IReadOnlyList<string>> BuildDesiredImagesByActivity(
        ISet<long> sourceActivityIds,
        StravaActivityImageIdIndex index)
    {
        var desired = new Dictionary<long, IReadOnlyList<string>>();

        foreach (var activityId in sourceActivityIds)
        {
            var activityKey = activityId.ToString(CultureInfo.InvariantCulture);
            if (!index.Items.TryGetValue(activityKey, out var imageIds) || imageIds.Count == 0)
            {
                continue;
            }

            desired[activityId] = imageIds
                .Where(imageId => !string.IsNullOrWhiteSpace(imageId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(imageId => imageId, StringComparer.Ordinal)
                .ToList();
        }

        return desired;
    }

    /// <summary>
    /// Loads downloadable image metadata for an activity.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, StravaActivityImage>> LoadImageMetadataByIdAsync(
        long activityId,
        CancellationToken cancellationToken)
    {
        try
        {
            var photos = await StravaRetryPolicy.ExecuteWithRetryAsync(
                operation: ct => _client.GetActivityPhotosAsync(activityId, ct),
                logger: _logger,
                operationName: $"activity photos lookup for {activityId}",
                cancellationToken: cancellationToken,
                delayAsync: _delayAsync);

            return photos
                .Where(image => !string.IsNullOrWhiteSpace(image.ImageId))
                .GroupBy(image => image.ImageId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }
        catch (RateLimitException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to load photos for activity {ActivityId}; indexed image IDs for that activity will be skipped.",
                activityId);
            return new Dictionary<string, StravaActivityImage>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Downloads a single image and saves it to the repository.
    /// </summary>
    private async Task<bool> DownloadAndSaveImageAsync(
        StravaActivityImage image,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var imageBytes = await StravaRetryPolicy.ExecuteWithRetryAsync(
                operation: ct => _client.DownloadImageAsync(image.ImageUrl, ct),
                logger: _logger,
                operationName: $"image download for {image.ImageId} on activity {image.ActivityId}",
                cancellationToken: cancellationToken,
                delayAsync: _delayAsync);

            if (imageBytes == null || imageBytes.Length == 0)
            {
                _logger.LogWarning("Failed to download image {ImageId} from activity {ActivityId}", image.ImageId, image.ActivityId);
                return false;
            }

            await _imageRepository.SaveImageAsync(
                image.ActivityId,
                image.ImageId,
                image.Extension,
                imageBytes,
                cancellationToken);

            _logger.LogInformation(
                "Cached image {ImageId} for activity {ActivityId} ({ByteCount} bytes)",
                image.ImageId,
                image.ActivityId,
                imageBytes.Length);

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RateLimitException ex)
        {
            _logger.LogWarning(
                ex,
                "Rate limit remained in effect after retries while downloading image {ImageId} for activity {ActivityId}. Moving on to the next image.",
                image.ImageId,
                image.ActivityId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image {ImageId} for activity {ActivityId}", image.ImageId, image.ActivityId);
            return false;
        }
        finally
        {
            semaphore.Release();
        }
    }

}