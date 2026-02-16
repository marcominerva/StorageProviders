namespace StorageProviders;

public interface IStorageProvider
{
    async Task SaveAsync(string path, byte[] content, IDictionary<string, string>? metadata, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await SaveAsync(path, stream, metadata, overwrite, cancellationToken).ConfigureAwait(false);
    }

    Task SaveAsync(string path, Stream stream, IDictionary<string, string>? metadata, bool overwrite = false, CancellationToken cancellationToken = default);

    async Task SaveAsync(string path, byte[] content, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await SaveAsync(path, stream, overwrite, cancellationToken).ConfigureAwait(false);
    }

    Task SaveAsync(string path, Stream stream, bool overwrite = false, CancellationToken cancellationToken = default)
        => SaveAsync(path, stream, null, overwrite, cancellationToken);

    Task<Stream?> ReadAsStreamAsync(string path, CancellationToken cancellationToken = default);

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

    Task<Uri> GetFullPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a URI with read access to the specified path that expires at the given date.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="expirationDate">The expiration date for the read access URI.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A URI with read access to the file, or <see langword="null" /> if the operation is not supported.</returns>
    Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, CancellationToken cancellationToken = default)
        => GetReadAccessUriAsync(path, expirationDate, fileName: null, cancellationToken);

    /// <summary>
    /// Gets a URI with read access to the specified path that expires at the given date, optionally specifying a file name for the download.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="expirationDate">The expiration date for the read access URI.</param>
    /// <param name="fileName">The optional file name to use for the download. If provided and supported by the provider, the Content-Disposition header will be set to suggest this file name to the browser. If <see langword="null" /> or not supported, the default file name from the storage path will be used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A URI with read access to the file, or <see langword="null" /> if the operation is not supported.</returns>
    Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, string? fileName, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> EnumerateAsync(string? prefix = null, params IEnumerable<string> extensions)
        => EnumerateAsync(prefix, extensions, CancellationToken.None);

    IAsyncEnumerable<string> EnumerateAsync(string? prefix, IEnumerable<string> extensions, CancellationToken cancellationToken);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    Task<StorageFileInfo> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);

    Task SetMetadataAsync(string path, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
}
