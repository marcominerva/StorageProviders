﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using AzureStorageProvider.Exceptions;
using Microsoft.Extensions.Options;
using MimeMapping;

namespace AzureStorageProvider
{
    public class AzureStorageProvider : IAzureStorageProvider
    {
        private readonly AzureStorageSettings settings;
        private readonly BlobServiceClient blobServiceClient;

        public AzureStorageProvider(IOptions<AzureStorageSettings> settingsOptions)
        {
            settings = settingsOptions.Value;
            blobServiceClient = new BlobServiceClient(settings.ConnectionString);
        }

        public async Task SaveAsync(string path, Stream stream, bool overwrite = false)
        {
            var blobClient = await GetBlobClientAsync(path, true).ConfigureAwait(false);

            if (!overwrite)
            {
                var blobExists = await blobClient.ExistsAsync().ConfigureAwait(false);
                if (blobExists)
                {
                    throw new ExistingBlobException();
                }
            }

            stream.Position = 0;
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = MimeUtility.GetMimeMapping(path) }).ConfigureAwait(false);
        }

        public async Task<Stream> ReadAsStreamAsync(string path)
        {
            var blobClient = await GetBlobClientAsync(path).ConfigureAwait(false);

            var blobExists = await blobClient.ExistsAsync().ConfigureAwait(false);
            if (!blobExists)
            {
                return null;
            }

            var stream = new MemoryStream();
            await blobClient.DownloadToAsync(stream).ConfigureAwait(false);
            stream.Position = 0;

            return stream;
        }

        public async Task<bool> BlobExistsAsync(string path)
        {
            var blobClient = await GetBlobClientAsync(path).ConfigureAwait(false);

            var blobExists = await blobClient.ExistsAsync().ConfigureAwait(false);
            return blobExists;
        }

        public async IAsyncEnumerable<string> EnumerateAsync(string prefix = null, params string[] extensions)
        {
            var (containerName, pathPrefix) = ExtractContainerBlobName(prefix);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            var list = blobContainerClient.GetBlobsAsync(prefix: pathPrefix).AsPages().ConfigureAwait(false);
            await foreach (var blobPage in list)
            {
                foreach (var blob in blobPage.Values.Where(b => !b.Deleted &&
                    ((!extensions?.Any() ?? true) || extensions.Any(e => string.Equals(Path.GetExtension(b.Name), e, StringComparison.InvariantCultureIgnoreCase)))))
                {
                    yield return blob.Name;
                }
            }
        }

        public async Task DeleteAsync(string path)
        {
            var (containerName, blobName) = ExtractContainerBlobName(path);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            await blobContainerClient.DeleteBlobIfExistsAsync(blobName).ConfigureAwait(false);
        }

        private async Task<BlobClient> GetBlobClientAsync(string path, bool createIfNotExists = false)
        {
            var (containerName, blobName) = ExtractContainerBlobName(path);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            if (createIfNotExists)
            {
                await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.None).ConfigureAwait(false);
            }

            var blobClient = blobContainerClient.GetBlobClient(blobName);
            return blobClient;
        }

        private (string ContainerName, string BlobName) ExtractContainerBlobName(string path)
        {
            path = path?.Replace(@"\", "/");

            // If a container name as been provided in the settings, use it.
            // Otherwise, extract the first folder name from the path.
            if (!string.IsNullOrWhiteSpace(settings.ContainerName))
            {
                return (settings.ContainerName.ToLowerInvariant(), path);
            }

            var root = Path.GetPathRoot(path);
            var fileNameWithoutRoot = path[root.Length..];
            var parts = fileNameWithoutRoot.Split('/');

            var containerName = parts.First().ToLowerInvariant();
            var blobName = string.Join('/', parts.Skip(1));

            return (containerName, blobName);
        }
    }
}
