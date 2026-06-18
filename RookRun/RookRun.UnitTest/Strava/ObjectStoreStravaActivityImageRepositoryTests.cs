using RookRun.ObjectStore;
using RookRun.Strava.Models;
using RookRun.Strava.Repositories;

namespace RookRun.UnitTest.Strava;

/// <summary>
/// Tests for <see cref="ObjectStoreStravaActivityImageRepository"/>.
/// </summary>
public sealed class ObjectStoreStravaActivityImageRepositoryTests
{
    /// <summary>
    /// Verifies saving and retrieving image bytes.
    /// </summary>
    [Fact]
    public async Task SaveImageAsync_AndGetImageAsync_RoundTripsImageBytes()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
        await repository.SaveImageAsync(123, "img-001", "jpg", imageBytes);

        var retrieved = await repository.GetImageAsync(123, "img-001");

        Assert.NotNull(retrieved);
        Assert.Equal(imageBytes, retrieved);
    }

    /// <summary>
    /// Verifies GetImageAsync returns null when image doesn't exist.
    /// </summary>
    [Fact]
    public async Task GetImageAsync_ReturnsNullWhenNotFound()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var image = await repository.GetImageAsync(999, "missing");

        Assert.Null(image);
    }

    /// <summary>
    /// Verifies ImageExistsAsync returns true when image exists.
    /// </summary>
    [Fact]
    public async Task ImageExistsAsync_ReturnsTrueWhenImageExists()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        await repository.SaveImageAsync(456, "img-002", "png", imageBytes);

        var exists = await repository.ImageExistsAsync(456, "img-002");

        Assert.True(exists);
    }

    /// <summary>
    /// Verifies ImageExistsAsync returns false when image doesn't exist.
    /// </summary>
    [Fact]
    public async Task ImageExistsAsync_ReturnsFalseWhenImageNotFound()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var exists = await repository.ImageExistsAsync(999, "missing");

        Assert.False(exists);
    }

    /// <summary>
    /// Verifies ListImagesByActivityAsync returns all images for an activity.
    /// </summary>
    [Fact]
    public async Task ListImagesByActivityAsync_ReturnsAllActivityImages()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var imageData1 = new byte[] { 0xFF, 0xD8 };
        var imageData2 = new byte[] { 0x89, 0x50 };
        
        await repository.SaveImageAsync(789, "img-a", "jpg", imageData1);
        await repository.SaveImageAsync(789, "img-b", "png", imageData2);
        await repository.SaveImageAsync(999, "img-x", "jpg", imageData1);

        var images = await repository.ListImagesByActivityAsync(789);

        Assert.Equal(2, images.Count);
        Assert.Contains("img-a", images);
        Assert.Contains("img-b", images);
        Assert.DoesNotContain("img-x", images);
    }

    /// <summary>
    /// Verifies ListImagesByActivityAsync returns empty list for activity with no images.
    /// </summary>
    [Fact]
    public async Task ListImagesByActivityAsync_ReturnsEmptyWhenNoImages()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var images = await repository.ListImagesByActivityAsync(999);

        Assert.Empty(images);
    }

    /// <summary>
    /// Verifies ListImageKeysAsync returns global image keys across activities.
    /// </summary>
    [Fact]
    public async Task ListImageKeysAsync_ReturnsImageKeysAcrossActivities()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        await repository.SaveImageAsync(100, "img-a", "jpg", [0x01]);
        await repository.SaveImageAsync(100, "img-b", "png", [0x02]);
        await repository.SaveImageAsync(200, "img-c", "jpg", [0x03]);

        var keys = await repository.ListImageKeysAsync();

        Assert.Equal(3, keys.Count);
        Assert.Contains(new StravaActivityImageKey(100, "img-a"), keys);
        Assert.Contains(new StravaActivityImageKey(100, "img-b"), keys);
        Assert.Contains(new StravaActivityImageKey(200, "img-c"), keys);
    }

    /// <summary>
    /// Verifies DeleteImageAsync removes an image.
    /// </summary>
    [Fact]
    public async Task DeleteImageAsync_RemovesImage()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var imageBytes = new byte[] { 0xFF, 0xD8 };
        await repository.SaveImageAsync(111, "img-del", "jpg", imageBytes);

        var existsBefore = await repository.ImageExistsAsync(111, "img-del");
        await repository.DeleteImageAsync(111, "img-del");
        var existsAfter = await repository.ImageExistsAsync(111, "img-del");

        Assert.True(existsBefore);
        Assert.False(existsAfter);
    }

    /// <summary>
    /// Verifies SaveImageAsync overwrites existing image.
    /// </summary>
    [Fact]
    public async Task SaveImageAsync_OverwritesExistingImage()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var bytes1 = new byte[] { 0xFF, 0xD8, 0xFF };
        var bytes2 = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        await repository.SaveImageAsync(222, "img-ov", "jpg", bytes1);
        var retrieved1 = await repository.GetImageAsync(222, "img-ov");

        await repository.SaveImageAsync(222, "img-ov", "png", bytes2);
        var retrieved2 = await repository.GetImageAsync(222, "img-ov");

        Assert.Equal(bytes1, retrieved1);
        Assert.Equal(bytes2, retrieved2);
    }

    /// <summary>
    /// Verifies custom prefix is included in storage path.
    /// </summary>
    [Fact]
    public async Task Constructor_WithPrefix_StoresToPrefixedPath()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store, "custom/prefix");

        var imageBytes = new byte[] { 0xFF, 0xD8 };
        await repository.SaveImageAsync(333, "img-pre", "jpg", imageBytes);

        var objects = await store.ListObjectsAsync("custom/prefix/");

        Assert.NotEmpty(objects);
        Assert.True(objects.Any(obj => obj.Contains("333", StringComparison.Ordinal)), "Should contain activity ID in path");
    }

    /// <summary>
    /// Verifies handling of image IDs with special characters or dots.
    /// </summary>
    [Fact]
    public async Task GetImageAsync_HandlesImageIdsWithSpecialCharacters()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        var imageBytes = new byte[] { 0xFF, 0xD8 };
        var imageId = "img-123-abc-xyz";
        
        await repository.SaveImageAsync(444, imageId, "jpg", imageBytes);
        var retrieved = await repository.GetImageAsync(444, imageId);

        Assert.NotNull(retrieved);
        Assert.Equal(imageBytes, retrieved);
    }

    /// <summary>
    /// Verifies image IDs with path separators are rejected.
    /// </summary>
    [Fact]
    public async Task SaveImageAsync_ThrowsForImageIdWithPathSeparators()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await repository.SaveImageAsync(555, "bad/id", "jpg", [0x01]));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await repository.SaveImageAsync(555, "bad\\id", "jpg", [0x01]));
    }

    /// <summary>
    /// Verifies image extensions with path separators are rejected.
    /// </summary>
    [Fact]
    public async Task SaveImageAsync_ThrowsForExtensionWithPathSeparators()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await repository.SaveImageAsync(556, "img-ok", "jp/g", [0x01]));

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await repository.SaveImageAsync(556, "img-ok", "jp\\g", [0x01]));
    }

    /// <summary>
    /// Verifies nested object paths are ignored by global key listing.
    /// </summary>
    [Fact]
    public async Task ListImageKeysAsync_IgnoresNestedPaths()
    {
        var store = new InMemoryObjectStore();
        var repository = new ObjectStoreStravaActivityImageRepository(store);

        await repository.SaveImageAsync(600, "img-valid", "jpg", [0x01]);
        await store.StoreStreamAsync(
            "activities/strava_images/600/nested/img-invalid.jpg",
            new MemoryStream([0x02]),
            overwrite: true);

        var keys = await repository.ListImageKeysAsync();

        Assert.Single(keys);
        Assert.Contains(new StravaActivityImageKey(600, "img-valid"), keys);
    }
}
