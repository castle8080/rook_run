using Microsoft.Extensions.Logging.Abstractions;
using RookRun.Job;
using RookRun.ObjectStore;
using RookRun.UnitTest.ObjectStore;

namespace RookRun.UnitTest.Job;

/// <summary>
/// Tests for <see cref="CopyObjectStoreJob"/>.
/// </summary>
[Collection("JobTests")]
public sealed class CopyObjectStoreJobTests
{
    /// <summary>
    /// Verifies constructor guard clauses for required dependencies.
    /// </summary>
    [Fact]
    public void Constructor_ThrowsWhenDependenciesAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CopyObjectStoreJob(null!, NullLogger<CopyObjectStoreJob>.Instance));
        Assert.Throws<ArgumentNullException>(() => new CopyObjectStoreJob(new InMemoryObjectStore(), null!));
    }

    /// <summary>
    /// Verifies the job copies stored objects into the target file-system store.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CopiesAllObjectsToTargetDirectory()
    {
        var source = new InMemoryObjectStore();
        await source.StoreObjectAsync("a/one.json.br", new ObjectStoreTestRecord { Name = "one", Count = 1 }, overwrite: true);
        await source.StoreObjectAsync("b/two.json.br", new ObjectStoreTestRecord { Name = "two", Count = 2 }, overwrite: true);

        var originalDirectory = Environment.CurrentDirectory;
        var tempRoot = Path.Combine(Path.GetTempPath(), "rookrun-copy-job-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            Environment.CurrentDirectory = tempRoot;
            var job = new CopyObjectStoreJob(source, NullLogger<CopyObjectStoreJob>.Instance);

            await job.ExecuteAsync(CancellationToken.None);

            var targetStore = new FileSystemObjectStore(Path.Combine(tempRoot, "var", "object-store-copy"));

            var first = await targetStore.TryReadObjectAsync<ObjectStoreTestRecord>("a/one.json.br");
            var second = await targetStore.TryReadObjectAsync<ObjectStoreTestRecord>("b/two.json.br");

            Assert.True(first.IsFound);
            Assert.True(second.IsFound);
            Assert.Equal("one", first.Value!.Name);
            Assert.Equal(2, second.Value!.Count);
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;

            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
