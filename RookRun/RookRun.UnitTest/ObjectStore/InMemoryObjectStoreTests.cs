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

        Assert.NotNull(loaded);
        Assert.Equal(value.Name, loaded!.Name);
        Assert.Equal(value.Count, loaded.Count);
        Assert.Equal(new[] { "runs/2026/one" }, listed);
        Assert.True(deleted);
        Assert.Null(missing);
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
}