using System.Text.Json.Serialization;

namespace StorageProviders.FileSystem;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(FileSystemMetadata))]
internal sealed partial class FileSystemJsonSerializerContext : JsonSerializerContext;

internal sealed class FileSystemMetadata : Dictionary<string, string?>
{
    public FileSystemMetadata()
    {
    }

    public FileSystemMetadata(IDictionary<string, string?> metadata)
        : base(metadata)
    {
    }
}