using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Client;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="SyncStravaActivityImageJob"/>.
/// </summary>
public sealed class SyncStravaActivityImageJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreNull()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        var (synchronizer, _) = CreateSynchronizer();

        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityImageJob(null!, activitiesRepository.Object, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityImageJob(NullLogger<SyncStravaActivityImageJob>.Instance, null!, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityImageJob(NullLogger<SyncStravaActivityImageJob>.Instance, activitiesRepository.Object, null!));
    }

    /// <summary>
    /// Verifies the job requests full source IDs and runs synchronizer once.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LoadsAllSourceIdsAndRunsSynchronizer()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        activitiesRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([10L, 20L, 30L]);

        var (synchronizer, synchronizerMock) = CreateSynchronizer();
        synchronizerMock.Setup(s => s.SyncAsync(It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        activitiesRepository.Verify(r => r.ListActivityIdsAsync(
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);

        synchronizerMock.Verify(s => s.SyncAsync(
            It.Is<IReadOnlyList<long>>(ids => ids.SequenceEqual(new[] { 10L, 20L, 30L })),
            It.IsAny<CancellationToken>()), Times.Once);
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

        var (synchronizer, synchronizerMock) = CreateSynchronizer();

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        synchronizerMock.Verify(s => s.SyncAsync(
            It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var (synchronizer, _) = CreateSynchronizer();

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
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

        var (synchronizer, _) = CreateSynchronizer();

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    private static (SyncStravaActivityImageSynchronizer, Mock<SyncStravaActivityImageSynchronizer>) CreateSynchronizer()
    {
        var synchronizerMock = new Mock<SyncStravaActivityImageSynchronizer>(
            NullLogger<SyncStravaActivityImageSynchronizer>.Instance,
            new Mock<IStravaActivityDetailClient>().Object,
            new Mock<IStravaActivityImageRepository>().Object,
            new Mock<IStravaActivityImageIdIndexRepository>().Object,
            null!)
        {
            CallBase = true
        };

        return (synchronizerMock.Object, synchronizerMock);
    }
}
