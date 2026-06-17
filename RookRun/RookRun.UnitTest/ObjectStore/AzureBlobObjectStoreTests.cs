using Azure.Storage.Blobs;
using RookRun.Common.Exceptions;
using RookRun.ObjectStore;

namespace RookRun.UnitTest.ObjectStore;

public class AzureBlobObjectStoreTests
{
    [Fact]
    public void Constructor_NormalizesRootPrefix_IntoBlobNames()
    {
        var containerClient = new BlobContainerClient(new Uri("https://example.test/container"));
        var store = new AzureBlobObjectStore(containerClient, rootPrefix: "/app/root/");

        var blobName = store.GetBlobName("runs/2026/one");

        Assert.Equal("app/root/runs/2026/one", blobName);
    }

    [Fact]
    public void GetBlobName_NormalizesPathSeparators()
    {
        var containerClient = new BlobContainerClient(new Uri("https://example.test/container"));
        var store = new AzureBlobObjectStore(containerClient, rootPrefix: "data");

        var blobName = store.GetBlobName("runs\\2026\\one");

        Assert.Equal("data/runs/2026/one", blobName);
    }

    [Fact]
    public async Task StoreStreamAsync_RejectsConflictingSemantics_OverwriteFalseWithETag()
    {
        var containerClient = new BlobContainerClient(new Uri("https://example.test/container"));
        var store = new AzureBlobObjectStore(containerClient);
        var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => store.StoreStreamAsync("runs/item", content, overwrite: false, ifMatchETag: "test-etag")
        );

        Assert.Contains("overwrite", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}