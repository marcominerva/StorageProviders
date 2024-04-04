using System.Runtime.CompilerServices;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using MimeMapping;

namespace StorageProviders.AzureStorage;

internal class AzureStorageProvider(AzureStorageSettings settings) : IStorageProvider
{
    private readonly BlobServiceClient blobServiceClient = new(settings.ConnectionString);

    public async Task SaveAsync(string path, Stream stream, bool overwrite = false, CancellationToken cancellationToken = default)
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

        await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = MimeUtility.GetMimeMapping(path) }, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    public Task<Uri?> GetReadAccessUriAsync(string path, DateTime expirationDate, CancellationToken cancellationToken = default)
    {
        var (containerName, blobName) = ExtractContainerBlobName(path);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expirationDate)
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
        };

        var blobClient = new BlobClient(settings.ConnectionString, containerName, blobName);

        var sharedAccessSignature = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult<Uri?>(sharedAccessSignature);
    }

    public async IAsyncEnumerable<string> EnumerateAsync(string? prefix, string[] extensions, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (containerName, pathPrefix) = ExtractContainerBlobName(prefix);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

        var list = blobContainerClient.GetBlobsAsync(prefix: pathPrefix, cancellationToken: cancellationToken).AsPages().WithCancellation(cancellationToken).ConfigureAwait(false);
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
        if (!path.StartsWith("/") && !string.IsNullOrWhiteSpace(settings.ContainerName))
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
}
