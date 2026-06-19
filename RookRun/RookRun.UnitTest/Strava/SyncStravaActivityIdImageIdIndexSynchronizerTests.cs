using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;
using System.Net;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="SyncStravaActivityIdImageIdIndexSynchronizer"/>.
/// </summary>
public sealed class SyncStravaActivityIdImageIdIndexSynchronizerTests
{
    /// <summary>
    /// Verifies the synchronizer indexes only missing activity IDs.
    /// </summary>
    [Fact]
    public async Task SyncAsync_IndexesOnlyMissingActivityIds()
    {
        var logger = new Mock<ILogger<SyncStravaActivityIdImageIdIndexSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        detailRepository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([123L, 456L]);
        detailRepository.Setup(r => r.GetByIdAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 456, Name = "Activity 456", TotalPhotoCount = 2 });
        client.Setup(c => c.GetActivityPhotosAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StravaActivityImage { ActivityId = 456, ImageId = "img-b", ImageUrl = "https://example.com/b.jpg", Extension = "jpg" },
                new StravaActivityImage { ActivityId = 456, ImageId = "img-a", ImageUrl = "https://example.com/a.jpg", Extension = "jpg" }
            ]);

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["123"] = ["img-existing"]
                }
            }, "v:1"));
        indexRepository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityIdImageIdIndexSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            indexRepository.Object);

        var result = await synchronizer.SyncAsync();

        Assert.Equal(1, result);
        indexRepository.Verify(r => r.SaveAsync(
            It.Is<StravaActivityImageIdIndex>(index =>
                index.Items.ContainsKey("456") &&
                index.Items["456"].SequenceEqual(new[] { "img-a", "img-b" })),
            "v:1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the synchronizer stores empty lists for activities without photos.
    /// </summary>
    [Fact]
    public async Task SyncAsync_StoresEmptyListsForActivitiesWithoutPhotos()
    {
        var logger = new Mock<ILogger<SyncStravaActivityIdImageIdIndexSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        detailRepository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([789L]);
        detailRepository.Setup(r => r.GetByIdAsync(789, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 789, Name = "No Photos" });
        client.Setup(c => c.GetActivityPhotosAsync(789, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImage>());
        client.Setup(c => c.ExtractActivityImages(It.IsAny<StravaActivityDetail>()))
            .Returns(Array.Empty<StravaActivityImage>());

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), null));
        indexRepository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityIdImageIdIndexSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            indexRepository.Object);

        var result = await synchronizer.SyncAsync();

        Assert.Equal(1, result);
        indexRepository.Verify(r => r.SaveAsync(It.Is<StravaActivityImageIdIndex>(index => index.Items["789"].Count == 0), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the synchronizer retries merge/save on precondition failure.
    /// </summary>
    [Fact]
    public async Task SyncAsync_RetriesOnPreconditionFailure()
    {
        var logger = new Mock<ILogger<SyncStravaActivityIdImageIdIndexSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        detailRepository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([900L]);
        detailRepository.Setup(r => r.GetByIdAsync(900, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 900, Name = "Conflict", TotalPhotoCount = 2 });
        client.Setup(c => c.GetActivityPhotosAsync(900, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImage { ActivityId = 900, ImageId = "img-900", ImageUrl = "https://example.com/900.jpg", Extension = "jpg" }]);

        indexRepository.SetupSequence(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), "v:1"))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["901"] = ["img-other"]
                }
            }, "v:2"))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["901"] = ["img-other"],
                    ["900"] = ["img-900"]
                }
            }, "v:3"));

        indexRepository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RookRun.Common.Exceptions.PreconditionFailedException("conflict"));

        var synchronizer = new SyncStravaActivityIdImageIdIndexSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            indexRepository.Object,
            (delay, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<RookRun.Common.Exceptions.PreconditionFailedException>(() => synchronizer.SyncAsync());
        indexRepository.Verify(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Verifies the synchronizer preserves loop errors while still attempting to flush pending work.
    /// </summary>
    [Fact]
    public async Task SyncAsync_PreservesLoopErrors()
    {
        var logger = new Mock<ILogger<SyncStravaActivityIdImageIdIndexSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        detailRepository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([1000L, 1001L]);
        detailRepository.Setup(r => r.GetByIdAsync(1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 1000, Name = "First" });
        detailRepository.Setup(r => r.GetByIdAsync(1001, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("detail failure"));
        client.Setup(c => c.GetActivityPhotosAsync(1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImage>());
        client.Setup(c => c.ExtractActivityImages(It.IsAny<StravaActivityDetail>()))
            .Returns(Array.Empty<StravaActivityImage>());

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), null));
        indexRepository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityIdImageIdIndexSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            indexRepository.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => synchronizer.SyncAsync());
        indexRepository.Verify(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies pending entries are flushed before a rate-limit retry sleeps.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SavesPendingEntriesBeforeRateLimitRetryDelay()
    {
        var logger = new Mock<ILogger<SyncStravaActivityIdImageIdIndexSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();
        var events = new List<string>();

        detailRepository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([100L, 200L]);
        detailRepository.Setup(r => r.GetByIdAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 100, Name = "First", TotalPhotoCount = 2 });
        detailRepository.Setup(r => r.GetByIdAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 200, Name = "Second", TotalPhotoCount = 2 });

        client.Setup(c => c.GetActivityPhotosAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImage { ActivityId = 100, ImageId = "img-100", ImageUrl = "https://example.com/100.jpg", Extension = "jpg" }]);
        client.SetupSequence(c => c.GetActivityPhotosAsync(200, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitException(HttpStatusCode.TooManyRequests, null, new Dictionary<string, string[]>(), TimeSpan.FromSeconds(5), "Strava API"))
            .ReturnsAsync([new StravaActivityImage { ActivityId = 200, ImageId = "img-200", ImageUrl = "https://example.com/200.jpg", Extension = "jpg" }]);

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), null));
        indexRepository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<StravaActivityImageIdIndex, string?, CancellationToken>((index, _, _) =>
                events.Add($"save:{string.Join(',', index.Items.Keys.OrderBy(key => key, StringComparer.Ordinal))}"))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityIdImageIdIndexSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            indexRepository.Object,
            (delay, _) =>
            {
                events.Add($"delay:{delay.TotalSeconds}");
                return Task.CompletedTask;
            });

        var result = await synchronizer.SyncAsync();

        Assert.Equal(2, result);
        Assert.Equal("save:100", events[0]);
        Assert.StartsWith("delay:", events[1]);
        Assert.Contains("save:200", events);
    }

    /// <summary>
    /// Verifies the photos API is skipped when detail indicates at most one photo.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsPhotosApiWhenTotalPhotoCountIsOneOrLess()
    {
        var logger = new Mock<ILogger<SyncStravaActivityIdImageIdIndexSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        detailRepository.Setup(r => r.ListActivityIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([321L]);
        detailRepository.Setup(r => r.GetByIdAsync(321, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StravaActivityDetail { Id = 321, Name = "Single Photo", TotalPhotoCount = 1 });

        client.Setup(c => c.ExtractActivityImages(It.Is<StravaActivityDetail>(d => d.Id == 321)))
            .Returns([new StravaActivityImage { ActivityId = 321, ImageId = "img-321", ImageUrl = "https://example.com/321.jpg", Extension = "jpg" }]);

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), null));
        indexRepository.Setup(r => r.SaveAsync(It.IsAny<StravaActivityImageIdIndex>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityIdImageIdIndexSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            indexRepository.Object);

        var result = await synchronizer.SyncAsync();

        Assert.Equal(1, result);
        client.Verify(c => c.GetActivityPhotosAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        indexRepository.Verify(r => r.SaveAsync(
            It.Is<StravaActivityImageIdIndex>(index =>
                index.Items.ContainsKey("321") &&
                index.Items["321"].SequenceEqual(new[] { "img-321" })),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}