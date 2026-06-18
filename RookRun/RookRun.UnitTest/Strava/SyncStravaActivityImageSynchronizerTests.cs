using Microsoft.Extensions.Logging;
using Moq;
using RookRun.Common.Exceptions;
using RookRun.Strava.Client;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;
using RookRun.Strava.Sync;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="SyncStravaActivityImageSynchronizer"/>.
/// </summary>
public sealed class SyncStravaActivityImageSynchronizerTests
{
    /// <summary>
    /// Verifies synchronizer extracts and downloads images.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ExtracsAndDownloadsImages()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        var detail = new StravaActivityDetail { Id = 123, Name = "Activity" };
        var images = new List<StravaActivityImage>
        {
            new() { ActivityId = 123, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" },
            new() { ActivityId = 123, ImageId = "img-2", ImageUrl = "https://example.com/2.jpg", Extension = "jpg" }
        };

        var imageBytes1 = new byte[] { 0xFF, 0xD8 };
        var imageBytes2 = new byte[] { 0xFF, 0xD9 };

        detailRepository.Setup(r => r.GetByIdAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        client.Setup(c => c.ExtractActivityImages(detail))
            .Returns(images);

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.Setup(c => c.DownloadImageAsync("https://example.com/1.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageBytes1);
        client.Setup(c => c.DownloadImageAsync("https://example.com/2.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imageBytes2);

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(logger.Object, client.Object, detailRepository.Object, imageRepository.Object);

        var result = await synchronizer.SyncAsync([123]);

        Assert.Equal(2, result);
        imageRepository.Verify(r => r.SaveImageAsync(123, "img-1", "jpg", imageBytes1, It.IsAny<CancellationToken>()), Times.Once);
        imageRepository.Verify(r => r.SaveImageAsync(123, "img-2", "jpg", imageBytes2, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer skips images that are already cached.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsCachedImages()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        var detail = new StravaActivityDetail { Id = 456, Name = "Activity" };
        var images = new List<StravaActivityImage>
        {
            new() { ActivityId = 456, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" },
            new() { ActivityId = 456, ImageId = "img-2", ImageUrl = "https://example.com/2.jpg", Extension = "jpg" }
        };

        detailRepository.Setup(r => r.GetByIdAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        client.Setup(c => c.ExtractActivityImages(detail))
            .Returns(images);

        // img-1 is already cached; img-2 is new
        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StravaActivityImageKey(456, "img-1")]);

        client.Setup(c => c.DownloadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0xFF });

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(logger.Object, client.Object, detailRepository.Object, imageRepository.Object);

        var result = await synchronizer.SyncAsync([456]);

        // Should only download img-2
        client.Verify(c => c.DownloadImageAsync("https://example.com/1.jpg", It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.DownloadImageAsync("https://example.com/2.jpg", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, result);
    }

    /// <summary>
    /// Verifies synchronizer handles missing activity details gracefully.
    /// </summary>
    [Fact]
    public async Task SyncAsync_SkipsActivitiesWithoutCachedDetails()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        detailRepository.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StravaActivityDetail?)null);

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        var synchronizer = new SyncStravaActivityImageSynchronizer(logger.Object, client.Object, detailRepository.Object, imageRepository.Object);

        var result = await synchronizer.SyncAsync([999]);

        Assert.Equal(0, result);
        client.Verify(c => c.ExtractActivityImages(It.IsAny<StravaActivityDetail>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer handles activities with no images.
    /// </summary>
    [Fact]
    public async Task SyncAsync_HandlesActivitiesWithNoImages()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        var detail = new StravaActivityDetail { Id = 789, Name = "No Images" };

        detailRepository.Setup(r => r.GetByIdAsync(789, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        client.Setup(c => c.ExtractActivityImages(detail))
            .Returns(new List<StravaActivityImage>());

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        var synchronizer = new SyncStravaActivityImageSynchronizer(logger.Object, client.Object, detailRepository.Object, imageRepository.Object);

        var result = await synchronizer.SyncAsync([789]);

        Assert.Equal(0, result);
        client.Verify(c => c.DownloadImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies synchronizer continues when image download fails.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ContinuesWhenDownloadFails()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        var detail = new StravaActivityDetail { Id = 111, Name = "Activity" };
        var images = new List<StravaActivityImage>
        {
            new() { ActivityId = 111, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" },
            new() { ActivityId = 111, ImageId = "img-2", ImageUrl = "https://example.com/2.jpg", Extension = "jpg" }
        };

        detailRepository.Setup(r => r.GetByIdAsync(111, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        client.Setup(c => c.ExtractActivityImages(detail))
            .Returns(images);

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.Setup(c => c.DownloadImageAsync("https://example.com/1.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Simulates download failure
        client.Setup(c => c.DownloadImageAsync("https://example.com/2.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0xFF, 0xD8 });

        imageRepository.Setup(r => r.SaveImageAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), 
            It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(logger.Object, client.Object, detailRepository.Object, imageRepository.Object);

        var result = await synchronizer.SyncAsync([111]);

        // Only img-2 should be saved
        imageRepository.Verify(r => r.SaveImageAsync(111, "img-2", It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, result);
    }

    /// <summary>
    /// Verifies synchronizer retries rate-limit responses before saving the image.
    /// </summary>
    [Fact]
    public async Task SyncAsync_RetriesRateLimitFailuresBeforeSavingImage()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();
        var delayCalls = new List<TimeSpan>();

        var detail = new StravaActivityDetail { Id = 222, Name = "Activity" };
        var image = new StravaActivityImage { ActivityId = 222, ImageId = "img-1", ImageUrl = "https://example.com/1.jpg", Extension = "jpg" };

        detailRepository.Setup(r => r.GetByIdAsync(222, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        client.Setup(c => c.ExtractActivityImages(detail))
            .Returns([image]);

        imageRepository.Setup(r => r.ListImageKeysAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<StravaActivityImageKey>());

        client.SetupSequence(c => c.DownloadImageAsync(image.ImageUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RateLimitException(System.Net.HttpStatusCode.TooManyRequests, "rate limited", new Dictionary<string, string[]>() ))
            .ReturnsAsync(new byte[] { 0xFF, 0xD8 });

        imageRepository.Setup(r => r.SaveImageAsync(222, "img-1", "jpg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var synchronizer = new SyncStravaActivityImageSynchronizer(
            logger.Object,
            client.Object,
            detailRepository.Object,
            imageRepository.Object,
            (delay, _) =>
            {
                delayCalls.Add(delay);
                return Task.CompletedTask;
            });

        var result = await synchronizer.SyncAsync([222]);

        Assert.Equal(1, result);
        Assert.Single(delayCalls);
        Assert.Equal(TimeSpan.FromSeconds(5), delayCalls[0]);
        imageRepository.Verify(r => r.SaveImageAsync(222, "img-1", "jpg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies synchronizer returns zero for empty input.
    /// </summary>
    [Fact]
    public async Task SyncAsync_ReturnsZeroForEmptyInput()
    {
        var logger = new Mock<ILogger<SyncStravaActivityImageSynchronizer>>();
        var client = new Mock<IStravaActivityDetailClient>();
        var detailRepository = new Mock<IStravaActivityDetailRepository>();
        var imageRepository = new Mock<IStravaActivityImageRepository>();

        var synchronizer = new SyncStravaActivityImageSynchronizer(logger.Object, client.Object, detailRepository.Object, imageRepository.Object);

        var result = await synchronizer.SyncAsync([]);

        Assert.Equal(0, result);
        detailRepository.Verify(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
