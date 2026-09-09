namespace StorageProviders.FileSystem;

/// <summary>
/// Contains the local file-system settings required by <see cref="FileSystemStorageProvider" />.
/// </summary>
/// <remarks>
/// The configured directory is the security boundary for all logical storage paths handled by the provider.
/// </remarks>
public sealed class FileSystemStorageSettings
{
    /// <summary>
    /// Gets or sets the absolute or application-relative directory under which files are stored.
    /// </summary>
    /// <remarks>
    /// The provider creates this directory when it does not exist and rejects paths that resolve outside it.
    /// </remarks>
    public string RootDirectory { get; set; } = null!;
}
