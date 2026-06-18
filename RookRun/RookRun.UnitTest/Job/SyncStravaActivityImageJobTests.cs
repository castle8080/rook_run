using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RookRun.Job;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
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
        var synchronizer = CreateSynchronizer();

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

        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        detailRepository.Setup(r => r.GetByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 10, Name = "Activity 10" });
        detailRepository.Setup(r => r.GetByIdAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 20, Name = "Activity 20" });
        detailRepository.Setup(r => r.GetByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 30, Name = "Activity 30" });

        var client = new Mock<IStravaActivityDetailClient>();
        client.Setup(c => c.ExtractActivityImages(It.Is<StravaActivityDetail>(d => d.Id == 10)))
            .Returns([new StravaActivityImage { ActivityId = 10, ImageId = "img-10", ImageUrl = "https://example.com/10.jpg", Extension = "jpg" }]);
        client.Setup(c => c.ExtractActivityImages(It.Is<StravaActivityDetail>(d => d.Id == 20)))
            .Returns([new StravaActivityImage { ActivityId = 20, ImageId = "img-20", ImageUrl = "https://example.com/20.jpg", Extension = "jpg" }]);
        client.Setup(c => c.ExtractActivityImages(It.Is<StravaActivityDetail>(d => d.Id == 30)))
            .Returns([new StravaActivityImage { ActivityId = 30, ImageId = "img-30", ImageUrl = "https://example.com/30.jpg", Extension = "jpg" }]);

        client.Setup(c => c.DownloadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x01 });

        var imageRepository = new Mock<IStravaActivityImageRepository>();
        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImageKey(30, "img-30")]);
        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            NullLogger<SyncStravaActivityImageSynchronizer>.Instance,
            client.Object,
            detailRepository.Object,
            imageRepository.Object);

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        activitiesRepository.Verify(r => r.ListActivityIdsAsync(
            It.IsAny<DateTimeOffset>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);

        client.Verify(c => c.DownloadImageAsync("https://example.com/10.jpg", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.DownloadImageAsync("https://example.com/20.jpg", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.DownloadImageAsync("https://example.com/30.jpg", It.IsAny<CancellationToken>()), Times.Never);
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

        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            NullLogger<SyncStravaActivityImageSynchronizer>.Instance,
            client.Object,
            detailRepository.Object,
            imageRepository.Object);

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await job.ExecuteAsync(CancellationToken.None);

        detailRepository.Verify(r => r.GetByIdAsync(
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.DownloadImageAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var synchronizer = CreateSynchronizer();

        var job = new SyncStravaActivityImageJob(
            NullLogger<SyncStravaActivityImageJob>.Instance,
            activitiesRepository.Object,
            synchronizer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));
    }

    private static SyncStravaActivityImageSynchronizer CreateSynchronizer()
    {
        return new SyncStravaActivityImageSynchronizer(
            NullLogger<SyncStravaActivityImageSynchronizer>.Instance,
            new Mock<IStravaActivityDetailClient>().Object,
            new Mock<IStravaActivityDetailRepository>().Object,
            new Mock<IStravaActivityImageRepository>().Object);
    }
}
