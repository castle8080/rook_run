using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Client;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="SyncStravaActivityIdImageIdIndexJob"/>.
/// </summary>
public sealed class SyncStravaActivityIdImageIdIndexJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreNull()
    {
        var (synchronizer, _) = CreateSynchronizer();

        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityIdImageIdIndexJob(null!, synchronizer));
        Assert.Throws<ArgumentNullException>(() =>
            new SyncStravaActivityIdImageIdIndexJob(NullLogger<SyncStravaActivityIdImageIdIndexJob>.Instance, null!));
    }

    /// <summary>
    /// Verifies the job invokes the synchronizer once.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CallsSynchronizerOnce()
    {
        var (synchronizer, synchronizerMock) = CreateSynchronizer();
        synchronizerMock.Setup(s => s.SyncAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var job = new SyncStravaActivityIdImageIdIndexJob(
            NullLogger<SyncStravaActivityIdImageIdIndexJob>.Instance,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        synchronizerMock.Verify(s => s.SyncAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies cancellation is rethrown.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RethrowsOperationCanceledException()
    {
        var (synchronizer, synchronizerMock) = CreateSynchronizer();
        synchronizerMock.Setup(s => s.SyncAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());

        var job = new SyncStravaActivityIdImageIdIndexJob(
            NullLogger<SyncStravaActivityIdImageIdIndexJob>.Instance,
            synchronizer);

        await Assert.ThrowsAsync<OperationCanceledException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    /// <summary>
    /// Verifies unexpected exceptions are rethrown.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RethrowsUnexpectedException()
    {
        var (synchronizer, synchronizerMock) = CreateSynchronizer();
        synchronizerMock.Setup(s => s.SyncAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("storage failure"));

        var job = new SyncStravaActivityIdImageIdIndexJob(
            NullLogger<SyncStravaActivityIdImageIdIndexJob>.Instance,
            synchronizer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    private static (SyncStravaActivityIdImageIdIndexSynchronizer Synchronizer, Mock<SyncStravaActivityIdImageIdIndexSynchronizer> Mock) CreateSynchronizer()
    {
        var synchronizerMock = new Mock<SyncStravaActivityIdImageIdIndexSynchronizer>(
            NullLogger<SyncStravaActivityIdImageIdIndexSynchronizer>.Instance,
            new Mock<IStravaActivityDetailClient>().Object,
            new Mock<IStravaActivityDetailRepository>().Object,
            new Mock<IStravaActivityImageIdIndexRepository>().Object,
            null!)
        {
            CallBase = true
        };

        return (synchronizerMock.Object, synchronizerMock);
    }
}