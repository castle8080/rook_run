using Azure.Storage.Blobs;
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
}