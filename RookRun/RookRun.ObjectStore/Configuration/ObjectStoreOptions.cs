namespace RookRun.ObjectStore.Configuration;

public sealed class ObjectStoreOptions
{
    public const string SectionName = "ObjectStore";

    public ObjectStoreType Type { get; set; } = ObjectStoreType.InMemory;

    public FileSystemObjectStoreOptions FileSystem { get; set; } = new();

    public AzureBlobObjectStoreOptions AzureBlob { get; set; } = new();
}