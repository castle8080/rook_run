using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="SyncStravaActivityDetailJob"/>.
/// </summary>
public sealed class SyncStravaActivityDetailJobTests
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
            new SyncStravaActivityDetailJob(null!, activitiesRepository.Object, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityDetailJob(NullLogger<SyncStravaActivityDetailJob>.Instance, null!, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityDetailJob(NullLogger<SyncStravaActivityDetailJob>.Instance, activitiesRepository.Object, null!));
    }

    /// <summary>
    /// Verifies the job requests full source IDs and syncs them in one pass.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LoadsAllSourceIdsAndRunsSynchronizer()
    {
        var activitiesRepository = new Mock<IStravaActivitiesRepository>();
        activitiesRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([10L, 20L, 30L]);

        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        detailRepository
            .Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([30L]);

        var client = new Mock<IStravaActivityDetailClient>();
        client.Setup(c => c.GetActivityDetailAsync(10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 10, Name = "Activity 10" });
        client.Setup(c => c.GetActivityDetailAsync(20, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 20, Name = "Activity 20" });

        detailRepository
            .Setup(r => r.SaveAsync(It.IsAny<StravaActivityDetail>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityDetailSynchronizer(
            NullLogger<SyncStravaActivityDetailSynchronizer>.Instance,
            client.Object,
            detailRepository.Object);

        var job = new SyncStravaActivityDetailJob(
            NullLogger<SyncStravaActivityDetailJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        activitiesRepository.Verify(r => r.ListActivityIdsAsync(
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);

        client.Verify(c => c.GetActivityDetailAsync(10, true, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetActivityDetailAsync(20, true, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetActivityDetailAsync(30, true, It.IsAny<CancellationToken>()), Times.Never);
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

        var detailClient = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();

        var synchronizer = new SyncStravaActivityDetailSynchronizer(
            NullLogger<SyncStravaActivityDetailSynchronizer>.Instance,
            detailClient.Object,
            detailRepository.Object);

        var job = new SyncStravaActivityDetailJob(
            NullLogger<SyncStravaActivityDetailJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        detailClient.Verify(c => c.GetActivityDetailAsync(
            It.IsAny<long>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var job = new SyncStravaActivityDetailJob(
            NullLogger<SyncStravaActivityDetailJob>.Instance,
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

        var job = new SyncStravaActivityDetailJob(
            NullLogger<SyncStravaActivityDetailJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    private static SyncStravaActivityDetailSynchronizer CreateSynchronizer()
    {
        return new SyncStravaActivityDetailSynchronizer(
            NullLogger<SyncStravaActivityDetailSynchronizer>.Instance,
            new Mock<IStravaActivityDetailClient>().Object,
            new Mock<IStravaActivityDetailRepository>().Object);
    }
}
