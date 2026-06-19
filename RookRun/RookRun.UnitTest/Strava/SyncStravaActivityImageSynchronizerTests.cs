using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="SyncStravaActivityImageSynchronizer"/> using the cached activity-image-id index.
/// </summary>
public sealed class SyncStravaActivityImageSynchronizerTests
{
    /// <summary>
    /// Verifies synchronizer downloads only images indexed for the requested activities.
    /// </summary>
    [Fact]
    public async Task SyncAsync_DownloadsIndexedImagesForRequestedActivities()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["123"] = ["img-1", "img-2"]
                }
            }, "v:1"));

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.Setup(c => c.GetActivityPhotosAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StravaActivityImage { ActivityId = 123, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" },
                new StravaActivityImage { ActivityId = 123, ImageId = "img-2", ImageUrl = "https://example.com/2.jpg", Extension = "jpg" }
            ]);

        client.Setup(c => c.DownloadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0xFF, 0xD8 });

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([123]);

        Assert.Equal(2, result);
        imageRepository.Verify(r => r.SaveImageAsync(123, "img-1", "jpg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        imageRepository.Verify(r => r.SaveImageAsync(123, "img-2", "jpg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer skips activities with no indexed images.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsActivitiesWithNoIndexedImages()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), null));

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([999]);

        Assert.Equal(0, result);
        client.Verify(c => c.GetActivityPhotosAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer skips images already cached.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsCachedImages()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["456"] = ["img-1", "img-2"]
                }
            }, "v:1"));

        // img-1 is cached, img-2 is missing
        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImageKey(456, "img-1")]);

        client.Setup(c => c.GetActivityPhotosAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StravaActivityImage { ActivityId = 456, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" },
                new StravaActivityImage { ActivityId = 456, ImageId = "img-2", ImageUrl = "https://example.com/2.jpg", Extension = "jpg" }
            ]);

        client.Setup(c => c.DownloadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0xFF, 0xD8 });

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([456]);

        Assert.Equal(1, result);
        client.Verify(c => c.DownloadImageAsync("https://example.com/2.jpg", It.IsAny<CancellationToken>()), Times.Once);
        imageRepository.Verify(r => r.SaveImageAsync(456, "img-2", "jpg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer returns empty result when index is empty.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ReturnsZeroForEmptyIndex()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex(), null));

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([100, 200, 300]);

        Assert.Equal(0, result);
    }

    /// <summary>
    /// Verifies synchronizer returns zero for null activity ID list.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ThrowsForNullActivityIds()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() => synchronizer.SyncAsync(null!));
    }

    /// <summary>
    /// Verifies synchronizer continues when a photo lookup fails.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ContinuesWhenPhotoLookupFails()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["789"] = ["img-a"]
                }
            }, "v:1"));

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.Setup(c => c.GetActivityPhotosAsync(789, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network error"));

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([789]);

        Assert.Equal(0, result);
        imageRepository.Verify(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer filters by requested activity IDs only.
    /// </summary>
    [Fact]
    public async Task SyncAsync_FiltersIndexByRequestedActivityIds()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["100"] = ["img-a"],
                    ["200"] = ["img-b"],
                    ["300"] = ["img-c"]
                }
            }, "v:1"));

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.Setup(c => c.GetActivityPhotosAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImage { ActivityId = 100, ImageId = "img-a", ImageUrl = "https://example.com/a.jpg", Extension = "jpg" }]);
        client.Setup(c => c.GetActivityPhotosAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImage { ActivityId = 200, ImageId = "img-b", ImageUrl = "https://example.com/b.jpg", Extension = "jpg" }]);

        client.Setup(c => c.DownloadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x01 });

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([100, 200]);

        Assert.Equal(2, result);
        client.Verify(c => c.GetActivityPhotosAsync(100, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetActivityPhotosAsync(200, It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(c => c.GetActivityPhotosAsync(300, It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer continues when download fails.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ContinuesWhenDownloadFails()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var indexRepository = new Mock<IStravaActivityImageIdIndexRepository>();

        indexRepository.Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StravaActivityImageIdIndex
            {
                Items = new Dictionary<string, List<string>>
                {
                    ["111"] = ["img-1", "img-2"]
                }
            }, "v:1"));

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.Setup(c => c.GetActivityPhotosAsync(111, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StravaActivityImage { ActivityId = 111, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" },
                new StravaActivityImage { ActivityId = 111, ImageId = "img-2", ImageUrl = "https://example.com/2.jpg", Extension = "jpg" }
            ]);

        client.Setup(c => c.DownloadImageAsync("https://example.com/1.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);
        client.Setup(c => c.DownloadImageAsync("https://example.com/2.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0xFF, 0xD8 });

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object, client.Object, imageRepository.Object, indexRepository.Object);

        var result = await synchronizer.SyncAsync([111]);

        Assert.Equal(1, result);
        imageRepository.Verify(r => r.SaveImageAsync(111, "img-2", "jpg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
