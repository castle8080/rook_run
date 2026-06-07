using System.Text.Json;

namespace RookRun.ObjectStore;

public sealed class ObjectStoreJsonOptions
{
    public JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}