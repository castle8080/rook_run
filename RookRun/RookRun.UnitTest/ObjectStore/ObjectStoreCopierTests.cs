using RookRun.ObjectStore;

namespace RookRun.UnitTest.ObjectStore;

public class ObjectStoreCopierTests
{
    [Fact]
    public async Task CopyAsync_CopiesAllObjectsFromSourceToTarget()
    {
        var source = new InMemoryObjectStore();
        var target = new InMemoryObjectStore();

        await source.StoreObjectAsync("a/one", new ObjectStoreTestRecord { Name = "one", Count = 1 }, overwrite: false);
        await source.StoreObjectAsync("a/two", new ObjectStoreTestRecord { Name = "two", Count = 2 }, overwrite: false);
        await source.StoreObjectAsync("b/three", new ObjectStoreTestRecord { Name = "three", Count = 3 }, overwrite: false);

        var copier = new ObjectStoreCopier(source, target, maxParallelism: 2);
        var copied = await copier.CopyAsync();

        Assert.Equal(3, copied);

        var one = await target.TryReadObjectAsync<ObjectStoreTestRecord>("a/one");
        var two = await target.TryReadObjectAsync<ObjectStoreTestRecord>("a/two");
        var three = await target.TryReadObjectAsync<ObjectStoreTestRecord>("b/three");

        Assert.True(one.IsFound);
        Assert.Equal("one", one.Value!.Name);
        Assert.Equal(1, one.Value.Count);

        Assert.True(two.IsFound);
        Assert.Equal("two", two.Value!.Name);

        Assert.True(three.IsFound);
        Assert.Equal("three", three.Value!.Name);
    }

    [Fact]
    public async Task CopyAsync_ReturnsZero_WhenSourceIsEmpty()
    {
        var source = new InMemoryObjectStore();
        var target = new InMemoryObjectStore();

        var copier = new ObjectStoreCopier(source, target);
        var copied = await copier.CopyAsync();

        Assert.Equal(0, copied);

        var listed = await target.ListObjectsAsync(string.Empty);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task CopyAsync_OverwritesExistingObjectsInTarget()
    {
        var source = new InMemoryObjectStore();
        var target = new InMemoryObjectStore();

        await source.StoreObjectAsync("items/one", new ObjectStoreTestRecord { Name = "updated", Count = 99 }, overwrite: false);
        await target.StoreObjectAsync("items/one", new ObjectStoreTestRecord { Name = "original", Count = 1 }, overwrite: false);

        var copier = new ObjectStoreCopier(source, target);
        await copier.CopyAsync();

        var result = await target.TryReadObjectAsync<ObjectStoreTestRecord>("items/one");

        Assert.True(result.IsFound);
        Assert.Equal("updated", result.Value!.Name);
        Assert.Equal(99, result.Value.Count);
    }

    [Fact]
    public async Task CopyAsync_DoesNotModifySource()
    {
        var source = new InMemoryObjectStore();
        var target = new InMemoryObjectStore();

        await source.StoreObjectAsync("data/item", new ObjectStoreTestRecord { Name = "original", Count = 7 }, overwrite: false);

        var copier = new ObjectStoreCopier(source, target);
        await copier.CopyAsync();

        var sourcePaths = await source.ListObjectsAsync(string.Empty);
        Assert.Equal(new[] { "data/item" }, sourcePaths);
    }

    [Fact]
    public void Constructor_ThrowsOnNullSource()
    {
        var target = new InMemoryObjectStore();
        Assert.Throws<ArgumentNullException>(() => new ObjectStoreCopier(null!, target));
    }

    [Fact]
    public void Constructor_ThrowsOnNullTarget()
    {
        var source = new InMemoryObjectStore();
        Assert.Throws<ArgumentNullException>(() => new ObjectStoreCopier(source, null!));
    }

    [Fact]
    public void Constructor_ThrowsWhenMaxParallelismIsZero()
    {
        var source = new InMemoryObjectStore();
        var target = new InMemoryObjectStore();
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObjectStoreCopier(source, target, maxParallelism: 0));
    }
}
