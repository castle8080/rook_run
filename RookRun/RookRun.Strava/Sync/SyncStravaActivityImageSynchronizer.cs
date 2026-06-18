using Microsoft.Extensions.Logging;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Strava.Sync;

/// <summary>
/// Synchronizes activity images from Strava API.
/// Extracts image metadata from activity details, downloads image files, and stores them.
/// Uses concurrent downloads (3 concurrent) to efficiently fetch from CDN while respecting rate limits.
/// </summary>
public sealed class SyncStravaActivityImageSynchronizer
{
    private readonly IStravaActivityDetailClient _client;
    private readonly IStravaActivityDetailRepository _detailRepository;
    private readonly IStravaActivityImageRepository _imageRepository;
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
    /// <param name="client">Client for downloading images from Strava CDN.</param>
    /// <param name="detailRepository">Repository for reading cached activity details.</param>
    /// <param name="imageRepository">Repository for storing downloaded images.</param>
    /// <param name="delayAsync">Optional delay function for rate-limit retries.</param>
    public SyncStravaActivityImageSynchronizer(
        ILogger<SyncStravaActivityImageSynchronizer> logger,
        IStravaActivityDetailClient client,
        IStravaActivityDetailRepository detailRepository,
        IStravaActivityImageRepository imageRepository,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _detailRepository = detailRepository ?? throw new ArgumentNullException(nameof(detailRepository));
        _imageRepository = imageRepository ?? throw new ArgumentNullException(nameof(imageRepository));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>
    /// Synchronizes images for all activities with cached details.
    /// Extracts image URLs from cached activity details and downloads/stores images.
    /// </summary>
    /// <param name="activityIds">The activity IDs to sync images for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Number of images successfully downloaded and stored.</returns>
    public async Task<int> SyncAsync(IReadOnlyList<long> activityIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityIds);

        if (activityIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Starting sync of images for {Count} activities", activityIds.Count);

        var sourceActivityIds = activityIds.ToHashSet();
        var cachedImageKeys = (await _imageRepository.ListImageKeysAsync(cancellationToken))
            .Where(key => sourceActivityIds.Contains(key.ActivityId))
            .ToHashSet();
        var cachedImageCountsByActivity = cachedImageKeys
            .GroupBy(key => key.ActivityId)
            .ToDictionary(group => group.Key, group => group.Count());

        var imagesToDownload = new List<StravaActivityImage>();
        var expectedImageKeyCount = 0;

        foreach (var activityId in activityIds)
        {
            try
            {
                var detail = await _detailRepository.GetByIdAsync(activityId, cancellationToken);
                if (detail == null)
                {
                    _logger.LogDebug("Activity detail {ActivityId} not cached, skipping image extraction", activityId);
                    continue;
                }

                var expectedPhotoCount = GetExpectedPhotoCount(detail);
                var cachedImageCount = cachedImageCountsByActivity.GetValueOrDefault(activityId, 0);

                if (expectedPhotoCount > 0 && cachedImageCount >= expectedPhotoCount)
                {
                    _logger.LogDebug(
                        "Activity {ActivityId} already has {CachedCount}/{ExpectedCount} images cached, skipping photo lookup",
                        activityId,
                        cachedImageCount,
                        expectedPhotoCount);
                    continue;
                }

                IReadOnlyList<StravaActivityImage> images;
                if (expectedPhotoCount > 0)
                {
                    images = await TryGetActivityPhotosAsync(activityId, detail, cancellationToken);
                }
                else
                {
                    images = _client.ExtractActivityImages(detail);
                }

                if (images.Count == 0)
                {
                    _logger.LogDebug("Activity {ActivityId} has no images", activityId);
                    continue;
                }

                images = DistinctByImageId(images);
                expectedImageKeyCount += expectedPhotoCount > 0 ? expectedPhotoCount : images.Count;

                foreach (var image in images)
                {
                    var imageKey = new StravaActivityImageKey(image.ActivityId, image.ImageId);
                    if (!cachedImageKeys.Contains(imageKey))
                    {
                        imagesToDownload.Add(image);
                    }
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
                ct => _client.DownloadImageAsync(image.ImageUrl, ct),
                _logger,
                $"image download for {image.ImageId} on activity {image.ActivityId}",
                cancellationToken,
                _delayAsync);

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

    /// <summary>
    /// Loads activity photos via API and falls back to detail extraction if the photos endpoint fails.
    /// </summary>
    private async Task<IReadOnlyList<StravaActivityImage>> TryGetActivityPhotosAsync(
        long activityId,
        StravaActivityDetail detail,
        CancellationToken cancellationToken)
    {
        try
        {
            var activityPhotos = await _client.GetActivityPhotosAsync(activityId, cancellationToken);
            if (activityPhotos.Count > 0)
            {
                return activityPhotos;
            }

            return _client.ExtractActivityImages(detail);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load full photo list for activity {ActivityId}; falling back to detail photo extraction.",
                activityId);
            return _client.ExtractActivityImages(detail);
        }
    }

    /// <summary>
    /// Removes duplicate images by Strava image ID.
    /// </summary>
    private static IReadOnlyList<StravaActivityImage> DistinctByImageId(
        IReadOnlyList<StravaActivityImage> images)
    {
        var byId = new Dictionary<string, StravaActivityImage>(StringComparer.Ordinal);

        foreach (var image in images)
        {
            byId[image.ImageId] = image;
        }

        return byId.Values.ToList();
    }

    /// <summary>
    /// Determines the expected number of photos for an activity from detail metadata.
    /// </summary>
    private static int GetExpectedPhotoCount(StravaActivityDetail detail)
    {
        if (detail.TotalPhotoCount > 0)
        {
            return detail.TotalPhotoCount;
        }

        return detail.PhotoCount;
    }
}