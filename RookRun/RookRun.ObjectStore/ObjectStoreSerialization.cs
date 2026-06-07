using System.IO.Compression;
using System.Text.Json;

namespace RookRun.ObjectStore;

internal sealed class ObjectStoreSerialization
{
    private readonly JsonSerializerOptions serializerOptions;

    public ObjectStoreSerialization(ObjectStoreJsonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        serializerOptions = options.SerializerOptions;
    }

    public async Task SerializeAsync<T>(Stream output, T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        await using var brotliStream = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true);
        await JsonSerializer.SerializeAsync(brotliStream, value, serializerOptions, cancellationToken);
    }

    public async Task<T?> DeserializeAsync<T>(Stream input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var brotliStream = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true);
        return await JsonSerializer.DeserializeAsync<T>(brotliStream, serializerOptions, cancellationToken);
    }

    public async Task<byte[]> SerializeToMemoryAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        await using var output = new MemoryStream();
        await SerializeAsync(output, value, cancellationToken);
        return output.ToArray();
    }

    public async Task<T?> DeserializeAsync<T>(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await using var input = new MemoryStream(buffer.ToArray(), writable: false);
        return await DeserializeAsync<T>(input, cancellationToken);
    }
}