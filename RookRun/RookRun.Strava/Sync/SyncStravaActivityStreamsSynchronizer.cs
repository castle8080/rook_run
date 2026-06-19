using Microsoft.Extensions.Logging;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.Strava.Sync;

/// <summary>
/// Synchronizes Strava activity streams for activities that are not yet cached.
/// </summary>
public sealed class SyncStravaActivityStreamsSynchronizer
{
    private readonly IStravaActivityStreamsClient _client;
    private readonly IStravaActivityStreamsRepository _repository;
    private readonly ILogger<SyncStravaActivityStreamsSynchronizer> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStravaActivityStreamsSynchronizer"/> class.
    /// </summary>
    /// <param name="logger">Logger for stream sync events.</param>
    /// <param name="client">Client for fetching activity streams from Strava.</param>
    /// <param name="repository">Repository for storing fetched stream documents.</param>
    /// <param name="delayAsync">Optional delay function used by retry policy tests.</param>
    public SyncStravaActivityStreamsSynchronizer(
        ILogger<SyncStravaActivityStreamsSynchronizer> logger,
        IStravaActivityStreamsClient client,
        IStravaActivityStreamsRepository repository,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    /// <summary>
    /// Synchronizes streams for activities that do not already have cached stream documents.
    /// </summary>
    /// <param name="activityIds">The activity IDs to evaluate for stream sync.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Number of activity stream documents successfully cached.</returns>
    public async Task<int> SyncAsync(IReadOnlyList<long> activityIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activityIds);

        if (activityIds.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Starting sync of activity streams for {Count} activities", activityIds.Count);

        var cachedIds = (await _repository.ListActivityIdsAsync(cancellationToken)).ToHashSet();
        var activityIdsToSync = activityIds
            .Where(static id => id > 0)
            .Distinct()
            .Where(activityId => !cachedIds.Contains(activityId))
            .ToList();

        _logger.LogInformation(
            "Stream sync lookup found {CachedCount} cached stream documents and {NewCount} new stream documents to retrieve out of {TotalCount} activities",
            cachedIds.Count,
            activityIdsToSync.Count,
            activityIds.Count);

        if (activityIdsToSync.Count == 0)
        {
            _logger.LogInformation("No new activity streams to sync");
            return 0;
        }

        var syncedCount = 0;

        foreach (var activityId in activityIdsToSync)
        {
            try
            {
                var streams = await StravaRetryPolicy.ExecuteWithRetryAsync(
                    operation: ct => _client.GetActivityStreamsAsync(activityId, StravaStreamKeys.DefaultPhase1, ct),
                    logger: _logger,
                    operationName: $"activity streams fetch for {activityId}",
                    cancellationToken: cancellationToken,
                    delayAsync: _delayAsync);

                if (streams is null)
                {
                    _logger.LogWarning("Activity streams not found for activity {ActivityId}", activityId);
                    continue;
                }

                // Missing-only policy still caches successful empty/partial stream payloads.
                await _repository.SaveAsync(streams, cancellationToken);
                syncedCount++;

                _logger.LogInformation(
                    "Cached streams for activity {ActivityId} with {StreamCount} stream types",
                    activityId,
                    streams.Streams.Count);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Activity stream sync cancelled while processing activity {ActivityId}. TokenCancellationRequested={TokenCancellationRequested}",
                    activityId,
                    cancellationToken.IsCancellationRequested);
                throw;
            }
            catch (RateLimitException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Rate limit remained in effect after retries while fetching streams for activity {ActivityId}. Moving on to the next activity.",
                    activityId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching streams for activity {ActivityId}", activityId);
            }
        }

        _logger.LogInformation(
            "Activity stream sync complete: {SyncedCount}/{TotalCount} synced",
            syncedCount,
            activityIdsToSync.Count);

        return syncedCount;
    }
}
