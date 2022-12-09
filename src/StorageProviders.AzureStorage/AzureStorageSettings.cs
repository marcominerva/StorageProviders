namespace StorageProviders.AzureStorage;

public class AzureStorageSettings
{
    public string ConnectionString { get; set; } = null!;

    private string? containerName;
    public string? ContainerName
    {
        get => containerName;
        set => containerName = value?.ToLowerInvariant();
    }
}
