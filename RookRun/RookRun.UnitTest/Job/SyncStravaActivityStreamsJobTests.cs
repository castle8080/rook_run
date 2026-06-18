using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="SyncStravaActivityStreamsJob"/>.
/// </summary>
public sealed class SyncStravaActivityStreamsJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreNull()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        var synchronizer = CreateSynchronizer();

        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityStreamsJob(null!, activitiesRepository.Object, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityStreamsJob(NullLogger<SyncStravaActivityStreamsJob>.Instance, null!, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityStreamsJob(NullLogger<SyncStravaActivityStreamsJob>.Instance, activitiesRepository.Object, null!));
    }

    /// <summary>
    /// Verifies the job requests source IDs and syncs streams in one pass.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LoadsAllSourceIdsAndRunsSynchronizer()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        activitiesRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([10L, 20L, 30L]);

        var streamsRepository = new Mock<IStravaActivityStreamsRepository>();
        streamsRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([30L]);

        var client = new Mock<IStravaActivityStreamsClient>();
        client.Setup(c => c.GetActivityStreamsAsync(10, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStreams(10));
        client.Setup(c => c.GetActivityStreamsAsync(20, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateStreams(20));

        streamsRepository
            .Setup(r => r.SaveAsync(It.IsAny<StravaActivityStreams>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(
            NullLogger<SyncStravaActivityStreamsSynchronizer>.Instance,
            client.Object,
            streamsRepository.Object);

        var job = new SyncStravaActivityStreamsJob(
            NullLogger<SyncStravaActivityStreamsJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        activitiesRepository.Verify(r => r.ListActivityIdsAsync(
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);

        client.Verify(c => c.GetActivityStreamsAsync(10, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetActivityStreamsAsync(20, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetActivityStreamsAsync(30, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies the job does not call the synchronizer when no activities are found.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DoesNotCallSynchronizerWhenNoActivities()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        activitiesRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<long>());

        var streamsClient = new Mock<IStravaActivityStreamsClient>();
        var streamsRepository = new Mock<IStravaActivityStreamsRepository>();

        var synchronizer = new SyncStravaActivityStreamsSynchronizer(
            NullLogger<SyncStravaActivityStreamsSynchronizer>.Instance,
            streamsClient.Object,
            streamsRepository.Object);

        var job = new SyncStravaActivityStreamsJob(
            NullLogger<SyncStravaActivityStreamsJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        streamsClient.Verify(c => c.GetActivityStreamsAsync(
            It.IsAny<long>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies the job rethrows OperationCanceledException.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RethrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        activitiesRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var synchronizer = CreateSynchronizer();

        var job = new SyncStravaActivityStreamsJob(
            NullLogger<SyncStravaActivityStreamsJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await Assert.ThrowsAsync<OperationCanceledException>(() => job.ExecuteAsync(cts.Token));
    }

    /// <summary>
    /// Verifies the job rethrows unexpected exceptions.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RethrowsUnexpectedException()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        activitiesRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage failure"));

        var synchronizer = CreateSynchronizer();

        var job = new SyncStravaActivityStreamsJob(
            NullLogger<SyncStravaActivityStreamsJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    /// <summary>
    /// Creates a synchronizer with no-op dependencies for constructor tests.
    /// </summary>
    private static SyncStravaActivityStreamsSynchronizer CreateSynchronizer()
    {
        return new SyncStravaActivityStreamsSynchronizer(
            NullLogger<SyncStravaActivityStreamsSynchronizer>.Instance,
            new Mock<IStravaActivityStreamsClient>().Object,
            new Mock<IStravaActivityStreamsRepository>().Object);
    }

    /// <summary>
    /// Creates deterministic stream test data.
    /// </summary>
    private static StravaActivityStreams CreateStreams(long activityId)
    {
        return new StravaActivityStreams
        {
            ActivityId = activityId,
            FetchedUtc = DateTimeOffset.UtcNow,
            RequestedKeys = StravaStreamKeys.DefaultPhase1,
            Streams = new Dictionary<string, StravaStreamData>(StringComparer.Ordinal)
            {
                [StravaStreamKeys.Time] = new()
                {
                    Type = StravaStreamKeys.Time,
                    SeriesType = "time",
                    Resolution = "high",
                    OriginalSize = 2,
                    Data = System.Text.Json.JsonSerializer.SerializeToElement(new[] { 0, 1 })
                }
            }
        };
    }
}
