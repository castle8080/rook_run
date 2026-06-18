using RookRun.Common.Exceptions;
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

    [Fact]
    public async Task StoreObjectAsync_WithETag_SucceedsWhenETagMatches()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        var original = new ObjectStoreTestRecord { Name = "first", Count = 1 };
        var updated = new ObjectStoreTestRecord { Name = "first", Count = 2 };

        await store.StoreObjectAsync("runs/item", original, overwrite: true);

        var read1 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        Assert.NotNull(read1.ETag);
        var initialETag = read1.ETag;

        // Small delay to ensure file modification time changes
        await Task.Delay(10);

        // Update with matching ETag should succeed (overwrite=true with ETag for optimistic concurrency)
        await store.StoreObjectAsync("runs/item", updated, overwrite: true, ifMatchETag: initialETag);

        var read2 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        Assert.Equal(updated.Count, read2.Value!.Count);
        Assert.NotEqual(initialETag, read2.ETag); // ETag should have changed (different mtime)
    }

    [Fact]
    public async Task StoreObjectAsync_WithETag_ThrowsWhenETagMismatches()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        var original = new ObjectStoreTestRecord { Name = "first", Count = 1 };

        await store.StoreObjectAsync("runs/item", original, overwrite: true);

        var read1 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        var initialETag = read1.ETag;

        // Modify the object to change its ETag
        await Task.Delay(10);
        var modified = new ObjectStoreTestRecord { Name = "first", Count = 99 };
        await store.StoreObjectAsync("runs/item", modified, overwrite: true);

        // Try to update with stale ETag should fail
        var updated = new ObjectStoreTestRecord { Name = "first", Count = 2 };
        var exception = await Assert.ThrowsAsync<PreconditionFailedException>(
            () => store.StoreObjectAsync("runs/item", updated, overwrite: true, ifMatchETag: initialETag)
        );

        Assert.Contains("ETag mismatch", exception.Message);
    }

    [Fact]
    public async Task StoreObjectAsync_WithETag_ThrowsWhenObjectMissing()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        var obj = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        // Try to update with ETag on non-existent object
        var exception = await Assert.ThrowsAsync<PreconditionFailedException>(
            () => store.StoreObjectAsync("missing/item", obj, overwrite: true, ifMatchETag: "mtime:123")
        );

        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public async Task StoreObjectAsync_RejectsConflictingSemantics_OverwriteFalseWithETag()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        var obj = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        var exception = Assert.Throws<ArgumentException>(
            () => store.StoreObjectAsync("runs/item", obj, overwrite: false, ifMatchETag: "mtime:123").GetAwaiter().GetResult()
        );

        Assert.Contains("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ETag_UsesFileModificationTime()
    {
        var store = new FileSystemObjectStore(rootDirectory);
        var obj = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        await store.StoreObjectAsync("runs/item", obj, overwrite: true);

        var read1 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        Assert.Matches(@"^mtime:\d+$", read1.ETag!);

        // Wait and modify
        await Task.Delay(10);
        var modified = new ObjectStoreTestRecord { Name = "test", Count = 2 };
        await store.StoreObjectAsync("runs/item", modified, overwrite: true);

        var read2 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        Assert.Matches(@"^mtime:\d+$", read2.ETag!);
        Assert.NotEqual(read1.ETag, read2.ETag);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}