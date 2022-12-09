namespace StorageProviders;

public interface IStorageProvider
{
    async Task SaveAsync(string path, byte[] content, bool overwrite = false)
    {
        using var stream = new MemoryStream(content);
        await SaveAsync(path, stream, overwrite).ConfigureAwait(false);
    }

    Task SaveAsync(string path, Stream stream, bool overwrite = false);

    Task<Stream?> ReadAsStreamAsync(string path);

    async Task<byte[]?> ReadAsByteArrayAsync(string path)
    {
        using var stream = await ReadAsStreamAsync(path).ConfigureAwait(false);
        if (stream is not null)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
            return memoryStream.ToArray();
        }

        return null;
    }

    Task<Uri> GetFullPathAsync(string path);

    Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate);

    Task<bool> ExistsAsync(string path);

    IAsyncEnumerable<string> EnumerateAsync(string? prefix = null, params string[] extensions);

    Task DeleteAsync(string path);
}
