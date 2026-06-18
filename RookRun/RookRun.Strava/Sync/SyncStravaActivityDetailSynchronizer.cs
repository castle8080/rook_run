using Microsoft.Extensions.Logging;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Strava.Sync;

/// <summary>
/// Synchronizes detailed activity information from Strava API.
/// Fetches full activity details (segment efforts, splits, laps, etc.) for specified activities.
/// Implements rate limiting to respect Strava API rate limits (200 req/15min, 2000 req/day).
/// </summary>
public sealed class SyncStravaActivityDetailSynchronizer
{
    private readonly IStravaActivityDetailClient _client;
    private readonly IStravaActivityDetailRepository _repository;
    private readonly ILogger<SyncStravaActivityDetailSynchronizer> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>
    /// Throttle delay between individual detail requests (500ms = ~12 requests/min, safe margin).
    /// </summary>
    private const int ThrottleDelayMs = 500;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityDetailSynchronizer"/> class.
    /// </summary>
    /// <param name="logger">Logger for activity sync events.</param>
    /// <param name="client">Client for fetching activity details from Strava API.</param>
    /// <param name="repository">Repository for storing activity details.</param>
    public SyncStravaActivityDetailSynchronizer(
        ILogger<SyncStravaActivityDetailSynchronizer> logger,
        IStravaActivityDetailClient client,
        IStravaActivityDetailRepository repository,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>
    /// Synchronizes activity details for all specified activity IDs.
    /// </summary>
    /// <param name="activityIds">The activity IDs to fetch details for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Number of activities successfully synced.</returns>
    public async Task<int> SyncAsync(IReadOnlyList<long> activityIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityIds);

        if (activityIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Starting sync of {Count} activity details", activityIds.Count);

        var cachedDetailIds = (await _repository.ListActivityIdsAsync(cancellationToken)).ToHashSet();
        var activityIdsToSync = activityIds
            .Where(activityId => !cachedDetailIds.Contains(activityId))
            .ToList();

        _logger.LogInformation(
            "Detail sync lookup found {CachedCount} cached activity details and {NewCount} new activity details to retrieve out of {TotalCount} activities",
            cachedDetailIds.Count,
            activityIdsToSync.Count,
            activityIds.Count);

        if (activityIdsToSync.Count == 0)
        {
            _logger.LogInformation("No new activity details to sync");
            return 0;
        }

        _logger.LogInformation(
            "Found {NewCount} new activity details to sync out of {TotalCount} activities",
            activityIdsToSync.Count,
            activityIds.Count);

        int syncedCount = 0;

        foreach (var activityId in activityIdsToSync)
        {
            var fetched = false;
            try
            {
                // Fetch detail with include_all_efforts=true to get complete segment data
                fetched = true;
                var detail = await StravaRetryPolicy.ExecuteWithRetryAsync(
                    ct => _client.GetActivityDetailAsync(
                        activityId,
                        includeAllEfforts: true,
                        cancellationToken: ct),
                    _logger,
                    $"activity detail fetch for {activityId}",
                    cancellationToken,
                    _delayAsync);

                if (detail == null)
                {
                    _logger.LogWarning("Activity {ActivityId} not found or access denied", activityId);
                    continue;
                }

                // Save to repository
                await _repository.SaveAsync(detail, cancellationToken);
                syncedCount++;

                _logger.LogInformation("Cached activity detail {ActivityId}", activityId);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Activity detail sync cancelled while processing activity {ActivityId}. TokenCancellationRequested={TokenCancellationRequested}",
                    activityId,
                    cancellationToken.IsCancellationRequested);
                throw;
            }
            catch (RateLimitException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Rate limit remained in effect after retries while fetching activity detail {ActivityId}. Moving on to the next activity.",
                    activityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activity detail {ActivityId}", activityId);
                // Continue with next activity instead of failing
            }
            finally
            {
                // Throttle after every API call (including 404s and errors) to respect Strava rate limits.
                // Use CancellationToken.None so a cancellation doesn't replace the rethrown OperationCanceledException.
                if (fetched)
                {
                    await Task.Delay(ThrottleDelayMs, CancellationToken.None);
                }
            }
        }

        _logger.LogInformation("Activity detail sync complete: {SyncedCount}/{TotalCount} synced", syncedCount, activityIds.Count);
        return syncedCount;
    }
}
