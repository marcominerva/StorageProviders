namespace StorageProviders;

public interface IStorageProvider
{
    async Task SaveAsync(string path, byte[] content, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content);
        await SaveAsync(path, stream, overwrite, cancellationToken).ConfigureAwait(false);
    }

    Task SaveAsync(string path, Stream stream, bool overwrite = false, CancellationToken cancellationToken = default);

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

    Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> EnumerateAsync(string? prefix = null, params IEnumerable<string> extensions)
        => EnumerateAsync(prefix, extensions, CancellationToken.None);

    IAsyncEnumerable<string> EnumerateAsync(string? prefix, IEnumerable<string> extensions, CancellationToken cancellationToken);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    Task<StorageFileInfo> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);
}
