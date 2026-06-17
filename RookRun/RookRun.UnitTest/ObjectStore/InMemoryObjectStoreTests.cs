using RookRun.Common.Exceptions;
using RookRun.ObjectStore;

namespace RookRun.UnitTest.ObjectStore;

public class InMemoryObjectStoreTests
{
    [Fact]
    public async Task StoreReadListAndDelete_RoundTripsObject()
    {
        var store = new InMemoryObjectStore();
        var value = new ObjectStoreTestRecord { Name = "alpha", Count = 5 };

        await store.StoreObjectAsync("runs/2026/one", value, overwrite: false);

        var loaded = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/2026/one");
        var listed = await store.ListObjectsAsync("runs/");
        var deleted = await store.TryDeleteObjectAsync("runs/2026/one");
        var missing = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/2026/one");

        Assert.True(loaded.IsFound);
        Assert.Equal(value.Name, loaded.Value!.Name);
        Assert.Equal(value.Count, loaded.Value.Count);
        Assert.Equal(new[] { "runs/2026/one" }, listed);
        Assert.True(deleted);
        Assert.True(missing.IsNotFound);
    }

    [Fact]
    public async Task StoreObjectAsync_WithoutOverwrite_ThrowsWhenPathExists()
    {
        var store = new InMemoryObjectStore();

        await store.StoreObjectAsync("runs/item", new ObjectStoreTestRecord { Name = "first" }, overwrite: false);

        await Assert.ThrowsAsync<IOException>(() => store.StoreObjectAsync("runs/item", new ObjectStoreTestRecord { Name = "second" }, overwrite: false));
    }

    [Fact]
    public async Task ListObjectsAsync_NormalizesPrefixSeparators()
    {
        var store = new InMemoryObjectStore();

        await store.StoreObjectAsync("runs/2026/one", new ObjectStoreTestRecord { Name = "one" }, overwrite: false);
        await store.StoreObjectAsync("runs/2025/two", new ObjectStoreTestRecord { Name = "two" }, overwrite: false);

        var listed = await store.ListObjectsAsync("runs\\2026");

        Assert.Equal(new[] { "runs/2026/one" }, listed);
    }

    [Fact]
    public async Task TryReadObjectAsync_ReturnsNotModified_WhenObjectIsNotNewerThanCutoff()
    {
        var store = new InMemoryObjectStore();
        await store.StoreObjectAsync("runs/item", new ObjectStoreTestRecord { Name = "one" }, overwrite: false);

        var initial = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        var notModified = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item", initial.LastModifiedUtc);

        Assert.True(initial.IsFound);
        Assert.True(notModified.IsNotModified);
        Assert.Null(notModified.Value);
    }

    [Fact]
    public async Task StoreStreamAsync_AndTryReadStreamAsync_RoundTripsRawBytes()
    {
        var store = new InMemoryObjectStore();
        var originalBytes = new byte[] { 1, 2, 3, 4, 5 };

        await using var writeStream = new MemoryStream(originalBytes);
        await store.StoreStreamAsync("raw/item", writeStream, overwrite: false);

        var result = await store.TryReadStreamAsync("raw/item");

        Assert.True(result.IsFound);
        Assert.NotNull(result.LastModifiedUtc);

        await using var readStream = result.Value!;
        var readBytes = new MemoryStream();
        await readStream.CopyToAsync(readBytes);

        Assert.Equal(originalBytes, readBytes.ToArray());
    }

    [Fact]
    public async Task TryReadStreamAsync_ReturnsNotFound_WhenPathDoesNotExist()
    {
        var store = new InMemoryObjectStore();

        var result = await store.TryReadStreamAsync("missing/item");

        Assert.True(result.IsNotFound);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task TryReadStreamAsync_ReturnsNotModified_WhenObjectIsNotNewerThanCutoff()
    {
        var store = new InMemoryObjectStore();

        await using var writeStream = new MemoryStream(new byte[] { 42 });
        await store.StoreStreamAsync("raw/item", writeStream, overwrite: false);

        var initial = await store.TryReadStreamAsync("raw/item");
        await using var _ = initial.Value!;

        var notModified = await store.TryReadStreamAsync("raw/item", initial.LastModifiedUtc);

        Assert.True(initial.IsFound);
        Assert.True(notModified.IsNotModified);
        Assert.Null(notModified.Value);
    }

    [Fact]
    public async Task StoreObjectAsync_WithETag_SucceedsWhenETagMatches()
    {
        var store = new InMemoryObjectStore();
        var original = new ObjectStoreTestRecord { Name = "first", Count = 1 };
        var updated = new ObjectStoreTestRecord { Name = "first", Count = 2 };

        await store.StoreObjectAsync("runs/item", original, overwrite: true);

        var read1 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        Assert.NotNull(read1.ETag);
        var initialETag = read1.ETag;

        // Update with matching ETag should succeed (overwrite=true with ETag for optimistic concurrency)
        await store.StoreObjectAsync("runs/item", updated, overwrite: true, ifMatchETag: initialETag);

        var read2 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        Assert.Equal(updated.Count, read2.Value!.Count);
        Assert.NotEqual(initialETag, read2.ETag); // ETag should have changed
    }

    [Fact]
    public async Task StoreObjectAsync_WithETag_ThrowsWhenETagMismatches()
    {
        var store = new InMemoryObjectStore();
        var original = new ObjectStoreTestRecord { Name = "first", Count = 1 };

        await store.StoreObjectAsync("runs/item", original, overwrite: true);

        var read1 = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
        var initialETag = read1.ETag;

        // Modify the object to change its ETag
        var modified = new ObjectStoreTestRecord { Name = "first", Count = 99 };
        await store.StoreObjectAsync("runs/item", modified, overwrite: true);

        // Try to update with stale ETag should fail
        var updated = new ObjectStoreTestRecord { Name = "first", Count = 2 };
        var exception = await Assert.ThrowsAsync<ObjectStorePreconditionFailedException>(
            () => store.StoreObjectAsync("runs/item", updated, overwrite: true, ifMatchETag: initialETag)
        );

        Assert.Contains("ETag mismatch", exception.Message);
    }

    [Fact]
    public async Task StoreObjectAsync_WithETag_ThrowsWhenObjectMissing()
    {
        var store = new InMemoryObjectStore();
        var obj = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        // Try to update with ETag on non-existent object
        var exception = await Assert.ThrowsAsync<ObjectStorePreconditionFailedException>(
            () => store.StoreObjectAsync("missing/item", obj, overwrite: true, ifMatchETag: "v:1")
        );

        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public async Task StoreObjectAsync_RejectsConflictingSemantics_OverwriteFalseWithETag()
    {
        var store = new InMemoryObjectStore();
        var obj = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        var exception = Assert.Throws<ArgumentException>(
            () => store.StoreObjectAsync("runs/item", obj, overwrite: false, ifMatchETag: "v:1").GetAwaiter().GetResult()
        );

        Assert.Contains("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StoreStreamAsync_WithETag_SucceedsWhenETagMatches()
    {
        var store = new InMemoryObjectStore();
        var content1 = new byte[] { 1, 2, 3 };
        var content2 = new byte[] { 4, 5, 6 };

        await using var stream1 = new MemoryStream(content1);
        await store.StoreStreamAsync("raw/item", stream1, overwrite: true);

        var read1 = await store.TryReadStreamAsync("raw/item");
        Assert.NotNull(read1.ETag);
        var initialETag = read1.ETag;
        await using var _ = read1.Value!;

        // Update with matching ETag should succeed (overwrite=true with ETag for optimistic concurrency)
        await using var stream2 = new MemoryStream(content2);
        await store.StoreStreamAsync("raw/item", stream2, overwrite: true, ifMatchETag: initialETag);

        var read2 = await store.TryReadStreamAsync("raw/item");
        Assert.NotEqual(initialETag, read2.ETag); // ETag should have changed
    }

    [Fact]
    public async Task ETag_IncrementOnEachMutation()
    {
        var store = new InMemoryObjectStore();
        var obj = new ObjectStoreTestRecord { Name = "test", Count = 0 };

        await store.StoreObjectAsync("runs/item", obj, overwrite: true);

        var eTags = new List<string>();
        for (int i = 1; i <= 3; i++)
        {
            var read = await store.TryReadObjectAsync<ObjectStoreTestRecord>("runs/item");
            eTags.Add(read.ETag!);

            var updated = new ObjectStoreTestRecord { Name = "test", Count = i };
            await store.StoreObjectAsync("runs/item", updated, overwrite: true);
        }

        // All ETags should be different and in incrementing format
        Assert.Equal(3, eTags.Count);
        Assert.All(eTags, eTag => Assert.Matches(@"^v:\d+$", eTag));
        Assert.NotEqual(eTags[0], eTags[1]);
        Assert.NotEqual(eTags[1], eTags[2]);
    }
}