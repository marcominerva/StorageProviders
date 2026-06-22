using MimeMapping;

namespace StorageProviders;

/// <summary>
/// Represents provider-neutral file details returned by <see cref="IStorageProvider.GetPropertiesAsync(string, CancellationToken)" />.
/// </summary>
/// <param name="name">The provider-defined object name used to infer values such as <see cref="ContentType" />.</param>
public class StorageFileInfo(string name)
{
    /// <summary>
    /// Gets the provider-defined object name used by callers to display or identify the stored file.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the MIME type inferred from <see cref="Name" />.
    /// </summary>
    public string ContentType { get; } = MimeUtility.GetMimeMapping(name);

    /// <summary>
    /// Gets or sets the last time the provider reports that the stored object was modified.
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Gets or sets the time the provider reports that the stored object was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>
    /// Gets or sets the object length in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata associated with the stored object.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
