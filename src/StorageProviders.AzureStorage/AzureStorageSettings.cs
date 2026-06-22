namespace StorageProviders.AzureStorage;

/// <summary>
/// Contains the Azure Blob Storage connection details required by <see cref="AzureStorageProvider" />.
/// </summary>
/// <remarks>
/// The connection string selects the storage account, and the optional container name controls whether paths are interpreted relative to a fixed container or include the container as their first segment.
/// </remarks>
public class AzureStorageSettings
{
    /// <summary>
    /// Gets or sets the Azure Storage connection string used to create Azure Blob Storage clients.
    /// </summary>
    /// <remarks>
    /// This value is required because the provider uses it both for blob operations and for generating shared access signature URIs.
    /// Prefer supplying it from secure configuration sources rather than hard-coding secrets in application code.
    /// </remarks>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Gets or sets the default blob container name used when caller-supplied paths should be interpreted relative to a single container.
    /// </summary>
    /// <remarks>
    /// When this value is <see langword="null" /> or whitespace, provider paths must include the container name as the first path segment.
    /// The value is normalized to lowercase to match Azure Blob Storage container naming requirements.
    /// </remarks>
    public string? ContainerName
    {
        get;
        set => field = value?.ToLowerInvariant();
    }
}
