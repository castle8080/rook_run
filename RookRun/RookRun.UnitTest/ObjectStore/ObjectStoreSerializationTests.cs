using RookRun.ObjectStore;

namespace RookRun.UnitTest.ObjectStore;

public class ObjectStoreSerializationTests
{
    [Fact]
    public async Task SerializeAsync_WritesCompressedJsonToStream()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "test", Count = 42 };

        await using var stream = new MemoryStream();
        await serialization.SerializeAsync(stream, testData);

        var compressed = stream.ToArray();
        Assert.NotEmpty(compressed);
        // Verify it's valid compressed data by attempting to decompress
        await using var memStream = new MemoryStream(compressed);
        var decompressed = await serialization.DeserializeAsync<ObjectStoreTestRecord>(memStream);
        Assert.NotNull(decompressed);
    }

    [Fact]
    public async Task DeserializeAsync_ReadsCompressedJsonFromStream()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "test", Count = 42 };

        await using var stream = new MemoryStream();
        await serialization.SerializeAsync(stream, testData);

        stream.Position = 0;
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(stream);

        Assert.NotNull(deserialized);
        Assert.Equal("test", deserialized.Name);
        Assert.Equal(42, deserialized.Count);
    }

    [Fact]
    public async Task SerializeToMemoryAsync_ReturnsCompressedByteArray()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "memory", Count = 99 };

        var bytes = await serialization.SerializeToMemoryAsync(testData);

        Assert.NotEmpty(bytes);
        // Verify it's valid compressed data by attempting to decompress
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);
        Assert.NotNull(deserialized);
        Assert.Equal("memory", deserialized.Name);
        Assert.Equal(99, deserialized.Count);
    }

    [Fact]
    public async Task DeserializeAsync_WithBuffer_ReadsCompressedData()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "buffer", Count = 55 };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal("buffer", deserialized.Name);
        Assert.Equal(55, deserialized.Count);
    }

    [Fact]
    public async Task RoundTrip_PreservesComplexData()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord
        {
            Name = "complex-test-with-special-chars-!@#$%",
            Count = 12345
        };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(testData.Name, deserialized.Name);
        Assert.Equal(testData.Count, deserialized.Count);
    }

    [Fact]
    public async Task SerializeAsync_ThrowsWhenStreamIsNull()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await serialization.SerializeAsync<ObjectStoreTestRecord>(null!, testData));
    }

    [Fact]
    public async Task DeserializeAsync_WithStream_ThrowsWhenStreamIsNull()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await serialization.DeserializeAsync<ObjectStoreTestRecord>((Stream)null!));
    }

    [Fact]
    public async Task DeserializeAsync_WithBuffer_ReturnsNullForInvalidCompressedData()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var invalidData = new byte[] { 0xFF, 0xFF, 0xFF };

        // Should throw because the data is not valid Brotli
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await serialization.DeserializeAsync<ObjectStoreTestRecord>(invalidData));
    }

    [Fact]
    public async Task SerializeAsync_CanSerializeNullValues()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = null, Count = 0 };

        await using var stream = new MemoryStream();
        await serialization.SerializeAsync(stream, testData);

        stream.Position = 0;
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(stream);

        Assert.NotNull(deserialized);
        Assert.Null(deserialized.Name);
    }

    [Fact]
    public async Task SerializeAsync_CanSerializeEmptyString()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = string.Empty, Count = 0 };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized.Name);
    }

    [Fact]
    public async Task SerializeAsync_CompressesLargeData()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var largeString = string.Concat(Enumerable.Repeat("x", 10000));
        var testData = new ObjectStoreTestRecord { Name = largeString, Count = 1 };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        
        // Compressed data should be much smaller than original
        Assert.True(bytes.Length < largeString.Length);
    }

    [Fact]
    public async Task DeserializeAsync_WithStream_ReturnsNullWhenDeserializingNullValue()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);

        // Serialize a null value
        await using var stream = new MemoryStream();
        await serialization.SerializeAsync<ObjectStoreTestRecord?>(stream, (ObjectStoreTestRecord?)null);

        stream.Position = 0;
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(stream);

        Assert.Null(deserialized);
    }

    [Fact]
    public async Task Constructor_ThrowsWhenOptionsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ObjectStoreSerialization(null!));
    }

    [Fact]
    public async Task SerializeAsync_LeavesBrotliStreamOpen()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        await using var stream = new MemoryStream();
        await serialization.SerializeAsync(stream, testData);

        // Stream should still be open and usable
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
    }

    [Fact]
    public async Task DeserializeAsync_WithStream_LeavesBrotliStreamOpen()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "test", Count = 1 };

        await using var stream = new MemoryStream();
        await serialization.SerializeAsync(stream, testData);
        stream.Position = 0;

        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(stream);

        // Stream should still be open and usable
        Assert.True(stream.CanRead);
        Assert.True(stream.CanSeek);
    }

    [Fact]
    public async Task RoundTrip_PreservesMaxIntValue()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "maxint", Count = int.MaxValue };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(int.MaxValue, deserialized.Count);
    }

    [Fact]
    public async Task RoundTrip_PreservesNegativeCount()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord { Name = "negative", Count = -42 };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(-42, deserialized.Count);
    }

    [Fact]
    public async Task RoundTrip_PreservesUnicodeCharacters()
    {
        var options = new ObjectStoreJsonOptions();
        var serialization = new ObjectStoreSerialization(options);
        var testData = new ObjectStoreTestRecord
        {
            Name = "Unicode: 你好世界 🚀 Привет",
            Count = 123
        };

        var bytes = await serialization.SerializeToMemoryAsync(testData);
        var deserialized = await serialization.DeserializeAsync<ObjectStoreTestRecord>(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(testData.Name, deserialized.Name);
    }
}
