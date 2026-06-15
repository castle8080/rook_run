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
}