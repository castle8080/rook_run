using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RookRun.ObjectStore;
using RookRun.ObjectStore.Configuration;
using RookRun.ObjectStore.DependencyInjection;

namespace RookRun.UnitTest.ObjectStore;

public class ObjectStoreDependencyInjectionTests
{
    [Fact]
    public void AddObjectStore_RegistersInMemoryStore_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.InMemory)
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var objectStore = serviceProvider.GetRequiredService<IObjectStore>();

        Assert.IsType<InMemoryObjectStore>(objectStore);
    }

    [Fact]
    public void AddObjectStore_RegistersFileSystemStore_WhenConfigured()
    {
        var rootDirectory = Path.Combine(Path.GetTempPath(), $"rookrun-di-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.FileSystem),
                [$"{ObjectStoreOptions.SectionName}:FileSystem:RootDirectory"] = rootDirectory
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var objectStore = serviceProvider.GetRequiredService<IObjectStore>();

        var fileSystemStore = Assert.IsType<FileSystemObjectStore>(objectStore);
        Assert.NotNull(fileSystemStore);
    }

    [Fact]
    public void AddObjectStore_RegistersAzureBlobStore_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.AzureBlob),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:Authentication"] = nameof(AzureBlobAuthenticationType.DefaultAzureCredential),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ServiceUri"] = "https://example.blob.core.windows.net",
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ContainerName"] = "objects",
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:RootPrefix"] = "app/data"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var objectStore = serviceProvider.GetRequiredService<IObjectStore>();

        var azureStore = Assert.IsType<AzureBlobObjectStore>(objectStore);
        Assert.Equal(new Uri("https://example.blob.core.windows.net/objects"), azureStore.ContainerClient.Uri);
        Assert.False(azureStore.ContainerClient.CanGenerateSasUri);
        Assert.Equal("app/data/sample", azureStore.GetBlobName("sample"));
    }

    [Fact]
    public void AddObjectStore_RegistersAzureBlobStore_WithSharedKey_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.AzureBlob),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:Authentication"] = nameof(AzureBlobAuthenticationType.SharedKey),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ServiceUri"] = "https://example.blob.core.windows.net",
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ContainerName"] = "objects",
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:AccountName"] = "example",
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:AccountKey"] = "ZmFrZWtleQ=="
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();
        var objectStore = serviceProvider.GetRequiredService<IObjectStore>();

        var azureStore = Assert.IsType<AzureBlobObjectStore>(objectStore);
        Assert.Equal(new Uri("https://example.blob.core.windows.net/objects"), azureStore.ContainerClient.Uri);
        Assert.True(azureStore.ContainerClient.CanGenerateSasUri);
    }

    [Fact]
    public void AddObjectStore_FileSystemStore_ThrowsWhenRootDirectoryMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.FileSystem)
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<IObjectStore>());
        Assert.Contains("ObjectStore:FileSystem:RootDirectory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddObjectStore_AzureBlobStore_ThrowsWhenRequiredOptionsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.AzureBlob),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ServiceUri"] = "https://example.blob.core.windows.net"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<IObjectStore>());
        Assert.Contains("ObjectStore:AzureBlob:ContainerName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddObjectStore_AzureBlobStore_SharedKey_ThrowsWhenSharedKeyOptionsMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObjectStoreOptions.SectionName}:Type"] = nameof(ObjectStoreType.AzureBlob),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:Authentication"] = nameof(AzureBlobAuthenticationType.SharedKey),
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ServiceUri"] = "https://example.blob.core.windows.net",
                [$"{ObjectStoreOptions.SectionName}:AzureBlob:ContainerName"] = "objects"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddObjectStore(configuration.GetSection(ObjectStoreOptions.SectionName));

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<IObjectStore>());
        Assert.Contains("ObjectStore:AzureBlob:AccountName", exception.Message, StringComparison.Ordinal);
    }
}