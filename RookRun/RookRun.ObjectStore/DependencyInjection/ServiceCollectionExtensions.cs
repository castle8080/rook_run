using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RookRun.ObjectStore.Configuration;

namespace RookRun.ObjectStore.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddObjectStore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ObjectStoreOptions>()
            .Bind(configuration.GetSection("ObjectStore"));

        services.AddSingleton<ObjectStoreJsonOptions>();
        services.AddSingleton<IObjectStore>(static serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ObjectStoreOptions>>().Value;
            var jsonOptions = serviceProvider.GetRequiredService<ObjectStoreJsonOptions>();

            return options.Type switch
            {
                ObjectStoreType.InMemory => new InMemoryObjectStore(jsonOptions),
                ObjectStoreType.FileSystem => CreateFileSystemObjectStore(options, jsonOptions),
                ObjectStoreType.AzureBlob => CreateAzureBlobObjectStore(options, jsonOptions),
                _ => throw new InvalidOperationException($"Unsupported object store type '{options.Type}'.")
            };
        });

        return services;
    }

    private static FileSystemObjectStore CreateFileSystemObjectStore(ObjectStoreOptions options, ObjectStoreJsonOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(options.FileSystem.RootDirectory))
        {
            throw new InvalidOperationException("ObjectStore:FileSystem:RootDirectory must be configured when using the FileSystem object store.");
        }

        return new FileSystemObjectStore(options.FileSystem.RootDirectory, jsonOptions);
    }

    private static AzureBlobObjectStore CreateAzureBlobObjectStore(ObjectStoreOptions options, ObjectStoreJsonOptions jsonOptions)
    {
        if (string.IsNullOrWhiteSpace(options.AzureBlob.ServiceUri))
        {
            throw new InvalidOperationException("ObjectStore:AzureBlob:ServiceUri must be configured when using the AzureBlob object store.");
        }

        if (string.IsNullOrWhiteSpace(options.AzureBlob.ContainerName))
        {
            throw new InvalidOperationException("ObjectStore:AzureBlob:ContainerName must be configured when using the AzureBlob object store.");
        }

        var serviceClient = CreateBlobServiceClient(options.AzureBlob);
        var containerClient = serviceClient.GetBlobContainerClient(options.AzureBlob.ContainerName);
        return new AzureBlobObjectStore(containerClient, jsonOptions, options.AzureBlob.RootPrefix);
    }

    private static BlobServiceClient CreateBlobServiceClient(AzureBlobObjectStoreOptions options)
    {
        var serviceUri = new Uri(options.ServiceUri);

        return options.Authentication switch
        {
            AzureBlobAuthenticationType.DefaultAzureCredential => new BlobServiceClient(serviceUri, new DefaultAzureCredential()),
            AzureBlobAuthenticationType.SharedKey => new BlobServiceClient(serviceUri, CreateSharedKeyCredential(options)),
            _ => throw new InvalidOperationException($"Unsupported Azure Blob authentication type '{options.Authentication}'.")
        };
    }

    private static StorageSharedKeyCredential CreateSharedKeyCredential(AzureBlobObjectStoreOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AccountName))
        {
            throw new InvalidOperationException("ObjectStore:AzureBlob:AccountName must be configured when using shared key authentication.");
        }

        if (string.IsNullOrWhiteSpace(options.AccountKey))
        {
            throw new InvalidOperationException("ObjectStore:AzureBlob:AccountKey must be configured when using shared key authentication.");
        }

        return new StorageSharedKeyCredential(options.AccountName, options.AccountKey);
    }
}