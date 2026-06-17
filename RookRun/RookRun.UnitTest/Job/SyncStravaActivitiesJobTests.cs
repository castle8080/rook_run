using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="SyncStravaActivitiesJob"/>.
/// </summary>
public sealed class SyncStravaActivitiesJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreNull()
    {
        var synchronizer = CreateSynchronizer();

        Assert.Throws<ArgumentNullException>(() => new SyncStravaActivitiesJob(null!, synchronizer));
        Assert.Throws<ArgumentNullException>(() => new SyncStravaActivitiesJob(NullLogger<SyncStravaActivitiesJob>.Instance, null!));
    }

    /// <summary>
    /// Verifies the job executes the synchronizer workflow and persists newly discovered activities.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RunsSynchronizationWorkflow()
    {
        var activity = new StravaActivity
        {
            Id = 42,
            StartDate = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var repository = new Mock<IStravaActivitiesRepository>(MockBehavior.Strict);
        repository
            .Setup(r => r.SearchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());
        repository
            .Setup(r => r.SaveAllAsync(It.Is<IReadOnlyList<StravaActivity>>(items => items.Count == 1 && items[0].Id == 42), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var client = new Mock<IStravaActivitiesClient>(MockBehavior.Strict);
        client
            .Setup(c => c.SearchActivitiesAsync(It.Is<StravaActivityQuery>(q => q.Page == 1 && q.PerPage == 200), It.IsAny<CancellationToken>()))
            .ReturnsAsync([activity]);
        client
            .Setup(c => c.SearchActivitiesAsync(It.Is<StravaActivityQuery>(q => q.Page == 2 && q.PerPage == 200), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());

        var synchronizer = new StravaActivitiesSynchronizer(
            NullLogger<StravaActivitiesSynchronizer>.Instance,
            repository.Object,
            client.Object);

        var job = new SyncStravaActivitiesJob(NullLogger<SyncStravaActivitiesJob>.Instance, synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        repository.VerifyAll();
        client.VerifyAll();
    }

    /// <summary>
    /// Creates a synchronizer with no-op dependencies for constructor validation tests.
    /// </summary>
    private static StravaActivitiesSynchronizer CreateSynchronizer()
    {
        var repository = new Mock<IStravaActivitiesRepository>();
        repository
            .Setup(r => r.SearchAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());

        var client = new Mock<IStravaActivitiesClient>();
        client
            .Setup(c => c.SearchActivitiesAsync(It.IsAny<StravaActivityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivity>());

        return new StravaActivitiesSynchronizer(NullLogger<StravaActivitiesSynchronizer>.Instance, repository.Object, client.Object);
    }
}
