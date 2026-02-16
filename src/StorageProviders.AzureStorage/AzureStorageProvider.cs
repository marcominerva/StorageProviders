using System.Runtime.CompilerServices;
using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using MimeMapping;

namespace StorageProviders.AzureStorage;

internal class AzureStorageProvider(AzureStorageSettings settings) : IStorageProvider
{
    private readonly BlobServiceClient blobServiceClient = new(settings.ConnectionString);

    public async Task SaveAsync(string path, Stream stream, IDictionary<string, string>? metadata, bool overwrite, CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientAsync(path, true, cancellationToken).ConfigureAwait(false);

        if (!overwrite)
        {
            var blobExists = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
            if (blobExists)
            {
                throw new IOException($"The file {path} already exists.");
            }
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var options = new BlobUploadOptions
        {
            Metadata = metadata,
            HttpHeaders = new BlobHttpHeaders { ContentType = MimeUtility.GetMimeMapping(path) }
        };

        await blobClient.UploadAsync(stream, options, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream?> ReadAsStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobExists = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
        if (!blobExists)
        {
            return null;
        }

        var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return stream;
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobExists = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
        return blobExists;
    }

    public async Task<StorageFileInfo> GetPropertiesAsync(string path, CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var fileInfo = new StorageFileInfo(string.IsNullOrWhiteSpace(settings.ContainerName) ? $"{blobClient.BlobContainerName}/{blobClient.Name}" : blobClient.Name)
        {
            Length = properties.Value.ContentLength,
            CreatedOn = properties.Value.CreatedOn,
            LastModified = properties.Value.LastModified,
            Metadata = properties.Value.Metadata
        };

        return fileInfo;
    }

    public Task<Uri> GetFullPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ExtractContainerBlobName(path);
        var uri = new Uri(blobServiceClient.Uri, $"{containerName}/{blobName}");
        return Task.FromResult(uri);
    }

    /// <inheritdoc />
    public Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, CancellationToken cancellationToken = default)
        => GetReadAccessUriAsync(path, expirationDate, fileName: null, cancellationToken);

    /// <inheritdoc />
    public Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, string? fileName, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ExtractContainerBlobName(path);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expirationDate)
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
        };

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            sasBuilder.ContentDisposition = CreateContentDispositionHeader(fileName);
        }

        var blobClient = new BlobClient(settings.ConnectionString, containerName, blobName);

        var sharedAccessSignature = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult<Uri?>(sharedAccessSignature);
    }

    public async IAsyncEnumerable<string> EnumerateAsync(string? prefix, IEnumerable<string> extensions, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (containerName, pathPrefix) = ExtractContainerBlobName(prefix);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

        var options = new GetBlobsOptions
        {
            Prefix = pathPrefix
        };

        var list = blobContainerClient.GetBlobsAsync(options, cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken).ConfigureAwait(false);
        await foreach (var blobPage in list)
        {
            foreach (var blob in blobPage.Values.Where(b => !b.Deleted &&
                ((!extensions?.Any() ?? true) || extensions!.Any(e => string.Equals(Path.GetExtension(b.Name), e, StringComparison.InvariantCultureIgnoreCase)))))
            {
                var name = string.IsNullOrWhiteSpace(settings.ContainerName) ? $"{containerName}/{blob.Name}" : blob.Name;
                yield return name;
            }
        }
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ExtractContainerBlobName(path);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

        await blobContainerClient.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> SetMetadataAsync(string path, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        var blobClient = await GetBlobClientAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobExists = await blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
        if (!blobExists)
        {
            return false;
        }

        // Note: Passing null will wipe/clear any existing metadata on the file.
        await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<BlobClient> GetBlobClientAsync(string path, bool createIfNotExists = false, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ExtractContainerBlobName(path);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
        if (createIfNotExists)
        {
            await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var blobClient = blobContainerClient.GetBlobClient(blobName);
        return blobClient;
    }

    private (string ContainerName, string BlobName) ExtractContainerBlobName(string? path)
    {
        path = path?.Replace(@"\", "/") ?? string.Empty;

        // If a relative path and a container name have been provided in the settings, uses them.
        // Otherwise, extracts the first folder name from the path.
        if (!path.StartsWith('/') && !string.IsNullOrWhiteSpace(settings.ContainerName))
        {
            return (settings.ContainerName, path);
        }

        var root = Path.GetPathRoot(path);
        var fileNameWithoutRoot = path[(root ?? string.Empty).Length..];
        var parts = fileNameWithoutRoot.Split('/');

        var containerName = parts.First().ToLowerInvariant();
        var blobName = string.Join('/', parts.Skip(1));

        return (containerName, blobName);
    }

    /// <summary>
    /// Creates a Content-Disposition header value that follows RFC 5987 for proper handling
    /// of international characters and prevents header injection vulnerabilities.
    /// </summary>
    /// <param name="fileName">The file name to include in the Content-Disposition header.</param>
    /// <returns>A properly formatted Content-Disposition header value.</returns>
    /// <remarks>
    /// This method implements RFC 5987/RFC 2231 by providing both a <c>filename</c> parameter
    /// (ASCII fallback) and a <c>filename*</c> parameter (UTF-8 encoded) for international characters.
    /// All control characters (U+0000 to U+001F and U+007F to U+009F) are removed to prevent
    /// header injection attacks.
    /// </remarks>
    private static string CreateContentDispositionHeader(string fileName)
    {
        // Remove all control characters (U+0000 to U+001F and U+007F to U+009F) to prevent header injection.
        // This includes \r, \n, \t, and other potentially dangerous characters.
        var sanitized = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            // Keep only characters that are not control characters
            if (ch is not (>= '\u0000' and <= '\u001F') and not (>= '\u007F' and <= '\u009F'))
            {
                sanitized.Append(ch);
            }
        }

        var sanitizedFileName = sanitized.ToString();

        // Create ASCII fallback: keep only ASCII characters, replace others with underscore
        var asciiFallback = new StringBuilder(sanitizedFileName.Length);
        foreach (var ch in sanitizedFileName)
        {
            // Escape quotes and backslashes first
            if (ch is '"' or '\\')
            {
                asciiFallback.Append('\\');
                asciiFallback.Append(ch);
            }
            // Skip semicolon to prevent header manipulation
            else if (ch == ';')
            {
                continue;
            }
            // Keep printable ASCII characters (space to ~, excluding control chars)
            else if (ch >= 32 && ch <= 126)
            {
                asciiFallback.Append(ch);
            }
            // Replace non-ASCII characters with underscore
            else if (!char.IsAscii(ch))
            {
                asciiFallback.Append('_');
            }
        }

        // RFC 5987 percent-encoding for filename*
        var encodedFileName = new StringBuilder(sanitizedFileName.Length * 3);
        foreach (var ch in sanitizedFileName)
        {
            if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.' or '~')
            {
                // Unreserved characters per RFC 3986 - no encoding needed
                encodedFileName.Append(ch);
            }
            else
            {
                // Percent-encode everything else
                var charBytes = Encoding.UTF8.GetBytes([ch]);
                foreach (var b in charBytes)
                {
                    encodedFileName.Append('%');
                    encodedFileName.Append(b.ToString("X2"));
                }
            }
        }

        // Return Content-Disposition with both filename (ASCII fallback) and filename* (UTF-8)
        return $"attachment; filename=\"{asciiFallback}\"; filename*=UTF-8''{encodedFileName}";
    }
}
