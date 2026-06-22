namespace StorageProviders;

/// <summary>
/// Defines the storage operations that provider implementations expose so application code can work with files without depending on a specific backing service.
/// </summary>
/// <remarks>
/// Implementations translate these operations to storage systems such as cloud object stores, file shares, or other durable stores while preserving consistent behavior for callers.
/// Paths are provider-defined logical locations; callers should not assume they map to local file-system paths unless a provider documents that behavior.
/// </remarks>
public interface IStorageProvider
{
    /// <summary>
    /// Saves binary content to a logical storage path with optional provider-specific metadata.
    /// </summary>
    /// <param name="path">The provider-defined destination path for the stored content.</param>
    /// <param name="content">The bytes to persist.</param>
    /// <param name="metadata">Optional key/value metadata to associate with the stored object, or <see langword="null" /> when no metadata is required.</param>
    /// <param name="overwrite"><see langword="true" /> to replace an existing object at <paramref name="path" />; <see langword="false" /> to let the provider protect existing content.</param>
    /// <param name="cancellationToken">A token that can cancel the save operation.</param>
    /// <returns>A task that completes when the content has been accepted by the provider.</returns>
    async Task SaveAsync(string path, byte[] content, IDictionary<string, string>? metadata, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await SaveAsync(path, stream, metadata, overwrite, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves stream content to a logical storage path with optional provider-specific metadata.
    /// </summary>
    /// <param name="path">The provider-defined destination path for the stored content.</param>
    /// <param name="stream">The readable stream whose current position is used as the start of the content to persist.</param>
    /// <param name="metadata">Optional key/value metadata to associate with the stored object, or <see langword="null" /> when no metadata is required.</param>
    /// <param name="overwrite"><see langword="true" /> to replace an existing object at <paramref name="path" />; <see langword="false" /> to let the provider protect existing content.</param>
    /// <param name="cancellationToken">A token that can cancel the save operation.</param>
    /// <returns>A task that completes when the content has been accepted by the provider.</returns>
    Task SaveAsync(string path, Stream stream, IDictionary<string, string>? metadata, bool overwrite = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves binary content to a logical storage path when no metadata needs to be supplied.
    /// </summary>
    /// <param name="path">The provider-defined destination path for the stored content.</param>
    /// <param name="content">The bytes to persist.</param>
    /// <param name="overwrite"><see langword="true" /> to replace an existing object at <paramref name="path" />; <see langword="false" /> to let the provider protect existing content.</param>
    /// <param name="cancellationToken">A token that can cancel the save operation.</param>
    /// <returns>A task that completes when the content has been accepted by the provider.</returns>
    async Task SaveAsync(string path, byte[] content, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await SaveAsync(path, stream, overwrite, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves stream content to a logical storage path when no metadata needs to be supplied.
    /// </summary>
    /// <param name="path">The provider-defined destination path for the stored content.</param>
    /// <param name="stream">The readable stream whose current position is used as the start of the content to persist.</param>
    /// <param name="overwrite"><see langword="true" /> to replace an existing object at <paramref name="path" />; <see langword="false" /> to let the provider protect existing content.</param>
    /// <param name="cancellationToken">A token that can cancel the save operation.</param>
    /// <returns>A task that completes when the content has been accepted by the provider.</returns>
    Task SaveAsync(string path, Stream stream, bool overwrite = false, CancellationToken cancellationToken = default)
        => SaveAsync(path, stream, null, overwrite, cancellationToken);

    /// <summary>
    /// Opens stored content for reading without forcing providers to buffer the entire object in memory.
    /// </summary>
    /// <param name="path">The provider-defined path of the stored object to read.</param>
    /// <param name="cancellationToken">A token that can cancel the read operation before the stream is returned.</param>
    /// <returns>A readable stream for the object at <paramref name="path" />, or <see langword="null" /> when the object does not exist.</returns>
    Task<Stream?> ReadAsStreamAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads stored content into memory for callers that need a byte-array representation.
    /// </summary>
    /// <param name="path">The provider-defined path of the stored object to read.</param>
    /// <param name="cancellationToken">A token that can cancel the read operation.</param>
    /// <returns>The object content as a byte array, or <see langword="null" /> when the object does not exist.</returns>
    async Task<byte[]?> ReadAsByteArrayAsync(string path, CancellationToken cancellationToken = default)
    {
        using var stream = await ReadAsStreamAsync(path, cancellationToken).ConfigureAwait(false);
        if (stream is not null)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            return memoryStream.ToArray();
        }

        return null;
    }

    /// <summary>
    /// Resolves a provider-defined path to the absolute <see cref="Uri" /> used to address the stored object.
    /// </summary>
    /// <param name="path">The provider-defined path to resolve.</param>
    /// <param name="cancellationToken">A token that can cancel the path resolution operation.</param>
    /// <returns>The absolute <see cref="Uri" /> that identifies <paramref name="path" /> for the current provider.</returns>
    Task<Uri> GetFullPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a temporary read-access <see cref="Uri" /> for a stored object when the provider supports delegated access.
    /// </summary>
    /// <param name="path">The provider-defined path of the object to expose for reading.</param>
    /// <param name="expirationDate">The date and time after which the returned URI should no longer grant access.</param>
    /// <param name="cancellationToken">A token that can cancel the URI creation operation.</param>
    /// <returns>A read-access <see cref="Uri" /> for the object, or <see langword="null" /> when delegated access is not supported by the provider.</returns>
    Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, CancellationToken cancellationToken = default)
        => GetReadAccessUriAsync(path, expirationDate, fileName: null, cancellationToken);

    /// <summary>
    /// Creates a temporary read-access <see cref="Uri" /> and optionally suggests a download file name when the provider supports it.
    /// </summary>
    /// <param name="path">The provider-defined path of the object to expose for reading.</param>
    /// <param name="expirationDate">The date and time after which the returned URI should no longer grant access.</param>
    /// <param name="fileName">The optional download name to request from the provider, or <see langword="null" /> to keep the provider's default naming behavior.</param>
    /// <param name="cancellationToken">A token that can cancel the URI creation operation.</param>
    /// <returns>A read-access <see cref="Uri" /> for the object, or <see langword="null" /> when delegated access is not supported by the provider.</returns>
    Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, string? fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an object exists at a provider-defined path before callers attempt operations that require it.
    /// </summary>
    /// <param name="path">The provider-defined path to check.</param>
    /// <param name="cancellationToken">A token that can cancel the existence check.</param>
    /// <returns><see langword="true" /> when an object exists at <paramref name="path" />; otherwise, <see langword="false" />.</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates stored object paths using optional prefix and extension filters.
    /// </summary>
    /// <param name="prefix">An optional provider-defined path prefix used to narrow the enumeration scope.</param>
    /// <param name="extensions">Optional file extensions used to include only matching objects.</param>
    /// <returns>An asynchronous sequence of provider-defined paths that match the supplied filters.</returns>
    IAsyncEnumerable<string> EnumerateAsync(string? prefix = null, params IEnumerable<string> extensions)
        => EnumerateAsync(prefix, extensions, CancellationToken.None);

    /// <summary>
    /// Enumerates stored object paths using optional prefix and extension filters while allowing cancellation during traversal.
    /// </summary>
    /// <param name="prefix">An optional provider-defined path prefix used to narrow the enumeration scope.</param>
    /// <param name="extensions">Optional file extensions used to include only matching objects.</param>
    /// <param name="cancellationToken">A token that can cancel enumeration while the provider is scanning storage.</param>
    /// <returns>An asynchronous sequence of provider-defined paths that match the supplied filters.</returns>
    IAsyncEnumerable<string> EnumerateAsync(string? prefix, IEnumerable<string> extensions, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the object at the specified provider-defined path.
    /// </summary>
    /// <param name="path">The provider-defined path of the object to remove.</param>
    /// <param name="cancellationToken">A token that can cancel the delete operation.</param>
    /// <returns>A task that completes when the provider has processed the delete request.</returns>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves provider-neutral information about stored content so callers can inspect metadata without reading the object body.
    /// </summary>
    /// <param name="path">The provider-defined path of the object whose properties should be retrieved.</param>
    /// <param name="cancellationToken">A token that can cancel the property retrieval operation.</param>
    /// <returns>A <see cref="StorageFileInfo" /> instance describing the stored object.</returns>
    Task<StorageFileInfo> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces or clears the provider-specific metadata associated with a stored object.
    /// </summary>
    /// <param name="path">The provider-defined path of the object whose metadata should be updated.</param>
    /// <param name="metadata">The metadata to apply, or <see langword="null" /> when metadata should be cleared or omitted according to provider behavior.</param>
    /// <param name="cancellationToken">A token that can cancel the metadata update operation.</param>
    /// <returns>A task that completes when the provider has processed the metadata update.</returns>
    Task SetMetadataAsync(string path, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}
