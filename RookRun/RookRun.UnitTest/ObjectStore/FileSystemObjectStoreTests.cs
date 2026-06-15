using RookRun.ObjectStore;

namespace RookRun.UnitTest.ObjectStore;

public class FileSystemObjectStoreTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"rookrun-objectstore-{Guid.NewGuid():N}");

    [Fact]
    public async Task StoreReadListAndDelete_RoundTripsObject()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        var value = new ObjectStoreTestRecord { Name = "alpha", Count = 7 };

        await store.StoreObjectAsync("runs/2026/one", value, overwrite: false);

        var loaded = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/2026/one");
        var listed = await store.ListObjectsAsync("runs");
        var deleted = await store.TryDeleteObjectAsync("runs/2026/one");
        var deletedAgain = await store.TryDeleteObjectAsync("runs/2026/one");

        Assert.True(loaded.IsFound);
        Assert.Equal(value.Name, loaded.Value!.Name);
        Assert.Equal(value.Count, loaded.Value.Count);
        Assert.Equal(new[] { "runs/2026/one" }, listed);
        Assert.True(deleted);
        Assert.False(deletedAgain);
    }

    [Fact]
    public async Task StoreObjectAsync_WithoutOverwrite_ThrowsWhenPathExists()
    {
        var store = new FileSystemObjectStore(rootDirectory);

        await store.StoreObjectAsync("runs/item", new ObjectStoreTestRecord { Name = "first" }, overwrite: false);

        await Assert.ThrowsAsync<IOException>(() => store.StoreObjectAsync("runs/item", new ObjectStoreTestRecord { Name = "second" }, overwrite: false));
    }

    [Fact]
    public async Task TryReadObjectAsync_ReturnsNotFoundWhenPathDoesNotExist()
    {
        var store = new FileSystemObjectStore(rootDirectory);

        var loaded = await store.TryReadObjectAsync<ObjectStoreTestRecord>("missing/item");

        Assert.True(loaded.IsNotFound);
    }

    [Fact]
    public async Task TryReadObjectAsync_ReturnsNotModified_WhenObjectIsNotNewerThanCutoff()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        await store.StoreObjectAsync("runs/item", new ObjectStoreTestRecord { Name = "one" }, overwrite: false);

        var initial = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        var notModified = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item", initial.LastModifiedUtc);

        Assert.True(initial.IsFound);
        Assert.True(notModified.IsNotModified);
        Assert.Null(notModified.Value);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}